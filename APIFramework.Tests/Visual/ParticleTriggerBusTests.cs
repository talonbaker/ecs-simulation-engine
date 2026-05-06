using System;
using System.Collections.Generic;
using APIFramework.Systems.Visual;
using Xunit;

namespace APIFramework.Tests.Visual;

/// <summary>
/// AT-01 — Bus emit dispatches to subscribers; subscribers can be added/removed.
/// </summary>
public class ParticleTriggerBusTests
{
    [Fact]
    public void Emit_DispatchesToSubscriber()
    {
        var bus = new ParticleTriggerBus();
        ParticleTriggerEvent? received = null;
        bus.Subscribe(e => received = e);

        bus.Emit(ParticleTriggerKind.Sparks, Guid.NewGuid(), 3f, 4f, 0.8f, 42L);

        Assert.NotNull(received);
        Assert.Equal(ParticleTriggerKind.Sparks, received!.Value.Kind);
        Assert.Equal(3f, received.Value.SourceX);
        Assert.Equal(4f, received.Value.SourceZ);
        Assert.Equal(0.8f, received.Value.IntensityMult, precision: 4);
        Assert.Equal(42L, received.Value.Tick);
    }

    [Fact]
    public void Emit_DispatchesToMultipleSubscribers()
    {
        var bus = new ParticleTriggerBus();
        var received = new List<ParticleTriggerEvent>();
        bus.Subscribe(e => received.Add(e));
        bus.Subscribe(e => received.Add(e));

        bus.Emit(ParticleTriggerKind.CleaningMist, Guid.Empty, 0f, 0f, 1f, 0L);

        Assert.Equal(2, received.Count);
    }

    [Fact]
    public void Unsubscribe_StopsDispatching()
    {
        var bus = new ParticleTriggerBus();
        int callCount = 0;
        Action<ParticleTriggerEvent> handler = _ => callCount++;
        bus.Subscribe(handler);
        bus.Emit(ParticleTriggerKind.Sparks, Guid.Empty, 0f, 0f, 1f, 0L);

        bus.Unsubscribe(handler);
        bus.Emit(ParticleTriggerKind.Sparks, Guid.Empty, 0f, 0f, 1f, 0L);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Emit_ClampsIntensityAboveOne()
    {
        var bus = new ParticleTriggerBus();
        float captured = -1f;
        bus.Subscribe(e => captured = e.IntensityMult);

        bus.Emit(ParticleTriggerKind.SpeechBubblePuff, Guid.Empty, 0f, 0f, 5.0f, 0L);

        Assert.Equal(1.0f, captured, precision: 4);
    }

    [Fact]
    public void Emit_ClampsIntensityBelowZero()
    {
        var bus = new ParticleTriggerBus();
        float captured = -1f;
        bus.Subscribe(e => captured = e.IntensityMult);

        bus.Emit(ParticleTriggerKind.Sparks, Guid.Empty, 0f, 0f, -2.0f, 0L);

        Assert.Equal(0.0f, captured, precision: 4);
    }

    [Fact]
    public void Emit_SequenceIdMonotonicallyIncreases()
    {
        var bus = new ParticleTriggerBus();
        var ids = new List<long>();
        bus.Subscribe(e => ids.Add(e.SequenceId));

        for (int i = 0; i < 5; i++)
            bus.Emit(ParticleTriggerKind.Sparks, Guid.Empty, 0f, 0f, 1f, 0L);

        for (int i = 1; i < ids.Count; i++)
            Assert.True(ids[i] > ids[i - 1], $"SequenceId[{i}] should be > SequenceId[{i-1}]");
    }

    [Fact]
    public void EmitNoSubscribers_DoesNotThrow()
    {
        var bus = new ParticleTriggerBus();
        var ex  = Record.Exception(() =>
            bus.Emit(ParticleTriggerKind.WaterSplash, Guid.Empty, 1f, 2f, 0.5f, 10L));
        Assert.Null(ex);
    }
}
