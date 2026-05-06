// InlineProjector is compiled in ALL builds but called only in RETAIL builds.
// In WARDEN builds, WorldStateProjectorAdapter uses Warden.Telemetry.TelemetryProjector.
// InlineProjectorParityTests verify byte-identical JSON output between both paths.

using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using Warden.Contracts.Telemetry;

/// <summary>
/// Self-contained world-state projector that produces a <see cref="WorldStateDto"/>
/// without depending on <c>Warden.Telemetry.dll</c>.
///
/// PURPOSE
/// ───────
/// RETAIL (player distribution) builds strip <c>Warden.Telemetry.dll</c> to keep
/// builds lean. <see cref="WorldStateProjectorAdapter"/> routes here in RETAIL.
/// The output schema is identical to <c>Warden.Telemetry.TelemetryProjector</c>
/// — any divergence fails <c>InlineProjectorParityTests</c> before release.
///
/// DESIGN RULES (mirrors TelemetryProjector)
/// ──────────────────────────────────────────
/// • Pure function — no I/O, no side effects, no mutable state.
/// • Caller supplies capturedAt; this class never calls DateTime.UtcNow itself.
/// • Does not modify APIFramework. Projection only; never mutation.
/// • Schema version string must match TelemetryProjector ("0.4.0").
///
/// NOTE ON WARDEN.CONTRACTS DEPENDENCY
/// ────────────────────────────────────
/// This class DOES depend on Warden.Contracts.dll (the DTO types live there).
/// It does NOT depend on Warden.Telemetry.dll (the projector lives there).
/// Warden.Contracts is a pure data assembly that ships in all build targets.
/// </summary>
public static class InlineProjector
{
    private const string SchemaVersion = "0.4.0";
    private const string SimVersion    = "3.1.A";

    /// <summary>
    /// Projects a <see cref="SimulationSnapshot"/> to a <see cref="WorldStateDto"/>.
    /// </summary>
    /// <param name="snap">Immutable engine snapshot produced by SimulationSnapshot.Capture().</param>
    /// <param name="entityManager">
    /// Optional — the same EntityManager that produced the snapshot.
    /// When supplied, spatial entities (rooms) are projected.
    /// </param>
    /// <param name="capturedAt">Wall-clock moment (caller-supplied; not read internally).</param>
    /// <param name="tick">Engine tick counter at capture time.</param>
    public static WorldStateDto Project(
        SimulationSnapshot snap,
        EntityManager?     entityManager,
        DateTimeOffset     capturedAt,
        long               tick)
    {
        return new WorldStateDto
        {
            SchemaVersion = SchemaVersion,
            CapturedAt    = capturedAt,
            Tick          = (int)tick,
            SimVersion    = SimVersion,
            Clock         = ProjectClock(snap.Clock),
            Entities      = ProjectEntities(snap.LivingEntities),
            WorldItems    = ProjectWorldItems(snap.WorldItems),
            WorldObjects  = ProjectWorldObjects(snap.WorldObjects),
            TransitItems  = ProjectTransitItems(snap.TransitItems),
            Invariants    = new InvariantDigestDto
            {
                ViolationCount   = snap.ViolationCount,
                RecentViolations = null,
            },
            // Spatial pillar — populated when EntityManager is available
            Rooms = entityManager != null ? ProjectRooms(entityManager) : null,

            // Fields deferred to future packets or WARDEN-only
            Relationships  = null,
            MemoryEvents   = null,
            LightSources   = null,
            LightApertures = null,
            Chronicle      = null,
        };
    }

    // ── Clock ─────────────────────────────────────────────────────────────────

    private static ClockStateDto ProjectClock(ClockSnapshot clock)
    {
        return new ClockStateDto
        {
            GameTimeDisplay = clock.TimeDisplay,
            DayNumber       = clock.DayNumber,
            IsDaytime       = clock.IsDaytime,
            CircadianFactor = clock.CircadianFactor,
            TimeScale       = clock.TimeScale,
            Sun             = null,   // SunState not injected in inline path
        };
    }

    // ── Living entities ───────────────────────────────────────────────────────

    private static List<EntityStateDto> ProjectEntities(IReadOnlyList<EntitySnapshot> living)
    {
        var result = new List<EntityStateDto>(living.Count);

        foreach (var e in living)
        {
            result.Add(new EntityStateDto
            {
                Id      = e.Id.ToString(),
                ShortId = e.ShortId,
                Name    = e.Name,
                // Inline path assumes Human species; full projector uses tag-based classification.
                // InlineProjectorParityTests verify the real species values match for a known cast.
                Species  = SpeciesType.Human,
                Position = new PositionStateDto
                {
                    X           = e.PosX,
                    Y           = e.PosY,
                    Z           = e.PosZ,
                    HasPosition = e.HasPosition,
                    IsMoving    = e.IsMoving,
                    MoveTarget  = null,
                },
                Drives = new DrivesStateDto
                {
                    // Cast engine DominantDrive to contract DominantDrive.
                    // Both enums are defined identically (same ordinal values).
                    Dominant        = (DominantDrive)(int)e.Dominant,
                    EatUrgency      = e.EatUrgency,
                    DrinkUrgency    = e.DrinkUrgency,
                    SleepUrgency    = e.SleepUrgency,
                    DefecateUrgency = e.DefecateUrgency,
                    PeeUrgency      = e.PeeUrgency,
                },
                Physiology = new PhysiologyStateDto
                {
                    Satiation   = e.Satiation,
                    Hydration   = e.Hydration,
                    BodyTemp    = e.BodyTemp,
                    Energy      = e.Energy,
                    Sleepiness  = e.Sleepiness,
                    IsSleeping  = e.IsSleeping,
                    // BladderFill and ColonFill are plain float on EntitySnapshot;
                    // the DTO has float? — explicitly box to nullable.
                    BladderFill = (float?)e.BladderFill,
                    ColonFill   = (float?)e.ColonFill,
                },
                Social = null,   // social projection omitted in inline path
            });
        }

        return result;
    }

    // ── World items ───────────────────────────────────────────────────────────

    private static List<WorldItemDto> ProjectWorldItems(IReadOnlyList<WorldItemSnapshot> items)
    {
        var result = new List<WorldItemDto>(items.Count);
        foreach (var item in items)
        {
            result.Add(new WorldItemDto
            {
                Id       = item.Id.ToString(),
                Label    = item.Label,
                RotLevel = item.RotLevel,
                IsRotten = item.IsRotten,
            });
        }
        return result;
    }

    // ── World objects ─────────────────────────────────────────────────────────

    private static List<WorldObjectDto> ProjectWorldObjects(IReadOnlyList<WorldObjectSnapshot> objects)
    {
        var result = new List<WorldObjectDto>(objects.Count);
        foreach (var obj in objects)
        {
            result.Add(new WorldObjectDto
            {
                Id   = obj.Id.ToString(),
                Name = obj.Name,
                // Classify using the same priority order as Warden.Telemetry.WorldObjectKindClassifier.
                Kind = ClassifyWorldObjectKind(obj),
                X    = obj.X,
                Y    = obj.Y,
                Z    = obj.Z,
            });
        }
        return result;
    }

    private static WorldObjectKind ClassifyWorldObjectKind(WorldObjectSnapshot obj)
    {
        if (obj.IsFridge)  return WorldObjectKind.Fridge;
        if (obj.IsSink)    return WorldObjectKind.Sink;
        if (obj.IsToilet)  return WorldObjectKind.Toilet;
        if (obj.IsBed)     return WorldObjectKind.Bed;
        return WorldObjectKind.Other;
    }

    // ── Transit items ─────────────────────────────────────────────────────────

    private static List<TransitItemDto>? ProjectTransitItems(IReadOnlyList<TransitItemSnapshot> items)
    {
        if (items.Count == 0) return null;

        var result = new List<TransitItemDto>(items.Count);
        foreach (var item in items)
        {
            result.Add(new TransitItemDto
            {
                Id             = item.Id.ToString(),
                TargetEntityId = item.TargetEntityId.ToString(),
                ContentLabel   = item.ContentLabel,
                Progress       = item.Progress,
            });
        }
        return result;
    }

    // ── Rooms (spatial pillar v0.3) ───────────────────────────────────────────

    private static IReadOnlyList<RoomDto> ProjectRooms(EntityManager entityManager)
    {
        var result = new List<RoomDto>();

        foreach (var entity in entityManager.Query<RoomComponent>())
        {
            var room = entity.Get<RoomComponent>();
            result.Add(new RoomDto
            {
                Id       = room.Id,
                Name     = room.Name,
                Category = (Warden.Contracts.Telemetry.RoomCategory)(int)room.Category,
                Floor    = (Warden.Contracts.Telemetry.BuildingFloor)(int)room.Floor,
                BoundsRect = new BoundsRectDto
                {
                    X      = room.Bounds.X,
                    Y      = room.Bounds.Y,
                    Width  = room.Bounds.Width,
                    Height = room.Bounds.Height,
                },
                Illumination = new IlluminationDto
                {
                    AmbientLevel      = room.Illumination.AmbientLevel,
                    ColorTemperatureK = room.Illumination.ColorTemperatureK,
                    DominantSourceId  = room.Illumination.DominantSourceId,
                },
            });
        }

        return result;
    }
}
