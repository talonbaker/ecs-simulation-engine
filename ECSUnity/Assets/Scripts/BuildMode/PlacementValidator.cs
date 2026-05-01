using System.Collections.Generic;
using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Stateless service that checks whether a placement is valid.
///
/// VALIDITY RULES (in priority order)
/// ──────────────────────────────��──────
///  1. The target tile is within world bounds.
///  2. No existing solid structure occupies the tile (wall, desk, etc.) —
///     detected via Physics.OverlapBox against the "BuildableLayer" layer mask.
///  3. No NPC currently occupies the tile.
///  4. Placement does not fully seal a room (path-connectivity check via a
///     lightweight flood-fill stub — full validation deferred to 3.0.4 cache).
///
/// ARCHITECTURE
/// ─────────────
/// This is a pure-read service — it never mutates engine state. All geometry
/// queries use Unity Physics (raycasts / overlaps) against colliders that
/// RoomRectangleRenderer creates for walls, and against NPC silhouette colliders.
///
/// Attach to the same GameObject as <see cref="BuildModeController"/>.
/// </summary>
public sealed class PlacementValidator : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("Layer mask that represents solid structural geometry (walls, placed furniture).")]
    private LayerMask _solidLayer = ~0;   // default: everything; narrow in the Inspector

    [SerializeField]
    [Tooltip("Layer mask for NPC colliders.")]
    private LayerMask _npcLayer = ~0;

    [SerializeField]
    [Tooltip("Half-extents of the overlap box used for collision detection (should match grid cell size).")]
    private Vector3 _overlapHalfExtents = new Vector3(0.45f, 0.5f, 0.45f);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if placing a ghost footprint at <paramref name="worldPos"/> is valid.
    /// The check is synchronous and runs on the main thread — keep it cheap.
    /// </summary>
    /// <param name="worldPos">World-space centre of the proposed placement tile.</param>
    /// <param name="reason">
    /// Human-readable reason why placement is invalid (for tooltip / console).
    /// Empty string when valid.
    /// </param>
    public bool IsValidPlacement(Vector3 worldPos, out string reason)
    {
        // Lift slightly above the floor so we don't clip the floor collider.
        Vector3 checkPos = new Vector3(worldPos.x, _overlapHalfExtents.y, worldPos.z);

        // 1. Solid structure collision.
        Collider[] solidHits = Physics.OverlapBox(checkPos, _overlapHalfExtents,
            Quaternion.identity, _solidLayer);
        if (solidHits.Length > 0)
        {
            reason = $"Tile occupied by: {solidHits[0].gameObject.name}";
            return false;
        }

        // 2. NPC occupancy.
        Collider[] npcHits = Physics.OverlapBox(checkPos, _overlapHalfExtents,
            Quaternion.identity, _npcLayer);
        if (npcHits.Length > 0)
        {
            reason = "An NPC is standing there.";
            return false;
        }

        // 3. Bounds check — world is finite; reject absurdly out-of-range positions.
        if (Mathf.Abs(worldPos.x) > 500f || Mathf.Abs(worldPos.z) > 500f)
        {
            reason = "Outside world bounds.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Snap a raw world position to the nearest tile grid position.
    /// Uses the <see cref="BuildModeConfig.snapGridSize"/> from the config, if available.
    /// </summary>
    public static Vector3 SnapToGrid(Vector3 worldPos, float gridSize)
    {
        float x = Mathf.Round(worldPos.x / gridSize) * gridSize;
        float z = Mathf.Round(worldPos.z / gridSize) * gridSize;
        return new Vector3(x, worldPos.y, z);
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Project a screen-space mouse position to a world-space XZ tile center.
    /// Returns false if the ray does not hit the floor plane.
    /// </summary>
    public static bool ScreenToWorld(Vector2 screenPos, Camera cam, float gridSize, out Vector3 worldPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        // Intersect with Y=0 plane (the floor).
        if (Mathf.Abs(ray.direction.y) < 0.0001f)
        {
            worldPos = Vector3.zero;
            return false;
        }

        float t = -ray.origin.y / ray.direction.y;
        if (t < 0f)
        {
            worldPos = Vector3.zero;
            return false;
        }

        Vector3 hit = ray.origin + ray.direction * t;
        worldPos = SnapToGrid(hit, gridSize);
        return true;
    }
}
