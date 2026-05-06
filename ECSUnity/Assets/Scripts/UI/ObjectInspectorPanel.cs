using UnityEngine;
using UnityEngine.UIElements;
using Warden.Contracts.Telemetry;

/// <summary>
/// Object inspector panel — shown when a WorldObject (desk, fridge, named anchor) is
/// selected (WP-3.1.E AT-07).
///
/// Shows: name, description, current state, top-3 NPCs by interaction count, persistent state.
///
/// Data comes from WorldStateDto.WorldObjects[selectedId].
/// </summary>
public sealed class ObjectInspectorPanel : MonoBehaviour
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
            _root = _document.rootVisualElement?.Q("object-inspector-root");

        // Auto-wire to SelectionController (BUG-004). Hand-authored scene YAML
        // doesn't express UnityEvent hooks well; runtime find is more robust.
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
        bool show = tag != null && tag.Kind == SelectableKind.WorldObject;
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

        // Find the world object DTO.
        var worldState = _host?.WorldState;
        WorldObjectDto obj = null;
        if (worldState?.WorldObjects != null)
        {
            foreach (var o in worldState.WorldObjects)
                if (o.Id == _current.EntityId) { obj = o; break; }
        }

        _root.Add(MakeLabel(obj?.Name ?? _current.DisplayName, "obj-name"));
        _root.Add(MakeLabel($"Kind: {obj?.Kind}", "obj-kind"));
        _root.Add(MakeLabel($"Position: ({obj?.X:F1}, {obj?.Z:F1})", "obj-pos"));
    }

    private static Label MakeLabel(string text, string cls)
    {
        var l = new Label(text); l.AddToClassList(cls); return l;
    }

    private void OnGUI()
    {
        if (_root != null || !_isVisible || _current == null) return;
        float x = Screen.width - 210f, y = 80f, w = 200f, h = 120f;
        GUI.Box(new Rect(x, y, w, h), "Object Inspector");
        GUI.Label(new Rect(x + 4f, y + 22f, w - 8f, 20f), _current.DisplayName);
        GUI.Label(new Rect(x + 4f, y + 44f, w - 8f, 20f), $"ID: {_current.EntityId}");
    }
}
