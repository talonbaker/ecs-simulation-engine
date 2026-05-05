using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Evolves EnergyComponent every tick and manages the energy-state tags.
///
/// AWAKE:   energy drains slowly; sleepiness accumulates.
/// SLEEPING: energy restores; sleepiness drains back toward zero.
///
/// This system only READS IsSleeping — it never sets it.
/// SleepSystem (later in the pipeline) is the sole writer of IsSleeping.
///
/// Tags managed here:
///   TiredTag     — Energy below tiredThreshold
///   ExhaustedTag — Energy below exhaustedThreshold (severe)
///   SleepingTag  — mirrors IsSleeping so the renderer can show it
///
/// Pipeline position: 2 of 10 — after MetabolismSystem, before BiologicalConditionSystem.
/// </summary>
/// <remarks>
/// Reads: <see cref="EnergyComponent"/>, <see cref="LifeStateComponent"/>.<br/>
/// Writes: <see cref="EnergyComponent"/> Energy/Sleepiness, plus
/// <see cref="SleepingTag"/>, <see cref="TiredTag"/>, <see cref="ExhaustedTag"/>
/// (single writer of these three energy-state tags). Does NOT toggle
/// <see cref="EnergyComponent"/>.IsSleeping — that is owned by <see cref="SleepSystem"/>.<br/>
/// Phase: Physiology, after <see cref="MetabolismSystem"/> drains macro-resources.
/// </remarks>
public class EnergySystem : ISystem
{
    private readonly EnergySystemConfig _cfg;

    /// <summary>Constructs the energy system with its threshold tuning.</summary>
    /// <param name="cfg">Energy thresholds (TiredThreshold, ExhaustedThreshold).</param>
    public EnergySystem(EnergySystemConfig cfg) => _cfg = cfg;

    /// <summary>Per-tick energy/sleepiness pass.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<EnergyComponent>().ToList())
        {
            if (!LifeStateGuard.IsBiologicallyTicking(entity)) continue;  // WP-3.0.0: skip Deceased NPCs (Incapacitated still ticks)

            var e = entity.Get<EnergyComponent>();

            if (e.IsSleeping)
            {
                // -- Sleeping: restore energy, drain sleepiness ----------------
                e.Energy     = MathF.Min(100f, e.Energy     + e.EnergyRestoreRate    * deltaTime);
                e.Sleepiness = MathF.Max(0f,   e.Sleepiness - e.SleepinessDrainRate  * deltaTime);
            }
            else
            {
                // -- Awake: drain energy, accumulate sleepiness ----------------
                e.Energy     = MathF.Max(0f,   e.Energy     - e.EnergyDrainRate     * deltaTime);
                e.Sleepiness = MathF.Min(100f, e.Sleepiness + e.SleepinessGainRate  * deltaTime);
            }

            entity.Add(e); // write back (struct copy)

            // -- Energy-state tags ---------------------------------------------

            // SleepingTag mirrors the IsSleeping flag
            if (e.IsSleeping) entity.Add(new SleepingTag());
            else              entity.Remove<SleepingTag>();

            // ExhaustedTag is more severe — check it first
            if (e.Energy <= _cfg.ExhaustedThreshold)
            {
                entity.Add(new ExhaustedTag());
                entity.Remove<TiredTag>(); // exhausted supersedes tired
            }
            else if (e.Energy <= _cfg.TiredThreshold)
            {
                entity.Add(new TiredTag());
                entity.Remove<ExhaustedTag>();
            }
            else
            {
                entity.Remove<TiredTag>();
                entity.Remove<ExhaustedTag>();
            }
        }
    }
}
