using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using Warden.Contracts.Telemetry;
using Warden.Telemetry.SaveLoad;
using Xunit;

namespace Warden.Telemetry.Tests;

/// <summary>
/// Acceptance tests for SaveLoadService and SimulationBootstrapper.BootFromWorldStateDto.
///
/// AT-SL-01 — Clock state (Tick, TotalTime, TimeScale) survives save/load.
/// AT-SL-02 — Entity ID counter survives save/load.
/// AT-SL-03 — NPC position, metabolism, and GI fill levels survive save/load.
/// AT-SL-04 — Stress, mask, mood, willpower, and workload survive save/load.
/// AT-SL-05 — Choking state survives save/load; IsChokingTag is restored.
/// AT-SL-06 — Fainting state survives save/load; IsFaintingTag is restored.
/// AT-SL-07 — LockedTag entity with position survives save/load.
/// AT-SL-08 — Corpse (CorpseTag + CorpseComponent) survives save/load.
/// AT-SL-09 — BereavementHistoryComponent survives save/load.
/// AT-SL-10 — Schema v0.4.0 document migrates to v0.5.0 on load.
/// AT-SL-11 — Schema version newer than v0.5.0 throws SaveLoadException.
/// AT-SL-12 — Task entities survive save/load.
/// AT-SL-13 — Stain entities survive save/load.
/// AT-SL-14 — Float fields survive with precision within 0.001f.
/// AT-SL-15 — LifeStateComponent survives save/load.
/// AT-SL-16 — ScheduleComponent survives save/load.
/// </summary>
public class SaveLoadRoundTripTests
{
    private static IConfigProvider Cfg() => new InMemoryConfigProvider(new SimConfig());

    private static SimulationBootstrapper MakeSim(int humans = 1)
        => new(Cfg(), humans);

    private static (string json, SimulationBootstrapper loaded) RoundTrip(SimulationBootstrapper sim)
    {
        var json   = SaveLoadService.Save(sim);
        var loaded = SaveLoadService.Load(json, Cfg());
        return (json, loaded);
    }

    // ── AT-SL-01: Clock ───────────────────────────────────────────────────────

    [Fact]
    public void AT_SL_01_Clock_RoundTrips()
    {
        var sim = MakeSim(0);
        sim.Clock.Tick(1.5f);
        sim.Clock.Tick(0.3f);

        var (_, loaded) = RoundTrip(sim);

        Assert.Equal(sim.Clock.CurrentTick, loaded.Clock.CurrentTick);
        Assert.Equal(sim.Clock.TotalTime,   loaded.Clock.TotalTime,  3);
        Assert.Equal(sim.Clock.TimeScale,   loaded.Clock.TimeScale,  3);
    }

    // ── AT-SL-02: Entity ID counter ───────────────────────────────────────────

    [Fact]
    public void AT_SL_02_EntityIdCounter_RoundTrips()
    {
        var sim = MakeSim(3);
        var counterBefore = sim.EntityManager.IdCounter;

        var (_, loaded) = RoundTrip(sim);

        Assert.Equal(counterBefore, loaded.EntityManager.IdCounter);
    }

    // ── AT-SL-03: NPC position and metabolism ────────────────────────────────

    [Fact]
    public void AT_SL_03_NpcBiologicalState_RoundTrips()
    {
        var sim = MakeSim(1);
        var npc = sim.EntityManager.Query<MetabolismComponent>().First();

        npc.Set(new MetabolismComponent
        {
            Satiation          = 55.5f,
            Hydration          = 42.1f,
            BodyTemp           = 37.2f,
            SatiationDrainRate = 0.002f,
            HydrationDrainRate = 0.003f,
            SleepMetabolismMultiplier = 0.5f
        });
        npc.Set(new PositionComponent { X = 3.14f, Y = 0f, Z = 2.71f });
        npc.Set(new ColonComponent    { StoolVolumeMl = 12.5f, UrgeThresholdMl = 100f, CapacityMl = 400f });
        npc.Set(new BladderComponent  { VolumeML = 88.0f, FillRate = 1f, UrgeThresholdMl = 100f, CapacityMl = 400f });

        var (_, loaded) = RoundTrip(sim);

        var r = loaded.EntityManager.Entities.First(e => e.Id == npc.Id);

        var m = r.Get<MetabolismComponent>();
        Assert.Equal(55.5f, m.Satiation,  3);
        Assert.Equal(42.1f, m.Hydration,  3);
        Assert.Equal(37.2f, m.BodyTemp,   3);

        var pos = r.Get<PositionComponent>();
        Assert.Equal(3.14f, pos.X, 3);
        Assert.Equal(2.71f, pos.Z, 3);

        var colon   = r.Get<ColonComponent>();
        var bladder = r.Get<BladderComponent>();
        Assert.Equal(12.5f, colon.StoolVolumeMl,   3);
        Assert.Equal(88.0f, bladder.VolumeML,       3);
    }

    // ── AT-SL-04: Stress, mask, mood, willpower ───────────────────────────────

    [Fact]
    public void AT_SL_04_DerivedComponents_RoundTrip()
    {
        var sim = MakeSim(1);
        var npc = sim.EntityManager.Query<MetabolismComponent>().First();

        npc.Add(new StressComponent    { AcuteLevel = 75, ChronicLevel = 0.8, LastDayUpdated = 3 });
        npc.Add(new SocialMaskComponent { IrritationMask = 12, AffectionMask = 5, Baseline = 50, LastSlipTick = 42 });
        npc.Add(new MoodComponent       { Joy = 60f, Fear = 10f, Anger = 25f });
        npc.Add(new WillpowerComponent  { Current = 80, Baseline = 100 });

        var (_, loaded) = RoundTrip(sim);

        var r = loaded.EntityManager.Entities.First(e => e.Id == npc.Id);

        Assert.Equal(75,  r.Get<StressComponent>().AcuteLevel);
        Assert.Equal(0.8, r.Get<StressComponent>().ChronicLevel, 6);
        Assert.Equal(12,  r.Get<SocialMaskComponent>().IrritationMask);
        Assert.Equal(60f, r.Get<MoodComponent>().Joy,            3);
        Assert.Equal(80,  r.Get<WillpowerComponent>().Current);
    }

    // ── AT-SL-05: Choking ─────────────────────────────────────────────────────

    [Fact]
    public void AT_SL_05_ChokingState_RoundTrips()
    {
        var sim = MakeSim(1);
        var npc = sim.EntityManager.Query<MetabolismComponent>().First();

        npc.Add(new ChokingComponent { ChokeStartTick = 500, RemainingTicks = 30, BolusSize = 2.5f, PendingCause = CauseOfDeath.Choked });
        npc.Add(new IsChokingTag());

        var (_, loaded) = RoundTrip(sim);

        var r = loaded.EntityManager.Entities.First(e => e.Id == npc.Id);

        Assert.True(r.Has<ChokingComponent>());
        Assert.True(r.Has<IsChokingTag>());

        var c = r.Get<ChokingComponent>();
        Assert.Equal(500L, c.ChokeStartTick);
        Assert.Equal(30,   c.RemainingTicks);
        Assert.Equal(2.5f, c.BolusSize, 3);
    }

    // ── AT-SL-06: Fainting ────────────────────────────────────────────────────

    [Fact]
    public void AT_SL_06_FaintingState_RoundTrips()
    {
        var sim = MakeSim(1);
        var npc = sim.EntityManager.Query<MetabolismComponent>().First();

        npc.Add(new FaintingComponent { FaintStartTick = 200, RecoveryTick = 260 });
        npc.Add(new IsFaintingTag());

        var (_, loaded) = RoundTrip(sim);

        var r = loaded.EntityManager.Entities.First(e => e.Id == npc.Id);

        Assert.True(r.Has<FaintingComponent>());
        Assert.True(r.Has<IsFaintingTag>());

        var f = r.Get<FaintingComponent>();
        Assert.Equal(200L, f.FaintStartTick);
        Assert.Equal(260L, f.RecoveryTick);
    }

    // ── AT-SL-07: Locked door ─────────────────────────────────────────────────

    [Fact]
    public void AT_SL_07_LockedDoor_RoundTrips()
    {
        var sim  = MakeSim(0);
        var door = sim.EntityManager.CreateEntity();
        door.Add(new LockedTag());
        door.Add(new PositionComponent { X = 5f, Y = 0f, Z = 7f });
        door.Add(new IdentityComponent { Name = "Office Door" });

        var (_, loaded) = RoundTrip(sim);

        var r = loaded.EntityManager.Entities.First(e => e.Id == door.Id);

        Assert.True(r.Has<LockedTag>());

        var pos = r.Get<PositionComponent>();
        Assert.Equal(5f, pos.X, 3);
        Assert.Equal(7f, pos.Z, 3);
        Assert.Equal("Office Door", r.Get<IdentityComponent>().Name);
    }

    // ── AT-SL-08: Corpse ──────────────────────────────────────────────────────

    [Fact]
    public void AT_SL_08_CorpseState_RoundTrips()
    {
        var sim  = MakeSim(1);
        var npc  = sim.EntityManager.Query<MetabolismComponent>().First();
        var origId = npc.Id;

        npc.Add(new CorpseTag());
        npc.Add(new CorpseComponent
        {
            DeathTick           = 999L,
            OriginalNpcEntityId = origId,
            LocationRoomId      = "room-001",
            HasBeenMoved        = false
        });
        npc.Add(new LifeStateComponent { State = LifeState.Deceased, LastTransitionTick = 999L, PendingDeathCause = CauseOfDeath.StarvedAlone });

        var (_, loaded) = RoundTrip(sim);

        var r = loaded.EntityManager.Entities.First(e => e.Id == npc.Id);

        Assert.True(r.Has<CorpseTag>());
        Assert.True(r.Has<CorpseComponent>());

        var c = r.Get<CorpseComponent>();
        Assert.Equal(999L,     c.DeathTick);
        Assert.Equal(origId,   c.OriginalNpcEntityId);
        Assert.Equal("room-001", c.LocationRoomId);

        var ls = r.Get<LifeStateComponent>();
        Assert.Equal(LifeState.Deceased, ls.State);
    }

    // ── AT-SL-09: Bereavement history ─────────────────────────────────────────

    [Fact]
    public void AT_SL_09_BereavementHistory_RoundTrips()
    {
        var sim    = MakeSim(1);
        var npc    = sim.EntityManager.Query<MetabolismComponent>().First();
        var corpseId = Guid.NewGuid();

        npc.Add(new BereavementHistoryComponent
        {
            EncounteredCorpseIds = new System.Collections.Generic.HashSet<Guid> { corpseId }
        });

        var (_, loaded) = RoundTrip(sim);

        var r = loaded.EntityManager.Entities.First(e => e.Id == npc.Id);

        Assert.True(r.Has<BereavementHistoryComponent>());
        var bh = r.Get<BereavementHistoryComponent>();
        Assert.Contains(corpseId, bh.EncounteredCorpseIds);
    }

    // ── AT-SL-10: v0.4 → v0.5 migration ─────────────────────────────────────

    [Fact]
    public void AT_SL_10_OlderSchema_MigratesSuccessfully()
    {
        var v04 = new WorldStateDto
        {
            SchemaVersion = "0.4.0",
            CapturedAt    = DateTimeOffset.UtcNow,
            Tick          = 10,
            Clock         = new ClockStateDto(),
            Invariants    = new InvariantDigestDto()
        };
        var json   = TelemetrySerializer.SerializeSnapshot(v04);
        var loaded = SaveLoadService.Load(json, Cfg());

        Assert.NotNull(loaded);
        Assert.Empty(loaded.EntityManager.Entities);
    }

    // ── AT-SL-11: Newer schema fails closed ───────────────────────────────────

    [Fact]
    public void AT_SL_11_NewerSchema_ThrowsSaveLoadException()
    {
        var futureDto = new WorldStateDto
        {
            SchemaVersion = "99.0.0",
            CapturedAt    = DateTimeOffset.UtcNow,
            Tick          = 0,
            Clock         = new ClockStateDto(),
            Invariants    = new InvariantDigestDto()
        };
        var json = TelemetrySerializer.SerializeSnapshot(futureDto);

        Assert.Throws<SaveLoadException>(() => SaveLoadService.Load(json, Cfg()));
    }

    // ── AT-SL-12: Task entities ────────────────────────────────────────────────

    [Fact]
    public void AT_SL_12_TaskEntities_RoundTrip()
    {
        var sim  = MakeSim(0);
        var task = sim.EntityManager.CreateEntity();
        task.Add(new TaskTag());
        task.Add(new TaskComponent
        {
            EffortHours  = 4f,
            DeadlineTick = 7200L,
            Priority     = 80,
            Progress     = 0.35f,
            QualityLevel = 0.9f,
            CreatedTick  = 100L
        });

        var (_, loaded) = RoundTrip(sim);

        var r = loaded.EntityManager.Entities.First(e => e.Id == task.Id);

        Assert.True(r.Has<TaskTag>());
        Assert.True(r.Has<TaskComponent>());

        var tc = r.Get<TaskComponent>();
        Assert.Equal(4f,    tc.EffortHours,  3);
        Assert.Equal(7200L, tc.DeadlineTick);
        Assert.Equal(80,    tc.Priority);
        Assert.Equal(0.35f, tc.Progress,     3);
    }

    // ── AT-SL-13: Stain entities ──────────────────────────────────────────────

    [Fact]
    public void AT_SL_13_StainEntities_RoundTrip()
    {
        var sim   = MakeSim(0);
        var stain = sim.EntityManager.CreateEntity();
        stain.Add(new StainTag());
        stain.Add(new PositionComponent { X = 2f, Y = 0f, Z = 3f });
        stain.Add(new StainComponent    { Source = "water", Magnitude = 40, CreatedAtTick = 50L, ChronicleEntryId = "ch-1" });
        stain.Add(new FallRiskComponent { RiskLevel = 0.6f });

        var (_, loaded) = RoundTrip(sim);

        var r = loaded.EntityManager.Entities.First(e => e.Id == stain.Id);

        Assert.True(r.Has<StainTag>());
        Assert.True(r.Has<StainComponent>());
        Assert.True(r.Has<FallRiskComponent>());

        var sc = r.Get<StainComponent>();
        Assert.Equal("water", sc.Source);
        Assert.Equal(40,      sc.Magnitude);
        Assert.Equal(0.6f,    r.Get<FallRiskComponent>().RiskLevel, 3);
    }

    // ── AT-SL-14: Float precision ─────────────────────────────────────────────

    [Fact]
    public void AT_SL_14_FloatPrecision_WithinTolerance()
    {
        var sim = MakeSim(1);
        var npc = sim.EntityManager.Query<MetabolismComponent>().First();

        npc.Set(new MetabolismComponent { Satiation = 33.333333f, Hydration = 66.666666f, BodyTemp = 36.6f, SatiationDrainRate = 0.00001f, HydrationDrainRate = 0.00001f });

        var (_, loaded) = RoundTrip(sim);

        var r = loaded.EntityManager.Entities.First(e => e.Id == npc.Id);
        var m = r.Get<MetabolismComponent>();

        Assert.True(Math.Abs(33.333333f - m.Satiation)  < 0.001f, "Satiation precision");
        Assert.True(Math.Abs(66.666666f - m.Hydration)  < 0.001f, "Hydration precision");
    }

    // ── AT-SL-15: LifeStateComponent ─────────────────────────────────────────

    [Fact]
    public void AT_SL_15_LifeState_RoundTrips()
    {
        var sim = MakeSim(1);
        var npc = sim.EntityManager.Query<MetabolismComponent>().First();

        npc.Add(new LifeStateComponent
        {
            State                   = LifeState.Incapacitated,
            LastTransitionTick      = 300L,
            IncapacitatedTickBudget = 120,
            PendingDeathCause       = CauseOfDeath.SlippedAndFell
        });

        var (_, loaded) = RoundTrip(sim);

        var r  = loaded.EntityManager.Entities.First(e => e.Id == npc.Id);
        var ls = r.Get<LifeStateComponent>();

        Assert.Equal(LifeState.Incapacitated,     ls.State);
        Assert.Equal(300L,                          ls.LastTransitionTick);
        Assert.Equal(120,                           ls.IncapacitatedTickBudget);
        Assert.Equal(CauseOfDeath.SlippedAndFell,  ls.PendingDeathCause);
    }

    // ── AT-SL-16: ScheduleComponent ──────────────────────────────────────────

    [Fact]
    public void AT_SL_16_ScheduleBlocks_RoundTrip()
    {
        var sim = MakeSim(1);
        var npc = sim.EntityManager.Query<MetabolismComponent>().First();

        var blocks = new System.Collections.Generic.List<ScheduleBlock>
        {
            new(6f, 9f,   "desk-01", ScheduleActivityKind.AtDesk),
            new(9f, 10f,  "kitchen", ScheduleActivityKind.Break),
            new(10f, 17f, "desk-01", ScheduleActivityKind.AtDesk),
            new(17f, 6f,  "home",    ScheduleActivityKind.Sleeping)
        };
        npc.Add(new ScheduleComponent { Blocks = blocks });

        var (_, loaded) = RoundTrip(sim);

        var r  = loaded.EntityManager.Entities.First(e => e.Id == npc.Id);
        Assert.True(r.Has<ScheduleComponent>());

        var restored = r.Get<ScheduleComponent>().Blocks;
        Assert.Equal(4, restored.Count);
        Assert.Equal(ScheduleActivityKind.AtDesk,   restored[0].Activity);
        Assert.Equal("desk-01",                     restored[0].AnchorId);
        Assert.Equal(ScheduleActivityKind.Sleeping, restored[3].Activity);
        Assert.Equal(6f,                            restored[0].StartHour, 3);
    }
}