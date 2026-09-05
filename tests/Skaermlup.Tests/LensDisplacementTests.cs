using Shouldly;
using Skaermlup.Core.Geometry;
using Skaermlup.Core.Magnification;

namespace Skaermlup.Tests;

public class LensDisplacementTests
{
    private static readonly PixelRect Screen = new(0, 0, 1920, 1080);

    /// <summary>Start-menuen i Windows 11: et rektangel midt for neden.</summary>
    private static readonly PixelRect StartMenu = new(640, 330, 640, 700);

    [Fact]
    public void Ingen_overlap_flytter_ingenting()
    {
        var view = new PixelRect(100, 100, 400, 300);

        LensDisplacement.MoveClear(view, StartMenu, Screen).ShouldBe(view);
    }

    [Fact]
    public void Luppen_flyttes_fri_af_Start_menuen()
    {
        var view = new PixelRect(700, 500, 500, 400);

        var moved = LensDisplacement.MoveClear(view, StartMenu, Screen);

        moved.Overlaps(StartMenu).ShouldBeFalse();
        moved.Size.ShouldBe(view.Size);
    }

    [Fact]
    public void Luppen_bliver_paa_skaermen()
    {
        var view = new PixelRect(700, 500, 500, 400);

        var moved = LensDisplacement.MoveClear(view, StartMenu, Screen);

        moved.X.ShouldBeGreaterThanOrEqualTo(Screen.X);
        moved.Y.ShouldBeGreaterThanOrEqualTo(Screen.Y);
        moved.Right.ShouldBeLessThanOrEqualTo(Screen.Right);
        moved.Bottom.ShouldBeLessThanOrEqualTo(Screen.Bottom);
    }

    /// <summary>
    /// Ligger luppen mest til venstre for menuen, skal den ud til venstre. Den mindste flytning
    /// er den mindst forvirrende — billedet dukker op lige dér hvor øjet i forvejen er.
    /// </summary>
    [Fact]
    public void Luppen_vaelger_den_naermeste_side()
    {
        var view = new PixelRect(500, 500, 400, 300);

        var moved = LensDisplacement.MoveClear(view, StartMenu, Screen);

        moved.Right.ShouldBeLessThanOrEqualTo(StartMenu.X);
    }

    [Fact]
    public void Ligger_luppen_mest_til_hoejre_gaar_den_til_hoejre()
    {
        var view = new PixelRect(1100, 500, 400, 300);

        var moved = LensDisplacement.MoveClear(view, StartMenu, Screen);

        moved.X.ShouldBeGreaterThanOrEqualTo(StartMenu.Right);
    }

    /// <summary>Proceslinjen fylder hele skærmens bredde, så der er kun vejen opad.</summary>
    [Fact]
    public void En_flade_i_fuld_bredde_skubber_luppen_opad()
    {
        var taskbar = new PixelRect(0, 1032, 1920, 48);
        var view = new PixelRect(700, 900, 500, 180);

        var moved = LensDisplacement.MoveClear(view, taskbar, Screen);

        moved.Overlaps(taskbar).ShouldBeFalse();
        moved.Bottom.ShouldBeLessThanOrEqualTo(taskbar.Y);
    }

    /// <summary>Meddelelsescenteret ligger i højre side; luppen skal til venstre for det.</summary>
    [Fact]
    public void Meddelelsescenteret_skubber_luppen_til_venstre()
    {
        var centre = new PixelRect(1520, 40, 400, 990);
        var view = new PixelRect(1400, 300, 480, 360);

        var moved = LensDisplacement.MoveClear(view, centre, Screen);

        moved.Overlaps(centre).ShouldBeFalse();
        moved.Right.ShouldBeLessThanOrEqualTo(centre.X);
    }

    /// <summary>
    /// Er luppen så stor at den ikke kan være ved siden af, må den blive hvor den er frem for
    /// at hoppe hen et sted hvor den er lige så dækket.
    /// </summary>
    [Fact]
    public void Er_der_ingen_plads_bliver_luppen_staaende()
    {
        var everything = new PixelRect(0, 0, 1920, 1080);
        var view = new PixelRect(200, 200, 1500, 700);

        LensDisplacement.MoveClear(view, everything, Screen).ShouldBe(view);
    }

    [Fact]
    public void Rektangler_ved_om_de_roerer_hinanden()
    {
        var a = new PixelRect(0, 0, 100, 100);

        a.Overlaps(new PixelRect(50, 50, 100, 100)).ShouldBeTrue();

        // Kanten er eksklusiv: to rektangler kant mod kant deler ingen pixel.
        a.Overlaps(new PixelRect(100, 0, 100, 100)).ShouldBeFalse();
        a.Overlaps(new PixelRect(0, 100, 100, 100)).ShouldBeFalse();
        a.Overlaps(PixelRect.Empty).ShouldBeFalse();
    }

    [Fact]
    public void Det_delte_areal_regnes_rigtigt()
    {
        var a = new PixelRect(0, 0, 100, 100);

        a.OverlapArea(new PixelRect(50, 50, 100, 100)).ShouldBe(2500);
        a.OverlapArea(new PixelRect(200, 200, 10, 10)).ShouldBe(0);
        a.OverlapArea(a).ShouldBe(10000);
    }

    [Fact]
    public void Foreningen_rummer_dem_begge()
    {
        var a = new PixelRect(10, 10, 40, 40);
        var b = new PixelRect(100, 80, 20, 20);

        a.Union(b).ShouldBe(new PixelRect(10, 10, 110, 90));
        a.Union(PixelRect.Empty).ShouldBe(a);
        PixelRect.Empty.Union(b).ShouldBe(b);
    }

    [Fact]
    public void Tomme_rektangler_flytter_ingenting()
    {
        var view = new PixelRect(700, 500, 400, 300);

        LensDisplacement.MoveClear(view, PixelRect.Empty, Screen).ShouldBe(view);
        LensDisplacement.MoveClear(view, StartMenu, PixelRect.Empty).ShouldBe(view);
        LensDisplacement.MoveClear(PixelRect.Empty, StartMenu, Screen).ShouldBe(PixelRect.Empty);
    }

    /// <summary>
    /// Størrelsen må aldrig ændre sig. Kilden er den samme, så et vindue i en anden størrelse
    /// ville strække billedet.
    /// </summary>
    [Theory]
    [InlineData(700, 500, 500, 400)]
    [InlineData(300, 900, 900, 200)]
    [InlineData(1200, 100, 600, 600)]
    public void Stoerrelsen_er_uroert(int x, int y, int w, int h)
    {
        var view = new PixelRect(x, y, w, h);

        LensDisplacement.MoveClear(view, StartMenu, Screen).Size.ShouldBe(view.Size);
    }
}
