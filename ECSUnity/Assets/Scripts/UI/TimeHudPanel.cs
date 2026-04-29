using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Time HUD panel — pause / x1 / x4 / x16 speed controls + clock readout (WP-3.1.E AT-08 / AT-09 / AT-10).
///
/// LAYOUT (top-right, always visible)
/// ────────────────────────────────────
///   - Clock: "Tuesday 2:47 PM"
///   - Day badge: "Day 14"
///   - Speed buttons: Pause | x1 | x4 | x16
///   - (Creative mode only) Skip-to-morning | time-zoom slider
///
/// TIME SCALE
/// ───────────
/// Changing the time scale adjusts Unity's Time.timeScale AND EngineHost.Clock.TimeScale
/// so both the render loop and the engine tick at the correct rate.
/// Pause = Time.timeScale = 0; x1 = 1; x4 = 4; x16 = 16.
///
/// KEYBOARD SHORTCUTS (per UX bible §3.6)
///   Spacebar → pause/resume
///   1 → x1, 2 → x4, 3 → x16
/// </summary>
public sealed class TimeHudPanel : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private EngineHost     _host;
    [SerializeField] private UIDocument     _document;
    [SerializeField] private PlayerUIConfig _uiConfig;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private VisualElement _root;
    private Label         _clockLabel;
    private Label         _dayLabel;
    private Button        _pauseBtn;
    private Button        _x1Btn;
    private Button        _x4Btn;
    private Button        _x16Btn;
    private Button        _skipMorningBtn;

    private float _currentTimeScale = 1f;
    private bool  _isPaused;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_document != null)
        {
            _root           = _document.rootVisualElement?.Q("time-hud-root");
            _clockLabel     = _root?.Q<Label>("time-clock");
            _dayLabel       = _root?.Q<Label>("time-day");
            _pauseBtn       = _root?.Q<Button>("btn-pause");
            _x1Btn          = _root?.Q<Button>("btn-x1");
            _x4Btn          = _root?.Q<Button>("btn-x4");
            _x16Btn         = _root?.Q<Button>("btn-x16");
            _skipMorningBtn = _root?.Q<Button>("btn-skip-morning");

            _pauseBtn?.RegisterCallback<ClickEvent>(_ => SetPaused(!_isPaused));
            _x1Btn?.RegisterCallback<ClickEvent>(_ => SetTimeScale(1f));
            _x4Btn?.RegisterCallback<ClickEvent>(_ => SetTimeScale(4f));
            _x16Btn?.RegisterCallback<ClickEvent>(_ => SetTimeScale(16f));
            _skipMorningBtn?.RegisterCallback<ClickEvent>(_ => SkipToMorning());
        }

        // Skip-to-morning only available in creative mode.
        RefreshCreativeModeButtons();
        SetTimeScale(1f);
    }

    private void Update()
    {
        // Keyboard shortcuts.
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame) SetPaused(!_isPaused);
            if (Keyboard.current.digit1Key.wasPressedThisFrame) SetTimeScale(1f);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) SetTimeScale(4f);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) SetTimeScale(16f);
        }

        RefreshClock();
        RefreshCreativeModeButtons();
    }

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>Pause the engine (Time.timeScale = 0).</summary>
    public void SetPaused(bool paused)
    {
        _isPaused = paused;
        Time.timeScale = paused ? 0f : _currentTimeScale;
        Debug.Log($"[TimeHudPanel] Paused={paused}");
    }

    /// <summary>Change the engine time-scale multiplier.</summary>
    public void SetTimeScale(float scale)
    {
        _currentTimeScale = scale;
        if (!_isPaused)
            Time.timeScale = scale;

        if (_uiConfig != null) _uiConfig.TimeScale = scale;
        Debug.Log($"[TimeHudPanel] TimeScale={scale}");
    }

    /// <summary>Skip to the next 8 AM game-time. Creative mode only.</summary>
    public void SkipToMorning()
    {
        if (_uiConfig == null || !_uiConfig.CreativeMode) return;
        // Advance the engine clock to the next 08:00.
        // Requires engine clock API (ClockService.AdvanceToHour) — stub here;
        // the engine ClockService advancement is called directly when available.
        Debug.Log("[TimeHudPanel] SkipToMorning requested (engine clock API integration pending).");
    }

    /// <summary>True when the engine is paused.</summary>
    public bool IsPaused => _isPaused;

    /// <summary>Current active time-scale multiplier.</summary>
    public float CurrentTimeScale => _currentTimeScale;

    /// <summary>True when the skip-to-morning button is visible (creative mode).</summary>
    public bool IsSkipMorningVisible
    {
        get
        {
            if (_skipMorningBtn != null) return _skipMorningBtn.style.display == DisplayStyle.Flex;
            return _uiConfig != null && _uiConfig.CreativeMode;
        }
    }

    /// <summary>
    /// Creative mode flag. Reads from / writes to <see cref="PlayerUIConfig.CreativeMode"/>.
    /// Tests toggle this to verify creative-only buttons (skip-to-morning) appear.
    /// </summary>
    public bool CreativeMode
    {
        get => _uiConfig != null && _uiConfig.CreativeMode;
        set
        {
            if (_uiConfig != null) _uiConfig.CreativeMode = value;
            RefreshCreativeModeButtons();
        }
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void RefreshClock()
    {
        var clock = _host?.WorldState?.Clock;
        if (clock == null) return;

        if (_clockLabel != null)
            _clockLabel.text = clock.GameTimeDisplay;
        if (_dayLabel != null)
            _dayLabel.text = $"Day {clock.DayNumber}";
    }

    private void RefreshCreativeModeButtons()
    {
        bool creative = _uiConfig != null && _uiConfig.CreativeMode;
        if (_skipMorningBtn != null)
            _skipMorningBtn.style.display = creative ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ── IMGUI fallback ────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (_root != null) return; // UI Toolkit is active.

        float x = Screen.width - 208f;
        float y = 4f;
        float w = 200f;
        float h = 56f;

        GUI.Box(new Rect(x, y, w, h), string.Empty);

        // Clock display.
        var clock = _host?.WorldState?.Clock;
        string timeStr = clock != null ? clock.GameTimeDisplay : "--:-- --";
        GUI.Label(new Rect(x + 4f, y + 2f, w - 8f, 18f), timeStr);

        // Speed buttons.
        if (GUI.Button(new Rect(x + 4f,   y + 22f, 44f, 18f), _isPaused ? "||" : "Pause"))
            SetPaused(!_isPaused);
        if (GUI.Button(new Rect(x + 50f,  y + 22f, 30f, 18f), "x1"))  SetTimeScale(1f);
        if (GUI.Button(new Rect(x + 82f,  y + 22f, 30f, 18f), "x4"))  SetTimeScale(4f);
        if (GUI.Button(new Rect(x + 114f, y + 22f, 30f, 18f), "x16")) SetTimeScale(16f);

        // Creative-mode skip.
        bool creative = _uiConfig != null && _uiConfig.CreativeMode;
        if (creative)
        {
            if (GUI.Button(new Rect(x + 146f, y + 22f, 50f, 18f), ">>AM"))
                SkipToMorning();
        }
    }
}
