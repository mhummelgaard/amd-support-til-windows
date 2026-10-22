using Skaermlup.Core.Settings;

namespace Skaermlup.Core.Text;

/// <summary>
/// Teksterne mens brugeren vælger, hvilken museknap der tænder luppen. De står i
/// kom-godt-i-gang-kortet, fordi det er dér, man opdager at man ikke har en midterste knap.
/// </summary>
public static class TriggerSetup
{
    public const string Waiting =
        "Tryk nu på den knap, du vil tænde luppen med. "
        + "Det kan være den midterste knap eller en af sideknapperne under tommelfingeren.";

    public const string TimedOut =
        "Der blev ikke trykket på nogen knap. Prøv igen — eller brug Ctrl + Alt + L på tastaturet.";

    public const string Cancelled = "Knappen er ikke ændret.";

    public static string Learned(MouseTriggerButtons button)
        => "Nu tænder " + Instructions.TriggerSummary(button).ToLowerInvariant() + " luppen.";

    public const string ChordChosen =
        "Nu tænder venstre og højre knap trykket samtidig luppen. "
        + "Den midterste knap virker også, hvis musen alligevel har en.";
}
