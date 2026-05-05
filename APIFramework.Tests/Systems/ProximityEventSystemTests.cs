using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>AT-07, AT-08, AT-09: ProximityEventSystem conversation-range events and ordering.</summary>
public class ProximityEventSystemTests
{
    private record TestSetup(
        EntityManager          Em,
        GridSpatialIndex       Idx,
        SpatialIndexSyncSystem Sync,
        EntityRoomMembership   Membership,
        ProximityEventBus      Bus,
        RoomMembershipSystem   RoomSys,
        ProximityEventSystem   ProxSys);

    private static TestSetup Setup()
    {
        var em         = new EntityManager();
        var idx        = new GridSpatialIndex(4, 128, 128);
        var structBus  = new StructuralChangeBus();
        var sync       = new SpatialIndexSyncSystem(idx, structBus);
        var membership = new EntityRoomMembership();
        var bus        = new ProximityEventBus();
        var roomSys    = new RoomMembershipSystem(membership, bus, structBus);
        var proxSys    = new ProximityEventSystem(idx, bus, membership);
        em.EntityDestroyed += sync.OnEntityDestroyed;
        return new TestSetup(em, idx, sync, membership, bus, roomSys, proxSys);
    }

    private static void Tick(TestSetup s)
    {
        s.Sync.Update(s.Em, 1f);
        s.RoomSys.Update(s.Em, 1f);
        s.ProxSys.Update(s.Em, 1f);
    }

    private static Entity SpawnNpc(EntityManager em, float x, float z)
    {
        var e = em.CreateEntity();
        e.Add(new PositionComponent { X = x, Y = 0f, Z = z });
        e.Add(ProximityComponent.Default);  // conv=2, awareness=8
        e.Add(new NpcTag());
        return e;
    }

    // -- AT-07: EnteredConversationRange fires exactly once --------------------

    [Fact]
    public void TwoNpcs_Approaching_EnteredConversationRange_FiresOnce()
    {
        var s   = Setup();
        var npc1 = SpawnNpc(s.Em, 0f, 0f);
        var npc2 = SpawnNpc(s.Em, 10f, 0f); // 10 tiles apart — outside awareness

        var entered = new List<ProximityEnteredConversationRange>();
        s.Bus.OnEnteredConversationRange += e => entered.Add(e);

        Tick(s);   // npc2 outside awareness range — no event

        // Move npc2 to tile (1,0) — 1 tile from npc1 → inside conversation range (≤2)
        npc2.Add(new PositionComponent { X = 1f, Y = 0f, Z = 0f });
        Tick(s);

        // Each NPC fires for the other → expect 2 events total (npc1 observes npc2, npc2 observes npc1)
        Assert.Equal(2, entered.Count);

        // Second tick — no movement, no new events
        Tick(s);
        Assert.Equal(2, entered.Count); // still just 2
    }

    [Fact]
    public void TwoNpcs_AlreadyInRange_NoRepeatOnNextTick()
    {
        var s    = Setup();
        var npc1 = SpawnNpc(s.Em, 0f, 0f);
        var npc2 = SpawnNpc(s.Em, 1f, 0f);  // already within conv range

        var entered = new List<ProximityEnteredConversationRange>();
        s.Bus.OnEnteredConversationRange += e => entered.Add(e);

        Tick(s);   // enter — 2 events (symmetric)
        int countAfterFirst = entered.Count;

        Tick(s);   // stay — no new events
        Assert.Equal(countAfterFirst, entered.Count);
    }

    // -- AT-08: LeftConversationRange fires exactly once -----------------------

    [Fact]
    public void TwoNpcs_Receding_LeftConversationRange_FiresOnce()
    {
        var s    = Setup();
        var npc1 = SpawnNpc(s.Em, 0f, 0f);
        var npc2 = SpawnNpc(s.Em, 1f, 0f);  // in conv range (1 tile)

        var left = new List<ProximityLeftConversationRange>();
        s.Bus.OnLeftConversationRange += e => left.Add(e);

        Tick(s);   // enter — sets up previous state

        // Move npc2 far away
        npc2.Add(new PositionComponent { X = 50f, Y = 0f, Z = 0f });
        Tick(s);

        Assert.Equal(2, left.Count); // symmetric: each NPC fires LeftConversationRange for the other

        // Third tick — no more events
        Tick(s);
        Assert.Equal(2, left.Count);
    }

    // -- AT-09: entity-id-ascending order -------------------------------------

    [Fact]
    public void Events_FiredInEntityIdAscendingOrder()
    {
        var s    = Setup();
        // Spawn three NPCs close together; observe the fire order
        var n1 = SpawnNpc(s.Em, 0f, 0f);
        var n2 = SpawnNpc(s.Em, 1f, 0f);
        var n3 = SpawnNpc(s.Em, 0f, 1f);

        var order = new List<(Guid obs, Guid tgt)>();
        s.Bus.OnEnteredConversationRange += e => order.Add((e.Observer.Id, e.Target.Id));

        Tick(s);

        // Verify that observers fire in ascending Id order
        for (int i = 1; i < order.Count; i++)
        {
            int cmpObs = order[i - 1].obs.CompareTo(order[i].obs);
            if (cmpObs == 0)
                Assert.True(order[i - 1].tgt.CompareTo(order[i].tgt) <= 0,
                    "Events with same observer should be sorted by target id");
            else
                Assert.True(cmpObs < 0,
                    "Events should be sorted by observer id ascending");
        }
    }

    // -- Multiple NPC scenario -------------------------------------------------

    [Fact]
    public void MultipleNpcs_ClusteredThenScattered_CorrectEventCounts()
    {
        var s = Setup();
        // 4 NPCs starting at origin (all in conv range of each other)
        var npcs = new[]
        {
            SpawnNpc(s.Em, 0f, 0f),
            SpawnNpc(s.Em, 1f, 0f),
            SpawnNpc(s.Em, 0f, 1f),
            SpawnNpc(s.Em, 1f, 1f),
        };

        var entered = new List<ProximityEnteredConversationRange>();
        var left    = new List<ProximityLeftConversationRange>();
        s.Bus.OnEnteredConversationRange += e => entered.Add(e);
        s.Bus.OnLeftConversationRange    += e => left.Add(e);

        Tick(s);
        // 4 NPCs each seeing 3 others = 12 enter events
        Assert.Equal(12, entered.Count);

        // Scatter all NPCs far apart
        float[] farPos = { 0f, 30f, 60f, 90f };
        for (int i = 0; i < npcs.Length; i++)
            npcs[i].Add(new PositionComponent { X = farPos[i], Y = 0f, Z = 0f });

        Tick(s);
        Assert.Equal(12, left.Count);
    }
}
