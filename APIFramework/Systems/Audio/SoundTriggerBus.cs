using System;
using System.Collections.Generic;

namespace APIFramework.Systems.Audio;

/// <summary>
/// Singleton in-process bus for diegetic sound triggers.
/// Registered by SimulationBootstrapper; subscribers receive triggers in
/// deterministic emission order (same-seed = byte-identical sequence).
/// Engine emits unconditionally; the host (Unity / WARDEN / RETAIL) synthesises audio.
/// </summary>
public sealed class SoundTriggerBus
{
    private readonly List<Action<SoundTriggerEvent>> _subscribers = new();
    private long _sequenceId;

    /// <summary>Registers a handler that will receive every emitted <see cref="SoundTriggerEvent"/>.</summary>
    public void Subscribe(Action<SoundTriggerEvent> handler) => _subscribers.Add(handler);

    /// <summary>
    /// Emits a sound trigger to all subscribers. Clamps intensity to [0,1] and stamps
    /// a monotonically-increasing SequenceId so subscribers can reconstruct emission order.
    /// </summary>
    public void Emit(SoundTriggerKind kind, Guid sourceEntity, float x, float z, float intensity, long tick)
    {
        var ev = new SoundTriggerEvent(kind, sourceEntity, x, z,
            Math.Clamp(intensity, 0f, 1f), tick, ++_sequenceId);
        foreach (var h in _subscribers) h(ev);
    }
}
