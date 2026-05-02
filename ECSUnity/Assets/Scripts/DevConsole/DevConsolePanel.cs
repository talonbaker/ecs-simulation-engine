#if WARDEN
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Minecraft-style developer console panel — WP-3.1.H.
///
/// TOGGLE: BackQuote (`) / Tilde (~) key opens/closes the panel.
/// INPUT:  Enter submits the command; Up/Down navigates history; Tab autocompletes.
///
/// OUTPUT HISTORY: scrollable list of ConsoleEntry values, coloured by kind.
///
/// MOUNTING
/// ────────
/// Attach to a persistent GameObject alongside EngineHost.
/// Assign _config, _document, and optionally _host, _mutationApiSource,
/// _saveLoadPanel, _timeHudPanel, _emitter in the Inspector.
///
/// The panel self-registers all built-in commands on Awake.
/// </summary>
public sealed class DevConsolePanel : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private DevConsoleConfig   _config;
    [SerializeField] private UIDocument         _document;
    [SerializeField] private EngineHost         _host;
    [SerializeField] private SaveLoadPanel      _saveLoadPanel;
    [SerializeField] private TimeHudPanel       _timeHudPanel;
    [SerializeField] private JsonlStreamEmitter _emitter;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool                          _isVisible;
    private readonly List<ConsoleEntry>   _history      = new List<ConsoleEntry>();
    private readonly List<string>         _cmdHistory   = new List<string>();  // navigation
    private int                           _cmdHistoryIdx = -1;
    private string                        _savedInput   = string.Empty;

    // ── IMGUI fallback (used when _document is not assigned in the scene) ─────
    private Vector2    _guiScrollPos;
    private GUIStyle   _guiLabel;
    private GUIStyle   _guiInput;
    private Texture2D  _guiBg;

    // ── Services ──────────────────────────────────────────────────────────────

    private DevConsoleCommandDispatcher   _dispatcher;
    private DevConsoleAutocomplete        _autocomplete;
    private DevConsoleHistoryPersister    _persister;

    // ── UI elements ───────────────────────────────────────────────────────────

    private VisualElement _root;
    private ScrollView    _historyScroll;
    private TextField     _inputField;

    // ── Public API ────────────────────────────────────────────────────────────

    public bool                         IsVisible  => _isVisible;
    public IReadOnlyList<ConsoleEntry>  GetHistory() => _history;
    public string                       CurrentInput => _inputField?.value ?? _savedInput;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _config ??= ScriptableObject.CreateInstance<DevConsoleConfig>();

        _dispatcher   = new DevConsoleCommandDispatcher();
        _autocomplete = new DevConsoleAutocomplete(_dispatcher);
        _persister    = new DevConsoleHistoryPersister();

        RegisterBuiltinCommands();
        RefreshContext();
    }

    private void Start()
    {
        if (_document != null)
        {
            _root          = _document.rootVisualElement?.Q("devconsole-root");
            _historyScroll = _root?.Q<ScrollView>("devconsole-history");
            _inputField    = _root?.Q<TextField>("devconsole-input");

            if (_inputField != null)
            {
                _inputField.RegisterCallback<KeyDownEvent>(OnInputKeyDown, TrickleDown.TrickleDown);
            }
        }

        SetVisible(false);

        // Load persisted command history.
        if (_config.PersistHistory)
        {
            var loaded = _persister.Load(_config.HistoryFilePath);
            _cmdHistory.AddRange(loaded);
        }
    }

    private void Update()
    {
        // Toggle key: BackQuote (`) or any configured toggle key.
        if (Input.GetKeyDown(_config.ToggleKey))
            Toggle();
    }

    private void OnDestroy()
    {
        if (_config.PersistHistory)
            _persister.Save(_cmdHistory, _config.HistoryFilePath, _config.MaxCommandHistory);
    }

    // ── Open / close ──────────────────────────────────────────────────────────

    public void Toggle()   => SetVisible(!_isVisible);
    public void Open()     => SetVisible(true);
    public void Close()    => SetVisible(false);

    public void SetVisible(bool v)
    {
        _isVisible = v;
        if (_root != null)
            _root.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;

        if (v)
        {
            RefreshContext();
            _inputField?.Focus();
        }
    }

    // ── Command submission ────────────────────────────────────────────────────

    /// <summary>
    /// Programmatically submit a command (used in tests and by the Enter key).
    /// Adds the raw command to visible history, then the output.
    /// </summary>
    public void SubmitCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        // Echo the command.
        AddEntry(new ConsoleEntry($"> {input}", ConsoleEntryKind.Command));

        // Push to navigation history.
        if (_cmdHistory.Count == 0 || _cmdHistory[_cmdHistory.Count - 1] != input)
            _cmdHistory.Add(input);

        while (_cmdHistory.Count > _config.MaxCommandHistory)
            _cmdHistory.RemoveAt(0);

        _cmdHistoryIdx = -1;

        // Execute.
        _dispatcher.Execute(input, out string output);

        if (!string.IsNullOrEmpty(output))
        {
            ConsoleEntryKind kind = ConsoleEntryKind.Success;
            string text = output;

            if (output.StartsWith("ERROR:"))   { kind = ConsoleEntryKind.Error;   text = output.Substring(6).Trim(); }
            else if (output.StartsWith("INFO:")) { kind = ConsoleEntryKind.Info;  text = output.Substring(5).Trim(); }

            AddEntry(new ConsoleEntry(text, kind));
        }

        // Clear the input field.
        if (_inputField != null) _inputField.value = string.Empty;
    }

    // ── History input ─────────────────────────────────────────────────────────

    public void AddEntry(ConsoleEntry entry)
    {
        _history.Add(entry);

        // Cap to MaxHistoryLines.
        while (_history.Count > _config.MaxHistoryLines)
            _history.RemoveAt(0);

        if (_historyScroll == null) return;

        var label = new Label(entry.Text);
        label.AddToClassList("console-history-line");
        Color c = DevConsoleColorPalette.FromKind(entry.Kind);
        label.style.color = new StyleColor(c);
        _historyScroll.Add(label);

        // Scroll to bottom.
        _historyScroll.ScrollTo(label);
    }

    public void ClearHistory()
    {
        _history.Clear();
        _historyScroll?.Clear();
    }

    // ── History navigation ────────────────────────────────────────────────────

    /// <summary>Navigate up (older) in command history.</summary>
    public void NavigateHistoryUp()
    {
        if (_cmdHistory.Count == 0) return;

        if (_cmdHistoryIdx == -1)
        {
            // Save what the user was typing before navigating.
            _savedInput    = _inputField?.value ?? string.Empty;
            _cmdHistoryIdx = _cmdHistory.Count - 1;
        }
        else if (_cmdHistoryIdx > 0)
        {
            _cmdHistoryIdx--;
        }

        SetInput(_cmdHistory[_cmdHistoryIdx]);
    }

    /// <summary>Navigate down (newer) in command history.</summary>
    public void NavigateHistoryDown()
    {
        if (_cmdHistoryIdx == -1) return;

        if (_cmdHistoryIdx < _cmdHistory.Count - 1)
        {
            _cmdHistoryIdx++;
            SetInput(_cmdHistory[_cmdHistoryIdx]);
        }
        else
        {
            _cmdHistoryIdx = -1;
            SetInput(_savedInput);
        }
    }

    public void SetInput(string text)
    {
        _savedInput = text;
        if (_inputField != null) _inputField.value = text;
    }

    // ── Autocomplete ──────────────────────────────────────────────────────────

    public void TriggerAutocomplete()
    {
        string current   = _inputField?.value ?? string.Empty;
        string completed = _autocomplete.Cycle(current, _host);
        SetInput(completed);
    }

    // ── Keyboard handling (UI Toolkit) ────────────────────────────────────────

    private void OnInputKeyDown(KeyDownEvent evt)
    {
        switch (evt.keyCode)
        {
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                SubmitCommand(_inputField?.value ?? string.Empty);
                _autocomplete.Reset();
                evt.StopPropagation();
                break;

            case KeyCode.UpArrow:
                NavigateHistoryUp();
                _autocomplete.Reset();
                evt.StopPropagation();
                break;

            case KeyCode.DownArrow:
                NavigateHistoryDown();
                _autocomplete.Reset();
                evt.StopPropagation();
                break;

            case KeyCode.Tab:
                TriggerAutocomplete();
                evt.StopPropagation();
                break;

            case KeyCode.Escape:
                Close();
                evt.StopPropagation();
                break;

            default:
                _autocomplete.Reset();
                break;
        }
    }

    // ── IMGUI fallback ────────────────────────────────────────────────────────

    private string _imguiInput = string.Empty;

    private void OnGUI()
    {
        if (_root != null || !_isVisible) return;

        float panelH = Screen.height * _config.PanelHeightFraction;
        float y      = Screen.height - panelH;

        // Dark background.
        GUI.Box(new Rect(0, y, Screen.width, panelH), GUIContent.none);

        // History lines.
        float lineH  = 16f;
        int   maxVis = Mathf.FloorToInt((panelH - 28f) / lineH);
        int   start  = Mathf.Max(0, _history.Count - maxVis);
        for (int i = start; i < _history.Count; i++)
        {
            Color prev = GUI.contentColor;
            GUI.contentColor = DevConsoleColorPalette.FromKind(_history[i].Kind);
            GUI.Label(new Rect(4f, y + (i - start) * lineH, Screen.width - 8f, lineH),
                _history[i].Text);
            GUI.contentColor = prev;
        }

        // Input field.
        GUI.SetNextControlName("DevConsoleInput");
        _imguiInput = GUI.TextField(
            new Rect(0, Screen.height - 22f, Screen.width - 60f, 20f),
            _imguiInput);

        if (GUI.Button(new Rect(Screen.width - 58f, Screen.height - 22f, 56f, 20f), "Submit"))
        {
            SubmitCommand(_imguiInput);
            _imguiInput = string.Empty;
        }
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private void RefreshContext()
    {
        _dispatcher.SetContext(new DevCommandContext
        {
            Host       = _host,
            MutationApi = null, // wired by SceneBootstrapper or Inspector
            SaveLoad   = _saveLoadPanel,
            TimeHud    = _timeHudPanel,
            Console    = this,
            Emitter    = _emitter,
        });
    }

    private void RegisterBuiltinCommands()
    {
        _dispatcher.RegisterCommand(new HelpCommand(_dispatcher));
        _dispatcher.RegisterCommand(new InspectCommand());
        _dispatcher.RegisterCommand(new InspectRoomCommand());
        _dispatcher.RegisterCommand(new SpawnCommand());
        _dispatcher.RegisterCommand(new DespawnCommand());
        _dispatcher.RegisterCommand(new MoveCommand());
        _dispatcher.RegisterCommand(new ForceKillCommand());
        _dispatcher.RegisterCommand(new ForceFaintCommand());
        _dispatcher.RegisterCommand(new ScenarioCommand());
        _dispatcher.RegisterCommand(new SetComponentCommand());
        _dispatcher.RegisterCommand(new LockCommand());
        _dispatcher.RegisterCommand(new UnlockCommand());
        _dispatcher.RegisterCommand(new TickRateCommand());
        _dispatcher.RegisterCommand(new PauseCommand());
        _dispatcher.RegisterCommand(new ResumeCommand());
        _dispatcher.RegisterCommand(new SaveCommand());
        _dispatcher.RegisterCommand(new LoadCommand());
        _dispatcher.RegisterCommand(new SeedCommand());
        _dispatcher.RegisterCommand(new ClearCommand());
        _dispatcher.RegisterCommand(new HistoryCommand());
        _dispatcher.RegisterCommand(new QuitCommand());
    }

    // ── Test accessors ─────────────────────────────────────────────────────────

    public DevConsoleCommandDispatcher Dispatcher    => _dispatcher;
    public int                         CmdHistoryCount => _cmdHistory.Count;

    public void SetHost(EngineHost host)
    {
        _host = host;
        RefreshContext();
    }

    public void SetMutationApi(APIFramework.Mutation.IWorldMutationApi api)
    {
        var ctx = _dispatcher.Context ?? new DevCommandContext();
        ctx.MutationApi = api;
        _dispatcher.SetContext(ctx);
    }

    // ── IMGUI fallback ────────────────────────────────────────────────────────
    // Activates only when _document is not wired in the scene. Provides the
    // same command surface with a plain IMGUI panel so the console works
    // without a UIDocument / PanelSettings asset.

    private void OnGUI()
    {
        if (!_isVisible || _document != null) return;

        // Lazy-init styles.
        if (_guiLabel == null)
        {
            _guiLabel = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false };
            _guiLabel.normal.textColor = new Color(0.87f, 0.87f, 0.87f);

            _guiInput = new GUIStyle(GUI.skin.textField);
            _guiInput.normal.textColor  = Color.white;
            _guiInput.focused.textColor = Color.white;

            _guiBg        = new Texture2D(1, 1);
            _guiBg.SetPixel(0, 0, new Color(0.067f, 0.067f, 0.094f, 0.96f));
            _guiBg.Apply();
        }

        float panelH  = Screen.height * (_config?.PanelHeightFraction ?? 0.45f);
        float panelY  = Screen.height - panelH;
        var   panel   = new Rect(0, panelY, Screen.width, panelH);

        // Dark background.
        GUI.DrawTexture(panel, _guiBg);

        GUILayout.BeginArea(panel);

        // Title bar.
        GUILayout.Label(
            "<color=#777788>WARDEN CONSOLE  ~: close | Enter: submit | ↑↓: history</color>",
            _guiLabel);

        // History scroll view.
        const float kInputRowH = 28f;
        const float kTitleH    = 22f;
        _guiScrollPos = GUILayout.BeginScrollView(
            _guiScrollPos,
            GUILayout.Height(panelH - kTitleH - kInputRowH - 4f));

        foreach (var entry in _history)
        {
            string hex = DevConsoleColorPalette.ToHex(DevConsoleColorPalette.FromKind(entry.Kind));
            GUILayout.Label($"<color={hex}>{entry.Text}</color>", _guiLabel);
        }
        GUILayout.EndScrollView();

        // Input row.
        GUILayout.BeginHorizontal(GUILayout.Height(kInputRowH));
        GUILayout.Label("<color=#3CB371>></color>", _guiLabel, GUILayout.Width(18));
        GUI.SetNextControlName("warden-console-input");
        _savedInput = GUILayout.TextField(_savedInput ?? string.Empty, _guiInput);

        if (Event.current.type == EventType.KeyDown)
        {
            switch (Event.current.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (!string.IsNullOrWhiteSpace(_savedInput))
                    {
                        string toSubmit = _savedInput;
                        _savedInput     = string.Empty;
                        SubmitCommand(toSubmit);
                        _guiScrollPos.y = float.MaxValue;
                    }
                    Event.current.Use();
                    break;

                case KeyCode.UpArrow:
                    NavigateHistoryUp();
                    Event.current.Use();
                    break;

                case KeyCode.DownArrow:
                    NavigateHistoryDown();
                    Event.current.Use();
                    break;
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        GUI.FocusControl("warden-console-input");
    }
}

#endif
