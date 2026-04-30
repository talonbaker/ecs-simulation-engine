using APIFramework.Components;
using System;
using System.Collections.Generic;
using System.Linq;

namespace APIFramework.Core;

/// <summary>
/// An immutable, read-per-frame picture of everything a frontend needs from the
/// simulation engine.
///
/// WHY THIS EXISTS
/// ───────────────
/// Currently the Avalonia GUI reaches directly into SimulationBootstrapper fields:
///   _sim.Clock.CircadianFactor, _sim.EntityManager.Query<T>(), _sim.Invariants, etc.
///
/// This couples every frontend to the internal class structure of the engine.
/// When Unity, a web server, or a replay system needs the same data they would all
/// duplicate the same "reach in and grab" pattern — and each one must change if the
/// engine's internals change.
///
/// SimulationSnapshot breaks that coupling:
///   • The engine PRODUCES a snapshot once per frame (cheap — one allocation)
///   • Frontends READ only from the snapshot (no engine internals visible)
///   • The snapshot type can evolve independently of both engine and frontend
///
/// IMMUTABILITY
/// ────────────
/// All collections are read-only; all value types are copies. The snapshot is a
/// point-in-time view — it cannot be used to mutate the simulation.
///
/// USAGE
/// ─────
///   // In the game loop (UI thread, Unity Update(), CLI loop, etc.):
///   var snap = sim.Capture();
///   // render snap.Clock.TimeDisplay, snap.Entities, snap.ViolationCount, etc.
/// </summary>
public sealed class SimulationSnapshot
{
    // ── Clock ─────────────────────────────────────────────────────────────────
    public ClockSnapshot        Clock          { get; init; } = default!;

    // ── Entity summaries ─────────────────────────────────────────────────────
    /// <summary>All living entities (those with MetabolismComponent).</summary>
    public IReadOnlyList<EntitySnapshot>      LivingEntities  { get; init; } = Array.Empty<EntitySnapshot>();

    /// <summary>Food and liquid entities currently sitting in the world (not in transit).</summary>
    public IReadOnlyList<WorldItemSnapshot>   WorldItems      { get; init; } = Array.Empty<WorldItemSnapshot>();

    /// <summary>Items currently traveling through the esophagus pipeline.</summary>
    public IReadOnlyList<TransitItemSnapshot> TransitItems    { get; init; } = Array.Empty<TransitItemSnapshot>();

    /// <summary>Fixed world objects (fridge, sink, toilet, bed) and their positions.</summary>
    public IReadOnlyList<WorldObjectSnapshot> WorldObjects    { get; init; } = Array.Empty<WorldObjectSnapshot>();

    // ── Invariant health ─────────────────────────────────────────────────────
    /// <summary>Total invariant violations recorded since simulation start.</summary>
    public int ViolationCount { get; init; }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Captures the current engine state into a new snapshot.
    /// Call once per frame from the main simulation loop, then hand the snapshot
    /// to every frontend system that needs to render or log it.
    /// </summary>
    public static SimulationSnapshot Capture(SimulationBootstrapper sim)
    {
        var clock = sim.Clock;
        var em    = sim.EntityManager;

        // ── Clock ─────────────────────────────────────────────────────────────
        var clockSnap = new ClockSnapshot(
            TimeDisplay:     clock.GameTimeDisplay,
            DayNumber:       clock.DayNumber,
            IsDaytime:       clock.IsDaytime,
            CircadianFactor: clock.CircadianFactor,
            TimeScale:       clock.TimeScale
        );

        // ── Living entities ───────────────────────────────────────────────────
        // Query NpcTag so cast-generator NPCs (which have no MetabolismComponent) are included.
        var living = new List<EntitySnapshot>();
        foreach (var e in em.Query<NpcTag>())
        {
            var meta   = e.Has<MetabolismComponent>() ? e.Get<MetabolismComponent>() : default;
            var drives = e.Has<DriveComponent>()          ? e.Get<DriveComponent>()          : default;
            var energy = e.Has<EnergyComponent>()         ? e.Get<EnergyComponent>()         : default;
            var si      = e.Has<SmallIntestineComponent>() ? e.Get<SmallIntestineComponent>() : default;
            var li      = e.Has<LargeIntestineComponent>() ? e.Get<LargeIntestineComponent>() : default;
            var colon   = e.Has<ColonComponent>()           ? e.Get<ColonComponent>()           : default;
            var bladder = e.Has<BladderComponent>()         ? e.Get<BladderComponent>()         : default;
            var pos     = e.Has<PositionComponent>()        ? e.Get<PositionComponent>()        : default;
            var mvtTgt  = e.Has<MovementTargetComponent>()  ? e.Get<MovementTargetComponent>()  : default;

            living.Add(new EntitySnapshot(
                Id:         e.Id,
                ShortId:    e.ShortId,
                Name:       e.Has<IdentityComponent>() ? e.Get<IdentityComponent>().Name : e.ShortId,
                Satiation:  meta.Satiation,
                Hydration:  meta.Hydration,
                BodyTemp:   meta.BodyTemp,
                Energy:     energy.Energy,
                Sleepiness: energy.Sleepiness,
                IsSleeping: energy.IsSleeping,
                Dominant:        drives.Dominant,
                EatUrgency:      drives.EatUrgency,
                DrinkUrgency:    drives.DrinkUrgency,
                SleepUrgency:    drives.SleepUrgency,
                DefecateUrgency: drives.DefecateUrgency,
                PeeUrgency:      drives.PeeUrgency,
                SiFill:          si.Fill,
                LiFill:          li.Fill,
                ColonFill:       colon.Fill,
                ColonHasUrge:    colon.HasUrge,
                ColonIsCritical: colon.IsCritical,
                BladderFill:        bladder.Fill,
                BladderHasUrge:     bladder.HasUrge,
                BladderIsCritical:  bladder.IsCritical,
                // ── Spatial (v0.8+) ───────────────────────────────────────────
                PosX:        pos.X,
                PosY:        pos.Y,
                PosZ:        pos.Z,
                HasPosition: e.Has<PositionComponent>(),
                IsMoving:    e.Has<MovementTargetComponent>(),
                MoveTarget:  mvtTgt.Label ?? string.Empty
            ));
        }

        // ── Transit items ─────────────────────────────────────────────────────
        var transit = new List<TransitItemSnapshot>();
        foreach (var e in em.Query<EsophagusTransitComponent>())
        {
            var t    = e.Get<EsophagusTransitComponent>();
            string label = e.Has<LiquidComponent>()
                ? e.Get<LiquidComponent>().LiquidType
                : e.Has<BolusComponent>()
                    ? e.Get<BolusComponent>().FoodType
                    : "Unknown";

            transit.Add(new TransitItemSnapshot(
                Id:             e.Id,
                TargetEntityId: t.TargetEntityId,
                ContentLabel:   label,
                Progress:       t.Progress
            ));
        }

        // ── World items ───────────────────────────────────────────────────────
        var transitIds = new HashSet<Guid>(
            em.Query<EsophagusTransitComponent>().Select(e => e.Id));

        var worldItems = new List<WorldItemSnapshot>();
        foreach (var e in em.Query<BolusComponent>().Concat(em.Query<LiquidComponent>())
                             .Where(e => !transitIds.Contains(e.Id))
                             .Distinct())
        {
            string label   = e.Has<BolusComponent>()  ? e.Get<BolusComponent>().FoodType
                           : e.Has<LiquidComponent>() ? e.Get<LiquidComponent>().LiquidType
                           : "Item";
            float  rotLevel = e.Has<RotComponent>() ? e.Get<RotComponent>().RotLevel : 0f;
            bool   isRotten = e.Has<RotTag>();

            worldItems.Add(new WorldItemSnapshot(
                Id:       e.Id,
                Label:    label,
                RotLevel: rotLevel,
                IsRotten: isRotten
            ));
        }

        // ── World objects (fridge, sink, toilet, bed) ────────────────────────
        var worldObjects = new List<WorldObjectSnapshot>();
        foreach (var e in em.GetAllEntities()
                             .Where(e => e.Has<FridgeComponent>()  ||
                                         e.Has<SinkComponent>()    ||
                                         e.Has<ToiletComponent>()  ||
                                         e.Has<BedComponent>()))
        {
            if (!e.Has<PositionComponent>()) continue;
            var p  = e.Get<PositionComponent>();
            string nm = e.Has<IdentityComponent>() ? e.Get<IdentityComponent>().Name : e.ShortId;
            // FridgeComponent now carries FoodCount directly.
            // ContainerComponent is a fallback for any other container-type world object.
            int stock = e.Has<FridgeComponent>()    ? e.Get<FridgeComponent>().FoodCount
                      : e.Has<ContainerComponent>() ? e.Get<ContainerComponent>().Count
                      : -1;
            worldObjects.Add(new WorldObjectSnapshot(
                Id:        e.Id,
                Name:      nm,
                X: p.X, Y: p.Y, Z: p.Z,
                IsFridge:   e.Has<FridgeComponent>(),
                IsSink:     e.Has<SinkComponent>(),
                IsToilet:   e.Has<ToiletComponent>(),
                IsBed:      e.Has<BedComponent>(),
                StockCount: stock
            ));
        }

        return new SimulationSnapshot
        {
            Clock          = clockSnap,
            LivingEntities = living,
            TransitItems   = transit,
            WorldItems     = worldItems,
            WorldObjects   = worldObjects,
            ViolationCount = sim.Invariants.Violations.Count,
        };
    }
}

// ── Nested snapshot records ───────────────────────────────────────────────────
// Records because they are pure data bags with value semantics — no behaviour.

public sealed record ClockSnapshot(
    string TimeDisplay,
    int    DayNumber,
    bool   IsDaytime,
    float  CircadianFactor,
    float  TimeScale
);

public sealed record EntitySnapshot(
    Guid        Id,
    string      ShortId,
    string      Name,
    float       Satiation,
    float       Hydration,
    float       BodyTemp,
    float       Energy,
    float       Sleepiness,
    bool        IsSleeping,
    DesireType  Dominant,
    float       EatUrgency,
    float       DrinkUrgency,
    float       SleepUrgency,
    // ── Elimination drives (v0.7.3+) ───────────────────────────────────────
    float       DefecateUrgency,
    float       PeeUrgency,
    // ── GI pipeline fills ──────────────────────────────────────────────────
    float       SiFill,             // SmallIntestine fill 0–1
    float       LiFill,             // LargeIntestine fill 0–1
    float       ColonFill,          // Colon fill 0–1 (relative to CapacityMl)
    bool        ColonHasUrge,
    bool        ColonIsCritical,
    // ── Bladder (v0.7.4+) ─────────────────────────────────────────────────
    float       BladderFill,        // 0–1 relative to CapacityMl
    bool        BladderHasUrge,
    bool        BladderIsCritical,
    // ── Spatial (v0.8+) ───────────────────────────────────────────────────
    float       PosX,
    float       PosY,
    float       PosZ,
    bool        HasPosition,        // false for entities without PositionComponent
    bool        IsMoving,           // true while MovementTargetComponent is present
    string      MoveTarget          // label of current destination ("Fridge", "Bed", etc.)
);

public sealed record TransitItemSnapshot(
    Guid   Id,
    Guid   TargetEntityId,
    string ContentLabel,
    float  Progress
);

public sealed record WorldItemSnapshot(
    Guid   Id,
    string Label,
    float  RotLevel,
    bool   IsRotten
);

public sealed record WorldObjectSnapshot(
    Guid   Id,
    string Name,
    float  X,
    float  Y,
    float  Z,
    bool   IsFridge,
    bool   IsSink,
    bool   IsToilet,
    bool   IsBed,
    int    StockCount  // -1 if no ContainerComponent; banana count for fridge
);
