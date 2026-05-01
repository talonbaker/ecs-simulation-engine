using System;

namespace APIFramework.Systems.Spatial;

/// <summary>
/// Emitted on StructuralChangeBus to signal that the pathfinding topology has changed.
/// Producers fill what's relevant; consumers ignore irrelevant fields.
/// </summary>
public readonly record struct StructuralChangeEvent(
    StructuralChangeKind Kind,
    Guid                 EntityId,
    int                  PreviousTileX,
    int                  PreviousTileY,
    int                  CurrentTileX,
    int                  CurrentTileY,
    Guid                 RoomId,           // affected room, or Guid.Empty if cross-room or floor-level
    long                 TopologyVersion,  // version stamped at emission
    long                 Tick
);
