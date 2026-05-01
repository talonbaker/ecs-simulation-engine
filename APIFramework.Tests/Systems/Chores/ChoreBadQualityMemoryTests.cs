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

/// <summary>AT-09: Low quality execution emits a persistent ChoreBadlyDone narrative.</summary>
public class ChoreBadQualityMemoryTests
{
    // Use a highly-stressed NPC to depress quality below the threshold.
    // quality = acceptanceBiasMult * stressMult
    // stressMult = clamp(1.0 - 100*0.005, 0.1, 1.0) = clamp(0.5, 0.1, 1.0) = 0.5
    // acceptanceBiasMult = clamp(0.40, 0.1, 1.0) = 0.40
    // quality = 0.40 * 0.5 = 0.20 < BadQualityThreshold (0.40)

    private const string BiasJson = @"{
        ""schemaVersion"": ""0.1.0"",
        ""biases"": {
            ""the-climber"": { ""cleanMicrowave"": 0.40 }
        }
    }";

    [Fact]
    public void LowQualityCompletion_EmitsChoreBadlyDoneNarrative()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = new ChoreConfig
        {
            ChoreCompletionRatePerSecond    = 0.01,
            MinChoreAcceptanceBias          = 0.20,
            BadQualityThreshold             = 0.40f,
            ChoreOverrotationThreshold      = 99,
            ChoreOverrotationWindowGameDays = 7,
        };
        var table = ChoreAcceptanceBiasTable.ParseJson(BiasJson);
        var bus   = new NarrativeEventBus();
        var sys   = new ChoreExecutionSystem(cfg, clock, table, bus);

        NarrativeEventCandidate? badlyDone = null;
        bus.OnCandidateEmitted += c =>
        {
            if (c.Kind == NarrativeEventKind.ChoreBadlyDone)
                badlyDone = c;
        };

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-climber" });
        npc.Add(new IntendedActionComponent(IntendedActionKind.ChoreWork, 0, DialogContextValue.None, 0));
        npc.Add(new StressComponent { AcuteLevel = 100 });  // max stress to depress quality
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

        sys.Update(em, 100_000f);

        Assert.NotNull(badlyDone);
        Assert.Equal(NarrativeEventKind.ChoreBadlyDone, badlyDone!.Kind);
    }

    [Fact]
    public void HighQualityCompletion_DoesNotEmitChoreBadlyDone()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = new ChoreConfig
        {
            ChoreCompletionRatePerSecond    = 0.01,
            MinChoreAcceptanceBias          = 0.20,
            BadQualityThreshold             = 0.40f,
            ChoreOverrotationThreshold      = 99,
            ChoreOverrotationWindowGameDays = 7,
        };
        // Use the-newbie with bias 0.95 and no stress: quality = 0.95 * 1.0 = 0.95 > 0.40
        const string goodBiasJson = @"{
            ""schemaVersion"": ""0.1.0"",
            ""biases"": {
                ""the-newbie"": { ""cleanMicrowave"": 0.95 }
            }
        }";
        var table = ChoreAcceptanceBiasTable.ParseJson(goodBiasJson);
        var bus   = new NarrativeEventBus();
        var sys   = new ChoreExecutionSystem(cfg, clock, table, bus);

        bool badlyDoneEmitted = false;
        bus.OnCandidateEmitted += c =>
        {
            if (c.Kind == NarrativeEventKind.ChoreBadlyDone)
                badlyDoneEmitted = true;
        };

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-newbie" });
        npc.Add(new IntendedActionComponent(IntendedActionKind.ChoreWork, 0, DialogContextValue.None, 0));
        npc.Add(new StressComponent { AcuteLevel = 0 });
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

        sys.Update(em, 100_000f);

        Assert.False(badlyDoneEmitted);
    }

    [Fact]
    public void ChoreBadlyDone_IsPersistent()
    {
        Assert.True(MemoryRecordingSystem.IsPersistent(NarrativeEventKind.ChoreBadlyDone));
    }

    [Fact]
    public void ChoreBadlyDone_QualityRecordedOnChore()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = new ChoreConfig
        {
            ChoreCompletionRatePerSecond    = 0.01,
            MinChoreAcceptanceBias          = 0.20,
            BadQualityThreshold             = 0.40f,
            ChoreOverrotationThreshold      = 99,
            ChoreOverrotationWindowGameDays = 7,
        };
        var table = ChoreAcceptanceBiasTable.ParseJson(BiasJson);
        var bus   = new NarrativeEventBus();
        var sys   = new ChoreExecutionSystem(cfg, clock, table, bus);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-climber" });
        npc.Add(new IntendedActionComponent(IntendedActionKind.ChoreWork, 0, DialogContextValue.None, 0));
        npc.Add(new StressComponent { AcuteLevel = 100 });
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

        sys.Update(em, 100_000f);

        float quality = choreEntity.Get<ChoreComponent>().QualityOfLastExecution;
        Assert.True(quality < cfg.BadQualityThreshold,
            $"Quality {quality} should be below bad-quality threshold {cfg.BadQualityThreshold}");
    }
}
