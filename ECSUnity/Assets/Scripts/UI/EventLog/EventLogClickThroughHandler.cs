using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Handles click-through from an <see cref="EventLogRow"/> — WP-3.1.G AT-08/09.
///
/// On click:
///   1. Glide the camera to the event location (room ID -> world-space centre, or
///      primary participant's last-known position).
///   2. Pin the SelectionController to the primary participant.
///
/// If the participant is deceased or the room is gone, glide to the office centre.
///
/// MOUNTING
/// ────────
/// Attach to the same persistent GameObject as EventLogPanel and SelectionController.
/// Wire _selectionController, _host in the Inspector.
/// </summary>
public sealed class EventLogClickThroughHandler : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private EngineHost          _host;
    [SerializeField] private SelectionController _selectionController;

    // ── Events ─────────────────────────────────────────────────────────────────

    /// <summary>Fired when a glide is triggered. Carries the target world position.</summary>
    public System.Action<Vector3> GlideTriggered;

    // ── Click-through entry point ─────────────────────────────────────────────

    /// <summary>
    /// Called by EventLogPanel when the player clicks a row.
    /// Glides the camera and pins the inspector to the primary participant.
    /// </summary>
    public void HandleRowClicked(ChronicleEntryDto entry)
    {
        if (entry == null) return;

        Vector3 targetPos = ResolveTargetPosition(entry);

        // Notify camera (fires GlideTriggered for CameraController to consume).
        GlideTriggered?.Invoke(targetPos);

        // Pin inspector: create a SelectableTag for the primary participant if possible.
        if (_selectionController != null)
            PinInspector(entry);
    }

    // ── Position resolution ───────────────────────────────────────────────────

    /// <summary>
    /// Resolves the world position to glide to for this event.
    /// Priority: (1) room centre, (2) primary participant last-known pos, (3) office centre.
    /// </summary>
    private Vector3 ResolveTargetPosition(ChronicleEntryDto entry)
    {
        var worldState = _host?.WorldState;

        // (1) Try room centre.
        if (!string.IsNullOrEmpty(entry.Location) && worldState?.Rooms != null)
        {
            foreach (var room in worldState.Rooms)
            {
                if (room?.Id == entry.Location)
                {
                    // RoomDto stores tile bounds in BoundsRect (X, Y, Width, Height).
                    // The engine's tile Y maps to Unity world-space Z (floor plane is XZ).
                    var b = room.BoundsRect;
                    float cx = b.X + b.Width  * 0.5f;
                    float cz = b.Y + b.Height * 0.5f;
                    return new Vector3(cx, 0f, cz);
                }
            }
        }

        // (2) Try primary participant's last-known position.
        string primaryId = entry.Participants?.Count > 0 ? entry.Participants[0] : null;
        if (primaryId != null && worldState?.Entities != null)
        {
            foreach (var entity in worldState.Entities)
            {
                if (entity?.Id == primaryId && entity.Position != null)
                    return new Vector3(entity.Position.X, 0f, entity.Position.Z);
            }
        }

        // (3) Fallback: office centre (zero on Y=0 plane).
        return new Vector3(5f, 0f, 5f);
    }

    // ── Inspector pinning ─────────────────────────────────────────────────────

    private void PinInspector(ChronicleEntryDto entry)
    {
        string primaryId = entry.Participants?.Count > 0 ? entry.Participants[0] : null;
        if (primaryId == null) return;

        // Find or synthesise a display name for the primary participant.
        string displayName = primaryId;
        var worldState = _host?.WorldState;
        if (worldState?.Entities != null)
        {
            foreach (var e in worldState.Entities)
            {
                if (e?.Id == primaryId)
                {
                    displayName = e.Name ?? primaryId;
                    break;
                }
            }
        }

        // Create a synthetic SelectableTag pointing to the participant.
        var go  = new GameObject($"EventLog_Pin_{primaryId}");
        var tag = go.AddComponent<SelectableTag>();
        tag.Kind        = SelectableKind.Npc;
        tag.EntityId    = primaryId;
        tag.DisplayName = displayName;

        _selectionController.SetSelection(tag);

        // Schedule destruction of the synthetic GameObject on the next frame
        // so the selection has been processed by then.
        Destroy(go, 0.1f);
    }

    // ── Test accessors ────────────────────────────────────────────────────────

    /// <summary>Sets the EngineHost dependency (for tests).</summary>
    public void SetHost(EngineHost host) => _host = host;

    /// <summary>Sets the SelectionController dependency (for tests).</summary>
    public void SetSelectionController(SelectionController ctrl) => _selectionController = ctrl;
}
