using System;
using UnityEngine;

/// <summary>
/// State machine for a prop that can be grabbed, moved with snap-to-grid, and dropped.
/// Attach to any prop GameObject that should be draggable in build mode.
/// </summary>
public sealed class DraggableProp : MonoBehaviour
{
    public enum DragState { Idle, Dragging, Settled }

    /// <summary>Fires after the prop settles at its final position (floor drop or socket snap).</summary>
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

    // Called by DragHandler when the player clicks this prop.
    public void BeginDrag()
    {
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
    // The prop hovers at surfaceY + _grabHeight so it tracks whatever it's above.
    public void UpdateDragPosition(float snappedX, float snappedZ, float surfaceY)
    {
        transform.position = new Vector3(snappedX, surfaceY + _grabHeight, snappedZ);
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
        OnDropped?.Invoke(this, transform.position);
    }

    // surfaceY: Y of the surface at the drop point; prop rests directly on it.
    public void FinalizeDrop(float snappedX, float snappedZ, float surfaceY)
    {
        transform.position = new Vector3(snappedX, surfaceY, snappedZ);
        CurrentState = DragState.Settled;
        OnDropped?.Invoke(this, transform.position);
    }

    private void LateUpdate()
    {
        if (CurrentState == DragState.Settled)
            CurrentState = DragState.Idle;
    }
}
