namespace Skaermlup.Core.Settings;

/// <summary>Hvordan det forstørrede billede vises.</summary>
public enum ViewMode
{
    /// <summary>Et forstørrelsesglas der ligger oven på og følger fokuspunktet.</summary>
    Lens = 0,

    /// <summary>Et fast læsebånd langs toppen eller bunden af skærmen.</summary>
    Dock = 1,
}

/// <summary>Lupvinduets form.</summary>
public enum LensShape
{
    Rectangle = 0,
    Circle = 1,
}

/// <summary>Hvilken skærmkant læsebåndet ligger op ad.</summary>
public enum DockEdge
{
    Top = 0,
    Bottom = 1,
}

/// <summary>Hvordan det sted markøren peger på bliver vist.</summary>
public enum MarkerStyle
{
    /// <summary>Kryds med hul i midten, så teksten under markøren ikke dækkes.</summary>
    Crosshair = 0,

    /// <summary>Streg under tekstlinjen i stedet for hen over den, plus en ring om stedet.</summary>
    Underline = 1,

    /// <summary>Kun en ring. Dækker mindst muligt.</summary>
    Ring = 2,

    /// <summary>Ingen markering.</summary>
    None = 3,
}

/// <summary>Farvebehandling af det forstørrede billede.</summary>
public enum ColorScheme
{
    /// <summary>Skærmens egne farver.</summary>
    Normal = 0,

    /// <summary>Farverne beholdes, men lyst bliver lysere og mørkt mørkere.</summary>
    HighContrast = 1,

    /// <summary>Negativ: lys tekst på mørk bund. Den mest brugte indstilling ved AMD.</summary>
    Inverted = 2,

    Grayscale = 3,
    YellowOnBlack = 4,
    BlackOnYellow = 5,
    WhiteOnBlack = 6,
}

/// <summary>Hvad museknapperne skal gøre ved luppen.</summary>
public enum TriggerMode
{
    /// <summary>Ét klik tænder, næste klik slukker.</summary>
    Toggle = 0,

    /// <summary>Luppen er tændt så længe knappen holdes nede — som at løfte et forstørrelsesglas.</summary>
    Hold = 1,
}

/// <summary>Hvad musehjulet gør, mens luppen er tændt.</summary>
public enum WheelAction
{
    /// <summary>Hjulet ruller som normalt. Kun Ctrl+hjul og Skift+hjul styrer luppen.</summary>
    ModifierOnly = 0,

    /// <summary>Hjulet alene ændrer forstørrelsen og sendes ikke videre til programmet under luppen.</summary>
    AlwaysZoom = 1,
}

/// <summary>Hvor instruktionsbjælken står.</summary>
public enum InstructionPlacement
{
    Bottom = 0,
    Top = 1,
}

/// <summary>Museknapper der kan tænde og slukke luppen.</summary>
[Flags]
public enum MouseTriggerButtons
{
    None = 0,

    /// <summary>Midterste knap — hjulet trykket ned.</summary>
    Middle = 1,

    /// <summary>Sideknap "tilbage" (den bageste af tommelfingerknapperne).</summary>
    SideBack = 2,

    /// <summary>Sideknap "frem".</summary>
    SideForward = 4,

    /// <summary>Venstre og højre knap trykket samtidig — til mus uden ekstra knapper.</summary>
    LeftRightChord = 8,
}
