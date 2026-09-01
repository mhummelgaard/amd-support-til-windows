using Shouldly;
using Skaermlup.Core.Geometry;
using Skaermlup.Core.Magnification;
using Skaermlup.Core.Settings;

namespace Skaermlup.Tests;

public class MagnifierGeometryTests
{
    private static readonly PixelRect FullHd = new(0, 0, 1920, 1080);

    [Fact]
    public void Lens_i_midten_af_skaermen_ligger_om_fokuspunktet()
    {
        var frame = MagnifierGeometry.ComputeLensFrame(
            new PixelPoint(960, 540), FullHd, new PixelSize(800, 600), 4);

        frame.View.ShouldBe(new PixelRect(560, 240, 800, 600));
        frame.Source.ShouldBe(new PixelRect(860, 465, 200, 150));
        frame.ScaleX.ShouldBe(4, 0.001);
        frame.ScaleY.ShouldBe(4, 0.001);
    }

    [Fact]
    public void Markoeren_staar_i_midten_naar_der_er_plads_hele_vejen_rundt()
    {
        var frame = MagnifierGeometry.ComputeLensFrame(
            new PixelPoint(960, 540), FullHd, new PixelSize(800, 600), 4);

        frame.Marker.X.ShouldBeInRange(frame.View.Width / 2 - 2, frame.View.Width / 2 + 2);
        frame.Marker.Y.ShouldBeInRange(frame.View.Height / 2 - 2, frame.View.Height / 2 + 2);
    }

    [Fact]
    public void Lup_og_kilde_holdes_inde_paa_skaermen_i_hjoernet()
    {
        var frame = MagnifierGeometry.ComputeLensFrame(
            new PixelPoint(0, 0), FullHd, new PixelSize(800, 600), 4);

        frame.View.X.ShouldBe(0);
        frame.View.Y.ShouldBe(0);
        frame.Source.X.ShouldBe(0);
        frame.Source.Y.ShouldBe(0);
        frame.Source.Right.ShouldBeLessThanOrEqualTo(FullHd.Right);
        frame.Source.Bottom.ShouldBeLessThanOrEqualTo(FullHd.Bottom);
    }

    [Fact]
    public void Markoeren_flytter_med_ud_i_hjoernet_i_stedet_for_at_blive_i_midten()
    {
        // Klemt op i hjørnet falder lup og kilde ikke længere sammen om musen, og
        // markøren skal derfor tegnes oppe i hjørnet — ikke midt i luppen.
        var frame = MagnifierGeometry.ComputeLensFrame(
            new PixelPoint(0, 0), FullHd, new PixelSize(800, 600), 4);

        frame.Marker.X.ShouldBe(0);
        frame.Marker.Y.ShouldBe(0);
    }

    [Fact]
    public void Skaerm_med_negativ_placering_haandteres()
    {
        var left = new PixelRect(-1920, -200, 1920, 1080);
        var frame = MagnifierGeometry.ComputeLensFrame(
            new PixelPoint(-1000, 300), left, new PixelSize(600, 400), 5);

        frame.View.X.ShouldBeGreaterThanOrEqualTo(left.X);
        frame.View.Right.ShouldBeLessThanOrEqualTo(left.Right);
        frame.Source.X.ShouldBeGreaterThanOrEqualTo(left.X);
        frame.Source.Bottom.ShouldBeLessThanOrEqualTo(left.Bottom);
    }

    [Fact]
    public void Lup_stoerre_end_skaermen_klippes_til_skaermen()
    {
        var tiny = new PixelRect(0, 0, 320, 240);
        var size = MagnifierGeometry.LensSizeFor(tiny, 100, 100, LensShape.Rectangle);

        size.Width.ShouldBe(320);
        size.Height.ShouldBe(240);
    }

    [Fact]
    public void Cirkelformet_lup_er_lige_bred_og_hoej()
    {
        var size = MagnifierGeometry.LensSizeFor(FullHd, 60, 40, LensShape.Circle);
        size.Width.ShouldBe(size.Height);
    }

    [Fact]
    public void Laesebaand_ligger_i_toppen_og_fylder_hele_bredden()
    {
        var frame = MagnifierGeometry.ComputeDockFrame(
            new PixelPoint(960, 800), FullHd, DockEdge.Top, 30, 4);

        frame.View.X.ShouldBe(0);
        frame.View.Y.ShouldBe(0);
        frame.View.Width.ShouldBe(1920);
        frame.View.Height.ShouldBe(324);
    }

    [Fact]
    public void Laesebaand_laeser_aldrig_fra_sig_selv()
    {
        // Musen står oppe i båndets eget område: kilden skal alligevel hentes nedenfor båndet,
        // ellers ville luppen forstørre sit eget billede i det uendelige.
        var frame = MagnifierGeometry.ComputeDockFrame(
            new PixelPoint(960, 20), FullHd, DockEdge.Top, 30, 4);

        frame.Source.Y.ShouldBeGreaterThanOrEqualTo(frame.View.Bottom);
    }

    [Fact]
    public void Laesebaand_i_bunden_laeser_ovenover_sig_selv()
    {
        var frame = MagnifierGeometry.ComputeDockFrame(
            new PixelPoint(960, 1070), FullHd, DockEdge.Bottom, 30, 4);

        frame.View.Bottom.ShouldBe(1080);
        frame.Source.Bottom.ShouldBeLessThanOrEqualTo(frame.View.Y);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(4)]
    [InlineData(20)]
    public void Kilden_er_altid_mindre_end_visningen(double zoom)
    {
        var frame = MagnifierGeometry.ComputeLensFrame(
            new PixelPoint(500, 500), FullHd, new PixelSize(700, 500), zoom);

        frame.Source.Width.ShouldBeLessThan(frame.View.Width);
        frame.Source.Height.ShouldBeLessThan(frame.View.Height);
        frame.Source.Width.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Tom_skaerm_giver_et_tomt_billede_i_stedet_for_en_fejl()
    {
        var frame = MagnifierGeometry.ComputeLensFrame(
            PixelPoint.Zero, PixelRect.Empty, new PixelSize(400, 300), 4);

        frame.IsEmpty.ShouldBeTrue();
    }
}
