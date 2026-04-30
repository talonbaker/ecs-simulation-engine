using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Cognition phase. Scores each NPC's biological drives (Eat, Drink, Sleep, Defecate, Pee)
/// using physiology, circadian state, and mood-tag modifiers, and writes per-drive urgencies
/// (whose <c>Dominant</c> getter selects the winning <see cref="DesireType"/>) into
/// <see cref="DriveComponent"/>. Single writer of <see cref="DriveComponent"/>.
/// </summary>
/// <remarks>
/// Reads: <see cref="MetabolismComponent"/>, <see cref="EnergyComponent"/>,
/// <see cref="ColonComponent"/>, <see cref="BladderComponent"/>, mood tags
/// (<see cref="BoredTag"/>, <see cref="SadTag"/>, <see cref="GriefTag"/>),
/// criticality tags (<see cref="BowelCriticalTag"/>, <see cref="BladderCriticalTag"/>),
/// circadian factor from <see cref="SimulationClock"/>.<br/>
/// Writes: <see cref="DriveComponent"/> urgencies (single writer).<br/>
/// Phase: Cognition. Runs after <see cref="MoodSystem"/> has updated emotion tags this
/// tick and before <see cref="PhysiologyGateSystem"/> writes the veto set.
/// </remarks>
public class BrainSystem : ISystem
{
    private readonly BrainSystemConfig _cfg;
    private readonly SimulationClock   _clock;

    /// <summary>Constructs the brain with its scoring tuning and the simulation clock.</summary>
    /// <param name="cfg">Brain scoring tuning (max scores, mood multipliers, urgency floor).</param>
    /// <param name="clock">Simulation clock; <c>CircadianFactor</c> shapes sleep urgency.</param>
    public BrainSystem(BrainSystemConfig cfg, SimulationClock clock)
    {
        _cfg   = cfg;
        _clock = clock;
    }

    /// <summary>Per-tick drive-scoring pass over every entity with a <see cref="MetabolismComponent"/>.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        float circadian = _clock.CircadianFactor;

        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
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
