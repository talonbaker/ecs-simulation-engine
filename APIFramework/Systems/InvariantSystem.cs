using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Diagnostics;

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
/// ───────────────────────
///   Single violation, once only  → transient floating-point rounding, usually fine
///   Same violation every tick    → a system is producing bad values continuously
///   Resource stuck at 0 for ages → entity in terminal state; feedback loop broken
///   Resource never drops below X → action threshold is set too low; entity never experiences need
/// </summary>
public class InvariantSystem : ISystem
{
    private readonly SimulationClock          _clock;
    private readonly List<InvariantViolation> _violations = new();

    public InvariantSystem(SimulationClock clock) => _clock = clock;

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

    public void Update(EntityManager em, float deltaTime)
    {
        double gameTime = _clock.TotalTime;

        foreach (var entity in em.GetAllEntities())
        {
            string name = entity.Has<IdentityComponent>()
                ? entity.Get<IdentityComponent>().Name
                : entity.ShortId;

            CheckMetabolism     (entity, name, gameTime);
            CheckEnergy         (entity, name, gameTime);
            CheckStomach        (entity, name, gameTime);
            CheckSmallIntestine (entity, name, gameTime);
            CheckLargeIntestine (entity, name, gameTime);
            CheckColon          (entity, name, gameTime);
            CheckBladder        (entity, name, gameTime);
            CheckDrives         (entity, name, gameTime);
            CheckTransit        (entity, name, gameTime);
        }
    }

    // ── Per-component checks ─────────────────────────────────────────────────

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

    // ── Guard primitive ───────────────────────────────────────────────────────

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
