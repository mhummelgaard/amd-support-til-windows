using Shouldly;
using Skaermlup.Core.Geometry;
using Skaermlup.Core.Input;
using Skaermlup.Core.Settings;
using Skaermlup.Core.Text;

namespace Skaermlup.Tests;

public class InputSignatureTests
{
    [Fact]
    public void Den_maalte_vaerdi_fra_en_finger_er_touch()
        // Målt på Windows 11: et tryk med fingeren giver præcis den her.
        => InputSignature.IsTouch(0xFF515782).ShouldBeTrue();

    [Fact]
    public void En_pen_er_ikke_en_finger_men_heller_ikke_en_mus()
    {
        InputSignature.IsTouch(0xFF515700).ShouldBeFalse();
        InputSignature.IsPenOrTouch(0xFF515700).ShouldBeTrue();
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(0x12345678UL)]
    [InlineData(0xFF515600UL)]
    public void En_rigtig_mus_er_hverken_finger_eller_pen(ulong extra)
    {
        InputSignature.IsTouch(extra).ShouldBeFalse();
        InputSignature.IsPenOrTouch(extra).ShouldBeFalse();
    }

    [Fact]
    public void Kun_de_nederste_32_bit_taeller()
        => InputSignature.IsTouch(0xFFFFFFFF_FF515782UL).ShouldBeTrue();
}

public class PointerFilterTests
{
    private static readonly PixelPoint Start = new(500, 400);

    [Fact]
    public void En_rigtig_mus_flytter_luppen()
    {
        var f = new PointerFilter();
        f.Update(Start, nowMs: 1_000, lastTouchMs: 0);

        f.Update(new PixelPoint(600, 450), nowMs: 1_016, lastTouchMs: 0).ShouldBe(new PixelPoint(600, 450));
    }

    [Fact]
    public void Et_tryk_med_fingeren_flytter_ikke_luppen()
    {
        // Fingeren trykkede på en knap; Windows flyttede musepilen derhen. Luppen bliver.
        var f = new PointerFilter();
        f.Update(Start, 1_000, 0);

        f.Update(new PixelPoint(1500, 900), nowMs: 2_010, lastTouchMs: 2_000).ShouldBe(Start);
    }

    [Fact]
    public void Musen_tager_over_igen_naar_den_bevaeger_sig_bagefter()
    {
        var f = new PointerFilter();
        f.Update(Start, 1_000, 0);
        f.Update(new PixelPoint(1500, 900), 2_010, 2_000);

        // Et sekund senere flytter musen sig fra trykkets plads.
        f.Update(new PixelPoint(1490, 905), nowMs: 3_000, lastTouchMs: 2_000).ShouldBe(new PixelPoint(1490, 905));
    }

    [Fact]
    public void Staar_musepilen_stille_efter_et_tryk_sker_der_intet()
    {
        var f = new PointerFilter();
        f.Update(Start, 1_000, 0);
        f.Update(new PixelPoint(1500, 900), 2_010, 2_000);

        // Pilen står stadig dér, hvor fingeren slap — det er ikke en ny bevægelse.
        f.Update(new PixelPoint(1500, 900), nowMs: 9_000, lastTouchMs: 2_000).ShouldBe(Start);
    }
}

public class FollowResolverTouchTests
{
    private static readonly PixelPoint Mouse = new(100, 100);

    [Fact]
    public void Traek_i_luppen_flytter_den_dertil()
    {
        var r = new FollowResolver();
        r.Resolve(Mouse, null);

        r.PlaceAt(new PixelPoint(800, 300), Mouse);

        r.Resolve(Mouse, null).ShouldBe(new PixelPoint(800, 300));
        r.Source.ShouldBe(FocusSource.Touch);
    }

    [Fact]
    public void Taendt_med_fingeren_starter_luppen_dér_og_ikke_ved_musen()
    {
        var r = new FollowResolver();

        r.PlaceAt(new PixelPoint(960, 540), mouseNow: Mouse);

        r.Resolve(Mouse, null).ShouldBe(new PixelPoint(960, 540));
    }

    [Fact]
    public void Feltet_med_fokus_da_luppen_blev_taendt_med_fingeren_er_ikke_en_bevaegelse()
    {
        var field = new PixelRect(1200, 800, 200, 40);
        var r = new FollowResolver();
        r.PlaceAt(new PixelPoint(960, 540), Mouse);

        r.Resolve(Mouse, null, field).ShouldBe(new PixelPoint(960, 540));
        r.Source.ShouldBe(FocusSource.Touch);
    }

    [Fact]
    public void En_rigtig_mus_tager_luppen_tilbage_fra_fingeren()
    {
        var r = new FollowResolver();
        r.Resolve(Mouse, null);
        r.PlaceAt(new PixelPoint(800, 300), Mouse);

        var moved = new PixelPoint(140, 120);
        r.Resolve(moved, null).ShouldBe(moved);
        r.Source.ShouldBe(FocusSource.Mouse);
    }
}

public class TouchSettingsTests
{
    [Fact]
    public void Automatisk_viser_knapperne_kun_paa_en_beroeringsskaerm()
    {
        var s = new AppSettings();

        s.ShowTouchControls.ShouldBeNull();
        s.TouchControlsWanted(hasTouchScreen: true).ShouldBeTrue();
        s.TouchControlsWanted(hasTouchScreen: false).ShouldBeFalse();
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void Brugerens_eget_valg_gaelder_over_det_automatiske(bool chosen, bool hasTouch, bool expected)
        => new AppSettings { ShowTouchControls = chosen }.TouchControlsWanted(hasTouch).ShouldBe(expected);

    [Fact]
    public void En_ukendt_side_bliver_til_hoejre()
        => new AppSettings { TouchControlsSide = (ScreenSide)9 }.Normalize().TouchControlsSide.ShouldBe(ScreenSide.Right);
}

public class TouchTextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Med_fingeren_naevner_vejledningen_hverken_museknap_eller_hjul(bool on)
    {
        var settings = new AppSettings();

        Instructions.CallToAction(on, settings, touch: true).ShouldNotContain("museknap");
        foreach (var chip in Instructions.Bar(on, settings, touch: true))
        {
            chip.Action.ShouldNotContain("museknap");
            chip.Action.ShouldNotContain("hjul");
        }
    }

    [Fact]
    public void Med_fingeren_kan_vejledningen_stadig_staa_med_stor_skrift()
    {
        var chips = Instructions.Bar(magnifierOn: true, new AppSettings(), touch: true);

        chips.Count.ShouldBeLessThanOrEqualTo(5);
        foreach (var chip in chips) chip.Action.Length.ShouldBeLessThanOrEqualTo(40);
    }

    [Fact]
    public void Den_fulde_vejledning_har_et_afsnit_om_fingeren()
        => Instructions.FullGuide(new AppSettings()).ShouldContain(s => s.Heading == "Med fingeren");

    [Fact]
    public void Oplaeser_kan_sige_hvad_LUP_knappen_goer()
    {
        TouchText.PowerName(magnifierOn: false).ShouldBe("Tænd luppen");
        TouchText.PowerName(magnifierOn: true).ShouldBe("Sluk luppen");
    }
}
