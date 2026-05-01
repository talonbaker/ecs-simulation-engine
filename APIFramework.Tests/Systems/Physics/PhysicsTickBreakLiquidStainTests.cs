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

/// <summary>AT-06: Mug thrown at high velocity → despawns mug; spawns water-puddle stain with FallRisk.</summary>
public class PhysicsTickBreakLiquidStainTests
{
    private static (PhysicsTickSystem sys, EntityManager em, Entity mug) Build()
    {
        var em    = new EntityManager();
        var bus   = new StructuralChangeBus();
        var api   = new WorldMutationApi(em, bus);
        var sound = new SoundTriggerBus();
        var clock = new SimulationClock();
        var cfg   = new PhysicsConfig { GravityPerTick = 0f, MinVelocity = 0.001f, WallHitClampMargin = 0.01f };
        var col   = new CollisionDetector(cfg, 10, 10);
        var sys   = new PhysicsTickSystem(cfg, col, api, sound, clock);

        // Mug near right wall with high velocity so it hits wall with high energy
        // mass=0.4, velocity=10 → KE = 0.5 * 0.4 * 100 = 20 J > threshold 8
        var mug = em.CreateEntity();
        mug.Add(new PositionComponent { X = 8f, Y = 0f, Z = 5f });
        mug.Add(new MassComponent { MassKilograms = 0.4f });
        mug.Add(new BreakableComponent
        {
            HitEnergyThreshold = 8f,
            OnBreak = BreakageBehavior.SpawnLiquidStain
        });
        mug.Add(new ThrownVelocityComponent { VelocityX = 10f, DecayPerTick = 0f });

        return (sys, em, mug);
    }

    [Fact]
    public void AT06_MugHitsWall_AboveThreshold_Despawns()
    {
        var (sys, em, mug) = Build();
        var mugId = mug.Id;

        sys.Update(em, 1f);

        Assert.DoesNotContain(em.GetAllEntities(), e => e.Id == mugId);
    }

    [Fact]
    public void AT06_MugBreaks_SpawnsStainWithFallRisk()
    {
        var (sys, em, mug) = Build();

        sys.Update(em, 1f);

        var stain = em.GetAllEntities().FirstOrDefault(e => e.Has<StainTag>());
        Assert.NotEqual(default, stain);
        Assert.True(stain!.Has<FallRiskComponent>());
        Assert.True(stain.Get<FallRiskComponent>().RiskLevel > 0f);
    }

    [Fact]
    public void AT06_MugBreaks_StainHasStainComponent()
    {
        var (sys, em, mug) = Build();

        sys.Update(em, 1f);

        var stain = em.GetAllEntities().FirstOrDefault(e => e.Has<StainTag>());
        Assert.NotEqual(default, stain);
        Assert.True(stain!.Has<StainComponent>());
    }
}
