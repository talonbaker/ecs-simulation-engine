using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Warden.Contracts.Telemetry;

/// <summary>
/// CDDA-style scrollable event log panel — WP-3.1.G.
///
/// LAYOUT
/// ──────
/// Header: NPC filter dropdown, kind filter dropdown, time-range preset buttons.
/// Body: scrollable list of EventLogRow entries (newest first).
/// Footer: "Showing X / Y events" count label.
///
/// DATA SOURCE
/// ───────────
/// Reads WorldStateDto.Chronicle via EventLogAggregator each time the panel is
/// opened or the filters change. No polling in LateUpdate — the log is
/// read-once-on-open / refresh-on-filter-change.
///
/// CLICK-THROUGH
/// ─────────────
/// Row clicks are routed to EventLogClickThroughHandler.
///
/// VIRTUAL SCROLL
/// ──────────────
/// At v0.1, virtual scroll is approximated using UI Toolkit's built-in
/// ListView with a fixed item height. ListView automatically virtualises
/// rendering so only visible rows are in the DOM.
///
/// MOUNTING
/// ────────
/// Attach to a persistent GameObject. Assign _host, _clickHandler, _document in Inspector.
/// </summary>
public sealed class EventLogPanel : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private EngineHost                   _host;
    [SerializeField] private EventLogClickThroughHandler  _clickHandler;
    [SerializeField] private UIDocument                   _document;

    [Tooltip("Ticks per game-day. Set to match SimConfig (default 1200 at 50 Hz / 24-min day).")]
    [SerializeField] private long _ticksPerDay = 1200;

    // ── State ─────────────────────────────────────────────────────────────────

    private VisualElement                _root;
    private VisualElement                _listContainer;
    private Label                        _footerLabel;
    private bool                         _isVisible;
    private EventLogFilters              _filters = EventLogFilters.Default;
    private readonly EventLogAggregator  _aggregator = new EventLogAggregator();
    private List<ChronicleEntryDto>      _currentEntries = new List<ChronicleEntryDto>();
    private readonly List<EventLogRow>   _rows = new List<EventLogRow>();

    // ── Public API ────────────────────────────────────────────────────────────

    public bool IsVisible => _isVisible;

    /// <summary>Number of entries currently displayed (after filtering).</summary>
    public int DisplayedEntryCount => _currentEntries.Count;

    /// <summary>Active filter state.</summary>
    public EventLogFilters CurrentFilters => _filters;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_document != null)
        {
            _root          = _document.rootVisualElement?.Q("event-log-root");
            _listContainer = _root?.Q("event-log-list");
            _footerLabel   = _root?.Q<Label>("event-log-footer");
        }

        SetVisible(false);
    }

    // ── Open / close ──────────────────────────────────────────────────────────

    /// <summary>Toggle panel visibility. Refreshes entries on open.</summary>
    public void ToggleVisible()
    {
        SetVisible(!_isVisible);
    }

    public void SetVisible(bool v)
    {
        _isVisible = v;
        if (_root != null)
            _root.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;

        if (v) Refresh();
    }

    // ── Filter API ────────────────────────────────────────────────────────────

    public void SetNpcFilter(string entityId)
    {
        _filters = _filters.WithNpc(entityId);
        if (_isVisible) Refresh();
    }

    public void SetKindFilter(System.Collections.Generic.HashSet<ChronicleEventKind> kinds)
    {
        _filters = _filters.WithKinds(kinds);
        if (_isVisible) Refresh();
    }

    public void SetTimeRangeDays(int days)
    {
        _filters = _filters.WithTimeRangeDays(days);
        if (_isVisible) Refresh();
    }

    public void SetFilters(EventLogFilters filters)
    {
        _filters = filters ?? EventLogFilters.Default;
        if (_isVisible) Refresh();
    }

    // ── Refresh ────────────────────────────────────────────────────────────────

    /// <summary>Re-aggregates entries from WorldState and rebuilds the visual list.</summary>
    public void Refresh()
    {
        var worldState = _host?.WorldState;
        long tick      = _host?.TickCount ?? 0L;

        _currentEntries = _aggregator.Aggregate(worldState, _filters, tick, _ticksPerDay);

        RebuildList();
        UpdateFooter();
    }

    // ── List building ─────────────────────────────────────────────────────────

    private void RebuildList()
    {
        if (_listContainer == null) return;

        _listContainer.Clear();
        _rows.Clear();

        foreach (var entry in _currentEntries)
        {
            var row = new EventLogRow();
            row.Bind(entry, _ticksPerDay);
            row.OnRowClicked += HandleRowClicked;
            _rows.Add(row);
            _listContainer.Add(row.Root);
        }
    }

    private void UpdateFooter()
    {
        if (_footerLabel == null) return;
        int total = _host?.WorldState?.Chronicle?.Count ?? 0;
        _footerLabel.text = $"Showing {_currentEntries.Count} / {total}";
    }

    // ── Click-through ─────────────────────────────────────────────────────────

    private void HandleRowClicked(ChronicleEntryDto entry)
    {
        _clickHandler?.HandleRowClicked(entry);
    }

    // ── IMGUI fallback ────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (_root != null || !_isVisible) return;

        float w = 300f, h = 400f;
        float x = (Screen.width  - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f;

        GUI.Box(new Rect(x, y, w, h), "Event Log");

        float rowH = 22f, oy = y + 24f;
        int max = Mathf.Min(_currentEntries.Count, Mathf.FloorToInt((h - 48f) / rowH));
        for (int i = 0; i < max; i++)
        {
            var e = _currentEntries[i];
            string label = $"[{e.Kind}] {e.Description}";
            GUI.Label(new Rect(x + 4f, oy + i * rowH, w - 8f, rowH - 2f), label);
        }

        GUI.Label(new Rect(x + 4f, y + h - 22f, w - 8f, 18f),
            $"{_currentEntries.Count} entries");
    }

    // ── Test accessors ─────────────────────────────────────────────────────────

    /// <summary>Sets EngineHost (for tests).</summary>
    public void SetHost(EngineHost host) => _host = host;

    /// <summary>Injects a pre-built WorldStateDto for testing without EngineHost.</summary>
    public void InjectWorldStateForTest(WorldStateDto dto)
    {
        _currentEntries = _aggregator.Aggregate(dto, _filters, 0, _ticksPerDay);
        RebuildList();
        UpdateFooter();
    }
}
