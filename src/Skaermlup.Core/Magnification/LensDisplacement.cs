using Skaermlup.Core.Geometry;

namespace Skaermlup.Core.Magnification;

/// <summary>
/// Flytter lupvinduet fri af Start-menuen og de andre flader Windows lægger oven på alting.
///
/// Luppen er ikke skjult på hele skærmen — kun dér hvor fladen faktisk ligger. Start-menuen
/// fylder et rektangel midt for neden, meddelelsescenteret et i højre side. Uden for det
/// rektangel er luppen fuldt synlig som altid. Flyttes vinduet derhen, kan brugeren stadig
/// se det forstørrede billede af dét musen står på — det står bare ved siden af, over eller
/// under menuen i stedet for oven på den.
///
/// Kun selve visningen flyttes. Kilden — det stykke skærm der læses — bliver liggende ved
/// musen, så indholdet er det samme; det er kun rammen der er rykket. Derfor kan flytningen
/// laves som en ren forskydning af rektanglet, og forstørrelsen og markeringens plads inde i
/// billedet er uberørt.
/// </summary>
public static class LensDisplacement
{
    /// <summary>
    /// Visningen flyttet fri af <paramref name="obstacle"/> og holdt inden for
    /// <paramref name="monitor"/>. Er der ikke overlap i forvejen, gives visningen uændret
    /// tilbage. Er der ingen plads nogen steder, vælges den placering der dækkes mindst.
    /// </summary>
    public static PixelRect MoveClear(PixelRect view, PixelRect obstacle, PixelRect monitor)
    {
        if (view.IsEmpty || obstacle.IsEmpty || monitor.IsEmpty) return view;
        if (!view.Overlaps(obstacle)) return view;

        // De fire naturlige udveje: til venstre, til højre, over og under fladen. Rækkefølgen
        // betyder ikke noget — der vælges bagefter efter hvor lidt vinduet skal flytte sig.
        Span<PixelRect> candidates =
        [
            new PixelRect(obstacle.X - view.Width, view.Y, view.Width, view.Height),
            new PixelRect(obstacle.Right, view.Y, view.Width, view.Height),
            new PixelRect(view.X, obstacle.Y - view.Height, view.Width, view.Height),
            new PixelRect(view.X, obstacle.Bottom, view.Width, view.Height),
        ];

        // Udgangspunktet er at blive stående. En placering skal dække mindre end det for at
        // vinde — ellers flytter luppen sig uden at brugeren får noget ud af det.
        var best = view;
        long bestOverlap = view.OverlapArea(obstacle);
        int bestDistance = 0;

        foreach (var candidate in candidates)
        {
            // Skærmkanten går forud: et vindue halvt uden for skærmen hjælper ingen.
            var placed = candidate.ClampInto(monitor);
            long overlap = placed.OverlapArea(obstacle);

            // Hvor langt vinduet skal flytte sig. Den mindste flytning er den mindst forvirrende:
            // billedet dukker op lige ved siden af, hvor øjet i forvejen er.
            int distance = placed.Center.DistanceSquaredTo(view.Center);

            if (overlap < bestOverlap || (overlap == bestOverlap && distance < bestDistance))
            {
                best = placed;
                bestOverlap = overlap;
                bestDistance = distance;
            }
        }

        return best;
    }
}
