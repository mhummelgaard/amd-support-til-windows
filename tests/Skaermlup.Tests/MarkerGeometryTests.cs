using Shouldly;
using Skaermlup.Core.Geometry;
using Skaermlup.Core.Magnification;
using Skaermlup.Core.Settings;

namespace Skaermlup.Tests;

public class MarkerGeometryTests
{
    private static readonly PixelSize Lens = new(800, 600);
    private static readonly PixelPoint Centre = new(400, 300);

    private static MarkerLayout Compute(MarkerStyle style, int gap = 30, PixelPoint? at = null)
        => MarkerGeometry.Compute(Lens, at ?? Centre, style, gap, 11, 26, 90);

    [Fact]
    public void Krydset_naar_ikke_sammen_paa_midten()
    {
        // Hullet er hele pointen: teksten under markøren skal kunne læses.
        var layout = Compute(MarkerStyle.Crosshair, gap: 30);

        layout.Left.Right.ShouldBe(Centre.X - 30);
        layout.Right.X.ShouldBe(Centre.X + 30);
        layout.Top.Bottom.ShouldBe(Centre.Y - 30);
        layout.Bottom.Y.ShouldBe(Centre.Y + 30);
    }

    [Fact]
    public void Krydset_naar_ud_til_luppens_kanter()
    {
        var layout = Compute(MarkerStyle.Crosshair);

        layout.Left.X.ShouldBe(0);
        layout.Right.Right.ShouldBe(Lens.Width);
        layout.Top.Y.ShouldBe(0);
        layout.Bottom.Bottom.ShouldBe(Lens.Height);
    }

    [Fact]
    public void Hullet_vokser_naar_musen_bevaeger_sig()
    {
        int still = MarkerGeometry.GapFor(30, 0);
        int slow = MarkerGeometry.GapFor(30, 300);
        int fast = MarkerGeometry.GapFor(30, 2000);

        still.ShouldBe(30);
        slow.ShouldBeGreaterThan(still);
        fast.ShouldBeGreaterThan(slow);
    }

    [Fact]
    public void Hullet_vokser_ikke_i_det_uendelige()
    {
        MarkerGeometry.GapFor(30, 100000).ShouldBeLessThanOrEqualTo(MarkerGeometry.MaximumGap);
        MarkerGeometry.GapFor(1, 0).ShouldBeGreaterThanOrEqualTo(MarkerGeometry.MinimumGap);
    }

    [Fact]
    public void Understregningen_ligger_under_punktet_ikke_hen_over_det()
    {
        var layout = Compute(MarkerStyle.Underline);

        layout.Left.Y.ShouldBeGreaterThan(Centre.Y);
        layout.Left.Width.ShouldBe(Lens.Width);
        layout.Top.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Understregningen_holdes_inde_i_luppen()
    {
        // Står markøren nede i bunden, må stregen ikke havne uden for billedet.
        var layout = MarkerGeometry.Compute(Lens, new PixelPoint(400, 595), MarkerStyle.Underline,
            30, 11, 26, 400);

        layout.Left.Bottom.ShouldBeLessThanOrEqualTo(Lens.Height);
    }

    [Fact]
    public void Ringen_staar_om_punktet()
    {
        var layout = Compute(MarkerStyle.Ring);

        layout.Ring.Center.X.ShouldBeInRange(Centre.X - 1, Centre.X + 1);
        layout.Ring.Center.Y.ShouldBeInRange(Centre.Y - 1, Centre.Y + 1);
        layout.Left.IsEmpty.ShouldBeTrue();
        layout.Top.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Ingen_markering_tegner_ingenting()
    {
        Compute(MarkerStyle.None).ShouldBe(MarkerLayout.Empty);
    }

    [Fact]
    public void Markoeren_helt_ude_i_hjoernet_giver_ingen_negative_streger()
    {
        var layout = MarkerGeometry.Compute(Lens, new PixelPoint(0, 0), MarkerStyle.Crosshair,
            30, 11, 26, 90);

        layout.Left.IsEmpty.ShouldBeTrue();
        layout.Top.IsEmpty.ShouldBeTrue();
        layout.Right.Width.ShouldBeGreaterThan(0);
        layout.Bottom.Height.ShouldBeGreaterThan(0);
    }
}
