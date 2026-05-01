using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Audio;
using APIFramework.Systems.Lighting;
using Xunit;

namespace APIFramework.Tests.Systems.Audio;

/// <summary>
/// Tests that LightSourceStateSystem emits BulbBuzz at the configured interval for flickering lights.
/// </summary>
public class LightSourceFlickerBuzzEmitTests
{
    private static Entity SpawnFlickeringSource(EntityManager em)
    {
        var e = em.CreateEntity();
        e.Add(new LightSourceTag());
        e.Add(new LightSourceComponent
        {
            Id                = Guid.NewGuid().ToString(),
            Kind              = LightKind.OverheadFluorescent,
            State             = LightState.Flickering,
            Intensity         = 80,
            ColorTemperatureK = 4000,
            TileX             = 5,
            TileY             = 5,
            RoomId            = "r1",
        });
        e.Add(new PositionComponent { X = 5f, Y = 3f, Z = 5f });
        return e;
    }

    [Fact]
    public void BulbBuzz_NotEmitted_BeforeInterval()
    {
        var em      = new EntityManager();
        var rng     = new SeededRandom(42);
        var cfg     = new LightingConfig();
        var soundCfg = new SoundTriggerConfig { BulbBuzzEmitIntervalTicks = 5 };
        var bus     = new SoundTriggerBus();
        var sys     = new LightSourceStateSystem(rng, cfg, soundCfg, bus);

        SpawnFlickeringSource(em);

        var buzzEvents = new List<SoundTriggerEvent>();
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.BulbBuzz) buzzEvents.Add(e); });

        // Run 4 ticks (< interval of 5)
        for (int i = 0; i < 4; i++)
            sys.Update(em, 1f);

        Assert.Empty(buzzEvents);
    }

    [Fact]
    public void BulbBuzz_EmittedAtInterval()
    {
        var em       = new EntityManager();
        var rng      = new SeededRandom(42);
        var cfg      = new LightingConfig();
        var soundCfg = new SoundTriggerConfig { BulbBuzzEmitIntervalTicks = 5 };
        var bus      = new SoundTriggerBus();
        var sys      = new LightSourceStateSystem(rng, cfg, soundCfg, bus);

        SpawnFlickeringSource(em);

        var buzzEvents = new List<SoundTriggerEvent>();
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.BulbBuzz) buzzEvents.Add(e); });

        // Run exactly 5 ticks — should emit at tick 5
        for (int i = 0; i < 5; i++)
            sys.Update(em, 1f);

        Assert.Single(buzzEvents);
    }

    [Fact]
    public void BulbBuzz_EmittedTwice_AfterTwoIntervals()
    {
        var em       = new EntityManager();
        var rng      = new SeededRandom(42);
        var cfg      = new LightingConfig();
        var soundCfg = new SoundTriggerConfig { BulbBuzzEmitIntervalTicks = 5 };
        var bus      = new SoundTriggerBus();
        var sys      = new LightSourceStateSystem(rng, cfg, soundCfg, bus);

        SpawnFlickeringSource(em);

        var buzzEvents = new List<SoundTriggerEvent>();
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.BulbBuzz) buzzEvents.Add(e); });

        // 10 ticks → 2 intervals
        for (int i = 0; i < 10; i++)
            sys.Update(em, 1f);

        Assert.Equal(2, buzzEvents.Count);
    }

    [Fact]
    public void BulbBuzz_HasCorrectIntensity()
    {
        var em       = new EntityManager();
        var rng      = new SeededRandom(42);
        var cfg      = new LightingConfig();
        var soundCfg = new SoundTriggerConfig { BulbBuzzEmitIntervalTicks = 1 };
        var bus      = new SoundTriggerBus();
        var sys      = new LightSourceStateSystem(rng, cfg, soundCfg, bus);

        SpawnFlickeringSource(em);

        SoundTriggerEvent? evt = null;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.BulbBuzz) evt = e; });

        sys.Update(em, 1f);

        Assert.NotNull(evt);
        Assert.Equal(0.2f, evt!.Value.Intensity, 4);
    }

    [Fact]
    public void BulbBuzz_NotEmitted_ForNonFlickeringSource()
    {
        var em       = new EntityManager();
        var rng      = new SeededRandom(42);
        var cfg      = new LightingConfig();
        var soundCfg = new SoundTriggerConfig { BulbBuzzEmitIntervalTicks = 1 };
        var bus      = new SoundTriggerBus();
        var sys      = new LightSourceStateSystem(rng, cfg, soundCfg, bus);

        // On source (not flickering)
        var e = em.CreateEntity();
        e.Add(new LightSourceTag());
        e.Add(new LightSourceComponent
        {
            Id = Guid.NewGuid().ToString(), Kind = LightKind.OverheadFluorescent,
            State = LightState.On, Intensity = 80,
            ColorTemperatureK = 4000, TileX = 1, TileY = 1, RoomId = "r1",
        });

        var buzzEvents = new List<SoundTriggerEvent>();
        bus.Subscribe(ev => { if (ev.Kind == SoundTriggerKind.BulbBuzz) buzzEvents.Add(ev); });

        sys.Update(em, 1f);

        Assert.Empty(buzzEvents);
    }
}
