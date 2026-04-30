using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems.Movement;

/// <summary>
/// Per-tick: computes a per-NPC speed multiplier from social drives and energy,
/// and writes it to MovementComponent.SpeedModifier.
///
/// Formula (applied before clamping to [min, max]):
///   multiplier = 1.0
///              + irritation.Current * irritationGainPerPoint
///              - affection.Current  * affectionLossPerPoint
///              - (100 - energy)     * lowEnergyLossPerPoint
/// </summary>
/// <remarks>
/// Phase: World (60), registered between <see cref="PathfindingTriggerSystem"/> and
/// <see cref="StepAsideSystem"/>. Reads <c>SocialDrivesComponent</c> and
/// <c>EnergyComponent</c>; writes <c>MovementComponent.SpeedModifier</c>.
/// Skips non-Alive NPCs.
/// </remarks>
public sealed class MovementSpeedModifierSystem : ISystem
{
    private readonly MovementSpeedModifierConfig _cfg;

    /// <summary>
    /// Captures the speed-modifier sub-config from <paramref name="cfg"/>.
    /// </summary>
    /// <param name="cfg">Movement config — its <c>SpeedModifier</c> sub-section is used.</param>
    public MovementSpeedModifierSystem(MovementConfig cfg)
    {
        _cfg = cfg.SpeedModifier;
    }

    /// <summary>
    /// Per-tick entry point. Computes and writes the speed multiplier for every NPC.
    /// </summary>
    /// <param name="em">Entity manager — queried for movement-capable entities.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<MovementComponent>())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
            var move = entity.Get<MovementComponent>();

            float multiplier = 1.0f;

            if (entity.Has<SocialDrivesComponent>())
            {
                var drives = entity.Get<SocialDrivesComponent>();
                multiplier += drives.Irritation.Current * _cfg.IrritationGainPerPoint;
                multiplier -= drives.Affection.Current  * _cfg.AffectionLossPerPoint;
            }

            if (entity.Has<EnergyComponent>())
            {
                var energy = entity.Get<EnergyComponent>();
                multiplier -= (100f - energy.Energy) * _cfg.LowEnergyLossPerPoint;
            }

            multiplier = System.Math.Clamp(multiplier, _cfg.MinMultiplier, _cfg.MaxMultiplier);

            move.SpeedModifier = multiplier;
            entity.Add(move);
        }
    }
}
