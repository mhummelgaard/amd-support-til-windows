using Skaermlup.Core.Geometry;

namespace Skaermlup.Desktop.Interop;

/// <summary>Skærmenes placering i fysiske pixels — også når der er flere skærme med hver sin opløsning.</summary>
internal static class ScreenInfo
{
    public static PixelPoint CursorPosition()
        => NativeMethods.GetCursorPos(out var p) ? new PixelPoint(p.X, p.Y) : PixelPoint.Zero;

    /// <summary>Hele skærmen (ikke kun arbejdsområdet) der indeholder punktet — nærmeste hvis det ligger udenfor.</summary>
    public static PixelRect MonitorAt(PixelPoint point)
    {
        var native = new NativeMethods.POINT { X = point.X, Y = point.Y };
        var handle = NativeMethods.MonitorFromPoint(native, NativeMethods.MONITOR_DEFAULTTONEAREST);
        return MonitorBounds(handle);
    }

    public static IntPtr MonitorHandleAt(PixelPoint point)
    {
        var native = new NativeMethods.POINT { X = point.X, Y = point.Y };
        return NativeMethods.MonitorFromPoint(native, NativeMethods.MONITOR_DEFAULTTONEAREST);
    }

    /// <summary>Skærmens arbejdsområde — altså uden proceslinjen. Bruges til vejledningsbjælken,
    /// så den ikke lægger sig oven på brugerens proceslinje.</summary>
    public static PixelRect WorkAreaAt(PixelPoint point)
    {
        var handle = MonitorHandleAt(point);
        if (handle != IntPtr.Zero)
        {
            var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (NativeMethods.GetMonitorInfoW(handle, ref info))
            {
                var r = info.rcWork;
                var work = PixelRect.FromBounds(r.Left, r.Top, r.Right, r.Bottom);
                if (!work.IsEmpty) return work;
            }
        }

        return MonitorAt(point);
    }

    public static PixelRect MonitorBounds(IntPtr monitor)
    {
        if (monitor != IntPtr.Zero)
        {
            var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (NativeMethods.GetMonitorInfoW(monitor, ref info))
            {
                var r = info.rcMonitor;
                return PixelRect.FromBounds(r.Left, r.Top, r.Right, r.Bottom);
            }
        }

        return VirtualScreen();
    }

    /// <summary>Alle skærme lagt sammen. Bruges som nødløsning hvis Windows ikke svarer.</summary>
    public static PixelRect VirtualScreen()
    {
        int x = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int y = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int w = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int h = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
        return w > 0 && h > 0 ? new PixelRect(x, y, w, h) : new PixelRect(0, 0, 1920, 1080);
    }
}
