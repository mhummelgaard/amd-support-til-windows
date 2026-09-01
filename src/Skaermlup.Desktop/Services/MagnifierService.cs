using System.Windows.Threading;
using Skaermlup.Core.Colors;
using Skaermlup.Core.Geometry;
using Skaermlup.Core.Input;
using Skaermlup.Core.Magnification;
using Skaermlup.Core.Settings;
using Skaermlup.Desktop.Capture;
using Skaermlup.Desktop.Interop;
using Skaermlup.Desktop.Views;

namespace Skaermlup.Desktop.Services;

/// <summary>
/// Motoren bag luppen: henter skærmudsnittet, farvebehandler det og viser det — mange gange
/// i sekundet, så billedet følger musen uden at hakke.
/// </summary>
internal sealed class MagnifierService : IDisposable
{
    /// <summary>Billedtakt når musen står stille. Mere end det er spildt arbejde.</summary>
    private const int IdleFps = 20;

    private readonly AppSettings _settings;
    private readonly ScreenCapture _capture = new();
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render);

    private FollowResolver _follow = new();
    private LensWindow? _lens;
    private ColorMap _colorMap = ColorMap.Identity;
    private PixelPoint _lastCursor = new(int.MinValue, int.MinValue);
    private PixelSize _lastSourceSize = new(200, 150);
    private bool _disposed;

    public MagnifierService(AppSettings settings)
    {
        _settings = settings;
        RebuildColorMap();

        _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / 60);
        _timer.Tick += OnTick;
    }

    /// <summary>Rejses når luppen tændes, slukkes eller ændrer sig, så vejledningen kan følge med.</summary>
    public event EventHandler? Changed;

    public bool IsActive { get; private set; }

    /// <summary>Skærmen luppen arbejder på lige nu.</summary>
    public PixelRect ActiveMonitor { get; private set; }

    /// <summary>Falsk hvis Windows ikke kunne skjule luppen for skærmkopiering.</summary>
    public bool HiddenFromCapture => _lens?.HiddenFromCapture ?? true;

    /// <summary>Kun til fejlsøgning: selve lupvinduet, så der kan gemmes et prøvebillede af det.</summary>
    internal System.Windows.Window? LensWindow => _lens;

    /// <summary>
    /// Bygger lupvinduet på forhånd. Så er der ingen ventetid første gang brugeren trykker,
    /// og vi ved allerede ved opstart om Windows kan skjule luppen for sig selv.
    /// </summary>
    public void Prepare()
    {
        if (_disposed) return;

        _lens ??= new LensWindow();
        _lens.EnsureCreated();
    }

    public void Toggle()
    {
        if (IsActive) TurnOff(); else TurnOn();
    }

    public void TurnOn()
    {
        if (IsActive || _disposed) return;

        // Ny sporing hver gang, så luppen starter dér hvor musen er — ikke hvor den
        // stod sidst luppen var tændt.
        _follow = new FollowResolver { FollowCaret = _settings.FollowCaret };

        _lens ??= new LensWindow();
        _lens.Show();
        _lens.EnsureClickThrough();

        IsActive = true;

        _timer.Start();

        RenderOnce();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void TurnOff()
    {
        if (!IsActive) return;

        _timer.Stop();
        IsActive = false;
        _lens?.Hide();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Kaldes når brugeren har ændret en indstilling.</summary>
    public void ApplySettings()
    {
        RebuildColorMap();
        _follow.FollowCaret = _settings.FollowCaret;
        if (IsActive) RenderOnce();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ZoomIn() => SetZoom(ZoomLadder.Next(_settings.Zoom));

    public void ZoomOut() => SetZoom(ZoomLadder.Previous(_settings.Zoom));

    private void SetZoom(double zoom)
    {
        if (Math.Abs(zoom - _settings.Zoom) < 1e-9) return;

        _settings.Zoom = zoom;
        ApplySettings();
    }

    public void ResizeLens(int deltaPercent)
    {
        if (_settings.Mode == ViewMode.Dock)
        {
            int height = Math.Clamp(_settings.DockHeightPercent + deltaPercent, 10, 90);
            if (height == _settings.DockHeightPercent) return;
            _settings.DockHeightPercent = height;
        }
        else
        {
            var (width, height) = LensSizer.Resize(_settings.LensWidthPercent, _settings.LensHeightPercent, deltaPercent);
            if (width == _settings.LensWidthPercent && height == _settings.LensHeightPercent) return;
            _settings.LensWidthPercent = width;
            _settings.LensHeightPercent = height;
        }

        ApplySettings();
    }

    public void NextColors()
    {
        _settings.Colors = ColorSchemes.Next(_settings.Colors);
        ApplySettings();
    }

    public void ToggleViewMode()
    {
        _settings.Mode = _settings.Mode == ViewMode.Lens ? ViewMode.Dock : ViewMode.Lens;
        ApplySettings();
    }

    /// <summary>Flytter luppen med tastaturet, for dem der ikke bruger mus.</summary>
    public void Pan(int stepsX, int stepsY)
    {
        if (!IsActive) TurnOn();

        int stepX = Math.Max(16, _lastSourceSize.Width / 4);
        int stepY = Math.Max(16, _lastSourceSize.Height / 4);
        var bounds = ActiveMonitor.IsEmpty ? ScreenInfo.VirtualScreen() : ActiveMonitor;

        _follow.PanBy(stepsX * stepX, stepsY * stepY, bounds);
        RenderOnce();
    }

    private void RebuildColorMap()
        => _colorMap = ColorMap.Build(_settings.Colors, _settings.Contrast, _settings.Brightness);

    /// <summary>
    /// Billedtakten. Står musen stille, er der sjældent noget nyt at vise, og så skrues der
    /// ned — det sparer både strøm og blæserstøj uden at kunne mærkes.
    /// </summary>
    private void OnTick(object? sender, EventArgs e)
    {
        int fps = Math.Clamp(_settings.MaxFps, 15, 120);

        var cursor = ScreenInfo.CursorPosition();
        if (cursor == _lastCursor) fps = Math.Min(fps, IdleFps);
        _lastCursor = cursor;

        var interval = TimeSpan.FromMilliseconds(1000.0 / fps);
        if (Math.Abs(_timer.Interval.TotalMilliseconds - interval.TotalMilliseconds) > 0.5)
            _timer.Interval = interval;

        RenderOnce();
    }

    private void RenderOnce()
    {
        if (!IsActive || _lens is null) return;

        var caret = _settings.FollowCaret ? CaretProbe.TryGetCaretRect() : null;
        var focus = _follow.Resolve(ScreenInfo.CursorPosition(), caret);
        var monitor = ScreenInfo.MonitorAt(focus);
        ActiveMonitor = monitor;

        var frame = _settings.Mode == ViewMode.Dock
            ? MagnifierGeometry.ComputeDockFrame(focus, monitor, _settings.DockEdge, _settings.DockHeightPercent, _settings.Zoom)
            : MagnifierGeometry.ComputeLensFrame(focus, monitor,
                MagnifierGeometry.LensSizeFor(monitor, _settings.LensWidthPercent, _settings.LensHeightPercent, _settings.Shape),
                _settings.Zoom);

        if (frame.IsEmpty) return;
        _lastSourceSize = frame.Source.Size;

        if (!_capture.Capture(frame.Source)) return;

        _lens.Present(frame, _capture, _colorMap, _settings);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        TurnOff();
        _timer.Tick -= OnTick;
        _lens?.Close();
        _lens = null;
        _capture.Dispose();
    }
}
