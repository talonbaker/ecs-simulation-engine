using System;
using UnityEngine;

/// <summary>
/// Describes what the player is currently doing inside Build Mode.
///
/// STATES
/// ───────
///   None       — no active intent; player can click to select / pick up.
///   Placing    — player dragged an item from the palette; ghost preview is active.
///   PickingUp  — player clicked a MutableTopologyTag entity; following the cursor.
///
/// USAGE
/// ──────
/// BuildModeController creates a BuildIntent when the player starts a drag or pickup
/// and clears it when the action is committed (click-to-confirm) or cancelled (Esc /
/// right-click). GhostPreview and PickupController each read the active intent to know
/// what to render and where.
///
/// Immutable once created — replace the whole object rather than mutating fields.
/// </summary>
public sealed class BuildIntent
{
    // ── Static factory helpers ─────────────────────────────────────────────────

    /// <summary>No active intent; build mode idle.</summary>
    public static readonly BuildIntent None = new BuildIntent(BuildIntentKind.None,
        Guid.Empty, null, string.Empty, string.Empty);

    /// <summary>Player is placing an item dragged from the palette.</summary>
    public static BuildIntent ForPlacement(Guid templateId, string label, string category)
        => new BuildIntent(BuildIntentKind.Placing, templateId, null, label, category);

    /// <summary>Player picked up an existing entity and is repositioning it.</summary>
    public static BuildIntent ForPickup(Guid entityId, string label)
        => new BuildIntent(BuildIntentKind.PickingUp, Guid.Empty, entityId, label, string.Empty);

    // ── Properties ─────────────────────────────────────────────────────────────

    /// <summary>Kind of intent currently active.</summary>
    public BuildIntentKind Kind { get; }

    /// <summary>
    /// Template ID to spawn (Placing mode).
    /// <see cref="Guid.Empty"/> when kind is PickingUp or None.
    /// </summary>
    public Guid TemplateId { get; }

    /// <summary>
    /// ID of the entity being repositioned (PickingUp mode).
    /// Null when kind is Placing or None.
    /// </summary>
    public Guid? EntityId { get; }

    /// <summary>Display label for the item being placed / moved. Used in the ghost preview tooltip.</summary>
    public string Label { get; }

    /// <summary>Palette category string (e.g. "Furniture"). Empty for PickingUp.</summary>
    public string Category { get; }

    // ── Private constructor ────────────────────────────────────────────────────

    private BuildIntent(BuildIntentKind kind, Guid templateId, Guid? entityId,
                        string label, string category)
    {
        Kind       = kind;
        TemplateId = templateId;
        EntityId   = entityId;
        Label      = label;
        Category   = category;
    }

    /// <summary>True when the intent is Placing or PickingUp (i.e. ghost should render).</summary>
    public bool IsActive => Kind != BuildIntentKind.None;

    public override string ToString()
        => $"BuildIntent({Kind}, label={Label}, template={TemplateId}, entity={EntityId})";
}

/// <summary>Discriminated kind for <see cref="BuildIntent"/>.</summary>
public enum BuildIntentKind
{
    /// <summary>Build mode idle — no drag or pickup in progress.</summary>
    None,

    /// <summary>Dragging a palette item to a world position; will call SpawnStructural on commit.</summary>
    Placing,

    /// <summary>Picked up an existing entity; will call MoveEntity on commit.</summary>
    PickingUp,
}
