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

/// <summary>Window-pane breakage spawns broken-glass stain with FallRisk.</summary>
public class PhysicsTickBreakGlassShardsTests
{
    private static (PhysicsTickSystem sys, EntityManager em, Entity pane) Build()
    {
        var em    = new EntityManager();
        var bus   = new StructuralChangeBus();
        var api   = new WorldMutationApi(em, bus);
        var sound = new SoundTriggerBus();
        var clock = new SimulationClock();
        var cfg   = new PhysicsConfig { GravityPerTick = 0f, MinVelocity = 0.001f, WallHitClampMargin = 0.01f };
        var col   = new CollisionDetector(cfg, 10, 10);
        var sys   = new PhysicsTickSystem(cfg, col, api, sound, clock);

        // Window: mass=5, threshold=20, v=5 → KE=0.5*5*25=62.5 J > 20
        var pane = em.CreateEntity();
        pane.Add(new PositionComponent { X = 8f, Y = 0f, Z = 5f });
        pane.Add(new MassComponent { MassKilograms = 5f });
        pane.Add(new BreakableComponent
        {
            HitEnergyThreshold = 20f,
            OnBreak = BreakageBehavior.SpawnGlassShards
        });
        pane.Add(new ThrownVelocityComponent { VelocityX = 5f, DecayPerTick = 0f });

        return (sys, em, pane);
    }

    [Fact]
    public void WindowPaneBreaks_SpawnsGlassStainWithFallRisk()
    {
        var (sys, em, pane) = Build();
        var paneId = pane.Id;

        sys.Update(em, 1f);

        Assert.DoesNotContain(em.GetAllEntities(), e => e.Id == paneId);

        var stain = em.GetAllEntities().FirstOrDefault(e => e.Has<StainTag>());
        Assert.NotEqual(default, stain);
        Assert.True(stain!.Has<FallRiskComponent>());
        var src = stain.Get<StainComponent>().Source;
        Assert.Contains("glass", src);
    }
}
