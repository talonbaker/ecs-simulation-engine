using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

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
public sealed class MovementSpeedModifierSystem : ISystem
{
    private readonly MovementSpeedModifierConfig _cfg;

    public MovementSpeedModifierSystem(MovementConfig cfg)
    {
        _cfg = cfg.SpeedModifier;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<MovementComponent>())
        {
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
