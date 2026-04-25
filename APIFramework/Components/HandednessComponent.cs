namespace APIFramework.Components;

/// <summary>
/// Which side an NPC passes on in a narrow hallway. Set at spawn; consistent for that NPC's lifetime.
/// LeftSidePass → shift left relative to motion; RightSidePass → shift right.
/// Default is RightSidePass (majority convention).
/// </summary>
public struct HandednessComponent
{
    public HandednessSide Side;
}
