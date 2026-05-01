using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Audio;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems.Audio;

/// <summary>
/// Tests that SlipAndFallSystem emits Slip + Thud when a slip occurs.
/// </summary>
public class SlipAndFallSlipThudEmitTests
{
    /// <summary>
    /// Build a SlipAndFallSystem setup with a guaranteed slip (roll always < slipChance).
    /// GlobalSlipChanceScale = 1.0 with risk=1.0 gives slipChance=1.0, guaranteeing slip.
    /// </summary>
    private static (EntityManager em, Entity npc, SoundTriggerBus bus, SlipAndFallSystem sys)
        BuildGuaranteedSlipSetup()
    {
        var em           = new EntityManager();
        var clock        = new SimulationClock();
        var narrativeBus = new NarrativeEventBus();
        var soundBus     = new SoundTriggerBus();
        var rng          = new SeededRandom(1);

        var simConfig = new SimConfig();
        simConfig.SlipAndFall.GlobalSlipChanceScale = 2.0f; // guarantee slip at risk=1.0

        var transition = new LifeStateTransitionSystem(narrativeBus, em, clock, simConfig);
        var sys        = new SlipAndFallSystem(em, clock, simConfig, transition, rng, soundBus);

        // Create alive NPC
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 3f, Y = 0f, Z = 7f });
        npc.Add(new MovementComponent { Speed = 1f, SpeedModifier = 1.0f });
        npc.Add(new LifeStateComponent { State = LifeState.Alive });

        // Create hazard at same tile
        var hazard = em.CreateEntity();
        hazard.Add(new FallRiskComponent { RiskLevel = 1.0f });
        hazard.Add(new PositionComponent { X = 3f, Y = 0f, Z = 7f });

        return (em, npc, soundBus, sys);
    }

    [Fact]
    public void Slip_Emitted_OnSlipEvent()
    {
        var (em, npc, bus, sys) = BuildGuaranteedSlipSetup();

        var events = new List<SoundTriggerEvent>();
        bus.Subscribe(e => events.Add(e));

        sys.Update(em, 1f);

        Assert.Contains(events, e => e.Kind == SoundTriggerKind.Slip);
    }

    [Fact]
    public void Thud_Emitted_OnSlipEvent()
    {
        var (em, npc, bus, sys) = BuildGuaranteedSlipSetup();

        var events = new List<SoundTriggerEvent>();
        bus.Subscribe(e => events.Add(e));

        sys.Update(em, 1f);

        Assert.Contains(events, e => e.Kind == SoundTriggerKind.Thud);
    }

    [Fact]
    public void Slip_HasCorrectEntityId()
    {
        var (em, npc, bus, sys) = BuildGuaranteedSlipSetup();

        SoundTriggerEvent? slip = null;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Slip) slip = e; });

        sys.Update(em, 1f);

        Assert.NotNull(slip);
        Assert.Equal(npc.Id, slip!.Value.SourceEntityId);
    }

    [Fact]
    public void Slip_HasCorrectIntensity()
    {
        var (em, npc, bus, sys) = BuildGuaranteedSlipSetup();

        SoundTriggerEvent? slip = null;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Slip) slip = e; });

        sys.Update(em, 1f);

        Assert.NotNull(slip);
        Assert.Equal(0.8f, slip!.Value.Intensity, 4);
    }

    [Fact]
    public void Thud_HasCorrectIntensity()
    {
        var (em, npc, bus, sys) = BuildGuaranteedSlipSetup();

        SoundTriggerEvent? thud = null;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Thud) thud = e; });

        sys.Update(em, 1f);

        Assert.NotNull(thud);
        Assert.Equal(0.9f, thud!.Value.Intensity, 4);
    }

    [Fact]
    public void Slip_PositionMatchesNpcPosition()
    {
        var (em, npc, bus, sys) = BuildGuaranteedSlipSetup();

        SoundTriggerEvent? slip = null;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Slip) slip = e; });

        sys.Update(em, 1f);

        Assert.NotNull(slip);
        Assert.Equal(3f, slip!.Value.SourceX, 4);
        Assert.Equal(7f, slip!.Value.SourceZ, 4);
    }
}
