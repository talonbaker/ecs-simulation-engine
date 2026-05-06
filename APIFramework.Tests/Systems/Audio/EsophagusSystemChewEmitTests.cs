using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Audio;
using Xunit;

namespace APIFramework.Tests.Systems.Audio;

/// <summary>
/// Tests that EsophagusSystem emits Chew when a bolus advances to the stomach,
/// and Slurp when a liquid advances.
/// </summary>
public class EsophagusSystemChewEmitTests
{
    private static (EntityManager em, Entity consumer, Entity transit, SoundTriggerBus bus, EsophagusSystem sys)
        BuildBolusSetup(float speed = 2f, float progress = 0.0f)
    {
        var em  = new EntityManager();
        var bus = new SoundTriggerBus();
        var sys = new EsophagusSystem(bus);

        // Consumer entity with stomach
        var consumer = em.CreateEntity();
        consumer.Add(new StomachComponent { CurrentVolumeMl = 0f, NutrientsQueued = default });
        consumer.Add(new PositionComponent { X = 3f, Y = 0f, Z = 5f });

        // Transit entity (bolus) heading toward consumer
        var transit = em.CreateEntity();
        transit.Add(new EsophagusTransitComponent
        {
            TargetEntityId = consumer.Id,
            Progress       = progress,
            Speed          = speed,
        });
        transit.Add(new BolusComponent { Volume = 50f, Nutrients = default });

        return (em, consumer, transit, bus, sys);
    }

    [Fact]
    public void Chew_Emitted_WhenBolus_ArrivesAtStomach()
    {
        var (em, consumer, transit, bus, sys) = BuildBolusSetup(speed: 2f, progress: 0.0f);

        var chewEvents = new List<SoundTriggerEvent>();
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Chew) chewEvents.Add(e); });

        // deltaTime=1f, speed=2f → progress=2.0 >= 1.0 → arrives
        sys.Update(em, 1f);

        Assert.Single(chewEvents);
        Assert.Equal(SoundTriggerKind.Chew, chewEvents[0].Kind);
        Assert.Equal(consumer.Id, chewEvents[0].SourceEntityId);
    }

    [Fact]
    public void Chew_NotEmitted_WhenBolus_StillInTransit()
    {
        var (em, consumer, transit, bus, sys) = BuildBolusSetup(speed: 0.3f, progress: 0.0f);

        var chewEvents = new List<SoundTriggerEvent>();
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Chew) chewEvents.Add(e); });

        // progress stays below 1.0
        sys.Update(em, 1f);

        Assert.Empty(chewEvents);
    }

    [Fact]
    public void Slurp_Emitted_WhenLiquid_ArrivesAtStomach()
    {
        var em  = new EntityManager();
        var bus = new SoundTriggerBus();
        var sys = new EsophagusSystem(bus);

        var consumer = em.CreateEntity();
        consumer.Add(new StomachComponent { CurrentVolumeMl = 0f, NutrientsQueued = default });
        consumer.Add(new PositionComponent { X = 1f, Y = 0f, Z = 2f });

        var transit = em.CreateEntity();
        transit.Add(new EsophagusTransitComponent
        {
            TargetEntityId = consumer.Id,
            Progress       = 0f,
            Speed          = 2f,
        });
        transit.Add(new LiquidComponent { VolumeMl = 15f, Nutrients = default });

        var slurpEvents = new List<SoundTriggerEvent>();
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Slurp) slurpEvents.Add(e); });

        sys.Update(em, 1f);

        Assert.Single(slurpEvents);
        Assert.Equal(SoundTriggerKind.Slurp, slurpEvents[0].Kind);
    }

    [Fact]
    public void Chew_UsesConsumerPosition()
    {
        var (em, consumer, transit, bus, sys) = BuildBolusSetup(speed: 2f, progress: 0.0f);

        SoundTriggerEvent? evt = null;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Chew) evt = e; });

        sys.Update(em, 1f);

        Assert.NotNull(evt);
        Assert.Equal(3f, evt!.Value.SourceX, 4);
        Assert.Equal(5f, evt!.Value.SourceZ, 4);
    }
}
