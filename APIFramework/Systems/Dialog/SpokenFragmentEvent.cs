using APIFramework.Core;

namespace APIFramework.Systems.Dialog;

/// <summary>
/// Emitted on the ProximityEventBus when an NPC speaks a phrase fragment to a listener.
/// SpeakerId and ListenerId are the entity references; Tick is the simulation tick counter.
/// </summary>
public readonly record struct SpokenFragmentEvent(Entity SpeakerId, Entity ListenerId, string FragmentId, long Tick);
