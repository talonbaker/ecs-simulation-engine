using System;
using System.Collections.Generic;
using APIFramework.Systems.Audio;
using Xunit;

namespace APIFramework.Tests.Systems.Audio;

public class SoundTriggerBusSequenceIdMonotonicTests
{
    [Fact]
    public void SequenceId_StartsAtOne_OnFirstEmit()
    {
        var bus = new SoundTriggerBus();
        SoundTriggerEvent? received = null;
        bus.Subscribe(e => received = e);

        bus.Emit(SoundTriggerKind.Footstep, Guid.NewGuid(), 0f, 0f, 0.3f, 0L);

        Assert.NotNull(received);
        Assert.Equal(1L, received!.Value.SequenceId);
    }

    [Fact]
    public void SequenceId_IncrementsBy1_PerEmit()
    {
        var bus = new SoundTriggerBus();
        var events = new List<SoundTriggerEvent>();
        bus.Subscribe(e => events.Add(e));

        for (int i = 0; i < 5; i++)
            bus.Emit(SoundTriggerKind.Cough, Guid.NewGuid(), 0f, 0f, 0.5f, i);

        Assert.Equal(5, events.Count);
        for (int i = 0; i < 5; i++)
            Assert.Equal(i + 1, events[i].SequenceId);
    }

    [Fact]
    public void SequenceId_IsMonotonic_AcrossMultipleKinds()
    {
        var bus = new SoundTriggerBus();
        var events = new List<SoundTriggerEvent>();
        bus.Subscribe(e => events.Add(e));

        bus.Emit(SoundTriggerKind.Footstep, Guid.NewGuid(), 0f, 0f, 0.3f, 0L);
        bus.Emit(SoundTriggerKind.Cough, Guid.NewGuid(), 1f, 2f, 0.6f, 1L);
        bus.Emit(SoundTriggerKind.Thud, Guid.NewGuid(), 3f, 4f, 0.9f, 2L);

        Assert.Equal(3, events.Count);
        Assert.True(events[0].SequenceId < events[1].SequenceId);
        Assert.True(events[1].SequenceId < events[2].SequenceId);
    }

    [Fact]
    public void SequenceId_IsUnique_AcrossAllEmits()
    {
        var bus = new SoundTriggerBus();
        var ids = new HashSet<long>();
        bus.Subscribe(e => ids.Add(e.SequenceId));

        for (int i = 0; i < 100; i++)
            bus.Emit(SoundTriggerKind.BulbBuzz, Guid.NewGuid(), 0f, 0f, 0.2f, i);

        Assert.Equal(100, ids.Count);
    }
}
