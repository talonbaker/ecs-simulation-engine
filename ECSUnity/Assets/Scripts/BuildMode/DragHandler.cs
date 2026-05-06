using APIFramework.Build;
using APIFramework.Components;
using UnityEngine;

/// <summary>
/// Singleton-ish manager that owns the "currently dragged prop" reference.
/// Polls mouse input, projects the cursor ray onto the current drag surface plane,
/// applies snap-to-grid, and coordinates grab/drop with DraggableProp and PropSocket.
///
/// FOOTPRINT-AWARE PLACEMENT (WP-4.0.G — BUG-001 fix)
/// ─────────────────────────────────────────────────────
/// On each frame and on drop, <see cref="GetSurfaceYAtXZ"/> consults the
/// <see cref="PropFootprintBridge"/> on any prop beneath the cursor and applies
/// <see cref="FootprintGeometry.CanStackOn"/> to determine placement validity.
///
///   · Empty target tiles      → valid floor placement at _floorPlaneY.
///   · Stackable target prop   → valid; Y = target.BottomHeight + target.TopHeight.
///   · Non-stackable target    → invalid; ghost goes red; drop cancelled.
///   · Mixed occupancy         → invalid (multi-tile prop half on floor, half on prop).
///   · No PropFootprintBridge  → geometry-cast fallback (defensive; should not occur
///                               after WP-4.0.C attaches footprints at spawn time).
///
/// On an invalid drop the prop snaps back to its pre-drag position via CancelDrag,
/// and the optional <see cref="_denialSoundSource"/> plays.
/// </summary>
public sealed class DragHandler : MonoBehaviour
{
    [Tooltip("Drag ray source. Auto-grabs Camera.main on Awake if null.")]
    [SerializeField] private Camera _camera;

    [Tooltip("Y of the floor — used as the minimum surface height when nothing else is found.")]
    [SerializeField] private float _floorPlaneY = 0f;

    [Tooltip("Which layers can be grabbed. Defaults to Everything.")]
    [SerializeField] private LayerMask _dragLayerMask = ~0;

    [Tooltip("Optional ghost preview to tint red/green while dragging.")]
    [SerializeField] private GhostPreview _ghostPreview;

    [Tooltip("Optional AudioSource played on invalid drop (CRT-style denial click).")]
    [SerializeField] private AudioSource _denialSoundSource;

    private DraggableProp _currentDrag;
    private bool          _active = true;
    private float         _dragPlaneY;
    private Vector3       _preDragPosition;
    private bool          _isCurrentDropValid = true;

    /// <summary>Allow this handler to process drag input. Called by BuildModeController on build-mode enter.</summary>
    public void Activate()   => _active = true;

    /// <summary>Suppress all drag input. Called by BuildModeController on build-mode exit.</summary>
    public void Deactivate() => _active = false;

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;
    }

    private void Update()
    {
        if (!_active || _camera == null) return;

        if (Input.GetMouseButtonDown(0) && _currentDrag == null)
        {
            if (TryGrab(out DraggableProp prop))
            {
                _currentDrag     = prop;
                _preDragPosition = prop.transform.position;
                _currentDrag.BeginDrag();
                _dragPlaneY = GetSurfaceYAtXZ(prop.transform.position.x, prop.transform.position.z);
            }
        }

        if (_currentDrag != null && TryProjectToPlane(_dragPlaneY, out Vector3 planePos))
        {
            float snappedX = SnapToGrid(planePos.x, _currentDrag.SnapTileSize);
            float snappedZ = SnapToGrid(planePos.z, _currentDrag.SnapTileSize);
            float surfaceY = GetSurfaceYAtXZ(snappedX, snappedZ);
            _dragPlaneY = surfaceY;
            _currentDrag.UpdateDragPosition(snappedX, snappedZ, surfaceY);
            _ghostPreview?.SetValid(_isCurrentDropValid);
        }

        if (Input.GetMouseButtonUp(0) && _currentDrag != null)
        {
            Drop();
        }
    }

    private void Drop()
    {
        if (TryProjectToPlane(_dragPlaneY, out Vector3 planePos))
        {
            float snappedX = SnapToGrid(planePos.x, _currentDrag.SnapTileSize);
            float snappedZ = SnapToGrid(planePos.z, _currentDrag.SnapTileSize);
            float surfaceY = GetSurfaceYAtXZ(snappedX, snappedZ);

            if (!_isCurrentDropValid)
            {
                RejectDrop();
                return;
            }

            PropSocket socket = FindMatchingSocket(
                new Vector3(snappedX, planePos.y, snappedZ),
                _currentDrag.TargetSocketTag);

            if (socket != null)
                _currentDrag.SnapToSocket(socket);
            else
                _currentDrag.FinalizeDrop(snappedX, snappedZ, surfaceY);
        }
        else
        {
            // Cursor off-screen — check position under the dragged prop.
            float sx = _currentDrag.transform.position.x;
            float sz = _currentDrag.transform.position.z;
            float sy = GetSurfaceYAtXZ(sx, sz);

            if (!_isCurrentDropValid)
            {
                RejectDrop();
                return;
            }

            _currentDrag.FinalizeDrop(sx, sz, sy);
        }

        _currentDrag = null;
    }

    private void RejectDrop()
    {
        _currentDrag.CancelDrag(_preDragPosition);
        _denialSoundSource?.Play();
        _ghostPreview?.SetValid(true);
        _currentDrag = null;
    }

    private bool TryGrab(out DraggableProp prop)
    {
        prop = null;
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, _dragLayerMask))
            return false;
        prop = hit.collider.GetComponentInParent<DraggableProp>();
        return prop != null && prop.CurrentState == DraggableProp.DragState.Idle;
    }

    // Mathematical plane intersection — no collider required on the surface.
    private bool TryProjectToPlane(float planeY, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        if (Mathf.Abs(ray.direction.y) < 0.0001f) return false;
        float t = (planeY - ray.origin.y) / ray.direction.y;
        if (t < 0f) return false;
        worldPos = ray.origin + ray.direction * t;
        return true;
    }

    private static float SnapToGrid(float value, float snapSize)
    {
        return Mathf.Round(value / snapSize) * snapSize;
    }

    // ── Footprint-aware surface Y ─────────────────────────────────────────────

    // Determines valid surface Y and sets _isCurrentDropValid as a side effect.
    // Footprint-first path: uses PropFootprintBridge for all props that have one.
    // Geometry-cast fallback: for props without PropFootprintBridge (defensive).
    private float GetSurfaceYAtXZ(float x, float z)
    {
        var bridge = _currentDrag != null ? _currentDrag.GetComponent<PropFootprintBridge>() : null;
        if (bridge != null)
        {
            var result = ComputeFootprintPlacement(x, z, bridge);
            _isCurrentDropValid = result.IsValid;
            return result.SurfaceY;
        }

        _isCurrentDropValid = true;
        return GeometryCastSurfaceY(x, z);
    }

    // Checks all tiles of the dragged prop's footprint.
    // Valid iff all tiles are empty (floor placement) or all share the same stackable prop.
    private PlacementResult ComputeFootprintPlacement(float anchorX, float anchorZ, PropFootprintBridge draggedBridge)
    {
        float snapSize  = _currentDrag != null ? _currentDrag.SnapTileSize : 1f;
        var   draggedFp = draggedBridge.ToComponent();

        PropFootprintBridge stackTarget = null;
        bool hasEmpty = false;

        for (int dx = 0; dx < draggedBridge.WidthTiles; dx++)
        for (int dz = 0; dz < draggedBridge.DepthTiles; dz++)
        {
            float checkX = anchorX + dx * snapSize;
            float checkZ = anchorZ + dz * snapSize;
            PropFootprintBridge tileProp = FindTopPropBridgeAtXZ(checkX, checkZ);

            if (tileProp == null)
            {
                hasEmpty = true;
                if (stackTarget != null) return PlacementResult.Invalid(_floorPlaneY);
            }
            else
            {
                if (hasEmpty) return PlacementResult.Invalid(_floorPlaneY);
                if (stackTarget == null)
                    stackTarget = tileProp;
                else if (!ReferenceEquals(stackTarget, tileProp))
                    return PlacementResult.Invalid(_floorPlaneY);
            }
        }

        if (stackTarget == null)
            return PlacementResult.Valid(_floorPlaneY);

        if (FootprintGeometry.CanStackOn(draggedFp, stackTarget.ToComponent()))
            return PlacementResult.Valid(stackTarget.BottomHeight + stackTarget.TopHeight);

        return PlacementResult.Invalid(_floorPlaneY);
    }

    // Returns the PropFootprintBridge of the highest prop at (x, z), excluding the dragged prop.
    private PropFootprintBridge FindTopPropBridgeAtXZ(float x, float z)
    {
        var hits = Physics.RaycastAll(new Vector3(x, 1000f, z), Vector3.down, 2000f, _dragLayerMask);
        PropFootprintBridge topBridge = null;
        float topY = _floorPlaneY;

        foreach (var hit in hits)
        {
            if (_currentDrag != null && hit.collider.transform.IsChildOf(_currentDrag.transform))
                continue;
            var b = hit.collider.GetComponentInParent<PropFootprintBridge>();
            if (b != null && hit.point.y > topY)
            {
                topBridge = b;
                topY = hit.point.y;
            }
        }

        return topBridge;
    }

    // Geometry-cast fallback: returns the Y of the highest surface below (x, z).
    // Excludes the dragged prop and its child hierarchy.
    private float GeometryCastSurfaceY(float x, float z)
    {
        var hits = Physics.RaycastAll(new Vector3(x, 1000f, z), Vector3.down, 2000f, _dragLayerMask);
        float topY = _floorPlaneY;
        foreach (var hit in hits)
        {
            if (_currentDrag != null && hit.collider.transform.IsChildOf(_currentDrag.transform))
                continue;
            if (hit.point.y > topY) topY = hit.point.y;
        }
        return topY;
    }

    // XZ-distance check: top-down game, socket height is irrelevant for snap.
    private static PropSocket FindMatchingSocket(Vector3 dropPos, string targetTag)
    {
        PropSocket nearest  = null;
        float      nearestD = float.MaxValue;

        foreach (var socket in FindObjectsOfType<PropSocket>())
        {
            if (socket.IsOccupied) continue;
            if (!string.IsNullOrEmpty(targetTag) && socket.Tag != targetTag) continue;

            float dx   = socket.transform.position.x - dropPos.x;
            float dz   = socket.transform.position.z - dropPos.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            if (dist <= socket.SnapRadius && dist < nearestD)
            {
                nearest  = socket;
                nearestD = dist;
            }
        }

        return nearest;
    }

    // ── Test accessors ────────────────────────────────────────────────────────

    /// <summary>True when the current drag position resolves to a valid drop target.</summary>
    public bool IsCurrentDropValid => _isCurrentDropValid;

    /// <summary>True while a prop is being dragged.</summary>
    public bool IsDragging => _currentDrag != null;

    // ── Value type ────────────────────────────────────────────────────────────

    private readonly struct PlacementResult
    {
        public readonly bool  IsValid;
        public readonly float SurfaceY;
        private PlacementResult(bool valid, float y) { IsValid = valid; SurfaceY = y; }
        public static PlacementResult Valid(float y)   => new(true,  y);
        public static PlacementResult Invalid(float y) => new(false, y);
    }
}
