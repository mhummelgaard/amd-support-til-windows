using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Skaermlup.Core.Colors;
using Skaermlup.Core.Geometry;
using Skaermlup.Core.Input;
using Skaermlup.Core.Magnification;
using Skaermlup.Core.Settings;
using Skaermlup.Desktop.Capture;
using Skaermlup.Desktop.Interop;

namespace Skaermlup.Desktop.Views;

internal enum TouchDragPhase
{
    Started,
    Moved,
    Ended,
}

/// <summary>En finger på luppen, i skærmpixels.</summary>
internal sealed class TouchDragEventArgs(TouchDragPhase phase, PixelPoint point) : EventArgs
{
    public TouchDragPhase Phase { get; } = phase;
    public PixelPoint Point { get; } = point;
}

/// <summary>Selve luppen: et gennemsigtigt-for-mus lag der viser det forstørrede skærmudsnit.</summary>
public partial class LensWindow : Window
{
    // "Tryk og hold" bliver ellers til et højreklik, med en ring der vokser frem under fingeren,
    // når man holder luppen stille for at læse. Flicks er pennens bevægelser — de skal heller ikke
    // gøre noget her. Microsofts egne konstanter.
    private const string TabletProperty = "MicrosoftTabletPenServiceProperty";
    private const int TabletDisablePressAndHold = 0x00000001;
    private const int TabletDisableFlicks = 0x00010000;

    private bool _touchDragging;

    /// <summary>
    /// En finger trækker i luppen. Windows giver fingeren videre som museklik, mærket med hvor
    /// de kom fra; kun dem fra en finger bruges. Musen går gennem hullet som altid.
    /// </summary>
    internal event EventHandler<TouchDragEventArgs>? TouchDrag;

    /// <summary>
    /// Hullets radius i pixels. Stort nok til at musen rammer det, selv når luppen er et par
    /// billeder bagud under en hurtig bevægelse, og lille nok til at musepilen dækker det.
    /// </summary>
    private const int HoleRadius = 7;

    private readonly EdgeUpscaler _upscaler = new();
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    private PixelPoint _lastMarker;
    private long _lastMarkerAt;
    private double _smoothedSpeed;
    private bool _markerIsLight;
    private bool _markerColoursApplied;

    private WriteableBitmap? _bitmap;
    private byte[] _sourceBuffer = [];
    private byte[] _frameBuffer = [];
    private PixelRect _appliedBounds;
    private LensShape _appliedShape = (LensShape)(-1);
    private PixelSize _appliedShapeSize;
    private PixelPoint _appliedHole = new(int.MinValue, int.MinValue);
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

    /// <summary>
    /// Sætter lag-typografien på plads igen. WPF rydder den væk når vinduet vises, og uden
    /// den kan brugeren ikke klikke gennem luppen.
    /// </summary>
    public void EnsureClickThrough() => OverlayWindows.Reassert(_handle);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;
        HiddenFromCapture = OverlayWindows.MakeOverlay(_handle);
        NativeMethods.SetPropW(_handle, TabletProperty, new IntPtr(TabletDisablePressAndHold | TabletDisableFlicks));
        HwndSource.FromHwnd(_handle)?.AddHook(OnWindowMessage);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Flytter luppen til en skærm med anden skalering, retter Windows selv i vinduet.
        // Placeringen sættes derfor forfra ved næste billede.
        if (msg is NativeMethods.WM_DPICHANGED or NativeMethods.WM_DISPLAYCHANGE)
            _appliedBounds = PixelRect.Empty;

        switch (msg)
        {
            case NativeMethods.WM_LBUTTONDOWN when FromTouch():
                // Hold fast i fingeren, også hvis den løber fra luppen. Ellers ville resten af
                // trækket ende i programmet nedenunder — og fx markere tekst eller rulle siden.
                NativeMethods.SetCapture(_handle);
                _touchDragging = true;
                RaiseTouchDrag(TouchDragPhase.Started);
                handled = true;
                break;

            case NativeMethods.WM_MOUSEMOVE when _touchDragging:
                RaiseTouchDrag(TouchDragPhase.Moved);
                handled = true;
                break;

            case NativeMethods.WM_LBUTTONUP when _touchDragging:
                _touchDragging = false;
                NativeMethods.ReleaseCapture();
                RaiseTouchDrag(TouchDragPhase.Ended);
                handled = true;
                break;

            case NativeMethods.WM_CAPTURECHANGED when _touchDragging:
                // Noget andet tog fingeren — fx Windows, der viste en dialog.
                _touchDragging = false;
                RaiseTouchDrag(TouchDragPhase.Ended);
                break;
        }

        return IntPtr.Zero;
    }

    private static bool FromTouch() => InputSignature.IsTouch((ulong)NativeMethods.GetMessageExtraInfo());

    /// <summary>
    /// Positionen tages fra beskeden selv og ikke fra vinduet. Luppen flytter sig under fingeren,
    /// og regnede man ud fra vinduets plads, ville hver flytning give et nyt ryk.
    /// </summary>
    private void RaiseTouchDrag(TouchDragPhase phase)
    {
        uint pos = NativeMethods.GetMessagePos();
        var point = new PixelPoint((short)(pos & 0xFFFF), (short)((pos >> 16) & 0xFFFF));
        TouchDrag?.Invoke(this, new TouchDragEventArgs(phase, point));
    }

    internal void Present(MagnifierFrame frame, ScreenCapture capture, ColorMap map, AppSettings settings)
    {
        if (frame.IsEmpty || capture.Width <= 0) return;

        // Billedet opdateres før vinduet flyttes. Omvendt ville luppen i et øjeblik vise
        // det forrige billede på den nye plads, og det ses som et ryk.
        ApplyChrome(settings);
        WritePixels(frame, capture, map, settings);
        UpdateMarker(frame, capture, settings);
        ApplyBounds(frame, settings);
    }

    private void ApplyBounds(MagnifierFrame frame, AppSettings settings)
    {
        var view = frame.View;
        bool moved = view != _appliedBounds;

        var shape = settings.Mode == ViewMode.Dock ? LensShape.Rectangle : settings.Shape;
        var hole = frame.HotSpot;

        // Hullet er til musen, og kun når det er musen, luppen står ved. Er luppen trukket på plads
        // med en finger, står musen et andet sted, og et hul midt i luppen ville blot lade en
        // finger falde igennem til programmet nedenunder.
        int radius = HoleAtFocus ? HoleRadius : 0;

        // Hullet flytter sig kun når luppen bliver klemt op mod en skærmkant. Ellers står
        // musen altid det samme sted i vinduet, og formen skal ikke sættes igen.
        if (shape != _appliedShape || view.Size != _appliedShapeSize || hole != _appliedHole || radius != _appliedHoleRadius)
        {
            if (settings.Mode == ViewMode.Dock)
                OverlayWindows.ApplyBandShape(_handle, view.Width, view.Height, hole, radius);
            else
                OverlayWindows.ApplyShape(_handle, view.Width, view.Height, shape, 14, hole, radius);

            _appliedShape = shape;
            _appliedShapeSize = view.Size;
            _appliedHole = hole;
            _appliedHoleRadius = radius;
        }

        if (moved)
        {
            OverlayWindows.SetBounds(_handle, view, show: true, KeepBelow);
            _appliedBounds = view;
            _framesSinceReassert = 0;
        }
        else if (++_framesSinceReassert > 30)
        {
            // Et andet program kan have lagt sig øverst. Kravl op igen med jævne mellemrum.
            OverlayWindows.Reassert(_handle, KeepBelow);
            _framesSinceReassert = 0;
        }
    }

    /// <summary>Om der skal være hul til musen, dér hvor luppen står. Se <see cref="ApplyBounds"/>.</summary>
    internal bool HoleAtFocus { get; set; } = true;

    /// <summary>
    /// Et vindue luppen skal ligge lige under — touch-knapperne. Ellers ville luppen kunne lægge
    /// sig oven på dem, og et tryk på LUP blev til et træk i luppen.
    /// </summary>
    internal IntPtr KeepBelow { get; set; }

    private int _appliedHoleRadius = -1;

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

    private void UpdateMarker(MagnifierFrame frame, ScreenCapture capture, AppSettings settings)
    {
        if (settings.Marker == MarkerStyle.None)
        {
            if (Overlay.Visibility != Visibility.Collapsed) Overlay.Visibility = Visibility.Collapsed;
            return;
        }

        if (Overlay.Visibility != Visibility.Visible) Overlay.Visibility = Visibility.Visible;

        double scale = Math.Clamp(settings.MarkerSize, 50, 250) / 100.0;
        int thickness = (int)Math.Round(11 * scale);
        int ringRadius = (int)Math.Round(26 * scale);
        int baseGap = (int)Math.Round(30 * scale);

        var layout = MarkerGeometry.Compute(
            frame.View.Size, frame.Marker, settings.Marker,
            MarkerGeometry.GapFor(baseGap, MeasureSpeed(frame.Marker)),
            thickness, ringRadius, UnderlineDrop(frame));

        ApplyMarkerColours(capture);

        double scaleX = GetDpiScaleX();
        double scaleY = GetDpiScaleY();

        Place(CrossLeft, layout.Left, scaleX, scaleY);
        Place(CrossRight, layout.Right, scaleX, scaleY);
        Place(CrossTop, layout.Top, scaleX, scaleY);
        Place(CrossBottom, layout.Bottom, scaleX, scaleY);
        Place(RingOuter, layout.Ring, scaleX, scaleY);
        Place(RingInner, layout.Ring, scaleX, scaleY);
    }

    /// <summary>
    /// Hvor langt under punktet understregningen lægges. Cirka ni pixels af den oprindelige
    /// skærm svarer til underkanten af en almindelig tekstlinje, og ganget op med forstørrelsen
    /// rammer stregen derfor under linjen uanset hvor meget der forstørres.
    /// </summary>
    private static int UnderlineDrop(MagnifierFrame frame) => (int)Math.Round(9 * frame.ScaleY);

    /// <summary>Fokuspunktets fart i pixels pr. sekund, brugt til at åbne hullet under flytning.</summary>
    private double MeasureSpeed(PixelPoint marker)
    {
        long now = _clock.ElapsedMilliseconds;
        long elapsed = now - _lastMarkerAt;
        _lastMarkerAt = now;

        var previous = _lastMarker;
        _lastMarker = marker;

        if (elapsed <= 0 || elapsed > 400) return 0;

        double distance = Math.Sqrt(previous.DistanceSquaredTo(marker));
        double speed = distance * 1000.0 / elapsed;

        // Farten glattes ud, så hullet ikke hopper i takt med de enkelte billeder.
        _smoothedSpeed = _smoothedSpeed * 0.7 + speed * 0.3;
        return _smoothedSpeed;
    }

    /// <summary>
    /// Markeringen skal kunne ses uanset hvad den ligger oven på. Farven vælges derfor efter
    /// hvor lyst det forstørrede billede er, og hver streg har en kant i den modsatte farve.
    /// </summary>
    private void ApplyMarkerColours(ScreenCapture capture)
    {
        bool light = MarkerContrast.PrefersLightMarker(
            capture.Pixels, capture.Stride, capture.Width, capture.Height, _markerIsLight);

        if (light == _markerIsLight && _markerColoursApplied) return;

        _markerIsLight = light;
        _markerColoursApplied = true;

        var core = light ? Brushes.White : Brushes.Black;
        var casing = light ? Brushes.Black : Brushes.White;

        foreach (var bar in new[] { CrossLeft, CrossRight, CrossTop, CrossBottom })
        {
            bar.Fill = core;
            bar.Stroke = casing;
        }

        RingOuter.Stroke = casing;
        RingInner.Stroke = core;
    }

    private static void Place(System.Windows.Shapes.Shape element, PixelRect rect, double scaleX, double scaleY)
    {
        if (rect.IsEmpty)
        {
            element.Visibility = Visibility.Collapsed;
            return;
        }

        System.Windows.Controls.Canvas.SetLeft(element, rect.X / scaleX);
        System.Windows.Controls.Canvas.SetTop(element, rect.Y / scaleY);
        element.Width = rect.Width / scaleX;
        element.Height = rect.Height / scaleY;
        element.Visibility = Visibility.Visible;
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
