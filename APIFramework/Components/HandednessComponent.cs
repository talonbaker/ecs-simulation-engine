namespace APIFramework.Components;

/// <summary>
/// Which side an NPC passes on in a narrow hallway. Set at spawn; consistent for that NPC's lifetime.
/// LeftSidePass → shift left relative to motion; RightSidePass → shift right.
/// Default is RightSidePass (majority convention).
/// </summary>
public struct HandednessComponent
{
    /// <summary>The side of a hallway this NPC prefers to pass on. Stable for the NPC's lifetime.</summary>
    public HandednessSide Side;
}
