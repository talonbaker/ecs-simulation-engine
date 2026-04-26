using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>AT-03..AT-06: StressSystem sources, decay, chronic update, and tag transitions.</summary>
public class StressSystemTests
{
    private static StressConfig DefaultCfg() => new();

    private static (EntityManager em, Entity npc, WillpowerEventQueue queue, NarrativeEventBus bus, StressSystem sys)
        Build(int acuteLevel = 0, double chronicLevel = 0.0, int lastDayUpdated = 0,
              int neuroticism = 0, SimulationClock? clock = null)
    {
        var cfg   = DefaultCfg();
        clock   ??= new SimulationClock();
        var queue = new WillpowerEventQueue();
        var bus   = new NarrativeEventBus();
        var sys   = new StressSystem(cfg, clock, queue, bus);

        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new StressComponent
        {
            AcuteLevel    = acuteLevel,
            ChronicLevel  = chronicLevel,
            LastDayUpdated = lastDayUpdated,
        });
        npc.Add(new PersonalityComponent(0, 0, 0, 0, neuroticism));

        return (em, npc, queue, bus, sys);
    }

    // ── AT-03: Suppression events increase AcuteLevel ────────────────────────

    [Fact]
    public void SuppressionTick_IncreasesAcuteLevel()
    {
        var (em, npc, queue, _, sys) = Build(acuteLevel: 0, neuroticism: 0);

        int entityId = WillpowerSystem.EntityIntId(npc);
        queue.Enqueue(new WillpowerEventSignal(entityId, WillpowerEventKind.SuppressionTick, 1));
        queue.DrainAll(); // populate LastDrainedBatch

        sys.Update(em, 1f);

        // gain = 1 * 1.5 * 1.0 = 1.5 → (int)1.5 = 1
        Assert.Equal(1, npc.Get<StressComponent>().AcuteLevel);
    }

    [Fact]
    public void SuppressionTick_MultipleEvents_AccumulateCorrectly()
    {
        var (em, npc, queue, _, sys) = Build(acuteLevel: 0, neuroticism: 0);

        int entityId = WillpowerSystem.EntityIntId(npc);
        queue.Enqueue(new WillpowerEventSignal(entityId, WillpowerEventKind.SuppressionTick, 1));
        queue.Enqueue(new WillpowerEventSignal(entityId, WillpowerEventKind.SuppressionTick, 1));
        queue.DrainAll();

        sys.Update(em, 1f);

        // gain = 2 * 1.5 * 1.0 = 3.0 → (int)3.0 = 3
        Assert.Equal(3, npc.Get<StressComponent>().AcuteLevel);
    }

    [Fact]
    public void SuppressionTick_ClampsAt100()
    {
        var (em, npc, queue, _, sys) = Build(acuteLevel: 99, neuroticism: 0);

        int entityId = WillpowerSystem.EntityIntId(npc);
        for (int i = 0; i < 10; i++)
            queue.Enqueue(new WillpowerEventSignal(entityId, WillpowerEventKind.SuppressionTick, 1));
        queue.DrainAll();

        sys.Update(em, 1f);

        Assert.Equal(100, npc.Get<StressComponent>().AcuteLevel);
    }

    [Fact]
    public void SuppressionTick_OtherEntityId_NotCounted()
    {
        var (em, npc, queue, _, sys) = Build(acuteLevel: 0);

        queue.Enqueue(new WillpowerEventSignal(99999, WillpowerEventKind.SuppressionTick, 1));
        queue.DrainAll();

        sys.Update(em, 1f);

        Assert.Equal(0, npc.Get<StressComponent>().AcuteLevel);
    }

    // ── AT-04: Per-tick acute decay ───────────────────────────────────────────

    [Fact]
    public void AcuteDecay_After20Ticks_DropsBy1()
    {
        // acuteDecayPerTick = 0.05; 20 * 0.05 = 1.0 → decayInt = 1 after 20 ticks
        var (em, npc, _, _, sys) = Build(acuteLevel: 10, lastDayUpdated: 1);

        for (int i = 0; i < 20; i++)
            sys.Update(em, 1f);

        Assert.Equal(9, npc.Get<StressComponent>().AcuteLevel);
    }

    [Fact]
    public void AcuteDecay_ClampsAt0()
    {
        var (em, npc, _, _, sys) = Build(acuteLevel: 0, lastDayUpdated: 1);

        for (int i = 0; i < 100; i++)
            sys.Update(em, 1f);

        Assert.Equal(0, npc.Get<StressComponent>().AcuteLevel);
    }

    // ── AT-05: Per-day chronic update ─────────────────────────────────────────

    [Fact]
    public void ChronicUpdate_OnDayAdvance_AppliesRollingMean()
    {
        // Set LastDayUpdated = 1, then advance clock to day 2
        var clock = new SimulationClock();
        // Bootstrap: run once to set LastDayUpdated = 1
        var (em, npc, _, _, sys) = Build(acuteLevel: 70, chronicLevel: 40.0,
            lastDayUpdated: 0, clock: clock);

        // First tick bootstraps LastDayUpdated to DayNumber=1
        sys.Update(em, 1f);
        Assert.Equal(1, npc.Get<StressComponent>().LastDayUpdated);

        // Advance clock to day 2 (one full game-day = 86400 game-seconds; TimeScale=120 → 720 real-s)
        clock.Tick(720f);
        Assert.Equal(2, clock.DayNumber);

        sys.Update(em, 1f);

        var stress = npc.Get<StressComponent>();
        // ChronicLevel = (40.0 * 6 + AcuteLevel) / 7
        // AcuteLevel was 70; after 1 tick of decay (decayInt = 0) it's still 70 at bootstrap.
        // After clock advance and second Update, AcuteLevel read is whatever survived the two ticks.
        // We care about the formula: (prevChronic * 6 + currentAcute) / 7
        // Just verify the formula was applied (ChronicLevel changed from 40.0)
        Assert.NotEqual(40.0, stress.ChronicLevel);
    }

    [Fact]
    public void ChronicUpdate_Formula_MatchesDesign()
    {
        var clock = new SimulationClock();
        var cfg   = DefaultCfg();
        // No decay accumulation across ticks we don't care about — use large AcuteLevel
        var queue = new WillpowerEventQueue();
        var bus   = new NarrativeEventBus();
        var sys   = new StressSystem(cfg, clock, queue, bus);

        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        // Pre-set LastDayUpdated = 1 so the first Update skips the bootstrap path
        npc.Add(new StressComponent
        {
            AcuteLevel    = 70,
            ChronicLevel  = 40.0,
            LastDayUpdated = 1,
        });

        // Advance to day 2
        clock.Tick(720f);
        Assert.Equal(2, clock.DayNumber);

        sys.Update(em, 1f);

        var stress = npc.Get<StressComponent>();
        // AcuteLevel after decay: 1 tick, decayRemainder = 0.05 → decayInt = 0, AcuteLevel still 70
        double expected = Math.Clamp((40.0 * 6 + 70) / 7.0, 0.0, 100.0);
        Assert.Equal(expected, stress.ChronicLevel, precision: 10);
    }

    [Fact]
    public void ChronicUpdate_ResetsEventCounters()
    {
        var clock = new SimulationClock();
        var (em, npc, _, _, sys) = Build(acuteLevel: 0, chronicLevel: 0, lastDayUpdated: 1,
            clock: clock);

        var sc = npc.Get<StressComponent>();
        sc.SuppressionEventsToday    = 5;
        sc.DriveSpikeEventsToday     = 3;
        sc.SocialConflictEventsToday = 2;
        npc.Add(sc);

        clock.Tick(720f);   // advance to day 2
        sys.Update(em, 1f);

        var after = npc.Get<StressComponent>();
        Assert.Equal(0, after.SuppressionEventsToday);
        Assert.Equal(0, after.DriveSpikeEventsToday);
        Assert.Equal(0, after.SocialConflictEventsToday);
    }

    [Fact]
    public void ChronicUpdate_DoesNotFire_SameDay()
    {
        var (em, npc, _, _, sys) = Build(acuteLevel: 0, chronicLevel: 50.0, lastDayUpdated: 1);

        // Run multiple ticks on day 1 — ChronicLevel should not change
        for (int i = 0; i < 5; i++)
            sys.Update(em, 1f);

        Assert.Equal(50.0, npc.Get<StressComponent>().ChronicLevel);
    }

    // ── AT-06: Tag transitions ────────────────────────────────────────────────

    [Fact]
    public void StressedTag_AppliedAt_Threshold()
    {
        // AcuteLevel = 60 (= stressedTagThreshold), decay = 0 for first tick
        var (em, npc, _, _, sys) = Build(acuteLevel: 60, lastDayUpdated: 1);

        sys.Update(em, 1f);

        Assert.True(npc.Has<StressedTag>());
    }

    [Fact]
    public void StressedTag_NotApplied_BelowThreshold()
    {
        var (em, npc, _, _, sys) = Build(acuteLevel: 59, lastDayUpdated: 1);

        sys.Update(em, 1f);

        Assert.False(npc.Has<StressedTag>());
    }

    [Fact]
    public void OverwhelmedTag_AppliedAt_Threshold()
    {
        var (em, npc, _, _, sys) = Build(acuteLevel: 85, lastDayUpdated: 1);

        sys.Update(em, 1f);

        Assert.True(npc.Has<OverwhelmedTag>());
        Assert.True(npc.Has<StressedTag>());
    }

    [Fact]
    public void OverwhelmedTag_NotApplied_BelowThreshold()
    {
        var (em, npc, _, _, sys) = Build(acuteLevel: 84, lastDayUpdated: 1);

        sys.Update(em, 1f);

        Assert.True(npc.Has<StressedTag>());
        Assert.False(npc.Has<OverwhelmedTag>());
    }

    [Fact]
    public void Tags_Removed_WhenLevelDropsBelowThreshold()
    {
        // Start above threshold, then let decay bring it below
        // Need many ticks: each tick drops by 0 or 1 (accumulator-based)
        // Use a large AcuteDecayPerTick via custom config
        var cfg   = new StressConfig { AcuteDecayPerTick = 1.0, StressedTagThreshold = 60 };
        var clock = new SimulationClock();
        var queue = new WillpowerEventQueue();
        var bus   = new NarrativeEventBus();
        var sys   = new StressSystem(cfg, clock, queue, bus);

        var em  = new EntityManager();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        npc.Add(new StressComponent { AcuteLevel = 62, LastDayUpdated = 1 });

        // Run 3 ticks: decay = 1/tick, 62 → 61 → 60 → 59 (below threshold on tick 3)
        sys.Update(em, 1f);
        Assert.True(npc.Has<StressedTag>());  // 61
        sys.Update(em, 1f);
        Assert.True(npc.Has<StressedTag>());  // 60
        sys.Update(em, 1f);
        Assert.False(npc.Has<StressedTag>()); // 59
    }

    // ── Drive spike source ────────────────────────────────────────────────────

    [Fact]
    public void DriveSpike_IncreasesAcuteLevel()
    {
        var (em, npc, _, _, sys) = Build(acuteLevel: 0, lastDayUpdated: 1);

        // Add a drive spiked above baseline by more than driveSpikeStressDelta (default 25)
        npc.Add(new SocialDrivesComponent
        {
            Loneliness = new DriveValue { Current = 80, Baseline = 50 }  // delta = 30 > 25
        });

        sys.Update(em, 1f);

        // gain = 1 spike * driveSpikeStressGain(2.0) * neuroFactor(1.0) = 2.0 → AcuteLevel = 2
        Assert.Equal(2, npc.Get<StressComponent>().AcuteLevel);
        Assert.Equal(1, npc.Get<StressComponent>().DriveSpikeEventsToday);
    }

    // ── Social conflict source ────────────────────────────────────────────────

    [Fact]
    public void SocialConflict_IncreasesAcuteLevel()
    {
        var (em, npc, _, bus, sys) = Build(acuteLevel: 0, lastDayUpdated: 1);

        int entityId = WillpowerSystem.EntityIntId(npc);
        bus.RaiseCandidate(new NarrativeEventCandidate(
            Tick: 1,
            Kind: NarrativeEventKind.LeftRoomAbruptly,
            ParticipantIds: new[] { entityId },
            RoomId: null,
            Detail: "conflict"));

        sys.Update(em, 1f);

        // gain = 3.0 * 1.0 = 3.0 → AcuteLevel = 3
        Assert.Equal(3, npc.Get<StressComponent>().AcuteLevel);
        Assert.Equal(1, npc.Get<StressComponent>().SocialConflictEventsToday);
    }
}
