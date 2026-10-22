using Skaermlup.Core.Geometry;

namespace Skaermlup.Core.Input;

/// <summary>
/// Opdager at Windows har fjernet lytteren på musen uden at sige det.
///
/// Svarer en lytter på musen ikke hurtigt nok, fjerner Windows den i stilhed. Programmet får
/// ingen besked, og der findes intet kald, der kan spørge om den stadig er der. Så holder
/// museknappen op med at tænde luppen, mens vejledningen på skærmen bliver ved med at sige
/// at den virker — og brugeren tror at det er dem, der gør noget forkert.
///
/// Tegnet er indirekte: musen har flyttet sig, men lytteren har ikke hørt noget. Hver
/// bevægelse går gennem lytteren, så har den været tavs mens musen flyttede sig, er den væk.
///
/// En falsk alarm koster næsten ingenting — lytteren sættes bare op igen — så vagten må hellere
/// slå til en gang for meget end en gang for lidt.
/// </summary>
public sealed class HookWatchdog
{
    private PixelPoint _lastCursor;
    private long _lastCheckMs;
    private bool _started;

    /// <param name="cursor">Musens position nu.</param>
    /// <param name="lastHookEventMs">Hvornår lytteren sidst hørte noget, målt på samme ur som <paramref name="nowMs"/>.</param>
    /// <param name="nowMs">Tidspunktet for dette tjek.</param>
    /// <returns>Sandt hvis lytteren bør sættes op igen.</returns>
    public bool Check(PixelPoint cursor, long lastHookEventMs, long nowMs)
    {
        bool moved = _started && cursor != _lastCursor;
        bool silentSinceLastCheck = lastHookEventMs < _lastCheckMs;
        bool lost = moved && silentSinceLastCheck;

        _lastCursor = cursor;
        _lastCheckMs = nowMs;
        _started = true;
        return lost;
    }
}
