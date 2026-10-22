using System.Reflection;

namespace Skaermlup.Desktop;

/// <summary>Programmets version, som brugeren skal se den: "1.4.0" — uden commit-id'et bagved.</summary>
internal static class AppInfo
{
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var assembly = typeof(AppInfo).Assembly;
        string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // Byggeriet sætter "+<commit>" bagved. Det er til sporbarhed, ikke til brugeren.
        if (!string.IsNullOrWhiteSpace(informational)) return informational.Split('+')[0];

        return assembly.GetName().Version?.ToString(3) ?? "?";
    }
}
