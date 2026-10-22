using Shouldly;
using Skaermlup.Core.Settings;

namespace Skaermlup.Tests;

public class StartupPlanTests
{
    private static AppSettings Settings(bool lensOnAtStart) => new() { StartMagnifierOnLaunch = lensOnAtStart };

    [Fact]
    public void Startet_af_Windows_uden_flueben_holder_luppen_slukket()
    {
        // Selve fejlen: luppen sprang frem ved hver logon, selvom brugeren ikke havde bedt om det.
        var plan = StartupPlan.From(["--start"], Settings(lensOnAtStart: false));

        plan.Kind.ShouldBe(LaunchKind.WithWindows);
        plan.TurnLensOn.ShouldBeFalse();
        plan.ShowControlPanel.ShouldBeFalse();
    }

    [Fact]
    public void Startet_af_Windows_med_flueben_taender_luppen()
    {
        var plan = StartupPlan.From(["--start"], Settings(lensOnAtStart: true));

        plan.TurnLensOn.ShouldBeTrue();
        plan.ShowControlPanel.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Startet_af_brugeren_viser_kontrolpanelet_og_foelger_fluebenet(bool lensOnAtStart)
    {
        var plan = StartupPlan.From([], Settings(lensOnAtStart));

        plan.Kind.ShouldBe(LaunchKind.Normal);
        plan.ShowControlPanel.ShouldBeTrue();
        plan.TurnLensOn.ShouldBe(lensOnAtStart);
    }

    [Fact]
    public void Proevebilledet_taender_altid_luppen_for_billedet_skal_vise_den()
    {
        var plan = StartupPlan.From(["--proevebillede=\"C:\\Støtte\\billeder\""], Settings(lensOnAtStart: false));

        plan.Kind.ShouldBe(LaunchKind.Sample);
        plan.TurnLensOn.ShouldBeTrue();
        plan.SampleFolder.ShouldBe("C:\\Støtte\\billeder");
    }

    [Fact]
    public void Argumenterne_er_ligeglade_med_store_og_smaa_bogstaver()
    {
        StartupPlan.From(["--START"], Settings(false)).Kind.ShouldBe(LaunchKind.WithWindows);
        StartupPlan.From(["--ProeveBillede=x"], Settings(false)).Kind.ShouldBe(LaunchKind.Sample);
    }

    [Fact]
    public void Ukendte_argumenter_giver_en_almindelig_start()
    {
        StartupPlan.From(["--noget-andet", "fil.txt"], Settings(false)).Kind.ShouldBe(LaunchKind.Normal);
    }

    [Fact]
    public void Et_proevebillede_uden_mappe_er_ikke_et_proevebillede()
    {
        StartupPlan.SampleFolderIn(["--proevebillede="]).ShouldBeNull();
        StartupPlan.SampleFolderIn(["--proevebillede=\"\""]).ShouldBeNull();
    }
}
