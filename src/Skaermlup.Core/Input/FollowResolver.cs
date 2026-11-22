using Skaermlup.Core.Geometry;

namespace Skaermlup.Core.Input;

public enum FocusSource
{
    Mouse,
    Caret,
    Keyboard,

    /// <summary>Det felt tastaturet står i — en knap, et afkrydsningsfelt, en række i en liste.</summary>
    Focus,

    /// <summary>Luppen er trukket på plads med en finger, som et forstørrelsesglas.</summary>
    Touch,
}

/// <summary>
/// Bestemmer hvilket punkt luppen skal stå på. Musen har altid forkørselsret; skriver brugeren
/// i stedet, følger luppen tekstmarkøren, så bogstaverne kommer frem efterhånden som de skrives.
/// Flytter brugeren rundt med Tab, følger luppen det felt tastaturet står i. Pilene på
/// tastaturet kan overtage styringen helt, for dem der ikke bruger mus.
/// </summary>
public sealed class FollowResolver
{
    private PixelPoint _mouseAnchor;
    private PixelPoint _caretAnchor;
    private PixelPoint _focusAnchor;
    private PixelPoint _keyboardPoint;
    private PixelPoint _current;
    private PixelRect? _lastFocus;
    private bool _focusPrimed;
    private bool _seeded;

    /// <summary>Hvor langt musen skal flytte sig for at overtage styringen igen.</summary>
    public int MouseWakeDistancePx { get; set; } = 8;

    public bool FollowCaret { get; set; } = true;

    public bool FollowFocus { get; set; } = true;

    public FocusSource Source { get; private set; } = FocusSource.Mouse;

    public PixelPoint Current => _current;

    /// <param name="mouse">Musens nuværende placering.</param>
    /// <param name="caret">Tekstmarkørens rektangel, hvis der er en. Ellers null.</param>
    /// <param name="focus">
    /// Feltet tastaturet står i, hvis det er til nogen hjælp (se <see cref="FocusFilter"/>). Ellers null.
    /// </param>
    public PixelPoint Resolve(PixelPoint mouse, PixelRect? caret, PixelRect? focus = null)
    {
        if (!_seeded)
        {
            _seeded = true;
            _mouseAnchor = mouse;
            _keyboardPoint = mouse;
            _current = mouse;

            // Det felt der havde fokus, da luppen blev tændt, er ikke en bevægelse. Luppen
            // starter ved musen.
            _lastFocus = focus;
            _focusPrimed = true;
            return _current;
        }

        // Et nyt fokusfelt bemærkes én gang, uanset hvem der vinder i denne omgang. Ellers ville
        // et fokusskift, som musen eller skrivemarkøren overtrumfede, springe frem senere.
        // Er luppen tændt med en finger, er dette første gang feltet ses — og det er heller ikke
        // en bevægelse.
        bool focusMoved = FollowFocus && _focusPrimed && focus is { IsEmpty: false } && focus != _lastFocus;
        _lastFocus = focus;
        _focusPrimed = true;

        int wake = Math.Max(1, MouseWakeDistancePx);
        if (mouse.DistanceSquaredTo(_mouseAnchor) >= wake * wake)
        {
            _mouseAnchor = mouse;
            _keyboardPoint = mouse;
            Source = FocusSource.Mouse;
            _current = mouse;
            return _current;
        }

        if (FollowCaret && caret is { IsEmpty: false } rect)
        {
            var point = rect.Center;
            if (point != _caretAnchor)
            {
                _caretAnchor = point;
                _keyboardPoint = point;
                Source = FocusSource.Caret;
                _current = point;
                return _current;
            }

            if (Source == FocusSource.Caret)
            {
                _current = _caretAnchor;
                return _current;
            }
        }
        else if (Source == FocusSource.Caret)
        {
            // Tekstmarkøren forsvandt (fx feltet mistede fokus) — musen tager over igen,
            // medmindre fokus er flyttet til et nyt felt. Så følges det herunder.
            Source = FocusSource.Mouse;
        }

        // Et klik flytter også fokus — til det man klikkede på. Står musen inde i det nye felt,
        // er det musen der har gjort det, og så bliver luppen ved musen. Ellers ville hvert klik
        // trække luppen hen til midten af den knap, man lige havde ramt.
        if (focusMoved && focus is { } field && !field.Contains(mouse))
        {
            _focusAnchor = FocusFilter.Target(field);
            _keyboardPoint = _focusAnchor;
            Source = FocusSource.Focus;
            _current = _focusAnchor;
            return _current;
        }

        _current = Source switch
        {
            FocusSource.Keyboard or FocusSource.Touch => _keyboardPoint,
            FocusSource.Caret => _caretAnchor,
            FocusSource.Focus => _focusAnchor,
            _ => _mouseAnchor,
        };
        return _current;
    }

    /// <summary>
    /// Luppen er trukket hen til et punkt med en finger. Den bliver dér, til musen, tekstmarkøren
    /// eller tastaturet flytter den igen.
    /// </summary>
    /// <param name="point">Det punkt luppen skal vise.</param>
    /// <param name="mouseNow">
    /// Musens plads lige nu. Bruges kun hvis luppen lige er tændt, så musen ikke bagefter tror,
    /// at den er flyttet langt og river luppen med sig.
    /// </param>
    public PixelPoint PlaceAt(PixelPoint point, PixelPoint mouseNow)
    {
        if (!_seeded)
        {
            _seeded = true;
            _mouseAnchor = mouseNow;
        }

        _keyboardPoint = point;
        Source = FocusSource.Touch;
        _current = point;
        return point;
    }

    /// <summary>Flytter fokuspunktet med tastaturet og låser styringen til tastaturet.</summary>
    public PixelPoint PanBy(int dx, int dy, PixelRect bounds)
    {
        if (!_seeded)
        {
            _seeded = true;
            _keyboardPoint = bounds.IsEmpty ? PixelPoint.Zero : bounds.Center;
        }
        else if (Source != FocusSource.Keyboard)
        {
            _keyboardPoint = _current;
        }

        var next = new PixelPoint(_keyboardPoint.X + dx, _keyboardPoint.Y + dy);
        if (!bounds.IsEmpty)
        {
            next = new PixelPoint(
                Math.Clamp(next.X, bounds.X, bounds.Right - 1),
                Math.Clamp(next.Y, bounds.Y, bounds.Bottom - 1));
        }

        _keyboardPoint = next;
        Source = FocusSource.Keyboard;
        _current = next;
        return next;
    }
}
