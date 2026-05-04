using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems;
using APIFramework.Systems.Audio;
using APIFramework.Systems.Chores;
using APIFramework.Systems.Dialog;
using APIFramework.Systems.Lighting;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Physics;
using APIFramework.Systems.Spatial;
using APIFramework.Systems.Visual;
using Xunit;

namespace APIFramework.Tests.Visual;

/// <summary>
/// AT-03 — Each of the 5 immediate producers emits the correct ParticleTriggerKind
/// on the correct event.
/// </summary>
public class ProducerEmissionTests
{
    // ── Sparks ────────────────────────────────────────────────────────────────

    [Fact]
    public void Sparks_EmittedOnBreakage_AboveThreshold()
    {
        var em       = new EntityManager();
        var bus      = new StructuralChangeBus();
        var api      = new WorldMutationApi(em, bus);
        var sound    = new SoundTriggerBus();
        var particle = new ParticleTriggerBus();
        var clock    = new SimulationClock();
        var cfg      = new PhysicsConfig { GravityPerTick = 0f, MinVelocity = 0.001f, WallHitClampMargin = 0.01f };
        var col      = new CollisionDetector(cfg, 10, 10);
        var sys      = new PhysicsTickSystem(cfg, col, api, sound, clock, particle);

        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 8f, Y = 0f, Z = 5f });
        entity.Add(new MassComponent { MassKilograms = 5f });
        entity.Add(new BreakableComponent { HitEnergyThreshold = 20f, OnBreak = BreakageBehavior.Despawn });
        entity.Add(new ThrownVelocityComponent { VelocityX = 5f, DecayPerTick = 0f });

        var received = new List<ParticleTriggerEvent>();
        particle.Subscribe(e => received.Add(e));

        sys.Update(em, 1f);

        Assert.Contains(received, e => e.Kind == ParticleTriggerKind.Sparks);
    }

    [Fact]
    public void Sparks_NotEmitted_BelowThreshold()
    {
        var em       = new EntityManager();
        var bus      = new StructuralChangeBus();
        var api      = new WorldMutationApi(em, bus);
        var sound    = new SoundTriggerBus();
        var particle = new ParticleTriggerBus();
        var clock    = new SimulationClock();
        var cfg      = new PhysicsConfig { GravityPerTick = 0f, MinVelocity = 0.001f, WallHitClampMargin = 0.01f };
        var col      = new CollisionDetector(cfg, 10, 10);
        var sys      = new PhysicsTickSystem(cfg, col, api, sound, clock, particle);

        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 8f, Y = 0f, Z = 5f });
        entity.Add(new MassComponent { MassKilograms = 0.1f });
        entity.Add(new BreakableComponent { HitEnergyThreshold = 999f, OnBreak = BreakageBehavior.Despawn });
        entity.Add(new ThrownVelocityComponent { VelocityX = 0.1f, DecayPerTick = 0f });

        var received = new List<ParticleTriggerEvent>();
        particle.Subscribe(e => received.Add(e));

        sys.Update(em, 1f);

        Assert.DoesNotContain(received, e => e.Kind == ParticleTriggerKind.Sparks);
    }

    // ── WaterSplash ───────────────────────────────────────────────────────────

    [Fact]
    public void WaterSplash_EmittedOnSpawnLiquidStain()
    {
        var em       = new EntityManager();
        var bus      = new StructuralChangeBus();
        var api      = new WorldMutationApi(em, bus);
        var sound    = new SoundTriggerBus();
        var particle = new ParticleTriggerBus();
        var clock    = new SimulationClock();
        var cfg      = new PhysicsConfig { GravityPerTick = 0f, MinVelocity = 0.001f, WallHitClampMargin = 0.01f };
        var col      = new CollisionDetector(cfg, 10, 10);
        var sys      = new PhysicsTickSystem(cfg, col, api, sound, clock, particle);

        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 8f, Y = 0f, Z = 5f });
        entity.Add(new MassComponent { MassKilograms = 0.4f });
        entity.Add(new BreakableComponent { HitEnergyThreshold = 8f, OnBreak = BreakageBehavior.SpawnLiquidStain });
        entity.Add(new ThrownVelocityComponent { VelocityX = 10f, DecayPerTick = 0f });

        var received = new List<ParticleTriggerEvent>();
        particle.Subscribe(e => received.Add(e));

        sys.Update(em, 1f);

        Assert.Contains(received, e => e.Kind == ParticleTriggerKind.WaterSplash);
    }

    // ── BulbFlicker ───────────────────────────────────────────────────────────

    [Fact]
    public void BulbFlicker_EmittedWhenFlickeringBulbReachesBuzzInterval()
    {
        var em       = new EntityManager();
        var rng      = new SeededRandom(42);
        var cfg      = new LightingConfig { FlickerOnProb = 1.0, DyingDecayProb = 0.0 };
        var soundCfg = new SoundTriggerConfig { BulbBuzzEmitIntervalTicks = 2 };
        var particle = new ParticleTriggerBus();
        var sys      = new LightSourceStateSystem(rng, cfg, soundCfg, null, particle);

        var bulb = em.CreateEntity();
        bulb.Add(new LightSourceTag());
        bulb.Add(new LightSourceComponent { State = LightState.Flickering, Intensity = 80 });
        bulb.Add(new PositionComponent { X = 2f, Z = 3f });

        var received = new List<ParticleTriggerEvent>();
        particle.Subscribe(e => received.Add(e));

        // Tick twice (interval = 2), so on tick 2 the counter hits the interval.
        sys.Update(em, 0.02f);
        sys.Update(em, 0.02f);

        Assert.Contains(received, e => e.Kind == ParticleTriggerKind.BulbFlicker);
    }

    // ── CleaningMist ──────────────────────────────────────────────────────────

    private const string BiasJson = @"{""schemaVersion"":""0.1.0"",""biases"":{""worker"":{""cleanMicrowave"":0.95}}}";

    [Fact]
    public void CleaningMist_EmittedOnChoreCompletion()
    {
        var em    = new EntityManager();
        var clock = new SimulationClock();
        var cfg   = new ChoreConfig
        {
            ChoreCompletionRatePerSecond    = 1.0,
            BadQualityThreshold             = 0.1f,
            ChoreOverrotationThreshold      = 99,
            ChoreOverrotationWindowGameDays = 7,
            FrequencyTicks                  = new ChoreFrequencyConfig { CleanMicrowave = 5000L },
        };
        var table    = ChoreAcceptanceBiasTable.ParseJson(BiasJson);
        var narrative = new NarrativeEventBus();
        var particle  = new ParticleTriggerBus();
        var sys       = new ChoreExecutionSystem(cfg, clock, table, narrative, particle);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new NpcArchetypeComponent { ArchetypeId = "worker" });
        npc.Add(new IntendedActionComponent(IntendedActionKind.ChoreWork, 0, DialogContextValue.None, 0));
        npc.Add(new StressComponent());
        npc.Add(new PositionComponent { X = 3f, Z = 3f });
        npc.Add(new ChoreHistoryComponent
        {
            TimesPerformed = new System.Collections.Generic.Dictionary<ChoreKind, int>(),
            TimesRefused = new System.Collections.Generic.Dictionary<ChoreKind, int>(),
            AverageQuality = new System.Collections.Generic.Dictionary<ChoreKind, float>(),
            WindowTimesPerformed = new System.Collections.Generic.Dictionary<ChoreKind, int>(),
            WindowStartDay = new System.Collections.Generic.Dictionary<ChoreKind, int>(),
        });

        var choreEntity = em.CreateEntity();
        choreEntity.Add(new ChoreComponent
        {
            Kind             = ChoreKind.CleanMicrowave,
            CompletionLevel  = 0.99f,
            CurrentAssigneeId = npc.Id,
        });

        var received = new List<ParticleTriggerEvent>();
        particle.Subscribe(e => received.Add(e));

        sys.Update(em, 1f);

        Assert.Contains(received, e => e.Kind == ParticleTriggerKind.CleaningMist);
    }

    // ── SpeechBubblePuff ─────────────────────────────────────────────────────

    private const string TwoFragmentCorpus = """
        {
          "schemaVersion": "0.1.0",
          "fragments": [
            {
              "id": "casual-greeting-A",
              "text": "Hey there.",
              "register": "casual",
              "context": "greeting",
              "valenceProfile": {},
              "noteworthiness": 10
            }
          ]
        }
        """;

    [Fact]
    public void SpeechBubblePuff_EmittedOnSpeechFragment()
    {
        var em       = new EntityManager();
        var queue    = new PendingDialogQueue();
        var corpus   = new DialogCorpusService(TwoFragmentCorpus);
        var proxBus  = new ProximityEventBus();
        var cfg      = new DialogConfig
        {
            ValenceMatchScore = 10, CalcifyBiasScore = 5, RecencyPenalty = -15,
            RecencyWindowSeconds = 30, TicRecognitionThreshold = 3,
            PerListenerBiasScore = 2, ValenceLowMaxValue = 33, ValenceMidMaxValue = 66,
        };
        var particle = new ParticleTriggerBus();
        var sys      = new DialogFragmentRetrievalSystem(queue, corpus, proxBus, cfg, null, particle);

        var speaker  = em.CreateEntity();
        speaker.Add(new NpcTag());
        speaker.Add(new LifeStateComponent { State = LifeState.Alive });
        speaker.Add(new PersonalityComponent { VocabularyRegister = VocabularyRegister.Casual });
        speaker.Add(new PositionComponent { X = 2f, Z = 2f });

        var listener = em.CreateEntity();
        listener.Add(new NpcTag());

        queue.Enqueue(speaker, listener, "greeting");

        var received = new List<ParticleTriggerEvent>();
        particle.Subscribe(e => received.Add(e));

        sys.Update(em, 0.02f);

        Assert.Contains(received, e => e.Kind == ParticleTriggerKind.SpeechBubblePuff);
    }
}
