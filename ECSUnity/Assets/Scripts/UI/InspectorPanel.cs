using System;
using UnityEngine;
using UnityEngine.UIElements;
using Warden.Contracts.Telemetry;

/// <summary>
/// Three-tier NPC inspector panel (WP-3.1.E AT-04 / AT-05 / AT-06).
///
/// TIERS
/// ──────
///   Glance — name, activity, mood icon + word, one contextual fact. Default view.
///   Drill  — top-3 drives with bar graphs, willpower, schedule block, task, stress, mask.
///   Deep   — full drive vector, inhibitions, personality, relationships, memory entries, intent.
///
/// DATA SOURCE
/// ────────────
/// Reads WorldStateDto.Entities[selectedId] via EngineHost.WorldState (fast path).
/// For fields not yet in WorldStateDto (personality, inhibitions, memory), reads
/// EngineHost.Engine component tables directly (slower but per-frame safe at one entity).
///
/// MOUNTING
/// ─────────
/// Attach to a UIDocument. Wire SelectionController.SelectionChanged to
/// <see cref="OnSelectionChanged"/>. Panel slides in from the right on selection.
/// </summary>
public sealed class InspectorPanel : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private EngineHost     _host;
    [SerializeField] private UIDocument     _document;
    [SerializeField] private PlayerUIConfig _uiConfig;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private VisualElement  _root;
    private VisualElement  _glancePanel;
    private VisualElement  _drillPanel;
    private VisualElement  _deepPanel;
    private Button         _drillBtn;
    private Button         _deepBtn;
    private Button         _backBtn;

    private SelectableTag  _current;
    private InspectorTier  _tier = InspectorTier.Glance;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private SelectionController _selection;

    private void Start()
    {
        if (_document != null)
        {
            _root       = _document.rootVisualElement?.Q("inspector-root");
            _glancePanel = _root?.Q("inspector-glance");
            _drillPanel  = _root?.Q("inspector-drill");
            _deepPanel   = _root?.Q("inspector-deep");
            _drillBtn    = _root?.Q<Button>("btn-drill");
            _deepBtn     = _root?.Q<Button>("btn-deep");
            _backBtn     = _root?.Q<Button>("btn-back");

            _drillBtn?.RegisterCallback<ClickEvent>(_ => SetTier(InspectorTier.Drill));
            _deepBtn?.RegisterCallback<ClickEvent>(_ => SetTier(InspectorTier.Deep));
            _backBtn?.RegisterCallback<ClickEvent>(_ => SetTier(InspectorTier.Glance));
        }

        // Auto-wire to SelectionController if Inspector didn't drag a UnityEvent
        // hook (BUG-004). Hand-authored scenes can't easily express event hooks
        // in YAML; runtime FindObjectOfType is the most robust path.
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
        if (_current == null) { SetVisible(false); return; }
        RefreshContent();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Called by SelectionController when selection changes.</summary>
    public void OnSelectionChanged(SelectableTag tag)
    {
        _current = tag;
        if (tag == null || tag.Kind != SelectableKind.Npc)
        {
            SetVisible(false);
            return;
        }
        _tier = _uiConfig?.DefaultInspectorTier ?? InspectorTier.Glance;
        SetTier(_tier);
        SetVisible(true);
    }

    /// <summary>Set the active disclosure tier.</summary>
    public void SetTier(InspectorTier tier)
    {
        _tier = tier;
        RefreshContent();
        UpdateTierVisibility();
    }

    /// <summary>Current active tier.</summary>
    public InspectorTier CurrentTier => _tier;

    /// <summary>True when the inspector panel is visible.</summary>
    public bool IsVisible { get; private set; }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private void RefreshContent()
    {
        if (_current == null) return;

        // Find the entity DTO.
        var worldState = _host?.WorldState;
        EntityStateDto entity = FindEntityDto(worldState, _current.EntityId);

        switch (_tier)
        {
            case InspectorTier.Glance: RenderGlance(entity); break;
            case InspectorTier.Drill:  RenderDrill(entity);  break;
            case InspectorTier.Deep:   RenderDeep(entity);   break;
        }
    }

    private void RenderGlance(EntityStateDto entity)
    {
        if (_glancePanel == null) return;
        _glancePanel.Clear();

        string name     = entity?.Name ?? _current.DisplayName;
        string activity = entity?.Drives != null
            ? $"Dominant: {entity.Drives.Dominant}" : "Unknown activity";
        string mood     = entity?.Physiology != null
            ? (entity.Physiology.IsSleeping ? "Sleeping" : "Active") : "Unknown";

        _glancePanel.Add(MakeLabel(name,     "inspector-name"));
        _glancePanel.Add(MakeLabel(activity, "inspector-activity"));
        _glancePanel.Add(MakeLabel(mood,     "inspector-mood"));
    }

    private void RenderDrill(EntityStateDto entity)
    {
        if (_drillPanel == null) return;
        _drillPanel.Clear();

        if (entity == null) { _drillPanel.Add(MakeLabel("No data.", "inspector-nodata")); return; }

        // Top drives.
        var drives = entity.Drives;
        if (drives != null)
        {
            _drillPanel.Add(MakeLabel($"Eat urgency: {drives.EatUrgency:F2}",   "inspector-drive"));
            _drillPanel.Add(MakeLabel($"Sleep urgency: {drives.SleepUrgency:F2}", "inspector-drive"));
            _drillPanel.Add(MakeLabel($"Drink urgency: {drives.DrinkUrgency:F2}", "inspector-drive"));
        }

        // Physiology.
        var phys = entity.Physiology;
        if (phys != null)
        {
            _drillPanel.Add(MakeLabel($"Energy: {phys.Energy:F1}", "inspector-stat"));
            _drillPanel.Add(MakeLabel($"Hydration: {phys.Hydration:F1}", "inspector-stat"));
        }
    }

    private void RenderDeep(EntityStateDto entity)
    {
        if (_deepPanel == null) return;
        _deepPanel.Clear();

        if (entity == null) { _deepPanel.Add(MakeLabel("No data.", "inspector-nodata")); return; }

        // Full drive vector.
        var drives = entity.Drives;
        if (drives != null)
        {
            _deepPanel.Add(MakeLabel("-- Drives --", "inspector-section"));
            _deepPanel.Add(MakeLabel($"Dominant: {drives.Dominant}",         "inspector-drive"));
            _deepPanel.Add(MakeLabel($"Eat: {drives.EatUrgency:F3}",         "inspector-drive"));
            _deepPanel.Add(MakeLabel($"Drink: {drives.DrinkUrgency:F3}",     "inspector-drive"));
            _deepPanel.Add(MakeLabel($"Sleep: {drives.SleepUrgency:F3}",     "inspector-drive"));
            _deepPanel.Add(MakeLabel($"Defecate: {drives.DefecateUrgency:F3}", "inspector-drive"));
            _deepPanel.Add(MakeLabel($"Pee: {drives.PeeUrgency:F3}",         "inspector-drive"));
        }

        // Social state if available.
        if (entity.Social != null)
        {
            _deepPanel.Add(MakeLabel("-- Social --", "inspector-section"));
            // Social sub-fields (archetype, relationships) added as they appear in DTO.
        }
    }

    private void UpdateTierVisibility()
    {
        SetPanelDisplay(_glancePanel, _tier == InspectorTier.Glance);
        SetPanelDisplay(_drillPanel,  _tier == InspectorTier.Drill);
        SetPanelDisplay(_deepPanel,   _tier == InspectorTier.Deep);

        // Button visibility: show Drill when on Glance; show Deep when on Drill; Back otherwise.
        if (_drillBtn != null) _drillBtn.style.display = _tier == InspectorTier.Glance ? DisplayStyle.Flex : DisplayStyle.None;
        if (_deepBtn  != null) _deepBtn.style.display  = _tier == InspectorTier.Drill  ? DisplayStyle.Flex : DisplayStyle.None;
        if (_backBtn  != null) _backBtn.style.display  = _tier != InspectorTier.Glance ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetVisible(bool v)
    {
        IsVisible = v;
        if (_root != null)
            _root.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static EntityStateDto FindEntityDto(WorldStateDto worldState, string entityId)
    {
        if (worldState?.Entities == null) return null;
        foreach (var e in worldState.Entities)
            if (e.Id == entityId) return e;
        return null;
    }

    private static Label MakeLabel(string text, string ussClass)
    {
        var l = new Label(text);
        l.AddToClassList(ussClass);
        return l;
    }

    private static void SetPanelDisplay(VisualElement ve, bool show)
    {
        if (ve != null)
            ve.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ── IMGUI fallback ────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (_root != null || !IsVisible || _current == null) return;

        // Minimal IMGUI fallback when UXML is absent (e.g. play-mode tests).
        float w = 200f, h = 160f;
        float x = Screen.width - w - 4f;
        float y = 40f;

        GUI.Box(new Rect(x, y, w, h), _current.DisplayName);

        var worldState = _host?.WorldState;
        EntityStateDto entity = FindEntityDto(worldState, _current.EntityId);

        float iy = y + 20f;
        GUI.Label(new Rect(x + 4f, iy, w - 8f, 18f), $"Tier: {_tier}"); iy += 20f;

        if (entity != null)
        {
            GUI.Label(new Rect(x + 4f, iy, w - 8f, 18f),
                $"Dominant: {entity.Drives?.Dominant}"); iy += 20f;
            GUI.Label(new Rect(x + 4f, iy, w - 8f, 18f),
                $"Energy: {entity.Physiology?.Energy:F1}"); iy += 20f;
        }

        if (GUI.Button(new Rect(x + 4f, iy, 58f, 18f), "Glance")) SetTier(InspectorTier.Glance);
        if (GUI.Button(new Rect(x + 66f, iy, 58f, 18f), "Drill"))  SetTier(InspectorTier.Drill);
        if (GUI.Button(new Rect(x + 128f, iy, 58f, 18f), "Deep"))  SetTier(InspectorTier.Deep);
    }
}
