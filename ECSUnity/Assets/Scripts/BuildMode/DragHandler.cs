using UnityEngine;

/// <summary>
/// Singleton-ish manager that owns the "currently dragged prop" reference.
/// Polls mouse input, projects the cursor ray onto the current drag surface plane,
/// applies snap-to-grid, and coordinates grab/drop with DraggableProp and PropSocket.
/// </summary>
public sealed class DragHandler : MonoBehaviour
{
    [Tooltip("Drag ray source. Auto-grabs Camera.main on Awake if null.")]
    [SerializeField] private Camera _camera;

    [Tooltip("Y of the floor — used as the minimum surface height when nothing else is found.")]
    [SerializeField] private float _floorPlaneY = 0f;

    [Tooltip("Which layers can be grabbed. Defaults to Everything.")]
    [SerializeField] private LayerMask _dragLayerMask = ~0;

    private DraggableProp _currentDrag;
    private bool          _active = true;
    private float         _dragPlaneY; // surface Y the prop is currently above; used as the projection plane

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
                _currentDrag = prop;
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

            PropSocket socket = FindMatchingSocket(
                new Vector3(snappedX, planePos.y, snappedZ),
                _currentDrag.TargetSocketTag);

            if (socket != null)
            {
                _currentDrag.SnapToSocket(socket);
            }
            else
            {
                _currentDrag.FinalizeDrop(snappedX, snappedZ, surfaceY);
            }
        }
        else
        {
            // Cursor off-screen — drop in place on whatever surface is below.
            float sx = _currentDrag.transform.position.x;
            float sz = _currentDrag.transform.position.z;
            _currentDrag.FinalizeDrop(sx, sz, GetSurfaceYAtXZ(sx, sz));
        }

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

    // Shoot a ray straight down; return the Y of the highest surface below (x, z).
    // Excludes the dragged prop and its entire child hierarchy (e.g. a banana parented
    // to a table socket) so neither the prop nor its passengers count as surfaces.
    private float GetSurfaceYAtXZ(float x, float z)
    {
        var hits = Physics.RaycastAll(
            new Vector3(x, 1000f, z), Vector3.down, 2000f, _dragLayerMask);

        float topY = _floorPlaneY;
        foreach (var hit in hits)
        {
            if (_currentDrag != null &&
                hit.collider.transform.IsChildOf(_currentDrag.transform))
                continue;
            if (hit.point.y > topY)
                topY = hit.point.y;
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
}
