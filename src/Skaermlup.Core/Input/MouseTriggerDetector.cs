using Skaermlup.Core.Settings;

namespace Skaermlup.Core.Input;

public enum MouseButton
{
    Left,
    Right,
    Middle,
    SideBack,
    SideForward,
}

public readonly record struct MouseButtonEvent(MouseButton Button, bool IsDown, long TimestampMs);

public enum TriggerOutcome
{
    None,
    Toggle,
    TurnOn,
    TurnOff,
}

/// <param name="Outcome">Hvad der skal ske med luppen.</param>
/// <param name="Swallow">Sandt når klikket ikke må sendes videre til programmet under musen.</param>
public readonly record struct TriggerDecision(TriggerOutcome Outcome, bool Swallow)
{
    public static TriggerDecision Ignore => new(TriggerOutcome.None, false);
}

/// <summary>
/// Oversætter museklik til tænd/sluk af luppen. Hele pointen er at brugeren ikke skal lede
/// efter noget lille på skærmen: knappen sidder under fingeren i forvejen.
/// </summary>
public sealed class MouseTriggerDetector
{
    private long _leftDownAt = -1;
    private long _rightDownAt = -1;
    private bool _chordActive;

    /// <summary>Hvor tæt på hinanden venstre og højre knap skal trykkes for at tælle som ét greb.</summary>
    public int ChordWindowMs { get; set; } = 300;

    public MouseTriggerButtons Buttons { get; set; } = MouseTriggerButtons.Middle;

    public TriggerMode Mode { get; set; } = TriggerMode.Toggle;

    /// <summary>Skal udløserklikket holdes tilbage, så fx browseren ikke også reagerer på det?</summary>
    public bool BlockTriggerClicks { get; set; } = true;

    public void Reset()
    {
        _leftDownAt = -1;
        _rightDownAt = -1;
        _chordActive = false;
    }

    public TriggerDecision Decide(MouseButtonEvent e)
    {
        if (e.Button is MouseButton.Left or MouseButton.Right)
            return DecideChord(e);

        var flag = e.Button switch
        {
            MouseButton.Middle => MouseTriggerButtons.Middle,
            MouseButton.SideBack => MouseTriggerButtons.SideBack,
            MouseButton.SideForward => MouseTriggerButtons.SideForward,
            _ => MouseTriggerButtons.None,
        };

        if (flag == MouseTriggerButtons.None || !Buttons.HasFlag(flag))
            return TriggerDecision.Ignore;

        // Både tryk og slip holdes tilbage, så programmet under musen ikke får et halvt klik.
        bool swallow = BlockTriggerClicks;

        if (Mode == TriggerMode.Hold)
            return new TriggerDecision(e.IsDown ? TriggerOutcome.TurnOn : TriggerOutcome.TurnOff, swallow);

        return new TriggerDecision(e.IsDown ? TriggerOutcome.Toggle : TriggerOutcome.None, swallow);
    }

    private TriggerDecision DecideChord(MouseButtonEvent e)
    {
        if (!Buttons.HasFlag(MouseTriggerButtons.LeftRightChord))
            return TriggerDecision.Ignore;

        if (e.IsDown)
        {
            long other;
            if (e.Button == MouseButton.Left) { _leftDownAt = e.TimestampMs; other = _rightDownAt; }
            else { _rightDownAt = e.TimestampMs; other = _leftDownAt; }

            if (other >= 0 && !_chordActive && e.TimestampMs - other <= ChordWindowMs)
            {
                _chordActive = true;
                // Venstre og højre knap sendes altid videre: de er for vigtige til at æde.
                return new TriggerDecision(
                    Mode == TriggerMode.Hold ? TriggerOutcome.TurnOn : TriggerOutcome.Toggle, false);
            }

            return TriggerDecision.Ignore;
        }

        if (e.Button == MouseButton.Left) _leftDownAt = -1; else _rightDownAt = -1;

        if (_chordActive && _leftDownAt < 0 && _rightDownAt < 0)
        {
            _chordActive = false;
            if (Mode == TriggerMode.Hold)
                return new TriggerDecision(TriggerOutcome.TurnOff, false);
        }

        return TriggerDecision.Ignore;
    }
}
