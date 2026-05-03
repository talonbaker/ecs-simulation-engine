using UnityEngine;
using UnityEngine.UIElements;
using Warden.Contracts.Telemetry;

/// <summary>
/// Room inspector panel — shown when a room rectangle is selected (WP-3.1.E AT-07).
///
/// Shows: room name, category, current occupants, lighting state, named anchors, stains.
/// Data comes from WorldStateDto.Rooms[selectedId].
/// </summary>
public sealed class RoomInspectorPanel : MonoBehaviour
{
    [SerializeField] private EngineHost _host;
    [SerializeField] private UIDocument _document;

    private VisualElement _root;
    private SelectableTag _current;
    private bool          _isVisible;
    private SelectionController _selection;

    private void Start()
    {
        if (_document != null)
            _root = _document.rootVisualElement?.Q("room-inspector-root");

        // Auto-wire to SelectionController (BUG-004).
        _selection = FindObjectOfType<SelectionController>();
        if (_selection != null)
            _selection.SelectionChanged += OnSelectionChanged;

        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (_selection != null)
            _selection.SelectionChanged -= OnSelectionChanged;
    }

    private void LateUpdate()
    {
        if (!_isVisible || _current == null) return;
        RefreshContent();
    }

    public void OnSelectionChanged(SelectableTag tag)
    {
        _current = tag;
        bool show = tag != null && tag.Kind == SelectableKind.Room;
        SetVisible(show);
    }

    public bool IsVisible => _isVisible;

    private void SetVisible(bool v)
    {
        _isVisible = v;
        if (_root != null) _root.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void RefreshContent()
    {
        if (_root == null || _current == null) return;
        _root.Clear();

        var worldState = _host?.WorldState;
        RoomDto room = null;
        if (worldState?.Rooms != null)
        {
            foreach (var r in worldState.Rooms)
                if (r.Id == _current.EntityId) { room = r; break; }
        }

        _root.Add(MakeLabel(room?.Name ?? _current.DisplayName, "room-name"));

        if (room?.Illumination != null)
        {
            var illum = room.Illumination;
            _root.Add(MakeLabel($"Ambient: {illum.AmbientLevel:F2}", "room-illum"));
            _root.Add(MakeLabel($"ColorK: {illum.ColorTemperatureK}", "room-illum"));
        }
    }

    private static Label MakeLabel(string text, string cls)
    {
        var l = new Label(text); l.AddToClassList(cls); return l;
    }

    private void OnGUI()
    {
        if (_root != null || !_isVisible || _current == null) return;
        float x = Screen.width - 210f, y = 80f, w = 200f, h = 100f;
        GUI.Box(new Rect(x, y, w, h), "Room Inspector");
        GUI.Label(new Rect(x + 4f, y + 22f, w - 8f, 20f), _current.DisplayName);
    }
}
