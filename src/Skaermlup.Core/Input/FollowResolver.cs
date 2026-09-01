using Skaermlup.Core.Geometry;

namespace Skaermlup.Core.Input;

public enum FocusSource
{
    Mouse,
    Caret,
    Keyboard,
}

/// <summary>
/// Bestemmer hvilket punkt luppen skal stå på. Musen har altid forkørselsret; skriver brugeren
/// i stedet, følger luppen tekstmarkøren, så bogstaverne kommer frem efterhånden som de skrives.
/// Pilene på tastaturet kan overtage styringen helt, for dem der ikke bruger mus.
/// </summary>
public sealed class FollowResolver
{
    private PixelPoint _mouseAnchor;
    private PixelPoint _caretAnchor;
    private PixelPoint _keyboardPoint;
    private PixelPoint _current;
    private bool _seeded;

    /// <summary>Hvor langt musen skal flytte sig for at overtage styringen igen.</summary>
    public int MouseWakeDistancePx { get; set; } = 8;

    public bool FollowCaret { get; set; } = true;

    public FocusSource Source { get; private set; } = FocusSource.Mouse;

    public PixelPoint Current => _current;

    /// <param name="mouse">Musens nuværende placering.</param>
    /// <param name="caret">Tekstmarkørens rektangel, hvis der er en. Ellers null.</param>
    public PixelPoint Resolve(PixelPoint mouse, PixelRect? caret)
    {
        if (!_seeded)
        {
            _seeded = true;
            _mouseAnchor = mouse;
            _keyboardPoint = mouse;
            _current = mouse;
            return _current;
        }

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
            // Tekstmarkøren forsvandt (fx feltet mistede fokus) — musen tager over igen.
            Source = FocusSource.Mouse;
        }

        _current = Source switch
        {
            FocusSource.Keyboard => _keyboardPoint,
            FocusSource.Caret => _caretAnchor,
            _ => _mouseAnchor,
        };
        return _current;
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
