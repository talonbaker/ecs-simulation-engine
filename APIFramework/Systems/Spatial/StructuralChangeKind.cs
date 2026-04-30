namespace APIFramework.Systems.Spatial;

/// <summary>
/// Enumerates the kinds of topology-affecting changes that invalidate pathfinding caches.
/// Emitted by StructuralChangeBus to notify systems that the walkability/doorway graph
/// has changed.
/// </summary>
public enum StructuralChangeKind
{
    /// <summary>A StructuralTag entity changed PositionComponent.</summary>
    EntityMoved        = 0,

    /// <summary>A StructuralTag entity was spawned at a tile.</summary>
    EntityAdded        = 1,

    /// <summary>A StructuralTag entity was destroyed or had its tag removed.</summary>
    EntityRemoved      = 2,

    /// <summary>ObstacleTag added to an existing entity.</summary>
    ObstacleAttached   = 3,

    /// <summary>ObstacleTag removed.</summary>
    ObstacleDetached   = 4,

    /// <summary>DoorwayTag added (or whatever doorway marker the engine uses).</summary>
    DoorwayAttached    = 5,

    /// <summary>DoorwayTag removed.</summary>
    DoorwayDetached    = 6,

    /// <summary>RoomComponent.Bounds changed.</summary>
    RoomBoundsChanged  = 7,
}
