using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Skaermlup.Core.Settings;
using Skaermlup.Desktop.Interop;
using Skaermlup.Desktop.Services;

namespace Skaermlup.Desktop;

public partial class App : Application
{
    private const string StartArgument = "--start";
    private const string SampleArgument = "--proevebillede=";

    private InstanceSignals? _signals;
    private readonly List<RegisteredWaitHandle> _waits = [];
    private AppShell? _shell;

    protected override void OnStartup(StartupEventArgs e)
    {
        string? sampleFolder = SampleFolder(e.Args);

        // Ét program ad gangen. To lyttere på musen ville skændes om den samme knap,
        // og brugeren ville få to lupper oven på hinanden.
        _signals = InstanceSignals.Open();

        if (!_signals.IsFirstInstance)
        {
            // Programmet kører allerede. Send beskeden videre til den kopi der har luppen,
            // og luk os selv igen.
            if (sampleFolder is not null)
            {
                SampleRequest.Write(sampleFolder);
                _signals.Sample.Set();
            }
            else
            {
                _signals.ShowPanel.Set();
            }

            Shutdown();
            return;
        }

        // Gik programmet sidst ned mens musepilen var skjult, sættes markørerne på plads nu.
        SystemCursor.RestoreIfLeftHidden();

        UseDanish();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

        // Sidste udvej: går programmet ned ad en anden vej end den normale, skal musepilen
        // stadig komme tilbage. En bruger må aldrig efterlades uden nogen musepil.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _shell?.Dispose();

        var store = new SettingsStore(SettingsStore.DefaultPath);
        _shell = new AppShell(store, store.Load());

        bool silentStart = e.Args.Any(a => string.Equals(a, StartArgument, StringComparison.OrdinalIgnoreCase));

        _shell.Start(startMagnifier: silentStart || sampleFolder is not null);

        if (sampleFolder is not null)
        {
            SaveSampleAndQuit(sampleFolder);
            return;
        }

        if (!silentStart) _shell.ShowControlPanel();

        Listen(_signals.ShowPanel, () => _shell?.ShowControlPanel());
        Listen(_signals.Sample, SaveRequestedSample);

        // Installeren beder om at få filerne fri. En pæn nedlukning giver musepilen tilbage —
        // en tvungen gør ikke, og brugeren har netop luppen tændt for at kunne læse installeren.
        Listen(_signals.Quit, () => _shell?.Exit());
        Listen(_signals.Uninstall, () =>
        {
            _shell?.ForgetStartWithWindows();
            _shell?.Exit();
        });
    }

    private void Listen(WaitHandle signal, Action onSignal)
        => _waits.Add(ThreadPool.RegisterWaitForSingleObject(
            signal,
            (_, _) => Dispatcher.BeginInvoke(onSignal),
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false));

    /// <summary>En anden kopi af programmet har bedt om et prøvebillede af den lup der kører her.</summary>
    private void SaveRequestedSample()
    {
        string? folder = SampleRequest.Take();
        if (folder is not null) SaveSampleAndWarn(folder);
    }

    /// <summary>
    /// Gemmer billederne og advarer om hvad de indeholder. De to veje ind i
    /// prøvebilledet deler denne, så advarslen ikke kan glemmes det ene sted.
    /// </summary>
    private Window? SaveSampleAndWarn(string folder)
    {
        if (_shell is null) return null;

        var saved = _shell.SaveDiagnosticImages(folder);
        return _shell.ShowSampleSaved(folder, saved);
    }

    private static string? SampleFolder(IEnumerable<string> args)
    {
        foreach (string arg in args)
        {
            if (arg.StartsWith(SampleArgument, StringComparison.OrdinalIgnoreCase))
                return arg[SampleArgument.Length..].Trim('"');
        }

        return null;
    }

    /// <summary>
    /// Supportfunktion: lad luppen tegne et par billeder, gem dem, og luk. Luppen kan ikke
    /// fotograferes udefra, så det er den eneste måde at se hvad brugeren ser.
    /// </summary>
    private void SaveSampleAndQuit(string folder)
    {
        _shell?.ShowControlPanel();
        _shell?.ShowHelp();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var warning = SaveSampleAndWarn(folder);

            // Luppen blev kun tændt for at tage billedet. Bliver den stående, skal
            // brugeren ramme en knap i advarslen med en musepil der er skjult.
            _shell?.Magnifier.TurnOff();

            if (warning is null) { Shutdown(); return; }
            warning.Closed += (_, _) => Shutdown();
        };
        timer.Start();
    }

    private static void UseDanish()
    {
        var danish = CultureInfo.GetCultureInfo("da-DK");
        CultureInfo.DefaultThreadCurrentCulture = danish;
        CultureInfo.DefaultThreadCurrentUICulture = danish;
        Thread.CurrentThread.CurrentCulture = danish;
        Thread.CurrentThread.CurrentUICulture = danish;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(danish.IetfLanguageTag)));
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Luppen må ikke rive brugerens arbejde med sig. Der siges tydeligt fra,
        // og programmet kører videre hvis det overhovedet kan lade sig gøre.
        MessageBox.Show(
            "Der opstod en fejl i Skærmlup:\n\n" + e.Exception.Message +
            "\n\nProgrammet prøver at køre videre. Virker luppen mærkeligt, så luk og start det igen.",
            "Skærmlup", MessageBoxButton.OK, MessageBoxImage.Warning);

        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        foreach (var wait in _waits) wait.Unregister(null);
        _shell?.Dispose();
        _signals?.Dispose();

        base.OnExit(e);
    }
}
