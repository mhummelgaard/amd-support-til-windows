using Skaermlup.Core.Settings;

namespace Skaermlup.Core.Input;

/// <param name="Learned">Knappen brugeren valgte, når valget er færdigt. Ellers null.</param>
/// <param name="Swallow">Sandt når trykket ikke må nå programmet under musen.</param>
public readonly record struct LearnStep(MouseTriggerButtons? Learned, bool Swallow);

/// <summary>
/// Finder udløserknappen ved at brugeren trykker på den, i stedet for at vælge den i en liste.
///
/// Windows' oplysning om hvor mange knapper musen har, kan ikke bruges til at gætte: en helt
/// almindelig mus meldte 16. Og navne som "bageste sideknap" er svære at koble til en bestemt
/// knap, når man ikke kan se musen tydeligt. Et tryk på den rigtige knap er entydigt.
///
/// Kun knapper der ikke bruges til andet — midterste og sideknapperne. Venstre og højre går
/// uændret igennem: det er dem, brugeren klikker med, også for at starte valget.
/// </summary>
public sealed class TriggerLearner
{
    private MouseButton? _pressed;

    public LearnStep Observe(MouseButtonEvent e)
    {
        if (ToTrigger(e.Button) is not { } trigger) return new LearnStep(null, Swallow: false);

        if (e.IsDown)
        {
            // Holdes tilbage, så fx browseren ikke åbner et link i en ny fane.
            _pressed = e.Button;
            return new LearnStep(null, Swallow: true);
        }

        // Først når knappen slippes igen, er valget færdigt. Ellers ville slippet nå programmet
        // under musen uden det tryk, der hørte til.
        if (_pressed != e.Button) return new LearnStep(null, Swallow: true);

        _pressed = null;
        return new LearnStep(trigger, Swallow: true);
    }

    public void Reset() => _pressed = null;

    private static MouseTriggerButtons? ToTrigger(MouseButton button) => button switch
    {
        MouseButton.Middle => MouseTriggerButtons.Middle,
        MouseButton.SideBack => MouseTriggerButtons.SideBack,
        MouseButton.SideForward => MouseTriggerButtons.SideForward,
        _ => null,
    };
}
