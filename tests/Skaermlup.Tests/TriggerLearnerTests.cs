using Shouldly;
using Skaermlup.Core.Input;
using Skaermlup.Core.Settings;

namespace Skaermlup.Tests;

public class TriggerLearnerTests
{
    private static MouseButtonEvent Down(MouseButton b, long t = 0) => new(b, IsDown: true, t);
    private static MouseButtonEvent Up(MouseButton b, long t = 50) => new(b, IsDown: false, t);

    [Theory]
    [InlineData(MouseButton.Middle, MouseTriggerButtons.Middle)]
    [InlineData(MouseButton.SideBack, MouseTriggerButtons.SideBack)]
    [InlineData(MouseButton.SideForward, MouseTriggerButtons.SideForward)]
    public void Et_tryk_paa_knappen_vaelger_den(MouseButton pressed, MouseTriggerButtons expected)
    {
        var learner = new TriggerLearner();

        learner.Observe(Down(pressed)).Learned.ShouldBeNull();
        learner.Observe(Up(pressed)).Learned.ShouldBe(expected);
    }

    [Fact]
    public void Baade_trykket_og_slippet_holdes_tilbage_fra_programmet_under_musen()
    {
        var learner = new TriggerLearner();

        learner.Observe(Down(MouseButton.Middle)).Swallow.ShouldBeTrue();
        learner.Observe(Up(MouseButton.Middle)).Swallow.ShouldBeTrue();
    }

    [Theory]
    [InlineData(MouseButton.Left)]
    [InlineData(MouseButton.Right)]
    public void Venstre_og_hoejre_gaar_uaendret_igennem(MouseButton button)
    {
        // Det er dem brugeren klikker med — også på knappen, der startede valget.
        var learner = new TriggerLearner();

        learner.Observe(Down(button)).ShouldBe(new LearnStep(null, Swallow: false));
        learner.Observe(Up(button)).ShouldBe(new LearnStep(null, Swallow: false));
    }

    [Fact]
    public void Et_slip_uden_det_tryk_der_hoerte_til_vaelger_ikke_noget()
    {
        // Midterste knap blev trykket ned, før valget startede, og slippes nu.
        var learner = new TriggerLearner();

        learner.Observe(Up(MouseButton.Middle)).Learned.ShouldBeNull();
    }

    [Fact]
    public void Skifter_brugeren_knap_midt_i_trykket_er_det_slippet_der_taeller()
    {
        var learner = new TriggerLearner();
        learner.Observe(Down(MouseButton.Middle));
        learner.Observe(Down(MouseButton.SideBack));

        learner.Observe(Up(MouseButton.Middle)).Learned.ShouldBeNull();
        learner.Observe(Up(MouseButton.SideBack)).Learned.ShouldBe(MouseTriggerButtons.SideBack);
    }
}
