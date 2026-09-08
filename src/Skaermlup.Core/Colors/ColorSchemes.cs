using Skaermlup.Core.Settings;

namespace Skaermlup.Core.Colors;

/// <summary>Danske navne og rækkefølge for farvetilstandene.</summary>
public static class ColorSchemes
{
    private static readonly ColorScheme[] Ordered =
    [
        ColorScheme.Normal,
        ColorScheme.HighContrast,
        ColorScheme.Inverted,
        ColorScheme.YellowOnBlack,
        ColorScheme.BlackOnYellow,
        ColorScheme.WhiteOnBlack,
        ColorScheme.Grayscale,
    ];

    /// <summary>Rækkefølgen der bladres igennem med tastaturgenvejen.</summary>
    public static IReadOnlyList<ColorScheme> Order => Ordered;

    public static ColorScheme Next(ColorScheme current)
    {
        int i = Array.IndexOf(Ordered, current);
        return i < 0 ? Ordered[0] : Ordered[(i + 1) % Ordered.Length];
    }

    public static string Name(ColorScheme scheme) => scheme switch
    {
        ColorScheme.Normal => "Normale farver",
        ColorScheme.HighContrast => "Kraftig kontrast",
        ColorScheme.Inverted => "Omvendte farver",
        ColorScheme.Grayscale => "Gråtoner",
        ColorScheme.YellowOnBlack => "Gul på sort",
        ColorScheme.BlackOnYellow => "Sort på gul",
        ColorScheme.WhiteOnBlack => "Hvid på sort",
        _ => scheme.ToString(),
    };

    public static string Description(ColorScheme scheme) => scheme switch
    {
        ColorScheme.Normal => "Skærmen som den er.",
        ColorScheme.HighContrast => "Beholder farverne, men gør lyst lysere og mørkt mørkere.",
        ColorScheme.Inverted => "Vender lyst og mørkt om. Mindre blænding fra hvide sider.",
        ColorScheme.Grayscale => "Fjerner farverne, så kun lys og mørke er tilbage.",
        ColorScheme.YellowOnBlack => "Sort bund med gul tekst. Meget høj kontrast. Den gule tone kan vælges.",
        ColorScheme.BlackOnYellow => "Gul bund med sort tekst. Den gule tone kan vælges.",
        ColorScheme.WhiteOnBlack => "Sort bund med hvid tekst.",
        _ => string.Empty,
    };
}
