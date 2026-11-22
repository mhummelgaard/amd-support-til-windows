namespace Skaermlup.Core.Input;

/// <summary>
/// Skelner en finger fra en mus. Windows laver tryk på en berøringsskærm om til museklik, så
/// almindelige programmer virker med fingeren — men mærker hvert af dem med en signatur i
/// beskedens ekstra oplysninger. Det er den, der læses her.
///
/// Målt på Windows 11 (26100): et tryk med fingeren giver ekstra-oplysningen 0xFF515782.
/// </summary>
public static class InputSignature
{
    // Microsofts egne konstanter: MI_WP_SIGNATURE og SIGNATURE_MASK. Bit 0x80 betyder touch.
    private const uint PenOrTouchSignature = 0xFF515700;
    private const uint SignatureMask = 0xFFFFFF00;
    private const uint TouchBit = 0x80;

    /// <summary>Sandt når musebeskeden i virkeligheden kom fra en finger.</summary>
    public static bool IsTouch(ulong extraInfo)
    {
        uint value = (uint)extraInfo;
        return (value & SignatureMask) == PenOrTouchSignature && (value & TouchBit) != 0;
    }

    /// <summary>Sandt når musebeskeden kom fra en finger eller en pen — ikke fra en rigtig mus.</summary>
    public static bool IsPenOrTouch(ulong extraInfo) => ((uint)extraInfo & SignatureMask) == PenOrTouchSignature;
}
