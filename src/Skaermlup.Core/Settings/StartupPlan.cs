namespace Skaermlup.Core.Settings;

/// <summary>Hvordan programmet blev startet.</summary>
public enum LaunchKind
{
    /// <summary>Brugeren startede det selv, fra Start-menuen eller skrivebordet.</summary>
    Normal = 0,

    /// <summary>Windows startede det ved logon, fordi brugeren har valgt "Start sammen med Windows".</summary>
    WithWindows = 1,

    /// <summary>Startet med --proevebillede for at gemme et billede til support.</summary>
    Sample = 2,

    /// <summary>Startet med --proevebillede, men IT har slået prøvebilleder fra på denne pc.</summary>
    SampleBlocked = 3,
}

/// <summary>
/// Hvad programmet gør, når det starter: om kontrolpanelet vises, og om luppen tændes.
///
/// Det her lå før inde i programmets opstart, hvor ingen test kunne nå det, og dér gemte sig en
/// fejl: når Windows startede programmet, blev luppen altid tændt — uanset hvad der stod i
/// afkrydsningsfeltet "Tænd luppen med det samme". Valgte man kun at starte med Windows, sprang
/// luppen alligevel frem og skjulte musepilen ved hver eneste logon.
/// </summary>
public sealed record StartupPlan(LaunchKind Kind, bool ShowControlPanel, bool TurnLensOn, string? SampleFolder)
{
    /// <summary>Det Windows starter programmet med. Skrives af autostart-registreringen.</summary>
    public const string WithWindowsArgument = "--start";

    public const string SampleArgument = "--proevebillede=";

    public static StartupPlan From(IReadOnlyList<string> args, AppSettings settings, MachinePolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(settings);
        policy ??= MachinePolicy.None;

        if (SampleFolderIn(args) is { } folder)
        {
            // Slået fra af IT: luppen tændes ikke, for der skal ikke tages noget billede. Programmet
            // siger kun fra og lukker igen. Mappen huskes ikke — der skal ikke skrives noget i den.
            if (policy.SampleImagesBlocked)
                return new StartupPlan(LaunchKind.SampleBlocked, ShowControlPanel: false, TurnLensOn: false, null);

            // Et prøvebillede skal vise luppen, så den tændes. Kontrolpanelet åbnes af prøvebilledet selv.
            return new StartupPlan(LaunchKind.Sample, ShowControlPanel: false, TurnLensOn: true, folder);
        }

        bool withWindows = args.Any(a => string.Equals(a, WithWindowsArgument, StringComparison.OrdinalIgnoreCase));

        return withWindows
            // Ved logon: ingen vinduer i ansigtet. Vejledningsbjælken siger, hvordan luppen tændes.
            ? new StartupPlan(LaunchKind.WithWindows, ShowControlPanel: false, settings.StartMagnifierOnLaunch, null)
            : new StartupPlan(LaunchKind.Normal, ShowControlPanel: true, settings.StartMagnifierOnLaunch, null);
    }

    /// <summary>Mappen fra --proevebillede=, eller null. Anførselstegn omkring stien fjernes.</summary>
    public static string? SampleFolderIn(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        foreach (string arg in args)
        {
            if (!arg.StartsWith(SampleArgument, StringComparison.OrdinalIgnoreCase)) continue;

            string folder = arg[SampleArgument.Length..].Trim('"');
            return string.IsNullOrWhiteSpace(folder) ? null : folder;
        }

        return null;
    }
}
