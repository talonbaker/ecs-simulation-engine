using System;
using System.Collections.Generic;

namespace APIFramework.Systems.Audio;

public sealed class SoundTriggerBus
{
    private readonly List<Action<SoundTriggerEvent>> _subscribers = new();
    private long _sequenceId;

    public void Subscribe(Action<SoundTriggerEvent> handler) => _subscribers.Add(handler);

    public void Emit(SoundTriggerKind kind, Guid sourceEntity, float x, float z, float intensity, long tick)
    {
        var ev = new SoundTriggerEvent(kind, sourceEntity, x, z,
            Math.Clamp(intensity, 0f, 1f), tick, ++_sequenceId);
        foreach (var h in _subscribers) h(ev);
    }
}
