using System.IO;
using Skaermlup.Core.Settings;

namespace Skaermlup.Desktop.Services;

/// <summary>
/// Sender en anmodning om et prøvebillede videre til den lup der allerede kører.
///
/// Programmet kører kun i ét eksemplar ad gangen, så starter man det igen med
/// <c>--proevebillede</c>, er det den nye kopi der lukker sig selv ned igen. Uden den her
/// besked ville kommandoen derfor gøre ingenting hos præcis de brugere der har mest brug for
/// den: dem der har luppen kørende og har brug for hjælp til den.
/// </summary>
internal static class SampleRequest
{
    private static string Path => System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(SettingsStore.DefaultPath) ?? System.IO.Path.GetTempPath(),
        "proevebillede.txt");

    public static void Write(string folder)
    {
        try
        {
            string? directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(Path, folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Kan beskeden ikke lægges, sker der bare ingenting. Det er en supportfunktion.
        }
    }

    /// <summary>Læser og fjerner anmodningen. Null hvis der ikke var nogen.</summary>
    public static string? Take()
    {
        try
        {
            if (!File.Exists(Path)) return null;

            string folder = File.ReadAllText(Path).Trim();
            File.Delete(Path);
            return string.IsNullOrWhiteSpace(folder) ? null : folder;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
