namespace Skaermlup.Core.Text;

/// <summary>
/// Teksterne til advarslen efter et prøvebillede.
///
/// Et prøvebillede viser luppen, og luppen viser skærmen. Alt hvad der stod på skærmen står
/// derfor også på billedet — en journal, et brev fra kommunen, et cpr-nummer. Det er hele
/// pointen med billedet, og netop derfor er det farligt: brugeren har nedsat syn og kan i
/// sagens natur ikke altid selv læse, hvad der er kommet med, før filen bliver sendt videre.
///
/// Advarslen står med programmets egen skriftstørrelse og ikke i en systemdialog. En advarsel
/// der er for lille til at læse for den, den er skrevet til, er ingen advarsel.
/// </summary>
public static class SampleWarning
{
    public const string Title = "Skærmlup — prøvebilleder gemt";

    public const string Headline = "Billederne er gemt";

    public const string Subheadline = "Læs det her, før du sender dem videre";

    /// <summary>Selve advarslen. Hvert punkt er et afsnit for sig, så der er luft mellem dem.</summary>
    public static IReadOnlyList<string> Body { get; } =
    [
        "Billederne viser, hvad der stod på din skærm, da de blev taget.",
        "Stod der en e-mail, et brev, en journal eller dit cpr-nummer, står det også på billedet.",
        "Se billederne igennem, før du sender dem til nogen. Er du i tvivl om, hvad de viser, "
        + "så bed en du stoler på om at kigge med.",
    ];

    public const string FolderHeading = "Billederne ligger her";

    public const string FileHeading = "Disse filer blev gemt";

    public const string NoFiles =
        "Der blev ikke gemt nogen billeder. Prøv igen, mens luppen er tændt.";

    public const string OpenFolder = "Åbn mappen med billederne";

    public const string Close = "Luk";
}
