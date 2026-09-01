using System.Runtime.InteropServices;
using Skaermlup.Core.Geometry;

namespace Skaermlup.Desktop.Interop;

/// <summary>
/// Finder tekstmarkøren i det program der er i forgrunden, så luppen kan følge med når
/// brugeren skriver. Windows fortæller det kun for programmer der bruger den almindelige
/// tekstmarkør; svarer et program ikke, får vi bare ingenting, og musen styrer videre.
/// </summary>
internal static class CaretProbe
{
    public static PixelRect? TryGetCaretRect()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return null;

        uint threadId = NativeMethods.GetWindowThreadProcessId(foreground, IntPtr.Zero);
        if (threadId == 0) return null;

        var info = new NativeMethods.GUITHREADINFO { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
        if (!NativeMethods.GetGUIThreadInfo(threadId, ref info)) return null;
        if (info.hwndCaret == IntPtr.Zero) return null;

        var caret = info.rcCaret;
        int width = caret.Right - caret.Left;
        int height = caret.Bottom - caret.Top;
        if (height <= 0) return null;

        // En markør der fylder hele vinduet er ikke en tekstmarkør, men et program der
        // rapporterer noget andet. Den slags skal ikke rive luppen med sig.
        if (height > 400 || width > 400) return null;

        var topLeft = new NativeMethods.POINT { X = caret.Left, Y = caret.Top };
        if (!NativeMethods.ClientToScreen(info.hwndCaret, ref topLeft)) return null;

        return new PixelRect(topLeft.X, topLeft.Y, Math.Max(1, width), height);
    }
}
