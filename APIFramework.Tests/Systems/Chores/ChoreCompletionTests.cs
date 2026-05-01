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

/// <summary>AT-07: When CompletionLevel reaches 1.0, ChoreCompleted narrative is raised and chore resets.</summary>
public class ChoreCompletionTests
{
    private const string BiasJson = @"{
        ""schemaVersion"": ""0.1.0"",
        ""biases"": {
            ""the-newbie"": { ""cleanMicrowave"": 0.95 }
        }
    }";

    private static (EntityManager em, Entity npc, Entity choreEntity, SimulationClock clock,
                    ChoreExecutionSystem sys, NarrativeEventBus bus)
        Build()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = new ChoreConfig
        {
            ChoreCompletionRatePerSecond    = 0.01,
            MinChoreAcceptanceBias          = 0.20,
            BadQualityThreshold             = 0.40f,
            ChoreOverrotationThreshold      = 99,   // disable overrotation for these tests
            ChoreOverrotationWindowGameDays = 7,
            FrequencyTicks                  = new ChoreFrequencyConfig { CleanMicrowave = 5000L },
        };
        var table = ChoreAcceptanceBiasTable.ParseJson(BiasJson);
        var bus   = new NarrativeEventBus();
        var sys   = new ChoreExecutionSystem(cfg, clock, table, bus);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });
        npc.Add(new IntendedActionComponent(IntendedActionKind.ChoreWork, 0, DialogContextValue.None, 0));
        npc.Add(new StressComponent());
        npc.Add(new ChoreHistoryComponent
        {
            TimesPerformed       = new Dictionary<ChoreKind, int>(),
            TimesRefused         = new Dictionary<ChoreKind, int>(),
            AverageQuality       = new Dictionary<ChoreKind, float>(),
            WindowTimesPerformed = new Dictionary<ChoreKind, int>(),
            WindowStartDay       = new Dictionary<ChoreKind, int>(),
        });

        var choreEntity = em.CreateEntity();
        choreEntity.Add(new ChoreComponent
        {
            Kind              = ChoreKind.CleanMicrowave,
            CompletionLevel   = 0.0f,
            CurrentAssigneeId = npc.Id,
        });

        return (em, npc, choreEntity, clock, sys, bus);
    }

    [Fact]
    public void Completion_EmitsChoreCompletedNarrative()
    {
        var (em, _, _, _, sys, bus) = Build();

        NarrativeEventCandidate? completed = null;
        bus.OnCandidateEmitted += c =>
        {
            if (c.Kind == NarrativeEventKind.ChoreCompleted)
                completed = c;
        };

        sys.Update(em, 100_000f);

        Assert.NotNull(completed);
        Assert.Equal(NarrativeEventKind.ChoreCompleted, completed!.Kind);
    }

    [Fact]
    public void Completion_ClearsAssigneeId()
    {
        var (em, _, choreEntity, _, sys, _) = Build();

        sys.Update(em, 100_000f);

        Assert.Equal(Guid.Empty, choreEntity.Get<ChoreComponent>().CurrentAssigneeId);
    }

    [Fact]
    public void Completion_SetsNextScheduledTick()
    {
        var (em, _, choreEntity, clock, sys, _) = Build();

        clock.Tick(1f);  // CurrentTick = 1
        sys.Update(em, 100_000f);

        long nextTick = choreEntity.Get<ChoreComponent>().NextScheduledTick;
        Assert.True(nextTick > 0, "NextScheduledTick should be set after completion");
    }

    [Fact]
    public void Completion_RecordsQualityOfLastExecution()
    {
        var (em, _, choreEntity, _, sys, _) = Build();

        sys.Update(em, 100_000f);

        float quality = choreEntity.Get<ChoreComponent>().QualityOfLastExecution;
        Assert.InRange(quality, 0.0f, 1.0f);
    }

    [Fact]
    public void Completion_UpdatesChoreHistory_TimesPerformed()
    {
        var (em, npc, _, _, sys, _) = Build();

        sys.Update(em, 100_000f);

        Assert.True(npc.Has<ChoreHistoryComponent>());
        int count = npc.Get<ChoreHistoryComponent>().TimesPerformed
            .GetValueOrDefault(ChoreKind.CleanMicrowave, 0);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Completion_SetsLastDoneTick()
    {
        var (em, _, choreEntity, clock, sys, _) = Build();

        clock.Tick(1f);
        sys.Update(em, 100_000f);

        Assert.True(choreEntity.Get<ChoreComponent>().LastDoneTick > 0);
    }

    [Fact]
    public void ChoreCompleted_IsNotPersistent()
    {
        Assert.False(MemoryRecordingSystem.IsPersistent(NarrativeEventKind.ChoreCompleted));
    }
}
