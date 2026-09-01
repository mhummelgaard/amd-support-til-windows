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
}
