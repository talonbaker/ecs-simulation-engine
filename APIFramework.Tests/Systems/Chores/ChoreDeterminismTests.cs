using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Chores;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems.Chores;

/// <summary>AT-11: Chore assignment and execution produce byte-identical state across two identical runs.</summary>
public class ChoreDeterminismTests
{
    private const string BiasJson = @"{
        ""schemaVersion"": ""0.1.0"",
        ""biases"": {
            ""the-newbie"":   { ""cleanMicrowave"": 0.95, ""cleanFridge"": 0.90, ""takeOutTrash"": 0.85 },
            ""the-old-hand"": { ""cleanMicrowave"": 0.70, ""cleanFridge"": 0.65, ""takeOutTrash"": 0.50 }
        }
    }";

    private record ChoreState(
        float CompletionLevel,
        long  LastDoneTick,
        long  NextScheduledTick,
        Guid  CurrentAssigneeId,
        float QualityOfLastExecution);

    private record NpcHistoryState(int TimesPerformed, int WindowTimesPerformed);

    private static (ChoreState chore, NpcHistoryState npc1, NpcHistoryState npc2)
        RunSimulation(string biasJson)
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = new ChoreConfig
        {
            ChoreCheckHourOfDay             = 0.0,    // run immediately
            ChoreCompletionRatePerSecond    = 0.005,
            MinChoreAcceptanceBias          = 0.20,
            BadQualityThreshold             = 0.40f,
            ChoreOverrotationThreshold      = 3,
            ChoreOverrotationWindowGameDays = 7,
            ChoreOverrotationStressGain     = 1.5,
            FrequencyTicks                  = new ChoreFrequencyConfig
            {
                CleanMicrowave = 500L,   // short cycle for more completions in 5000 ticks
            },
        };
        var table    = ChoreAcceptanceBiasTable.ParseJson(biasJson);
        var bus      = new NarrativeEventBus();
        var assignSys = new ChoreAssignmentSystem(cfg, clock, table, bus);
        var execSys   = new ChoreExecutionSystem(cfg, clock, table, bus);

        var npc1 = em.CreateEntity();
        npc1.Add(new NpcTag());
        npc1.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });
        npc1.Add(new StressComponent());
        npc1.Add(new ChoreHistoryComponent
        {
            TimesPerformed       = new Dictionary<ChoreKind, int>(),
            TimesRefused         = new Dictionary<ChoreKind, int>(),
            AverageQuality       = new Dictionary<ChoreKind, float>(),
            WindowTimesPerformed = new Dictionary<ChoreKind, int>(),
            WindowStartDay       = new Dictionary<ChoreKind, int>(),
        });

        var npc2 = em.CreateEntity();
        npc2.Add(new NpcTag());
        npc2.Add(new NpcArchetypeComponent { ArchetypeId = "the-old-hand" });
        npc2.Add(new StressComponent());
        npc2.Add(new ChoreHistoryComponent
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
            NextScheduledTick = 0L,
            CurrentAssigneeId = Guid.Empty,
        });

        for (int i = 0; i < 5000; i++)
        {
            clock.Tick(1f);

            // Re-attach ChoreWork intent to whichever NPC is assigned
            var c = choreEntity.Get<ChoreComponent>();
            if (c.CurrentAssigneeId == npc1.Id)
                npc1.Add(new IntendedActionComponent(IntendedActionKind.ChoreWork, 0, DialogContextValue.None, 0));
            else if (c.CurrentAssigneeId == npc2.Id)
                npc2.Add(new IntendedActionComponent(IntendedActionKind.ChoreWork, 0, DialogContextValue.None, 0));

            assignSys.Update(em, 1f);
            execSys.Update(em, 1f);

            // After completion, clear intent (chore now unassigned)
            if (choreEntity.Get<ChoreComponent>().CurrentAssigneeId == Guid.Empty)
            {
                npc1.Add(new IntendedActionComponent(IntendedActionKind.Idle, 0, DialogContextValue.None, 0));
                npc2.Add(new IntendedActionComponent(IntendedActionKind.Idle, 0, DialogContextValue.None, 0));
            }
        }

        var finalChore = choreEntity.Get<ChoreComponent>();
        var h1 = npc1.Get<ChoreHistoryComponent>();
        var h2 = npc2.Get<ChoreHistoryComponent>();

        return (
            new ChoreState(
                finalChore.CompletionLevel,
                finalChore.LastDoneTick,
                finalChore.NextScheduledTick,
                finalChore.CurrentAssigneeId,
                finalChore.QualityOfLastExecution),
            new NpcHistoryState(
                h1.TimesPerformed?.GetValueOrDefault(ChoreKind.CleanMicrowave, 0) ?? 0,
                h1.WindowTimesPerformed?.GetValueOrDefault(ChoreKind.CleanMicrowave, 0) ?? 0),
            new NpcHistoryState(
                h2.TimesPerformed?.GetValueOrDefault(ChoreKind.CleanMicrowave, 0) ?? 0,
                h2.WindowTimesPerformed?.GetValueOrDefault(ChoreKind.CleanMicrowave, 0) ?? 0)
        );
    }

    [Fact]
    public void SameSetup_ProducesIdenticalChoreState_After5000Ticks()
    {
        var (state1, h1a, h1b) = RunSimulation(BiasJson);
        var (state2, h2a, h2b) = RunSimulation(BiasJson);

        Assert.Equal(state1.CompletionLevel,        state2.CompletionLevel);
        Assert.Equal(state1.LastDoneTick,           state2.LastDoneTick);
        Assert.Equal(state1.NextScheduledTick,      state2.NextScheduledTick);
        Assert.Equal(state1.CurrentAssigneeId,      state2.CurrentAssigneeId);
        Assert.Equal(state1.QualityOfLastExecution, state2.QualityOfLastExecution);

        Assert.Equal(h1a.TimesPerformed,       h2a.TimesPerformed);
        Assert.Equal(h1a.WindowTimesPerformed, h2a.WindowTimesPerformed);
        Assert.Equal(h1b.TimesPerformed,       h2b.TimesPerformed);
        Assert.Equal(h1b.WindowTimesPerformed, h2b.WindowTimesPerformed);
    }

    [Fact]
    public void HighBiasNpc_PerformsChoreMoreOften_ThanLowBiasNpc()
    {
        var (_, h1, h2) = RunSimulation(BiasJson);

        // the-newbie (bias 0.95) should be assigned more often than the-old-hand (bias 0.70)
        Assert.True(h1.TimesPerformed >= h2.TimesPerformed,
            $"the-newbie ({h1.TimesPerformed}) should perform microwave chore at least as often as " +
            $"the-old-hand ({h2.TimesPerformed})");
    }
}
