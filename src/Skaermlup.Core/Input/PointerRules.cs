using Skaermlup.Core.Settings;

namespace Skaermlup.Core.Input;

/// <summary>Hvad Windows' egen musepil skal se ud som lige nu.</summary>
public enum PointerVisual
{
    /// <summary>Brugerens egne markører, urørte.</summary>
    System = 0,

    /// <summary>Skjult, fordi luppen tegner sin egen markering ovenpå.</summary>
    Hidden = 1,

    /// <summary>Markøren med en kraftig ring om. Ringen tegnes af Windows selv og ligger
    /// derfor øverst — også oven på Start-menuen, hvor luppen ikke kan komme til.</summary>
    HighlightRing = 2,
}

/// <summary>
/// Ét sted der afgør hvad der skal ske med musepilen. Reglen er lille, men den er nem at
/// tage fejl af, og fejlen er dyr: bliver pilen skjult ét sted for meget, står brugeren
/// uden markør.
/// </summary>
public static class PointerRules
{
    /// <param name="lensIsOn">Om luppen er tændt. Er den slukket, rører vi ikke markørerne.</param>
    /// <param name="marker">Den markering luppen selv tegner.</param>
    /// <param name="shellInFront">Om Windows' egen skal ligger oven på luppen lige nu.</param>
    /// <param name="ringWanted">Brugerens valg: må vi sætte en ring om markøren?</param>
    /// <param name="ringPossible">Om der overhovedet er plads til en ring ved den
    /// markørstørrelse systemet er sat op med.</param>
    public static PointerVisual Decide(
        bool lensIsOn, MarkerStyle marker, bool shellInFront, bool ringWanted, bool ringPossible)
    {
        // Luppen er slukket: der er ingen grund til at have rørt ved noget.
        if (!lensIsOn) return PointerVisual.System;

        // Skallen dækker luppen. Her er markøren det eneste brugeren har, så den skal frem —
        // med ring hvis der er plads, og ellers bare som den plejer.
        if (shellInFront)
            return ringWanted && ringPossible ? PointerVisual.HighlightRing : PointerVisual.System;

        // Har brugeren valgt at bruge musens egen pil, tegner luppen ingenting, og så er
        // pilen netop det der skal ses.
        if (marker == MarkerStyle.None) return PointerVisual.System;

        return PointerVisual.Hidden;
    }
}
