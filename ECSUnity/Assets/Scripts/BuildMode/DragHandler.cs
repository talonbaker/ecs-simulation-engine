using UnityEngine;

/// <summary>
/// Singleton-ish manager that owns the "currently dragged prop" reference.
/// Polls mouse input, projects the cursor ray onto the floor plane, applies
/// snap-to-grid, and coordinates grab/drop with DraggableProp and PropSocket.
/// </summary>
public sealed class DragHandler : MonoBehaviour
{
    [Tooltip("Drag ray source. Auto-grabs Camera.main on Awake if null.")]
    [SerializeField] private Camera _camera;

    [Tooltip("Y coordinate of the floor projection plane.")]
    [SerializeField] private float _floorPlaneY = 0f;

    [Tooltip("Which layers can be grabbed. Defaults to Everything.")]
    [SerializeField] private LayerMask _dragLayerMask = ~0;

    private DraggableProp _currentDrag;

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;
    }

    private void Update()
    {
        if (_camera == null) return;

        if (Input.GetMouseButtonDown(0) && _currentDrag == null)
        {
            if (TryGrab(out DraggableProp prop))
            {
                _currentDrag = prop;
                _currentDrag.BeginDrag();
            }
        }

        if (_currentDrag != null && TryProjectToFloor(out Vector3 floorPos))
        {
            float snappedX = SnapToGrid(floorPos.x, _currentDrag.SnapTileSize);
            float snappedZ = SnapToGrid(floorPos.z, _currentDrag.SnapTileSize);
            _currentDrag.UpdateDragPosition(snappedX, snappedZ);
        }

        if (Input.GetMouseButtonUp(0) && _currentDrag != null)
        {
            Drop();
        }
    }

    private void Drop()
    {
        if (TryProjectToFloor(out Vector3 floorPos))
        {
            float snappedX = SnapToGrid(floorPos.x, _currentDrag.SnapTileSize);
            float snappedZ = SnapToGrid(floorPos.z, _currentDrag.SnapTileSize);
            Vector3 dropPos = new Vector3(snappedX, floorPos.y, snappedZ);

            PropSocket socket = FindMatchingSocket(dropPos, _currentDrag.TargetSocketTag);
            if (socket != null)
                _currentDrag.SnapToSocket(socket);
            else
                _currentDrag.FinalizeDrop(snappedX, snappedZ);
        }
        else
        {
            // Cursor off-screen or nearly parallel to floor — drop in place.
            _currentDrag.FinalizeDrop(
                _currentDrag.transform.position.x,
                _currentDrag.transform.position.z);
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

    // Mathematical plane intersection — does not require a floor collider.
    private bool TryProjectToFloor(out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        if (Mathf.Abs(ray.direction.y) < 0.0001f) return false;
        float t = (_floorPlaneY - ray.origin.y) / ray.direction.y;
        if (t < 0f) return false;
        worldPos = ray.origin + ray.direction * t;
        return true;
    }

    private static float SnapToGrid(float value, float snapSize)
    {
        return Mathf.Round(value / snapSize) * snapSize;
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
