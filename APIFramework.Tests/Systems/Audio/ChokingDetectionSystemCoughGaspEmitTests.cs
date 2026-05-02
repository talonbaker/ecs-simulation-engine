using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Audio;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.Audio;

/// <summary>
/// Tests that ChokingDetectionSystem emits Cough and Gasp at choke onset.
/// </summary>
public class ChokingDetectionSystemCoughGaspEmitTests
{
    private static ChokingConfig MakeCfg() => new()
    {
        BolusSizeThreshold      = 0.5f,
        EnergyThreshold         = 40,
        StressThreshold         = 60,
        IrritationThreshold     = 70,
        IncapacitationTicks     = 10,
        PanicMoodIntensity      = 0.8f,
        EmitChokeStartedNarrative = false,
    };

    private static (EntityManager em, Entity npc, SoundTriggerBus bus, ChokingDetectionSystem sys,
                    LifeStateTransitionSystem transition)
        BuildSetup()
    {
        var em         = new EntityManager();
        var clock      = new SimulationClock();
        var narrativeBus = new NarrativeEventBus();
        var soundBus   = new SoundTriggerBus();
        var cfg        = MakeCfg();
        var simConfig  = new SimConfig();

        var transition = new LifeStateTransitionSystem(narrativeBus, em, clock, simConfig);
        var sys        = new ChokingDetectionSystem(transition, narrativeBus, clock, cfg, em, soundBus);

        // Create a choking-eligible NPC
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 2f, Y = 0f, Z = 4f });
        npc.Add(new LifeStateComponent { State = LS.Alive });
        npc.Add(new MoodComponent());

        // EsophagusTransitComponent lives on the bolus entity; TargetEntityId = the NPC swallowing it
        var bolusEntity = em.CreateEntity();
        bolusEntity.Add(new BolusComponent { Toughness = 0.8f }); // above threshold 0.5
        bolusEntity.Add(new EsophagusTransitComponent { TargetEntityId = npc.Id, Progress = 0.5f, Speed = 0.1f });

        // Low energy → distracted
        npc.Add(new EnergyComponent { Energy = 20f }); // below EnergyThreshold 40

        // EsophagusTransitComponent lives on the BOLUS entity; TargetEntityId = the NPC.
        // Toughness > BolusSizeThreshold(0.5) triggers choke.
        var bolusEntity = em.CreateEntity();
        bolusEntity.Add(new BolusComponent { Toughness = 0.8f });
        bolusEntity.Add(new EsophagusTransitComponent { TargetEntityId = npc.Id, Progress = 0.5f, Speed = 0.1f });

        return (em, npc, soundBus, sys, transition);
    }

    [Fact]
    public void Cough_Emitted_AtChokeOnset()
    {
        var (em, npc, bus, sys, _) = BuildSetup();

        var events = new List<SoundTriggerEvent>();
        bus.Subscribe(e => events.Add(e));

        sys.Update(em, 1f);

        Assert.Contains(events, e => e.Kind == SoundTriggerKind.Cough);
    }

    [Fact]
    public void Gasp_Emitted_AtChokeOnset()
    {
        var (em, npc, bus, sys, _) = BuildSetup();

        var events = new List<SoundTriggerEvent>();
        bus.Subscribe(e => events.Add(e));

        sys.Update(em, 1f);

        Assert.Contains(events, e => e.Kind == SoundTriggerKind.Gasp);
    }

    [Fact]
    public void CoughAndGasp_HaveCorrectEntityId()
    {
        var (em, npc, bus, sys, _) = BuildSetup();

        var events = new List<SoundTriggerEvent>();
        bus.Subscribe(e => events.Add(e));

        sys.Update(em, 1f);

        var cough = events.Find(e => e.Kind == SoundTriggerKind.Cough);
        var gasp  = events.Find(e => e.Kind == SoundTriggerKind.Gasp);

        Assert.Equal(npc.Id, cough.SourceEntityId);
        Assert.Equal(npc.Id, gasp.SourceEntityId);
    }

    [Fact]
    public void Cough_HasExpectedIntensity()
    {
        var (em, npc, bus, sys, _) = BuildSetup();

        SoundTriggerEvent? cough = null;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Cough) cough = e; });

        sys.Update(em, 1f);

        Assert.NotNull(cough);
        Assert.Equal(0.6f, cough!.Value.Intensity, 4);
    }

    [Fact]
    public void Gasp_HasExpectedIntensity()
    {
        var (em, npc, bus, sys, _) = BuildSetup();

        SoundTriggerEvent? gasp = null;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Gasp) gasp = e; });

        sys.Update(em, 1f);

        Assert.NotNull(gasp);
        Assert.Equal(0.7f, gasp!.Value.Intensity, 4);
    }

    [Fact]
    public void NoCoughOrGasp_WhenNpcAlreadyChoking()
    {
        var (em, npc, bus, sys, _) = BuildSetup();
        // Pre-attach IsChokingTag so system skips this NPC
        npc.Add(new IsChokingTag());

        var events = new List<SoundTriggerEvent>();
        bus.Subscribe(e => events.Add(e));

        sys.Update(em, 1f);

        Assert.DoesNotContain(events, e => e.Kind == SoundTriggerKind.Cough);
        Assert.DoesNotContain(events, e => e.Kind == SoundTriggerKind.Gasp);
    }
}
