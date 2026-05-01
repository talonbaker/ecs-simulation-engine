using UnityEngine;

public sealed class InspectorTarget : MonoBehaviour
{
    [Tooltip("Data shown in the inspector popup when this object is clicked.")]
    [SerializeField] private InspectorPopupData _data;

    public InspectorPopupData Data => _data;
}
