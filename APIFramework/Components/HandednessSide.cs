namespace APIFramework.Components;

/// <summary>Which side of a hallway an NPC prefers to pass on. Used by the path-conflict resolver.</summary>
public enum HandednessSide
{
    /// <summary>NPC shifts to the left side relative to the direction of motion.</summary>
    LeftSidePass  = 0,
    /// <summary>NPC shifts to the right side relative to the direction of motion (majority convention).</summary>
    RightSidePass = 1,
}
