using System;
using UnityEngine;

/// <summary>
/// State machine for a prop that can be grabbed, moved with snap-to-grid, and dropped.
/// Attach to any prop GameObject that should be draggable in build mode.
/// </summary>
public sealed class DraggableProp : MonoBehaviour
{
    public enum DragState { Idle, Dragging, Settled }

    /// <summary>Fires after the prop settles at its final user-initiated position (floor drop or socket snap).
    /// Not fired for system-driven displacements — see SnapToSocketSilent.</summary>
    public event Action<DraggableProp, Vector3> OnDropped;

    [Tooltip("World-units per snap step. Default 0.5 = half-tile resolution.")]
    [Range(0.1f, 5.0f)]
    [SerializeField] private float _snapTileSize = 0.5f;

    [Tooltip("If non-empty, attempt to parent onto a PropSocket with this tag on drop.")]
    [SerializeField] private string _targetSocketTag = "";

    [Tooltip("World-units lifted above the current surface while dragging.")]
    [Range(0f, 2f)]
    [SerializeField] private float _grabHeight = 0.1f;

    public DragState CurrentState { get; private set; } = DragState.Idle;
    public float SnapTileSize     => _snapTileSize;
    public string TargetSocketTag => _targetSocketTag;
    public float GrabHeight       => _grabHeight;

    private float _pivotToBottom;

    private void Awake()
    {
        // Props are placed manually by code; prevent physics from overriding those positions.
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
    }

    // Called by DragHandler when the player clicks this prop.
    public void BeginDrag()
    {
        _pivotToBottom = ComputePivotToBottom();

        // If parented to a PropSocket (e.g. banana on a table), detach and free the socket.
        if (transform.parent != null)
        {
            var socket = transform.parent.GetComponent<PropSocket>();
            if (socket != null)
            {
                socket.IsOccupied    = false;
                socket.OccupyingProp = null;
                transform.SetParent(null);
            }
        }
        CurrentState = DragState.Dragging;
    }

    // surfaceY: Y of the highest surface under the prop's XZ, supplied by DragHandler each frame.
    // The prop hovers _grabHeight above the surface, with its bottom at surfaceY + _grabHeight.
    public void UpdateDragPosition(float snappedX, float snappedZ, float surfaceY)
    {
        transform.position = new Vector3(snappedX, surfaceY + _pivotToBottom + _grabHeight, snappedZ);
    }

    // Called by DragHandler when a matching socket is found at the drop point.
    // Fires OnDropped so the engine bridge can record the placement.
    public void SnapToSocket(PropSocket socket)
    {
        SnapToSocketInternal(socket);
        OnDropped?.Invoke(this, transform.position);
    }

    // Called by DragHandler.DisplaceIntersecting when the system moves a prop out of the way.
    // Does NOT fire OnDropped — displacement is a system action, not a user drop, and firing
    // OnDropped would trigger PropToEngineBridge's snap-back logic, undoing the displacement.
    internal void SnapToSocketSilent(PropSocket socket)
    {
        SnapToSocketInternal(socket);
    }

    // surfaceY: Y of the surface at the drop point; prop bottom rests on it.
    // Recomputes pivot offset each call so this works when invoked externally
    // (e.g. DragHandler displacement) without a preceding BeginDrag.
    public void FinalizeDrop(float snappedX, float snappedZ, float surfaceY)
    {
        transform.position = new Vector3(snappedX, surfaceY + ComputePivotToBottom(), snappedZ);
        CurrentState = DragState.Settled;
        OnDropped?.Invoke(this, transform.position);
    }

    // Undo a drag — return to the position the prop was at when BeginDrag() was called.
    // Called by DragHandler when a drop is rejected (footprint conflict with no socket available).
    public void CancelDrag(Vector3 returnPosition)
    {
        transform.position = returnPosition;
        CurrentState = DragState.Idle;
    }

    private void LateUpdate()
    {
        if (CurrentState == DragState.Settled)
            CurrentState = DragState.Idle;
    }

    private void SnapToSocketInternal(PropSocket socket)
    {
        transform.SetParent(socket.transform);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        socket.IsOccupied    = true;
        socket.OccupyingProp = this;
        CurrentState = DragState.Idle;
    }

    // Distance from this pivot to the lowest collider point.
    // Skips colliders that belong to a nested DraggableProp (e.g. banana on a table socket)
    // so that child props don't corrupt the table's own pivot offset when they hang at
    // unusual heights relative to the socket position.
    private float ComputePivotToBottom()
    {
        float minY = float.MaxValue;
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            var owner = col.GetComponentInParent<DraggableProp>();
            if (owner != null && owner != this) continue;
            if (col.bounds.min.y < minY) minY = col.bounds.min.y;
        }
        return minY < float.MaxValue ? transform.position.y - minY : 0f;
    }
}
