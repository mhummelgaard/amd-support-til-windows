using System.Windows.Interop;
using Skaermlup.Core.Text;
using Skaermlup.Desktop.Interop;

namespace Skaermlup.Desktop.Services;

/// <summary>
/// Melder programmets tastaturgenveje til Windows, så de virker uanset hvilket program der
/// er i forgrunden. Genveje som et andet program allerede har taget, springes over — og
/// bliver stående på listen, så kontrolpanelet kan sige det til brugeren.
/// </summary>
internal sealed class HotkeyService : IDisposable
{
    private readonly List<int> _registered = [];
    private readonly List<ShortcutDefinition> _failed = [];
    private HwndSource? _source;

    public event EventHandler<AppCommand>? Command;

    /// <summary>Genveje Windows ikke ville give os. Som regel fordi et andet program bruger dem.</summary>
    public IReadOnlyList<ShortcutDefinition> Unavailable => _failed;

    public void Install()
    {
        if (_source is not null) return;

        // Et vindue der kun findes for at modtage beskeder. Det vises aldrig.
        var parameters = new HwndSourceParameters("Skaermlup.Genveje")
        {
            ParentWindow = new IntPtr(-3),
            WindowStyle = 0,
        };

        _source = new HwndSource(parameters);
        _source.AddHook(OnMessage);

        var shortcuts = Shortcuts.All;
        for (int i = 0; i < shortcuts.Count; i++)
        {
            var shortcut = shortcuts[i];
            if (NativeMethods.RegisterHotKey(_source.Handle, i, (uint)shortcut.Modifiers, (uint)shortcut.VirtualKey))
                _registered.Add(i);
            else
                _failed.Add(shortcut);
        }
    }

    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_HOTKEY) return IntPtr.Zero;

        int id = wParam.ToInt32();
        if (id >= 0 && id < Shortcuts.All.Count)
        {
            Command?.Invoke(this, Shortcuts.All[id].Command);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is null) return;

        foreach (int id in _registered) NativeMethods.UnregisterHotKey(_source.Handle, id);
        _registered.Clear();

        _source.RemoveHook(OnMessage);
        _source.Dispose();
        _source = null;
    }
}
