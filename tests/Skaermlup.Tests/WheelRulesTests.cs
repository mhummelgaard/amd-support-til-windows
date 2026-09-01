using Shouldly;
using Skaermlup.Core.Input;
using Skaermlup.Core.Settings;

namespace Skaermlup.Tests;

public class WheelRulesTests
{
    [Fact]
    public void Hjulet_alene_ruller_som_normalt()
    {
        // Ellers kunne man ikke læse en lang side færdig med luppen tændt.
        var d = WheelRules.Decide(120, ctrl: false, shift: false, magnifierOn: true, WheelAction.ModifierOnly);

        d.Outcome.ShouldBe(WheelOutcome.None);
        d.Swallow.ShouldBeFalse();
    }

    [Fact]
    public void Ctrl_og_hjul_aendrer_forstoerrelsen_og_naar_ikke_ud_til_programmet_under()
    {
        var d = WheelRules.Decide(120, ctrl: true, shift: false, magnifierOn: true, WheelAction.ModifierOnly);

        d.Outcome.ShouldBe(WheelOutcome.ZoomIn);
        d.Swallow.ShouldBeTrue();
    }

    [Fact]
    public void Skift_og_hjul_aendrer_lupstoerrelsen()
    {
        WheelRules.Decide(-120, ctrl: false, shift: true, magnifierOn: true, WheelAction.ModifierOnly)
            .Outcome.ShouldBe(WheelOutcome.LensSmaller);
    }

    [Fact]
    public void Ctrl_vinder_over_skift_naar_begge_holdes_nede()
    {
        WheelRules.Decide(120, ctrl: true, shift: true, magnifierOn: true, WheelAction.ModifierOnly)
            .Outcome.ShouldBe(WheelOutcome.ZoomIn);
    }

    [Fact]
    public void Slukket_lup_roerer_ikke_hjulet()
    {
        WheelRules.Decide(120, ctrl: true, shift: false, magnifierOn: false, WheelAction.AlwaysZoom)
            .ShouldBe(WheelDecision.Ignore);
    }

    [Fact]
    public void Hjulet_alene_kan_saettes_til_at_forstoerre()
    {
        var d = WheelRules.Decide(-120, ctrl: false, shift: false, magnifierOn: true, WheelAction.AlwaysZoom);

        d.Outcome.ShouldBe(WheelOutcome.ZoomOut);
        d.Swallow.ShouldBeTrue();
    }
}
