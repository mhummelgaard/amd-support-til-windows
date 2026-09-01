using Shouldly;
using Skaermlup.Core.Colors;
using Skaermlup.Core.Magnification;
using Skaermlup.Core.Settings;
using Skaermlup.Core.Text;

namespace Skaermlup.Tests;

public class ZoomLadderTests
{
    [Fact]
    public void Trinene_gaar_kun_opad()
    {
        for (int i = 1; i < ZoomLadder.Steps.Count; i++)
            ZoomLadder.Steps[i].ShouldBeGreaterThan(ZoomLadder.Steps[i - 1]);
    }

    [Fact]
    public void Naeste_og_forrige_trin_finder_naboen()
    {
        ZoomLadder.Next(4).ShouldBe(5);
        ZoomLadder.Previous(4).ShouldBe(3);
    }

    [Fact]
    public void Der_er_bund_og_loft()
    {
        ZoomLadder.Previous(ZoomLadder.Min).ShouldBe(ZoomLadder.Min);
        ZoomLadder.Next(ZoomLadder.Max).ShouldBe(ZoomLadder.Max);
        ZoomLadder.Clamp(500).ShouldBe(ZoomLadder.Max);
        ZoomLadder.Clamp(double.NaN).ShouldBe(ZoomLadder.Min);
    }

    [Fact]
    public void En_vaerdi_mellem_to_trin_hopper_op_paa_stigen()
    {
        ZoomLadder.Snap(3.9).ShouldBe(4);
        ZoomLadder.Next(3.9).ShouldBe(4);
    }

    [Fact]
    public void Forstoerrelsen_skrives_med_dansk_komma()
    {
        ZoomLadder.Format(4).ShouldBe("4x");
        ZoomLadder.Format(2.5).ShouldBe("2,5x");
    }
}

public class LensSizerTests
{
    [Fact]
    public void Luppen_vokser_og_holder_facon()
    {
        var (w, h) = LensSizer.Resize(40, 30, 10);

        w.ShouldBe(50);
        h.ShouldBe(38);
    }

    [Fact]
    public void Begge_sider_stopper_naar_den_ene_rammer_loftet()
    {
        var (w, h) = LensSizer.Resize(90, 100, 20);

        w.ShouldBeLessThanOrEqualTo(LensSizer.MaxPercent);
        h.ShouldBe(LensSizer.MaxPercent);
    }

    [Fact]
    public void Luppen_kan_ikke_krympe_vaek()
    {
        var (w, h) = LensSizer.Resize(15, 12, -50);

        w.ShouldBeGreaterThanOrEqualTo(LensSizer.MinPercent);
        h.ShouldBeGreaterThanOrEqualTo(LensSizer.MinPercent);
    }
}

public class ColorSchemesTests
{
    [Fact]
    public void Farverne_bladres_rundt_i_ring()
    {
        var scheme = ColorSchemes.Order[^1];
        ColorSchemes.Next(scheme).ShouldBe(ColorSchemes.Order[0]);
    }

    [Fact]
    public void Alle_farvetilstande_har_et_dansk_navn()
    {
        foreach (ColorScheme scheme in Enum.GetValues<ColorScheme>())
        {
            ColorSchemes.Name(scheme).ShouldNotBeNullOrWhiteSpace();
            ColorSchemes.Description(scheme).ShouldNotBeNullOrWhiteSpace();
        }
    }
}
