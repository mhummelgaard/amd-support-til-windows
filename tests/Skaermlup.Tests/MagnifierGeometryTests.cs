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

        // Luppen ligger en anelse forskudt af midten med vilje: den lægges dér hvor den
        // forstørrede udgave af pixlen under musen havner præcis under musepilen.
        frame.View.ShouldBe(new PixelRect(558, 238, 800, 600));
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
    public void Luppen_holdes_inde_paa_skaermen_i_hjoernet()
    {
        // Selve vinduet skal blive på skærmen — en lup der hænger halvt udenfor hjælper ingen.
        var frame = MagnifierGeometry.ComputeLensFrame(
            new PixelPoint(0, 0), FullHd, new PixelSize(800, 600), 4);

        frame.View.X.ShouldBe(0);
        frame.View.Y.ShouldBe(0);
        frame.View.Right.ShouldBeLessThanOrEqualTo(FullHd.Right);
        frame.View.Bottom.ShouldBeLessThanOrEqualTo(FullHd.Bottom);
    }

    [Fact]
    public void Kilden_maa_gerne_raekke_ud_over_skaermkanten()
    {
        // Det modsatte af vinduet: kilden klemmes ikke ind. Gjorde den det, ville musen ikke
        // længere stå i midten af det udsnit der læses, og det man peger på ville glide ud
        // mod luppens kant. Det der ikke findes uden for skærmen, fyldes med en neutral flade.
        var frame = MagnifierGeometry.ComputeLensFrame(
            new PixelPoint(0, 0), FullHd, new PixelSize(800, 600), 4);

        frame.Source.X.ShouldBeLessThan(0);
        frame.Source.Y.ShouldBeLessThan(0);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1919, 0)]
    [InlineData(0, 1079)]
    [InlineData(1919, 1079)]
    [InlineData(960, 3)]
    [InlineData(960, 1077)]
    public void Det_man_peger_paa_bliver_i_midten_ogsaa_ude_ved_kanten(int x, int y)
    {
        // Det her er hele pointen. Før klemte kilden sig ind på skærmen, og så gled det man
        // pegede på ud mod luppens kant — helt ude ved syv-otte pixels fra rammen, hvor det
        // forsvandt bag den. Med en stor lup begyndte det allerede en tredjedel inde.
        var lens = new PixelSize(900, 500);
        var frame = MagnifierGeometry.ComputeLensFrame(new PixelPoint(x, y), FullHd, lens, 4);

        frame.Marker.X.ShouldBeInRange(lens.Width / 2 - 3, lens.Width / 2 + 3);
        frame.Marker.Y.ShouldBeInRange(lens.Height / 2 - 3, lens.Height / 2 + 3);
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

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(10)]
    [InlineData(20)]
    public void Musepilen_peger_paa_det_den_ligger_paa(double zoom)
    {
        // Det vigtigste af det hele: den forstørrede udgave af pixlen under musen skal ligge
        // præcis dér hvor musepilen står. Ellers peger pilen ved siden af — og fejlen vokser
        // med forstørrelsen, fordi én kildepixel bliver mange pixels bred på skærmen.
        var focus = new PixelPoint(960, 540);
        var frame = MagnifierGeometry.ComputeLensFrame(focus, FullHd, new PixelSize(900, 500), zoom);

        (frame.View.X + frame.Marker.X).ShouldBeInRange(focus.X - 1, focus.X + 1);
        (frame.View.Y + frame.Marker.Y).ShouldBeInRange(focus.Y - 1, focus.Y + 1);
    }

    [Fact]
    public void Hullet_til_museklik_ligger_hvor_musen_er()
    {
        var focus = new PixelPoint(700, 400);
        var frame = MagnifierGeometry.ComputeLensFrame(focus, FullHd, new PixelSize(900, 500), 8);

        (frame.View.X + frame.HotSpot.X).ShouldBe(focus.X);
        (frame.View.Y + frame.HotSpot.Y).ShouldBe(focus.Y);
    }

    [Fact]
    public void Hullet_foelger_med_naar_luppen_klemmes_op_i_hjoernet()
    {
        // Klemt op mod kanten falder luppens midte og musen ikke længere sammen, men hullet
        // skal stadig ligge nøjagtigt under musen, ellers kan brugeren ikke klikke.
        var focus = new PixelPoint(20, 15);
        var frame = MagnifierGeometry.ComputeLensFrame(focus, FullHd, new PixelSize(900, 500), 8);

        (frame.View.X + frame.HotSpot.X).ShouldBe(focus.X);
        (frame.View.Y + frame.HotSpot.Y).ShouldBe(focus.Y);
    }
}
