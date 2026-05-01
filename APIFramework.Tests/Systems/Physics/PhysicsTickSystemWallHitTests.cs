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

/// <summary>AT-05/AT-07: Entity traveling into wall → clamps and stops (non-breakable).</summary>
public class PhysicsTickSystemWallHitTests
{
    private static (PhysicsTickSystem sys, EntityManager em, Entity entity)
        Build(float startX, float vx, bool breakable = false, float threshold = 999f)
    {
        var em    = new EntityManager();
        var bus   = new StructuralChangeBus();
        var api   = new WorldMutationApi(em, bus);
        var sound = new SoundTriggerBus();
        var clock = new SimulationClock();
        var cfg   = new PhysicsConfig { GravityPerTick = 0f, MinVelocity = 0.001f, WallHitClampMargin = 0.01f };
        var col   = new CollisionDetector(cfg, 10, 10);  // small world to hit walls easily
        var sys   = new PhysicsTickSystem(cfg, col, api, sound, clock);

        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = startX, Y = 0f, Z = 5f });
        entity.Add(new MassComponent { MassKilograms = 1f });

        if (breakable)
            entity.Add(new BreakableComponent
            {
                HitEnergyThreshold = threshold,
                OnBreak = BreakageBehavior.Despawn
            });

        entity.Add(new ThrownVelocityComponent { VelocityX = vx, DecayPerTick = 0f });

        return (sys, em, entity);
    }

    [Fact]
    public void AT07_StaplerHitsWall_NoBreak_ClampsAndRemovesThrown()
    {
        // stapler under threshold — no break
        var (sys, em, entity) = Build(startX: 8f, vx: 5f, breakable: true, threshold: 999f);

        sys.Update(em, 1f);

        // Entity should be clamped at world boundary, not destroyed
        Assert.Contains(em.GetAllEntities(), e => e.Id == entity.Id);
        Assert.False(entity.Has<ThrownVelocityComponent>());
        Assert.False(entity.Has<ThrownTag>());

        var pos = entity.Get<PositionComponent>();
        Assert.True(pos.X <= 9f); // clamped inside world
    }

    [Fact]
    public void AT05_NonBreakable_WallHit_ClampsAndStops()
    {
        var (sys, em, entity) = Build(startX: 8f, vx: 10f);

        sys.Update(em, 1f);

        Assert.False(entity.Has<ThrownVelocityComponent>());
        var pos = entity.Get<PositionComponent>();
        Assert.True(pos.X < 10f);
    }
}
