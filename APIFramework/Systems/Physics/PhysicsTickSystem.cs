using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Audio;

namespace APIFramework.Systems.Physics;

/// <summary>
/// Cleanup-phase system. Advances all ThrownVelocityComponent-bearing entities by one tick.
/// Applies gravity, decay, collision detection, and breakage. Deterministic: iterates by
/// entity ID ascending; no RNG.
/// </summary>
public sealed class PhysicsTickSystem : ISystem
{
    private readonly PhysicsConfig      _cfg;
    private readonly CollisionDetector  _collision;
    private readonly IWorldMutationApi  _mutationApi;
    private readonly SoundTriggerBus    _soundBus;
    private readonly SimulationClock    _clock;

    public PhysicsTickSystem(
        PhysicsConfig      cfg,
        CollisionDetector  collision,
        IWorldMutationApi  mutationApi,
        SoundTriggerBus    soundBus,
        SimulationClock    clock)
    {
        _cfg         = cfg;
        _collision   = collision;
        _mutationApi = mutationApi;
        _soundBus    = soundBus;
        _clock       = clock;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        var thrown = em.GetAllEntities()
            .Where(e => e.Has<ThrownVelocityComponent>())
            .OrderBy(e => e.Id)
            .ToList();

        foreach (var entity in thrown)
        {
            var v = entity.Get<ThrownVelocityComponent>();
            var p = entity.Get<PositionComponent>();

            var mass = entity.Has<MassComponent>()
                ? entity.Get<MassComponent>().MassKilograms
                : 1.0f;

            v.VelocityY -= _cfg.GravityPerTick;

            var newPos = new PositionComponent
            {
                X = p.X + v.VelocityX * deltaTime,
                Z = p.Z + v.VelocityZ * deltaTime,
                Y = MathF.Max(0f, p.Y + v.VelocityY * deltaTime)
            };

            var hit = _collision.DetectHit(p, newPos, entity.Id);

            if (hit.Surface != HitSurface.None)
            {
                float velocityMag = MathF.Sqrt(
                    v.VelocityX * v.VelocityX +
                    v.VelocityZ * v.VelocityZ +
                    v.VelocityY * v.VelocityY);
                float hitEnergy = 0.5f * mass * velocityMag * velocityMag;

                var soundKind = entity.Has<BreakableComponent>() &&
                                entity.Get<BreakableComponent>().OnBreak == BreakageBehavior.SpawnGlassShards
                    ? SoundTriggerKind.Glass
                    : SoundTriggerKind.Crash;

                _soundBus.Emit(soundKind, entity.Id, hit.X, hit.Z,
                    MathF.Min(1.0f, hitEnergy / 100.0f), _clock.CurrentTick);

                if (entity.Has<BreakableComponent>())
                {
                    var breakable = entity.Get<BreakableComponent>();
                    if (hitEnergy >= breakable.HitEnergyThreshold)
                    {
                        ApplyBreakage(entity, breakable, hit.X, hit.Z);
                        continue;
                    }
                }

                entity.Add(new PositionComponent { X = hit.X, Z = hit.Z, Y = hit.Y });
                entity.Remove<ThrownVelocityComponent>();
                entity.Remove<ThrownTag>();
                continue;
            }

            entity.Add(newPos);

            v.VelocityX *= 1.0f - v.DecayPerTick;
            v.VelocityZ *= 1.0f - v.DecayPerTick;

            if (MathF.Abs(v.VelocityX) < _cfg.MinVelocity &&
                MathF.Abs(v.VelocityZ) < _cfg.MinVelocity &&
                MathF.Abs(v.VelocityY) < _cfg.MinVelocity)
            {
                entity.Remove<ThrownVelocityComponent>();
                entity.Remove<ThrownTag>();
                continue;
            }

            entity.Add(v);
        }
    }

    private void ApplyBreakage(Entity entity, BreakableComponent breakable, float x, float z)
    {
        switch (breakable.OnBreak)
        {
            case BreakageBehavior.Despawn:
                _mutationApi.DespawnStructural(entity.Id);
                break;

            case BreakageBehavior.SpawnLiquidStain:
                _mutationApi.DespawnStructural(entity.Id);
                _mutationApi.SpawnStain(StainTemplates.WaterPuddle, (int)x, (int)z);
                break;

            case BreakageBehavior.SpawnGlassShards:
                _mutationApi.DespawnStructural(entity.Id);
                _mutationApi.SpawnStain(StainTemplates.BrokenGlass, (int)x, (int)z);
                break;

            case BreakageBehavior.SpawnDebris:
                entity.Add(new DebrisTag());
                entity.Remove<ThrownVelocityComponent>();
                entity.Remove<ThrownTag>();
                break;
        }
    }
}
