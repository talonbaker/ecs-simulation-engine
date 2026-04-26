using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-07 — Ring-buffer overflow: 50 candidates against capacity-32 buffer leaves
///         the most recent 32; oldest 18 dropped.  Equally for capacity-16 personal buffer.
/// </summary>
public class MemoryRecordingSystemBufferTests
{
    private static int IntId(Entity e)
    {
        var b = e.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    // ── Relationship buffer: 50 into capacity-32 ──────────────────────────────

    [Fact]
    public void RelationshipBuffer_50Candidates_Capacity32_Retains32MostRecent()
    {
        const int Capacity = 32;
        const int Total    = 50;

        var cfg = new MemoryConfig { MaxRelationshipMemoryCount = Capacity, MaxPersonalMemoryCount = 16 };
        var em  = new EntityManager();
        var bus = new NarrativeEventBus();
        _       = new MemoryRecordingSystem(bus, em, cfg);

        var npcA = em.CreateEntity();
        npcA.Add(new NpcTag());
        var npcB = em.CreateEntity();
        npcB.Add(new NpcTag());

        int idA = IntId(npcA);
        int idB = IntId(npcB);

        // Seed relationship entity
        var rel = em.CreateEntity();
        rel.Add(new RelationshipTag());
        rel.Add(new RelationshipComponent(idA, idB));

        for (int tick = 1; tick <= Total; tick++)
        {
            bus.RaiseCandidate(new NarrativeEventCandidate(
                Tick:           tick,
                Kind:           NarrativeEventKind.DriveSpike,
                ParticipantIds: new[] { idA, idB },
                RoomId:         null,
                Detail:         $"tick {tick}"));
        }

        var recent = rel.Get<RelationshipMemoryComponent>().Recent;
        Assert.Equal(Capacity, recent.Count);
        Assert.Equal(Total - Capacity + 1, recent[0].Tick);   // oldest retained
        Assert.Equal(Total, recent[^1].Tick);                  // newest retained
    }

    [Fact]
    public void RelationshipBuffer_50Candidates_Capacity32_Drops18Oldest()
    {
        const int Capacity = 32;
        const int Total    = 50;
        const int Dropped  = Total - Capacity;   // 18

        var cfg = new MemoryConfig { MaxRelationshipMemoryCount = Capacity, MaxPersonalMemoryCount = 16 };
        var em  = new EntityManager();
        var bus = new NarrativeEventBus();
        _       = new MemoryRecordingSystem(bus, em, cfg);

        var npcA = em.CreateEntity();
        npcA.Add(new NpcTag());
        var npcB = em.CreateEntity();
        npcB.Add(new NpcTag());
        int idA = IntId(npcA);
        int idB = IntId(npcB);

        var rel = em.CreateEntity();
        rel.Add(new RelationshipTag());
        rel.Add(new RelationshipComponent(idA, idB));

        for (int tick = 1; tick <= Total; tick++)
        {
            bus.RaiseCandidate(new NarrativeEventCandidate(
                Tick:           tick,
                Kind:           NarrativeEventKind.DriveSpike,
                ParticipantIds: new[] { idA, idB },
                RoomId:         null,
                Detail:         $"tick {tick}"));
        }

        var recent = rel.Get<RelationshipMemoryComponent>().Recent;
        // Ticks 1..Dropped must be absent
        for (int t = 1; t <= Dropped; t++)
        {
            Assert.DoesNotContain(recent, e => e.Tick == t);
        }
    }

    // ── Personal buffer: capacity-16 ─────────────────────────────────────────

    [Fact]
    public void PersonalBuffer_30Candidates_Capacity16_Retains16MostRecent()
    {
        const int Capacity = 16;
        const int Total    = 30;

        var cfg = new MemoryConfig { MaxRelationshipMemoryCount = 32, MaxPersonalMemoryCount = Capacity };
        var em  = new EntityManager();
        var bus = new NarrativeEventBus();
        _       = new MemoryRecordingSystem(bus, em, cfg);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        int id = IntId(npc);

        for (int tick = 1; tick <= Total; tick++)
        {
            bus.RaiseCandidate(new NarrativeEventCandidate(
                Tick:           tick,
                Kind:           NarrativeEventKind.DriveSpike,
                ParticipantIds: new[] { id },
                RoomId:         null,
                Detail:         $"tick {tick}"));
        }

        var recent = npc.Get<PersonalMemoryComponent>().Recent;
        Assert.Equal(Capacity, recent.Count);
        Assert.Equal(Total - Capacity + 1, recent[0].Tick);
        Assert.Equal(Total, recent[^1].Tick);
    }
}
