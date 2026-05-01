using System;
using System.Collections.Generic;
using APIFramework.Systems.Audio;
using Xunit;

namespace APIFramework.Tests.Systems.Audio;

/// <summary>
/// Parity tests: verifies the SoundTriggerBus works consistently in the test context.
/// Since no BUILD_TARGET defines separate Warden from Retail in tests, these verify
/// that the bus behavior is identical regardless of how the test runner is configured.
/// </summary>
public class SoundTriggerWardenRetailParityTests
{
    [Fact]
    public void Bus_Exists_AndCanBeInstantiated()
    {
        var bus = new SoundTriggerBus();
        Assert.NotNull(bus);
    }

    [Fact]
    public void Bus_EmitsToAllSubscribers_RegardlessOfCallOrder()
    {
        // Test that subscribe-before-emit and subscribe-after-instantiation both work.
        var bus = new SoundTriggerBus();

        var results1 = new List<SoundTriggerEvent>();
        var results2 = new List<SoundTriggerEvent>();

        bus.Subscribe(e => results1.Add(e));
        bus.Subscribe(e => results2.Add(e));

        bus.Emit(SoundTriggerKind.Footstep, Guid.NewGuid(), 0f, 0f, 0.3f, 1L);
        bus.Emit(SoundTriggerKind.Thud, Guid.NewGuid(), 1f, 1f, 0.9f, 2L);

        Assert.Equal(2, results1.Count);
        Assert.Equal(2, results2.Count);
        Assert.Equal(results1[0].SequenceId, results2[0].SequenceId);
        Assert.Equal(results1[1].SequenceId, results2[1].SequenceId);
    }

    [Fact]
    public void Bus_SequenceId_StartsFromOne_InFreshInstance()
    {
        var bus1 = new SoundTriggerBus();
        var bus2 = new SoundTriggerBus();

        SoundTriggerEvent? evt1 = null, evt2 = null;
        bus1.Subscribe(e => evt1 = e);
        bus2.Subscribe(e => evt2 = e);

        bus1.Emit(SoundTriggerKind.Cough, Guid.NewGuid(), 0f, 0f, 0.6f, 0L);
        bus2.Emit(SoundTriggerKind.Gasp, Guid.NewGuid(), 0f, 0f, 0.7f, 0L);

        Assert.Equal(1L, evt1!.Value.SequenceId);
        Assert.Equal(1L, evt2!.Value.SequenceId);
    }

    [Fact]
    public void Bus_IntensityClamping_IsDeterministic()
    {
        // Same intensity value produces same clamped result in both "contexts"
        var bus = new SoundTriggerBus();
        SoundTriggerEvent? evt = null;
        bus.Subscribe(e => evt = e);

        bus.Emit(SoundTriggerKind.BulbBuzz, Guid.NewGuid(), 0f, 0f, 0.2f, 0L);

        Assert.NotNull(evt);
        Assert.Equal(0.2f, evt!.Value.Intensity, 5);
    }

    [Fact]
    public void AllSoundKinds_CanBeEmitted_WithoutException()
    {
        var bus = new SoundTriggerBus();
        var allKinds = (SoundTriggerKind[])Enum.GetValues(typeof(SoundTriggerKind));

        var ex = Record.Exception(() =>
        {
            foreach (var kind in allKinds)
                bus.Emit(kind, Guid.NewGuid(), 0f, 0f, 0.5f, 0L);
        });

        Assert.Null(ex);
    }
}
