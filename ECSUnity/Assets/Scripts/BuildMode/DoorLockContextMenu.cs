using System;
using UnityEngine;
using APIFramework.Mutation;

/// <summary>
/// Handles right-click "Lock / Unlock" context menu for door entities in Build Mode
/// (WP-3.1.D AT-08 / AT-09).
///
/// FLOW
/// ─────
///   1. Player right-clicks inside Build Mode.
///   2. <see cref="BuildModeController"/> calls <see cref="TryShowContextMenu"/> with
///      the cursor screen position.
///   3. This class raycasts to find a <see cref="SelectableTag"/> collider on a door.
///   4. If found, shows a minimal IMGUI popup (lock/unlock options).
///   5. Player clicks "Lock" → IWorldMutationApi.AttachObstacle; path cache invalidates.
///      Player clicks "Unlock" → IWorldMutationApi.DetachObstacle.
///
/// PATH CACHE INVALIDATION
/// ────────────────────────
/// AttachObstacle / DetachObstacle emit ObstacleAttached / ObstacleDetached on the
/// StructuralChangeBus, which the 3.0.4 pathfinding cache subscribes to. No explicit
/// cache call is needed here — it's automatic.
///
/// NOTE ON IMGUI
/// ──────────────
/// The context menu uses legacy IMGUI (OnGUI) at v0.1 for simplicity. A full
/// UI Toolkit context-menu widget can replace this in a later polish pass.
/// </summary>
public sealed class DoorLockContextMenu : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("Layer mask that contains door colliders.")]
    private LayerMask _doorLayer = ~0;

    [SerializeField]
    private BuildModeConfig _config;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private IWorldMutationApi _mutationApi;
    private EngineHost        _host;

    private bool              _menuVisible;
    private Vector2           _menuScreenPos;
    private Guid              _menuTargetEntityId;
    private bool              _targetIsLocked;      // current lock state of target
    private string            _targetLabel = string.Empty;

    // ── Dependency injection ──────────────────────────────────────────────────

    public void SetDependencies(EngineHost host, IWorldMutationApi api)
    {
        _host        = host;
        _mutationApi = api;
    }

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Try to show a lock/unlock context menu at <paramref name="screenPos"/>.
    /// Returns true if a door was found and the menu is now visible.
    /// </summary>
    public bool TryShowContextMenu(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        float radius = _config != null ? _config.contextMenuPickRadius : 24f;
        Ray   ray    = cam.ScreenPointToRay(screenPos);

        // Sphere-cast toward the scene with a small radius to match the pick radius.
        if (!Physics.SphereCast(ray, radius / cam.pixelHeight * 10f,
                out RaycastHit hit, 200f, _doorLayer))
            return false;

        // Check the hit object for SelectableTag and entity ID.
        var tag = hit.collider.GetComponentInParent<SelectableTag>();
        if (tag == null || string.IsNullOrEmpty(tag.EntityId)) return false;

        if (!Guid.TryParse(tag.EntityId, out Guid entityId)) return false;

        // Determine current locked state via engine component.
        bool isLocked = IsDoorLocked(entityId);

        _menuVisible            = true;
        _menuScreenPos          = screenPos;
        _menuTargetEntityId     = entityId;
        _targetIsLocked         = isLocked;
        _targetLabel            = tag.DisplayName;
        return true;
    }

    /// <summary>Dismiss the context menu without taking action.</summary>
    public void DismissMenu() => _menuVisible = false;

    // ── IMGUI ─────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (!_menuVisible) return;

        // Convert screen-space (Unity input = Y-up) to GUI-space (Y-down).
        float guiY = Screen.height - _menuScreenPos.y;
        Rect  rect = new Rect(_menuScreenPos.x, guiY - 60f, 140f, 64f);

        GUI.Box(rect, _targetLabel);

        Rect btnRect = new Rect(rect.x + 4f, rect.y + 20f, 132f, 20f);

        string btnLabel = _targetIsLocked ? "Unlock" : "Lock";
        if (GUI.Button(btnRect, btnLabel))
        {
            if (_targetIsLocked)
                UnlockDoor(_menuTargetEntityId);
            else
                LockDoor(_menuTargetEntityId);
            _menuVisible = false;
        }

        Rect cancelRect = new Rect(rect.x + 4f, rect.y + 42f, 132f, 18f);
        if (GUI.Button(cancelRect, "Cancel"))
            _menuVisible = false;

        // Click outside → close.
        if (Event.current.type == EventType.MouseDown)
        {
            if (!rect.Contains(Event.current.mousePosition))
                _menuVisible = false;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsDoorLocked(Guid entityId)
    {
        if (_host == null || _host.Engine == null) return false;
        foreach (var entity in _host.Engine.GetAllEntities())
            if (entity.Id == entityId)
                return entity.Has<APIFramework.Components.LockedTag>();
        return false;
    }

    private void LockDoor(Guid entityId)
    {
        if (_mutationApi == null) return;
        bool ok = _mutationApi.AttachObstacle(entityId);
        if (ok) _targetIsLocked = true;
        Debug.Log($"[DoorLockContextMenu] Lock({entityId}) → {ok}");
    }

    private void UnlockDoor(Guid entityId)
    {
        if (_mutationApi == null) return;
        bool ok = _mutationApi.DetachObstacle(entityId);
        if (ok) _targetIsLocked = false;
        Debug.Log($"[DoorLockContextMenu] Unlock({entityId}) → {ok}");
    }

    // ── Test accessors ────────────────────────────────────────────────────────

    /// <summary>True when the context menu popup is currently displayed.</summary>
    public bool IsMenuVisible => _menuVisible;

    /// <summary>Entity ID that the context menu is targeting.</summary>
    public Guid MenuTargetEntityId => _menuTargetEntityId;

    /// <summary>Whether the target door is currently shown as locked.</summary>
    public bool TargetIsLocked => _targetIsLocked;

    // ── Direct mutation for tests ─────────────────────────────────────────────

    /// <summary>Directly lock a door entity by ID. Used in tests to bypass UI.</summary>
    public bool DirectLock(Guid entityId)
    {
        if (_mutationApi == null) return false;
        return _mutationApi.AttachObstacle(entityId);
    }

    /// <summary>Directly unlock a door entity by ID. Used in tests to bypass UI.</summary>
    public bool DirectUnlock(Guid entityId)
    {
        if (_mutationApi == null) return false;
        return _mutationApi.DetachObstacle(entityId);
    }
}
