namespace Skaermlup.Core.Text;

public enum AppCommand
{
    ToggleMagnifier,
    ZoomIn,
    ZoomOut,
    LensBigger,
    LensSmaller,
    NextColors,
    ToggleInstructions,
    ToggleViewMode,
    ShowControlPanel,
    PanLeft,
    PanRight,
    PanUp,
    PanDown,
}

/// <summary>Modifikationstaster med de talværdier Windows selv bruger til globale genveje.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

/// <param name="Display">Sådan skrives genvejen for brugeren.</param>
public readonly record struct ShortcutDefinition(
    AppCommand Command,
    HotkeyModifiers Modifiers,
    int VirtualKey,
    string Display,
    string Description);

/// <param name="Display">Genvejen som den skrives for brugeren.</param>
public readonly record struct ShortcutRow(string Display, string Description);

/// <summary>
/// Ét sted med alle tastaturgenveje. Både registreringen i Windows og teksterne på skærmen
/// læser herfra, så vejledningen aldrig kan komme til at love noget andet end programmet gør.
/// </summary>
public static class Shortcuts
{
    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;

    // Virtuelle tastekoder fra Windows.
    private const int VkL = 0x4C;
    private const int VkK = 0x4B;
    private const int VkH = 0x48;
    private const int VkS = 0x53;
    private const int VkB = 0x42;
    private const int VkNumpadPlus = 0x6B;
    private const int VkNumpadMinus = 0x6D;
    private const int VkOemPlus = 0xBB;
    private const int VkOemMinus = 0xBD;
    private const int VkPageUp = 0x21;
    private const int VkPageDown = 0x22;
    private const int VkLeft = 0x25;
    private const int VkUp = 0x26;
    private const int VkRight = 0x27;
    private const int VkDown = 0x28;

    public static IReadOnlyList<ShortcutDefinition> All { get; } =
    [
        new(AppCommand.ToggleMagnifier, CtrlAlt, VkL, "Ctrl + Alt + L", "Tænd og sluk luppen"),
        new(AppCommand.ZoomIn, CtrlAlt, VkOemPlus, "Ctrl + Alt + plus", "Forstør mere"),
        new(AppCommand.ZoomIn, CtrlAlt, VkNumpadPlus, "Ctrl + Alt + plus", "Forstør mere"),
        new(AppCommand.ZoomOut, CtrlAlt, VkOemMinus, "Ctrl + Alt + minus", "Forstør mindre"),
        new(AppCommand.ZoomOut, CtrlAlt, VkNumpadMinus, "Ctrl + Alt + minus", "Forstør mindre"),
        new(AppCommand.LensBigger, CtrlAlt, VkPageUp, "Ctrl + Alt + Page Up", "Gør luppen større"),
        new(AppCommand.LensSmaller, CtrlAlt, VkPageDown, "Ctrl + Alt + Page Down", "Gør luppen mindre"),
        new(AppCommand.NextColors, CtrlAlt, VkK, "Ctrl + Alt + K", "Skift farver"),
        new(AppCommand.ToggleInstructions, CtrlAlt, VkH, "Ctrl + Alt + H", "Vis eller skjul vejledningen"),
        new(AppCommand.ToggleViewMode, CtrlAlt, VkB, "Ctrl + Alt + B", "Skift mellem lup og læsebånd"),
        new(AppCommand.ShowControlPanel, CtrlAlt, VkS, "Ctrl + Alt + S", "Åbn indstillinger"),
        new(AppCommand.PanLeft, CtrlAlt, VkLeft, "Ctrl + Alt + piletaster", "Flyt luppen uden mus"),
        new(AppCommand.PanRight, CtrlAlt, VkRight, "Ctrl + Alt + piletaster", "Flyt luppen uden mus"),
        new(AppCommand.PanUp, CtrlAlt, VkUp, "Ctrl + Alt + piletaster", "Flyt luppen uden mus"),
        new(AppCommand.PanDown, CtrlAlt, VkDown, "Ctrl + Alt + piletaster", "Flyt luppen uden mus"),
    ];

    /// <summary>Genvejene som de skal stå i en vejledning: én linje pr. handling.</summary>
    public static IReadOnlyList<ShortcutRow> ForDisplay()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<ShortcutRow>();
        foreach (var s in All)
            if (seen.Add(s.Display + "|" + s.Description))
                rows.Add(new ShortcutRow(s.Display, s.Description));
        return rows;
    }

    public static string DisplayFor(AppCommand command)
    {
        foreach (var s in All)
            if (s.Command == command) return s.Display;
        return string.Empty;
    }
}
