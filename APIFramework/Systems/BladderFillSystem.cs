using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Fills the bladder at a constant rate each tick.
///
/// PIPELINE POSITION
/// ─────────────────
///   Phase: Physiology (10) — runs alongside MetabolismSystem and EnergySystem.
///   BladderSystem (Elimination/55) reads the resulting volume to apply tags.
///
/// DESIGN
/// ──────
/// Kidneys are deliberately omitted (see BladderComponent). The fill rate is a
/// flat ml/game-second, configurable per entity type in SimConfig.json.
///
/// The bladder does NOT slow during sleep — the kidneys filter continuously
/// even while unconscious. An entity waking after 8 game-hours will typically
/// have a full bladder, matching realistic morning-bathroom behaviour.
///
/// Volume is capped at CapacityMl — BladderSystem and BowelCriticalTag handle
/// the gameplay consequence of overflow; the volume itself never exceeds cap.
/// </summary>
public class BladderFillSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<BladderComponent>().ToList())
        {
            if (!LifeStateGuard.IsBiologicallyTicking(entity)) continue;  // WP-3.0.0: skip Deceased NPCs (Incapacitated still ticks)

            var bladder = entity.Get<BladderComponent>();

            bladder.VolumeML = MathF.Min(
                bladder.CapacityMl,
                bladder.VolumeML + bladder.FillRate * deltaTime);

            entity.Add(bladder);
        }
    }
}
