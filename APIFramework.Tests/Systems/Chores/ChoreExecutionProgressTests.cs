using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Chores;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems.Chores;

/// <summary>AT-06: ChoreExecutionSystem advances CompletionLevel when NPC has ChoreWork intent.</summary>
public class ChoreExecutionProgressTests
{
    private const string BiasJson = @"{
        ""schemaVersion"": ""0.1.0"",
        ""biases"": {
            ""the-newbie"": { ""cleanMicrowave"": 0.95 }
        }
    }";

    private static (EntityManager em, Entity npc, Entity choreEntity, ChoreExecutionSystem sys)
        Build(float completionLevel = 0.0f, int acuteStress = 0)
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = new ChoreConfig
        {
            ChoreCompletionRatePerSecond = 0.01,
            MinChoreAcceptanceBias       = 0.20,
            BadQualityThreshold          = 0.40f,
            ChoreOverrotationThreshold   = 3,
            ChoreOverrotationWindowGameDays = 7,
        };
        var table = ChoreAcceptanceBiasTable.ParseJson(BiasJson);
        var bus   = new NarrativeEventBus();
        var sys   = new ChoreExecutionSystem(cfg, clock, table, bus);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });
        npc.Add(new IntendedActionComponent(IntendedActionKind.ChoreWork, 0, DialogContextValue.None, 0));
        npc.Add(new StressComponent { AcuteLevel = acuteStress });
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
            CompletionLevel   = completionLevel,
            CurrentAssigneeId = npc.Id,
        });

        return (em, npc, choreEntity, sys);
    }

    [Fact]
    public void ChoreWork_AdvancesCompletionLevel()
    {
        var (em, _, choreEntity, sys) = Build(completionLevel: 0.0f);

        sys.Update(em, 1f);

        Assert.True(choreEntity.Get<ChoreComponent>().CompletionLevel > 0.0f);
    }

    [Fact]
    public void CompletionLevelAdvance_MatchesExpectedRate()
    {
        // rate = 0.01 * deltaTime=1f * biasMult(0.95 clamped to [0.1,1.0]=0.95) * stressMult(1.0)
        // ≈ 0.0095
        var (em, _, choreEntity, sys) = Build(completionLevel: 0.0f, acuteStress: 0);

        sys.Update(em, 1f);

        float expected = (float)(0.01 * 1.0 * Math.Clamp(0.95, 0.1, 1.0) * 1.0);
        Assert.Equal(expected, choreEntity.Get<ChoreComponent>().CompletionLevel, precision: 5);
    }

    [Fact]
    public void HighStress_SlowsChoreProgress()
    {
        var (emLow, _, choreEntityLow, sysLow) = Build(completionLevel: 0.0f, acuteStress: 0);
        var (emHigh, _, choreEntityHigh, sysHigh) = Build(completionLevel: 0.0f, acuteStress: 80);

        sysLow.Update(emLow, 1f);
        sysHigh.Update(emHigh, 1f);

        Assert.True(choreEntityLow.Get<ChoreComponent>().CompletionLevel >
                    choreEntityHigh.Get<ChoreComponent>().CompletionLevel,
            "Stressed NPC should advance the chore more slowly");
    }

    [Fact]
    public void AlreadyCompletedChore_IsNotAdvanced()
    {
        var (em, _, choreEntity, sys) = Build(completionLevel: 1.0f);

        sys.Update(em, 1f);

        Assert.Equal(1.0f, choreEntity.Get<ChoreComponent>().CompletionLevel);
    }

    [Fact]
    public void IdleIntent_DoesNotAdvanceChore()
    {
        var (em, npc, choreEntity, sys) = Build(completionLevel: 0.0f);

        npc.Add(new IntendedActionComponent(IntendedActionKind.Idle, 0, DialogContextValue.None, 0));

        sys.Update(em, 1f);

        Assert.Equal(0.0f, choreEntity.Get<ChoreComponent>().CompletionLevel);
    }

    [Fact]
    public void CompletionLevelIsClamped_ToOne()
    {
        var (em, _, choreEntity, sys) = Build(completionLevel: 0.999f);

        sys.Update(em, 100_000f);  // huge delta to overshoot

        Assert.Equal(1.0f, choreEntity.Get<ChoreComponent>().CompletionLevel);
    }
}
