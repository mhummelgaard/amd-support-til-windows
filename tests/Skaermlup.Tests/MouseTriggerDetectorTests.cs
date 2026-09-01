using Shouldly;
using Skaermlup.Core.Input;
using Skaermlup.Core.Settings;

namespace Skaermlup.Tests;

public class MouseTriggerDetectorTests
{
    private static MouseButtonEvent Down(MouseButton b, long t = 0) => new(b, true, t);
    private static MouseButtonEvent Up(MouseButton b, long t = 0) => new(b, false, t);

    [Fact]
    public void Midterknap_skifter_paa_tryk_ikke_paa_slip()
    {
        var d = new MouseTriggerDetector { Buttons = MouseTriggerButtons.Middle };

        d.Decide(Down(MouseButton.Middle)).Outcome.ShouldBe(TriggerOutcome.Toggle);
        d.Decide(Up(MouseButton.Middle)).Outcome.ShouldBe(TriggerOutcome.None);
    }

    [Fact]
    public void Udloeserklikket_holdes_tilbage_baade_paa_tryk_og_slip()
    {
        // Slippet skal med, ellers får programmet under musen et halvt klik.
        var d = new MouseTriggerDetector { Buttons = MouseTriggerButtons.Middle, BlockTriggerClicks = true };

        d.Decide(Down(MouseButton.Middle)).Swallow.ShouldBeTrue();
        d.Decide(Up(MouseButton.Middle)).Swallow.ShouldBeTrue();
    }

    [Fact]
    public void Klikket_sendes_videre_naar_brugeren_har_slaaet_blokering_fra()
    {
        var d = new MouseTriggerDetector { Buttons = MouseTriggerButtons.Middle, BlockTriggerClicks = false };
        d.Decide(Down(MouseButton.Middle)).Swallow.ShouldBeFalse();
    }

    [Fact]
    public void Knapper_der_ikke_er_valgt_roeres_ikke()
    {
        var d = new MouseTriggerDetector { Buttons = MouseTriggerButtons.Middle };

        d.Decide(Down(MouseButton.SideBack)).ShouldBe(TriggerDecision.Ignore);
        d.Decide(Down(MouseButton.SideForward)).ShouldBe(TriggerDecision.Ignore);
        d.Decide(Down(MouseButton.Left)).ShouldBe(TriggerDecision.Ignore);
    }

    [Fact]
    public void Hold_tilstand_taender_paa_tryk_og_slukker_paa_slip()
    {
        var d = new MouseTriggerDetector
        {
            Buttons = MouseTriggerButtons.SideBack,
            Mode = TriggerMode.Hold,
        };

        d.Decide(Down(MouseButton.SideBack)).Outcome.ShouldBe(TriggerOutcome.TurnOn);
        d.Decide(Up(MouseButton.SideBack)).Outcome.ShouldBe(TriggerOutcome.TurnOff);
    }

    [Fact]
    public void Venstre_og_hoejre_samtidig_taeller_som_et_greb()
    {
        var d = new MouseTriggerDetector { Buttons = MouseTriggerButtons.LeftRightChord };

        d.Decide(Down(MouseButton.Left, 1000)).Outcome.ShouldBe(TriggerOutcome.None);
        d.Decide(Down(MouseButton.Right, 1120)).Outcome.ShouldBe(TriggerOutcome.Toggle);
    }

    [Fact]
    public void Venstre_og_hoejre_langt_fra_hinanden_er_to_almindelige_klik()
    {
        var d = new MouseTriggerDetector { Buttons = MouseTriggerButtons.LeftRightChord, ChordWindowMs = 300 };

        d.Decide(Down(MouseButton.Left, 1000)).Outcome.ShouldBe(TriggerOutcome.None);
        d.Decide(Down(MouseButton.Right, 2000)).Outcome.ShouldBe(TriggerOutcome.None);
    }

    [Fact]
    public void Grebet_udloeser_kun_en_gang_selv_om_knapperne_holdes_nede()
    {
        var d = new MouseTriggerDetector { Buttons = MouseTriggerButtons.LeftRightChord };

        d.Decide(Down(MouseButton.Left, 0));
        d.Decide(Down(MouseButton.Right, 100)).Outcome.ShouldBe(TriggerOutcome.Toggle);
        d.Decide(Up(MouseButton.Right, 200));
        d.Decide(Down(MouseButton.Right, 250)).Outcome.ShouldBe(TriggerOutcome.None);
    }

    [Fact]
    public void Grebet_kan_bruges_igen_naar_begge_knapper_er_sluppet()
    {
        var d = new MouseTriggerDetector { Buttons = MouseTriggerButtons.LeftRightChord };

        d.Decide(Down(MouseButton.Left, 0));
        d.Decide(Down(MouseButton.Right, 50)).Outcome.ShouldBe(TriggerOutcome.Toggle);
        d.Decide(Up(MouseButton.Left, 200));
        d.Decide(Up(MouseButton.Right, 220));

        d.Decide(Down(MouseButton.Left, 500));
        d.Decide(Down(MouseButton.Right, 550)).Outcome.ShouldBe(TriggerOutcome.Toggle);
    }

    [Fact]
    public void Venstre_og_hoejre_knap_holdes_aldrig_tilbage()
    {
        // De to store knapper er for vigtige til at æde: gør vi det, kan brugeren ikke klikke.
        var d = new MouseTriggerDetector
        {
            Buttons = MouseTriggerButtons.LeftRightChord,
            BlockTriggerClicks = true,
        };

        d.Decide(Down(MouseButton.Left, 0)).Swallow.ShouldBeFalse();
        d.Decide(Down(MouseButton.Right, 50)).Swallow.ShouldBeFalse();
    }

    [Fact]
    public void Flere_valgte_knapper_virker_hver_for_sig()
    {
        var d = new MouseTriggerDetector
        {
            Buttons = MouseTriggerButtons.Middle | MouseTriggerButtons.SideForward,
        };

        d.Decide(Down(MouseButton.Middle)).Outcome.ShouldBe(TriggerOutcome.Toggle);
        d.Decide(Down(MouseButton.SideForward)).Outcome.ShouldBe(TriggerOutcome.Toggle);
    }
}
