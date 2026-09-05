using Skaermlup.Core.Input;
using Skaermlup.Core.Magnification;

namespace Skaermlup.Core.Settings;

/// <summary>
/// Alt hvad brugeren kan stille på. Værdierne er valgt som et fornuftigt udgangspunkt for
/// en svagtseende bruger: fire gange forstørrelse, en stor lup, tydelig markør og
/// instruktionsbjælken tændt.
/// </summary>
public sealed class AppSettings
{
    public double Zoom { get; set; } = 4.0;

    public ViewMode Mode { get; set; } = ViewMode.Lens;

    public LensShape Shape { get; set; } = LensShape.Rectangle;

    public int LensWidthPercent { get; set; } = 48;

    public int LensHeightPercent { get; set; } = 40;

    public DockEdge DockEdge { get; set; } = DockEdge.Top;

    public int DockHeightPercent { get; set; } = 35;

    public ColorScheme Colors { get; set; } = ColorScheme.Normal;

    public int Contrast { get; set; }

    public int Brightness { get; set; }

    /// <summary>Blødt forstørret billede. Slås fra hvis brugeren foretrækker skarpe pixelkanter.</summary>
    public bool SmoothImage { get; set; } = true;

    /// <summary>Hvordan stedet markøren peger på vises. Musepilen kommer ikke med i en
    /// skærmkopi, så uden en markering kan brugeren ikke se hvor luppen peger.</summary>
    public MarkerStyle Marker { get; set; } = MarkerStyle.Crosshair;

    /// <summary>Markeringens størrelse i procent. 100 er udgangspunktet.</summary>
    public int MarkerSize { get; set; } = 100;

    /// <summary>
    /// Sæt en kraftig ring om musepilen når Windows' egen skal — Start-menuen, opgavevisningen,
    /// meddelelsescenteret — ligger oven på luppen. Ringen tegnes af Windows selv som en del af
    /// markøren og ligger derfor øverst, dér hvor luppen ikke kan komme til.
    /// </summary>
    public bool ShellPointerRing { get; set; } = true;

    /// <summary>Ringens størrelse i procent af markørens egen. 300 er tre gange så bred som pilen.</summary>
    public int ShellPointerRingSize { get; set; } = CursorRing.DefaultScalePercent;

    /// <summary>
    /// Flyt luppen ud til siden af Start-menuen og de andre Windows-flader i stedet for at lade
    /// den forsvinde ind bag dem. Indholdet er det samme — billedet står bare ved siden af,
    /// over eller under menuen.
    /// </summary>
    public bool ShellMoveLensAside { get; set; } = true;

    /// <summary>Rammens tykkelse omkring luppen i pixels. Rammen gør det nemt at se hvor luppen er.</summary>
    public int BorderThickness { get; set; } = 6;

    public MouseTriggerButtons Trigger { get; set; } = MouseTriggerButtons.Middle;

    public TriggerMode TriggerMode { get; set; } = TriggerMode.Toggle;

    public bool BlockTriggerClicks { get; set; } = true;

    public WheelAction Wheel { get; set; } = WheelAction.ModifierOnly;

    public bool FollowCaret { get; set; } = true;

    public bool ShowInstructions { get; set; } = true;

    public InstructionPlacement InstructionsAt { get; set; } = InstructionPlacement.Bottom;

    /// <summary>Instruktionsbjælkens tekststørrelse i procent. 100 er allerede stor.</summary>
    public int InstructionScale { get; set; } = 100;

    public int MaxFps { get; set; } = 60;

    public bool StartMagnifierOnLaunch { get; set; }

    public bool StartWithWindows { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    /// <summary>Retter værdier der ligger uden for det tilladte, fx efter en håndredigeret fil.</summary>
    public AppSettings Normalize()
    {
        Zoom = ZoomLadder.Snap(Zoom);
        LensWidthPercent = LensSizer.ClampPercent(LensWidthPercent);
        LensHeightPercent = LensSizer.ClampPercent(LensHeightPercent);
        DockHeightPercent = Math.Clamp(DockHeightPercent, 10, 90);
        Contrast = Math.Clamp(Contrast, -50, 50);
        Brightness = Math.Clamp(Brightness, -50, 50);
        BorderThickness = Math.Clamp(BorderThickness, 0, 24);
        MarkerSize = Math.Clamp(MarkerSize, 50, 250);
        ShellPointerRingSize = CursorRing.ClampScale(ShellPointerRingSize);
        InstructionScale = Math.Clamp(InstructionScale, 75, 250);
        MaxFps = Math.Clamp(MaxFps, 15, 120);

        if (!Enum.IsDefined(Mode)) Mode = ViewMode.Lens;
        if (!Enum.IsDefined(Shape)) Shape = LensShape.Rectangle;
        if (!Enum.IsDefined(DockEdge)) DockEdge = DockEdge.Top;
        if (!Enum.IsDefined(Colors)) Colors = ColorScheme.Normal;
        if (!Enum.IsDefined(Marker)) Marker = MarkerStyle.Crosshair;
        if (!Enum.IsDefined(TriggerMode)) TriggerMode = TriggerMode.Toggle;
        if (!Enum.IsDefined(Wheel)) Wheel = WheelAction.ModifierOnly;
        if (!Enum.IsDefined(InstructionsAt)) InstructionsAt = InstructionPlacement.Bottom;

        const MouseTriggerButtons all = MouseTriggerButtons.Middle | MouseTriggerButtons.SideBack
                                      | MouseTriggerButtons.SideForward | MouseTriggerButtons.LeftRightChord;
        Trigger &= all;

        // Uden mindst én udløser kan luppen kun tændes fra tastaturet, og så er hele pointen væk.
        if (Trigger == MouseTriggerButtons.None) Trigger = MouseTriggerButtons.Middle;

        return this;
    }
}
