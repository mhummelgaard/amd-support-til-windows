using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Skaermlup.Core.Settings;
using Skaermlup.Desktop.Services;

namespace Skaermlup.Desktop;

public partial class App : Application
{
    private const string InstanceKey = "Skaermlup.EnKoerselAdGangen.v1";
    private const string StartArgument = "--start";
    private const string SampleArgument = "--proevebillede=";

    private Mutex? _instanceLock;
    private EventWaitHandle? _showPanelSignal;
    private RegisteredWaitHandle? _showPanelWait;
    private AppShell? _shell;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Ét program ad gangen. To lyttere på musen ville skændes om den samme knap,
        // og brugeren ville få to lupper oven på hinanden.
        _instanceLock = new Mutex(initiallyOwned: true, InstanceKey, out bool isFirstInstance);
        _showPanelSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceKey + ".VisPanel");

        if (!isFirstInstance)
        {
            // Programmet kører allerede. Bed det om at vise kontrolpanelet og luk os selv.
            _showPanelSignal.Set();
            Shutdown();
            return;
        }

        UseDanish();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

        var store = new SettingsStore(SettingsStore.DefaultPath);
        _shell = new AppShell(store, store.Load());

        bool silentStart = e.Args.Any(a => string.Equals(a, StartArgument, StringComparison.OrdinalIgnoreCase));
        string? sampleFolder = SampleFolder(e.Args);

        _shell.Start(startMagnifier: silentStart || sampleFolder is not null);

        if (sampleFolder is not null)
        {
            SaveSampleAndQuit(sampleFolder);
            return;
        }

        if (!silentStart) _shell.ShowControlPanel();

        _showPanelWait = ThreadPool.RegisterWaitForSingleObject(
            _showPanelSignal,
            (_, _) => Dispatcher.BeginInvoke(() => _shell?.ShowControlPanel()),
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);
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
            _shell?.SaveDiagnosticImages(folder);
            Shutdown();
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
        _showPanelWait?.Unregister(null);
        _shell?.Dispose();
        _showPanelSignal?.Dispose();

        if (_instanceLock is not null)
        {
            try { _instanceLock.ReleaseMutex(); } catch (ApplicationException) { }
            _instanceLock.Dispose();
        }

        base.OnExit(e);
    }
}
