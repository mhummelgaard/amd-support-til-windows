using Shouldly;
using Skaermlup.Core.Colors;

namespace Skaermlup.Tests;

public class MarkerContrastTests
{
    private const int Bytes = MarkerContrast.BytesPerPixel;

    private static byte[] Flat(byte value, int width = 40, int height = 40)
    {
        var pixels = new byte[width * height * Bytes];
        for (int i = 0; i < width * height; i++)
        {
            pixels[i * Bytes] = value;
            pixels[i * Bytes + 1] = value;
            pixels[i * Bytes + 2] = value;
        }

        return pixels;
    }

    [Fact]
    public void Moerk_baggrund_giver_lys_markering()
    {
        MarkerContrast.PrefersLightMarker(Flat(20), 40 * Bytes, 40, 40, wasLight: false).ShouldBeTrue();
    }

    [Fact]
    public void Lys_baggrund_giver_moerk_markering()
    {
        MarkerContrast.PrefersLightMarker(Flat(240), 40 * Bytes, 40, 40, wasLight: true).ShouldBeFalse();
    }

    [Fact]
    public void Farven_skifter_ikke_frem_og_tilbage_paa_grænsen()
    {
        // Midt imellem lyst og mørkt skal valget blive stående, ellers ville markeringen
        // blinke hver gang musen bevægede sig hen over noget der lå lige på vippen.
        var midway = Flat(120);

        MarkerContrast.PrefersLightMarker(midway, 40 * Bytes, 40, 40, wasLight: true).ShouldBeTrue();
        MarkerContrast.PrefersLightMarker(midway, 40 * Bytes, 40, 40, wasLight: false).ShouldBeFalse();
    }

    [Fact]
    public void Lysstyrken_maales_over_hele_billedet()
    {
        var pixels = Flat(0, 40, 40);
        for (int i = 0; i < 40 * 40 / 2; i++)
        {
            pixels[i * Bytes] = 255;
            pixels[i * Bytes + 1] = 255;
            pixels[i * Bytes + 2] = 255;
        }

        MarkerContrast.AverageLuma(pixels, 40 * Bytes, 40, 40).ShouldBeInRange(100, 155);
    }

    [Fact]
    public void Tomt_billede_beholder_det_forrige_valg()
    {
        MarkerContrast.PrefersLightMarker([], 0, 0, 0, wasLight: true).ShouldBeTrue();
        MarkerContrast.PrefersLightMarker([], 0, 0, 0, wasLight: false).ShouldBeFalse();
    }
}
