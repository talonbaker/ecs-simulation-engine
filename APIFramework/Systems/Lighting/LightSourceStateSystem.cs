using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems.Lighting;

/// <summary>
/// Phase: Lighting (7) — ticks light source state machines each frame.
///
/// On/Off: stable, no transitions (external triggers handle On↔Off).
/// Flickering: stochastic per-tick effective intensity; component Intensity not modified.
/// Dying: slow intensity decay toward 0; transitions to Off when intensity reaches 0.
///
/// All randomness goes through SeededRandom — never System.Random.
/// </summary>
public sealed class LightSourceStateSystem : ISystem
{
    private readonly SeededRandom   _rng;
    private readonly LightingConfig _cfg;

    // per-entity effective intensity for the current tick (used by IlluminationAccumulationSystem)
    private readonly Dictionary<Entity, double> _effectiveIntensity = new();

    public LightSourceStateSystem(SeededRandom rng, LightingConfig cfg)
    {
        _rng = rng;
        _cfg = cfg;
    }

    /// <summary>
    /// Returns the effective intensity for <paramref name="entity"/> as computed on the
    /// most recent tick. Returns 0 for unregistered entities.
    /// </summary>
    public double GetEffectiveIntensity(Entity entity) =>
        _effectiveIntensity.TryGetValue(entity, out var v) ? v : 0.0;

    public void Update(EntityManager em, float deltaTime)
    {
        _effectiveIntensity.Clear();

        foreach (var entity in em.Query<LightSourceTag>())
        {
            var comp = entity.Get<LightSourceComponent>();

            switch (comp.State)
            {
                case LightState.On:
                    _effectiveIntensity[entity] = comp.Intensity;
                    break;

                case LightState.Off:
                    _effectiveIntensity[entity] = 0.0;
                    break;

                case LightState.Flickering:
                    // Component.Intensity not modified; effective intensity is stochastic per tick.
                    double flickerEffective = _rng.NextDouble() < _cfg.FlickerOnProb
                        ? comp.Intensity
                        : 0.0;
                    _effectiveIntensity[entity] = flickerEffective;
                    break;

                case LightState.Dying:
                    // Slowly decrement Intensity; transition to Off when it reaches 0.
                    if (_rng.NextDouble() < _cfg.DyingDecayProb && comp.Intensity > 0)
                    {
                        comp.Intensity--;
                        if (comp.Intensity == 0)
                            comp.State = LightState.Off;
                        entity.Add(comp);
                    }
                    _effectiveIntensity[entity] = comp.Intensity;
                    break;
            }
        }
    }
}
