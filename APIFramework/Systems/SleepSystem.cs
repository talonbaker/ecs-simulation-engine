using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Translates the brain's SLEEP dominant drive into an actual sleep state.
///
/// This is the sole writer of EnergyComponent.IsSleeping.
/// EnergySystem (earlier in the pipeline) reads that flag to decide
/// whether to drain or restore energy.
///
/// FALL ASLEEP:  BrainSystem has scored SLEEP as dominant.
/// WAKE UP:      BrainSystem's dominant is no longer SLEEP.
///               Additionally, we enforce a minimum-sleepiness floor (WakeThreshold):
///               if the entity woke up naturally but sleepiness hasn't dropped low
///               enough yet, it stays asleep until the brain genuinely lets it go.
///
/// Note: because BrainSystem runs before this system, the dominant desire it computed
/// already accounts for the circadian factor and current sleepiness level.
/// No extra logic is needed here — the brain handles all priority decisions.
///
/// Pipeline position: 7 of 10 — after action systems, before InteractionSystem.
/// </summary>
public class SleepSystem : ISystem
{
    private readonly SleepSystemConfig _cfg;

    public SleepSystem(SleepSystemConfig cfg) => _cfg = cfg;

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<EnergyComponent>().ToList())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip Incapacitated/Deceased NPCs

            // Only entities that the brain evaluates are eligible for sleep
            if (!entity.Has<DriveComponent>()) continue;

            var energy  = entity.Get<EnergyComponent>();
            var drives  = entity.Get<DriveComponent>();

            bool brainWantsSleep = drives.Dominant == DesireType.Sleep;

            // Social inhibition veto: vulnerability overrides exhaustion.
            if (entity.Has<BlockedActionsComponent>() &&
                entity.Get<BlockedActionsComponent>().Contains(BlockedActionClass.Sleep))
            {
                // Veto only suppresses the fall-asleep action; wake-up is always allowed.
                if (!energy.IsSleeping) continue;
            }

            if (!energy.IsSleeping && brainWantsSleep)
            {
                // ── Fall asleep ───────────────────────────────────────────────
                energy.IsSleeping = true;
                entity.Add(energy);
            }
            else if (energy.IsSleeping && !brainWantsSleep)
            {
                // ── Wake up — only when sleepiness is genuinely low enough ────
                // This prevents snapping awake momentarily just because hunger
                // briefly spikes and then gets satisfied mid-sleep.
                if (energy.Sleepiness <= _cfg.WakeThreshold)
                {
                    energy.IsSleeping = false;
                    entity.Add(energy);
                }
                // else: stay asleep until sleepiness drains below the threshold
            }
        }
    }
}
