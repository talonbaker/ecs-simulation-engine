using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using Warden.Contracts.Telemetry;

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
    /// When supplied, <see cref="SpeciesClassifier"/> uses tag-based classification.
    /// </param>
    /// <param name="capturedAt">Wall-clock moment this snapshot was taken (caller-owned).</param>
    /// <param name="tick">Simulation tick counter at capture time.</param>
    /// <param name="seed">RNG seed the simulation was booted with.</param>
    /// <param name="simVersion">Value of <c>SimVersion.Full</c> at capture time.</param>
    public static WorldStateDto Project(
        SimulationSnapshot snap,
        EntityManager?     entityManager,
        DateTimeOffset     capturedAt,
        long               tick,
        int                seed,
        string             simVersion)
    {
        return new WorldStateDto
        {
            SchemaVersion = "0.1.0",
            CapturedAt    = capturedAt,
            Tick          = (int)tick,
            Seed          = seed,
            SimVersion    = simVersion,
            Clock         = ProjectClock(snap.Clock),
            Entities      = ProjectEntities(snap.LivingEntities, entityManager),
            WorldItems    = ProjectWorldItems(snap.WorldItems),
            WorldObjects  = ProjectWorldObjects(snap.WorldObjects),
            TransitItems  = ProjectTransitItems(snap.TransitItems),
            Invariants    = new InvariantDigestDto
            {
                ViolationCount   = snap.ViolationCount,
                RecentViolations = null,   // SimulationSnapshot carries only the count
            },
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

    private static ClockStateDto ProjectClock(ClockSnapshot c) => new()
    {
        GameTimeDisplay = c.TimeDisplay,
        DayNumber       = c.DayNumber,
        IsDaytime       = c.IsDaytime,
        CircadianFactor = c.CircadianFactor,
        TimeScale       = c.TimeScale,
    };

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
            if (byId is not null && byId.TryGetValue(s.Id, out var raw))
                species = SpeciesClassifier.Classify(raw);

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
            });
        }
        return result;
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
