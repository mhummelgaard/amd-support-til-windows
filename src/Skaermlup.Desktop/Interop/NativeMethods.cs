using System.Runtime.InteropServices;

namespace Skaermlup.Desktop.Interop;

/// <summary>
/// De Windows-kald luppen har brug for. Alt der rører skærmen direkte samles her, så resten
/// af programmet kan holdes i almindelig C#.
/// </summary>
internal static partial class NativeMethods
{
    // ---------- Vinduesstile ----------
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    public static readonly IntPtr HWND_TOPMOST = new(-1);

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    // Skjuler vinduet for alt skærmkopiering. Uden det ville luppen fotografere sig selv.
    public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;


    // ---------- Beskeder ----------
    public const int WM_HOTKEY = 0x0312;


    public const int WM_DPICHANGED = 0x02E0;
    public const int WM_DISPLAYCHANGE = 0x007E;

    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_MBUTTONUP = 0x0208;
    public const int WM_MOUSEWHEEL = 0x020A;
    public const int WM_XBUTTONDOWN = 0x020B;
    public const int WM_XBUTTONUP = 0x020C;

    public const int XBUTTON1 = 1;
    public const int XBUTTON2 = 2;

    public const int WH_MOUSE_LL = 14;

    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;

    // ---------- Strukturer ----------
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    /// <summary>Sat i <see cref="GUITHREADINFO.flags"/> når tråden viser en menu.</summary>
    public const uint GUI_INMENUMODE = 0x00000004;

    /// <summary>Sat i <see cref="GUITHREADINFO.flags"/> når en menu er slået ned.</summary>
    public const uint GUI_POPUPMENUMODE = 0x00000010;

    /// <summary>Beskriver en markør eller et ikon når det bygges eller læses.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ICONINFO
    {
        /// <summary>Sandt for et ikon, falsk for en markør. En markør har til gengæld et hotspot.</summary>
        public int fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }
}
