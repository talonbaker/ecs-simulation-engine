using System;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Diagnostics;
using APIFramework.Systems.Chronicle;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Guards the simulation against impossible component states.
///
/// Runs FIRST in the pipeline so violations from the previous tick are caught
/// and clamped before any system reads bad data this tick.
///
/// For every known component on every entity it:
///   1. Checks whether each value is within its documented valid range.
///   2. Clamps the value back into range so the simulation continues.
///   3. Records an InvariantViolation for later inspection / reporting.
///
/// INTERPRETING VIOLATIONS
/// -----------------------
///   Single violation, once only  → transient floating-point rounding, usually fine
///   Same violation every tick    → a system is producing bad values continuously
///   Resource stuck at 0 for ages → entity in terminal state; feedback loop broken
///   Resource never drops below X → action threshold is set too low; entity never experiences need
/// </summary>
/// <remarks>
/// Reads: every component listed in the per-component checks
/// (<see cref="LifeStateComponent"/>, <see cref="MetabolismComponent"/>,
/// <see cref="EnergyComponent"/>, <see cref="StomachComponent"/>,
/// <see cref="SmallIntestineComponent"/>, <see cref="LargeIntestineComponent"/>,
/// <see cref="ColonComponent"/>, <see cref="BladderComponent"/>,
/// <see cref="DriveComponent"/>, <see cref="EsophagusTransitComponent"/>,
/// <see cref="StainComponent"/>, <see cref="BrokenItemComponent"/>).<br/>
/// Writes: clamps any out-of-range value back into range on the offending component
/// and appends an <see cref="APIFramework.Diagnostics.InvariantViolation"/> to its
/// internal list.<br/>
/// Phase: PreUpdate (always first) — guards against stale bad state from the
/// previous tick before any system reads it this tick.
/// </remarks>
public class InvariantSystem : ISystem
{
    private readonly SimulationClock          _clock;
    private readonly ChronicleService?        _chronicle;
    private readonly List<InvariantViolation> _violations = new();

    /// <summary>Constructs the invariant guard with the simulation clock and an optional chronicle service.</summary>
    /// <param name="clock">Simulation clock; supplies the GameTime stamp on every recorded violation.</param>
    /// <param name="chronicle">Optional chronicle service; when supplied, the chronicle ↔ entity-tree integrity check runs.</param>
    public InvariantSystem(SimulationClock clock, ChronicleService? chronicle = null)
    {
        _clock     = clock;
        _chronicle = chronicle;
    }

    /// <summary>All violations detected since the simulation started.</summary>
    public IReadOnlyList<InvariantViolation> Violations => _violations;

    /// <summary>Violations added since the last call to FlushNewViolations().</summary>
    private int _lastFlushed = 0;

    /// <summary>
    /// Returns violations added since the last flush and advances the flush cursor.
    /// Use this in the CLI main loop to print only newly-detected violations.
    /// </summary>
    public IEnumerable<InvariantViolation> FlushNewViolations()
    {
        var batch = _violations.Skip(_lastFlushed).ToList();
        _lastFlushed = _violations.Count;
        return batch;
    }

    /// <summary>Per-tick invariant pass; checks every entity's components and clamps offenders.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds, unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        double gameTime = _clock.TotalTime;

        foreach (var entity in em.GetAllEntities())
        {
            string name = entity.Has<IdentityComponent>()
                ? entity.Get<IdentityComponent>().Name
                : entity.ShortId;

            // WP-3.0.0: Deceased entities are checked against a reduced invariant set only.
            // Their physiology components are frozen by design — do not raise alerts for them.
            if (entity.Has<LifeStateComponent>() &&
                entity.Get<LifeStateComponent>().State == LifeState.Deceased)
            {
                CheckLifeStateComponent (entity, name, gameTime);
                CheckCauseOfDeath       (entity, name, gameTime);
                continue;
            }

            CheckLifeStateComponent (entity, name, gameTime);
            CheckMetabolism         (entity, name, gameTime);
            CheckEnergy             (entity, name, gameTime);
            CheckStomach            (entity, name, gameTime);
            CheckSmallIntestine     (entity, name, gameTime);
            CheckLargeIntestine     (entity, name, gameTime);
            CheckColon              (entity, name, gameTime);
            CheckBladder            (entity, name, gameTime);
            CheckDrives             (entity, name, gameTime);
            CheckTransit            (entity, name, gameTime);
        }

        if (_chronicle is not null)
            CheckChronicleIntegrity(em, gameTime);
    }

    // -- Per-component checks -------------------------------------------------

    private void CheckLifeStateComponent(Entity entity, string name, double t)
    {
        if (!entity.Has<LifeStateComponent>()) return;
        var ls = entity.Get<LifeStateComponent>();

        // State must be a defined enum value (0=Alive, 1=Incapacitated, 2=Deceased).
        int stateVal = (int)ls.State;
        if (stateVal < 0 || stateVal > 2)
        {
            _violations.Add(new InvariantViolation(
                GameTime:    t,
                EntityName:  name,
                Component:   "LifeStateComponent",
                Property:    "State",
                ActualValue: (float)stateVal,
                ClampedTo:   (float)Math.Clamp(stateVal, 0, 2),
                ValidMin:    0f,
                ValidMax:    2f));
        }

        // Deceased entities must have a CauseOfDeathComponent attached.
        if (ls.State == LifeState.Deceased && !entity.Has<CauseOfDeathComponent>())
        {
            _violations.Add(new InvariantViolation(
                GameTime:    t,
                EntityName:  name,
                Component:   "LifeStateComponent",
                Property:    "MissingCauseOfDeath",
                ActualValue: 0f,
                ClampedTo:   0f,
                ValidMin:    0f,
                ValidMax:    0f));
        }
    }

    private void CheckCauseOfDeath(Entity entity, string name, double t)
    {
        if (!entity.Has<CauseOfDeathComponent>()) return;
        var cod = entity.Get<CauseOfDeathComponent>();

        // Cause must be a defined CauseOfDeath enum value (0=Unknown … 3=StarvedAlone).
        int causeVal = (int)cod.Cause;
        if (causeVal < 0 || causeVal > 3)
        {
            _violations.Add(new InvariantViolation(
                GameTime:    t,
                EntityName:  name,
                Component:   "CauseOfDeathComponent",
                Property:    "Cause",
                ActualValue: (float)causeVal,
                ClampedTo:   (float)Math.Clamp(causeVal, 0, 3),
                ValidMin:    0f,
                ValidMax:    3f));
        }

        // DeathTick must be positive (zero means uninitialized).
        if (cod.DeathTick <= 0)
        {
            _violations.Add(new InvariantViolation(
                GameTime:    t,
                EntityName:  name,
                Component:   "CauseOfDeathComponent",
                Property:    "DeathTick",
                ActualValue: (float)cod.DeathTick,
                ClampedTo:   1f,
                ValidMin:    1f,
                ValidMax:    float.MaxValue));
        }
    }

    private void CheckMetabolism(Entity entity, string name, double t)
    {
        if (!entity.Has<MetabolismComponent>()) return;
        var m     = entity.Get<MetabolismComponent>();
        bool dirty = false;

        (m.Satiation, var v1) = Guard(m.Satiation, 0f, 100f, "MetabolismComponent", "Satiation", name, t);
        (m.Hydration, var v2) = Guard(m.Hydration, 0f, 100f, "MetabolismComponent", "Hydration", name, t);
        dirty = v1 | v2;

        // NutrientStores are cumulative and unbounded upward (future organ-systems
        // will drain them), so we only guard against floating-point negatives.
        var store = m.NutrientStores;
        bool storeDirty = GuardNonNegative(ref store, "MetabolismComponent.NutrientStores", name, t);
        if (storeDirty) { m.NutrientStores = store; dirty = true; }

        if (dirty) entity.Add(m);
    }

    private void CheckEnergy(Entity entity, string name, double t)
    {
        if (!entity.Has<EnergyComponent>()) return;
        var e     = entity.Get<EnergyComponent>();
        bool dirty = false;

        (e.Energy,     var v1) = Guard(e.Energy,     0f, 100f, "EnergyComponent", "Energy",     name, t);
        (e.Sleepiness, var v2) = Guard(e.Sleepiness, 0f, 100f, "EnergyComponent", "Sleepiness", name, t);
        dirty = v1 | v2;

        if (dirty) entity.Add(e);
    }

    private void CheckStomach(Entity entity, string name, double t)
    {
        if (!entity.Has<StomachComponent>()) return;
        var s     = entity.Get<StomachComponent>();
        bool dirty = false;

        (s.CurrentVolumeMl, var v1) = Guard(s.CurrentVolumeMl, 0f, StomachComponent.MaxVolumeMl,
                                            "StomachComponent", "CurrentVolumeMl", name, t);
        dirty = v1;

        // NutrientsQueued is a full profile. It can never legally contain negative
        // values — DigestionSystem's ratio-proportional release can drift one of
        // the fields slightly negative on the last tick before empty, but that's
        // the worst case we need to clamp.
        var queued = s.NutrientsQueued;
        bool queuedDirty = GuardNonNegative(ref queued, "StomachComponent.NutrientsQueued", name, t);
        if (queuedDirty) { s.NutrientsQueued = queued; dirty = true; }

        if (dirty) entity.Add(s);
    }

    /// <summary>
    /// Clamps every field of a NutrientProfile to [0, ∞). Returns true if any
    /// field was negative (violations are logged per-field).
    /// </summary>
    private bool GuardNonNegative(ref NutrientProfile p, string label, string entity, double t)
    {
        bool violated = false;
        (p.Carbohydrates, var v1)  = Guard(p.Carbohydrates, 0f, float.MaxValue, label, "Carbohydrates", entity, t);
        (p.Proteins,      var v2)  = Guard(p.Proteins,      0f, float.MaxValue, label, "Proteins",      entity, t);
        (p.Fats,           var v3) = Guard(p.Fats,           0f, float.MaxValue, label, "Fats",           entity, t);
        (p.Fiber,          var v4) = Guard(p.Fiber,          0f, float.MaxValue, label, "Fiber",          entity, t);
        (p.Water,          var v5) = Guard(p.Water,          0f, float.MaxValue, label, "Water",          entity, t);
        (p.VitaminA,       var v6) = Guard(p.VitaminA,       0f, float.MaxValue, label, "VitaminA",       entity, t);
        (p.VitaminB,       var v7) = Guard(p.VitaminB,       0f, float.MaxValue, label, "VitaminB",       entity, t);
        (p.VitaminC,       var v8) = Guard(p.VitaminC,       0f, float.MaxValue, label, "VitaminC",       entity, t);
        (p.VitaminD,       var v9) = Guard(p.VitaminD,       0f, float.MaxValue, label, "VitaminD",       entity, t);
        (p.VitaminE,       var v10)= Guard(p.VitaminE,       0f, float.MaxValue, label, "VitaminE",       entity, t);
        (p.VitaminK,       var v11)= Guard(p.VitaminK,       0f, float.MaxValue, label, "VitaminK",       entity, t);
        (p.Sodium,         var v12)= Guard(p.Sodium,         0f, float.MaxValue, label, "Sodium",         entity, t);
        (p.Potassium,      var v13)= Guard(p.Potassium,      0f, float.MaxValue, label, "Potassium",      entity, t);
        (p.Calcium,        var v14)= Guard(p.Calcium,        0f, float.MaxValue, label, "Calcium",        entity, t);
        (p.Iron,           var v15)= Guard(p.Iron,           0f, float.MaxValue, label, "Iron",           entity, t);
        (p.Magnesium,      var v16)= Guard(p.Magnesium,      0f, float.MaxValue, label, "Magnesium",      entity, t);
        violated = v1 | v2 | v3 | v4 | v5 | v6 | v7 | v8 | v9 | v10 | v11 | v12 | v13 | v14 | v15 | v16;
        return violated;
    }

    private void CheckSmallIntestine(Entity entity, string name, double t)
    {
        if (!entity.Has<SmallIntestineComponent>()) return;
        var si    = entity.Get<SmallIntestineComponent>();
        bool dirty = false;

        (si.ChymeVolumeMl, var v1) = Guard(si.ChymeVolumeMl, 0f, SmallIntestineComponent.MaxVolumeMl,
                                           "SmallIntestineComponent", "ChymeVolumeMl", name, t);
        dirty = v1;

        // Guard the tracked chyme profile against floating-point negatives.
        var chyme = si.Chyme;
        bool chymeDirty = GuardNonNegative(ref chyme, "SmallIntestineComponent.Chyme", name, t);
        if (chymeDirty) { si.Chyme = chyme; dirty = true; }

        if (dirty) entity.Add(si);
    }

    private void CheckLargeIntestine(Entity entity, string name, double t)
    {
        if (!entity.Has<LargeIntestineComponent>()) return;
        var li    = entity.Get<LargeIntestineComponent>();
        bool dirty = false;

        (li.ContentVolumeMl, var v1) = Guard(li.ContentVolumeMl, 0f, LargeIntestineComponent.MaxVolumeMl,
                                             "LargeIntestineComponent", "ContentVolumeMl", name, t);
        dirty = v1;

        if (dirty) entity.Add(li);
    }

    private void CheckColon(Entity entity, string name, double t)
    {
        if (!entity.Has<ColonComponent>()) return;
        var colon = entity.Get<ColonComponent>();
        bool dirty = false;

        // Use the entity's own CapacityMl as the upper bound — it varies by entity type.
        float cap = colon.CapacityMl > 0f ? colon.CapacityMl : 200f;
        (colon.StoolVolumeMl, var v1) = Guard(colon.StoolVolumeMl, 0f, cap,
                                              "ColonComponent", "StoolVolumeMl", name, t);
        dirty = v1;

        if (dirty) entity.Add(colon);
    }

    private void CheckBladder(Entity entity, string name, double t)
    {
        if (!entity.Has<BladderComponent>()) return;
        var bladder = entity.Get<BladderComponent>();
        bool dirty  = false;

        float cap = bladder.CapacityMl > 0f ? bladder.CapacityMl : 100f;
        (bladder.VolumeML, var v1) = Guard(bladder.VolumeML, 0f, cap,
                                           "BladderComponent", "VolumeML", name, t);
        dirty = v1;

        if (dirty) entity.Add(bladder);
    }

    private void CheckDrives(Entity entity, string name, double t)
    {
        if (!entity.Has<DriveComponent>()) return;
        var d     = entity.Get<DriveComponent>();
        bool dirty = false;

        (d.EatUrgency,      var v1) = Guard(d.EatUrgency,      0f, 1f, "DriveComponent", "EatUrgency",      name, t);
        (d.DrinkUrgency,    var v2) = Guard(d.DrinkUrgency,    0f, 1f, "DriveComponent", "DrinkUrgency",    name, t);
        (d.SleepUrgency,    var v3) = Guard(d.SleepUrgency,    0f, 1f, "DriveComponent", "SleepUrgency",    name, t);
        (d.DefecateUrgency, var v4) = Guard(d.DefecateUrgency, 0f, 1f, "DriveComponent", "DefecateUrgency", name, t);
        (d.PeeUrgency,      var v5) = Guard(d.PeeUrgency,      0f, 1f, "DriveComponent", "PeeUrgency",      name, t);
        dirty = v1 | v2 | v3 | v4 | v5;

        if (dirty) entity.Add(d);
    }

    private void CheckTransit(Entity entity, string name, double t)
    {
        if (!entity.Has<EsophagusTransitComponent>()) return;
        var tr    = entity.Get<EsophagusTransitComponent>();
        bool dirty = false;

        (tr.Progress, var v1) = Guard(tr.Progress, 0f, 1f,
                                      "EsophagusTransitComponent", "Progress", name, t);
        dirty = v1;

        if (dirty) entity.Add(tr);
    }

    // -- Chronicle integrity ---------------------------------------------------

    private void CheckChronicleIntegrity(EntityManager em, double gameTime)
    {
        // Build lookup of all entity int-IDs.
        var stainIds  = new HashSet<string>();
        var brokenIds = new HashSet<string>();

        foreach (var entity in em.GetAllEntities())
        {
            if (entity.Has<StainTag>() && entity.Has<StainComponent>())
            {
                var sc = entity.Get<StainComponent>();
                // Stain entity with missing chronicle entry → violation.
                if (!string.IsNullOrEmpty(sc.ChronicleEntryId)
                    && !_chronicle!.All.Any(e => e.Id == sc.ChronicleEntryId))
                {
                    _violations.Add(new InvariantViolation(
                        GameTime:    gameTime,
                        EntityName:  entity.ShortId,
                        Component:   "StainComponent",
                        Property:    "ChronicleEntryId",
                        ActualValue: 0f,
                        ClampedTo:   0f,
                        ValidMin:    0f,
                        ValidMax:    0f));
                }
                stainIds.Add(sc.ChronicleEntryId ?? string.Empty);
            }

            if (entity.Has<BrokenItemTag>() && entity.Has<BrokenItemComponent>())
            {
                var bc = entity.Get<BrokenItemComponent>();
                // BrokenItem entity with missing chronicle entry → violation.
                if (!string.IsNullOrEmpty(bc.ChronicleEntryId)
                    && !_chronicle!.All.Any(e => e.Id == bc.ChronicleEntryId))
                {
                    _violations.Add(new InvariantViolation(
                        GameTime:    gameTime,
                        EntityName:  entity.ShortId,
                        Component:   "BrokenItemComponent",
                        Property:    "ChronicleEntryId",
                        ActualValue: 0f,
                        ClampedTo:   0f,
                        ValidMin:    0f,
                        ValidMax:    0f));
                }
                brokenIds.Add(bc.ChronicleEntryId ?? string.Empty);
            }
        }

        // Build set of entity int-IDs for fast lookup.
        var entityIntIds = new HashSet<int>();
        foreach (var entity in em.GetAllEntities())
            entityIntIds.Add(EntityIntId(entity));

        // Chronicle entry with physicalManifestEntityId pointing to missing entity → violation.
        foreach (var entry in _chronicle!.All)
        {
            if (string.IsNullOrEmpty(entry.PhysicalManifestEntityId)) continue;
            int manifestIntId = GuidStringToIntId(entry.PhysicalManifestEntityId);
            if (!entityIntIds.Contains(manifestIntId))
            {
                _violations.Add(new InvariantViolation(
                    GameTime:    gameTime,
                    EntityName:  entry.Id,
                    Component:   "ChronicleService",
                    Property:    "PhysicalManifestEntityId",
                    ActualValue: 0f,
                    ClampedTo:   0f,
                    ValidMin:    0f,
                    ValidMax:    0f));
            }
        }
    }

    private static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    private static int GuidStringToIntId(string guidStr)
    {
        if (!Guid.TryParse(guidStr, out var g)) return -1;
        var b = g.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    // -- Guard primitive -------------------------------------------------------

    /// <summary>
    /// Returns (clampedValue, violated).
    /// Logs a violation if the value is outside [min, max].
    /// </summary>
    private (float value, bool violated) Guard(
        float value, float min, float max,
        string component, string property, string entity, double gameTime)
    {
        if (value >= min && value <= max) return (value, false);

        float clamped = Math.Clamp(value, min, max);
        _violations.Add(new InvariantViolation(
            GameTime:    gameTime,
            EntityName:  entity,
            Component:   component,
            Property:    property,
            ActualValue: value,
            ClampedTo:   clamped,
            ValidMin:    min,
            ValidMax:    max));

        return (clamped, true);
    }
}
