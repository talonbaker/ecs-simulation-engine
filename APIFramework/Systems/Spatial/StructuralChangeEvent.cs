using System;

namespace APIFramework.Systems.Spatial;

public readonly record struct StructuralChangeEvent(
    StructuralChangeKind Kind,
    Guid                 EntityId,
    int                  PreviousTileX,
    int                  PreviousTileY,
    int                  CurrentTileX,
    int                  CurrentTileY,
    Guid                 RoomId,
    long                 TopologyVersion,
    long                 Tick
);
