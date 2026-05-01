using System;
using UnityEngine;
using APIFramework.Mutation;

/// <summary>
/// Handles click-to-pickup and click-to-drop in Build Mode (WP-3.1.D).
///
/// FLOW
/// ─────
///   1. Player clicks on an entity that has a <see cref="SelectableTag"/> collider.
///   2. <see cref="BuildModeController"/> calls <see cref="TryPickup"/> with the
///      entity's ID and an existing <see cref="BuildIntent.None"/> state.
///   3. TryPickup verifies the entity has MutableTopologyTag on the engine side
///      (via EngineHost.Engine). If yes, creates a PickingUp intent and returns it.
///   4. Each frame while PickingUp, <see cref="GhostPreview"/> follows the cursor.
///   5. Player clicks again → <see cref="CommitPickup"/> calls
///      IWorldMutationApi.MoveEntity and clears the intent.
///   6. Esc / right-click → <see cref="CancelPickup"/> clears the intent without mutating.
///
/// NPC REJECTION
/// ──────────────
/// NPCs have NpcTag but NOT MutableTopologyTag. TryPickup returns false for any
/// entity that lacks MutableTopologyTag, surfacing a "can't pickup" indicator.
///
/// MOUNTING
/// ─────────
/// Attach to the same GameObject as <see cref="BuildModeController"/>.
/// Wire the EngineHost and IWorldMutationApi references.
/// </summary>
public sealed class PickupController : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────���────────

    [SerializeField]
    [Tooltip("Drag in the EngineHost to allow reading entity component data.")]
    private EngineHost _host;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private IWorldMutationApi _mutationApi;

    // ── Dependency injection ──────────────────────────────────────────────────

    /// <summary>Called by BuildModeController to inject the mutation API post-boot.</summary>
    public void SetMutationApi(IWorldMutationApi api) => _mutationApi = api;

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to enter pickup mode for the entity at the given Guid.
    /// Returns true and sets <paramref name="intent"/> if the entity is moveable.
    /// Returns false (and sets <paramref name="reason"/>) for NPCs or other non-moveable entities.
    /// </summary>
    public bool TryPickup(Guid entityId, string displayName,
                          out BuildIntent intent, out string reason)
    {
        intent = BuildIntent.None;

        // Validate engine host is available.
        if (_host == null || _host.Engine == null)
        {
            reason = "EngineHost not ready.";
            return false;
        }

        // Check entity exists and has MutableTopologyTag.
        // We query Engine.GetAllEntities() — acceptable cost for an input event (not per-frame).
        bool found       = false;
        bool isMutable   = false;
        bool isNpc       = false;

        foreach (var entity in _host.Engine.GetAllEntities())
        {
            if (entity.Id == entityId)
            {
                found     = true;
                isMutable = entity.Has<APIFramework.Components.MutableTopologyTag>();
                isNpc     = entity.Has<APIFramework.Components.NpcTag>();
                break;
            }
        }

        if (!found)
        {
            reason = "Entity not found.";
            return false;
        }

        if (isNpc)
        {
            reason = "NPCs cannot be picked up.";
            return false;
        }

        if (!isMutable)
        {
            reason = "Entity is not moveable (no MutableTopologyTag).";
            return false;
        }

        intent = BuildIntent.ForPickup(entityId, displayName);
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Commits the pickup: calls MoveEntity at the snapped tile position.
    /// Returns true on success.
    /// </summary>
    public bool CommitPickup(Guid entityId, Vector3 worldPos)
    {
        if (_mutationApi == null)
        {
            Debug.LogWarning("[PickupController] MutationApi not set; cannot commit pickup.");
            return false;
        }

        int tileX = Mathf.RoundToInt(worldPos.x);
        int tileY = Mathf.RoundToInt(worldPos.z);
        _mutationApi.MoveEntity(entityId, tileX, tileY);

        return true;
    }

    /// <summary>Cancel pickup — no mutation, just intent cleared.</summary>
    public void CancelPickup()
    {
        // Nothing to undo here; the intent is cleared by BuildModeController.
        // This method exists as a hook for future undo-stack integration.
    }
}
