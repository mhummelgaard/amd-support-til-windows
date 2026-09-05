using Shouldly;
using Skaermlup.Core.Input;
using Skaermlup.Core.Settings;

namespace Skaermlup.Tests;

public class PointerRulesTests
{
    private static PointerVisual Decide(
        bool lensIsOn = true,
        MarkerStyle marker = MarkerStyle.Crosshair,
        bool shellInFront = false,
        bool ringWanted = true,
        bool ringPossible = true)
        => PointerRules.Decide(lensIsOn, marker, shellInFront, ringWanted, ringPossible);

    [Fact]
    public void Slukket_lup_roerer_ikke_brugerens_markoerer()
    {
        Decide(lensIsOn: false).ShouldBe(PointerVisual.System);
        Decide(lensIsOn: false, shellInFront: true).ShouldBe(PointerVisual.System);
        Decide(lensIsOn: false, marker: MarkerStyle.None).ShouldBe(PointerVisual.System);
    }

    [Theory]
    [InlineData(MarkerStyle.Crosshair)]
    [InlineData(MarkerStyle.Underline)]
    [InlineData(MarkerStyle.Ring)]
    public void Taendt_lup_skjuler_pilen_saa_der_ikke_er_to_markoerer(MarkerStyle marker)
        => Decide(marker: marker).ShouldBe(PointerVisual.Hidden);

    [Fact]
    public void Har_brugeren_valgt_musens_egen_pil_bliver_den_staaende()
        => Decide(marker: MarkerStyle.None).ShouldBe(PointerVisual.System);

    /// <summary>
    /// Det her er hele pointen: bag Start-menuen kan luppen ikke ses, og uden den her regel
    /// ville brugeren hverken have lup eller markør.
    /// </summary>
    [Fact]
    public void Shell_flade_henter_markoeren_frem_med_ring()
        => Decide(shellInFront: true).ShouldBe(PointerVisual.HighlightRing);

    [Fact]
    public void Uden_plads_til_ringen_vises_den_almindelige_pil()
        => Decide(shellInFront: true, ringPossible: false).ShouldBe(PointerVisual.System);

    [Fact]
    public void Ringen_kan_slaas_fra_og_saa_kommer_den_almindelige_pil_frem()
        => Decide(shellInFront: true, ringWanted: false).ShouldBe(PointerVisual.System);

    /// <summary>Bag skallen er markøren det eneste der er, uanset hvad luppen ellers tegner.</summary>
    [Fact]
    public void Shell_flade_gaar_forud_for_markeringsvalget()
    {
        Decide(shellInFront: true, marker: MarkerStyle.None).ShouldBe(PointerVisual.HighlightRing);
        Decide(shellInFront: true, marker: MarkerStyle.Ring).ShouldBe(PointerVisual.HighlightRing);
    }

    [Fact]
    public void Markoeren_bliver_aldrig_skjult_naar_skallen_er_fremme()
    {
        foreach (var marker in Enum.GetValues<MarkerStyle>())
        foreach (bool wanted in new[] { true, false })
        foreach (bool possible in new[] { true, false })
        {
            Decide(shellInFront: true, marker: marker, ringWanted: wanted, ringPossible: possible)
                .ShouldNotBe(PointerVisual.Hidden);
        }
    }
}
