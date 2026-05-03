using APIFramework.Components;
using APIFramework.Core;
using Warden.Contracts.Telemetry;

namespace Warden.Telemetry.SaveLoad;

/// <summary>
/// Produces a save-game <see cref="WorldStateDto"/> that captures ALL persistent component
/// state needed for a faithful round-trip. Unlike <see cref="TelemetryProjector"/>, which
/// produces an AI-consumable subset, this projector writes every mutable field.
/// </summary>
internal static class SaveProjector
{
    internal static WorldStateDto Project(SimulationBootstrapper sim)
    {
        var snap     = sim.Capture();
        var base_dto = TelemetryProjector.Project(
            snap,
            sim.EntityManager,
            DateTimeOffset.UtcNow,
            sim.Clock.CurrentTick,
            0,
            "save");

        return base_dto with
        {
            SchemaVersion   = "0.5.1",
            SaveTick        = sim.Clock.CurrentTick,
            SaveTotalTime   = sim.Clock.TotalTime,
            SaveTimeScale   = sim.Clock.TimeScale,
            EntityIdCounter = sim.EntityManager.IdCounter,
            NpcSaveStates   = ProjectNpcs(sim),
            TaskEntities    = ProjectTasks(sim),
            StainEntities   = ProjectStains(sim),
            LockedDoors     = ProjectLockedDoors(sim)
        };
    }

    // ── NPC entities ──────────────────────────────────────────────────────────

    private static List<NpcSaveDto> ProjectNpcs(SimulationBootstrapper sim)
    {
        var result = new List<NpcSaveDto>();
        var em     = sim.EntityManager;

        var seen = new HashSet<Entity>();
        foreach (var e in em.Query<MetabolismComponent>()) seen.Add(e);
        foreach (var e in em.Query<CorpseTag>())           seen.Add(e);

        foreach (var entity in seen)
            result.Add(ProjectNpc(entity));

        return result;
    }

    private static NpcSaveDto ProjectNpc(Entity entity)
    {
        var id      = entity.Id.ToString();
        var name    = entity.Has<IdentityComponent>() ? entity.Get<IdentityComponent>().Name : string.Empty;
        var isHuman = entity.Has<HumanTag>();
        float posX = 0f, posY = 0f, posZ = 0f;
        if (entity.Has<PositionComponent>())
        {
            var pos = entity.Get<PositionComponent>();
            posX = pos.X; posY = pos.Y; posZ = pos.Z;
        }

        float satiation = 0f, hydration = 0f, bodyTemp = 36.6f;
        float satiationDrain = 0f, hydrationDrain = 0f;
        if (entity.Has<MetabolismComponent>())
        {
            var m = entity.Get<MetabolismComponent>();
            satiation = m.Satiation; hydration = m.Hydration; bodyTemp = m.BodyTemp;
            satiationDrain = m.SatiationDrainRate; hydrationDrain = m.HydrationDrainRate;
        }

        float energy = 0f, sleepiness = 0f;
        bool isSleeping = false;
        if (entity.Has<EnergyComponent>())
        {
            var e2 = entity.Get<EnergyComponent>();
            energy = e2.Energy; sleepiness = e2.Sleepiness; isSleeping = e2.IsSleeping;
        }

        float stomachVol = 0f, siVol = 0f, liVol = 0f, colonVol = 0f, bladderVol = 0f;
        if (entity.Has<StomachComponent>())        stomachVol = entity.Get<StomachComponent>().CurrentVolumeMl;
        if (entity.Has<SmallIntestineComponent>()) siVol      = entity.Get<SmallIntestineComponent>().ChymeVolumeMl;
        if (entity.Has<LargeIntestineComponent>()) liVol      = entity.Get<LargeIntestineComponent>().ContentVolumeMl;
        if (entity.Has<ColonComponent>())          colonVol   = entity.Get<ColonComponent>().StoolVolumeMl;
        if (entity.Has<BladderComponent>())        bladderVol = entity.Get<BladderComponent>().VolumeML;

        return new NpcSaveDto
        {
            Id               = id,
            Name             = name,
            IsHuman          = isHuman,
            PosX             = posX,
            PosY             = posY,
            PosZ             = posZ,
            Satiation        = satiation,
            Hydration        = hydration,
            BodyTemp         = bodyTemp,
            SatiationDrainRate  = satiationDrain,
            HydrationDrainRate  = hydrationDrain,
            Energy           = energy,
            Sleepiness       = sleepiness,
            IsSleeping       = isSleeping,
            StomachVolumeMl  = stomachVol,
            SiChymeVolumeMl  = siVol,
            LiContentVolumeMl = liVol,
            ColonStoolVolumeMl = colonVol,
            BladderVolumeMl  = bladderVol,
            LifeState        = ProjectLifeState(entity),
            Choking          = ProjectChoking(entity),
            Fainting         = ProjectFainting(entity),
            LockedIn         = ProjectLockedIn(entity),
            CauseOfDeath     = ProjectCauseOfDeath(entity),
            Corpse           = ProjectCorpse(entity),
            Stress           = ProjectStress(entity),
            Mask             = ProjectMask(entity),
            Mood             = ProjectMood(entity),
            Willpower        = ProjectWillpower(entity),
            Workload         = ProjectWorkload(entity),
            PersonalSpace    = ProjectPersonalSpace(entity),
            ScheduleBlocks   = ProjectScheduleBlocks(entity),
            EncounteredCorpseIds = ProjectEncounteredCorpses(entity)
        };
    }

    private static LifeStateSaveDto? ProjectLifeState(Entity entity)
    {
        if (!entity.Has<LifeStateComponent>()) return null;
        var c = entity.Get<LifeStateComponent>();
        return new LifeStateSaveDto
        {
            State                   = (SaveLifeState)(int)c.State,
            LastTransitionTick      = c.LastTransitionTick,
            IncapacitatedTickBudget = c.IncapacitatedTickBudget,
            PendingDeathCause       = (SaveCauseOfDeath)(int)c.PendingDeathCause
        };
    }

    private static ChokeSaveDto? ProjectChoking(Entity entity)
    {
        if (!entity.Has<ChokingComponent>()) return null;
        var c = entity.Get<ChokingComponent>();
        return new ChokeSaveDto
        {
            ChokeStartTick = c.ChokeStartTick,
            RemainingTicks = c.RemainingTicks,
            BolusSize      = c.BolusSize,
            PendingCause   = (SaveCauseOfDeath)(int)c.PendingCause
        };
    }

    private static FaintSaveDto? ProjectFainting(Entity entity)
    {
        if (!entity.Has<FaintingComponent>()) return null;
        var c = entity.Get<FaintingComponent>();
        return new FaintSaveDto { FaintStartTick = c.FaintStartTick, RecoveryTick = c.RecoveryTick };
    }

    private static LockedInSaveDto? ProjectLockedIn(Entity entity)
    {
        if (!entity.Has<LockedInComponent>()) return null;
        var c = entity.Get<LockedInComponent>();
        return new LockedInSaveDto { FirstDetectedTick = c.FirstDetectedTick, StarvationTickBudget = c.StarvationTickBudget };
    }

    private static CauseOfDeathSaveDto? ProjectCauseOfDeath(Entity entity)
    {
        if (!entity.Has<CauseOfDeathComponent>()) return null;
        var c = entity.Get<CauseOfDeathComponent>();
        return new CauseOfDeathSaveDto
        {
            Cause          = (SaveCauseOfDeath)(int)c.Cause,
            DeathTick      = c.DeathTick,
            WitnessedById  = c.WitnessedByNpcId == Guid.Empty ? string.Empty : c.WitnessedByNpcId.ToString(),
            LocationRoomId = c.LocationRoomId   == Guid.Empty ? string.Empty : c.LocationRoomId.ToString()
        };
    }

    private static CorpseSaveDto? ProjectCorpse(Entity entity)
    {
        if (!entity.Has<CorpseComponent>()) return null;
        var c = entity.Get<CorpseComponent>();
        return new CorpseSaveDto
        {
            DeathTick           = c.DeathTick,
            OriginalNpcEntityId = c.OriginalNpcEntityId.ToString(),
            LocationRoomId      = c.LocationRoomId,
            HasBeenMoved        = c.HasBeenMoved
        };
    }

    private static StressSaveDto? ProjectStress(Entity entity)
    {
        if (!entity.Has<StressComponent>()) return null;
        var c = entity.Get<StressComponent>();
        return new StressSaveDto
        {
            AcuteLevel                = c.AcuteLevel,
            ChronicLevel              = c.ChronicLevel,
            LastDayUpdated            = c.LastDayUpdated,
            SuppressionEventsToday    = c.SuppressionEventsToday,
            DriveSpikeEventsToday     = c.DriveSpikeEventsToday,
            SocialConflictEventsToday = c.SocialConflictEventsToday,
            OverdueTaskEventsToday    = c.OverdueTaskEventsToday,
            BurnoutLastAppliedDay     = c.BurnoutLastAppliedDay,
            WitnessedDeathEventsToday = c.WitnessedDeathEventsToday,
            BereavementEventsToday    = c.BereavementEventsToday
        };
    }

    private static MaskSaveDto? ProjectMask(Entity entity)
    {
        if (!entity.Has<SocialMaskComponent>()) return null;
        var c = entity.Get<SocialMaskComponent>();
        return new MaskSaveDto
        {
            IrritationMask = c.IrritationMask,
            AffectionMask  = c.AffectionMask,
            AttractionMask = c.AttractionMask,
            LonelinessMask = c.LonelinessMask,
            CurrentLoad    = c.CurrentLoad,
            Baseline       = c.Baseline,
            LastSlipTick   = c.LastSlipTick
        };
    }

    private static MoodSaveDto? ProjectMood(Entity entity)
    {
        if (!entity.Has<MoodComponent>()) return null;
        var c = entity.Get<MoodComponent>();
        return new MoodSaveDto
        {
            Joy          = c.Joy,
            Trust        = c.Trust,
            Fear         = c.Fear,
            Surprise     = c.Surprise,
            Sadness      = c.Sadness,
            Disgust      = c.Disgust,
            Anger        = c.Anger,
            Anticipation = c.Anticipation,
            PanicLevel   = c.PanicLevel,
            GriefLevel   = c.GriefLevel
        };
    }

    private static WillpowerSaveDto? ProjectWillpower(Entity entity)
    {
        if (!entity.Has<WillpowerComponent>()) return null;
        var c = entity.Get<WillpowerComponent>();
        return new WillpowerSaveDto { Current = c.Current, Baseline = c.Baseline };
    }

    private static WorkloadSaveDto? ProjectWorkload(Entity entity)
    {
        if (!entity.Has<WorkloadComponent>()) return null;
        var c = entity.Get<WorkloadComponent>();
        var taskIds = c.ActiveTasks?.Select(g => g.ToString()).ToList();
        return new WorkloadSaveDto
        {
            Capacity      = c.Capacity,
            CurrentLoad   = c.CurrentLoad,
            ActiveTaskIds = taskIds
        };
    }

    private static List<ScheduleBlockSaveDto>? ProjectScheduleBlocks(Entity entity)
    {
        if (!entity.Has<ScheduleComponent>()) return null;
        var blocks = entity.Get<ScheduleComponent>().Blocks;
        if (blocks == null || blocks.Count == 0) return null;
        return blocks.Select(b => new ScheduleBlockSaveDto
        {
            StartHour = b.StartHour,
            EndHour   = b.EndHour,
            AnchorId  = b.AnchorId,
            Activity  = (SaveScheduleActivity)(int)b.Activity
        }).ToList();
    }

    private static PersonalSpaceSaveDto? ProjectPersonalSpace(Entity entity)
    {
        if (!entity.Has<PersonalSpaceComponent>()) return null;
        var c = entity.Get<PersonalSpaceComponent>();
        return new PersonalSpaceSaveDto
        {
            RadiusMeters      = c.RadiusMeters,
            RepulsionStrength = c.RepulsionStrength
        };
    }

    private static List<string>? ProjectEncounteredCorpses(Entity entity)
    {
        if (!entity.Has<BereavementHistoryComponent>()) return null;
        var c = entity.Get<BereavementHistoryComponent>();
        return c.EncounteredCorpseIds?.Select(g => g.ToString()).ToList();
    }

    // ── Task entities ─────────────────────────────────────────────────────────

    private static List<TaskSaveDto> ProjectTasks(SimulationBootstrapper sim)
    {
        var result = new List<TaskSaveDto>();
        foreach (var entity in sim.EntityManager.Query<TaskTag>())
        {
            if (!entity.Has<TaskComponent>()) continue;
            var c = entity.Get<TaskComponent>();
            result.Add(new TaskSaveDto
            {
                Id            = entity.Id.ToString(),
                EffortHours   = c.EffortHours,
                DeadlineTick  = c.DeadlineTick,
                Priority      = c.Priority,
                Progress      = c.Progress,
                QualityLevel  = c.QualityLevel,
                AssignedNpcId = c.AssignedNpcId == Guid.Empty ? string.Empty : c.AssignedNpcId.ToString(),
                CreatedTick   = c.CreatedTick
            });
        }
        return result;
    }

    // ── Stain entities ────────────────────────────────────────────────────────

    private static List<StainEntitySaveDto> ProjectStains(SimulationBootstrapper sim)
    {
        var result = new List<StainEntitySaveDto>();
        foreach (var entity in sim.EntityManager.Query<StainTag>())
        {
            if (!entity.Has<StainComponent>()) continue;
            var c   = entity.Get<StainComponent>();
            var pos = entity.Has<PositionComponent>() ? entity.Get<PositionComponent>() : default;
            result.Add(new StainEntitySaveDto
            {
                Id               = entity.Id.ToString(),
                PosX             = pos.X,
                PosZ             = pos.Z,
                Source           = c.Source,
                Magnitude        = c.Magnitude,
                CreatedAtTick    = c.CreatedAtTick,
                ChronicleEntryId = c.ChronicleEntryId,
                FallRisk         = entity.Has<FallRiskComponent>() ? entity.Get<FallRiskComponent>().RiskLevel : (float?)null,
                IsObstacle       = entity.Has<ObstacleTag>()
            });
        }
        return result;
    }

    // ── Locked-door entities ──────────────────────────────────────────────────

    private static List<LockedDoorSaveDto> ProjectLockedDoors(SimulationBootstrapper sim)
    {
        var result = new List<LockedDoorSaveDto>();
        foreach (var entity in sim.EntityManager.Query<LockedTag>())
        {
            var pos  = entity.Has<PositionComponent>() ? entity.Get<PositionComponent>() : default;
            var name = entity.Has<IdentityComponent>() ? entity.Get<IdentityComponent>().Name : null;
            result.Add(new LockedDoorSaveDto
            {
                Id   = entity.Id.ToString(),
                PosX = pos.X,
                PosY = pos.Y,
                PosZ = pos.Z,
                Name = name
            });
        }
        return result;
    }
}