using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-02 — System subscribed to the bus receives candidates emitted during a sim pass.
/// AT-03 — Two-participant candidate appended to relationship entity; canonical pair order.
/// AT-04 — Two-participant candidate where no relationship exists auto-creates one (Intensity=50).
/// AT-05 — Solo candidate appended to participant's PersonalMemoryComponent.
/// AT-06 — 3+-participant candidate fans out to all participants' personal logs.
/// </summary>
public class MemoryRecordingSystemTests
{
    private static (EntityManager em, NarrativeEventBus bus, MemoryRecordingSystem sys)
        Build(MemoryConfig? cfg = null)
    {
        cfg ??= new MemoryConfig();
        var em  = new EntityManager();
        var bus = new NarrativeEventBus();
        var sys = new MemoryRecordingSystem(bus, em, cfg);
        return (em, bus, sys);
    }

    private static Entity MakeNpc(EntityManager em)
    {
        var e = em.CreateEntity();
        e.Add(new NpcTag());
        e.Add(new SocialDrivesComponent());
        e.Add(new WillpowerComponent(50, 50));
        return e;
    }

    private static int IntId(Entity e)
    {
        var b = e.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    // -- AT-02: subscription is active ----------------------------------------

    [Fact]
    public void AT02_SystemReceivesCandidatesViaBus()
    {
        var (em, bus, _) = Build();
        var npc = MakeNpc(em);
        int id  = IntId(npc);

        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.WillpowerLow,
            ParticipantIds: new[] { id },
            RoomId:         null,
            Detail:         "willpower low"));

        // solo candidate → personal memory
        Assert.True(npc.Has<PersonalMemoryComponent>());
    }

    // -- AT-03: pair → relationship entity, canonical order -------------------

    [Fact]
    public void AT03_PairCandidate_AppendsToRelationshipEntity_CanonicalOrder()
    {
        var (em, bus, _) = Build();
        var npcA = MakeNpc(em);
        var npcB = MakeNpc(em);
        int idA  = IntId(npcA);
        int idB  = IntId(npcB);

        // Seed an existing relationship (canonical order)
        var rel = em.CreateEntity();
        rel.Add(new RelationshipTag());
        rel.Add(new RelationshipComponent(idA, idB));

        // Emit candidate with participants in reverse order
        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.WillpowerCollapse,
            ParticipantIds: new[] { idB, idA },   // intentionally reversed
            RoomId:         null,
            Detail:         "test"));

        Assert.True(rel.Has<RelationshipMemoryComponent>());
        var recent = rel.Get<RelationshipMemoryComponent>().Recent;
        Assert.Single(recent);

        // participants in the stored entry should be canonical (lower first)
        Assert.Equal(Math.Min(idA, idB), recent[0].ParticipantIds[0]);
        Assert.Equal(Math.Max(idA, idB), recent[0].ParticipantIds[1]);
    }

    // -- AT-04: auto-create relationship --------------------------------------

    [Fact]
    public void AT04_PairCandidate_NoExistingRelationship_AutoCreates_Intensity50()
    {
        var (em, bus, _) = Build();
        var npcA = MakeNpc(em);
        var npcB = MakeNpc(em);
        int idA  = IntId(npcA);
        int idB  = IntId(npcB);

        // Confirm no relationship entity exists before raising the candidate
        var relsBefore = em.Query<RelationshipTag>().ToList();
        Assert.Empty(relsBefore);

        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           5,
            Kind:           NarrativeEventKind.ConversationStarted,
            ParticipantIds: new[] { idA, idB },
            RoomId:         null,
            Detail:         "chat"));

        var relsAfter = em.Query<RelationshipTag>()
            .Where(e => e.Has<RelationshipComponent>())
            .ToList();
        Assert.Single(relsAfter);

        var rc = relsAfter[0].Get<RelationshipComponent>();
        Assert.Equal(Math.Min(idA, idB), rc.ParticipantA);
        Assert.Equal(Math.Max(idA, idB), rc.ParticipantB);
        Assert.Equal(50, rc.Intensity);
        Assert.Empty(rc.Patterns);

        Assert.True(relsAfter[0].Has<RelationshipMemoryComponent>());
    }

    // -- AT-05: solo → personal ------------------------------------------------

    [Fact]
    public void AT05_SoloCandidate_AppendsToPersonalMemory()
    {
        var (em, bus, _) = Build();
        var npc = MakeNpc(em);
        int id  = IntId(npc);

        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           3,
            Kind:           NarrativeEventKind.DriveSpike,
            ParticipantIds: new[] { id },
            RoomId:         "breakroom",
            Detail:         "irritation spike"));

        Assert.True(npc.Has<PersonalMemoryComponent>());
        var recent = npc.Get<PersonalMemoryComponent>().Recent;
        Assert.Single(recent);
        Assert.Equal(3L, recent[0].Tick);
        Assert.Equal("breakroom", recent[0].RoomId);
    }

    // -- AT-06: 3+ → fan-out to all personal logs -----------------------------

    [Fact]
    public void AT06_ThreePlusParticipants_FanOutToAllPersonalLogs()
    {
        var (em, bus, _) = Build();
        var npc1 = MakeNpc(em);
        var npc2 = MakeNpc(em);
        var npc3 = MakeNpc(em);

        int id1 = IntId(npc1);
        int id2 = IntId(npc2);
        int id3 = IntId(npc3);

        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           10,
            Kind:           NarrativeEventKind.DriveSpike,
            ParticipantIds: new[] { id1, id2, id3 },
            RoomId:         null,
            Detail:         "group event"));

        Assert.True(npc1.Has<PersonalMemoryComponent>());
        Assert.True(npc2.Has<PersonalMemoryComponent>());
        Assert.True(npc3.Has<PersonalMemoryComponent>());

        Assert.Single(npc1.Get<PersonalMemoryComponent>().Recent);
        Assert.Single(npc2.Get<PersonalMemoryComponent>().Recent);
        Assert.Single(npc3.Get<PersonalMemoryComponent>().Recent);

        // No relationship entity created for 3+-participant candidates
        Assert.Empty(em.Query<RelationshipTag>());
    }

    // -- Zero-participant: defensive guard -------------------------------------

    [Fact]
    public void ZeroParticipants_NoEntityModified()
    {
        var (em, bus, _) = Build();

        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.DriveSpike,
            ParticipantIds: Array.Empty<int>(),
            RoomId:         null,
            Detail:         "empty"));

        Assert.Empty(em.Query<RelationshipTag>());
        Assert.Empty(em.Query<PersonalMemoryComponent>());
    }
}
