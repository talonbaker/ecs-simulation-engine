using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Audio;
using APIFramework.Systems.Physics;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Physics;

/// <summary>AT-03: Velocity decays each tick at decayPerTick rate.</summary>
public class PhysicsTickSystemDecayTests
{
    private static (PhysicsTickSystem sys, EntityManager em, Entity entity) Build(float vx, float decay)
    {
        var em    = new EntityManager();
        var bus   = new StructuralChangeBus();
        var api   = new WorldMutationApi(em, bus);
        var sound = new SoundTriggerBus();
        var clock = new SimulationClock();
        var cfg   = new PhysicsConfig { GravityPerTick = 0f, MinVelocity = 0.001f, WallHitClampMargin = 0.01f };
        var col   = new CollisionDetector(cfg, 512, 512);
        var sys   = new PhysicsTickSystem(cfg, col, api, sound, clock);

        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = 5f, Y = 1f, Z = 5f });
        entity.Add(new ThrownVelocityComponent { VelocityX = vx, DecayPerTick = decay });
        entity.Add(new MassComponent { MassKilograms = 1f });

        return (sys, em, entity);
    }

    [Fact]
    public void AT03_VelocityDecays_ByDecayPerTickFraction()
    {
        var (sys, em, entity) = Build(vx: 10f, decay: 0.10f);

        sys.Update(em, 1f);

        var v = entity.Get<ThrownVelocityComponent>();
        Assert.Equal(9f, v.VelocityX, 4);
    }

    [Fact]
    public void AT03b_TwoTicks_DecaysCompound()
    {
        var (sys, em, entity) = Build(vx: 10f, decay: 0.10f);

        sys.Update(em, 1f);
        sys.Update(em, 1f);

        var v = entity.Get<ThrownVelocityComponent>();
        Assert.Equal(8.1f, v.VelocityX, 4);
    }
}
