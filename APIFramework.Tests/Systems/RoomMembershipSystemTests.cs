using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>AT-05, AT-06: RoomMembershipSystem point-in-rect and transition events.</summary>
public class RoomMembershipSystemTests
{
    private static (EntityManager em, EntityRoomMembership membership, ProximityEventBus bus, RoomMembershipSystem sys)
        Setup()
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();
        var bus        = new ProximityEventBus();
        var structBus  = new StructuralChangeBus();
        var sys        = new RoomMembershipSystem(membership, bus, structBus);
        return (em, membership, bus, sys);
    }

    // -- AT-05: point-in-rect classification -----------------------------------

    [Fact]
    public void Entity_InsideRoom_GetsRoomAssigned()
    {
        var (em, membership, _, sys) = Setup();
        var room   = EntityTemplates.Room(em, "r1", "office", RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 20, 20));
        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f }); // tile (5,5) → inside

        sys.Update(em, 1f);

        Assert.Equal(room, membership.GetRoom(entity));
    }

    [Fact]
    public void Entity_OutsideRoom_GetsNullAssigned()
    {
        var (em, membership, _, sys) = Setup();
        EntityTemplates.Room(em, "r1", "office", RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 20, 20));
        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 25f, Y = 0f, Z = 25f }); // outside

        sys.Update(em, 1f);

        Assert.Null(membership.GetRoom(entity));
    }

    [Fact]
    public void OverlappingRooms_SmallestAreaWins()
    {
        var (em, membership, _, sys) = Setup();
        // Large room: 20×20 = 400 tiles
        var large = EntityTemplates.Room(em, "r-large", "floor", RoomCategory.CubicleGrid,   BuildingFloor.First, new BoundsRect(0, 0, 20, 20));
        // Small room: 5×5 = 25 tiles, wholly inside the large room
        var small = EntityTemplates.Room(em, "r-small", "office", RoomCategory.Office, BuildingFloor.First, new BoundsRect(5, 5, 5, 5));

        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 7f, Y = 0f, Z = 7f }); // inside both rooms

        sys.Update(em, 1f);

        Assert.Equal(small, membership.GetRoom(entity));
        Assert.NotEqual(large, membership.GetRoom(entity));
    }

    // -- AT-06: transition events ----------------------------------------------

    [Fact]
    public void EnteringRoom_FiresRoomMembershipChangedOnce()
    {
        var (em, _, bus, sys) = Setup();
        EntityTemplates.Room(em, "r1", "office", RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 20, 20));
        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });

        var events = new List<RoomMembershipChanged>();
        bus.OnRoomMembershipChanged += e => events.Add(e);

        sys.Update(em, 1f);

        Assert.Single(events);
        Assert.Equal(entity, events[0].Subject);
        Assert.Null(events[0].OldRoom);
        Assert.NotNull(events[0].NewRoom);
    }

    [Fact]
    public void StationaryEntity_NoEventOnSecondTick()
    {
        var (em, _, bus, sys) = Setup();
        EntityTemplates.Room(em, "r1", "office", RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 20, 20));
        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });

        sys.Update(em, 1f); // enters room

        var events = new List<RoomMembershipChanged>();
        bus.OnRoomMembershipChanged += e => events.Add(e);

        sys.Update(em, 1f); // still in same room

        Assert.Empty(events);
    }

    [Fact]
    public void MovingRooms_FiresTransitionEvent()
    {
        var (em, _, bus, sys) = Setup();
        var r1 = EntityTemplates.Room(em, "r1", "a", RoomCategory.Office, BuildingFloor.First, new BoundsRect(0,  0, 10, 10));
        var r2 = EntityTemplates.Room(em, "r2", "b", RoomCategory.Office, BuildingFloor.First, new BoundsRect(20, 0, 10, 10));
        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f }); // in r1

        sys.Update(em, 1f); // enters r1

        var events = new List<RoomMembershipChanged>();
        bus.OnRoomMembershipChanged += e => events.Add(e);

        entity.Add(new PositionComponent { X = 25f, Y = 0f, Z = 5f }); // move to r2
        sys.Update(em, 1f);

        Assert.Single(events);
        Assert.Equal(r1, events[0].OldRoom);
        Assert.Equal(r2, events[0].NewRoom);
    }

    [Fact]
    public void RoomEntity_NotClassifiedAsOccupant()
    {
        var (em, membership, _, sys) = Setup();
        var room = EntityTemplates.Room(em, "r1", "office", RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 20, 20));

        sys.Update(em, 1f);

        // The room entity itself should not appear in membership (RoomTag skipped)
        Assert.Null(membership.GetRoom(room));
    }
}
