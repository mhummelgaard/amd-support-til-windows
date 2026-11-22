using System.IO;
using System.Windows;
using System.Windows.Threading;
using Skaermlup.Core.Geometry;
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
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly GlobalMouseHook _mouse = new();
    private readonly HotkeyService _hotkeys = new();

    // Bruges fra både musens lyttertråd og hovedtråden, så adgangen går gennem en lås.
    private readonly MouseTriggerDetector _trigger = new();
    private readonly TriggerLearner _learner = new();
    private readonly object _triggerLock = new();
    private bool _learning;
    private readonly DispatcherTimer _learnTimer;

    private readonly HookWatchdog _hookWatchdog = new();
    private readonly DispatcherTimer _hookTimer;
    private readonly DispatcherTimer _instructionTimer;
    private readonly DispatcherTimer _saveTimer;

    private InstructionBar? _bar;
    private TrayIcon? _tray;
    private TouchControls? _touch;
    private MainWindow? _panel;
    private HelpWindow? _help;
    private bool _disposed;

    public AppShell(SettingsStore store, AppSettings settings)
    {
        _store = store;
        Settings = settings;
        Magnifier = new MagnifierService(settings, () => _mouse.LastTouchMs)
        {
            KeepLensBelow = () => _touch is { IsVisible: true } t ? t.Handle : IntPtr.Zero,
        };
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

        _hookTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        _hookTimer.Tick += (_, _) => CheckMouseHook();

        // Har man ikke trykket inden da, er man nok i tvivl. Så siges det, og intet er ændret.
        _learnTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _learnTimer.Tick += (_, _) => CancelLearningTrigger(TriggerSetup.TimedOut);
    }

    public AppSettings Settings { get; }

    public MagnifierService Magnifier { get; }

    /// <summary>Falsk hvis Windows ikke ville lade os lytte på musen. Så virker museknappen ikke.</summary>
    public bool MouseTriggerWorks { get; private set; } = true;

    /// <summary>Hvor mange gange musens lytter er sat op igen, fordi Windows havde fjernet den.</summary>
    public int MouseHookReinstalls => _mouse.Reinstalls;

    /// <summary>Museknappen er holdt op med at virke, eller virker igen.</summary>
    public event EventHandler? MouseTriggerHealthChanged;

    public IReadOnlyList<ShortcutDefinition> UnavailableShortcuts => _hotkeys.Unavailable;

    /// <param name="turnLensOn">Afgjort af <see cref="StartupPlan"/> ud fra hvordan programmet blev startet.</param>
    public void Start(bool turnLensOn)
    {
        Magnifier.Prepare();

        // Abonnér før lytteren startes. Den kører på sin egen tråd og kan nå at høre et klik,
        // før Install vender tilbage.
        _mouse.Button += OnMouseButton;
        _mouse.Wheel += OnMouseWheel;
        MouseTriggerWorks = _mouse.Install();
        _hookTimer.Start();

        _hotkeys.Command += (_, command) => Execute(command);
        _hotkeys.Install();

        _tray = new TrayIcon(this);

        _instructionTimer.Start();
        RefreshInstructions();
        WriteDiagnostics();

        if (turnLensOn) Magnifier.TurnOn();
    }

    // ---------- Museknapper ----------

    // Kører på musens lyttertråd. Beslutningen træffes her, fordi Windows skal have svar på om
    // klikket skal holdes tilbage, før metoden vender tilbage. Selve arbejdet sendes til
    // hovedtråden. Indstillingerne læses herfra uden lås: det er enkle værdier, og et klik
    // der læser den gamle værdi et øjeblik efter en ændring, gør ingen skade.
    private void OnMouseButton(object? sender, MouseButtonHookEventArgs e)
    {
        // Et tryk med en finger eller en pen er ikke en museknap. Det må hverken tænde luppen
        // (venstre + højre samtidig kunne ellers ramme ved et uheld) eller holdes tilbage.
        if (e.FromTouchOrPen) return;

        TriggerDecision decision;
        lock (_triggerLock)
        {
            // Mens brugeren vælger en ny knap, er et tryk et valg — ikke et tænd eller sluk.
            if (_learning)
            {
                var step = _learner.Observe(new MouseButtonEvent(e.Button, e.IsDown, e.TimestampMs));
                e.Swallow = step.Swallow;
                if (step.Learned is { } learned)
                {
                    _learning = false;
                    _dispatcher.BeginInvoke(() => ApplyLearnedTrigger(learned));
                }
                return;
            }

            _trigger.Buttons = Settings.Trigger;
            _trigger.Mode = Settings.TriggerMode;
            _trigger.BlockTriggerClicks = Settings.BlockTriggerClicks;

            decision = _trigger.Decide(new MouseButtonEvent(e.Button, e.IsDown, e.TimestampMs));
        }
        e.Swallow = decision.Swallow;

        if (decision.Outcome == TriggerOutcome.None) return;

        _dispatcher.BeginInvoke(() =>
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
        if (e.FromTouchOrPen) return;

        var decision = WheelRules.Decide(e.Delta, e.Control, e.Shift, Magnifier.IsActive, Settings.Wheel);
        e.Swallow = decision.Swallow;

        if (decision.Outcome == WheelOutcome.None) return;

        _dispatcher.BeginInvoke(() =>
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

    // ---------- Vælg knappen ved at trykke på den ----------

    /// <summary>Sandt mens programmet venter på, at brugeren trykker på sin nye udløserknap.</summary>
    public bool IsLearningTrigger
    {
        get { lock (_triggerLock) return _learning; }
    }

    /// <summary>Hvad der skal stå om knapvalget lige nu — eller null, når der intet er at sige.</summary>
    public string? TriggerSetupMessage { get; private set; }

    public event EventHandler? TriggerSetupChanged;

    public void StartLearningTrigger()
    {
        lock (_triggerLock)
        {
            _learner.Reset();
            _learning = true;
        }

        TriggerSetupMessage = TriggerSetup.Waiting;
        _learnTimer.Stop();
        _learnTimer.Start();
        TriggerSetupChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CancelLearningTrigger(string message)
    {
        lock (_triggerLock) _learning = false;

        _learnTimer.Stop();
        TriggerSetupMessage = message;
        TriggerSetupChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>For mus uden midterste knap: venstre og højre trykket samtidig.</summary>
    public void UseLeftRightChord()
    {
        CancelLearningTrigger(TriggerSetup.ChordChosen);
        Settings.Trigger = MouseTriggerButtons.Middle | MouseTriggerButtons.LeftRightChord;
        SettingsChanged();
    }

    private void ApplyLearnedTrigger(MouseTriggerButtons button)
    {
        _learnTimer.Stop();

        // Den valgte knap erstatter de andre. Én knap er lettest at huske.
        Settings.Trigger = button;
        SettingsChanged();

        TriggerSetupMessage = TriggerSetup.Learned(button);
        TriggerSetupChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Vagten over musens lytter. Windows fjerner en lytter der er for længe om at svare, uden at
    /// sige det. Har musen flyttet sig uden at lytteren hørte det, sættes den op igen.
    /// </summary>
    private void CheckMouseHook()
    {
        if (_hookWatchdog.Check(ScreenInfo.CursorPosition(), _mouse.LastEventMs, Environment.TickCount64))
            _mouse.Reinstall();

        bool works = _mouse.IsInstalled;
        if (works == MouseTriggerWorks) return;

        MouseTriggerWorks = works;
        RefreshInstructions();
        MouseTriggerHealthChanged?.Invoke(this, EventArgs.Empty);
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

    /// <summary>
    /// Kun til support: gemmer et billede af luppen og bjælken som de ser ud nu, og siger tydeligt
    /// fra bagefter — billederne viser skærmen.
    ///
    /// Den eneste vej til <see cref="DiagnosticImages"/>. Har IT slået prøvebilleder fra, gemmes
    /// intet, og brugeren får at vide hvorfor. Spørgsmålet stilles her og ikke længere oppe, så
    /// ingen fremtidig vej ind i prøvebilledet kan komme uden om det.
    ///
    /// Vinduet returneres, fordi den kopi af programmet der kun blev startet for at tage
    /// prøvebilledet, skal blive stående indtil beskeden er læst — ikke lukke ned midt i den.
    /// </summary>
    public Window SaveSample(string folder)
    {
        if (MachinePolicyReader.Read().SampleImagesBlocked) return ShowSampleBlocked();

        var saved = DiagnosticImages.SaveTo(folder, Magnifier.LensWindow, _bar, _panel, _help);
        return Present(new SampleSavedWindow(folder, saved));
    }

    /// <summary>IT har slået prøvebilleder fra. Intet er gemt; brugeren får at vide hvorfor.</summary>
    public Window ShowSampleBlocked() => Present(SampleSavedWindow.Blocked());

    private static Window Present(Window window)
    {
        window.Show();
        window.Activate();
        return window;
    }

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

        RefreshTouchControls();

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

        _bar.Refresh(Magnifier.IsActive, Settings, monitor, MouseTriggerWorks, touch: GuideForTouch);
        _bar.Reassert();
    }

    // ---------- Berøringsskærm ----------

    /// <summary>Om knapperne til berøringsskærm står fremme lige nu.</summary>
    public bool TouchControlsShown => _touch is { IsVisible: true };

    /// <summary>Sandt når fingeren — ikke musen — var det, brugeren sidst brugte.</summary>
    public bool LastInputWasTouch => _mouse.LastTouchMs > 0 && _mouse.LastTouchMs > _mouse.LastMouseMs;

    /// <summary>
    /// Vejledningen handler om touch-knapperne, når de står fremme og fingeren er i brug. Ellers
    /// om museknappen og hjulet, som før.
    /// </summary>
    public bool GuideForTouch => TouchControlsShown && LastInputWasTouch;

    private void RefreshTouchControls()
    {
        if (!Settings.TouchControlsWanted(ScreenInfo.HasTouchScreen))
        {
            _touch?.Hide();
            return;
        }

        if (_touch is null)
        {
            _touch = new TouchControls();
            _touch.PowerPressed += (_, _) => TouchPower();
            _touch.ZoomInPressed += (_, _) => { Magnifier.ZoomIn(); SaveSoon(); };
            _touch.ZoomOutPressed += (_, _) => { Magnifier.ZoomOut(); SaveSoon(); };
            _touch.SettingsPressed += (_, _) => ShowControlPanel();
        }

        if (!_touch.IsVisible) _touch.Show();

        // Knapperne står på hovedskærmen — den, hvis øverste venstre hjørne er (0,0).
        _touch.Refresh(Magnifier.IsActive, Settings.TouchControlsSide, PrimaryWorkArea());
        _touch.Reassert();
    }

    private void TouchPower()
    {
        if (Magnifier.IsActive) Magnifier.TurnOff();
        else Magnifier.TurnOnForTouch(PrimaryWorkArea().Center);
    }

    private static PixelRect PrimaryWorkArea() => ScreenInfo.WorkAreaAt(PixelPoint.Zero);

    // ---------- Indstillinger ----------

    /// <summary>Kaldes når brugeren har ændret noget i kontrolpanelet.</summary>
    public void SettingsChanged()
    {
        // Skifter brugeren udløserknap midt i et tryk, skal det halve tryk glemmes.
        lock (_triggerLock) _trigger.Reset();
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

    /// <summary>
    /// Afslutter uden at spørge. Bruges af installeren, der ikke kan svare på et spørgsmål.
    /// Knapperne bruger <see cref="ExitAfterAsking"/>.
    /// </summary>
    public void Exit()
    {
        IsShuttingDown = true;
        SaveNow();
        Application.Current?.Shutdown();
    }

    /// <summary>Afslut, men først når brugeren har bekræftet det.</summary>
    public void ExitAfterAsking()
    {
        if (Confirm(Confirmations.Quit(Settings.StartWithWindows))) Exit();
    }

    /// <summary>Spørger med programmets egen store skrift. Sandt hvis brugeren vil gøre det.</summary>
    public bool Confirm(Confirmation question) => ConfirmWindow.Ask(question, _panel);

    /// <summary>
    /// Programmet afinstalleres. Autostart ligger hos brugeren selv, og installeren — der kører
    /// som administrator, måske som en anden — kan ikke nå den. Uden det her ville Windows
    /// forsøge at starte en slettet fil ved hver opstart.
    /// </summary>
    public void ForgetStartWithWindows()
    {
        StartupRegistration.Set(false);
        Settings.StartWithWindows = false;
        SaveNow();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _instructionTimer.Stop();
        _hookTimer.Stop();
        _learnTimer.Stop();
        _saveTimer.Stop();

        _mouse.Button -= OnMouseButton;
        _mouse.Wheel -= OnMouseWheel;
        _mouse.Dispose();
        _hotkeys.Dispose();
        _tray?.Dispose();
        _touch?.Close();
        _touch = null;
        _tray = null;
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
