using System;
using System.Collections.Generic;

namespace APIFramework.Components;

// ── World-object marker components ────────────────────────────────────────────
// Empty structs — used as tags to identify the type of a world-object entity.

public struct FridgeComponent  { }
public struct SinkComponent    { }
public struct ToiletComponent  { }
public struct BedComponent     { }

// ── StoredTag ─────────────────────────────────────────────────────────────────
/// <summary>
/// Marks food items that are physically stored inside a container (e.g. fridge).
/// Prevents the fallback world-food scan in FeedingSystem from treating them as
/// freely available floor food.
/// </summary>
public struct StoredTag { }

// ── ContainerComponent ────────────────────────────────────────────────────────
/// <summary>
/// Holds a list of entity IDs representing items stored inside this container.
/// Used on fridge entities to track banana inventory.
/// </summary>
public struct ContainerComponent
{
    /// <summary>IDs of entities currently stored in this container.</summary>
    public List<Guid> Contents;

    public readonly int  Count   => Contents?.Count ?? 0;
    public readonly bool IsEmpty => Count == 0;
}
