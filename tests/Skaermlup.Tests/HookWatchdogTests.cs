using Shouldly;
using Skaermlup.Core.Geometry;
using Skaermlup.Core.Input;

namespace Skaermlup.Tests;

public class HookWatchdogTests
{
    private static readonly PixelPoint Here = new(100, 100);
    private static readonly PixelPoint There = new(400, 250);

    [Fact]
    public void Det_foerste_tjek_slaar_aldrig_alarm()
    {
        // Der er intet at sammenligne med endnu.
        new HookWatchdog().Check(There, lastHookEventMs: 0, nowMs: 2_000).ShouldBeFalse();
    }

    [Fact]
    public void En_mus_der_ligger_stille_er_ikke_et_tegn_paa_noget()
    {
        var vagt = new HookWatchdog();
        vagt.Check(Here, 0, 2_000);

        // Ingen bevægelse og intet hørt i lang tid: brugeren læser bare.
        vagt.Check(Here, 0, 60_000).ShouldBeFalse();
    }

    [Fact]
    public void Musen_flyttede_sig_og_lytteren_hoerte_det()
    {
        var vagt = new HookWatchdog();
        vagt.Check(Here, 1_000, 2_000);

        vagt.Check(There, lastHookEventMs: 3_500, nowMs: 4_000).ShouldBeFalse();
    }

    [Fact]
    public void Musen_flyttede_sig_men_lytteren_var_tavs_saa_er_den_vaek()
    {
        var vagt = new HookWatchdog();
        vagt.Check(Here, 1_000, 2_000);

        vagt.Check(There, lastHookEventMs: 1_000, nowMs: 4_000).ShouldBeTrue();
    }

    [Fact]
    public void Naar_lytteren_er_sat_op_igen_og_hoerer_musen_falder_vagten_til_ro()
    {
        var vagt = new HookWatchdog();
        vagt.Check(Here, 1_000, 2_000);
        vagt.Check(There, 1_000, 4_000).ShouldBeTrue();

        // Sat op igen kl. 4.000 og har hørt musen siden.
        vagt.Check(Here, lastHookEventMs: 5_200, nowMs: 6_000).ShouldBeFalse();
    }

    [Fact]
    public void Lytteren_markeres_som_hoert_naar_den_saettes_op_saa_den_faar_en_runde_at_vise_sig_paa()
    {
        var vagt = new HookWatchdog();
        vagt.Check(Here, 1_000, 2_000);
        vagt.Check(There, 1_000, 4_000).ShouldBeTrue();

        // Sat op kl. 4.010 — det tæller som hørt. Er musen flyttet uden mere fra lytteren,
        // gives den ikke skylden endnu: den fik ikke en hel runde.
        vagt.Check(Here, lastHookEventMs: 4_010, nowMs: 6_000).ShouldBeFalse();

        // Men næste runde: stadig tavs mens musen flytter sig. Så er den væk igen.
        vagt.Check(There, lastHookEventMs: 4_010, nowMs: 8_000).ShouldBeTrue();
    }
}
