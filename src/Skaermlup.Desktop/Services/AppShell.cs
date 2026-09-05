using System.IO;
using System.Windows;
using System.Windows.Threading;
using Skaermlup.Core.Input;
using Skaermlup.Core.Magnification;
using Skaermlup.Core.Settings;
using Skaermlup.Core.Text;
using Skaermlup.Desktop.Interop;
using Skaermlup.Desktop.Views;

namespace Skaermlup.Desktop.Services;

/// <summary>
/// Binder programmet sammen: indstillinger, luppen, museknapperne, tastaturgenvejene,
/// vejledningsbjælken og kontrolpanelet.
/// </summary>
internal sealed class AppShell : IDisposable
{
    private readonly SettingsStore _store;
    private readonly GlobalMouseHook _mouse = new();
    private readonly HotkeyService _hotkeys = new();
    private readonly MouseTriggerDetector _trigger = new();
    private readonly DispatcherTimer _instructionTimer;
    private readonly DispatcherTimer _saveTimer;

    private InstructionBar? _bar;
    private MainWindow? _panel;
    private HelpWindow? _help;
    private bool _disposed;

    public AppShell(SettingsStore store, AppSettings settings)
    {
        _store = store;
        Settings = settings;
        Magnifier = new MagnifierService(settings);
        Magnifier.Changed += (_, _) => RefreshInstructions();

        _instructionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(350),
        };
        _instructionTimer.Tick += (_, _) => RefreshInstructions();

        _saveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(800),
        };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveNow();
        };
    }

    public AppSettings Settings { get; }

    public MagnifierService Magnifier { get; }

    /// <summary>Falsk hvis Windows ikke ville lade os lytte på musen. Så virker museknappen ikke.</summary>
    public bool MouseTriggerWorks { get; private set; } = true;

    public IReadOnlyList<ShortcutDefinition> UnavailableShortcuts => _hotkeys.Unavailable;

    public void Start(bool startMagnifier)
    {
        Magnifier.Prepare();
        MouseTriggerWorks = _mouse.Install();
        _mouse.Button += OnMouseButton;
        _mouse.Wheel += OnMouseWheel;

        _hotkeys.Command += (_, command) => Execute(command);
        _hotkeys.Install();

        _instructionTimer.Start();
        RefreshInstructions();
        WriteDiagnostics();

        if (startMagnifier || Settings.StartMagnifierOnLaunch) Magnifier.TurnOn();
    }

    // ---------- Museknapper ----------

    private void OnMouseButton(object? sender, MouseButtonHookEventArgs e)
    {
        _trigger.Buttons = Settings.Trigger;
        _trigger.Mode = Settings.TriggerMode;
        _trigger.BlockTriggerClicks = Settings.BlockTriggerClicks;

        var decision = _trigger.Decide(new MouseButtonEvent(e.Button, e.IsDown, e.TimestampMs));
        e.Swallow = decision.Swallow;

        if (decision.Outcome == TriggerOutcome.None) return;

        // Selve arbejdet lægges i kø. En lytter på musen skal svare med det samme,
        // ellers fjerner Windows den igen.
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            switch (decision.Outcome)
            {
                case TriggerOutcome.Toggle: Magnifier.Toggle(); break;
                case TriggerOutcome.TurnOn: Magnifier.TurnOn(); break;
                case TriggerOutcome.TurnOff: Magnifier.TurnOff(); break;
            }
        });
    }

    private void OnMouseWheel(object? sender, MouseWheelHookEventArgs e)
    {
        var decision = WheelRules.Decide(e.Delta, e.Control, e.Shift, Magnifier.IsActive, Settings.Wheel);
        e.Swallow = decision.Swallow;

        if (decision.Outcome == WheelOutcome.None) return;

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            switch (decision.Outcome)
            {
                case WheelOutcome.ZoomIn: Magnifier.ZoomIn(); break;
                case WheelOutcome.ZoomOut: Magnifier.ZoomOut(); break;
                case WheelOutcome.LensBigger: Magnifier.ResizeLens(LensSizer.StepPercent); break;
                case WheelOutcome.LensSmaller: Magnifier.ResizeLens(-LensSizer.StepPercent); break;
            }

            SaveSoon();
        });
    }

    // ---------- Kommandoer ----------

    public void Execute(AppCommand command)
    {
        switch (command)
        {
            case AppCommand.ToggleMagnifier: Magnifier.Toggle(); break;
            case AppCommand.ZoomIn: Magnifier.ZoomIn(); break;
            case AppCommand.ZoomOut: Magnifier.ZoomOut(); break;
            case AppCommand.LensBigger: Magnifier.ResizeLens(LensSizer.StepPercent); break;
            case AppCommand.LensSmaller: Magnifier.ResizeLens(-LensSizer.StepPercent); break;
            case AppCommand.NextColors: Magnifier.NextColors(); break;
            case AppCommand.ToggleViewMode: Magnifier.ToggleViewMode(); break;
            case AppCommand.ToggleInstructions:
                Settings.ShowInstructions = !Settings.ShowInstructions;
                RefreshInstructions();
                break;
            case AppCommand.ShowControlPanel: ShowControlPanel(); break;
            case AppCommand.PanLeft: Magnifier.Pan(-1, 0); break;
            case AppCommand.PanRight: Magnifier.Pan(1, 0); break;
            case AppCommand.PanUp: Magnifier.Pan(0, -1); break;
            case AppCommand.PanDown: Magnifier.Pan(0, 1); break;
        }

        SaveSoon();
    }

    // ---------- Vinduer ----------

    public void ShowControlPanel()
    {
        _panel ??= new MainWindow(this);

        if (!_panel.IsVisible) _panel.Show();
        if (_panel.WindowState == WindowState.Minimized) _panel.WindowState = WindowState.Normal;

        _panel.Activate();
    }

    /// <summary>Kun til support: gemmer et billede af luppen og bjælken som de ser ud nu.</summary>
    public IReadOnlyList<string> SaveDiagnosticImages(string folder)
        => DiagnosticImages.SaveTo(folder, Magnifier.LensWindow, _bar, _panel, _help);

    public void ShowHelp()
    {
        if (_help is null)
        {
            _help = new HelpWindow(Settings);
            _help.Closed += (_, _) => _help = null;
            _help.Show();
        }

        _help.Activate();
    }

    private void RefreshInstructions()
    {
        if (_disposed) return;

        if (!Settings.ShowInstructions)
        {
            _bar?.Hide();
            return;
        }

        _bar ??= new InstructionBar();
        if (!_bar.IsVisible)
        {
            _bar.Show();
            _bar.ForgetPlacement();
        }

        // Bjælken lægges i skærmens arbejdsområde, så den ikke dækker proceslinjen.
        var anchor = Magnifier.IsActive && !Magnifier.ActiveMonitor.IsEmpty
            ? Magnifier.ActiveMonitor.Center
            : ScreenInfo.CursorPosition();
        var monitor = ScreenInfo.WorkAreaAt(anchor);

        _bar.Refresh(Magnifier.IsActive, Settings, monitor);
        _bar.Reassert();
    }

    // ---------- Indstillinger ----------

    /// <summary>Kaldes når brugeren har ændret noget i kontrolpanelet.</summary>
    public void SettingsChanged()
    {
        // Skifter brugeren udløserknap midt i et tryk, skal det halve tryk glemmes.
        _trigger.Reset();
        Magnifier.ApplySettings();
        RefreshInstructions();
        SaveSoon();
    }

    public void SaveSoon()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveNow()
    {
        try
        {
            _store.Save(Settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Kan indstillingerne ikke gemmes, skal luppen stadig virke resten af dagen.
        }
    }

    /// <summary>Sandt når programmet er på vej ned, så vinduerne ikke afviser at lukke.</summary>
    public bool IsShuttingDown { get; private set; }

    public void Exit()
    {
        IsShuttingDown = true;
        SaveNow();
        Application.Current?.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _instructionTimer.Stop();
        _saveTimer.Stop();

        _mouse.Button -= OnMouseButton;
        _mouse.Wheel -= OnMouseWheel;
        _mouse.Dispose();
        _hotkeys.Dispose();
        Magnifier.Dispose();

        _bar?.Close();
        _bar = null;
        _help?.Close();
        _help = null;
    }

    /// <summary>
    /// Skriver en lille statusfil ved siden af indstillingerne. Går noget galt hos brugeren,
    /// kan man se på én side om museknappen og luppen overhovedet fik lov af Windows.
    /// </summary>
    private void WriteDiagnostics()
    {
        try
        {
            string folder = Path.GetDirectoryName(_store.FilePath) ?? Path.GetTempPath();
            Directory.CreateDirectory(folder);

            var lines = new List<string>
            {
                "Skærmlup " + (typeof(AppShell).Assembly.GetName().Version?.ToString() ?? "?"),
                "Tidspunkt: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "Windows: " + Environment.OSVersion.VersionString,
                "Museknappen virker: " + (MouseTriggerWorks ? "ja" : "NEJ"),
                "Luppen er skjult for skærmkopiering: " + (Magnifier.HiddenFromCapture ? "ja" : "NEJ — brug læsebånd"),
                "Ring om markøren ved Start-menuen: " + Magnifier.PointerRingStatus,
                "Skærm ved musen: " + ScreenInfo.MonitorAt(ScreenInfo.CursorPosition()),
                "Hele skrivebordet: " + ScreenInfo.VirtualScreen(),
                "Indstillinger: " + _store.FilePath,
            };

            lines.AddRange(SelfTest.Run());

            foreach (var shortcut in UnavailableShortcuts)
                lines.Add("Genvej optaget af et andet program: " + shortcut.Display);

            File.WriteAllLines(Path.Combine(folder, "diagnostik.txt"), lines);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Statusfilen er en hjælp, ikke en forudsætning.
        }
    }
}
