using System;
using System.Collections.Generic;
using APIFramework.Systems.Audio;
using Xunit;

namespace APIFramework.Tests.Systems.Audio;

public class SoundTriggerBusMultiSubscriberTests
{
    [Fact]
    public void AllSubscribers_ReceiveSameEvent()
    {
        var bus = new SoundTriggerBus();
        var received1 = new List<SoundTriggerEvent>();
        var received2 = new List<SoundTriggerEvent>();
        var received3 = new List<SoundTriggerEvent>();

        bus.Subscribe(e => received1.Add(e));
        bus.Subscribe(e => received2.Add(e));
        bus.Subscribe(e => received3.Add(e));

        var id = Guid.NewGuid();
        bus.Emit(SoundTriggerKind.Cough, id, 2f, 4f, 0.6f, 99L);

        Assert.Single(received1);
        Assert.Single(received2);
        Assert.Single(received3);

        Assert.Equal(received1[0].SequenceId, received2[0].SequenceId);
        Assert.Equal(received1[0].SequenceId, received3[0].SequenceId);
        Assert.Equal(id, received1[0].SourceEntityId);
        Assert.Equal(SoundTriggerKind.Cough, received3[0].Kind);
    }

    [Fact]
    public void AllSubscribers_ReceiveAllEvents_InOrder()
    {
        var bus = new SoundTriggerBus();
        var received1 = new List<long>();
        var received2 = new List<long>();

        bus.Subscribe(e => received1.Add(e.SequenceId));
        bus.Subscribe(e => received2.Add(e.SequenceId));

        bus.Emit(SoundTriggerKind.Footstep, Guid.NewGuid(), 0f, 0f, 0.3f, 0L);
        bus.Emit(SoundTriggerKind.Thud, Guid.NewGuid(), 1f, 1f, 0.9f, 1L);
        bus.Emit(SoundTriggerKind.Cough, Guid.NewGuid(), 2f, 2f, 0.6f, 2L);

        Assert.Equal(3, received1.Count);
        Assert.Equal(3, received2.Count);
        Assert.Equal(received1, received2);
    }

    [Fact]
    public void FiveSubscribers_AllGetEvent()
    {
        const int n = 5;
        var bus = new SoundTriggerBus();
        var counters = new int[n];
        for (int i = 0; i < n; i++)
        {
            int captured = i;
            bus.Subscribe(_ => counters[captured]++);
        }

        bus.Emit(SoundTriggerKind.BulbBuzz, Guid.NewGuid(), 0f, 0f, 0.2f, 0L);

        for (int i = 0; i < n; i++)
            Assert.Equal(1, counters[i]);
    }
}
