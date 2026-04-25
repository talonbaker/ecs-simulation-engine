using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>
/// A cached A* path computed by PathfindingService.
/// Written by PathfindingTriggerSystem when MovementTargetComponent changes.
/// Consumed by MovementSystem (advances CurrentWaypointIndex per waypoint arrival).
/// Removed by MovementSystem once the final waypoint is reached.
/// </summary>
public struct PathComponent
{
    /// <summary>Ordered tile waypoints from start (exclusive) to goal (inclusive).</summary>
    public IReadOnlyList<(int X, int Y)> Waypoints;

    /// <summary>Index of the waypoint the NPC is currently steering toward.</summary>
    public int CurrentWaypointIndex;
}
