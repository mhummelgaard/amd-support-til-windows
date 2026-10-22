using Shouldly;
using Skaermlup.Core.Geometry;
using Skaermlup.Core.Input;

namespace Skaermlup.Tests;

public class FollowResolverTests
{
    private static readonly PixelRect Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void Foelger_musen_som_udgangspunkt()
    {
        var r = new FollowResolver();

        r.Resolve(new PixelPoint(100, 100), null);
        r.Resolve(new PixelPoint(400, 300), null).ShouldBe(new PixelPoint(400, 300));
        r.Source.ShouldBe(FocusSource.Mouse);
    }

    [Fact]
    public void Tekstmarkoeren_overtager_naar_der_skrives()
    {
        var r = new FollowResolver { FollowCaret = true };
        r.Resolve(new PixelPoint(100, 100), null);

        var focus = r.Resolve(new PixelPoint(100, 100), new PixelRect(500, 200, 2, 24));

        focus.ShouldBe(new PixelPoint(501, 212));
        r.Source.ShouldBe(FocusSource.Caret);
    }

    [Fact]
    public void Smaa_rystelser_i_haanden_stjaeler_ikke_styringen_fra_tekstmarkoeren()
    {
        var r = new FollowResolver { MouseWakeDistancePx = 8, FollowCaret = true };
        var mouse = new PixelPoint(100, 100);

        r.Resolve(mouse, null);
        r.Resolve(mouse, new PixelRect(700, 400, 2, 20));
        r.Source.ShouldBe(FocusSource.Caret);

        r.Resolve(new PixelPoint(102, 101), new PixelRect(700, 400, 2, 20));
        r.Source.ShouldBe(FocusSource.Caret);
    }

    [Fact]
    public void Musen_tager_altid_styringen_tilbage()
    {
        var r = new FollowResolver { FollowCaret = true };
        r.Resolve(new PixelPoint(100, 100), null);
        r.Resolve(new PixelPoint(100, 100), new PixelRect(500, 200, 2, 24));

        var focus = r.Resolve(new PixelPoint(900, 700), new PixelRect(500, 200, 2, 24));

        focus.ShouldBe(new PixelPoint(900, 700));
        r.Source.ShouldBe(FocusSource.Mouse);
    }

    [Fact]
    public void Tekstmarkoeren_kan_slaas_fra()
    {
        var r = new FollowResolver { FollowCaret = false };
        r.Resolve(new PixelPoint(100, 100), null);

        r.Resolve(new PixelPoint(100, 100), new PixelRect(500, 200, 2, 24));
        r.Source.ShouldBe(FocusSource.Mouse);
    }

    [Fact]
    public void Luppen_bliver_staaende_naar_markoeren_holder_stille()
    {
        var r = new FollowResolver { FollowCaret = true };
        r.Resolve(new PixelPoint(100, 100), null);
        var first = r.Resolve(new PixelPoint(100, 100), new PixelRect(500, 200, 2, 24));

        r.Resolve(new PixelPoint(100, 100), new PixelRect(500, 200, 2, 24)).ShouldBe(first);
    }

    [Fact]
    public void Musen_tager_over_naar_tekstmarkoeren_forsvinder()
    {
        var r = new FollowResolver { FollowCaret = true };
        r.Resolve(new PixelPoint(100, 100), null);
        r.Resolve(new PixelPoint(100, 100), new PixelRect(500, 200, 2, 24));

        r.Resolve(new PixelPoint(100, 100), null);
        r.Source.ShouldBe(FocusSource.Mouse);
    }

    [Fact]
    public void Piletasterne_kan_flytte_luppen_helt_uden_mus()
    {
        var r = new FollowResolver();
        r.Resolve(new PixelPoint(500, 500), null);

        var moved = r.PanBy(60, 0, Screen);

        moved.ShouldBe(new PixelPoint(560, 500));
        r.Source.ShouldBe(FocusSource.Keyboard);
    }

    [Fact]
    public void Tastaturstyringen_bliver_paa_skaermen()
    {
        var r = new FollowResolver();
        r.Resolve(new PixelPoint(10, 10), null);

        r.PanBy(-500, -500, Screen).ShouldBe(new PixelPoint(0, 0));
    }

    [Fact]
    public void Tastaturstyringen_holder_indtil_musen_flyttes_rigtigt()
    {
        var r = new FollowResolver { MouseWakeDistancePx = 8 };
        r.Resolve(new PixelPoint(500, 500), null);
        r.PanBy(100, 0, Screen);

        r.Resolve(new PixelPoint(502, 500), null).ShouldBe(new PixelPoint(600, 500));
        r.Resolve(new PixelPoint(800, 500), null).ShouldBe(new PixelPoint(800, 500));
        r.Source.ShouldBe(FocusSource.Mouse);
    }

    // ---------- Tastaturets fokus ----------

    private static readonly PixelPoint StillMouse = new(100, 100);
    private static readonly PixelRect ButtonA = new(800, 600, 120, 40);
    private static readonly PixelRect ButtonB = new(800, 700, 120, 40);

    private static FollowResolver Started(PixelRect? focus = null)
    {
        var r = new FollowResolver();
        r.Resolve(StillMouse, null, focus);
        return r;
    }

    [Fact]
    public void Tab_til_en_knap_flytter_luppen_til_knappen()
    {
        var r = Started(ButtonA);

        r.Resolve(StillMouse, null, ButtonB).ShouldBe(ButtonB.Center);
        r.Source.ShouldBe(FocusSource.Focus);
    }

    [Fact]
    public void Det_felt_der_havde_fokus_da_luppen_blev_taendt_er_ikke_en_bevaegelse()
    {
        var r = Started(ButtonA);

        r.Resolve(StillMouse, null, ButtonA).ShouldBe(StillMouse);
        r.Source.ShouldBe(FocusSource.Mouse);
    }

    [Fact]
    public void Luppen_bliver_ved_feltet_saa_laenge_fokus_bliver_der()
    {
        var r = Started(ButtonA);
        r.Resolve(StillMouse, null, ButtonB);

        r.Resolve(StillMouse, null, ButtonB).ShouldBe(ButtonB.Center);
        r.Resolve(StillMouse, null, ButtonB).ShouldBe(ButtonB.Center);
    }

    [Fact]
    public void Et_klik_flytter_fokus_men_ikke_luppen()
    {
        // Musen står inde i knappen, der får fokus: det er musen der har klikket.
        var mouseOnB = new PixelPoint(830, 715);
        var r = new FollowResolver();
        r.Resolve(mouseOnB, null, ButtonA);

        r.Resolve(mouseOnB, null, ButtonB).ShouldBe(mouseOnB);
        r.Source.ShouldBe(FocusSource.Mouse);
    }

    [Fact]
    public void Musen_tager_styringen_tilbage_fra_fokus()
    {
        var r = Started(ButtonA);
        r.Resolve(StillMouse, null, ButtonB);

        var moved = new PixelPoint(300, 300);
        r.Resolve(moved, null, ButtonB).ShouldBe(moved);
        r.Source.ShouldBe(FocusSource.Mouse);
    }

    [Fact]
    public void Et_fokusskift_som_musen_overtrumfede_springer_ikke_frem_bagefter()
    {
        var r = Started(ButtonA);

        // Musen flytter sig i samme øjeblik som fokus skifter. Musen vinder.
        var moved = new PixelPoint(300, 300);
        r.Resolve(moved, null, ButtonB).ShouldBe(moved);

        // Næste omgang er fokus uændret — luppen skal ikke pludselig hoppe til knappen.
        r.Resolve(moved, null, ButtonB).ShouldBe(moved);
    }

    [Fact]
    public void Skrivemarkoeren_vinder_over_feltet_den_staar_i()
    {
        var field = new PixelRect(500, 200, 400, 30);
        var caret = new PixelRect(620, 205, 2, 20);
        var r = Started(ButtonA);

        r.Resolve(StillMouse, caret, field).ShouldBe(caret.Center);
        r.Source.ShouldBe(FocusSource.Caret);
    }

    [Fact]
    public void Fra_et_tekstfelt_med_Tab_til_en_knap_foelger_luppen_knappen()
    {
        var field = new PixelRect(500, 200, 400, 30);
        var caret = new PixelRect(620, 205, 2, 20);
        var r = Started(ButtonA);
        r.Resolve(StillMouse, caret, field);

        // Markøren forsvinder, og fokus står nu på knappen.
        r.Resolve(StillMouse, null, ButtonB).ShouldBe(ButtonB.Center);
        r.Source.ShouldBe(FocusSource.Focus);
    }

    [Fact]
    public void Slaaet_fra_foelges_fokus_ikke()
    {
        var r = new FollowResolver { FollowFocus = false };
        r.Resolve(StillMouse, null, ButtonA);

        r.Resolve(StillMouse, null, ButtonB).ShouldBe(StillMouse);
    }
}

public class FocusFilterTests
{
    private static readonly PixelRect Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void Et_helt_vindue_er_ikke_til_hjaelp()
        => FocusFilter.Useful(new PixelRect(0, 0, 1900, 1000), Screen).ShouldBeNull();

    [Fact]
    public void En_knap_er()
        => FocusFilter.Useful(new PixelRect(800, 600, 120, 40), Screen).ShouldNotBeNull();

    [Fact]
    public void En_lang_raekke_i_en_liste_er_ogsaa()
        => FocusFilter.Useful(new PixelRect(0, 300, 1800, 32), Screen).ShouldNotBeNull();

    [Fact]
    public void I_en_lang_raekke_kigges_der_naer_begyndelsen_hvor_teksten_er()
    {
        var row = new PixelRect(100, 300, 1600, 30);
        var target = FocusFilter.Target(row);

        target.X.ShouldBe(100 + 30 * 4);
        target.Y.ShouldBe(315);
    }

    [Fact]
    public void En_almindelig_knap_ses_fra_midten()
    {
        var button = new PixelRect(800, 600, 120, 40);
        FocusFilter.Target(button).ShouldBe(button.Center);
    }
}
