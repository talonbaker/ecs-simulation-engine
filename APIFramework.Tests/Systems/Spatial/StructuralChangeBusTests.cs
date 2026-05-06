using System;
using System.Collections.Generic;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Spatial;

public class StructuralChangeBusTests
{
    // AT-02: TopologyVersion increments exactly once per Emit
    [Fact]
    public void Emit_IncrementsTopologyVersionByOne()
    {
        var bus = new StructuralChangeBus();
        Assert.Equal(0L, bus.TopologyVersion);

        bus.Emit(StructuralChangeKind.EntityAdded, Guid.NewGuid(), 0, 0, 1, 1, Guid.Empty, 0);
        Assert.Equal(1L, bus.TopologyVersion);

        bus.Emit(StructuralChangeKind.EntityMoved, Guid.NewGuid(), 1, 1, 2, 2, Guid.Empty, 1);
        Assert.Equal(2L, bus.TopologyVersion);
    }

    [Fact]
    public void Emit_TopologyVersionIsMonotonic()
    {
        var bus = new StructuralChangeBus();
        long prev = bus.TopologyVersion;

        for (int i = 0; i < 10; i++)
        {
            bus.Emit(StructuralChangeKind.EntityMoved, Guid.NewGuid(), 0, 0, i, i, Guid.Empty, i);
            Assert.True(bus.TopologyVersion > prev);
            prev = bus.TopologyVersion;
        }
    }

    [Fact]
    public void Emit_SubscriberReceivesEventWithMatchingVersionStamp()
    {
        var bus = new StructuralChangeBus();
        StructuralChangeEvent? received = null;
        bus.Subscribe(e => received = e);

        var entityId = Guid.NewGuid();
        bus.Emit(StructuralChangeKind.EntityAdded, entityId, 3, 4, 3, 4, Guid.Empty, 99);

        Assert.NotNull(received);
        Assert.Equal(StructuralChangeKind.EntityAdded, received!.Value.Kind);
        Assert.Equal(entityId, received.Value.EntityId);
        Assert.Equal(1L, received.Value.TopologyVersion);
        Assert.Equal(bus.TopologyVersion, received.Value.TopologyVersion);
        Assert.Equal(99L, received.Value.Tick);
    }

    [Fact]
    public void Subscribe_MultipleSubscribers_AllReceiveEvent()
    {
        var bus = new StructuralChangeBus();
        var received = new List<StructuralChangeEvent>();
        bus.Subscribe(e => received.Add(e));
        bus.Subscribe(e => received.Add(e));

        bus.Emit(StructuralChangeKind.RoomBoundsChanged, Guid.NewGuid(), 0, 0, 1, 1, Guid.Empty, 0);

        Assert.Equal(2, received.Count);
    }

    [Fact]
    public void TopologyVersion_StartsAtZero()
    {
        var bus = new StructuralChangeBus();
        Assert.Equal(0L, bus.TopologyVersion);
    }

    [Fact]
    public void Emit_EventFieldsMatchParameters()
    {
        var bus = new StructuralChangeBus();
        StructuralChangeEvent? received = null;
        bus.Subscribe(e => received = e);

        var entityId = Guid.NewGuid();
        var roomId   = Guid.NewGuid();
        bus.Emit(StructuralChangeKind.EntityMoved, entityId, 2, 3, 5, 6, roomId, 42);

        Assert.NotNull(received);
        Assert.Equal(StructuralChangeKind.EntityMoved, received!.Value.Kind);
        Assert.Equal(entityId, received.Value.EntityId);
        Assert.Equal(2, received.Value.PreviousTileX);
        Assert.Equal(3, received.Value.PreviousTileY);
        Assert.Equal(5, received.Value.CurrentTileX);
        Assert.Equal(6, received.Value.CurrentTileY);
        Assert.Equal(roomId, received.Value.RoomId);
        Assert.Equal(42L, received.Value.Tick);
    }
}
