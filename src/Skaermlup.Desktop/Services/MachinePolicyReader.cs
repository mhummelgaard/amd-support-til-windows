using System.IO;
using System.Security;
using Microsoft.Win32;
using Skaermlup.Core.Settings;

namespace Skaermlup.Desktop.Services;

/// <summary>
/// Læser IT-afdelingens politik for hele pc'en. Se <see cref="MachinePolicy"/>.
///
/// Det eneste sted programmet rører HKEY_LOCAL_MACHINE, og kun for at læse.
/// <c>efterproev-dataflow.ps1</c> siger fra, hvis det ændrer sig.
///
/// Læses hver gang der spørges, ikke én gang ved opstart. Slår IT prøvebilleder fra, mens
/// luppen kører, gælder det fra næste forsøg — uden at brugeren skal genstarte noget.
/// </summary>
internal static class MachinePolicyReader
{
    public static MachinePolicy Read()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(MachinePolicy.RegistryPath);
            return key is null
                ? MachinePolicy.None
                : MachinePolicy.FromRegistry(key.GetValue(MachinePolicy.SampleImagesValue));
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return MachinePolicy.Unreadable;
        }
    }
}
