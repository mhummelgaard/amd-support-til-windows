using System.Windows;
using System.Windows.Media;

namespace Skaermlup.Desktop.Themes;

/// <summary>
/// Windows' kontrasttemaer. Mange med nedsat syn har ét slået til — fx hvid tekst på sort — og så
/// skal kontrolpanelet, vejledningen og spørgsmålene følge det, i stedet for at stå i deres egne
/// grønne farver.
///
/// Temaets farver byttes ud med Windows' systemfarver, før det første vindue åbnes. Vinduerne
/// henter farverne én gang, når de bygges, så skifter brugeren kontrasttema, mens programmet
/// kører, slår det først igennem næste gang Skærmlup startes.
///
/// Luppen og vejledningsbjælken følger det ikke, og det er med vilje: gul på sort er selv
/// programmets højkontrast, og billedet i luppen styres af brugerens egne farvevalg.
/// </summary>
internal static class ContrastTheme
{
    // Hver af temaets farver hører til én af fire roller, og hver rolle har sin systemfarve.
    private static readonly (string Key, Func<Brush> SystemColor)[] Roles =
    [
        // Flader: sider, kort og bløde baggrunde.
        ("Surface", () => SystemColors.WindowBrush),
        ("SurfaceAlt", () => SystemColors.WindowBrush),
        ("PrimarySoft", () => SystemColors.WindowBrush),
        ("Welcome", () => SystemColors.WindowBrush),
        ("WarningBg", () => SystemColors.WindowBrush),
        ("DangerSoft", () => SystemColors.WindowBrush),

        // Tekst og streger på fladerne. Også fokusrammen, så den kan ses mod enhver flade.
        ("Ink", () => SystemColors.WindowTextBrush),
        ("InkMuted", () => SystemColors.WindowTextBrush),
        ("Line", () => SystemColors.WindowTextBrush),
        ("Accent", () => SystemColors.WindowTextBrush),
        ("AccentDark", () => SystemColors.WindowTextBrush),

        // Fyldte felter: hoveder, de store knapper, advarsler.
        ("Primary", () => SystemColors.HighlightBrush),
        ("PrimaryDark", () => SystemColors.HighlightBrush),
        ("Danger", () => SystemColors.HighlightBrush),

        // Tekst på de fyldte felter.
        ("OnDark", () => SystemColors.HighlightTextBrush),
        ("OnDarkMuted", () => SystemColors.HighlightTextBrush),
        ("OnDangerMuted", () => SystemColors.HighlightTextBrush),
    ];

    /// <summary>Navnene på de farver, der byttes. Bruges af prøven, der sikrer at temaet ikke har flere.</summary>
    public static IEnumerable<string> Keys => Roles.Select(r => r.Key);

    /// <summary>Bytter farverne, hvis Windows' kontrasttema er slået til. Kaldes før det første vindue.</summary>
    public static bool ApplyIfActive(ResourceDictionary resources)
    {
        if (!SystemParameters.HighContrast) return false;

        Apply(resources);
        return true;
    }

    public static void Apply(ResourceDictionary resources)
    {
        foreach (var (key, systemColor) in Roles) Replace(resources, key, systemColor());
    }

    private static void Replace(ResourceDictionary root, string key, Brush brush)
    {
        if (!TryReplace(root, key, brush)) root[key] = brush;
    }

    /// <summary>
    /// Farverne står i Colors.xaml, som er flettet ind i Amd.xaml, som er flettet ind i programmet.
    /// De byttes dér, hvor de står — ellers ville stilarterne stadig finde de gamle.
    /// </summary>
    private static bool TryReplace(ResourceDictionary dictionary, string key, Brush brush)
    {
        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (TryReplace(merged, key, brush)) return true;
        }

        if (!dictionary.Contains(key)) return false;
        dictionary[key] = brush;
        return true;
    }
}
