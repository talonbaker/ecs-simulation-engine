using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Audio;
using APIFramework.Systems.Physics;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Physics;

/// <summary>AT-04: Falling entity stops at Y=0 (floor clamp).</summary>
public class PhysicsTickSystemFloorClampTests
{
    private static (PhysicsTickSystem sys, EntityManager em, Entity entity) Build(float startY, float vy)
    {
        var em    = new EntityManager();
        var bus   = new StructuralChangeBus();
        var api   = new WorldMutationApi(em, bus);
        var sound = new SoundTriggerBus();
        var clock = new SimulationClock();
        var cfg   = new PhysicsConfig { GravityPerTick = 1.5f, MinVelocity = 0.001f, WallHitClampMargin = 0.01f };
        var col   = new CollisionDetector(cfg, 512, 512);
        var sys   = new PhysicsTickSystem(cfg, col, api, sound, clock);

        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 5f, Y = startY, Z = 5f });
        entity.Add(new ThrownVelocityComponent
        {
            VelocityX = 1f, VelocityY = vy, DecayPerTick = 0.0f
        });
        entity.Add(new MassComponent { MassKilograms = 1f });

        return (sys, em, entity);
    }

    [Fact]
    public void AT04_FallingEntity_ClampsToFloorAtYZero()
    {
        var (sys, em, entity) = Build(startY: 0.5f, vy: -10f);

        sys.Update(em, 1f);

        var pos = entity.Get<PositionComponent>();
        Assert.Equal(0f, pos.Y);
    }

    [Fact]
    public void AT04b_EntityAlreadyOnFloor_YStaysZero()
    {
        var (sys, em, entity) = Build(startY: 0f, vy: 0f);

        sys.Update(em, 1f);

        var pos = entity.Get<PositionComponent>();
        Assert.Equal(0f, pos.Y);
    }
}
