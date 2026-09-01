using System.Globalization;

namespace Skaermlup.Core.Magnification;

/// <summary>
/// Faste forstørrelsestrin. En stige frem for et frit tal, fordi trinene skal kunne rammes
/// med ét tastetryk eller ét hjulklik, og fordi "4 gange" er nemmere at holde styr på end
/// et vilkårligt decimaltal.
/// </summary>
public static class ZoomLadder
{
    public static IReadOnlyList<double> Steps { get; } =
        [1.5, 2, 2.5, 3, 4, 5, 6, 8, 10, 12, 16, 20];

    public static double Min => Steps[0];
    public static double Max => Steps[^1];

    public static double Clamp(double zoom)
        => double.IsNaN(zoom) ? Steps[0] : Math.Clamp(zoom, Min, Max);

    /// <summary>Indekset for det trin der ligger tættest på den givne forstørrelse.</summary>
    private static int NearestIndex(double zoom)
    {
        zoom = Clamp(zoom);
        int best = 0;
        double bestDiff = double.MaxValue;
        for (int i = 0; i < Steps.Count; i++)
        {
            double diff = Math.Abs(Steps[i] - zoom);
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }
        return best;
    }

    /// <summary>Nærmeste trin på stigen.</summary>
    public static double Snap(double zoom) => Steps[NearestIndex(zoom)];

    public static double Next(double zoom)
    {
        zoom = Clamp(zoom);
        foreach (double step in Steps)
            if (step > zoom + 1e-9) return step;
        return Max;
    }

    public static double Previous(double zoom)
    {
        zoom = Clamp(zoom);
        for (int i = Steps.Count - 1; i >= 0; i--)
            if (Steps[i] < zoom - 1e-9) return Steps[i];
        return Min;
    }

    /// <summary>Læsbar tekst til skærmen, fx "4x" eller "2,5x" med dansk decimalkomma.</summary>
    public static string Format(double zoom)
    {
        zoom = Clamp(zoom);
        var da = CultureInfo.GetCultureInfo("da-DK");
        return Math.Abs(zoom - Math.Round(zoom)) < 1e-9
            ? Math.Round(zoom).ToString("0", da) + "x"
            : zoom.ToString("0.0", da) + "x";
    }
}
