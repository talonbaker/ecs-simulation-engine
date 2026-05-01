using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Chores;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems.Chores;

/// <summary>AT-02: ChoreAssignmentSystem assigns a due chore to the highest-acceptance-bias alive NPC.</summary>
public class ChoreAssignmentSystemTests
{
    private const string BiasJson = @"{
        ""schemaVersion"": ""0.1.0"",
        ""biases"": {
            ""the-newbie"":   { ""cleanMicrowave"": 0.95 },
            ""the-old-hand"": { ""cleanMicrowave"": 0.70 }
        }
    }";

    private static ChoreConfig DefaultCfg() => new()
    {
        ChoreCheckHourOfDay   = 0.0,   // runs immediately (GameHour starts at 6.0)
        MinChoreAcceptanceBias = 0.20,
    };

    [Fact]
    public void AssignsDueChore_ToHighestBiasNpc()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = DefaultCfg();
        var table = ChoreAcceptanceBiasTable.ParseJson(BiasJson);
        var bus   = new NarrativeEventBus();
        var sys   = new ChoreAssignmentSystem(cfg, clock, table, bus);

        // Low-bias NPC
        var lowBias = em.CreateEntity();
        lowBias.Add(new NpcTag());
        lowBias.Add(new NpcArchetypeComponent { ArchetypeId = "the-old-hand" });

        // High-bias NPC
        var highBias = em.CreateEntity();
        highBias.Add(new NpcTag());
        highBias.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });

        // Due chore (NextScheduledTick = 0, CurrentTick starts at 0)
        var choreEntity = em.CreateEntity();
        choreEntity.Add(new ChoreComponent
        {
            Kind              = ChoreKind.CleanMicrowave,
            CompletionLevel   = 0.0f,
            NextScheduledTick = 0L,
            CurrentAssigneeId = Guid.Empty,
        });

        clock.Tick(1f);
        sys.Update(em, 1f);

        var chore = choreEntity.Get<ChoreComponent>();
        Assert.Equal(highBias.Id, chore.CurrentAssigneeId);
    }

    [Fact]
    public void AssignsDueChore_EmitsChoreAssignedNarrative()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = DefaultCfg();
        var table = ChoreAcceptanceBiasTable.ParseJson(BiasJson);
        var bus   = new NarrativeEventBus();
        var sys   = new ChoreAssignmentSystem(cfg, clock, table, bus);

        NarrativeEventCandidate? raised = null;
        bus.OnCandidateEmitted += c => raised = c;

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });

        var choreEntity = em.CreateEntity();
        choreEntity.Add(new ChoreComponent
        {
            Kind              = ChoreKind.CleanMicrowave,
            NextScheduledTick = 0L,
            CurrentAssigneeId = Guid.Empty,
        });

        clock.Tick(1f);
        sys.Update(em, 1f);

        Assert.NotNull(raised);
        Assert.Equal(NarrativeEventKind.ChoreAssigned, raised!.Kind);
    }

    [Fact]
    public void AlreadyAssignedChore_IsNotReassigned()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = DefaultCfg();
        var table = ChoreAcceptanceBiasTable.ParseJson(BiasJson);
        var bus   = new NarrativeEventBus();
        var sys   = new ChoreAssignmentSystem(cfg, clock, table, bus);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });

        var existingAssignee = Guid.NewGuid();
        var choreEntity = em.CreateEntity();
        choreEntity.Add(new ChoreComponent
        {
            Kind              = ChoreKind.CleanMicrowave,
            NextScheduledTick = 0L,
            CurrentAssigneeId = existingAssignee,
        });

        clock.Tick(1f);
        sys.Update(em, 1f);

        Assert.Equal(existingAssignee, choreEntity.Get<ChoreComponent>().CurrentAssigneeId);
    }

    [Fact]
    public void NotDueChore_IsNotAssigned()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = DefaultCfg();
        var table = ChoreAcceptanceBiasTable.ParseJson(BiasJson);
        var bus   = new NarrativeEventBus();
        var sys   = new ChoreAssignmentSystem(cfg, clock, table, bus);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });

        var choreEntity = em.CreateEntity();
        choreEntity.Add(new ChoreComponent
        {
            Kind              = ChoreKind.CleanMicrowave,
            NextScheduledTick = 999_999_999L,  // far future
            CurrentAssigneeId = Guid.Empty,
        });

        clock.Tick(1f);
        sys.Update(em, 1f);

        Assert.Equal(Guid.Empty, choreEntity.Get<ChoreComponent>().CurrentAssigneeId);
    }

    [Fact]
    public void RunsOncePerDay_NotEveryTick()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = DefaultCfg();
        var table = ChoreAcceptanceBiasTable.ParseJson(BiasJson);
        var bus   = new NarrativeEventBus();
        var sys   = new ChoreAssignmentSystem(cfg, clock, table, bus);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });

        var choreEntity = em.CreateEntity();
        choreEntity.Add(new ChoreComponent
        {
            Kind              = ChoreKind.CleanMicrowave,
            NextScheduledTick = 0L,
            CurrentAssigneeId = Guid.Empty,
        });

        // First tick: assigns
        clock.Tick(1f);
        sys.Update(em, 1f);
        Assert.NotEqual(Guid.Empty, choreEntity.Get<ChoreComponent>().CurrentAssigneeId);

        // Clear assignment; run second tick (same day) — should NOT re-assign
        var c2 = choreEntity.Get<ChoreComponent>();
        c2.CurrentAssigneeId = Guid.Empty;
        choreEntity.Add(c2);

        sys.Update(em, 1f);
        Assert.Equal(Guid.Empty, choreEntity.Get<ChoreComponent>().CurrentAssigneeId);
    }
}
