using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Bridges the live selection seam to the inspector popup.
/// Subscribes to SelectionManager.OnSelectionChanged, looks up the entity in
/// WorldStateDto, and refreshes InspectorPopup each frame while an NPC is selected.
/// </summary>
public sealed class WorldStateInspectorBinder : MonoBehaviour
{
    [SerializeField] private EngineHost       _engineHost;
    [SerializeField] private SelectionManager _selectionManager;
    [SerializeField] private InspectorPopup   _popup;

    private string _trackedEntityId;

    private void Awake()
    {
        if (_engineHost       == null) _engineHost       = FindObjectOfType<EngineHost>();
        if (_selectionManager == null) _selectionManager = FindObjectOfType<SelectionManager>();
        if (_popup            == null) _popup            = FindObjectOfType<InspectorPopup>();

        if (_popup != null)
            _popup.SetExternallyDriven(true);

        if (_selectionManager != null)
            _selectionManager.OnSelectionChanged += OnSelectionChanged;
    }

    private void OnDestroy()
    {
        if (_selectionManager != null)
            _selectionManager.OnSelectionChanged -= OnSelectionChanged;
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(_trackedEntityId)) return;

        var data = BuildDataForEntity(_trackedEntityId);
        if (data.HasValue)
            _popup.Show(data.Value);
        else
            _popup.Hide();
    }

    private void OnSelectionChanged(Selectable _)
    {
        _trackedEntityId = _selectionManager != null ? _selectionManager.SelectedEntityId : null;
        if (string.IsNullOrEmpty(_trackedEntityId))
            _popup?.Hide();
    }

    private InspectorPopupData? BuildDataForEntity(string entityId)
    {
        var ws = _engineHost?.WorldState;
        if (ws?.Entities == null) return null;

        foreach (var entity in ws.Entities)
        {
            if (entity.Id == entityId)
            {
                return new InspectorPopupData
                {
                    Surface = new SurfaceTierData
                    {
                        Name          = entity.Name,
                        CurrentAction = DeriveCurrentAction(entity),
                    },
                    Behaviour = default,
                    Internal  = default,
                };
            }
        }

        return null;
    }

    // EntityStateDto has no IntendedAction field; DominantDrive is the closest proxy.
    private static string DeriveCurrentAction(EntityStateDto entity)
    {
        if (entity.Physiology.IsSleeping) return "Sleep";
        var dominant = entity.Drives.Dominant;
        return dominant == DominantDrive.None ? "Idle" : dominant.ToString();
    }
}
