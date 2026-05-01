using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Chronicle;
using APIFramework.Systems.Lighting;
using Warden.Contracts.SchemaValidation;
using Warden.Contracts.Telemetry;

namespace Warden.Telemetry.SaveLoad;

/// <summary>
/// Projects the full engine state from <see cref="SimulationBootstrapper"/> into a
/// <see cref="WorldStateDto"/> suitable for save/load round-trips (schema v0.5).
///
/// Unlike <see cref="TelemetryProjector"/>, which is designed for AI consumption,
/// this projector captures ALL persistent component state.
/// </summary>
public static class SaveProjector
{
    /// <summary>
    /// Produces a complete save-format <see cref="WorldStateDto"/> from the running simulation.
    /// Includes full NPC state, task entities, stain entities, locked doors, and the extended
    /// clock state needed for exact restoration.
    /// </summary>
    public static WorldStateDto Project(SimulationBootstrapper sim)
    {
        var capturedAt = DateTimeOffset.UtcNow;
        var snap       = sim.Capture();

        // Base telemetry projection (rooms, lights, relationships, chronicle, clock display)
        var baseDto = TelemetryProjector.Project(
            snap,
            sim.EntityManager,
            capturedAt,
            sim.Clock.CurrentTick,
            sim.Random.Seed,
            APIFramework.Core.SimVersion.Full,
            sim.SunState,
            sim.Chronicle);

        return baseDto with
        {
            SchemaVersion   = SchemaVersions.WorldState,
            SaveTick        = sim.Clock.CurrentTick,
            SaveTotalTime   = sim.Clock.TotalTime,
            SaveTimeScale   = sim.Clock.TimeScale,
            EntityIdCounter = sim.EntityManager.IdCounter,
            NpcSaveStates   = ProjectNpcs(sim.EntityManager),
            TaskEntities    = ProjectTasks(sim.EntityManager),
            StainEntities   = ProjectStains(sim.EntityManager),
            LockedDoors     = ProjectLockedDoors(sim.EntityManager),
        };
    }

    // ── NPC entities ──────────────────────────────────────────────────────────

    private static IReadOnlyList<NpcSaveDto> ProjectNpcs(EntityManager em)
    {
        var result = new List<NpcSaveDto>();
        foreach (var e in em.GetAllEntities()
                             .Where(e => e.Has<MetabolismComponent>() || e.Has<CorpseTag>())
                             .OrderBy(e => e.Id))
        {
            result.Add(ProjectNpc(e));
        }
        return result;
    }

    private static NpcSaveDto ProjectNpc(Entity e)
    {
        var meta    = e.Has<MetabolismComponent>()      ? e.Get<MetabolismComponent>()      : default;
        var energy  = e.Has<EnergyComponent>()           ? e.Get<EnergyComponent>()           : default;
        var pos     = e.Has<PositionComponent>()         ? e.Get<PositionComponent>()         : default;
        var stomach = e.Has<StomachComponent>()          ? e.Get<StomachComponent>()          : default;
        var si      = e.Has<SmallIntestineComponent>()   ? e.Get<SmallIntestineComponent>()   : default;
        var li      = e.Has<LargeIntestineComponent>()   ? e.Get<LargeIntestineComponent>()   : default;
        var colon   = e.Has<ColonComponent>()            ? e.Get<ColonComponent>()            : default;
        var bladder = e.Has<BladderComponent>()          ? e.Get<BladderComponent>()          : default;

        LifeStateSaveDto? lifeState = null;
        if (e.Has<LifeStateComponent>())
        {
            var ls = e.Get<LifeStateComponent>();
            lifeState = new LifeStateSaveDto
            {
                State                   = (SaveLifeState)(int)ls.State,
                LastTransitionTick      = ls.LastTransitionTick,
                IncapacitatedTickBudget = ls.IncapacitatedTickBudget,
                PendingDeathCause       = (SaveCauseOfDeath)(int)ls.PendingDeathCause,
            };
        }

        ChokeSaveDto? choking = null;
        if (e.Has<ChokingComponent>())
        {
            var c = e.Get<ChokingComponent>();
            choking = new ChokeSaveDto
            {
                ChokeStartTick = c.ChokeStartTick,
                RemainingTicks = c.RemainingTicks,
                BolusSize      = c.BolusSize,
                PendingCause   = (SaveCauseOfDeath)(int)c.PendingCause,
            };
        }

        FaintSaveDto? fainting = null;
        if (e.Has<FaintingComponent>())
        {
            var f = e.Get<FaintingComponent>();
            fainting = new FaintSaveDto
            {
                FaintStartTick = f.FaintStartTick,
                RecoveryTick   = f.RecoveryTick,
            };
        }

        LockedInSaveDto? lockedIn = null;
        if (e.Has<LockedInComponent>())
        {
            var li2 = e.Get<LockedInComponent>();
            lockedIn = new LockedInSaveDto
            {
                FirstDetectedTick    = li2.FirstDetectedTick,
                StarvationTickBudget = li2.StarvationTickBudget,
            };
        }

        CauseOfDeathSaveDto? causeOfDeath = null;
        if (e.Has<CauseOfDeathComponent>())
        {
            var cod = e.Get<CauseOfDeathComponent>();
            causeOfDeath = new CauseOfDeathSaveDto
            {
                Cause          = (SaveCauseOfDeath)(int)cod.Cause,
                DeathTick      = cod.DeathTick,
                WitnessedById  = cod.WitnessedByNpcId.ToString(),
                LocationRoomId = cod.LocationRoomId.ToString(),
            };
        }

        CorpseSaveDto? corpse = null;
        if (e.Has<CorpseComponent>())
        {
            var c = e.Get<CorpseComponent>();
            corpse = new CorpseSaveDto
            {
                DeathTick           = c.DeathTick,
                OriginalNpcEntityId = c.OriginalNpcEntityId.ToString(),
                LocationRoomId      = c.LocationRoomId,
                HasBeenMoved        = c.HasBeenMoved,
            };
        }

        StressSaveDto? stress = null;
        if (e.Has<StressComponent>())
        {
            var s = e.Get<StressComponent>();
            stress = new StressSaveDto
            {
                AcuteLevel                = s.AcuteLevel,
                ChronicLevel              = s.ChronicLevel,
                LastDayUpdated            = s.LastDayUpdated,
                SuppressionEventsToday    = s.SuppressionEventsToday,
                DriveSpikeEventsToday     = s.DriveSpikeEventsToday,
                SocialConflictEventsToday = s.SocialConflictEventsToday,
                OverdueTaskEventsToday    = s.OverdueTaskEventsToday,
                WitnessedDeathEventsToday = s.WitnessedDeathEventsToday,
                BereavementEventsToday    = s.BereavementEventsToday,
                BurnoutLastAppliedDay     = s.BurnoutLastAppliedDay,
            };
        }

        MaskSaveDto? mask = null;
        if (e.Has<SocialMaskComponent>())
        {
            var m = e.Get<SocialMaskComponent>();
            mask = new MaskSaveDto
            {
                IrritationMask = m.IrritationMask,
                AffectionMask  = m.AffectionMask,
                AttractionMask = m.AttractionMask,
                LonelinessMask = m.LonelinessMask,
                CurrentLoad    = m.CurrentLoad,
                Baseline       = m.Baseline,
                LastSlipTick   = m.LastSlipTick,
            };
        }

        MoodSaveDto? mood = null;
        if (e.Has<MoodComponent>())
        {
            var m = e.Get<MoodComponent>();
            mood = new MoodSaveDto
            {
                Joy          = m.Joy,
                Trust        = m.Trust,
                Fear         = m.Fear,
                Surprise     = m.Surprise,
                Sadness      = m.Sadness,
                Disgust      = m.Disgust,
                Anger        = m.Anger,
                Anticipation = m.Anticipation,
                PanicLevel   = m.PanicLevel,
                GriefLevel   = m.GriefLevel,
            };
        }

        WillpowerSaveDto? willpower = null;
        if (e.Has<WillpowerComponent>())
        {
            var w = e.Get<WillpowerComponent>();
            willpower = new WillpowerSaveDto { Current = w.Current, Baseline = w.Baseline };
        }

        WorkloadSaveDto? workload = null;
        if (e.Has<WorkloadComponent>())
        {
            var w = e.Get<WorkloadComponent>();
            workload = new WorkloadSaveDto
            {
                ActiveTaskIds = (w.ActiveTasks ?? Array.Empty<Guid>())
                                .Select(g => g.ToString()).ToList(),
                Capacity    = w.Capacity,
                CurrentLoad = w.CurrentLoad,
            };
        }

        IReadOnlyList<string>? corpseIds = null;
        if (e.Has<BereavementHistoryComponent>())
        {
            var bh = e.Get<BereavementHistoryComponent>();
            if (bh.EncounteredCorpseIds?.Count > 0)
                corpseIds = bh.EncounteredCorpseIds.Select(g => g.ToString()).ToList();
        }

        IReadOnlyList<ScheduleBlockSaveDto>? scheduleBlocks = null;
        if (e.Has<ScheduleComponent>())
        {
            var sc = e.Get<ScheduleComponent>();
            if (sc.Blocks?.Count > 0)
            {
                scheduleBlocks = sc.Blocks.Select(b => new ScheduleBlockSaveDto
                {
                    StartHour = b.StartHour,
                    EndHour   = b.EndHour,
                    AnchorId  = b.AnchorId,
                    Activity  = (SaveScheduleActivity)b.Activity,
                }).ToList();
            }
        }

        return new NpcSaveDto
        {
            Id                   = e.Id.ToString(),
            Name                 = e.Has<IdentityComponent>() ? e.Get<IdentityComponent>().Name : e.ShortId,
            PosX                 = pos.X,
            PosY                 = pos.Y,
            PosZ                 = pos.Z,
            Satiation            = meta.Satiation,
            Hydration            = meta.Hydration,
            BodyTemp             = meta.BodyTemp,
            Energy               = energy.Energy,
            Sleepiness           = energy.Sleepiness,
            IsSleeping           = energy.IsSleeping,
            SatiationDrainRate   = meta.SatiationDrainRate,
            HydrationDrainRate   = meta.HydrationDrainRate,
            StomachVolumeMl      = stomach.CurrentVolumeMl,
            SiChymeVolumeMl      = si.ChymeVolumeMl,
            LiContentVolumeMl    = li.ContentVolumeMl,
            ColonStoolVolumeMl   = colon.StoolVolumeMl,
            BladderVolumeMl      = bladder.VolumeML,
            LifeState            = lifeState,
            Choking              = choking,
            Fainting             = fainting,
            LockedIn             = lockedIn,
            CauseOfDeath         = causeOfDeath,
            Corpse               = corpse,
            Stress               = stress,
            Mask                 = mask,
            Mood                 = mood,
            Willpower            = willpower,
            Workload             = workload,
            EncounteredCorpseIds = corpseIds,
            ScheduleBlocks       = scheduleBlocks,
        };
    }

    // ── Task entities ─────────────────────────────────────────────────────────

    private static IReadOnlyList<TaskSaveDto>? ProjectTasks(EntityManager em)
    {
        var taskEntities = em.Query<TaskTag>()
            .Where(e => e.Has<TaskComponent>())
            .OrderBy(e => e.Id)
            .ToList();

        if (taskEntities.Count == 0) return null;

        return taskEntities.Select(e =>
        {
            var t = e.Get<TaskComponent>();
            return new TaskSaveDto
            {
                Id            = e.Id.ToString(),
                EffortHours   = t.EffortHours,
                DeadlineTick  = t.DeadlineTick,
                Priority      = t.Priority,
                Progress      = t.Progress,
                QualityLevel  = t.QualityLevel,
                AssignedNpcId = t.AssignedNpcId.ToString(),
                CreatedTick   = t.CreatedTick,
            };
        }).ToList();
    }

    // ── Stain entities ────────────────────────────────────────────────────────

    private static IReadOnlyList<StainEntitySaveDto>? ProjectStains(EntityManager em)
    {
        var stainEntities = em.Query<StainTag>()
            .Where(e => e.Has<StainComponent>())
            .OrderBy(e => e.Id)
            .ToList();

        if (stainEntities.Count == 0) return null;

        return stainEntities.Select(e =>
        {
            var s   = e.Get<StainComponent>();
            var pos = e.Has<PositionComponent>() ? e.Get<PositionComponent>() : default;
            return new StainEntitySaveDto
            {
                Id               = e.Id.ToString(),
                PosX             = pos.X,
                PosZ             = pos.Z,
                Source           = s.Source,
                Magnitude        = s.Magnitude,
                CreatedAtTick    = s.CreatedAtTick,
                ChronicleEntryId = s.ChronicleEntryId,
                FallRisk         = e.Has<FallRiskComponent>() ? e.Get<FallRiskComponent>().RiskLevel : null,
            };
        }).ToList();
    }

    // ── Locked doors ──────────────────────────────────────────────────────────

    private static IReadOnlyList<LockedDoorSaveDto>? ProjectLockedDoors(EntityManager em)
    {
        var doorEntities = em.Query<LockedTag>()
            .Where(e => e.Has<PositionComponent>())
            .OrderBy(e => e.Id)
            .ToList();

        if (doorEntities.Count == 0) return null;

        return doorEntities.Select(e =>
        {
            var pos = e.Get<PositionComponent>();
            return new LockedDoorSaveDto
            {
                Id   = e.Id.ToString(),
                PosX = pos.X,
                PosY = pos.Y,
                PosZ = pos.Z,
                Name = e.Has<IdentityComponent>() ? e.Get<IdentityComponent>().Name : null,
            };
        }).ToList();
    }
}
