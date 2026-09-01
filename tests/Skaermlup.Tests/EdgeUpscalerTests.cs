using Shouldly;
using Skaermlup.Core.Magnification;

namespace Skaermlup.Tests;

public class EdgeUpscalerTests
{
    private const int Bytes = EdgeUpscaler.BytesPerPixel;

    /// <summary>Et billede hvor venstre halvdel er sort og højre halvdel hvid.</summary>
    private static byte[] HardEdge(int width, int height)
    {
        var pixels = new byte[width * height * Bytes];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte value = x < width / 2 ? (byte)0 : (byte)255;
                int p = (y * width + x) * Bytes;
                pixels[p] = value;
                pixels[p + 1] = value;
                pixels[p + 2] = value;
            }
        }

        return pixels;
    }

    private static byte[] Solid(int width, int height, byte blue, byte green, byte red)
    {
        var pixels = new byte[width * height * Bytes];
        for (int i = 0; i < width * height; i++)
        {
            pixels[i * Bytes] = blue;
            pixels[i * Bytes + 1] = green;
            pixels[i * Bytes + 2] = red;
        }

        return pixels;
    }

    private static byte[] Upscale(byte[] source, int width, int height, int outWidth, int outHeight, double zoom)
    {
        var destination = new byte[outWidth * outHeight * Bytes];
        new EdgeUpscaler().Upscale(
            source, width * Bytes, width, height,
            destination, outWidth * Bytes, outWidth, outHeight,
            EdgeUpscaler.SteepnessFor(zoom));
        return destination;
    }

    [Fact]
    public void Stejlheden_foelger_forstoerrelsen()
    {
        // Målet er en kant på cirka to skærmpixels uanset hvor meget der forstørres.
        EdgeUpscaler.SteepnessFor(10).ShouldBe(5);
        EdgeUpscaler.SteepnessFor(4).ShouldBe(2);
    }

    [Fact]
    public void Stejlheden_har_bund_og_loft()
    {
        EdgeUpscaler.SteepnessFor(1.5).ShouldBe(1);
        EdgeUpscaler.SteepnessFor(20).ShouldBe(8);
    }

    [Fact]
    public void Der_arbejdes_ikke_ved_lav_forstoerrelse()
    {
        // Under tre gange er overgangen stadig smal, og grafikkortet klarer det billigere.
        EdgeUpscaler.IsWorthwhile(2).ShouldBeFalse();
        EdgeUpscaler.IsWorthwhile(3).ShouldBeTrue();
        EdgeUpscaler.IsWorthwhile(10).ShouldBeTrue();
    }

    [Fact]
    public void En_skarp_kant_bliver_ved_med_at_vaere_skarp()
    {
        var result = Upscale(HardEdge(8, 4), 8, 4, 80, 40, zoom: 10);

        int middleRow = 20 * 80 * Bytes;
        int blurred = 0;
        for (int x = 0; x < 80; x++)
        {
            byte value = result[middleRow + x * Bytes];
            if (value > 20 && value < 235) blurred++;
        }

        // Almindelig udglatning ville give omkring ti udflydende kolonner her.
        blurred.ShouldBeLessThanOrEqualTo(3);
    }

    [Fact]
    public void Kanten_bliver_liggende_hvor_den_var()
    {
        var result = Upscale(HardEdge(8, 4), 8, 4, 80, 40, zoom: 10);

        int middleRow = 20 * 80 * Bytes;
        int firstLight = -1;
        for (int x = 0; x < 80 && firstLight < 0; x++)
            if (result[middleRow + x * Bytes] > 128) firstLight = x;

        // Kilden skifter midt i billedet, så det skal resultatet også.
        firstLight.ShouldBeInRange(36, 44);
    }

    [Fact]
    public void En_ensfarvet_flade_faar_hverken_pletter_eller_kanter()
    {
        var result = Upscale(Solid(6, 6, 40, 90, 160), 6, 6, 60, 60, zoom: 10);

        for (int i = 0; i < 60 * 60; i++)
        {
            result[i * Bytes].ShouldBe((byte)40);
            result[i * Bytes + 1].ShouldBe((byte)90);
            result[i * Bytes + 2].ShouldBe((byte)160);
        }
    }

    [Fact]
    public void Alfakanalen_saettes_saa_billedet_ikke_bliver_gennemsigtigt()
    {
        var result = Upscale(HardEdge(8, 4), 8, 4, 80, 40, zoom: 10);

        for (int i = 0; i < 80 * 40; i++) result[i * Bytes + 3].ShouldBe((byte)255);
    }

    [Fact]
    public void En_for_lille_malbuffer_afvises_i_stedet_for_at_skrive_uden_for()
    {
        var source = HardEdge(8, 4);
        var tooSmall = new byte[10];

        Should.Throw<ArgumentException>(() => new EdgeUpscaler().Upscale(
            source, 8 * Bytes, 8, 4, tooSmall, 80 * Bytes, 80, 40, 5));
    }

    [Fact]
    public void Tomt_billede_giver_ingen_fejl()
    {
        Should.NotThrow(() => new EdgeUpscaler().Upscale(
            [], 0, 0, 0, [], 0, 0, 0, 5));
    }

    [Fact]
    public void Samme_opskalering_kan_koeres_igen_med_en_anden_stoerrelse()
    {
        // Buffere genbruges mellem billeder, så et skift i lupstørrelse må ikke give rester.
        var upscaler = new EdgeUpscaler();
        var big = new byte[120 * 60 * Bytes];
        upscaler.Upscale(HardEdge(12, 6), 12 * Bytes, 12, 6, big, 120 * Bytes, 120, 60, 5);

        var small = new byte[40 * 20 * Bytes];
        Should.NotThrow(() => upscaler.Upscale(HardEdge(8, 4), 8 * Bytes, 8, 4, small, 40 * Bytes, 40, 20, 5));
    }
}
