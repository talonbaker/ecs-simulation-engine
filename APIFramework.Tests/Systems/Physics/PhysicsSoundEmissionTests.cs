using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Audio;
using APIFramework.Systems.Physics;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Physics;

/// <summary>AT-12: Breakage emits correct sound trigger per kind (Crash vs Glass).</summary>
public class PhysicsSoundEmissionTests
{
    private static (PhysicsTickSystem sys, EntityManager em, SoundTriggerBus sound) Build(
        BreakageBehavior behavior, float mass, float velocity, float threshold)
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
        entity.Add(new MassComponent { MassKilograms = mass });
        entity.Add(new BreakableComponent { HitEnergyThreshold = threshold, OnBreak = behavior });
        entity.Add(new ThrownVelocityComponent { VelocityX = velocity, DecayPerTick = 0f });

        return (sys, em, sound);
    }

    [Fact]
    public void AT12_GlassShards_EmitsGlassSound()
    {
        var (sys, em, sound) = Build(BreakageBehavior.SpawnGlassShards, mass: 5f, velocity: 5f, threshold: 20f);

        var events = new List<SoundTriggerEvent>();
        sound.Subscribe(e => events.Add(e));

        sys.Update(em, 1f);

        Assert.Contains(events, e => e.Kind == SoundTriggerKind.Glass);
    }

    [Fact]
    public void AT12_LiquidStain_EmitsCrashSound()
    {
        // mug: mass=0.4, v=10, KE=20 > threshold 8
        var (sys, em, sound) = Build(BreakageBehavior.SpawnLiquidStain, mass: 0.4f, velocity: 10f, threshold: 8f);

        var events = new List<SoundTriggerEvent>();
        sound.Subscribe(e => events.Add(e));

        sys.Update(em, 1f);

        Assert.Contains(events, e => e.Kind == SoundTriggerKind.Crash);
    }

    [Fact]
    public void AT12_Despawn_EmitsCrashSound()
    {
        var (sys, em, sound) = Build(BreakageBehavior.Despawn, mass: 1.2f, velocity: 10f, threshold: 1f);

        var events = new List<SoundTriggerEvent>();
        sound.Subscribe(e => events.Add(e));

        sys.Update(em, 1f);

        Assert.Contains(events, e => e.Kind == SoundTriggerKind.Crash);
    }

    [Fact]
    public void SoundEmitted_IntensityScaledByHitEnergy()
    {
        var (sys, em, sound) = Build(BreakageBehavior.Despawn, mass: 1f, velocity: 10f, threshold: 1f);

        var events = new List<SoundTriggerEvent>();
        sound.Subscribe(e => events.Add(e));

        sys.Update(em, 1f);

        Assert.NotEmpty(events);
        Assert.True(events[0].Intensity > 0f && events[0].Intensity <= 1f);
    }
}
