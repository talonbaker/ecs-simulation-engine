using System.Collections.Generic;
using APIFramework.Systems.Audio;
using Xunit;

namespace APIFramework.Tests.Systems.Audio;

public class SoundTriggerBusEmitTests
{
    [Fact]
    public void AT01_Emit_InvokesSubscriber_WithCorrectFields()
    {
        var bus = new SoundTriggerBus();
        SoundTriggerEvent? received = null;
        bus.Subscribe(e => received = e);

        var sourceId = System.Guid.NewGuid();
        bus.Emit(SoundTriggerKind.Cough, sourceId, 3f, 5f, 0.7f, 42L);

        Assert.NotNull(received);
        Assert.Equal(SoundTriggerKind.Cough, received!.Value.Kind);
        Assert.Equal(sourceId,              received.Value.SourceEntityId);
        Assert.Equal(3f,                    received.Value.SourceX);
        Assert.Equal(5f,                    received.Value.SourceZ);
        Assert.Equal(0.7f,                  received.Value.Intensity, 4);
        Assert.Equal(42L,                   received.Value.Tick);
    }

    [Fact]
    public void AT02_Emit_IntensityAboveOne_ClampedToOne()
    {
        var bus = new SoundTriggerBus();
        SoundTriggerEvent? received = null;
        bus.Subscribe(e => received = e);

        bus.Emit(SoundTriggerKind.Thud, System.Guid.NewGuid(), 0f, 0f, 9.9f, 0L);

        Assert.NotNull(received);
        Assert.Equal(1f, received!.Value.Intensity, 4);
    }

    [Fact]
    public void AT02b_Emit_IntensityBelowZero_ClampedToZero()
    {
        var bus = new SoundTriggerBus();
        SoundTriggerEvent? received = null;
        bus.Subscribe(e => received = e);

        bus.Emit(SoundTriggerKind.Footstep, System.Guid.NewGuid(), 0f, 0f, -0.5f, 0L);

        Assert.NotNull(received);
        Assert.Equal(0f, received!.Value.Intensity, 4);
    }

    [Fact]
    public void AT03_Emit_WithNoSubscribers_DoesNotThrow()
    {
        var bus = new SoundTriggerBus();
        var ex = Record.Exception(() =>
            bus.Emit(SoundTriggerKind.BulbBuzz, System.Guid.NewGuid(), 1f, 1f, 0.3f, 10L));
        Assert.Null(ex);
    }
}
