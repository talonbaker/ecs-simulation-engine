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
/// Tests that LifeStateTransitionSystem emits Wheeze each tick for choking NPCs.
/// </summary>
public class LifeStateIncapacitationWheezeEmitTests
{
    private static (EntityManager em, Entity npc, SoundTriggerBus bus, LifeStateTransitionSystem sys)
        BuildSetup()
    {
        var em           = new EntityManager();
        var clock        = new SimulationClock();
        var narrativeBus = new NarrativeEventBus();
        var soundBus     = new SoundTriggerBus();
        var simConfig    = new SimConfig();

        var sys = new LifeStateTransitionSystem(narrativeBus, em, clock, simConfig, soundBus);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new PositionComponent { X = 5f, Y = 0f, Z = 3f });
        npc.Add(new LifeStateComponent
        {
            State                   = APIFramework.Components.LifeState.Incapacitated,
            IncapacitatedTickBudget = 100,
            PendingDeathCause       = CauseOfDeath.Choked,
        });
        // Mark as choking
        npc.Add(new IsChokingTag());

        return (em, npc, soundBus, sys);
    }

    [Fact]
    public void Wheeze_Emitted_EachTick_ForChokingNpc()
    {
        var (em, npc, bus, sys) = BuildSetup();

        var wheezeEvents = new List<SoundTriggerEvent>();
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Wheeze) wheezeEvents.Add(e); });

        sys.Update(em, 1f);

        Assert.Single(wheezeEvents);
        Assert.Equal(npc.Id, wheezeEvents[0].SourceEntityId);
    }

    [Fact]
    public void Wheeze_EmittedEveryTick_MultipleTicks()
    {
        var (em, npc, bus, sys) = BuildSetup();

        int wheezeCount = 0;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Wheeze) wheezeCount++; });

        for (int i = 0; i < 3; i++)
            sys.Update(em, 1f);

        Assert.True(wheezeCount >= 3, $"Expected >= 3 Wheeze events, got {wheezeCount}");
    }

    [Fact]
    public void Wheeze_HasCorrectIntensity()
    {
        var (em, npc, bus, sys) = BuildSetup();

        SoundTriggerEvent? evt = null;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Wheeze) evt = e; });

        sys.Update(em, 1f);

        Assert.NotNull(evt);
        Assert.Equal(0.4f, evt!.Value.Intensity, 4);
    }

    [Fact]
    public void Wheeze_NotEmitted_ForNonChokingNpc()
    {
        var em           = new EntityManager();
        var clock        = new SimulationClock();
        var narrativeBus = new NarrativeEventBus();
        var soundBus     = new SoundTriggerBus();
        var simConfig    = new SimConfig();
        var sys          = new LifeStateTransitionSystem(narrativeBus, em, clock, simConfig, soundBus);

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = APIFramework.Components.LifeState.Alive });
        // No IsChokingTag

        var wheezeEvents = new List<SoundTriggerEvent>();
        soundBus.Subscribe(e => { if (e.Kind == SoundTriggerKind.Wheeze) wheezeEvents.Add(e); });

        sys.Update(em, 1f);

        Assert.Empty(wheezeEvents);
    }
}
