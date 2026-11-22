namespace Skaermlup.Core.Text;

/// <summary>
/// Teksterne til berøringsskærm: knapperne i skærmkanten og vejledningen, når fingeren er det,
/// brugeren sidst brugte. Med en finger findes der hverken museknap eller musehjul, så de
/// almindelige tekster ville love noget, der ikke kan lade sig gøre.
/// </summary>
public static class TouchText
{
    public const string PowerLabel = "LUP";

    /// <summary>Hvad Oplæser siger om LUP-knappen.</summary>
    public static string PowerName(bool magnifierOn) => magnifierOn ? "Sluk luppen" : "Tænd luppen";

    public const string ZoomInLabel = "+";
    public const string ZoomInName = "Forstør mere";
    public const string ZoomOutLabel = "−";
    public const string ZoomOutName = "Forstør mindre";
    public const string SettingsLabel = "⚙";
    public const string SettingsName = "Indstillinger";

    public static string CallToAction(bool magnifierOn) => magnifierOn
        ? "Træk i luppen med fingeren for at flytte den"
        : "Tryk på LUP i skærmkanten for at tænde luppen";

    public static IReadOnlyList<InstructionChip> Bar(bool magnifierOn) => magnifierOn
        ?
        [
            new("LUP-knappen", "sluk luppen"),
            new("Træk i luppen", "flyt den med fingeren"),
            new("+ og −", "forstør mere eller mindre"),
            new("⚙-knappen", "indstillinger"),
        ]
        :
        [
            new("LUP-knappen", "tænd luppen"),
            new("⚙-knappen", "indstillinger"),
        ];

    public static GuideSection Guide { get; } = new("Med fingeren", [
        new InstructionChip("LUP i skærmkanten", "tænder og slukker luppen"),
        new InstructionChip("Træk i luppen", "flytter den, som et forstørrelsesglas over papiret"),
        new InstructionChip("+ og −", "forstør mere eller mindre"),
        new InstructionChip("Tryk uden for luppen", "virker som altid — luppen bliver, hvor den er"),
    ]);
}
