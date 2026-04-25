using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// The priority queue. Scores every active drive, applies mood modifiers, and
/// writes the dominant DesireType onto each entity's DriveComponent.
///
/// Action systems (FeedingSystem, DrinkingSystem, SleepSystem) MUST check
/// DriveComponent.Dominant before acting. This is the single source of truth for
/// "what should Billy do right now."
///
/// SCORING FORMULA
/// ───────────────
///   EatUrgency      = (Hunger     / 100) * EatMaxScore
///   DrinkUrgency    = (Thirst     / 100) * DrinkMaxScore
///   SleepUrgency    = (Sleepiness / 100) * SleepMaxScore * CircadianFactor
///   DefecateUrgency = ColonComponent.Fill * DefecateMaxScore
///                     (overrides to 1.0 when BowelCriticalTag is present)
///
/// MOOD MODIFIERS (applied after base scores)
/// ───────────────────────────────────────────
///   BoredTag    → +BoredUrgencyBonus flat added to every drive (idle → more likely to act)
///   SadTag      → ×SadnessUrgencyMult on every drive (sadness reduces motivation)
///   GriefTag    → ×GriefUrgencyMult  on every drive (grief strongly suppresses action)
///
/// MINIMUM URGENCY FLOOR
/// ──────────────────────
/// If all urgency scores remain below MinUrgencyThreshold after mood modifiers,
/// all scores are zeroed — Dominant returns None via DriveComponent's 0.001 guard.
/// This is the idle state from which boredom accumulates in MoodSystem.
///
/// Pipeline position: 6 — after MoodSystem has updated emotion tags this tick.
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

            // ── Base drive scores ─────────────────────────────────────────────
            drives.EatUrgency   = (meta.Hunger / 100f) * _cfg.EatMaxScore;
            drives.DrinkUrgency = (meta.Thirst / 100f) * _cfg.DrinkMaxScore;

            if (entity.Has<EnergyComponent>())
            {
                var energy = entity.Get<EnergyComponent>();
                drives.SleepUrgency = (energy.Sleepiness / 100f) * _cfg.SleepMaxScore * circadian;
            }
            else
            {
                drives.SleepUrgency = 0f;
            }

            // Defecation: urgency scales with colon fill; critical tag forces override
            if (entity.Has<ColonComponent>())
            {
                var colon = entity.Get<ColonComponent>();
                drives.DefecateUrgency = entity.Has<BowelCriticalTag>()
                    ? 1.0f
                    : colon.Fill * _cfg.DefecateMaxScore;
            }
            else
            {
                drives.DefecateUrgency = 0f;
            }

            // Urination: urgency scales with bladder fill; critical tag forces override
            if (entity.Has<BladderComponent>())
            {
                var bladder = entity.Get<BladderComponent>();
                drives.PeeUrgency = entity.Has<BladderCriticalTag>()
                    ? 1.0f
                    : bladder.Fill * _cfg.PeeMaxScore;
            }
            else
            {
                drives.PeeUrgency = 0f;
            }

            // ── Mood modifiers ────────────────────────────────────────────────

            // Boredom: idle state makes even small drives feel more pressing
            if (entity.Has<BoredTag>())
            {
                drives.EatUrgency      += _cfg.BoredUrgencyBonus;
                drives.DrinkUrgency    += _cfg.BoredUrgencyBonus;
                drives.SleepUrgency    += _cfg.BoredUrgencyBonus;
                // Elimination urgencies are purely physiological — boredom doesn't amplify them
            }

            // Grief → heavy suppression; Sadness → mild suppression.
            // Critical bladder/bowel tags bypass suppression — you can't willpower away
            // a full colon or bladder.
            bool bowelOverride   = entity.Has<BowelCriticalTag>();
            bool bladderOverride = entity.Has<BladderCriticalTag>();
            if (entity.Has<GriefTag>())
            {
                drives.EatUrgency      *= _cfg.GriefUrgencyMult;
                drives.DrinkUrgency    *= _cfg.GriefUrgencyMult;
                drives.SleepUrgency    *= _cfg.GriefUrgencyMult;
                if (!bowelOverride)   drives.DefecateUrgency *= _cfg.GriefUrgencyMult;
                if (!bladderOverride) drives.PeeUrgency      *= _cfg.GriefUrgencyMult;
            }
            else if (entity.Has<SadTag>())
            {
                drives.EatUrgency      *= _cfg.SadnessUrgencyMult;
                drives.DrinkUrgency    *= _cfg.SadnessUrgencyMult;
                drives.SleepUrgency    *= _cfg.SadnessUrgencyMult;
                if (!bowelOverride)   drives.DefecateUrgency *= _cfg.SadnessUrgencyMult;
                if (!bladderOverride) drives.PeeUrgency      *= _cfg.SadnessUrgencyMult;
            }

            // ── Minimum urgency floor → idle state ────────────────────────────
            float maxUrgency = MathF.Max(drives.EatUrgency,
                               MathF.Max(drives.DrinkUrgency,
                               MathF.Max(drives.SleepUrgency,
                               MathF.Max(drives.DefecateUrgency, drives.PeeUrgency))));

            if (maxUrgency < _cfg.MinUrgencyThreshold)
            {
                drives.EatUrgency      = 0f;
                drives.DrinkUrgency    = 0f;
                drives.SleepUrgency    = 0f;
                drives.DefecateUrgency = 0f;
                drives.PeeUrgency      = 0f;
            }

            entity.Add(drives);
        }
    }
}
