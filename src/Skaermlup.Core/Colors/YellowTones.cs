using Skaermlup.Core.Settings;

namespace Skaermlup.Core.Colors;

/// <summary>
/// De gule toner der kan vælges imellem, ordnet fra den varmeste til den blegeste.
/// Farverne er den værdi den lyseste pixel i billedet ender på; alt derunder skaleres
/// ned mod sort i samme forhold, så tonen holdes hele vejen ned gennem gråtonerne.
/// </summary>
public static class YellowTones
{
    private static readonly YellowTone[] Ordered =
    [
        YellowTone.Amber,
        YellowTone.Golden,
        YellowTone.Pure,
        YellowTone.Lemon,
        YellowTone.Cream,
    ];

    /// <summary>Tonerne fra varmest til blegest.</summary>
    public static IReadOnlyList<YellowTone> Order => Ordered;

    /// <summary>Et trin mod den varme rav.</summary>
    public static YellowTone Warmer(YellowTone tone) => Step(tone, -1);

    /// <summary>Et trin mod den blege fløde.</summary>
    public static YellowTone Paler(YellowTone tone) => Step(tone, +1);

    /// <summary>Den fulde farve tonen står for, som blå, grøn og rød.</summary>
    public static (byte Red, byte Green, byte Blue) Rgb(YellowTone tone) => tone switch
    {
        YellowTone.Amber => (255, 176, 0),
        YellowTone.Golden => (255, 214, 0),
        YellowTone.Lemon => (255, 255, 128),
        YellowTone.Cream => (255, 250, 205),
        _ => (255, 255, 0),
    };

    public static string Name(YellowTone tone) => tone switch
    {
        YellowTone.Amber => "Ravgul",
        YellowTone.Golden => "Gyldengul",
        YellowTone.Lemon => "Citrongul",
        YellowTone.Cream => "Flødegul",
        _ => "Ren gul",
    };

    public static string Description(YellowTone tone) => tone switch
    {
        YellowTone.Amber => "Varm og dyb. Næsten intet blåt lys — blænder mindst af dem alle.",
        YellowTone.Golden => "Et trin varmere end den rene gule.",
        YellowTone.Lemon => "Lysere og mildere end den rene gule.",
        YellowTone.Cream => "Næsten som gulligt papir. Rarest som baggrund på en hel skærm.",
        _ => "Den kraftigste gule, og den største forskel til sort.",
    };

    /// <summary>
    /// Trinnet bliver stående på den yderste tone i stedet for at fortsætte rundt.
    /// En knap der springer fra den blegeste tilbage til den varmeste ville gøre det
    /// svært at mærke hvornår man er nået til enden.
    /// </summary>
    private static YellowTone Step(YellowTone tone, int direction)
    {
        int i = Array.IndexOf(Ordered, tone);
        if (i < 0) i = Array.IndexOf(Ordered, YellowTone.Pure);
        return Ordered[Math.Clamp(i + direction, 0, Ordered.Length - 1)];
    }
}
