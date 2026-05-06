using System;
using System.Collections.Generic;

namespace APIFramework.Systems.Visual;

/// <summary>
/// Singleton in-process bus for particle effect triggers.
/// Registered by SimulationBootstrapper; subscribers receive triggers in
/// deterministic emission order (same-seed = byte-identical sequence).
/// Engine emits unconditionally; the host (Unity / WARDEN) spawns VFX Graph instances.
/// </summary>
public sealed class ParticleTriggerBus
{
    private readonly List<Action<ParticleTriggerEvent>> _subscribers = new();
    private long _sequenceId;

    /// <summary>Registers a handler that will receive every emitted <see cref="ParticleTriggerEvent"/>.</summary>
    public void Subscribe(Action<ParticleTriggerEvent> handler) => _subscribers.Add(handler);

    /// <summary>Removes a previously registered handler.</summary>
    public void Unsubscribe(Action<ParticleTriggerEvent> handler) => _subscribers.Remove(handler);

    /// <summary>
    /// Emits a particle trigger to all subscribers. Clamps intensity to [0,1] and stamps
    /// a monotonically-increasing SequenceId so subscribers can reconstruct emission order.
    /// </summary>
    public void Emit(ParticleTriggerKind kind, Guid sourceEntityId, float x, float z, float intensityMult, long tick)
    {
        var ev = new ParticleTriggerEvent(kind, sourceEntityId, x, z,
            Math.Clamp(intensityMult, 0f, 1f), tick, ++_sequenceId);
        foreach (var h in _subscribers) h(ev);
    }
}
