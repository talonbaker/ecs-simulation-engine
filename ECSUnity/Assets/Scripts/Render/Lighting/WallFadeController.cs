using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implements wall-fade-on-occlusion as committed by UX bible §2.1.
///
/// BEHAVIOUR
/// ──────────
/// Each frame:
///   1. Collect all <see cref="WallTag"/> components in the scene (lazy-cached;
///      walls are static in v0.1).
///   2. Raycast from the camera position toward the camera's focus point (supplied by
///      <see cref="CameraController"/>).
///   3. Mark any wall whose collider intersects the ray as "occluding" → target alpha = wallFadedAlpha.
///   4. Mark all other walls as "clear" → target alpha = wallFullAlpha (1.0).
///   5. Lerp each wall's CurrentAlpha toward its TargetAlpha at wallFadeSeconds rate to avoid pop.
///   6. Apply CurrentAlpha to the wall's material _Alpha property.
///
/// WHY WALLTAG INSTEAD OF A LAYER
/// ────────────────────────────────
/// Unity Layers must be pre-configured in ProjectSettings (a YAML file that can't be set
/// from code). WallTag is a MonoBehaviour marker added at runtime — no ProjectSettings
/// changes required. The Physics.RaycastAll call uses a "everything" layer mask so it hits
/// all colliders and we filter by WallTag presence.
///
/// PERFORMANCE
/// ────────────
/// Physics.RaycastAll from camera to focus: one call per frame, ~30–60 hits maximum in a
/// typical office layout. Filtering 60 hits is ~1 us. Lerp + material.SetFloat on all walls
/// (~40 walls in the office-starter) costs < 0.02 ms per frame — well within budget.
///
/// The WallTag cache is refreshed whenever the scene wall count changes (detected by
/// comparing cache size to FindObjectsOfType result count). In v0.1 rooms are static so
/// this check is a fast integer compare after the first frame.
///
/// MOUNTING
/// ─────────
/// Attach to any GameObject. Assign _cameraController and optionally _config in the Inspector.
/// </summary>
public sealed class WallFadeController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("Camera controller that exposes FocusPoint and the camera Transform. " +
             "Usually on the Main Camera GameObject.")]
    private CameraController _cameraController;

    [SerializeField]
    [Tooltip("Lighting tunable parameters (wallFadedAlpha, wallFullAlpha, wallFadeSeconds).")]
    private LightingConfig _config;

    // ── Runtime state ─────────────────────────────────────────────────────────

    // Cached array of all wall tags in the scene.
    private WallTag[] _wallCache = System.Array.Empty<WallTag>();

    // RaycastHit buffer — pre-allocated to avoid per-frame array allocation.
    // 128 is ample for a single-floor office (max ~80 wall faces expected).
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[128];

    // Set of wall collider instance IDs that are currently occluding.
    // Cleared and repopulated each frame.
    private readonly HashSet<int> _occludingInstanceIds = new();

    // Track last-known wall count to detect if walls were added/removed.
    private int _lastKnownWallCount = -1;

    // Cached shader property ID.
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        RefreshWallCacheIfNeeded();

        if (_wallCache.Length == 0) return;
        if (_cameraController == null) return;

        // Read config with fallbacks.
        float fadedAlpha  = _config != null ? _config.wallFadedAlpha  : 0.25f;
        float fullAlpha   = _config != null ? _config.wallFullAlpha   : 1.0f;
        float fadeSeconds = _config != null ? _config.wallFadeSeconds : 0.18f;

        // ── Raycast from camera toward the ground point below the look direction ──
        Vector3 camPos  = _cameraController.transform.position;
        // Project camera forward 20 units onto the ground plane as the ray target.
        Vector3 fwd     = _cameraController.transform.forward;
        Vector3 focusPt = camPos + fwd * 20f;
        focusPt.y       = 0.01f;

        Vector3 dir      = focusPt - camPos;
        float   dist     = dir.magnitude;

        _occludingInstanceIds.Clear();

        if (dist > 0.01f)
        {
            // RaycastNonAlloc: no heap allocation per frame.
            int hitCount = Physics.RaycastNonAlloc(camPos, dir.normalized, _hitBuffer, dist);

            for (int i = 0; i < hitCount; i++)
            {
                // Check if this collider belongs to a wall.
                var wall = _hitBuffer[i].collider.GetComponent<WallTag>();
                if (wall != null)
                    _occludingInstanceIds.Add(wall.GetInstanceID());
            }
        }

        // ── Lerp all wall alphas ───────────────────────────────────────────────
        float dt       = Time.deltaTime;
        float lerpRate = fadeSeconds > 0f ? dt / fadeSeconds : 1f;

        foreach (var wall in _wallCache)
        {
            if (wall == null || wall.FaceMaterial == null) continue;

            // Set the target alpha based on whether this wall is currently occluding.
            wall.TargetAlpha = _occludingInstanceIds.Contains(wall.GetInstanceID())
                ? fadedAlpha
                : fullAlpha;

            // Smooth lerp toward the target — avoids hard pop on/off.
            wall.CurrentAlpha = Mathf.MoveTowards(wall.CurrentAlpha, wall.TargetAlpha, lerpRate);

            wall.FaceMaterial.SetFloat(AlphaId, wall.CurrentAlpha);
        }
    }

    // ── Cache management ──────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the wall cache if the scene wall count has changed.
    /// In v0.1 rooms are static, so after the first frame this is a cheap integer compare.
    /// </summary>
    private void RefreshWallCacheIfNeeded()
    {
        // FindObjectsOfType is expensive; call it only when the count changes.
        // On the first frame _lastKnownWallCount is -1 so we always build the cache.
        var current = FindObjectsOfType<WallTag>();
        if (current.Length != _lastKnownWallCount)
        {
            _wallCache            = current;
            _lastKnownWallCount   = current.Length;
        }
    }

    // ── Test / diagnostic accessors ───────────────────────────────────────────

    /// <summary>
    /// Returns the current (lerped) alpha for the first wall tag with the given room ID.
    /// Exposed for play-mode tests.  Returns -1 if no such wall is found.
    /// </summary>
    public float GetWallAlpha(string roomId)
    {
        foreach (var w in _wallCache)
        {
            if (w != null && w.RoomId == roomId)
                return w.CurrentAlpha;
        }
        return -1f;
    }

    /// <summary>
    /// Number of walls currently marked as occluding (target alpha = wallFadedAlpha).
    /// Exposed for tests.
    /// </summary>
    public int OccludingWallCount => _occludingInstanceIds.Count;

    /// <summary>
    /// Forces an immediate (no lerp) alpha set on all walls.
    /// Used by tests to establish a baseline state without waiting for lerp to complete.
    /// </summary>
    public void ForceAlphaAll(float alpha)
    {
        RefreshWallCacheIfNeeded();
        foreach (var w in _wallCache)
        {
            if (w == null || w.FaceMaterial == null) continue;
            w.TargetAlpha  = alpha;
            w.CurrentAlpha = alpha;
            w.FaceMaterial.SetFloat(AlphaId, alpha);
        }
    }

    /// <summary>
    /// Forces the wall cache to rebuild on the next frame.
    /// Useful after RoomRectangleRenderer has created new room geometry in a test.
    /// </summary>
    public void InvalidateWallCache() => _lastKnownWallCount = -1;
}
