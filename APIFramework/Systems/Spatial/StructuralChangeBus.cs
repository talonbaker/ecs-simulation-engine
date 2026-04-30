using System;
using System.Collections.Generic;
using System.Threading;

namespace APIFramework.Systems.Spatial;

/// <summary>
/// Singleton bus for structural topology change signals.
/// Parallel to ProximityEventBus and NarrativeEventBus.
/// Increments TopologyVersion on every emission — consumers can gate on version
/// changes to decide whether caches need invalidation.
/// </summary>
public sealed class StructuralChangeBus
{
    private readonly List<Action<StructuralChangeEvent>> _subscribers = new();
    private long _topologyVersion;

    public long TopologyVersion => _topologyVersion;

    public void Subscribe(Action<StructuralChangeEvent> handler) => _subscribers.Add(handler);

    public void Emit(StructuralChangeKind kind, Guid entityId,
        int prevX, int prevY, int curX, int curY, Guid roomId, long tick)
    {
        // Interlocked is single-threaded paranoia per SRD §4.2; the engine is not concurrent.
        Interlocked.Increment(ref _topologyVersion);
        var ev = new StructuralChangeEvent(kind, entityId, prevX, prevY, curX, curY, roomId, _topologyVersion, tick);
        foreach (var h in _subscribers) h(ev);
    }
}
