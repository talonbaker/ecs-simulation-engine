using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Spatial;

/// <summary>
/// AT-09 / Critical guardrail: NPC walking across a room boundary fires on ProximityEventBus
/// but produces ZERO emissions on StructuralChangeBus.
/// </summary>
public class RoomMembershipNoStructuralEmitOnNpcTransitionTests
{
    [Fact]
    public void NpcRoomTransition_EmitsOnProximityBus_NotOnStructuralBus()
    {
        var em             = new EntityManager();
        var membership     = new EntityRoomMembership();
        var proximityBus   = new ProximityEventBus();
        var structuralBus  = new StructuralChangeBus();

        var sys = new RoomMembershipSystem(membership, proximityBus, structuralBus);

        // Two rooms side by side
        var roomA = em.CreateEntity();
        roomA.Add(default(RoomTag));
        roomA.Add(new RoomComponent { Bounds = new BoundsRect(0, 0, 5, 5) });

        var roomB = em.CreateEntity();
        roomB.Add(default(RoomTag));
        roomB.Add(new RoomComponent { Bounds = new BoundsRect(5, 0, 5, 5) });

        // NPC starts in room A
        var npc = em.CreateEntity();
        npc.Add(default(NpcTag));
        npc.Add(new PositionComponent { X = 2f, Z = 2f });

        var proximityEvents  = new List<RoomMembershipChanged>();
        var structuralEvents = new List<StructuralChangeEvent>();

        proximityBus.OnRoomMembershipChanged  += e => proximityEvents.Add(e);
        structuralBus.Subscribe(e => structuralEvents.Add(e));

        sys.Update(em, 0.016f);  // tick 1: NPC registers in room A

        // Move NPC to room B
        npc.Add(new PositionComponent { X = 7f, Z = 2f });
        sys.Update(em, 0.016f);  // tick 2: transition detected

        Assert.NotEmpty(proximityEvents);
        Assert.Empty(structuralEvents);
    }

    [Fact]
    public void NpcWalking100Tiles_ZeroStructuralEmissions()
    {
        var em            = new EntityManager();
        var membership    = new EntityRoomMembership();
        var proximityBus  = new ProximityEventBus();
        var structuralBus = new StructuralChangeBus();

        var sys = new RoomMembershipSystem(membership, proximityBus, structuralBus);

        var npc = em.CreateEntity();
        npc.Add(default(NpcTag));
        npc.Add(new PositionComponent { X = 0f, Z = 0f });

        var structuralEvents = new List<StructuralChangeEvent>();
        structuralBus.Subscribe(e => structuralEvents.Add(e));

        for (int i = 0; i < 100; i++)
        {
            npc.Add(new PositionComponent { X = i, Z = 0f });
            sys.Update(em, 0.016f);
        }

        Assert.Empty(structuralEvents);
    }
}
