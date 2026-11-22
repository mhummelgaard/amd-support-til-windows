using Shouldly;
using Skaermlup.Core.Settings;
using Skaermlup.Core.Text;

namespace Skaermlup.Tests;

public class MachinePolicyTests
{
    private static AppSettings Settings(bool lensOnAtStart) => new() { StartMagnifierOnLaunch = lensOnAtStart };

    private static readonly MachinePolicy Blocked = new(SampleImagesBlocked: true);

    // ---------- Hvad registreringsværdien betyder ----------

    [Fact]
    public void Uden_vaerdi_har_IT_ikke_bestemt_noget()
    {
        MachinePolicy.FromRegistry(null).SampleImagesBlocked.ShouldBeFalse();
        MachinePolicy.None.SampleImagesBlocked.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(-1)]
    public void Et_tal_forskelligt_fra_nul_slaar_proevebilleder_fra(int value)
    {
        // DWORD 1 er hvad gruppepolitikken skriver. Alt andet end 0 betyder det samme.
        MachinePolicy.FromRegistry(value).SampleImagesBlocked.ShouldBeTrue();
    }

    [Fact]
    public void Nul_tillader_dem_udtrykkeligt()
    {
        MachinePolicy.FromRegistry(0).SampleImagesBlocked.ShouldBeFalse();
        MachinePolicy.FromRegistry(0L).SampleImagesBlocked.ShouldBeFalse();
    }

    [Fact]
    public void QWORD_tolkes_som_DWORD()
    {
        MachinePolicy.FromRegistry(1L).SampleImagesBlocked.ShouldBeTrue();
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData(" 0 ", false)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("ja", true)]
    [InlineData("", true)]
    public void En_tekstvaerdi_fra_en_haandskrevet_reg_fil_slaar_fra_medmindre_den_siger_nej(string value, bool blocked)
    {
        // En forkert type i en .reg-fil må ikke åbne for det, IT ville lukke.
        MachinePolicy.FromRegistry(value).SampleImagesBlocked.ShouldBe(blocked);
    }

    [Fact]
    public void En_uventet_type_slaar_fra()
    {
        MachinePolicy.FromRegistry(new byte[] { 1, 0, 0, 0 }).SampleImagesBlocked.ShouldBeTrue();
        MachinePolicy.FromRegistry(new[] { "1" }).SampleImagesBlocked.ShouldBeTrue();
    }

    [Fact]
    public void Kan_politikken_ikke_laeses_holdes_proevebilleder_lukket()
    {
        MachinePolicy.Unreadable.SampleImagesBlocked.ShouldBeTrue();
    }

    [Fact]
    public void Politikken_ligger_hvor_kun_en_administrator_kan_skrive()
    {
        // Under HKEY_LOCAL_MACHINE\SOFTWARE\Policies — gruppepolitikkens eget sted. Flyttes den til
        // brugerens egen gren, kan brugeren slå den fra igen, og så er den ingenting værd.
        MachinePolicy.RegistryPath.ShouldBe(@"SOFTWARE\Policies\Skaermlup");
        MachinePolicy.SampleImagesValue.ShouldBe("DisableSampleImages");
    }

    // ---------- Hvad programmet gør ved det ----------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Slaaet_fra_taendes_luppen_ikke_og_mappen_glemmes(bool lensOnAtStart)
    {
        var plan = StartupPlan.From(["--proevebillede=C:\\Støtte"], Settings(lensOnAtStart), Blocked);

        plan.Kind.ShouldBe(LaunchKind.SampleBlocked);
        plan.TurnLensOn.ShouldBeFalse();
        plan.ShowControlPanel.ShouldBeFalse();
        plan.SampleFolder.ShouldBeNull();
    }

    [Fact]
    public void Politikken_roerer_ikke_en_almindelig_start()
    {
        StartupPlan.From([], Settings(true), Blocked).ShouldBe(StartupPlan.From([], Settings(true)));
        StartupPlan.From(["--start"], Settings(false), Blocked).ShouldBe(StartupPlan.From(["--start"], Settings(false)));
    }

    [Fact]
    public void Uden_politik_er_proevebilledet_som_foer()
    {
        StartupPlan.From(["--proevebillede=x"], Settings(false), MachinePolicy.None).Kind.ShouldBe(LaunchKind.Sample);
    }

    // ---------- Hvad brugeren får at vide ----------

    [Fact]
    public void Beskeden_har_tekst_i_alle_felter()
    {
        SampleWarning.BlockedTitle.ShouldNotBeNullOrWhiteSpace();
        SampleWarning.BlockedHeadline.ShouldNotBeNullOrWhiteSpace();
        SampleWarning.BlockedSubheadline.ShouldNotBeNullOrWhiteSpace();
        SampleWarning.BlockedBody.ShouldAllBe(line => !string.IsNullOrWhiteSpace(line));
    }

    [Fact]
    public void Beskeden_siger_at_intet_er_gemt_at_luppen_virker_og_hvem_man_spoerger()
    {
        // Tre ting, brugeren skal vide. Skrives beskeden om, skal de stadig stå der.
        string alt = string.Join(' ', SampleWarning.BlockedBody);

        alt.ShouldContain("ikke gemt", Case.Insensitive);
        alt.ShouldContain("Luppen virker", Case.Insensitive);
        alt.ShouldContain("IT-afdeling", Case.Insensitive);
        (SampleWarning.BlockedHeadline + SampleWarning.BlockedSubheadline).ShouldContain("IT", Case.Sensitive);
    }
}
