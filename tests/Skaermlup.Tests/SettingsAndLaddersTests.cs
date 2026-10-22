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

public class YellowTonesTests
{
    [Fact]
    public void Tonerne_gaar_fra_varmest_til_blegest()
    {
        // Blåt er det der adskiller en varm rav fra en bleg fløde, og det skal stige
        // hele vejen. Ellers ville minus- og plusknappen ikke svare til noget man kan se.
        for (int i = 1; i < YellowTones.Order.Count; i++)
        {
            var before = YellowTones.Rgb(YellowTones.Order[i - 1]);
            var now = YellowTones.Rgb(YellowTones.Order[i]);

            (now.Green + now.Blue).ShouldBeGreaterThan(before.Green + before.Blue);
        }
    }

    [Fact]
    public void Alle_toner_har_fuld_roed_saa_de_bliver_ved_med_at_vaere_gule()
    {
        foreach (YellowTone tone in Enum.GetValues<YellowTone>())
        {
            var (red, green, blue) = YellowTones.Rgb(tone);

            red.ShouldBe((byte)255);
            green.ShouldBeGreaterThan(blue);
        }
    }

    [Fact]
    public void Trinene_bliver_staaende_i_enderne_i_stedet_for_at_gaa_rundt()
    {
        YellowTones.Warmer(YellowTones.Order[0]).ShouldBe(YellowTones.Order[0]);
        YellowTones.Paler(YellowTones.Order[^1]).ShouldBe(YellowTones.Order[^1]);
    }

    [Fact]
    public void Et_trin_frem_og_et_tilbage_ender_samme_sted()
    {
        YellowTones.Warmer(YellowTones.Paler(YellowTone.Pure)).ShouldBe(YellowTone.Pure);
        YellowTones.Paler(YellowTones.Warmer(YellowTone.Pure)).ShouldBe(YellowTone.Pure);
    }

    [Fact]
    public void En_ukendt_vaerdi_lander_paa_den_rene_gule()
    {
        YellowTones.Paler((YellowTone)99).ShouldBe(YellowTone.Lemon);
    }

    [Fact]
    public void Alle_toner_har_et_dansk_navn()
    {
        foreach (YellowTone tone in Enum.GetValues<YellowTone>())
        {
            YellowTones.Name(tone).ShouldNotBeNullOrWhiteSpace();
            YellowTones.Description(tone).ShouldNotBeNullOrWhiteSpace();
        }
    }
}

public class SampleWarningTests
{
    [Fact]
    public void Advarslen_har_tekst_i_alle_felter()
    {
        SampleWarning.Title.ShouldNotBeNullOrWhiteSpace();
        SampleWarning.Headline.ShouldNotBeNullOrWhiteSpace();
        SampleWarning.Subheadline.ShouldNotBeNullOrWhiteSpace();
        SampleWarning.FolderHeading.ShouldNotBeNullOrWhiteSpace();
        SampleWarning.FileHeading.ShouldNotBeNullOrWhiteSpace();
        SampleWarning.NoFiles.ShouldNotBeNullOrWhiteSpace();
        SampleWarning.OpenFolder.ShouldNotBeNullOrWhiteSpace();
        SampleWarning.Close.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Advarslen_siger_hvad_billedet_indeholder_og_hvad_man_skal_goere()
    {
        // Den her advarsel har ét job. Bliver teksten skrevet om til noget venligt og vagt,
        // skal testen sige fra: den skal nævne skærmen, og den skal bede om at se efter.
        string alt = string.Join(' ', SampleWarning.Body);

        alt.ShouldContain("skærm", Case.Insensitive);
        alt.ShouldContain("send", Case.Insensitive);
        SampleWarning.Body.Count.ShouldBeGreaterThanOrEqualTo(2);
    }
}

public class ConfirmationsTests
{
    public static TheoryData<Confirmation> Alle =>
    [
        Confirmations.Reset,
        Confirmations.Quit(startsWithWindows: true),
        Confirmations.Quit(startsWithWindows: false),
    ];

    [Theory]
    [MemberData(nameof(Alle))]
    public void Hvert_spoergsmaal_har_tekst_og_to_forskellige_svar(Confirmation q)
    {
        q.Title.ShouldNotBeNullOrWhiteSpace();
        q.Body.ShouldNotBeEmpty();
        q.Keep.ShouldNotBeNullOrWhiteSpace();
        q.Proceed.ShouldNotBeNullOrWhiteSpace();

        // Knapperne skal kunne skelnes på ordene alene, ikke kun på farven.
        q.Keep.ShouldStartWith("Nej");
        q.Proceed.ShouldStartWith("Ja");
    }

    [Fact]
    public void Nulstil_siger_at_det_ikke_kan_fortrydes()
        => string.Join(' ', Confirmations.Reset.Body).ShouldContain("kan ikke fortrydes");

    [Fact]
    public void Afslut_fortaeller_hvordan_luppen_kommer_tilbage()
    {
        string medWindows = string.Join(' ', Confirmations.Quit(startsWithWindows: true).Body);
        string udenWindows = string.Join(' ', Confirmations.Quit(startsWithWindows: false).Body);

        medWindows.ShouldContain("computeren starter");
        udenWindows.ShouldNotContain("computeren starter");
        udenWindows.ShouldContain("Start-menuen");
    }
}
