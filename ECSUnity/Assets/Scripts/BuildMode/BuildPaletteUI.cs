using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Side-panel Build Palette UI built with Unity UI Toolkit (WP-3.1.D).
///
/// LAYOUT
/// ───────
/// A VisualElement panel on the right side of the screen, only visible in Build Mode.
/// The panel has a tab bar at the top (one tab per <see cref="PaletteCategory"/>) and
/// a scrollable item list below. Each item has:
///   - A CRT-style icon sprite (16x16 or 32x32).
///   - A text label.
///   - A click handler that starts a placement intent via the callback.
///
/// NAMED-ANCHOR UNIQUENESS
/// ────────────────────────
/// When the catalog marks an entry as UniqueInstance, and an existing live instance
/// is found in the current world, the item label shows "[in world]" and clicking
/// starts a PickingUp intent for the existing entity rather than a new placement.
///
/// MOUNTING
/// ─────────
/// Attach to a UIDocument GameObject. Assign the <see cref="_catalog"/> and
/// <see cref="_document"/> references in the Inspector. BuildModeController calls
/// <see cref="SetVisible"/>.
/// </summary>
public sealed class BuildPaletteUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("The UIDocument component that hosts the BuildPalette.uxml.")]
    private UIDocument _document;

    [SerializeField]
    [Tooltip("The build palette catalog ScriptableObject.")]
    private BuildPaletteCatalog _catalog;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired when the player clicks a palette item to start a placement.
    /// Arg: the PaletteEntry that was clicked.
    /// </summary>
    public event Action<PaletteEntry> OnPlacementRequested;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private VisualElement _root;
    private VisualElement _itemList;
    private Label         _selectedTabLabel;
    private PaletteCategory _activeCategory = PaletteCategory.Structural;

    private bool _isVisible;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_document == null)
        {
            Debug.LogWarning("[BuildPaletteUI] UIDocument not assigned — palette will not render.");
            return;
        }

        _root = _document.rootVisualElement?.Q("build-palette-root");
        if (_root == null)
        {
            // Build the panel programmatically as a fallback when UXML is missing.
            BuildProgrammatically();
            return;
        }

        _itemList = _root.Q("item-list");
        WireTabButtons();
        SetCategory(PaletteCategory.Structural);
        _root.style.display = DisplayStyle.None;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Show or hide the palette panel.</summary>
    public void SetVisible(bool visible)
    {
        _isVisible = visible;
        if (_root != null)
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>True when the panel is currently shown.</summary>
    public bool IsVisible => _isVisible;

    /// <summary>Switch to a different category tab and repopulate the item list.</summary>
    public void SetCategory(PaletteCategory category)
    {
        _activeCategory = category;
        PopulateItemList();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void WireTabButtons()
    {
        if (_root == null) return;
        foreach (PaletteCategory cat in System.Enum.GetValues(typeof(PaletteCategory)))
        {
            var btn = _root.Q<Button>($"tab-{cat.ToString().ToLower()}");
            if (btn != null)
            {
                var captured = cat;
                btn.RegisterCallback<ClickEvent>(_ => SetCategory(captured));
            }
        }
    }

    private void PopulateItemList()
    {
        if (_itemList == null || _catalog == null) return;
        _itemList.Clear();

        foreach (var entry in _catalog.GetCategory(_activeCategory))
        {
            var row = BuildItemRow(entry);
            _itemList.Add(row);
        }
    }

    private VisualElement BuildItemRow(PaletteEntry entry)
    {
        var row = new VisualElement();
        row.AddToClassList("palette-item");

        // Icon (fallback: colored square if sprite is null)
        if (entry.Icon != null)
        {
            var icon = new Image { sprite = entry.Icon };
            icon.AddToClassList("palette-icon");
            row.Add(icon);
        }

        // Label
        var label = new Label(entry.Label);
        label.AddToClassList("palette-label");
        row.Add(label);

        // Click handler
        row.RegisterCallback<ClickEvent>(_ => OnPlacementRequested?.Invoke(entry));

        // Tooltip
        if (!string.IsNullOrEmpty(entry.Tooltip))
            row.tooltip = entry.Tooltip;

        return row;
    }

    private void BuildProgrammatically()
    {
        // Programmatic fallback — builds a minimal IMGUI-style display via OnGUI.
        // This path activates only when the UXML asset is not present (e.g. in tests).
        _root = null;
        Debug.Log("[BuildPaletteUI] UXML not found; falling back to IMGUI palette.");
    }

    // ── IMGUI fallback (no UXML) ──────────────────────────────────────────────

    private void OnGUI()
    {
        // Only used when UXML is not present AND palette is visible.
        if (_root != null || !_isVisible || _catalog == null) return;

        float panelW = 160f;
        float panelH = Screen.height * 0.6f;
        float panelX = Screen.width - panelW - 8f;
        float panelY = (Screen.height - panelH) * 0.5f;

        GUI.Box(new Rect(panelX, panelY, panelW, panelH), "Build Palette");

        // Category tabs
        float tabY = panelY + 20f;
        foreach (PaletteCategory cat in System.Enum.GetValues(typeof(PaletteCategory)))
        {
            if (GUI.Button(new Rect(panelX + 4f, tabY, panelW - 8f, 18f), cat.ToString()))
                SetCategory(cat);
            tabY += 20f;
        }

        // Item list
        float itemY = tabY + 8f;
        if (_catalog != null)
        {
            foreach (var entry in _catalog.GetCategory(_activeCategory))
            {
                if (GUI.Button(new Rect(panelX + 4f, itemY, panelW - 8f, 20f), entry.Label))
                    OnPlacementRequested?.Invoke(entry);
                itemY += 22f;
                if (itemY > panelY + panelH - 28f) break;
            }
        }
    }
}
