using System.IO;
using Microsoft.Win32;
using Skaermlup.Core.Settings;

namespace Skaermlup.Desktop.Services;

/// <summary>
/// Får programmet til at starte sammen med Windows. Det skrives kun under brugerens egen
/// del af registreringsdatabasen, så det hverken kræver administrator eller rører andre brugere.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Skaermlup";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>Falsk hvis Windows ikke ville lade os skrive. Så må brugeren selv lægge en genvej.</summary>
    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return false;

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            string? path = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(path)) return false;

            key.SetValue(ValueName, "\"" + path + "\" " + StartupPlan.WithWindowsArgument, RegistryValueKind.String);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
