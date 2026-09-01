using Skaermlup.Core.Geometry;
using Skaermlup.Core.Settings;

namespace Skaermlup.Desktop.Interop;

/// <summary>
/// Gør et WPF-vindue til et lag der ligger oven på alt andet, ikke tager fokus og ikke kan
/// ses af skærmkopiering. Det sidste er afgørende: uden det ville luppen
/// forstørre sit eget billede igen og igen.
///
/// At musen går <em>igennem</em> laget kommer af to ting: WS_EX_TRANSPARENT her, og
/// AllowsTransparency i vinduets XAML. Det sidste får WPF til selv at gøre vinduet lagdelt,
/// og først med begge dele sender Windows klikket videre til programmet nedenunder. Sætter
/// man WS_EX_LAYERED i stedet herfra, fjerner WPF den igen med det samme.
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

    /// <summary>
    /// Lægger vinduet øverst igen, hvis et andet program har lagt sig ovenpå.
    /// </summary>
    public static void Reassert(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }


    /// <summary>
    /// Klipper vinduet til sin form og laver et lille hul dér hvor musen står.
    ///
    /// Hullet er hele grunden til at man kan klikke gennem luppen. Windows sender kun klik
    /// videre til programmet nedenunder, hvis vinduet enten er lagdelt eller har et hul netop
    /// dér. Lagdelte vinduer tegnes uden om grafikkortet og flimrer på store flader, så det
    /// er hullet der bruges. Det er få pixels stort og ligger under musepilen, som Windows
    /// selv tegner ovenpå — så det ses ikke i praksis.
    /// </summary>
    public static void ApplyShape(
        IntPtr hwnd, int width, int height, LensShape shape, int cornerRadius,
        PixelPoint hole, int holeRadius)
    {
        if (hwnd == IntPtr.Zero || width <= 0 || height <= 0) return;

        IntPtr region = shape == LensShape.Circle
            ? NativeMethods.CreateEllipticRgn(0, 0, width + 1, height + 1)
            : NativeMethods.CreateRoundRectRgn(0, 0, width + 1, height + 1,
                Math.Max(1, cornerRadius * 2), Math.Max(1, cornerRadius * 2));

        if (region == IntPtr.Zero) return;

        if (holeRadius > 0)
        {
            var opening = NativeMethods.CreateEllipticRgn(
                hole.X - holeRadius, hole.Y - holeRadius,
                hole.X + holeRadius, hole.Y + holeRadius);

            if (opening != IntPtr.Zero)
            {
                NativeMethods.CombineRgn(region, region, opening, NativeMethods.RGN_DIFF);
                NativeMethods.DeleteObject(opening);
            }
        }

        // Windows overtager regionen når kaldet lykkes, og frigiver den selv bagefter.
        if (NativeMethods.SetWindowRgn(hwnd, region, true) == 0)
            NativeMethods.DeleteObject(region);
    }

    /// <summary>Et helt rektangel med hul i, til læsebåndet der ikke har afrundede hjørner.</summary>
    public static void ApplyBandShape(IntPtr hwnd, int width, int height, PixelPoint hole, int holeRadius)
    {
        if (hwnd == IntPtr.Zero || width <= 0 || height <= 0) return;

        var region = NativeMethods.CreateRectRgn(0, 0, width + 1, height + 1);
        if (region == IntPtr.Zero) return;

        if (holeRadius > 0)
        {
            var opening = NativeMethods.CreateEllipticRgn(
                hole.X - holeRadius, hole.Y - holeRadius,
                hole.X + holeRadius, hole.Y + holeRadius);

            if (opening != IntPtr.Zero)
            {
                NativeMethods.CombineRgn(region, region, opening, NativeMethods.RGN_DIFF);
                NativeMethods.DeleteObject(opening);
            }
        }

        if (NativeMethods.SetWindowRgn(hwnd, region, true) == 0)
            NativeMethods.DeleteObject(region);
    }
}
