using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Audio;
using Xunit;

namespace APIFramework.Tests.Systems.Audio;

/// <summary>
/// Tests that MovementSystem emits Footstep when an NPC moves >= 1 tile.
/// </summary>
public class MovementSystemFootstepEmitTests
{
    private static (EntityManager em, Entity npc, SoundTriggerBus bus, MovementSystem sys)
        BuildSetup(float startX = 0f, float startZ = 0f, float targetX = 5f, float targetZ = 0f, float speed = 5f)
    {
        var em  = new EntityManager();
        var rng = new SeededRandom(42);
        var bus = new SoundTriggerBus();
        var sys = new MovementSystem(rng, bus)
        {
            WorldMinX = -10f, WorldMaxX = 10f,
            WorldMinZ = -10f, WorldMaxZ = 10f,
        };

        var npc = em.CreateEntity();
        npc.Add(new PositionComponent { X = startX, Y = 0f, Z = startZ });
        npc.Add(new MovementComponent { Speed = speed, SpeedModifier = 1f, ArrivalDistance = 0.1f });
        npc.Add(new MovementTargetComponent { TargetEntityId = Guid.NewGuid() });

        // Place a world object at the target so MovementSystem can steer toward it
        var target = em.CreateEntity();
        target.Add(new FridgeComponent());
        target.Add(new PositionComponent { X = targetX, Y = 0f, Z = targetZ });
        npc.Get<MovementTargetComponent>(); // re-read to get the id
        // Override to target the fridge entity
        npc.Add(new MovementTargetComponent { TargetEntityId = target.Id });

        return (em, npc, bus, sys);
    }

    [Fact]
    public void Footstep_Emitted_WhenNpcMoves_AtLeastOneTile()
    {
        var em  = new EntityManager();
        var rng = new SeededRandom(42);
        var bus = new SoundTriggerBus();
        var sys = new MovementSystem(rng, bus)
        {
            WorldMinX = -10f, WorldMaxX = 10f,
            WorldMinZ = -10f, WorldMaxZ = 10f,
        };

        var npc = em.CreateEntity();
        npc.Add(new PositionComponent { X = 0f, Y = 0f, Z = 0f });
        npc.Add(new MovementComponent { Speed = 5f, SpeedModifier = 1f, ArrivalDistance = 0.1f });

        // Use a fridge as world target
        var fridge = em.CreateEntity();
        fridge.Add(new FridgeComponent());
        fridge.Add(new PositionComponent { X = 10f, Y = 0f, Z = 0f });
        npc.Add(new MovementTargetComponent { TargetEntityId = fridge.Id });

        var footsteps = new List<SoundTriggerEvent>();
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Footstep) footsteps.Add(e); });

        // deltaTime=1f, speed=5f → moves 5 tiles → >= 1.0f
        sys.Update(em, 1f);

        Assert.NotEmpty(footsteps);
        Assert.Equal(SoundTriggerKind.Footstep, footsteps[0].Kind);
        Assert.Equal(npc.Id, footsteps[0].SourceEntityId);
    }

    [Fact]
    public void Footstep_NotEmitted_WhenNpcAtArrivalDistance()
    {
        var em  = new EntityManager();
        var rng = new SeededRandom(42);
        var bus = new SoundTriggerBus();
        var sys = new MovementSystem(rng, bus)
        {
            WorldMinX = -10f, WorldMaxX = 10f,
            WorldMinZ = -10f, WorldMaxZ = 10f,
        };

        var npc = em.CreateEntity();
        npc.Add(new PositionComponent { X = 10f, Y = 0f, Z = 0f });
        npc.Add(new MovementComponent { Speed = 1f, SpeedModifier = 1f, ArrivalDistance = 0.5f });

        var fridge = em.CreateEntity();
        fridge.Add(new FridgeComponent());
        fridge.Add(new PositionComponent { X = 10f, Y = 0f, Z = 0f }); // same position → arrival
        npc.Add(new MovementTargetComponent { TargetEntityId = fridge.Id });

        var footsteps = new List<SoundTriggerEvent>();
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Footstep) footsteps.Add(e); });

        sys.Update(em, 1f);

        Assert.Empty(footsteps);
    }

    [Fact]
    public void Footstep_PositionMatchesNewEntityPosition()
    {
        var em  = new EntityManager();
        var rng = new SeededRandom(99);
        var bus = new SoundTriggerBus();
        var sys = new MovementSystem(rng, bus)
        {
            WorldMinX = -20f, WorldMaxX = 20f,
            WorldMinZ = -20f, WorldMaxZ = 20f,
        };

        var npc = em.CreateEntity();
        npc.Add(new PositionComponent { X = 0f, Y = 0f, Z = 0f });
        npc.Add(new MovementComponent { Speed = 10f, SpeedModifier = 1f, ArrivalDistance = 0.1f });

        var fridge = em.CreateEntity();
        fridge.Add(new FridgeComponent());
        fridge.Add(new PositionComponent { X = 15f, Y = 0f, Z = 0f });
        npc.Add(new MovementTargetComponent { TargetEntityId = fridge.Id });

        SoundTriggerEvent? evt = null;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Footstep) evt = e; });

        sys.Update(em, 1f);

        Assert.NotNull(evt);
        // New position should match the emitted X coordinate
        var newPos = npc.Get<PositionComponent>();
        Assert.Equal(newPos.X, evt!.Value.SourceX, 4);
        Assert.Equal(newPos.Z, evt!.Value.SourceZ, 4);
    }
}
