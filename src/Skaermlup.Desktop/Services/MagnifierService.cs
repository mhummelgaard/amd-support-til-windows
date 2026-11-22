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
    private readonly SystemCursor _cursor = new();
    private readonly FocusTracker _focus = new();
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render);

    /// <summary>
    /// Hvor tit der spørges om Windows' egen skal ligger øverst. Hurtigt nok til at markøren
    /// er der inden brugeren når at lede efter den, langsomt nok til ikke at koste noget.
    /// </summary>
    private static readonly TimeSpan ShellPollInterval = TimeSpan.FromMilliseconds(120);

    private FollowResolver _follow = new();
    private LensWindow? _lens;
    private ColorMap _colorMap = ColorMap.Identity;
    private PixelPoint _lastCursor = new(int.MinValue, int.MinValue);
    private PixelSize _lastSourceSize = new(200, 150);
    private readonly System.Diagnostics.Stopwatch _shellClock = System.Diagnostics.Stopwatch.StartNew();
    private TimeSpan _shellPolledAt = TimeSpan.MinValue;
    private bool _shellInFront;
    private PixelRect _shellBounds;
    private PointerVisual _pointer = PointerVisual.System;
    private CursorRingLayout _appliedRing = CursorRingLayout.None;
    private bool _disposed;

    /// <param name="lastTouchMs">
    /// Hvornår en finger sidst rørte skærmen. Bruges til at holde fingerens tryk ude af musens
    /// plads, så luppen ikke springer hen til hver knap, der trykkes på uden for den.
    /// </param>
    public MagnifierService(AppSettings settings, Func<long>? lastTouchMs = null)
    {
        _settings = settings;
        _lastTouchMs = lastTouchMs ?? (() => 0);
        RebuildColorMap();

        _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / 60);
        _timer.Tick += OnTick;
    }

    private readonly Func<long> _lastTouchMs;
    private PointerFilter _pointerFilter = new();
    private PixelPoint _dragOffset;
    private PixelPoint? _lastTouchFocus;

    /// <summary>Et vindue luppen skal blive under — touch-knapperne, når de vises.</summary>
    public Func<IntPtr>? KeepLensBelow { get; set; }

    private LensWindow EnsureLens()
    {
        if (_lens is null)
        {
            _lens = new LensWindow();
            _lens.TouchDrag += OnTouchDrag;
        }
        return _lens;
    }

    /// <summary>
    /// En finger trækker i luppen. Luppen flytter sig med, og det sted fingeren greb fat, bliver
    /// under fingeren — som når man flytter et forstørrelsesglas hen over papiret.
    /// </summary>
    private void OnTouchDrag(object? sender, TouchDragEventArgs e)
    {
        if (!IsActive) return;

        if (e.Phase == TouchDragPhase.Started)
        {
            _dragOffset = new PixelPoint(e.Point.X - _follow.Current.X, e.Point.Y - _follow.Current.Y);
            return;
        }

        var screen = ScreenInfo.VirtualScreen();
        var focus = new PixelPoint(
            Math.Clamp(e.Point.X - _dragOffset.X, screen.X, screen.Right - 1),
            Math.Clamp(e.Point.Y - _dragOffset.Y, screen.Y, screen.Bottom - 1));

        _follow.PlaceAt(focus, ScreenInfo.CursorPosition());
        _lastTouchFocus = focus;
        RenderOnce();
    }

    /// <summary>
    /// Tænder luppen fra touch-knapperne. Musepilen står ved knappen — dér skal luppen ikke stå.
    /// Den kommer frem, hvor den sidst blev trukket hen, eller midt på skærmen første gang.
    /// </summary>
    public void TurnOnForTouch(PixelPoint fallback) => TurnOn(_lastTouchFocus ?? fallback);

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

        EnsureLens().EnsureCreated();
    }

    public void Toggle()
    {
        if (IsActive) TurnOff(); else TurnOn();
    }

    /// <param name="at">Hvor luppen skal starte. Tom: ved musen.</param>
    public void TurnOn(PixelPoint? at = null)
    {
        if (IsActive || _disposed) return;

        // Ny sporing hver gang, så luppen starter dér hvor musen er — ikke hvor den
        // stod sidst luppen var tændt.
        _follow = new FollowResolver { FollowCaret = _settings.FollowCaret, FollowFocus = _settings.FollowFocus };
        _pointerFilter = new PointerFilter();
        if (at is { } start) _follow.PlaceAt(start, ScreenInfo.CursorPosition());
        if (_settings.FollowFocus) _focus.Start();

        _lens = EnsureLens();
        _lens.Show();
        _lens.EnsureClickThrough();

        IsActive = true;

        // Et gammelt svar om hvad der lå øverst sidst, siger intet om nu.
        ShellWatch.Forget();
        _shellInFront = false;
        _shellBounds = PixelRect.Empty;
        _shellPolledAt = TimeSpan.MinValue;
        UpdateSystemCursor();

        _timer.Start();

        RenderOnce();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void TurnOff()
    {
        if (!IsActive) return;

        _timer.Stop();
        IsActive = false;
        _focus.Stop();
        _lens?.Hide();
        _cursor.Show();
        _pointer = PointerVisual.System;
        _appliedRing = CursorRingLayout.None;
        _shellInFront = false;
        _shellBounds = PixelRect.Empty;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Kaldes når brugeren har ændret en indstilling.</summary>
    public void ApplySettings()
    {
        RebuildColorMap();
        UpdateSystemCursor();
        _follow.FollowCaret = _settings.FollowCaret;
        _follow.FollowFocus = _settings.FollowFocus;
        if (IsActive && _settings.FollowFocus) _focus.Start();
        else _focus.Stop();
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

    /// <summary>
    /// Windows egen musepil skjules mens luppen tegner sin egen markering. To markører oven i
    /// hinanden er forvirrende, og systemets pil peger forkert når luppen er klemt op mod en
    /// skærmkant. Vælger brugeren "ingen markering", er det netop pilen der skal bruges, og
    /// så bliver den stående.
    ///
    /// Ligger Start-menuen eller en anden af Windows' egne flader oven på luppen, gælder det
    /// modsatte: luppen kan ikke ses alligevel, og så er markøren det eneste brugeren har.
    /// Den hentes frem igen — med en kraftig ring om, hvis der er plads til en.
    /// </summary>
    private void UpdateSystemCursor()
    {
        var ring = _settings.ShellPointerRing
            ? CursorRing.Compute(SystemCursor.PointerSize, _settings.ShellPointerRingSize)
            : CursorRingLayout.None;

        var want = PointerRules.Decide(
            IsActive, _settings.Marker, _shellInFront,
            ringWanted: _settings.ShellPointerRing, ringPossible: !ring.IsEmpty);

        if (want == _pointer && (want != PointerVisual.HighlightRing || ring == _appliedRing)) return;

        switch (want)
        {
            case PointerVisual.Hidden:
                if (!_cursor.Hide()) want = PointerVisual.System;
                break;

            case PointerVisual.HighlightRing:
                // Kan ringen ikke tegnes alligevel, er brugerens egen pil det rigtige svar.
                if (!_cursor.Highlight(ring))
                {
                    _cursor.Show();
                    want = PointerVisual.System;
                }
                break;

            default:
                _cursor.Show();
                break;
        }

        _pointer = want;
        _appliedRing = want == PointerVisual.HighlightRing ? ring : CursorRingLayout.None;
    }

    /// <summary>
    /// Flytter lupvinduet ud til siden af Start-menuen i stedet for at lade det forsvinde ind
    /// bag den. Kun visningen flyttes — kilden bliver liggende ved musen, så det er det samme
    /// billede, bare et andet sted på skærmen.
    ///
    /// Kun i lupvisning. Læsebåndet holder med vilje sin kilde uden for båndet, så luppen ikke
    /// kan komme til at forstørre sig selv; flyttede vi båndet, ville den garanti falde.
    /// </summary>
    private MagnifierFrame MoveClearOfShell(MagnifierFrame frame, PixelRect monitor)
    {
        if (!_settings.ShellMoveLensAside) return frame;
        if (_settings.Mode != ViewMode.Lens) return frame;
        if (!_shellInFront || _shellBounds.IsEmpty) return frame;

        var moved = LensDisplacement.MoveClear(frame.View, _shellBounds, monitor);
        return moved == frame.View ? frame : frame with { View = moved };
    }

    /// <summary>
    /// Spørger med jævne mellemrum om Windows' egen skal ligger øverst. Svaret kan skifte uden
    /// at luppen får besked, så der er ikke andet for end at kigge efter.
    /// </summary>
    private void PollShellSurface()
    {
        var now = _shellClock.Elapsed;
        if (_shellPolledAt != TimeSpan.MinValue && now - _shellPolledAt < ShellPollInterval) return;

        _shellPolledAt = now;

        var shell = ShellWatch.Probe();
        _shellInFront = shell.InFront;
        _shellBounds = shell.Bounds;
    }

    /// <summary>Til statusfilen: om ringen om markøren kan lade sig gøre på denne maskine.</summary>
    public string PointerRingStatus
    {
        get
        {
            if (!_settings.ShellPointerRing) return "slået fra";

            int pointer = SystemCursor.PointerSize;
            var ring = CursorRing.Compute(pointer, _settings.ShellPointerRingSize);

            return ring.IsEmpty
                ? $"NEJ — markørstørrelsen er {pointer} px, og der er ikke plads til en ring. "
                  + "Skru markøren op eller ned under Indstillinger → Tilgængelighed → Musemarkør og berøring."
                : $"ja ({ring.Canvas} px om en markør på {pointer} px)";
        }
    }

    private void RebuildColorMap()
        => _colorMap = ColorMap.Build(_settings.Colors, _settings.Contrast, _settings.Brightness,
                                      _settings.YellowTone);

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

        PollShellSurface();
        UpdateSystemCursor();

        RenderOnce();
    }

    private void RenderOnce()
    {
        if (!IsActive || _lens is null) return;

        var caret = _settings.FollowCaret ? CaretProbe.TryGetCaretRect() : null;
        var field = _settings.FollowFocus && _focus.Current is { } f
            ? FocusFilter.Useful(f, ScreenInfo.MonitorAt(f.Center))
            : null;
        // Fingerens tryk flytter musepilen, men ikke luppen. Se PointerFilter.
        var mouse = _pointerFilter.Update(ScreenInfo.CursorPosition(), Environment.TickCount64, _lastTouchMs());
        var focus = _follow.Resolve(mouse, caret, field);
        var monitor = ScreenInfo.MonitorAt(focus);
        ActiveMonitor = monitor;

        var frame = _settings.Mode == ViewMode.Dock
            ? MagnifierGeometry.ComputeDockFrame(focus, monitor, _settings.DockEdge, _settings.DockHeightPercent, _settings.Zoom)
            : MagnifierGeometry.ComputeLensFrame(focus, monitor,
                MagnifierGeometry.LensSizeFor(monitor, _settings.LensWidthPercent, _settings.LensHeightPercent, _settings.Shape),
                _settings.Zoom);

        if (frame.IsEmpty) return;
        _lastSourceSize = frame.Source.Size;

        frame = MoveClearOfShell(frame, monitor);

        if (!_capture.Capture(frame.Source)) return;

        _lens.HoleAtFocus = _follow.Source == FocusSource.Mouse;
        _lens.KeepBelow = KeepLensBelow?.Invoke() ?? IntPtr.Zero;
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
        _cursor.Dispose();
        _focus.Dispose();
    }
}
