using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Systems.Animation;
using APIFramework.Systems.Audio;
using Xunit;

namespace APIFramework.Tests.Systems.Animation;

/// <summary>AT-11: Working state emits KeyboardClack.</summary>
public class AnimationSoundTriggerWorkClackTests
{
    [Fact]
    public void OnWorkingCycle_EmitsKeyboardClack()
    {
        var bus      = new SoundTriggerBus();
        var received = new List<SoundTriggerEvent>();
        bus.Subscribe(e => received.Add(e));

        var entityId = Guid.NewGuid();
        AnimationCycleSoundEmitter.OnWorkingCycle(bus, entityId, 2f, 4f, tick: 20L);

        Assert.Single(received);
        Assert.Equal(SoundTriggerKind.KeyboardClack, received[0].Kind);
        Assert.Equal(entityId, received[0].SourceEntityId);
    }

    [Fact]
    public void CycleSoundFor_Working_ReturnsKeyboardClack()
    {
        var kind = AnimationCycleSoundEmitter.CycleSoundFor(NpcAnimationState.Working);
        Assert.Equal(SoundTriggerKind.KeyboardClack, kind);
    }

    [Fact]
    public void OnDrinkingCycle_EmitsSlurp()
    {
        var bus      = new SoundTriggerBus();
        SoundTriggerEvent? received = null;
        bus.Subscribe(e => received = e);

        AnimationCycleSoundEmitter.OnDrinkingCycle(bus, Guid.NewGuid(), 0f, 0f, 5L);

        Assert.NotNull(received);
        Assert.Equal(SoundTriggerKind.Slurp, received!.Value.Kind);
    }

    [Fact]
    public void OnCoughingCycle_EmitsCough()
    {
        var bus      = new SoundTriggerBus();
        SoundTriggerEvent? received = null;
        bus.Subscribe(e => received = e);

        AnimationCycleSoundEmitter.OnCoughingCycle(bus, Guid.NewGuid(), 0f, 0f, 5L);

        Assert.NotNull(received);
        Assert.Equal(SoundTriggerKind.Cough, received!.Value.Kind);
    }

    [Fact]
    public void OnCryingPeriodic_EmitsSigh()
    {
        var bus      = new SoundTriggerBus();
        SoundTriggerEvent? received = null;
        bus.Subscribe(e => received = e);

        AnimationCycleSoundEmitter.OnCryingPeriodic(bus, Guid.NewGuid(), 0f, 0f, 5L);

        Assert.NotNull(received);
        Assert.Equal(SoundTriggerKind.Sigh, received!.Value.Kind);
    }
}
