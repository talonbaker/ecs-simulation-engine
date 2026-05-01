using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Chores;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems.Chores;

/// <summary>AT-03: When all NPC biases are below minChoreAcceptanceBias, the chore stays unassigned.</summary>
public class ChoreAssignmentNoEligibleTests
{
    private const string LowBiasJson = @"{
        ""schemaVersion"": ""0.1.0"",
        ""biases"": {
            ""the-founders-nephew"": { ""cleanMicrowave"": 0.05 }
        }
    }";

    [Fact]
    public void NoEligibleNpc_ChoreRemainsUnassigned()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = new ChoreConfig
        {
            ChoreCheckHourOfDay    = 0.0,
            MinChoreAcceptanceBias = 0.20,
        };
        var table = ChoreAcceptanceBiasTable.ParseJson(LowBiasJson);
        var bus   = new NarrativeEventBus();
        var sys   = new ChoreAssignmentSystem(cfg, clock, table, bus);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-founders-nephew" });

        var choreEntity = em.CreateEntity();
        choreEntity.Add(new ChoreComponent
        {
            Kind              = ChoreKind.CleanMicrowave,
            NextScheduledTick = 0L,
            CurrentAssigneeId = Guid.Empty,
        });

        clock.Tick(1f);
        sys.Update(em, 1f);

        Assert.Equal(Guid.Empty, choreEntity.Get<ChoreComponent>().CurrentAssigneeId);
    }

    [Fact]
    public void NoNpcs_ChoreRemainsUnassigned()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = new ChoreConfig { ChoreCheckHourOfDay = 0.0, MinChoreAcceptanceBias = 0.20 };
        var table = ChoreAcceptanceBiasTable.ParseJson(LowBiasJson);
        var bus   = new NarrativeEventBus();
        var sys   = new ChoreAssignmentSystem(cfg, clock, table, bus);

        var choreEntity = em.CreateEntity();
        choreEntity.Add(new ChoreComponent
        {
            Kind              = ChoreKind.CleanMicrowave,
            NextScheduledTick = 0L,
            CurrentAssigneeId = Guid.Empty,
        });

        clock.Tick(1f);
        sys.Update(em, 1f);

        Assert.Equal(Guid.Empty, choreEntity.Get<ChoreComponent>().CurrentAssigneeId);
    }

    [Fact]
    public void NoBiasEntry_FallsBackToDefault_BelowThreshold_ChoreUnassigned()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = new ChoreConfig
        {
            ChoreCheckHourOfDay    = 0.0,
            MinChoreAcceptanceBias = 0.60,  // threshold above default 0.50
        };
        var table = ChoreAcceptanceBiasTable.ParseJson("{}", defaultBias: 0.50f);
        var bus   = new NarrativeEventBus();
        var sys   = new ChoreAssignmentSystem(cfg, clock, table, bus);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "unknown-archetype" });

        var choreEntity = em.CreateEntity();
        choreEntity.Add(new ChoreComponent
        {
            Kind              = ChoreKind.CleanMicrowave,
            NextScheduledTick = 0L,
            CurrentAssigneeId = Guid.Empty,
        });

        clock.Tick(1f);
        sys.Update(em, 1f);

        Assert.Equal(Guid.Empty, choreEntity.Get<ChoreComponent>().CurrentAssigneeId);
    }
}
