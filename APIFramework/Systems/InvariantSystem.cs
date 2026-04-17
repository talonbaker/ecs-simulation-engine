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

            CheckMetabolism(entity, name, gameTime);
            CheckEnergy    (entity, name, gameTime);
            CheckStomach   (entity, name, gameTime);
            CheckDrives    (entity, name, gameTime);
            CheckTransit   (entity, name, gameTime);
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
        (s.NutritionQueued, var v2) = Guard(s.NutritionQueued, 0f, 500f,
                                            "StomachComponent", "NutritionQueued", name, t);
        (s.HydrationQueued, var v3) = Guard(s.HydrationQueued, 0f, 500f,
                                            "StomachComponent", "HydrationQueued", name, t);
        dirty = v1 | v2 | v3;

        if (dirty) entity.Add(s);
    }

    private void CheckDrives(Entity entity, string name, double t)
    {
        if (!entity.Has<DriveComponent>()) return;
        var d     = entity.Get<DriveComponent>();
        bool dirty = false;

        (d.EatUrgency,   var v1) = Guard(d.EatUrgency,   0f, 1f, "DriveComponent", "EatUrgency",   name, t);
        (d.DrinkUrgency, var v2) = Guard(d.DrinkUrgency, 0f, 1f, "DriveComponent", "DrinkUrgency", name, t);
        (d.SleepUrgency, var v3) = Guard(d.SleepUrgency, 0f, 1f, "DriveComponent", "SleepUrgency", name, t);
        dirty = v1 | v2 | v3;

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
