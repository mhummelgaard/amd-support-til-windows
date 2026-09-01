using System.Runtime.InteropServices;
using Skaermlup.Core.Input;
using Skaermlup.Desktop.Interop;

namespace Skaermlup.Desktop.Services;

internal sealed class MouseButtonHookEventArgs : EventArgs
{
    public required MouseButton Button { get; init; }
    public required bool IsDown { get; init; }
    public required long TimestampMs { get; init; }

    /// <summary>Sæt til sandt for at holde klikket tilbage fra programmet under musen.</summary>
    public bool Swallow { get; set; }
}

internal sealed class MouseWheelHookEventArgs : EventArgs
{
    public required int Delta { get; init; }
    public required bool Control { get; init; }
    public required bool Shift { get; init; }
    public bool Swallow { get; set; }
}

/// <summary>
/// Lytter med på musen i hele Windows, så luppen kan tændes med en museknap uanset hvilket
/// program der er i forgrunden. Behandlingen skal være hurtig: bliver den her metode
/// hængende, fjerner Windows selv lytteren igen.
/// </summary>
internal sealed class GlobalMouseHook : IDisposable
{
    private readonly NativeMethods.HookProc _callback;
    private IntPtr _hook;

    public GlobalMouseHook()
    {
        // Delegaten skal holdes i live i et felt. Bliver den samlet af skraldemanden
        // mens Windows stadig har den, går programmet ned.
        _callback = OnMouseEvent;
    }

    public event EventHandler<MouseButtonHookEventArgs>? Button;

    public event EventHandler<MouseWheelHookEventArgs>? Wheel;

    public bool IsInstalled => _hook != IntPtr.Zero;

    public bool Install()
    {
        if (_hook != IntPtr.Zero) return true;

        var module = NativeMethods.GetModuleHandleW(null);
        _hook = NativeMethods.SetWindowsHookExW(NativeMethods.WH_MOUSE_LL, _callback, module, 0);
        return _hook != IntPtr.Zero;
    }

    public void Uninstall()
    {
        if (_hook == IntPtr.Zero) return;

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr OnMouseEvent(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0) return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);

        try
        {
            if (Handle((int)wParam, lParam)) return new IntPtr(1);
        }
        catch (Exception)
        {
            // En fejl her må aldrig lamme musen for hele maskinen.
        }

        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }

    private bool Handle(int message, IntPtr lParam)
    {
        var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

        if (message == NativeMethods.WM_MOUSEWHEEL)
        {
            var wheel = new MouseWheelHookEventArgs
            {
                Delta = (short)((data.mouseData >> 16) & 0xFFFF),
                Control = IsDown(NativeMethods.VK_CONTROL),
                Shift = IsDown(NativeMethods.VK_SHIFT),
            };
            Wheel?.Invoke(this, wheel);
            return wheel.Swallow;
        }

        (MouseButton button, bool isDown)? mapped = message switch
        {
            NativeMethods.WM_LBUTTONDOWN => (MouseButton.Left, true),
            NativeMethods.WM_LBUTTONUP => (MouseButton.Left, false),
            NativeMethods.WM_RBUTTONDOWN => (MouseButton.Right, true),
            NativeMethods.WM_RBUTTONUP => (MouseButton.Right, false),
            NativeMethods.WM_MBUTTONDOWN => (MouseButton.Middle, true),
            NativeMethods.WM_MBUTTONUP => (MouseButton.Middle, false),
            NativeMethods.WM_XBUTTONDOWN => (XButton(data), true),
            NativeMethods.WM_XBUTTONUP => (XButton(data), false),
            _ => null,
        };

        if (mapped is not { } value) return false;

        var args = new MouseButtonHookEventArgs
        {
            Button = value.button,
            IsDown = value.isDown,
            TimestampMs = data.time,
        };
        Button?.Invoke(this, args);
        return args.Swallow;
    }

    private static MouseButton XButton(NativeMethods.MSLLHOOKSTRUCT data)
        => ((data.mouseData >> 16) & 0xFFFF) == NativeMethods.XBUTTON1
            ? MouseButton.SideBack
            : MouseButton.SideForward;

    private static bool IsDown(int virtualKey) => (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    public void Dispose() => Uninstall();
}
