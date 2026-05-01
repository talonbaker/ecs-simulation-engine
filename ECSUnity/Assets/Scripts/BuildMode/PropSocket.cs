using UnityEngine;

/// <summary>
/// Marks a child transform on a prop as an attachment point.
/// DragHandler checks nearby sockets on drop and parents the dropped prop here.
/// </summary>
public sealed class PropSocket : MonoBehaviour
{
    [Tooltip("Socket type identifier. Match against DraggableProp._targetSocketTag.")]
    [SerializeField] private string _tag = "Surface";

    [Tooltip("XZ world-units within which a drop counts as landing on this socket.")]
    [Range(0.05f, 5f)]
    [SerializeField] private float _snapRadius = 0.5f;

    public string Tag         => _tag;
    public float  SnapRadius  => _snapRadius;
    public bool   IsOccupied  { get; set; }
    public DraggableProp OccupyingProp { get; set; }
}
