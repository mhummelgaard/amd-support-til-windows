namespace Skaermlup.Core.Settings;

/// <summary>
/// Det, IT-afdelingen har bestemt for hele pc'en.
///
/// Ligger i <c>HKLM\SOFTWARE\Policies\Skaermlup</c>, hvor kun en administrator eller en
/// gruppepolitik kan skrive. Brugeren kan derfor ikke selv slå det fra igen — og det er meningen.
/// Programmet læser stedet, men skriver aldrig selv dér.
///
/// Lige nu er der én ting at bestemme: om prøvebilleder må gemmes. Et prøvebillede viser hvad der
/// stod på skærmen. På en arbejdsplads med fortrolige eller klassificerede oplysninger skal IT kunne
/// lukke den vej helt, uden at skulle stole på, at hver enkelt bruger læser advarslen.
/// </summary>
public sealed record MachinePolicy(bool SampleImagesBlocked)
{
    /// <summary>Under HKEY_LOCAL_MACHINE. Samme sted som gruppepolitikskabelonen skriver.</summary>
    public const string RegistryPath = @"SOFTWARE\Policies\Skaermlup";

    /// <summary>DWORD. 1 slår prøvebilleder fra på hele pc'en.</summary>
    public const string SampleImagesValue = "DisableSampleImages";

    /// <summary>IT har ikke bestemt noget. Alt er som det plejer.</summary>
    public static MachinePolicy None { get; } = new(SampleImagesBlocked: false);

    /// <summary>
    /// Stedet findes, men kunne ikke læses. Så vides det ikke, hvad IT har bestemt, og
    /// prøvebillederne holdes lukket. Det koster en supportfunktion — det modsatte kunne koste en
    /// lækage på netop den pc, hvor nogen har gjort sig umage for at undgå den.
    /// </summary>
    public static MachinePolicy Unreadable { get; } = new(SampleImagesBlocked: true);

    /// <summary>
    /// Hvad værdien betyder, som den kommer ud af registreringsdatabasen.
    ///
    /// Mangler den, er der intet bestemt. Er den 0, er prøvebilleder udtrykkeligt tilladt. Alt andet
    /// slår dem fra — også en værdi af en type, der ikke var ventet. Ingen skriver
    /// <c>DisableSampleImages</c> uden at mene det, og en forkert type i en håndskrevet .reg-fil må
    /// ikke åbne for det, IT ville lukke.
    /// </summary>
    public static MachinePolicy FromRegistry(object? disableSampleImages)
        => new(SampleImagesBlocked: disableSampleImages switch
        {
            null => false,
            int number => number != 0,
            long number => number != 0,
            string text => !IsExplicitlyOff(text),
            _ => true,
        });

    private static bool IsExplicitlyOff(string text)
    {
        string trimmed = text.Trim();
        return trimmed == "0" || string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase);
    }
}
