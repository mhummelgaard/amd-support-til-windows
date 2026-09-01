using Shouldly;
using Skaermlup.Core.Colors;
using Skaermlup.Core.Settings;

namespace Skaermlup.Tests;

public class ColorPipelineTests
{
    /// <summary>Ét billede på 2x2 pixels: hvid, sort, rød, blå.</summary>
    private static byte[] Sample() =>
    [
        255, 255, 255, 0,   0, 0, 0, 0,
        0, 0, 255, 0,       255, 0, 0, 0,
    ];

    private static (byte B, byte G, byte R) PixelAt(byte[] buffer, int index)
        => (buffer[index * 4], buffer[index * 4 + 1], buffer[index * 4 + 2]);

    private static byte[] Run(ColorScheme scheme, int contrast = 0, int brightness = 0)
    {
        var src = Sample();
        var dst = new byte[src.Length];
        ColorPipeline.Apply(src, 8, dst, 8, 2, 2, ColorMap.Build(scheme, contrast, brightness));
        return dst;
    }

    [Fact]
    public void Normale_farver_uden_justering_lader_billedet_vaere()
    {
        ColorMap.Build(ColorScheme.Normal, 0, 0).IsIdentity.ShouldBeTrue();
        Run(ColorScheme.Normal).ShouldBe(Sample());
    }

    [Fact]
    public void Omvendte_farver_bytter_hvidt_og_sort()
    {
        var result = Run(ColorScheme.Inverted);

        PixelAt(result, 0).ShouldBe(((byte)0, (byte)0, (byte)0));
        PixelAt(result, 1).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [Fact]
    public void Graatoner_giver_ens_vaerdier_i_alle_tre_kanaler()
    {
        var result = Run(ColorScheme.Grayscale);
        var red = PixelAt(result, 2);

        red.B.ShouldBe(red.G);
        red.G.ShouldBe(red.R);
    }

    [Fact]
    public void Gul_paa_sort_har_altid_nul_i_den_blaa_kanal()
    {
        var result = Run(ColorScheme.YellowOnBlack);

        for (int i = 0; i < 4; i++) PixelAt(result, i).B.ShouldBe((byte)0);
    }

    [Fact]
    public void Gul_paa_sort_goer_hvid_bund_sort_og_sort_tekst_gul()
    {
        var result = Run(ColorScheme.YellowOnBlack);

        PixelAt(result, 0).ShouldBe(((byte)0, (byte)0, (byte)0));
        PixelAt(result, 1).ShouldBe(((byte)0, (byte)255, (byte)255));
    }

    [Fact]
    public void Sort_paa_gul_vender_den_anden_vej()
    {
        var result = Run(ColorScheme.BlackOnYellow);

        PixelAt(result, 0).ShouldBe(((byte)0, (byte)255, (byte)255));
        PixelAt(result, 1).ShouldBe(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void Hvid_paa_sort_holder_graatonerne_men_vender_dem()
    {
        var result = Run(ColorScheme.WhiteOnBlack);

        PixelAt(result, 0).ShouldBe(((byte)0, (byte)0, (byte)0));
        PixelAt(result, 1).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [Fact]
    public void Kraftig_kontrast_traekker_lyse_og_moerke_toner_fra_hinanden()
    {
        var map = ColorMap.Build(ColorScheme.HighContrast, 0, 0);

        map.Red[200].ShouldBeGreaterThan((byte)200);
        map.Red[60].ShouldBeLessThan((byte)60);
        map.IsIdentity.ShouldBeFalse();
    }

    [Fact]
    public void Alfakanalen_saettes_saa_billedet_ikke_bliver_gennemsigtigt()
    {
        var result = Run(ColorScheme.Inverted);

        for (int i = 0; i < 4; i++) result[i * 4 + 3].ShouldBe((byte)255);
    }
}
