using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// The priority queue. Runs after BiologicalConditionSystem has set condition tags,
/// scores every active drive, and writes the dominant DesireType onto each entity.
///
/// Action systems (FeedingSystem, DrinkingSystem, SleepSystem…) MUST check
/// DriveComponent.Dominant before acting. If it is not their drive, they stand down.
/// This is the single source of truth for "what should Billy do right now."
///
/// Pipeline position: 3 of 8 — after condition tagging, before any action systems.
/// </summary>
public class BrainSystem : ISystem
{
    private readonly BrainSystemConfig _cfg;

    public BrainSystem(BrainSystemConfig cfg) => _cfg = cfg;

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            var meta = entity.Get<MetabolismComponent>();

            // ── Score each drive ─────────────────────────────────────────────
            //
            // Score = (sensation_0_to_100 / 100) * maxScore
            // maxScore caps the priority ceiling — sleep is 0.9 so it can never
            // outbid a life-threatening hunger or thirst (which reach 1.0).
            //
            var drives = entity.Has<DriveComponent>()
                ? entity.Get<DriveComponent>()
                : new DriveComponent();

            drives.EatUrgency   = (meta.Hunger / 100f) * _cfg.EatMaxScore;
            drives.DrinkUrgency = (meta.Thirst / 100f) * _cfg.DrinkMaxScore;

            // SleepUrgency will be driven by EnergyComponent once implemented.
            // Placeholder: stays at 0 until that system exists.
            drives.SleepUrgency = 0f;

            entity.Add(drives);
        }
    }
}
