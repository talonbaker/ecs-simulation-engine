using APIFramework.Core;

namespace APIFramework.Systems.Spatial;

// -- Proximity event records ----------------------------------------------------
// Each record is a readonly struct: value-type, allocation-free, pooling-friendly.
// All carry the emitting Observer, the Target entity, and the simulation tick.

/// <summary>
/// Target NPC entered Observer's conversation range (within <c>ProximityComponent.ConversationRangeTiles</c>).
/// </summary>
/// <param name="Observer">NPC whose perspective generated the event.</param>
/// <param name="Target">NPC who entered the conversation range.</param>
/// <param name="Tick">Simulation tick at which the entry occurred.</param>
public readonly record struct ProximityEnteredConversationRange(Entity Observer, Entity Target, int Tick);

/// <summary>
/// Target NPC left Observer's conversation range.
/// </summary>
/// <param name="Observer">NPC whose perspective generated the event.</param>
/// <param name="Target">NPC who left the conversation range.</param>
/// <param name="Tick">Simulation tick at which the exit occurred.</param>
public readonly record struct ProximityLeftConversationRange(Entity Observer, Entity Target, int Tick);

/// <summary>NPC Target entered the same room as Observer (detected by ProximityEventSystem).</summary>
/// <param name="Observer">NPC whose perspective generated the event.</param>
/// <param name="Target">NPC who entered the same room.</param>
/// <param name="Tick">Simulation tick at which the entry occurred.</param>
public readonly record struct ProximityEnteredRoom(Entity Observer, Entity Target, int Tick);

/// <summary>NPC Target left the same room as Observer (detected by ProximityEventSystem).</summary>
/// <param name="Observer">NPC whose perspective generated the event.</param>
/// <param name="Target">NPC who left the room.</param>
/// <param name="Tick">Simulation tick at which the exit occurred.</param>
public readonly record struct ProximityLeftRoom(Entity Observer, Entity Target, int Tick);

/// <summary>
/// Target is visible from Observer (within awareness range, not in conversation
/// range and not sharing a room). Fired once on entry; no paired LeftVisible event.
/// </summary>
/// <param name="Observer">NPC whose perspective generated the event.</param>
/// <param name="Target">NPC who became visible.</param>
/// <param name="Tick">Simulation tick at which visibility began.</param>
public readonly record struct ProximityVisibleFromHere(Entity Observer, Entity Target, int Tick);

/// <summary>
/// Subject transitioned rooms (or entered/left the building interior).
/// Fired by RoomMembershipSystem; OldRoom or NewRoom may be null.
/// </summary>
/// <param name="Subject">The entity whose room membership changed.</param>
/// <param name="OldRoom">The previous room entity, or null if subject was in no room.</param>
/// <param name="NewRoom">The new room entity, or null if subject is now in no room.</param>
/// <param name="Tick">Simulation tick at which the transition occurred.</param>
public readonly record struct RoomMembershipChanged(Entity Subject, Entity? OldRoom, Entity? NewRoom, int Tick);
