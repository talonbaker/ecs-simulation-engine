namespace APIFramework.Systems.Spatial;

public enum StructuralChangeKind
{
    EntityMoved       = 0,
    EntityAdded       = 1,
    EntityRemoved     = 2,
    ObstacleAttached  = 3,
    ObstacleDetached  = 4,
    DoorwayAttached   = 5,
    DoorwayDetached   = 6,
    RoomBoundsChanged = 7,
}
