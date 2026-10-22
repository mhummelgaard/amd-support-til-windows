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
/// program der er i forgrunden.
///
/// Lytteren har sin egen tråd, som ikke laver andet end at svare Windows. Svarer en lytter
/// ikke inden for omkring et sekund, fjerner Windows den i stilhed — og dengang den delte tråd
/// med luppens tegning og alle vinduerne, kunne et travlt øjeblik koste museknappen. Nu kan
/// programmet hænge i tegningen uden at musen mærker det.
///
/// Begivenhederne <see cref="Button"/> og <see cref="Wheel"/> kommer derfor på lyttertråden,
/// ikke på programmets hovedtråd. Den der lytter, skal beslutte hurtigt og lægge arbejdet i kø.
/// </summary>
internal sealed class GlobalMouseHook : IDisposable
{
    private const uint WM_REINSTALL = NativeMethods.WM_APP + 1;

    private readonly NativeMethods.HookProc _callback;
    private readonly ManualResetEventSlim _ready = new();
    private Thread? _thread;
    private uint _threadId;

    // Kun rørt på lyttertråden.
    private IntPtr _hook;

    private volatile bool _installed;
    private long _lastEventMs;
    private int _reinstalls;

    public GlobalMouseHook()
    {
        // Delegaten skal holdes i live i et felt. Bliver den samlet af skraldemanden
        // mens Windows stadig har den, går programmet ned.
        _callback = OnMouseEvent;
    }

    public event EventHandler<MouseButtonHookEventArgs>? Button;

    public event EventHandler<MouseWheelHookEventArgs>? Wheel;

    public bool IsInstalled => _installed;

    /// <summary>Hvornår lytteren sidst hørte noget fra musen, på <see cref="Environment.TickCount64"/>.</summary>
    public long LastEventMs => Volatile.Read(ref _lastEventMs);

    /// <summary>Hvor mange gange lytteren er sat op igen, fordi vagten mente den var væk.</summary>
    public int Reinstalls => Volatile.Read(ref _reinstalls);

    public bool Install()
    {
        if (_thread is not null) return _installed;

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Skaermlup: musens lytter",
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(5));
        return _installed;
    }

    /// <summary>Beder lyttertråden om at fjerne lytteren og sætte den op igen.</summary>
    public void Reinstall()
    {
        if (_threadId != 0)
            NativeMethods.PostThreadMessageW(_threadId, WM_REINSTALL, IntPtr.Zero, IntPtr.Zero);
    }

    private void Run()
    {
        _threadId = NativeMethods.GetCurrentThreadId();

        // Tråden får først en beskedkø, når den kalder noget i Windows' vindueslag. Uden kø kan
        // der ikke postes beskeder til den — heller ikke den der skal få den til at stoppe.
        NativeMethods.PeekMessageW(out _, IntPtr.Zero, 0, 0, NativeMethods.PM_NOREMOVE);

        InstallHere();
        _ready.Set();

        // Windows kalder lytteren inde fra GetMessage. 0 er WM_QUIT, -1 er en fejl.
        while (NativeMethods.GetMessageW(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            if (message.message != WM_REINSTALL) continue;

            RemoveHere();
            InstallHere();
            Interlocked.Increment(ref _reinstalls);
        }

        RemoveHere();
    }

    private void InstallHere()
    {
        var module = NativeMethods.GetModuleHandleW(null);
        _hook = NativeMethods.SetWindowsHookExW(NativeMethods.WH_MOUSE_LL, _callback, module, 0);
        _installed = _hook != IntPtr.Zero;

        // En nyopsat lytter regnes for at have hørt musen nu. Ellers ville vagten give den skylden
        // for tavshed fra før den fandtes.
        if (_installed) Volatile.Write(ref _lastEventMs, Environment.TickCount64);
    }

    private void RemoveHere()
    {
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        _installed = false;
    }

    private IntPtr OnMouseEvent(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0) return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);

        // Hver eneste bevægelse noteres. Det er sådan vagten kan se, at lytteren stadig er der.
        Volatile.Write(ref _lastEventMs, Environment.TickCount64);

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

    public void Dispose()
    {
        if (_thread is null) return;

        if (_threadId != 0)
            NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);

        _thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _ready.Dispose();
    }
}
