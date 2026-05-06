using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Audio;
using APIFramework.Systems.Physics;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Physics;

/// <summary>AT-02: Entity with ThrownVelocityComponent {5, 0, 0, 0.10} advances X by velocity each tick.</summary>
public class PhysicsTickSystemPositionAdvanceTests
{
    private static (PhysicsTickSystem sys, EntityManager em, Entity entity) Build(
        float vx = 5f, float vz = 0f, float vy = 0f, float decay = 0.10f,
        float startX = 5f, float startZ = 5f, float startY = 1f)
    {
        var em     = new EntityManager();
        var bus    = new StructuralChangeBus();
        var api    = new WorldMutationApi(em, bus);
        var sound  = new SoundTriggerBus();
        var clock  = new SimulationClock();
        var cfg    = new PhysicsConfig { GravityPerTick = 0f, MinVelocity = 0.001f, WallHitClampMargin = 0.01f };
        var col    = new CollisionDetector(cfg, 512, 512);
        var sys    = new PhysicsTickSystem(cfg, col, api, sound, clock);

        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = startX, Y = startY, Z = startZ });
        entity.Add(new ThrownVelocityComponent
        {
            VelocityX = vx, VelocityZ = vz, VelocityY = vy, DecayPerTick = decay
        });
        entity.Add(new MassComponent { MassKilograms = 0.4f });

        return (sys, em, entity);
    }

    [Fact]
    public void AT02_PositionAdvances_ByVelocity_EachTick()
    {
        var (sys, em, entity) = Build(vx: 5f, startX: 5f, startY: 1f);

        sys.Update(em, 1f);

        var pos = entity.Get<PositionComponent>();
        Assert.Equal(10f, pos.X, 3);
    }

    [Fact]
    public void AT02b_PositionAdvances_Z_ByVelocityZ()
    {
        var (sys, em, entity) = Build(vx: 0f, vz: 3f, startX: 5f, startZ: 5f, startY: 1f);

        sys.Update(em, 1f);

        var pos = entity.Get<PositionComponent>();
        Assert.Equal(8f, pos.Z, 3);
    }

    [Fact]
    public void AT02c_ThrownVelocityComponent_RemovedWhenVelocityDecaysToMin()
    {
        // tiny velocity + high decay — should stop in one tick
        var (sys, em, entity) = Build(vx: 0.03f, decay: 0.99f, startX: 5f, startY: 0f);

        sys.Update(em, 1f);

        Assert.False(entity.Has<ThrownVelocityComponent>());
        Assert.False(entity.Has<ThrownTag>());
    }
}
