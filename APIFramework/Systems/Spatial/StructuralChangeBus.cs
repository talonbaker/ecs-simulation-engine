using System;
using System.Collections.Generic;
using System.Threading;

namespace APIFramework.Systems.Spatial;

/// <summary>
/// Event bus for structural/topology changes.
/// Mirrors ProximityEventBus shape: producers emit, consumers subscribe.
/// Subscribers are called synchronously during Emit.
/// TopologyVersion is monotonically incremented with every emission and included in the event record.
/// </summary>
public sealed class StructuralChangeBus
{
    private readonly List<Action<StructuralChangeEvent>> _subscribers = new();
    private long _topologyVersion;

    /// <summary>Read-only access to the current topology version. Incremented on every emission.</summary>
    public long TopologyVersion => _topologyVersion;

    /// <summary>Subscribe a handler to receive structural change events.</summary>
    public void Subscribe(Action<StructuralChangeEvent> handler) => _subscribers.Add(handler);

    /// <summary>
    /// Emit a structural change event. Increments TopologyVersion, constructs the event,
    /// and invokes all subscribers synchronously.
    /// </summary>
    public void Emit(StructuralChangeKind kind, Guid entityId, int prevX, int prevY, int curX, int curY, Guid roomId, long tick)
    {
        Interlocked.Increment(ref _topologyVersion);
        var ev = new StructuralChangeEvent(kind, entityId, prevX, prevY, curX, curY, roomId, _topologyVersion, tick);
        foreach (var h in _subscribers) h(ev);
    }
}
