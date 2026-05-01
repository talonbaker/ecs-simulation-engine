using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Audio;
using APIFramework.Systems.Physics;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Physics;

/// <summary>Despawn breakage behavior: entity is destroyed, no stain spawned.</summary>
public class PhysicsTickDespawnNonBreakableTests
{
    private static (PhysicsTickSystem sys, EntityManager em, Entity entity) Build(bool hasBreakable)
    {
        var em    = new EntityManager();
        var bus   = new StructuralChangeBus();
        var api   = new WorldMutationApi(em, bus);
        var sound = new SoundTriggerBus();
        var clock = new SimulationClock();
        var cfg   = new PhysicsConfig { GravityPerTick = 0f, MinVelocity = 0.001f, WallHitClampMargin = 0.01f };
        var col   = new CollisionDetector(cfg, 10, 10);
        var sys   = new PhysicsTickSystem(cfg, col, api, sound, clock);

        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 8f, Y = 0f, Z = 5f });
        entity.Add(new MassComponent { MassKilograms = 1f });
        entity.Add(new ThrownVelocityComponent { VelocityX = 10f, DecayPerTick = 0f });

        if (hasBreakable)
            entity.Add(new BreakableComponent { HitEnergyThreshold = 1f, OnBreak = BreakageBehavior.Despawn });

        return (sys, em, entity);
    }

    [Fact]
    public void DespawnBreakable_AboveThreshold_EntityDestroyed()
    {
        var (sys, em, entity) = Build(hasBreakable: true);
        var id = entity.Id;

        sys.Update(em, 1f);

        Assert.DoesNotContain(em.GetAllEntities(), e => e.Id == id);
    }

    [Fact]
    public void DespawnBreakable_NoStainSpawned()
    {
        var (sys, em, entity) = Build(hasBreakable: true);

        sys.Update(em, 1f);

        Assert.DoesNotContain(em.GetAllEntities(), e => e.Has<StainTag>());
    }

    [Fact]
    public void NonBreakable_WallHit_EntitySurvives_NoStain()
    {
        var (sys, em, entity) = Build(hasBreakable: false);
        var id = entity.Id;

        sys.Update(em, 1f);

        // Entity should survive (no BreakableComponent)
        Assert.Contains(em.GetAllEntities(), e => e.Id == id);
        Assert.DoesNotContain(em.GetAllEntities(), e => e.Has<StainTag>());
    }
}
