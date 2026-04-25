using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Lighting;
using Warden.Contracts.Telemetry;
using ContractVocabRegister       = Warden.Contracts.Telemetry.VocabularyRegister;
using ContractInhibitionClass     = Warden.Contracts.Telemetry.InhibitionClass;
using ContractInhibitionAwareness = Warden.Contracts.Telemetry.InhibitionAwareness;
using ContractBigFiveDimension    = Warden.Contracts.Telemetry.BigFiveDimension;
using ContractRelationshipPattern = Warden.Contracts.Telemetry.RelationshipPattern;
using ContractRoomCategory        = Warden.Contracts.Telemetry.RoomCategory;
using ContractBuildingFloor       = Warden.Contracts.Telemetry.BuildingFloor;
using ContractLightKind           = Warden.Contracts.Telemetry.LightKind;
using ContractLightState          = Warden.Contracts.Telemetry.LightState;
using ContractApertureFacing      = Warden.Contracts.Telemetry.ApertureFacing;
using ContractDayPhase            = Warden.Contracts.Telemetry.DayPhase;

namespace Warden.Telemetry;

/// <summary>
/// Projects the engine's internal <see cref="SimulationSnapshot"/> into the
/// wire-format <see cref="WorldStateDto"/> consumed by Tier-3 Haiku agents.
///
/// DESIGN RULES
/// ────────────
/// • Pure function — no I/O, no side-effects, no mutable state.
/// • Never calls <c>DateTime.UtcNow</c> or <c>Guid.NewGuid</c>.
///   Both <paramref name="capturedAt"/> and <paramref name="tick"/> are explicit
///   caller inputs so two runs with identical parameters produce identical output.
/// • Does not modify <c>APIFramework</c>. This is a projection, not a refactor.
/// </summary>
public static class TelemetryProjector
{
    // ── Public surface ────────────────────────────────────────────────────────

    /// <summary>
    /// Projects <paramref name="snap"/> to a <see cref="WorldStateDto"/>.
    /// Species classification falls back to <see cref="SpeciesType.Unknown"/> when
    /// <paramref name="entityManager"/> is <c>null</c>.
    /// </summary>
    /// <param name="snap">Immutable engine snapshot.</param>
    /// <param name="entityManager">
    /// Optional — the same <see cref="EntityManager"/> that produced <paramref name="snap"/>.
    /// When supplied, <see cref="SpeciesClassifier"/> uses tag-based classification and
    /// spatial entities (rooms, light sources, apertures, relationships) are projected.
    /// </param>
    /// <param name="capturedAt">Wall-clock moment this snapshot was taken (caller-owned).</param>
    /// <param name="tick">Simulation tick counter at capture time.</param>
    /// <param name="seed">RNG seed the simulation was booted with.</param>
    /// <param name="simVersion">Value of <c>SimVersion.Full</c> at capture time.</param>
    /// <param name="sunStateService">
    /// Optional — the <see cref="SunStateService"/> singleton from the same bootstrpper.
    /// When supplied, <c>clock.sun</c> is populated from <see cref="SunStateService.CurrentSunState"/>.
    /// </param>
    public static WorldStateDto Project(
        SimulationSnapshot snap,
        EntityManager?     entityManager,
        DateTimeOffset     capturedAt,
        long               tick,
        int                seed,
        string             simVersion,
        SunStateService?   sunStateService = null)
    {
        return new WorldStateDto
        {
            SchemaVersion  = "0.3.0",
            CapturedAt     = capturedAt,
            Tick           = (int)tick,
            Seed           = seed,
            SimVersion     = simVersion,
            Clock          = ProjectClock(snap.Clock, sunStateService),
            Entities       = ProjectEntities(snap.LivingEntities, entityManager),
            WorldItems     = ProjectWorldItems(snap.WorldItems),
            WorldObjects   = ProjectWorldObjects(snap.WorldObjects),
            TransitItems   = ProjectTransitItems(snap.TransitItems),
            Invariants     = new InvariantDigestDto
            {
                ViolationCount   = snap.ViolationCount,
                RecentViolations = null,   // SimulationSnapshot carries only the count
            },
            Relationships  = entityManager is not null ? ProjectRelationships(entityManager) : null,
            Rooms          = entityManager is not null ? ProjectRooms(entityManager)          : null,
            LightSources   = entityManager is not null ? ProjectLightSources(entityManager)   : null,
            LightApertures = entityManager is not null ? ProjectLightApertures(entityManager) : null,
        };
    }

    /// <summary>
    /// Overload without <see cref="EntityManager"/> — species always resolves to
    /// <see cref="SpeciesType.Unknown"/>. Kept for callers that do not have direct
    /// access to the manager (e.g. replay replayers reading serialised snapshots).
    /// </summary>
    public static WorldStateDto Project(
        SimulationSnapshot snap,
        DateTimeOffset     capturedAt,
        long               tick,
        int                seed,
        string             simVersion)
        => Project(snap, null, capturedAt, tick, seed, simVersion);

    // ── Clock ─────────────────────────────────────────────────────────────────

    private static ClockStateDto ProjectClock(ClockSnapshot c, SunStateService? sunService) => new()
    {
        GameTimeDisplay = c.TimeDisplay,
        DayNumber       = c.DayNumber,
        IsDaytime       = c.IsDaytime,
        CircadianFactor = c.CircadianFactor,
        TimeScale       = c.TimeScale,
        Sun             = sunService is not null ? ProjectSunState(sunService.CurrentSunState) : null,
    };

    private static SunStateDto ProjectSunState(SunStateRecord s) => new()
    {
        AzimuthDeg   = s.AzimuthDeg,
        ElevationDeg = s.ElevationDeg,
        DayPhase     = (ContractDayPhase)(int)s.DayPhase,
    };

    // ── Rooms ─────────────────────────────────────────────────────────────────

    private static IReadOnlyList<RoomDto>? ProjectRooms(EntityManager em)
    {
        var roomEntities = em.Query<RoomTag>()
            .Where(e => e.Has<RoomComponent>())
            .OrderBy(e => e.Id)
            .ToList();

        if (roomEntities.Count == 0) return null;

        var result = new List<RoomDto>(roomEntities.Count);
        foreach (var e in roomEntities)
        {
            var r = e.Get<RoomComponent>();
            result.Add(new RoomDto
            {
                Id           = r.Id,
                Name         = r.Name,
                Category     = (ContractRoomCategory)(int)r.Category,
                Floor        = (ContractBuildingFloor)(int)r.Floor,
                BoundsRect   = new BoundsRectDto
                {
                    X      = r.Bounds.X,
                    Y      = r.Bounds.Y,
                    Width  = r.Bounds.Width,
                    Height = r.Bounds.Height,
                },
                Illumination = new IlluminationDto
                {
                    AmbientLevel      = r.Illumination.AmbientLevel,
                    ColorTemperatureK = r.Illumination.ColorTemperatureK,
                    DominantSourceId  = r.Illumination.DominantSourceId,
                },
            });
        }
        return result;
    }

    // ── Light sources ─────────────────────────────────────────────────────────

    private static IReadOnlyList<LightSourceDto>? ProjectLightSources(EntityManager em)
    {
        var sourceEntities = em.Query<LightSourceTag>()
            .Where(e => e.Has<LightSourceComponent>())
            .OrderBy(e => e.Id)
            .ToList();

        if (sourceEntities.Count == 0) return null;

        var result = new List<LightSourceDto>(sourceEntities.Count);
        foreach (var e in sourceEntities)
        {
            var s = e.Get<LightSourceComponent>();
            result.Add(new LightSourceDto
            {
                Id                = s.Id,
                Kind              = (ContractLightKind)(int)s.Kind,
                State             = (ContractLightState)(int)s.State,
                Intensity         = s.Intensity,
                ColorTemperatureK = s.ColorTemperatureK,
                Position          = new TilePointDto { X = s.TileX, Y = s.TileY },
                RoomId            = s.RoomId,
            });
        }
        return result;
    }

    // ── Light apertures ───────────────────────────────────────────────────────

    private static IReadOnlyList<LightApertureDto>? ProjectLightApertures(EntityManager em)
    {
        var apertureEntities = em.Query<LightApertureTag>()
            .Where(e => e.Has<LightApertureComponent>())
            .OrderBy(e => e.Id)
            .ToList();

        if (apertureEntities.Count == 0) return null;

        var result = new List<LightApertureDto>(apertureEntities.Count);
        foreach (var e in apertureEntities)
        {
            var a = e.Get<LightApertureComponent>();
            result.Add(new LightApertureDto
            {
                Id          = a.Id,
                Position    = new TilePointDto { X = a.TileX, Y = a.TileY },
                RoomId      = a.RoomId,
                Facing      = (ContractApertureFacing)(int)a.Facing,
                AreaSqTiles = a.AreaSqTiles,
            });
        }
        return result;
    }

    // ── Entities ──────────────────────────────────────────────────────────────

    private static List<EntityStateDto> ProjectEntities(
        IReadOnlyList<EntitySnapshot> snapshots,
        EntityManager?                em)
    {
        // Build an ID→Entity lookup so SpeciesClassifier can resolve tags.
        Dictionary<Guid, Entity>? byId = null;
        if (em is not null)
        {
            byId = new Dictionary<Guid, Entity>(snapshots.Count);
            foreach (var e in em.GetAllEntities())
                byId[e.Id] = e;
        }

        var result = new List<EntityStateDto>(snapshots.Count);
        foreach (var s in snapshots)
        {
            SpeciesType species = SpeciesType.Unknown;
            Entity?     raw     = null;
            if (byId is not null && byId.TryGetValue(s.Id, out var found))
            {
                raw     = found;
                species = SpeciesClassifier.Classify(found);
            }

            result.Add(new EntityStateDto
            {
                Id       = s.Id.ToString(),
                ShortId  = s.ShortId,
                Name     = s.Name,
                Species  = species,
                Position = new PositionStateDto
                {
                    X           = s.PosX,
                    Y           = s.PosY,
                    Z           = s.PosZ,
                    HasPosition = s.HasPosition,
                    IsMoving    = s.IsMoving,
                    MoveTarget  = string.IsNullOrEmpty(s.MoveTarget) ? null : s.MoveTarget,
                },
                Drives = new DrivesStateDto
                {
                    Dominant        = MapDominant(s.Dominant),
                    EatUrgency      = s.EatUrgency,
                    DrinkUrgency    = s.DrinkUrgency,
                    SleepUrgency    = s.SleepUrgency,
                    DefecateUrgency = s.DefecateUrgency,
                    PeeUrgency      = s.PeeUrgency,
                },
                Physiology = new PhysiologyStateDto
                {
                    Satiation   = s.Satiation,
                    Hydration   = s.Hydration,
                    BodyTemp    = s.BodyTemp,
                    Energy      = s.Energy,
                    Sleepiness  = s.Sleepiness,
                    IsSleeping  = s.IsSleeping,
                    SiFill      = s.SiFill,
                    LiFill      = s.LiFill,
                    ColonFill   = s.ColonFill,
                    BladderFill = s.BladderFill,
                },
                Social = (raw is not null && raw.Has<NpcTag>()) ? ProjectSocial(raw) : null,
            });
        }
        return result;
    }

    // ── Social state ─────────────────────────────────────────────────────────

    private static SocialStateDto ProjectSocial(Entity e)
    {
        DrivesDto? drivesDto = null;
        if (e.Has<SocialDrivesComponent>())
        {
            var d = e.Get<SocialDrivesComponent>();
            drivesDto = new DrivesDto
            {
                Belonging  = MapDrive(d.Belonging),
                Status     = MapDrive(d.Status),
                Affection  = MapDrive(d.Affection),
                Irritation = MapDrive(d.Irritation),
                Attraction = MapDrive(d.Attraction),
                Trust      = MapDrive(d.Trust),
                Suspicion  = MapDrive(d.Suspicion),
                Loneliness = MapDrive(d.Loneliness),
            };
        }

        WillpowerDto? willpowerDto = null;
        if (e.Has<WillpowerComponent>())
        {
            var w = e.Get<WillpowerComponent>();
            willpowerDto = new WillpowerDto
            {
                Current  = SocialDrivesComponent.Clamp0100(w.Current),
                Baseline = SocialDrivesComponent.Clamp0100(w.Baseline),
            };
        }

        List<PersonalityTraitDto>? traitsDto    = null;
        string?                    currentMood  = null;
        ContractVocabRegister?     vocabReg     = null;
        if (e.Has<PersonalityComponent>())
        {
            var p = e.Get<PersonalityComponent>();
            traitsDto = new List<PersonalityTraitDto>
            {
                new() { Dimension = ContractBigFiveDimension.Openness,          Value = p.Openness },
                new() { Dimension = ContractBigFiveDimension.Conscientiousness,  Value = p.Conscientiousness },
                new() { Dimension = ContractBigFiveDimension.Extraversion,       Value = p.Extraversion },
                new() { Dimension = ContractBigFiveDimension.Agreeableness,      Value = p.Agreeableness },
                new() { Dimension = ContractBigFiveDimension.Neuroticism,        Value = p.Neuroticism },
            };
            currentMood = string.IsNullOrEmpty(p.CurrentMood) ? null : p.CurrentMood;
            vocabReg    = (ContractVocabRegister)(int)p.VocabularyRegister;
        }

        IReadOnlyList<InhibitionDto>? inhibitionsDto = null;
        if (e.Has<InhibitionsComponent>())
        {
            var inh  = e.Get<InhibitionsComponent>();
            var list = new List<InhibitionDto>(inh.Inhibitions.Count);
            foreach (var i in inh.Inhibitions)
            {
                list.Add(new InhibitionDto
                {
                    Class     = (ContractInhibitionClass)(int)i.Class,
                    Strength  = i.Strength,
                    Awareness = (ContractInhibitionAwareness)(int)i.Awareness,
                });
            }
            inhibitionsDto = list.Count > 0 ? list : null;
        }

        return new SocialStateDto
        {
            Drives             = drivesDto,
            Willpower          = willpowerDto,
            PersonalityTraits  = traitsDto,
            CurrentMood        = currentMood,
            VocabularyRegister = vocabReg,
            Inhibitions        = inhibitionsDto,
        };
    }

    private static DriveValueDto MapDrive(DriveValue d) => new()
    {
        Current  = SocialDrivesComponent.Clamp0100(d.Current),
        Baseline = SocialDrivesComponent.Clamp0100(d.Baseline),
    };

    // ── Relationships ─────────────────────────────────────────────────────────

    private static List<RelationshipDto>? ProjectRelationships(EntityManager em)
    {
        var relEntities = em.Query<RelationshipTag>()
            .Where(e => e.Has<RelationshipComponent>())
            .OrderBy(e => e.Id)
            .ToList();

        if (relEntities.Count == 0) return null;

        var result = new List<RelationshipDto>(relEntities.Count);
        foreach (var e in relEntities)
        {
            var r = e.Get<RelationshipComponent>();

            var patterns = new List<ContractRelationshipPattern>(r.Patterns.Count);
            foreach (var p in r.Patterns)
                patterns.Add((ContractRelationshipPattern)(int)p);

            result.Add(new RelationshipDto
            {
                Id              = e.Id.ToString(),
                ParticipantA    = ParticipantIntIdToGuidString(r.ParticipantA),
                ParticipantB    = ParticipantIntIdToGuidString(r.ParticipantB),
                Patterns        = patterns,
                Intensity       = r.Intensity,
                HistoryEventIds = Array.Empty<string>(),
            });
        }
        return result;
    }

    // EntityManager encodes entity counter N as a Guid: bytes[0..3] = N little-endian,
    // bytes[4..15] = 0. RelationshipComponent.ParticipantA/B stores that counter as int.
    private static string ParticipantIntIdToGuidString(int id)
    {
        var bytes = new byte[16];
        bytes[0] = (byte)( id        & 0xFF);
        bytes[1] = (byte)((id >>  8) & 0xFF);
        bytes[2] = (byte)((id >> 16) & 0xFF);
        bytes[3] = (byte)((id >> 24) & 0xFF);
        return new Guid(bytes).ToString();
    }

    // ── World items ───────────────────────────────────────────────────────────

    private static List<WorldItemDto> ProjectWorldItems(IReadOnlyList<WorldItemSnapshot> items)
    {
        var result = new List<WorldItemDto>(items.Count);
        foreach (var s in items)
        {
            result.Add(new WorldItemDto
            {
                Id       = s.Id.ToString(),
                Label    = s.Label,
                RotLevel = s.RotLevel,
                IsRotten = s.IsRotten,
            });
        }
        return result;
    }

    // ── World objects ─────────────────────────────────────────────────────────

    private static List<WorldObjectDto> ProjectWorldObjects(IReadOnlyList<WorldObjectSnapshot> objects)
    {
        var result = new List<WorldObjectDto>(objects.Count);
        foreach (var s in objects)
        {
            result.Add(new WorldObjectDto
            {
                Id         = s.Id.ToString(),
                Name       = s.Name,
                Kind       = WorldObjectKindClassifier.Classify(s),
                X          = s.X,
                Y          = s.Y,
                Z          = s.Z,
                StockCount = s.StockCount >= 0 ? s.StockCount : null,
            });
        }
        return result;
    }

    // ── Transit items ─────────────────────────────────────────────────────────

    private static List<TransitItemDto>? ProjectTransitItems(IReadOnlyList<TransitItemSnapshot> items)
    {
        if (items.Count == 0) return null;

        var result = new List<TransitItemDto>(items.Count);
        foreach (var s in items)
        {
            result.Add(new TransitItemDto
            {
                Id             = s.Id.ToString(),
                TargetEntityId = s.TargetEntityId.ToString(),
                ContentLabel   = s.ContentLabel,
                Progress       = s.Progress,
            });
        }
        return result;
    }

    // ── Enum mapping ──────────────────────────────────────────────────────────

    private static DominantDrive MapDominant(DesireType d) => d switch
    {
        DesireType.None      => DominantDrive.None,
        DesireType.Eat       => DominantDrive.Eat,
        DesireType.Drink     => DominantDrive.Drink,
        DesireType.Sleep     => DominantDrive.Sleep,
        DesireType.Defecate  => DominantDrive.Defecate,
        DesireType.Pee       => DominantDrive.Pee,
        _                    => DominantDrive.None,
    };
}
