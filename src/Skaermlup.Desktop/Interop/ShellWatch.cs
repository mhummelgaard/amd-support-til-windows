using System.Runtime.InteropServices;
using System.Text;
using Skaermlup.Core.Geometry;
using Skaermlup.Core.Input;

namespace Skaermlup.Desktop.Interop;

/// <summary>Hvad der ligger oven på luppen lige nu, og hvor stort et område det dækker.</summary>
/// <param name="InFront">Sandt hvis Windows' egen skal eller en menu er fremme.</param>
/// <param name="Bounds">Fladens rektangel i skærmpixels. Tomt hvis det ikke kunne måles.</param>
internal readonly record struct ShellSurface(bool InFront, PixelRect Bounds)
{
    public static ShellSurface None => new(false, PixelRect.Empty);
}

/// <summary>
/// Spørger Windows hvad der ligger øverst lige nu, og lader <see cref="ShellSurfaces"/> afgøre
/// om det er skallen. Svaret på det enkelte vindue gemmes, for klassenavn og procesnavn ændrer
/// sig aldrig for et vindue der allerede findes, og opslaget skal kunne laves mange gange i
/// sekundet uden at koste noget. Rektanglet måles derimod hver gang — en menu folder sig ud, og
/// Start-menuen skifter størrelse når den åbnes.
/// </summary>
internal static class ShellWatch
{
    private static IntPtr _lastWindow = IntPtr.Zero;
    private static bool _lastAnswer;

    /// <summary>Hvad der dækker luppen, og hvor.</summary>
    public static ShellSurface Probe()
    {
        // En åben menu gør ikke sig selv til forgrundsvindue — ejeren bliver stående — så den
        // skal spørges om for sig, og svaret kan ikke gemmes fra gang til gang.
        var menu = MenuBounds();
        bool menuOpen = menu.HasValue;

        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
            return menuOpen ? new ShellSurface(true, menu ?? PixelRect.Empty) : ShellSurface.None;

        if (foreground != _lastWindow)
        {
            _lastWindow = foreground;
            _lastAnswer = ShellSurfaces.IsShellSurface(
                ClassNameOf(foreground), ProcessNameOf(foreground), menuIsOpen: false);
        }

        if (!_lastAnswer)
            return menuOpen ? new ShellSurface(true, menu ?? PixelRect.Empty) : ShellSurface.None;

        var bounds = BoundsOf(foreground);
        if (menu.HasValue) bounds = bounds.Union(menu.Value);
        return new ShellSurface(true, bounds);
    }

    /// <summary>Glemmer det gemte svar. Kaldes når luppen tændes, så et skift ikke bæres med over.</summary>
    public static void Forget()
    {
        _lastWindow = IntPtr.Zero;
        _lastAnswer = false;
    }

    /// <summary>
    /// Alle nedslåede menuer lagt sammen. Der kan være flere på én gang, for en undermenu er
    /// sit eget vindue ved siden af hovedmenuen.
    /// </summary>
    private static PixelRect? MenuBounds()
    {
        if (!IsMenuMode()) return null;

        var all = PixelRect.Empty;
        var menu = IntPtr.Zero;

        // Der er sjældent mere end tre-fire menuer i kæde. Loftet er der for ikke at kunne
        // hænge fast, hvis Windows en dag skulle svare noget uventet.
        for (int i = 0; i < 16; i++)
        {
            menu = NativeMethods.FindWindowExW(IntPtr.Zero, menu, NativeMethods.PopupMenuClass, null);
            if (menu == IntPtr.Zero) break;
            if (!NativeMethods.IsWindowVisible(menu)) continue;

            all = all.Union(BoundsOf(menu));
        }

        return all.IsEmpty ? null : all;
    }

    private static bool IsMenuMode()
    {
        var info = new NativeMethods.GUITHREADINFO { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };

        // Tråd 0 betyder "den tråd der har forgrunden", og det er ejeren af menuen.
        if (!NativeMethods.GetGUIThreadInfo(0, ref info)) return false;

        if (info.hwndMenuOwner != IntPtr.Zero) return true;

        const uint inMenu = NativeMethods.GUI_INMENUMODE | NativeMethods.GUI_POPUPMENUMODE;
        return (info.flags & inMenu) != 0;
    }

    private static PixelRect BoundsOf(IntPtr window)
    {
        if (!NativeMethods.GetWindowRect(window, out var rect)) return PixelRect.Empty;
        return PixelRect.FromBounds(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private static string ClassNameOf(IntPtr window)
    {
        var buffer = new StringBuilder(256);
        int length = NativeMethods.GetClassNameW(window, buffer, buffer.Capacity);
        return length > 0 ? buffer.ToString(0, length) : string.Empty;
    }

    private static string ProcessNameOf(IntPtr window)
    {
        if (NativeMethods.GetWindowProcessId(window, out uint pid) == 0 || pid == 0) return string.Empty;

        var handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return string.Empty;

        try
        {
            var buffer = new StringBuilder(512);
            int size = buffer.Capacity;
            return NativeMethods.QueryFullProcessImageNameW(handle, 0, buffer, ref size)
                ? buffer.ToString(0, Math.Min(size, buffer.Length))
                : string.Empty;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }
}
