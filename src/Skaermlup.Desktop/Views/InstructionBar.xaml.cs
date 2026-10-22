using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Skaermlup.Core.Geometry;
using Skaermlup.Core.Settings;
using Skaermlup.Core.Text;
using Skaermlup.Desktop.Interop;

namespace Skaermlup.Desktop.Views;

/// <summary>
/// Den store vejledningsbjælke langs skærmkanten. Den fortæller altid hvad luppen laver
/// lige nu, og hvilken knap der ændrer det.
/// </summary>
public partial class InstructionBar : Window
{
    private IntPtr _handle;
    private PixelRect _appliedBounds;
    private PixelRect _monitor;
    private string _signature = string.Empty;

    public InstructionBar()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;
        OverlayWindows.MakeOverlay(_handle);
        HwndSource.FromHwnd(_handle)?.AddHook(OnWindowMessage);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg is NativeMethods.WM_DPICHANGED or NativeMethods.WM_DISPLAYCHANGE)
            ForgetPlacement();

        return IntPtr.Zero;
    }

    /// <summary>Opdaterer teksten og lægger bjælken langs den rigtige kant af den rigtige skærm.</summary>
    public void Refresh(bool magnifierOn, AppSettings settings, PixelRect monitor, bool mouseWorks)
    {
        if (monitor.IsEmpty) return;

        var headline = Instructions.Headline(magnifierOn, settings);
        var callToAction = Instructions.CallToAction(magnifierOn, settings, mouseWorks);
        var chips = Instructions.Bar(magnifierOn, settings, mouseWorks);
        var edge = PlacementFor(magnifierOn, settings);

        // Bjælken bliver spurgt flere gange i sekundet. Er teksten den samme som sidst,
        // skal den ikke bygges op igen — det ville både koste kræfter og få den til at blinke.
        string signature = string.Join("|",
            headline, callToAction, edge, settings.InstructionScale,
            string.Join(";", chips.Select(c => c.Action + "=" + c.Meaning)));

        if (signature == _signature && monitor == _monitor) return;

        _signature = signature;
        _monitor = monitor;

        double scale = Math.Clamp(settings.InstructionScale, 75, 250) / 100.0;
        Scale.ScaleX = scale;
        Scale.ScaleY = scale;

        Headline.Text = headline;
        CallToAction.Text = callToAction;
        Chips.ItemsSource = chips;

        Root.BorderThickness = edge == InstructionPlacement.Bottom
            ? new Thickness(0, 5, 0, 0)
            : new Thickness(0, 0, 0, 5);

        Position(monitor, edge);
    }

    /// <summary>
    /// Læsebåndet og vejledningen må ikke ligge oven i hinanden. Ligger de på samme kant,
    /// flytter vejledningen sig til den modsatte side af skærmen.
    /// </summary>
    private static InstructionPlacement PlacementFor(bool magnifierOn, AppSettings settings)
    {
        var wanted = settings.InstructionsAt;
        if (!magnifierOn || settings.Mode != ViewMode.Dock) return wanted;

        bool collides = (settings.DockEdge == DockEdge.Top && wanted == InstructionPlacement.Top)
                     || (settings.DockEdge == DockEdge.Bottom && wanted == InstructionPlacement.Bottom);

        if (!collides) return wanted;

        return wanted == InstructionPlacement.Top ? InstructionPlacement.Bottom : InstructionPlacement.Top;
    }

    private void Position(PixelRect monitor, InstructionPlacement placement)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1;
        double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1;

        Root.Measure(new Size(monitor.Width / scaleX, double.PositiveInfinity));
        int height = (int)Math.Ceiling(Root.DesiredSize.Height * scaleY);
        height = Math.Clamp(height, 90, Math.Max(90, monitor.Height / 2));

        int top = placement == InstructionPlacement.Top ? monitor.Y : monitor.Bottom - height;
        var bounds = new PixelRect(monitor.X, top, monitor.Width, height);

        if (bounds == _appliedBounds) return;

        OverlayWindows.SetBounds(_handle, bounds, show: true);
        _appliedBounds = bounds;
    }

    public void Reassert() => OverlayWindows.Reassert(_handle);

    /// <summary>Glemmer hvor bjælken stod, så den tegnes og placeres forfra.</summary>
    public void ForgetPlacement()
    {
        _appliedBounds = PixelRect.Empty;
        _monitor = PixelRect.Empty;
        _signature = string.Empty;
    }
}
