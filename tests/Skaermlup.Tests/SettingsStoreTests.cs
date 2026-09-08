using Shouldly;
using Skaermlup.Core.Input;
using Skaermlup.Core.Magnification;
using Skaermlup.Core.Settings;
using Skaermlup.Core.Text;

namespace Skaermlup.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "skaermlup-test-" + Guid.NewGuid().ToString("N"));

    private string File1 => Path.Combine(_folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Foerste_start_uden_fil_giver_standardindstillinger()
    {
        var settings = new SettingsStore(File1).Load();

        settings.Zoom.ShouldBe(4.0);
        settings.Trigger.ShouldBe(MouseTriggerButtons.Middle);
        settings.ShowInstructions.ShouldBeTrue();
    }

    [Fact]
    public void Indstillinger_overlever_en_genstart()
    {
        var store = new SettingsStore(File1);
        var settings = store.Load();
        settings.Zoom = 8;
        settings.Colors = ColorScheme.YellowOnBlack;
        settings.YellowTone = YellowTone.Amber;
        settings.Trigger = MouseTriggerButtons.SideBack | MouseTriggerButtons.Middle;
        store.Save(settings);

        var again = new SettingsStore(File1).Load();

        again.Zoom.ShouldBe(8);
        again.Colors.ShouldBe(ColorScheme.YellowOnBlack);
        again.YellowTone.ShouldBe(YellowTone.Amber);
        again.Trigger.ShouldBe(MouseTriggerButtons.SideBack | MouseTriggerButtons.Middle);
    }

    [Fact]
    public void En_oedelagt_fil_stopper_ikke_programmet()
    {
        Directory.CreateDirectory(_folder);
        System.IO.File.WriteAllText(File1, "{ dette er ikke json");

        var settings = new SettingsStore(File1).Load();

        settings.Zoom.ShouldBe(4.0);
        System.IO.File.Exists(File1 + ".ugyldig").ShouldBeTrue();
    }

    [Fact]
    public void Urimelige_vaerdier_rettes_naar_filen_laeses()
    {
        Directory.CreateDirectory(_folder);
        System.IO.File.WriteAllText(File1, """
            { "Zoom": 999, "LensWidthPercent": 400, "Contrast": -900, "MaxFps": 5, "Trigger": "None" }
            """);

        var settings = new SettingsStore(File1).Load();

        settings.Zoom.ShouldBe(ZoomLadder.Max);
        settings.LensWidthPercent.ShouldBe(LensSizer.MaxPercent);
        settings.Contrast.ShouldBe(-50);
        settings.MaxFps.ShouldBe(15);
    }

    [Fact]
    public void Ringens_stoerrelse_rettes_naar_filen_laeses()
    {
        Directory.CreateDirectory(_folder);
        System.IO.File.WriteAllText(File1, """
            { "ShellPointerRingSize": 5000 }
            """);

        var settings = new SettingsStore(File1).Load();

        settings.ShellPointerRingSize.ShouldBe(CursorRing.MaxScalePercent);
        settings.ShellPointerRing.ShouldBeTrue();
    }

    [Fact]
    public void Der_er_altid_mindst_en_museknap_tilbage()
    {
        // Uden udløser kan luppen kun tændes fra tastaturet, og så er hele pointen væk.
        var settings = new AppSettings { Trigger = MouseTriggerButtons.None }.Normalize();

        settings.Trigger.ShouldBe(MouseTriggerButtons.Middle);
    }

    [Fact]
    public void Gemning_efterlader_ikke_en_halv_fil()
    {
        var store = new SettingsStore(File1);
        store.Save(new AppSettings());

        Directory.GetFiles(_folder).ShouldHaveSingleItem();
        Path.GetFileName(Directory.GetFiles(_folder)[0]).ShouldBe("settings.json");
    }
}

public class InstructionsTests
{
    [Fact]
    public void Vejledningen_naevner_den_knap_brugeren_faktisk_har_valgt()
    {
        var settings = new AppSettings { Trigger = MouseTriggerButtons.SideForward };

        Instructions.CallToAction(magnifierOn: false, settings)
            .ShouldContain("forreste sideknap");
    }

    [Fact]
    public void Flere_valgte_knapper_naevnes_alle_sammen()
    {
        var settings = new AppSettings
        {
            Trigger = MouseTriggerButtons.Middle | MouseTriggerButtons.SideBack,
        };

        var text = Instructions.TriggerSummary(settings.Trigger);

        text.ShouldContain("Midterste museknap");
        text.ShouldContain("Bageste sideknap");
    }

    [Fact]
    public void Hold_tilstanden_faar_sin_egen_besked()
    {
        var settings = new AppSettings { TriggerMode = TriggerMode.Hold };

        Instructions.CallToAction(magnifierOn: false, settings).ShouldContain("Hold");
        Instructions.CallToAction(magnifierOn: true, settings).ShouldContain("Slip");
    }

    [Fact]
    public void Overskriften_siger_tydeligt_om_luppen_er_taendt()
    {
        var settings = new AppSettings { Zoom = 6 };

        Instructions.Headline(true, settings).ShouldBe("LUPPEN ER TÆNDT — 6x");
        Instructions.Headline(false, settings).ShouldBe("LUPPEN ER SLUKKET");
    }

    [Fact]
    public void Alle_genveje_har_baade_tekst_og_forklaring()
    {
        foreach (var (display, description) in Shortcuts.ForDisplay())
        {
            display.ShouldNotBeNullOrWhiteSpace();
            description.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Vejledningen_paa_skaermen_er_kort_nok_til_at_staa_med_stor_skrift()
    {
        var chips = Instructions.Bar(magnifierOn: true, new AppSettings());

        chips.Count.ShouldBeLessThanOrEqualTo(5);
        foreach (var chip in chips) chip.Action.Length.ShouldBeLessThanOrEqualTo(40);
    }
}
