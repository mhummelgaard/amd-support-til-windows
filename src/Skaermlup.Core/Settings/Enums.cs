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

    /// <summary>
    /// Ingen egen markering — Windows egen musepil bliver stående og bruges i stedet.
    /// I alle de andre tilstande skjules pilen, så der ikke er to markører på skærmen.
    /// </summary>
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

/// <summary>
/// Hvilken gul de to gule tilstande bruger. Hvad der er rarest at læse er forskelligt fra
/// person til person: den varme rav har næsten intet blåt i sig og blænder mindst, mens den
/// blege fløde er mildere at have som baggrund på en hel skærm. Rækkefølgen går fra varmest
/// til blegest, så en minus- og en plusknap er nok til at finde sin egen.
/// </summary>
public enum YellowTone
{
    /// <summary>Varm rav som på de gamle ravskærme. Mindst blåt lys.</summary>
    Amber = 0,

    /// <summary>Gylden — et trin varmere end den rene gule.</summary>
    Golden = 1,

    /// <summary>Ren gul. Den kraftigste, og den største forskel til sort.</summary>
    Pure = 2,

    /// <summary>Citrongul: lysere og mildere end den rene.</summary>
    Lemon = 3,

    /// <summary>Bleg flødegul, næsten som gulligt papir.</summary>
    Cream = 4,
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
