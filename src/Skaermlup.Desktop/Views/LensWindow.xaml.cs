using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Skaermlup.Core.Colors;
using Skaermlup.Core.Geometry;
using Skaermlup.Core.Magnification;
using Skaermlup.Core.Settings;
using Skaermlup.Desktop.Capture;
using Skaermlup.Desktop.Interop;

namespace Skaermlup.Desktop.Views;

/// <summary>Selve luppen: et gennemsigtigt-for-mus lag der viser det forstørrede skærmudsnit.</summary>
public partial class LensWindow : Window
{
    private const double RingRadiusDip = 26;

    private readonly EdgeUpscaler _upscaler = new();

    private WriteableBitmap? _bitmap;
    private byte[] _sourceBuffer = [];
    private byte[] _frameBuffer = [];
    private PixelRect _appliedBounds;
    private LensShape _appliedShape = (LensShape)(-1);
    private PixelSize _appliedShapeSize;
    private BitmapScalingMode _appliedScaling = BitmapScalingMode.Unspecified;
    private double _appliedBorder = -1;
    private IntPtr _handle;
    private int _framesSinceReassert;

    public LensWindow()
    {
        InitializeComponent();
    }

    /// <summary>Falsk hvis Windows ikke kunne skjule luppen for skærmkopiering.</summary>
    public bool HiddenFromCapture { get; private set; } = true;

    /// <summary>
    /// Opretter vinduet uden at vise det, så det er klart i det sekund brugeren trykker
    /// på museknappen — og så vi allerede ved opstart ved om Windows kan skjule det.
    /// </summary>
    public void EnsureCreated() => new WindowInteropHelper(this).EnsureHandle();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;
        HiddenFromCapture = OverlayWindows.MakeOverlay(_handle);
        HwndSource.FromHwnd(_handle)?.AddHook(OnWindowMessage);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Flytter luppen til en skærm med anden skalering, retter Windows selv i vinduet.
        // Placeringen sættes derfor forfra ved næste billede.
        if (msg is NativeMethods.WM_DPICHANGED or NativeMethods.WM_DISPLAYCHANGE)
            _appliedBounds = PixelRect.Empty;

        return IntPtr.Zero;
    }

    internal void Present(MagnifierFrame frame, ScreenCapture capture, ColorMap map, AppSettings settings)
    {
        if (frame.IsEmpty || capture.Width <= 0) return;

        ApplyBounds(frame.View, settings);
        ApplyChrome(settings);
        WritePixels(frame, capture, map, settings);
        UpdateMarker(frame, settings);
    }

    private void ApplyBounds(PixelRect view, AppSettings settings)
    {
        bool moved = view != _appliedBounds;
        if (moved)
        {
            OverlayWindows.SetBounds(_handle, view, show: true);
            _appliedBounds = view;
            _framesSinceReassert = 0;
        }
        else if (++_framesSinceReassert > 120)
        {
            // Et andet program kan have lagt sig øverst. Kravl op igen med jævne mellemrum.
            OverlayWindows.Reassert(_handle);
            _framesSinceReassert = 0;
        }

        var shape = settings.Mode == ViewMode.Dock ? LensShape.Rectangle : settings.Shape;
        if (moved || shape != _appliedShape || view.Size != _appliedShapeSize)
        {
            if (settings.Mode == ViewMode.Dock)
                OverlayWindows.ClearShape(_handle);
            else
                OverlayWindows.ApplyShape(_handle, view.Width, view.Height, shape, 14);

            _appliedShape = shape;
            _appliedShapeSize = view.Size;
        }
    }

    private void ApplyChrome(AppSettings settings)
    {
        var scaling = settings.SmoothImage ? BitmapScalingMode.HighQuality : BitmapScalingMode.NearestNeighbor;
        if (scaling != _appliedScaling)
        {
            RenderOptions.SetBitmapScalingMode(Surface, scaling);
            _appliedScaling = scaling;
        }

        bool circle = settings.Mode == ViewMode.Lens && settings.Shape == LensShape.Circle;
        double thickness = settings.BorderThickness / GetDpiScaleX();

        if (Math.Abs(thickness - _appliedBorder) > 0.01 || (FrameCircle.Visibility == Visibility.Visible) != circle)
        {
            FrameRect.Visibility = circle ? Visibility.Collapsed : Visibility.Visible;
            FrameCircle.Visibility = circle ? Visibility.Visible : Visibility.Collapsed;
            FrameRect.BorderThickness = new Thickness(thickness);
            FrameCircle.StrokeThickness = thickness;
            _appliedBorder = thickness;
        }
    }

    /// <summary>
    /// Ved lav forstørrelse er det billigst at sende det lille udsnit direkte til
    /// grafikkortet og lade det skalere op. Ved høj forstørrelse bliver den opskalering
    /// udflydende, og så regnes billedet i stedet ud i fuld størrelse med kantstyret
    /// forstørrelse, så bogstaverne bliver skarpe.
    /// </summary>
    private void WritePixels(MagnifierFrame frame, ScreenCapture capture, ColorMap map, AppSettings settings)
    {
        double zoom = Math.Min(frame.ScaleX, frame.ScaleY);

        if (settings.SmoothImage && EdgeUpscaler.IsWorthwhile(zoom))
            WriteSharpened(frame, capture, map, zoom);
        else
            WriteDirect(capture, map);
    }

    private void WriteDirect(ScreenCapture capture, ColorMap map)
    {
        int width = capture.Width;
        int height = capture.Height;
        var bitmap = EnsureBitmap(width, height);

        bitmap.Lock();
        try
        {
            unsafe
            {
                var destination = new Span<byte>((void*)bitmap.BackBuffer, bitmap.BackBufferStride * height);
                ColorPipeline.Apply(capture.Pixels, capture.Stride, destination, bitmap.BackBufferStride,
                    width, height, map);
            }

            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            bitmap.Unlock();
        }
    }

    private void WriteSharpened(MagnifierFrame frame, ScreenCapture capture, ColorMap map, double zoom)
    {
        int sourceWidth = capture.Width;
        int sourceHeight = capture.Height;
        int sourceStride = sourceWidth * 4;

        // Farvebehandlingen sker på det lille udsnit, altså på en brøkdel af de pixels
        // den ellers skulle røre ved.
        EnsureBuffer(ref _sourceBuffer, sourceHeight * sourceStride);
        ColorPipeline.Apply(capture.Pixels, capture.Stride, _sourceBuffer, sourceStride,
            sourceWidth, sourceHeight, map);

        // ClearTypes farvede kanter hjælper kun ved skærmens egen opløsning. Forstørret
        // bliver de til røde og blå bræmmer langs bogstaverne.
        SubpixelFringe.Remove(_sourceBuffer, sourceStride, sourceWidth, sourceHeight);

        int width = frame.View.Width;
        int height = frame.View.Height;
        int stride = width * 4;
        var bitmap = EnsureBitmap(width, height);

        EnsureBuffer(ref _frameBuffer, height * stride);
        _upscaler.Upscale(_sourceBuffer, sourceStride, sourceWidth, sourceHeight,
            _frameBuffer, stride, width, height, EdgeUpscaler.SteepnessFor(zoom));

        bitmap.WritePixels(new Int32Rect(0, 0, width, height), _frameBuffer, stride, 0);
    }

    private WriteableBitmap EnsureBitmap(int width, int height)
    {
        if (_bitmap is null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
        {
            _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            Surface.Source = _bitmap;
        }

        return _bitmap;
    }

    private static void EnsureBuffer(ref byte[] buffer, int size)
    {
        if (buffer.Length < size) buffer = new byte[size];
    }

    private void UpdateMarker(MagnifierFrame frame, AppSettings settings)
    {
        if (!settings.ShowCrosshair)
        {
            if (Overlay.Visibility != Visibility.Collapsed) Overlay.Visibility = Visibility.Collapsed;
            return;
        }

        if (Overlay.Visibility != Visibility.Visible) Overlay.Visibility = Visibility.Visible;

        double scaleX = GetDpiScaleX();
        double scaleY = GetDpiScaleY();
        double width = frame.View.Width / scaleX;
        double height = frame.View.Height / scaleY;
        double x = frame.Marker.X / scaleX;
        double y = frame.Marker.Y / scaleY;
        const double lineThickness = 11;
        double gap = RingRadiusDip;

        Place(CrossLeft, 0, y - lineThickness / 2, Math.Max(0, x - gap), lineThickness);
        Place(CrossRight, x + gap, y - lineThickness / 2, Math.Max(0, width - (x + gap)), lineThickness);
        Place(CrossTop, x - lineThickness / 2, 0, lineThickness, Math.Max(0, y - gap));
        Place(CrossBottom, x - lineThickness / 2, y + gap, lineThickness, Math.Max(0, height - (y + gap)));

        Place(RingOuter, x - gap, y - gap, gap * 2, gap * 2);
        Place(RingInner, x - gap, y - gap, gap * 2, gap * 2);
    }

    private static void Place(FrameworkElement element, double left, double top, double width, double height)
    {
        System.Windows.Controls.Canvas.SetLeft(element, left);
        System.Windows.Controls.Canvas.SetTop(element, top);
        element.Width = Math.Max(0, width);
        element.Height = Math.Max(0, height);
        element.Visibility = width > 0.5 && height > 0.5 ? Visibility.Visible : Visibility.Collapsed;
    }

    private double GetDpiScaleX()
    {
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        return scale > 0 ? scale : 1;
    }

    private double GetDpiScaleY()
    {
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleY;
        return scale > 0 ? scale : 1;
    }
}
