using Skaermlup.Core.Geometry;
using Skaermlup.Core.Settings;

namespace Skaermlup.Desktop.Interop;

/// <summary>
/// Gør et WPF-vindue til et lag der ligger oven på alt andet, ikke kan klikkes på, ikke tager
/// fokus og ikke kan ses af skærmkopiering. Det sidste er afgørende: uden det ville luppen
/// forstørre sit eget billede igen og igen.
/// </summary>
internal static class OverlayWindows
{
    /// <summary>Sandt hvis Windows kunne skjule vinduet for skærmkopiering.</summary>
    public static bool MakeOverlay(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;

        var ex = NativeMethods.GetWindowStyle(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        ex |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowStyle(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));

        return NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
    }

    public static void SetBounds(IntPtr hwnd, PixelRect rect, bool show)
    {
        if (hwnd == IntPtr.Zero || rect.IsEmpty) return;

        uint flags = NativeMethods.SWP_NOACTIVATE;
        if (show) flags |= NativeMethods.SWP_SHOWWINDOW;

        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
            rect.X, rect.Y, rect.Width, rect.Height, flags);
    }

    /// <summary>Lægger vinduet øverst igen, hvis et andet program har lagt sig ovenpå.</summary>
    public static void Reassert(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>Klipper vinduet til en rund lup eller et rektangel med bløde hjørner.</summary>
    public static void ApplyShape(IntPtr hwnd, int width, int height, LensShape shape, int cornerRadius)
    {
        if (hwnd == IntPtr.Zero || width <= 0 || height <= 0) return;

        IntPtr region = shape == LensShape.Circle
            ? NativeMethods.CreateEllipticRgn(0, 0, width + 1, height + 1)
            : NativeMethods.CreateRoundRectRgn(0, 0, width + 1, height + 1,
                Math.Max(1, cornerRadius * 2), Math.Max(1, cornerRadius * 2));

        if (region == IntPtr.Zero) return;

        // Windows overtager regionen når kaldet lykkes, og frigiver den selv bagefter.
        if (NativeMethods.SetWindowRgn(hwnd, region, true) == 0)
            NativeMethods.DeleteObject(region);
    }

    public static void ClearShape(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero) NativeMethods.SetWindowRgn(hwnd, IntPtr.Zero, true);
    }
}
