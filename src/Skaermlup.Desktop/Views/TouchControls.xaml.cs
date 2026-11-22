using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using Skaermlup.Core.Geometry;
using Skaermlup.Core.Settings;
using Skaermlup.Core.Text;
using Skaermlup.Desktop.Interop;

namespace Skaermlup.Desktop.Views;

/// <summary>
/// Knapperne til berøringsskærm i skærmkanten: LUP tænder og slukker, plus og minus forstørrer,
/// tandhjulet åbner indstillingerne. Med en finger findes der hverken midterste museknap eller
/// musehjul, så uden dem kunne luppen ikke tændes.
///
/// Vinduet tager aldrig fokus. Et tryk på LUP må ikke flytte skrivemarkøren væk fra det felt,
/// man er i gang med — det er afprøvet med et rigtigt tryk: knappen trykkes, forgrundsvinduet
/// bliver, hvor det var.
/// </summary>
public partial class TouchControls : Window
{
    private const int EdgeMarginPx = 8;

    private IntPtr _handle;
    private PixelRect _appliedBounds;
    private bool? _appliedOn;

    public TouchControls()
    {
        InitializeComponent();

        ZoomInButton.Content = TouchText.ZoomInLabel;
        AutomationProperties.SetName(ZoomInButton, TouchText.ZoomInName);
        ZoomOutButton.Content = TouchText.ZoomOutLabel;
        AutomationProperties.SetName(ZoomOutButton, TouchText.ZoomOutName);
        SettingsButton.Content = TouchText.SettingsLabel;
        AutomationProperties.SetName(SettingsButton, TouchText.SettingsName);
        PowerButton.Content = TouchText.PowerLabel;
    }

    public event EventHandler? PowerPressed;
    public event EventHandler? ZoomInPressed;
    public event EventHandler? ZoomOutPressed;
    public event EventHandler? SettingsPressed;

    /// <summary>Vinduets håndtag, så luppen kan lægge sig lige under knapperne.</summary>
    public IntPtr Handle => _handle;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;

        // Ingen fokus, intet ikon på proceslinjen. Men heller ikke gennemsigtigt for klik —
        // det er jo netop knapper.
        long ex = NativeMethods.GetWindowStyle(_handle, NativeMethods.GWL_EXSTYLE).ToInt64();
        ex |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowStyle(_handle, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));

        HwndSource.FromHwnd(_handle)?.AddHook(OnWindowMessage);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg is NativeMethods.WM_DPICHANGED or NativeMethods.WM_DISPLAYCHANGE) _appliedBounds = PixelRect.Empty;
        return IntPtr.Zero;
    }

    /// <summary>Viser om luppen er tændt, og lægger knapperne ved den rigtige kant.</summary>
    public void Refresh(bool magnifierOn, ScreenSide side, PixelRect workArea)
    {
        if (workArea.IsEmpty) return;

        if (_appliedOn != magnifierOn)
        {
            // Tændt: LUP står gul med sort skrift, så det kan ses på afstand. Plus og minus
            // giver kun mening, når der er en lup at forstørre med.
            PowerButton.Background = magnifierOn ? Brush(0xFF, 0xD5, 0x4F) : Brush(0x16, 0x16, 0x16);
            PowerButton.Foreground = magnifierOn ? Brushes.Black : Brush(0xFF, 0xD5, 0x4F);
            AutomationProperties.SetName(PowerButton, TouchText.PowerName(magnifierOn));

            var visible = magnifierOn ? Visibility.Visible : Visibility.Collapsed;
            ZoomInButton.Visibility = visible;
            ZoomOutButton.Visibility = visible;

            _appliedOn = magnifierOn;
            _appliedBounds = PixelRect.Empty;
        }

        Position(side, workArea);
    }

    private void Position(ScreenSide side, PixelRect workArea)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1;
        double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1;

        Root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        int width = (int)Math.Ceiling(Root.DesiredSize.Width * scaleX);
        int height = Math.Min((int)Math.Ceiling(Root.DesiredSize.Height * scaleY), workArea.Height);

        int x = side == ScreenSide.Left ? workArea.X + EdgeMarginPx : workArea.Right - width - EdgeMarginPx;
        int y = workArea.Y + (workArea.Height - height) / 2;
        var bounds = new PixelRect(x, y, width, height);

        if (bounds == _appliedBounds) return;
        OverlayWindows.SetBounds(_handle, bounds, show: true);
        _appliedBounds = bounds;
    }

    /// <summary>Lægger knapperne øverst igen, hvis et andet program har lagt sig ovenpå.</summary>
    public void Reassert() => OverlayWindows.Reassert(_handle);

    private static SolidColorBrush Brush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private void OnPower(object sender, RoutedEventArgs e) => PowerPressed?.Invoke(this, EventArgs.Empty);
    private void OnZoomIn(object sender, RoutedEventArgs e) => ZoomInPressed?.Invoke(this, EventArgs.Empty);
    private void OnZoomOut(object sender, RoutedEventArgs e) => ZoomOutPressed?.Invoke(this, EventArgs.Empty);
    private void OnSettings(object sender, RoutedEventArgs e) => SettingsPressed?.Invoke(this, EventArgs.Empty);
}
