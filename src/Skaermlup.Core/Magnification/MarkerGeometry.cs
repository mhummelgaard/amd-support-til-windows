using Skaermlup.Core.Geometry;
using Skaermlup.Core.Settings;

namespace Skaermlup.Core.Magnification;

/// <summary>
/// Hvor markeringens streger og ring skal ligge inde i luppen. Alle mål er i skærmpixels
/// med luppens øverste venstre hjørne som nulpunkt. Tomme rektangler tegnes ikke.
/// </summary>
public readonly record struct MarkerLayout(
    PixelRect Left,
    PixelRect Right,
    PixelRect Top,
    PixelRect Bottom,
    PixelRect Ring)
{
    public static MarkerLayout Empty => new(PixelRect.Empty, PixelRect.Empty, PixelRect.Empty,
        PixelRect.Empty, PixelRect.Empty);
}

/// <summary>
/// Regner markeringen ud. Musepilen kommer ikke med i en skærmkopi, så uden en markering ville
/// brugeren ikke kunne se hvor på skærmen luppen peger. Men markeringen må ikke dække det man
/// er ved at læse eller skrive — derfor hullet i midten, og derfor stregen der kan lægges
/// under linjen i stedet for hen over den.
/// </summary>
public static class MarkerGeometry
{
    /// <summary>Mindste hul om markeringspunktet.</summary>
    public const int MinimumGap = 14;

    /// <summary>Største hul. Derover kan man ikke længere se hvor krydset peger.</summary>
    public const int MaximumGap = 90;

    /// <summary>
    /// Hullet vokser mens musen bevæger sig. Under flytning skal man se hvor man er på vej
    /// hen, og der er hverken tekst at læse eller markør at sætte; står musen stille, skal
    /// krydset til gengæld pege så præcist som muligt.
    /// </summary>
    /// <param name="baseGap">Hullet når musen står stille.</param>
    /// <param name="speedPixelsPerSecond">Fokuspunktets fart på skærmen.</param>
    public static int GapFor(int baseGap, double speedPixelsPerSecond)
    {
        baseGap = Math.Clamp(baseGap, MinimumGap, MaximumGap);
        if (double.IsNaN(speedPixelsPerSecond) || speedPixelsPerSecond <= 0) return baseGap;

        // Ved cirka 1200 pixels i sekundet — en rask bevægelse over skærmen — er hullet
        // vokset til det dobbelte.
        double growth = Math.Min(speedPixelsPerSecond / 1200.0, 1.0);
        return Math.Clamp(baseGap + (int)Math.Round(baseGap * growth), MinimumGap, MaximumGap);
    }

    /// <param name="view">Luppens størrelse i skærmpixels.</param>
    /// <param name="marker">Punktet der peges på, inde i luppen.</param>
    /// <param name="gap">Hullet omkring punktet.</param>
    /// <param name="thickness">Stregernes tykkelse.</param>
    /// <param name="ringRadius">Ringens radius.</param>
    /// <param name="underlineDrop">Hvor langt under punktet stregen lægges i understregningen.</param>
    public static MarkerLayout Compute(
        PixelSize view, PixelPoint marker, MarkerStyle style,
        int gap, int thickness, int ringRadius, int underlineDrop)
    {
        if (view.IsEmpty || style == MarkerStyle.None) return MarkerLayout.Empty;

        thickness = Math.Max(1, thickness);
        gap = Math.Clamp(gap, MinimumGap, MaximumGap);
        ringRadius = Math.Max(4, ringRadius);

        int x = Math.Clamp(marker.X, 0, view.Width);
        int y = Math.Clamp(marker.Y, 0, view.Height);
        int half = thickness / 2;
        var ring = new PixelRect(x - ringRadius, y - ringRadius, ringRadius * 2, ringRadius * 2);

        switch (style)
        {
            case MarkerStyle.Ring:
                return MarkerLayout.Empty with { Ring = ring };

            case MarkerStyle.Underline:
            {
                // Stregen lægges under tekstlinjen i stedet for hen over den, så bogstaverne
                // står frit. Den lodrette pind går fra stregen op mod punktet, men stopper
                // før det, så kolonnen kan ses uden at blive dækket.
                int drop = Math.Clamp(underlineDrop, ringRadius + thickness, Math.Max(1, view.Height));
                int lineY = Math.Min(y + drop, view.Height - thickness);

                var line = new PixelRect(0, lineY, view.Width, thickness);
                var stem = Height(x - half, y + ringRadius, thickness, lineY - (y + ringRadius));

                return MarkerLayout.Empty with { Left = line, Bottom = stem, Ring = ring };
            }

            default:
            {
                // Krydset når ikke sammen på midten. Der hvor markøren peger, skal der være
                // fri udsigt til det man læser eller skriver.
                var left = Width(0, y - half, x - gap, thickness);
                var right = Width(x + gap, y - half, view.Width - (x + gap), thickness);
                var top = Height(x - half, 0, thickness, y - gap);
                var bottom = Height(x - half, y + gap, thickness, view.Height - (y + gap));

                return new MarkerLayout(left, right, top, bottom, ring);
            }
        }
    }

    private static PixelRect Width(int x, int y, int width, int height)
        => width <= 0 ? PixelRect.Empty : new PixelRect(x, y, width, height);

    private static PixelRect Height(int x, int y, int width, int height)
        => height <= 0 ? PixelRect.Empty : new PixelRect(x, y, width, height);
}
