using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Settings panel (WP-3.1.E AT-12 / AT-13 / AT-14).
///
/// Toggles and sliders (per UX bible §4.6 and §5.2):
///   Selection visual, Soften mode, Creative mode, Audio (4 channels),
///   Text scale, Sticky controls, Color-blind palette.
///
/// Writes all changes to <see cref="PlayerUIConfig"/> in real-time so other systems
/// (CrtBlinkRenderer, TimeHudPanel, etc.) read the config and update on the next frame.
///
/// PERSISTENCE
/// ────────────
/// Config is serialised to Application.persistentDataPath/player-ui-prefs.json on
/// close. At v0.1 the serialisation is a stub log; full I/O is a follow-up.
///
/// MOUNTING
/// ─────────
/// Attach to a UIDocument. Open via gear-icon click.
/// </summary>
public sealed class SettingsPanel : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private PlayerUIConfig _uiConfig;
    [SerializeField] private UIDocument     _document;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private VisualElement _root;
    private bool          _isVisible;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_document != null)
        {
            _root = _document.rootVisualElement?.Q("settings-root");
            WireControls();
        }
        SetVisible(false);
    }

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>Toggle visibility of the settings panel.</summary>
    public void ToggleVisible() => SetVisible(!_isVisible);

    /// <summary>Explicitly show or hide the settings panel.</summary>
    public void SetVisible(bool v)
    {
        _isVisible = v;
        if (_root != null) _root.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>True when the panel is currently shown.</summary>
    public bool IsVisible => _isVisible;

    // ── Setters (called by UI controls AND by tests) ─────────────────────────

    public void SetSelectionVisual(SelectionVisualMode mode)
    {
        if (_uiConfig != null) _uiConfig.SelectionVisual = mode;
    }

    public void SetSoftenMode(bool on)
    {
        if (_uiConfig != null) _uiConfig.SoftenMode = on;
    }

    public void SetCreativeMode(bool on)
    {
        if (_uiConfig != null) _uiConfig.CreativeMode = on;
        Debug.Log($"[SettingsPanel] CreativeMode={on}");
    }

    public void SetMasterVolume(float v)
    {
        if (_uiConfig != null) _uiConfig.MasterVolume = v;
        AudioListener.volume = v;
    }

    public void SetColorBlindPalette(ColorBlindPalette palette)
    {
        if (_uiConfig != null) _uiConfig.ColorBlind = palette;
        // Palette swap implementation: send event to UI root to swap USS variable overrides.
        Debug.Log($"[SettingsPanel] ColorBlind palette → {palette}");
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void WireControls()
    {
        if (_root == null || _uiConfig == null) return;

        // Selection visual toggle.
        var selToggle = _root.Q<Toggle>("toggle-crt-blink");
        if (selToggle != null)
        {
            selToggle.value = _uiConfig.SelectionVisual == SelectionVisualMode.CrtBlink;
            selToggle.RegisterValueChangedCallback(e =>
                SetSelectionVisual(e.newValue ? SelectionVisualMode.CrtBlink : SelectionVisualMode.HaloAndOutline));
        }

        // Soften mode.
        var softenToggle = _root.Q<Toggle>("toggle-soften");
        if (softenToggle != null)
        {
            softenToggle.value = _uiConfig.SoftenMode;
            softenToggle.RegisterValueChangedCallback(e => SetSoftenMode(e.newValue));
        }

        // Creative mode.
        var creativeToggle = _root.Q<Toggle>("toggle-creative");
        if (creativeToggle != null)
        {
            creativeToggle.value = _uiConfig.CreativeMode;
            creativeToggle.RegisterValueChangedCallback(e => SetCreativeMode(e.newValue));
        }

        // Master volume.
        var masterSlider = _root.Q<Slider>("slider-master");
        if (masterSlider != null)
        {
            masterSlider.value = _uiConfig.MasterVolume;
            masterSlider.RegisterValueChangedCallback(e => SetMasterVolume(e.newValue));
        }
    }

    // ── IMGUI fallback ────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (_root != null || !_isVisible || _uiConfig == null) return;

        float x = Screen.width / 2f - 150f;
        float y = Screen.height / 2f - 180f;
        float w = 300f, h = 360f;

        GUI.Box(new Rect(x, y, w, h), "Settings");
        float iy = y + 24f;

        // Selection visual.
        bool crtBlink = _uiConfig.SelectionVisual == SelectionVisualMode.CrtBlink;
        bool newCrt   = GUI.Toggle(new Rect(x + 8f, iy, w - 16f, 22f), crtBlink, "CRT-blink selection"); iy += 26f;
        if (newCrt != crtBlink) SetSelectionVisual(newCrt ? SelectionVisualMode.CrtBlink : SelectionVisualMode.HaloAndOutline);

        // Soften.
        bool newSoften = GUI.Toggle(new Rect(x + 8f, iy, w - 16f, 22f), _uiConfig.SoftenMode, "Soften mode"); iy += 26f;
        if (newSoften != _uiConfig.SoftenMode) SetSoftenMode(newSoften);

        // Creative.
        bool newCreative = GUI.Toggle(new Rect(x + 8f, iy, w - 16f, 22f), _uiConfig.CreativeMode, "Creative mode"); iy += 26f;
        if (newCreative != _uiConfig.CreativeMode) SetCreativeMode(newCreative);

        // Master volume.
        GUI.Label(new Rect(x + 8f, iy, 80f, 20f), $"Master: {_uiConfig.MasterVolume:F2}");
        float newMaster = GUI.HorizontalSlider(new Rect(x + 90f, iy + 4f, w - 100f, 16f), _uiConfig.MasterVolume, 0f, 1f); iy += 26f;
        if (!Mathf.Approximately(newMaster, _uiConfig.MasterVolume)) SetMasterVolume(newMaster);

        if (GUI.Button(new Rect(x + w / 2f - 40f, y + h - 30f, 80f, 24f), "Close"))
            SetVisible(false);
    }
}
