using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Systems.Animation;
using APIFramework.Systems.Audio;
using Xunit;

namespace APIFramework.Tests.Systems.Animation;

/// <summary>AT-10: Eating state emits Chew at expected cadence.</summary>
public class AnimationSoundTriggerEatChewTests
{
    [Fact]
    public void OnEatingCycle_EmitsChew()
    {
        var bus      = new SoundTriggerBus();
        var received = new List<SoundTriggerEvent>();
        bus.Subscribe(e => received.Add(e));

        var entityId = Guid.NewGuid();
        AnimationCycleSoundEmitter.OnEatingCycle(bus, entityId, 3f, 5f, tick: 10L);

        Assert.Single(received);
        Assert.Equal(SoundTriggerKind.Chew, received[0].Kind);
        Assert.Equal(entityId, received[0].SourceEntityId);
        Assert.Equal(10L, received[0].Tick);
    }

    [Fact]
    public void OnEatingCycle_IntensityIsNonZero()
    {
        var bus      = new SoundTriggerBus();
        SoundTriggerEvent? received = null;
        bus.Subscribe(e => received = e);

        AnimationCycleSoundEmitter.OnEatingCycle(bus, Guid.NewGuid(), 0f, 0f, 1L);

        Assert.NotNull(received);
        Assert.True(received!.Value.Intensity > 0f, "Chew intensity must be > 0.");
    }

    [Fact]
    public void CycleSoundFor_Eating_ReturnsChew()
    {
        var kind = AnimationCycleSoundEmitter.CycleSoundFor(NpcAnimationState.Eating);
        Assert.Equal(SoundTriggerKind.Chew, kind);
    }

    [Fact]
    public void CycleSoundFor_Idle_ReturnsNull()
    {
        var kind = AnimationCycleSoundEmitter.CycleSoundFor(NpcAnimationState.Idle);
        Assert.Null(kind);
    }

    [Fact]
    public void MultipleEatingCycles_EachEmitsChew()
    {
        var bus      = new SoundTriggerBus();
        var received = new List<SoundTriggerEvent>();
        bus.Subscribe(e => received.Add(e));

        var id = Guid.NewGuid();
        AnimationCycleSoundEmitter.OnEatingCycle(bus, id, 1f, 1f, 10L);
        AnimationCycleSoundEmitter.OnEatingCycle(bus, id, 1f, 1f, 30L);
        AnimationCycleSoundEmitter.OnEatingCycle(bus, id, 1f, 1f, 50L);

        Assert.Equal(3, received.Count);
        Assert.All(received, e => Assert.Equal(SoundTriggerKind.Chew, e.Kind));
    }
}
