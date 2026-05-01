using UnityEngine;

/// <summary>
/// State machine for a prop that can be grabbed, moved with snap-to-grid, and dropped.
/// Attach to any prop GameObject that should be draggable in build mode.
/// </summary>
public sealed class DraggableProp : MonoBehaviour
{
    public enum DragState { Idle, Dragging, Settled }

    [Tooltip("World-units per snap step. Default 1.0 matches the engine tile size.")]
    [Range(0.1f, 5.0f)]
    [SerializeField] private float _snapTileSize = 1.0f;

    [Tooltip("If non-empty, attempt to parent onto a PropSocket with this tag on drop.")]
    [SerializeField] private string _targetSocketTag = "";

    [Tooltip("World-units lifted off the floor while dragging — visual grab feedback.")]
    [Range(0f, 2f)]
    [SerializeField] private float _grabHeight = 0.1f;

    public DragState CurrentState { get; private set; } = DragState.Idle;
    public float SnapTileSize    => _snapTileSize;
    public string TargetSocketTag => _targetSocketTag;
    public float GrabHeight      => _grabHeight;

    private float _idleY;

    // Called by DragHandler when the player clicks this prop.
    // floorY: the Y the prop should return to when dropped on the floor.
    // Needed when detaching from a socket so _idleY resets to floor, not the socket height.
    public void BeginDrag(float floorY = 0f)
    {
        bool detachedFromSocket = false;

        // If parented to a PropSocket (e.g. banana on a table), detach and free the socket.
        if (transform.parent != null)
        {
            var socket = transform.parent.GetComponent<PropSocket>();
            if (socket != null)
            {
                socket.IsOccupied    = false;
                socket.OccupyingProp = null;
                transform.SetParent(null);
                detachedFromSocket = true;
            }
        }

        // When coming off a socket the prop's world Y is the socket height.
        // Use floorY so a floor-drop lands at the correct level, not mid-air.
        _idleY = detachedFromSocket ? floorY : transform.position.y;
        CurrentState = DragState.Dragging;
    }

    // Called each frame by DragHandler with the snapped cursor projection.
    public void UpdateDragPosition(float snappedX, float snappedZ)
    {
        transform.position = new Vector3(snappedX, _idleY + _grabHeight, snappedZ);
    }

    // Called by DragHandler when a matching socket is found at the drop point.
    public void SnapToSocket(PropSocket socket)
    {
        transform.SetParent(socket.transform);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        socket.IsOccupied    = true;
        socket.OccupyingProp = this;
        CurrentState = DragState.Idle;
    }

    // Called by DragHandler when dropped on empty floor (no matching socket nearby).
    public void FinalizeDrop(float snappedX, float snappedZ)
    {
        transform.position = new Vector3(snappedX, _idleY, snappedZ);
        CurrentState = DragState.Settled;
    }

    private void LateUpdate()
    {
        if (CurrentState == DragState.Settled)
            CurrentState = DragState.Idle;
    }
}
