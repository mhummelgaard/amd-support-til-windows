namespace Skaermlup.Core.Text;

/// <summary>Et spørgsmål før noget, der ikke kan fortrydes.</summary>
/// <param name="Keep">
/// Den sikre knap. Den vælges af Enter og af Escape, så et utilsigtet tryk aldrig gør skade —
/// og brugeren kan ikke nødvendigvis se, hvilken knap der har fokus.
/// </param>
/// <param name="Proceed">Knappen der gør det.</param>
public sealed record Confirmation(string Title, IReadOnlyList<string> Body, string Keep, string Proceed);

/// <summary>
/// Spørgsmålene før de to knapper i kontrolpanelet, der kan koste brugeren noget.
///
/// Begge stod i bunden af panelet, altid synlige, og virkede ved første klik. Nulstil tog alle de
/// indstillinger, brugeren havde fundet frem til. Afslut tog luppen — og for at få den tilbage
/// skulle Skærmlup findes i Start-menuen, uden lup.
/// </summary>
public static class Confirmations
{
    public static Confirmation Reset { get; } = new(
        "Nulstil alle indstillinger?",
        [
            "Forstørrelse, størrelse, farver, den gule tone, markeringen, museknappen og vejledningen "
            + "sættes tilbage til, som de var, da Skærmlup var ny.",
            "Om Skærmlup starter sammen med Windows, bliver som det er.",
            "Det kan ikke fortrydes.",
        ],
        Keep: "Nej, behold mine indstillinger",
        Proceed: "Ja, nulstil");

    public static Confirmation Quit(bool startsWithWindows) => new(
        "Afslut Skærmlup?",
        [
            "Når programmet er afsluttet, kan luppen ikke tændes — hverken med museknappen eller med tastaturet.",
            startsWithWindows
                ? "Den kommer igen, næste gang computeren starter. Eller start Skærmlup fra Start-menuen."
                : "Du får den igen ved at starte Skærmlup fra Start-menuen.",
        ],
        Keep: "Nej, behold luppen",
        Proceed: "Ja, afslut");
}
