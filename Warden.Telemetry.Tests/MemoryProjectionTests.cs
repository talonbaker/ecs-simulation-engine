using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Narrative;
using Warden.Contracts.Telemetry;
using Warden.Telemetry;
using Xunit;

namespace Warden.Telemetry.Tests;

/// <summary>
/// AT-09 — Projector populates relationships[].historyEventIds[] with persistent ids only;
///          ephemeral memories live engine-side but don't appear in historyEventIds.
/// AT-10 — Projector populates top-level worldState.memoryEvents[] with all engine-side
///          memories (persistent + ephemeral), deduplicated by id.
///          Engine-side memory count and DTO list count match (modulo dedup).
/// </summary>
public class MemoryProjectionTests
{
    private static readonly DateTimeOffset FixedCapture =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static WorldStateDto Capture(SimulationBootstrapper sim)
    {
        var snap = sim.Capture();
        return TelemetryProjector.Project(
            snap, sim.EntityManager, FixedCapture, 0L, 42, "test-0.0.1");
    }

    private static int IntId(Entity e)
    {
        var b = e.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    // -- AT-09: historyEventIds contains only persistent memories -------------

    [Fact]
    public void AT09_HistoryEventIds_ContainsOnlyPersistentIds()
    {
        var cfg = new SimConfig();
        var sim = new SimulationBootstrapper(new InMemoryConfigProvider(cfg), humanCount: 0);
        var em  = sim.EntityManager;
        var bus = sim.NarrativeBus;

        // Create two NPC-like entities
        var entA = em.CreateEntity();
        entA.Add(new NpcTag());
        var entB = em.CreateEntity();
        entB.Add(new NpcTag());

        int idA = IntId(entA);
        int idB = IntId(entB);

        // Seed a relationship entity
        var rel = em.CreateEntity();
        rel.Add(new RelationshipTag());
        rel.Add(new RelationshipComponent(idA, idB));

        // Ephemeral candidate (DriveSpike → Persistent=false)
        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.DriveSpike,
            ParticipantIds: new[] { idA, idB },
            RoomId:         null,
            Detail:         "ephemeral"));

        // Persistent candidate (WillpowerCollapse → Persistent=true)
        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           2,
            Kind:           NarrativeEventKind.WillpowerCollapse,
            ParticipantIds: new[] { idA, idB },
            RoomId:         null,
            Detail:         "persistent"));

        var dto = Capture(sim);

        var relDto = dto.Relationships!.First(r =>
            r.Id == rel.Id.ToString());

        // Only the persistent entry appears in historyEventIds
        Assert.Single(relDto.HistoryEventIds);
        Assert.Contains(relDto.HistoryEventIds, id => id.Contains("WillpowerCollapse"));

        // Ephemeral not in historyEventIds
        Assert.DoesNotContain(relDto.HistoryEventIds, id => id.Contains("DriveSpike"));
    }

    // -- AT-10: memoryEvents contains all memories; engine count == dto count --

    [Fact]
    public void AT10_MemoryEvents_ContainsAllMemories_CountMatchesEngineSide()
    {
        var cfg = new SimConfig();
        var sim = new SimulationBootstrapper(new InMemoryConfigProvider(cfg), humanCount: 0);
        var em  = sim.EntityManager;
        var bus = sim.NarrativeBus;

        var entA = em.CreateEntity();
        entA.Add(new NpcTag());
        var entB = em.CreateEntity();
        entB.Add(new NpcTag());

        int idA = IntId(entA);
        int idB = IntId(entB);

        // Emit one ephemeral and one persistent pair candidate
        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.DriveSpike,
            ParticipantIds: new[] { idA, idB },
            RoomId:         null,
            Detail:         "ephemeral"));

        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           2,
            Kind:           NarrativeEventKind.WillpowerCollapse,
            ParticipantIds: new[] { idA, idB },
            RoomId:         null,
            Detail:         "persistent"));

        // Emit one solo candidate
        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           3,
            Kind:           NarrativeEventKind.WillpowerLow,
            ParticipantIds: new[] { idA },
            RoomId:         null,
            Detail:         "solo"));

        // Engine-side count: 2 on relationship + 1 on entA personal
        var engineRelMem = em.Query<RelationshipMemoryComponent>()
            .SelectMany(e => e.Get<RelationshipMemoryComponent>().Recent)
            .ToList();
        var enginePerMem = em.Query<PersonalMemoryComponent>()
            .SelectMany(e => e.Get<PersonalMemoryComponent>().Recent)
            .ToList();
        int engineTotal = engineRelMem.Count + enginePerMem.Count;

        var dto = Capture(sim);

        Assert.NotNull(dto.MemoryEvents);
        Assert.Equal(engineTotal, dto.MemoryEvents!.Count);
    }

    [Fact]
    public void AT10_MemoryEvents_DeduplicatesById()
    {
        var cfg = new SimConfig();
        var sim = new SimulationBootstrapper(new InMemoryConfigProvider(cfg), humanCount: 0);
        var em  = sim.EntityManager;
        var bus = sim.NarrativeBus;

        var entA = em.CreateEntity();
        entA.Add(new NpcTag());
        var entB = em.CreateEntity();
        entB.Add(new NpcTag());
        int idA = IntId(entA);
        int idB = IntId(entB);

        // Single pair candidate — stored once on relationship entity
        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick:           1,
            Kind:           NarrativeEventKind.ConversationStarted,
            ParticipantIds: new[] { idA, idB },
            RoomId:         null,
            Detail:         "conversation"));

        var dto = Capture(sim);
        Assert.NotNull(dto.MemoryEvents);

        var ids = dto.MemoryEvents!.Select(m => m.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // -- No memories → MemoryEvents is null -----------------------------------

    [Fact]
    public void NoMemories_MemoryEventsIsNull()
    {
        var sim = new SimulationBootstrapper(new InMemoryConfigProvider(new SimConfig()), humanCount: 0);
        var dto = Capture(sim);
        Assert.Null(dto.MemoryEvents);
    }
}
