using APIFramework.Core;

namespace APIFramework.Systems.Dialog;

/// <summary>
/// Emitted on the ProximityEventBus when an NPC speaks a phrase fragment to a listener.
/// SpeakerId and ListenerId are the entity references; Tick is the simulation tick counter.
/// </summary>
/// <param name="SpeakerId">Entity reference of the speaking NPC.</param>
/// <param name="ListenerId">Entity reference of the listening NPC.</param>
/// <param name="FragmentId">Stable id of the corpus fragment that was spoken.</param>
/// <param name="Tick">Simulation tick on which the fragment was spoken.</param>
public readonly record struct SpokenFragmentEvent(Entity SpeakerId, Entity ListenerId, string FragmentId, long Tick);
