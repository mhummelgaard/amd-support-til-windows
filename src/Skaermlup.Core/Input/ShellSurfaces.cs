namespace Skaermlup.Core.Input;

/// <summary>
/// Afgør ud fra forgrundsvinduet om det er Windows' egen skal der ligger øverst: Start-menuen,
/// søgefeltet, opgavevisningen, meddelelsescenteret, proceslinjens flyouts eller en åben menu.
///
/// De flader lægger Windows i et højere lag end almindelige programmer kan nå, og luppen er
/// derfor skjult bag dem uanset hvor tit den kravler op. Det i sig selv er til at leve med —
/// det slemme er, at luppen samtidig har skjult musepilen. Brugeren står så tilbage med
/// hverken lup eller markør, og det er præcis dér man har allermest brug for at kunne se
/// hvor musen er.
///
/// Reglen står her, adskilt fra Windows-kaldene, så den kan afprøves uden en Start-menu.
/// </summary>
public static class ShellSurfaces
{
    /// <summary>
    /// Processer der ikke laver andet end at tegne Windows' egen skal. Ligger en af dem i
    /// forgrunden, er det skallen der er fremme — uanset hvad vinduet ellers hedder.
    /// </summary>
    private static readonly string[] Processes =
    [
        "startmenuexperiencehost",  // Start-menuen
        "shellexperiencehost",      // meddelelsescenter, hurtige indstillinger, ur-flyout
        "searchhost",               // søgefeltet i Windows 11
        "searchapp",                // søgefeltet i Windows 10
        "shellhost",                // opgavevisning og widgets i nyere udgaver
        "textinputhost",            // skærmtastatur, emoji-panel og udklipshistorik
    ];

    /// <summary>
    /// Vinduesklasser der hører til skallen. De kører i explorer.exe sammen med almindelige
    /// mappevinduer, så explorer kan ikke stå på listen ovenfor — det ville gøre hver eneste
    /// åben mappe til en "shell-flade".
    /// </summary>
    private static readonly string[] Classes =
    [
        "Shell_TrayWnd",                        // proceslinjen
        "Shell_SecondaryTrayWnd",               // proceslinjen på de øvrige skærme
        "NotifyIconOverflowWindow",             // den skjulte del af systembakken
        "TopLevelWindowForOverflowXamlIsland",  // samme, Windows 11
        "MultitaskingViewFrame",                // opgavevisning
        "XamlExplorerHostIslandWindow",         // opgavevisning og Alt+Tab i Windows 11
        "ForegroundStaging",                    // mellemvinduet mens der skiftes opgave
        "ControlCenterWindow",                  // hurtige indstillinger
    ];

    /// <summary>
    /// Sandt hvis luppen lige nu er dækket af Windows' egen skal.
    /// </summary>
    /// <param name="windowClass">Forgrundsvinduets klassenavn.</param>
    /// <param name="processName">Navnet på processen bag vinduet, med eller uden sti og ".exe".</param>
    /// <param name="menuIsOpen">
    /// Sandt hvis en almindelig Win32-menu er slået ned — højrekliksmenuer og programmenuer.
    /// De er ikke en del af skallen, men de er topmost-vinduer, og luppen taber kapløbet om
    /// at ligge øverst tit nok til at det generer.
    /// </param>
    public static bool IsShellSurface(string? windowClass, string? processName, bool menuIsOpen)
    {
        if (menuIsOpen) return true;

        string process = NormalizeProcess(processName);
        if (process.Length > 0 && Processes.Contains(process, StringComparer.Ordinal)) return true;

        if (string.IsNullOrEmpty(windowClass)) return false;
        return Classes.Contains(windowClass, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Skærer sti og filendelse væk, så både "C:\Windows\Explorer.EXE" og "explorer" passer.</summary>
    public static string NormalizeProcess(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return string.Empty;

        var span = processName.AsSpan().Trim();

        int slash = span.LastIndexOfAny('\\', '/');
        if (slash >= 0) span = span[(slash + 1)..];

        if (span.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            span = span[..^4];

        return span.ToString().ToLowerInvariant();
    }
}
