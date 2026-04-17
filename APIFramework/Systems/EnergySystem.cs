using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

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
public class EnergySystem : ISystem
{
    private readonly EnergySystemConfig _cfg;

    public EnergySystem(EnergySystemConfig cfg) => _cfg = cfg;

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<EnergyComponent>().ToList())
        {
            var e = entity.Get<EnergyComponent>();

            if (e.IsSleeping)
            {
                // ── Sleeping: restore energy, drain sleepiness ────────────────
                e.Energy     = MathF.Min(100f, e.Energy     + e.EnergyRestoreRate    * deltaTime);
                e.Sleepiness = MathF.Max(0f,   e.Sleepiness - e.SleepinessDrainRate  * deltaTime);
            }
            else
            {
                // ── Awake: drain energy, accumulate sleepiness ────────────────
                e.Energy     = MathF.Max(0f,   e.Energy     - e.EnergyDrainRate     * deltaTime);
                e.Sleepiness = MathF.Min(100f, e.Sleepiness + e.SleepinessGainRate  * deltaTime);
            }

            entity.Add(e); // write back (struct copy)

            // ── Energy-state tags ─────────────────────────────────────────────

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
