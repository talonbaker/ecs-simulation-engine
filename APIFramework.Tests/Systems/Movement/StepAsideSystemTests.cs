using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Movement;

/// <summary>
/// AT-06 to AT-07: StepAsideSystem — head-on avoidance in hallways.
/// </summary>
public class StepAsideSystemTests
{
    private static (EntityManager em, ISpatialIndex idx, EntityRoomMembership rooms, StepAsideSystem sys)
        BuildSetup(float stepAsideRadius = 5f, float stepAsideShift = 0.4f)
    {
        var em    = new EntityManager();
        var idx   = new GridSpatialIndex(4, 64, 64);
        var rooms = new EntityRoomMembership();
        var cfg   = new MovementConfig { StepAsideRadius = stepAsideRadius, StepAsideShift = stepAsideShift };
        var sys   = new StepAsideSystem(idx, rooms, cfg);
        return (em, idx, rooms, sys);
    }

    private static Entity SpawnNpc(EntityManager em, ISpatialIndex idx, EntityRoomMembership rooms,
        float posX, float posZ, float velX, float velZ,
        HandednessSide side, Entity? room = null)
    {
        var e = em.CreateEntity();
        e.Add(new PositionComponent { X = posX, Y = 0f, Z = posZ });
        e.Add(new MovementComponent { Speed = 1f, SpeedModifier = 1f, LastVelocityX = velX, LastVelocityZ = velZ });
        e.Add(new HandednessComponent { Side = side });
        e.Add(new NpcTag());

        int tx = (int)MathF.Round(posX);
        int ty = (int)MathF.Round(posZ);
        idx.Register(e, tx, ty);

        if (room != null)
            rooms.SetRoom(e, room);

        return e;
    }

    private static Entity SpawnHallway(EntityManager em)
    {
        var room = em.CreateEntity();
        room.Add(new RoomTag());
        room.Add(new RoomComponent
        {
            Id       = "hall",
            Name     = "Hallway",
            Category = RoomCategory.Hallway,
            Bounds   = new BoundsRect(0, 0, 20, 20),
        });
        return room;
    }

    private static Entity SpawnBreakroom(EntityManager em)
    {
        var room = em.CreateEntity();
        room.Add(new RoomTag());
        room.Add(new RoomComponent
        {
            Id       = "break",
            Name     = "Breakroom",
            Category = RoomCategory.Breakroom,
            Bounds   = new BoundsRect(0, 0, 20, 20),
        });
        return room;
    }

    // AT-06: Two NPCs approaching head-on in a Hallway each get a perpendicular shift
    [Fact]
    public void HeadOn_InHallway_BothNpcsShift()
    {
        var (em, idx, rooms, sys) = BuildSetup();
        var hallway = SpawnHallway(em);

        // NPC A at (5, 5) moving right (+X direction): velX=1, velZ=0
        // NPC B at (8, 5) moving left  (-X direction): velX=-1, velZ=0
        var npcA = SpawnNpc(em, idx, rooms, 5f, 5f,  1f, 0f, HandednessSide.RightSidePass, hallway);
        var npcB = SpawnNpc(em, idx, rooms, 8f, 5f, -1f, 0f, HandednessSide.RightSidePass, hallway);

        float beforeAZ = npcA.Get<PositionComponent>().Z;
        float beforeBZ = npcB.Get<PositionComponent>().Z;

        sys.Update(em, 1f);

        float afterAZ = npcA.Get<PositionComponent>().Z;
        float afterBZ = npcB.Get<PositionComponent>().Z;

        // Both NPCs should have their Z position shifted (perpendicular to X movement)
        Assert.NotEqual(beforeAZ, afterAZ);
        Assert.NotEqual(beforeBZ, afterBZ);
    }

    // AT-06: RightSidePass → shift in +Z when moving in +X direction
    [Fact]
    public void RightSidePass_MovingRight_ShiftsPositiveZ()
    {
        var (em, idx, rooms, sys) = BuildSetup();
        var hallway = SpawnHallway(em);

        var npcA = SpawnNpc(em, idx, rooms, 5f, 5f,  1f, 0f, HandednessSide.RightSidePass, hallway);
        var npcB = SpawnNpc(em, idx, rooms, 8f, 5f, -1f, 0f, HandednessSide.RightSidePass, hallway);

        sys.Update(em, 1f);

        // A moves right (+X); right perpendicular is +Z
        float shiftA = npcA.Get<PositionComponent>().Z - 5f;
        Assert.True(shiftA > 0f, $"NPC A (RightSidePass, moving right) should shift to +Z, got {shiftA}");
    }

    // AT-06: LeftSidePass → shift in -Z when moving in +X direction
    [Fact]
    public void LeftSidePass_MovingRight_ShiftsNegativeZ()
    {
        var (em, idx, rooms, sys) = BuildSetup();
        var hallway = SpawnHallway(em);

        var npcA = SpawnNpc(em, idx, rooms, 5f, 5f,  1f, 0f, HandednessSide.LeftSidePass, hallway);
        var npcB = SpawnNpc(em, idx, rooms, 8f, 5f, -1f, 0f, HandednessSide.RightSidePass, hallway);

        sys.Update(em, 1f);

        float shiftA = npcA.Get<PositionComponent>().Z - 5f;
        Assert.True(shiftA < 0f, $"NPC A (LeftSidePass, moving right) should shift to -Z, got {shiftA}");
    }

    // AT-07: Two NPCs in a Breakroom (non-hallway) do NOT get a step-aside shift
    [Fact]
    public void HeadOn_InBreakroom_NpcsDoNotShift()
    {
        var (em, idx, rooms, sys) = BuildSetup();
        var breakroom = SpawnBreakroom(em);

        var npcA = SpawnNpc(em, idx, rooms, 5f, 5f,  1f, 0f, HandednessSide.RightSidePass, breakroom);
        var npcB = SpawnNpc(em, idx, rooms, 8f, 5f, -1f, 0f, HandednessSide.RightSidePass, breakroom);

        float beforeAZ = npcA.Get<PositionComponent>().Z;
        float beforeBZ = npcB.Get<PositionComponent>().Z;

        sys.Update(em, 1f);

        Assert.Equal(beforeAZ, npcA.Get<PositionComponent>().Z);
        Assert.Equal(beforeBZ, npcB.Get<PositionComponent>().Z);
    }

    // NPCs moving in the same direction should not step aside
    [Fact]
    public void SameDirection_NoStepAside()
    {
        var (em, idx, rooms, sys) = BuildSetup();
        var hallway = SpawnHallway(em);

        var npcA = SpawnNpc(em, idx, rooms, 5f, 5f, 1f, 0f, HandednessSide.RightSidePass, hallway);
        var npcB = SpawnNpc(em, idx, rooms, 7f, 5f, 1f, 0f, HandednessSide.RightSidePass, hallway);

        float beforeAZ = npcA.Get<PositionComponent>().Z;

        sys.Update(em, 1f);

        Assert.Equal(beforeAZ, npcA.Get<PositionComponent>().Z);
    }

    // NPCs in a null room (no room assigned) do not step aside
    [Fact]
    public void NoRoom_NoStepAside()
    {
        var (em, idx, rooms, sys) = BuildSetup();

        var npcA = SpawnNpc(em, idx, rooms, 5f, 5f,  1f, 0f, HandednessSide.RightSidePass, room: null);
        var npcB = SpawnNpc(em, idx, rooms, 8f, 5f, -1f, 0f, HandednessSide.RightSidePass, room: null);

        float beforeAZ = npcA.Get<PositionComponent>().Z;

        sys.Update(em, 1f);

        Assert.Equal(beforeAZ, npcA.Get<PositionComponent>().Z);
    }
}
