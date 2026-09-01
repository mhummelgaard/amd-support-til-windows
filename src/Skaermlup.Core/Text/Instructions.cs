using Skaermlup.Core.Colors;
using Skaermlup.Core.Magnification;
using Skaermlup.Core.Settings;

namespace Skaermlup.Core.Text;

/// <param name="Action">Handlingen, fx "Midterste museknap".</param>
/// <param name="Meaning">Hvad den gør, fx "tænd og sluk".</param>
public readonly record struct InstructionChip(string Action, string Meaning);

public readonly record struct GuideSection(string Heading, IReadOnlyList<InstructionChip> Items);

/// <summary>
/// Alle vejledningstekster ét sted. Programmet er lavet til folk der ikke kan læse små skilte,
/// så teksterne er korte, konkrete og skrevet så de kan stå med meget stor skrift.
/// </summary>
public static class Instructions
{
    public static string ButtonName(MouseTriggerButtons button) => button switch
    {
        MouseTriggerButtons.Middle => "Midterste museknap",
        MouseTriggerButtons.SideBack => "Bageste sideknap",
        MouseTriggerButtons.SideForward => "Forreste sideknap",
        MouseTriggerButtons.LeftRightChord => "Venstre + højre museknap",
        _ => "Museknap",
    };

    /// <summary>De valgte udløsere skrevet sammen, fx "Midterste museknap eller Bageste sideknap".</summary>
    public static string TriggerSummary(MouseTriggerButtons buttons)
    {
        var names = new List<string>();
        foreach (var b in new[]
                 {
                     MouseTriggerButtons.Middle, MouseTriggerButtons.SideBack,
                     MouseTriggerButtons.SideForward, MouseTriggerButtons.LeftRightChord,
                 })
        {
            if (buttons.HasFlag(b)) names.Add(ButtonName(b));
        }

        return names.Count switch
        {
            0 => "Ingen museknap valgt",
            1 => names[0],
            _ => string.Join(" eller ", names),
        };
    }

    /// <summary>Den store overskrift i instruktionsbjælken.</summary>
    public static string Headline(bool magnifierOn, AppSettings settings)
        => magnifierOn
            ? "LUPPEN ER TÆNDT — " + ZoomLadder.Format(settings.Zoom)
            : "LUPPEN ER SLUKKET";

    /// <summary>Den ene sætning brugeren skal kunne handle på uden at lede efter noget.</summary>
    public static string CallToAction(bool magnifierOn, AppSettings settings)
    {
        string trigger = TriggerSummary(settings.Trigger);

        if (!magnifierOn)
            return settings.TriggerMode == TriggerMode.Hold
                ? "Hold " + trigger.ToLowerInvariant() + " nede for at forstørre"
                : "Tryk på " + trigger.ToLowerInvariant() + " for at tænde luppen";

        return settings.TriggerMode == TriggerMode.Hold
            ? "Slip knappen for at slukke luppen"
            : "Tryk på " + trigger.ToLowerInvariant() + " igen for at slukke";
    }

    /// <summary>De få genveje der skal stå fremme hele tiden. Flere ville drukne budskabet.</summary>
    public static IReadOnlyList<InstructionChip> Bar(bool magnifierOn, AppSettings settings)
    {
        var chips = new List<InstructionChip>
        {
            new(TriggerSummary(settings.Trigger), magnifierOn ? "sluk luppen" : "tænd luppen"),
        };

        if (magnifierOn)
        {
            chips.Add(new InstructionChip("Ctrl + rul på hjulet", "forstør mere eller mindre"));
            chips.Add(new InstructionChip("Skift + rul på hjulet", "større eller mindre lup"));
            chips.Add(new InstructionChip(Shortcuts.DisplayFor(AppCommand.NextColors),
                "farver: " + ColorSchemes.Name(settings.Colors).ToLowerInvariant()));
        }

        chips.Add(new InstructionChip(Shortcuts.DisplayFor(AppCommand.ShowControlPanel), "indstillinger"));
        return chips;
    }

    /// <summary>Den korte kom-i-gang, som står øverst i kontrolpanelet.</summary>
    public static IReadOnlyList<string> GettingStarted(AppSettings settings) =>
    [
        "1. Sæt musen dér hvor du vil læse.",
        "2. Tryk på " + TriggerSummary(settings.Trigger).ToLowerInvariant() + ". Luppen tændes med det samme.",
        "3. Rul med Ctrl nede for at forstørre mere. Tryk på knappen igen for at slukke.",
    ];

    public static IReadOnlyList<GuideSection> FullGuide(AppSettings settings) =>
    [
        new("Sådan tænder og slukker du", [
            new InstructionChip(TriggerSummary(settings.Trigger), "tænder og slukker luppen"),
            new InstructionChip(Shortcuts.DisplayFor(AppCommand.ToggleMagnifier), "gør det samme fra tastaturet"),
        ]),
        new("Størrelse og forstørrelse", [
            new InstructionChip("Ctrl + rul på hjulet", "forstør mere eller mindre"),
            new InstructionChip("Skift + rul på hjulet", "gør selve luppen større eller mindre"),
            new InstructionChip(Shortcuts.DisplayFor(AppCommand.ZoomIn) + " / " + Shortcuts.DisplayFor(AppCommand.ZoomOut), "forstørrelse fra tastaturet"),
            new InstructionChip(Shortcuts.DisplayFor(AppCommand.LensBigger) + " / " + Shortcuts.DisplayFor(AppCommand.LensSmaller), "lupstørrelse fra tastaturet"),
        ]),
        new("Farver og læsning", [
            new InstructionChip(Shortcuts.DisplayFor(AppCommand.NextColors), "skifter til næste farvetilstand"),
            new InstructionChip(Shortcuts.DisplayFor(AppCommand.ToggleViewMode), "skifter mellem lup og læsebånd"),
            new InstructionChip("Læsebånd", "en bred stribe i toppen af skærmen — god til lange tekster"),
        ]),
        new("Uden mus", [
            new InstructionChip(Shortcuts.DisplayFor(AppCommand.PanLeft), "flytter luppen skridt for skridt"),
            new InstructionChip("Skriv i et tekstfelt", "luppen følger selv skrivemarkøren"),
        ]),
        new("Vejledningen på skærmen", [
            new InstructionChip(Shortcuts.DisplayFor(AppCommand.ToggleInstructions), "viser eller skjuler bjælken med vejledningen"),
            new InstructionChip(Shortcuts.DisplayFor(AppCommand.ShowControlPanel), "åbner indstillingerne igen"),
        ]),
    ];
}
