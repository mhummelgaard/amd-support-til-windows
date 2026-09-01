using System.Text.Json;
using System.Text.Json.Serialization;

namespace Skaermlup.Core.Settings;

/// <summary>
/// Læser og skriver indstillingerne som JSON. En ødelagt fil må aldrig stoppe programmet:
/// den lægges til side, og brugeren starter på standardindstillingerne.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly string _path;

    public SettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _path = filePath;
    }

    public string FilePath => _path;

    /// <summary>Standardplacering: %LocalAppData%\Skaermlup\settings.json</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Skaermlup",
        "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();

            string json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, Options);
            return (settings ?? new AppSettings()).Normalize();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            TryQuarantine();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? folder = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        // Skriv ved siden af og byt om, så et strømsvigt midt i en skrivning ikke
        // efterlader en halv fil.
        string temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings.Clone().Normalize(), Options));
        File.Move(temp, _path, overwrite: true);
    }

    private void TryQuarantine()
    {
        try
        {
            if (File.Exists(_path)) File.Move(_path, _path + ".ugyldig", overwrite: true);
        }
        catch (IOException) { /* Ligegyldigt: standardindstillingerne bruges alligevel. */ }
        catch (UnauthorizedAccessException) { /* Samme. */ }
    }
}
