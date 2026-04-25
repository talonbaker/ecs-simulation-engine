using APIFramework.Core;

namespace APIFramework.Systems.Spatial;

// ── Proximity event records ────────────────────────────────────────────────────
// Each record is a readonly struct: value-type, allocation-free, pooling-friendly.
// All carry the emitting Observer, the Target entity, and the simulation tick.

public readonly record struct ProximityEnteredConversationRange(Entity Observer, Entity Target, int Tick);
public readonly record struct ProximityLeftConversationRange(Entity Observer, Entity Target, int Tick);

/// <summary>NPC Target entered the same room as Observer (detected by ProximityEventSystem).</summary>
public readonly record struct ProximityEnteredRoom(Entity Observer, Entity Target, int Tick);

/// <summary>NPC Target left the same room as Observer (detected by ProximityEventSystem).</summary>
public readonly record struct ProximityLeftRoom(Entity Observer, Entity Target, int Tick);

/// <summary>
/// Target is visible from Observer (within awareness range, not in conversation
/// range and not sharing a room). Fired once on entry; no paired LeftVisible event.
/// </summary>
public readonly record struct ProximityVisibleFromHere(Entity Observer, Entity Target, int Tick);

/// <summary>
/// Subject transitioned rooms (or entered/left the building interior).
/// Fired by RoomMembershipSystem; OldRoom or NewRoom may be null.
/// </summary>
public readonly record struct RoomMembershipChanged(Entity Subject, Entity? OldRoom, Entity? NewRoom, int Tick);
