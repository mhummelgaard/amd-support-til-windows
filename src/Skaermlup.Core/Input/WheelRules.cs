using Skaermlup.Core.Settings;

namespace Skaermlup.Core.Input;

public enum WheelOutcome
{
    None,
    ZoomIn,
    ZoomOut,
    LensBigger,
    LensSmaller,
}

public readonly record struct WheelDecision(WheelOutcome Outcome, bool Swallow)
{
    public static WheelDecision Ignore => new(WheelOutcome.None, false);
}

/// <summary>
/// Musehjulets betydning mens luppen kører. Uden modifikationstast ruller hjulet som normalt —
/// ellers kunne man ikke læse en side færdig med luppen tændt.
/// </summary>
public static class WheelRules
{
    public static WheelDecision Decide(int delta, bool ctrl, bool shift, bool magnifierOn, WheelAction action)
    {
        if (!magnifierOn || delta == 0) return WheelDecision.Ignore;

        if (ctrl)
            return new WheelDecision(delta > 0 ? WheelOutcome.ZoomIn : WheelOutcome.ZoomOut, true);

        if (shift)
            return new WheelDecision(delta > 0 ? WheelOutcome.LensBigger : WheelOutcome.LensSmaller, true);

        if (action == WheelAction.AlwaysZoom)
            return new WheelDecision(delta > 0 ? WheelOutcome.ZoomIn : WheelOutcome.ZoomOut, true);

        return WheelDecision.Ignore;
    }
}
