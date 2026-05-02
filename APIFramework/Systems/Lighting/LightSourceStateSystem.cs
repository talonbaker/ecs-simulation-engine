using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Audio;

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
/// <remarks>
/// Reads and writes <c>LightSourceComponent</c>; produces a per-tick effective-intensity cache
/// consumed by <see cref="IlluminationAccumulationSystem"/>. Must run BEFORE
/// <see cref="IlluminationAccumulationSystem"/>.
/// </remarks>
public sealed class LightSourceStateSystem : ISystem
{
    private readonly SeededRandom   _rng;
    private readonly LightingConfig _cfg;
    private readonly SoundTriggerBus? _soundBus;
    private readonly int _bulbBuzzInterval;

    // per-entity effective intensity for the current tick (used by IlluminationAccumulationSystem)
    private readonly Dictionary<Entity, double> _effectiveIntensity = new();

    // per-entity tick counter for BulbBuzz throttling
    private readonly Dictionary<Entity, int> _flickerTick = new();

    /// <summary>
    /// Stores RNG and config references used per tick.
    /// </summary>
    /// <param name="rng">Deterministic RNG used for flicker and decay rolls.</param>
    /// <param name="cfg">Lighting tuning — supplies <c>FlickerOnProb</c> and <c>DyingDecayProb</c>.</param>
    /// <param name="soundCfg">Optional sound tuning for BulbBuzz interval.</param>
    /// <param name="soundBus">Optional bus for BulbBuzz emission.</param>
    public LightSourceStateSystem(SeededRandom rng, LightingConfig cfg, SoundTriggerConfig? soundCfg = null, SoundTriggerBus? soundBus = null)
    {
        _rng = rng;
        _cfg = cfg;
        _soundBus = soundBus;
        _bulbBuzzInterval = soundCfg?.BulbBuzzEmitIntervalTicks ?? 10;
    }

    /// <summary>
    /// Returns the effective intensity for <paramref name="entity"/> as computed on the
    /// most recent tick. Returns 0 for unregistered entities.
    /// </summary>
    /// <param name="entity">A light-source entity.</param>
    public double GetEffectiveIntensity(Entity entity) =>
        _effectiveIntensity.TryGetValue(entity, out var v) ? v : 0.0;

    /// <summary>
    /// Per-tick entry point. Advances the state machine for every light source and
    /// publishes the resulting effective-intensity cache.
    /// </summary>
    /// <param name="em">Entity manager — queried for light sources.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused).</param>
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

                    // Emit BulbBuzz at the configured interval
                    if (_soundBus != null)
                    {
                        _flickerTick.TryGetValue(entity, out var ft);
                        ft++;
                        if (ft >= _bulbBuzzInterval)
                        {
                            ft = 0;
                            var pos = entity.Has<PositionComponent>() ? entity.Get<PositionComponent>() : default;
                            _soundBus.Emit(SoundTriggerKind.BulbBuzz, entity.Id, pos.X, pos.Z, 0.2f, 0L);
                        }
                        _flickerTick[entity] = ft;
                    }
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
