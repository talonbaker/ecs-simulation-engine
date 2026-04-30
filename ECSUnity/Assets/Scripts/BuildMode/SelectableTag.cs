using UnityEngine;

/// <summary>
/// Marker component that makes a Unity GameObject selectable in both normal mode
/// and Build Mode.
///
/// USAGE (WP-3.1.E)
/// ─────────────────
/// Attach to:
///   - NPC silhouette parent GameObjects (or NPC dot GameObjects if 3.1.B not merged).
///   - Object renderers (chairs, desks, named anchors).
///   - Room rectangle GameObjects.
///
/// <see cref="SelectionController"/> (WP-3.1.E) raycasts against colliders that have
/// this component to determine what was clicked. The <see cref="Kind"/> field tells
/// the selection controller how to open the inspector.
///
/// BUILD MODE INTERACTION
/// ───────────────────────
/// <see cref="PickupController"/> (WP-3.1.D) additionally checks for
/// MutableTopologyTag on the engine entity before allowing pickup. Having
/// SelectableTag is necessary but not sufficient for pickup.
/// </summary>
[DisallowMultipleComponent]
public sealed class SelectableTag : MonoBehaviour
{
    [Tooltip("What kind of entity this selectable represents — drives which inspector panel opens.")]
    public SelectableKind Kind = SelectableKind.Npc;

    [Tooltip("Engine entity ID (Guid string) this selectable maps to. " +
             "Populated at spawn time by the renderer that creates this GameObject.")]
    public string EntityId = string.Empty;

    [Tooltip("Human-readable display name for this selectable. " +
             "Used in inspector header and tooltip.")]
    public string DisplayName = string.Empty;
}

/// <summary>Discriminated kind for <see cref="SelectableTag"/>.</summary>
public enum SelectableKind
{
    /// <summary>An NPC — opens the three-tier NPC inspector.</summary>
    Npc,

    /// <summary>A world object (desk, chair, fridge, named anchor) — opens the object inspector.</summary>
    WorldObject,

    /// <summary>A room rectangle — opens the room inspector.</summary>
    Room,
}
