using System;

namespace APIFramework.Components;

/// <summary>
/// The NPC's resistance reservoir. Depletes with sustained suppression; regenerates with rest.
/// Both Current and Baseline stay in 0–100.
/// WillpowerSystem drains the WillpowerEventQueue each tick and applies deltas here.
/// </summary>
public struct WillpowerComponent
{
    /// <summary>Live willpower, 0–100. Drained by suppression events; regenerated during rest.</summary>
    public int Current;
    /// <summary>Resting willpower target, 0–100. Set at spawn and stable for the save.</summary>
    public int Baseline;

    /// <summary>Constructs a willpower component. Both values are clamped to [0, 100].</summary>
    public WillpowerComponent(int current, int baseline)
    {
        Current  = Math.Clamp(current,  0, 100);
        Baseline = Math.Clamp(baseline, 0, 100);
    }
}
