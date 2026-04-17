using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// The priority queue. Scores every active drive and writes the dominant DesireType
/// onto each entity's DriveComponent.
///
/// Action systems (FeedingSystem, DrinkingSystem, SleepSystem) MUST check
/// DriveComponent.Dominant before acting. This is the single source of truth for
/// "what should Billy do right now."
///
/// SCORING FORMULA
/// ───────────────
///   EatUrgency   = (Hunger   / 100) * EatMaxScore
///   DrinkUrgency = (Thirst   / 100) * DrinkMaxScore
///   SleepUrgency = (Sleepiness / 100) * SleepMaxScore * CircadianFactor
///
/// CircadianFactor (from SimulationClock) amplifies sleep at night and suppresses
/// it in the morning, producing a natural 24-hour rhythm.
///
/// SleepMaxScore (default 0.9) caps sleep below the survival ceiling (1.0) so that
/// life-threatening hunger or thirst can always override exhaustion.
///
/// Pipeline position: 4 of 10 — after EnergySystem + BiologicalConditionSystem, before action systems.
/// </summary>
public class BrainSystem : ISystem
{
    private readonly BrainSystemConfig _cfg;
    private readonly SimulationClock   _clock;

    public BrainSystem(BrainSystemConfig cfg, SimulationClock clock)
    {
        _cfg   = cfg;
        _clock = clock;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        float circadian = _clock.CircadianFactor;

        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            var meta = entity.Get<MetabolismComponent>();

            var drives = entity.Has<DriveComponent>()
                ? entity.Get<DriveComponent>()
                : new DriveComponent();

            // ── Survival drives (unaffected by circadian) ─────────────────────
            drives.EatUrgency   = (meta.Hunger / 100f) * _cfg.EatMaxScore;
            drives.DrinkUrgency = (meta.Thirst / 100f) * _cfg.DrinkMaxScore;

            // ── Sleep drive (amplified / suppressed by time of day) ───────────
            if (entity.Has<EnergyComponent>())
            {
                var energy = entity.Get<EnergyComponent>();
                drives.SleepUrgency = (energy.Sleepiness / 100f) * _cfg.SleepMaxScore * circadian;
            }
            else
            {
                drives.SleepUrgency = 0f;
            }

            entity.Add(drives);
        }
    }
}
