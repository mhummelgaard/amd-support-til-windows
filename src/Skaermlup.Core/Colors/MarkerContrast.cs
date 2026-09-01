namespace Skaermlup.Core.Colors;

/// <summary>
/// Vælger om markeringen skal være lys eller mørk.
///
/// En fast farve virker ikke: gult ses tydeligt på en mørk baggrund og forsvinder næsten på en
/// hvid side. Markeringen skal kunne ses uanset hvad den ligger oven på, så farven vælges efter
/// hvor lyst der er under den. Sammen med den modsatfarvede kant om hver streg betyder det, at
/// markeringen står frem både på hvidt papir, på sort baggrund og hen over et billede.
/// </summary>
public static class MarkerContrast
{
    public const int BytesPerPixel = 4;

    /// <summary>
    /// Under dette lysstyrkeniveau skiftes til en lys markering. Der er luft mellem de to
    /// grænser med vilje: uden den ville markeringen skifte farve frem og tilbage, hver gang
    /// musen bevægede sig hen over noget der lå lige på vippen.
    /// </summary>
    private const int BecomeLightBelow = 90;

    /// <summary>Over dette niveau skiftes tilbage til en mørk markering.</summary>
    private const int StayLightBelow = 150;

    /// <summary>Sandt hvis markeringen skal tegnes lys, fordi det den dækker er mørkt.</summary>
    /// <param name="wasLight">Hvad der blev valgt sidste gang. Holder valget roligt.</param>
    public static bool PrefersLightMarker(
        ReadOnlySpan<byte> pixels, int stride, int width, int height, bool wasLight)
    {
        if (width <= 0 || height <= 0) return wasLight;

        int average = AverageLuma(pixels, stride, width, height);
        return wasLight ? average < StayLightBelow : average < BecomeLightBelow;
    }

    /// <summary>
    /// Gennemsnitlig opfattet lysstyrke, målt på et gitter af stikprøver. Markeringens streger
    /// går hele vejen gennem luppen, så det er hele billedet der tæller — ikke kun midten.
    /// </summary>
    public static int AverageLuma(ReadOnlySpan<byte> pixels, int stride, int width, int height)
    {
        if (width <= 0 || height <= 0) return 0;

        const int samplesPerAxis = 12;
        int stepX = Math.Max(1, width / samplesPerAxis);
        int stepY = Math.Max(1, height / samplesPerAxis);

        long total = 0;
        int count = 0;

        for (int y = 0; y < height; y += stepY)
        {
            int row = y * stride;
            for (int x = 0; x < width; x += stepX)
            {
                int offset = row + x * BytesPerPixel;
                if (offset + 2 >= pixels.Length) continue;

                total += (pixels[offset + 2] * 77 + pixels[offset + 1] * 150 + pixels[offset] * 29) >> 8;
                count++;
            }
        }

        return count == 0 ? 0 : (int)(total / count);
    }
}
