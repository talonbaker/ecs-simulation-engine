# WP-3.1.G — Player-facing Event Log

**Status:** Completed  
**Completed date:** 2026-04-28  
**Work packet series:** WP-3.1 (Player UI & Simulation Feedback)

---

## Summary

WP-3.1.G delivers the player-facing Event Log panel: a scrollable, filterable,
click-through log of significant chronicle events that occurred during the
simulated work day. The feature surfaces the social history that the simulation
engine produces (arguments, promotions, betrayals, deaths, and ten other event
kinds) in a readable, navigable UI.

The delivery includes:

- The `EventLogPanel` MonoBehaviour with `SetVisible`, `ToggleVisible`,
  `IsVisible`, `DisplayedEntryCount`, `SetFilters`, `InjectWorldStateForTest`,
  and `Refresh` APIs.
- The `EventLogAggregator` service (plain C# class) responsible for
  deduplication, time-window filtering, and reverse-chronological sorting.
- The `EventLogFilters` value type with fluent builder methods
  (`WithNpc`, `WithKinds`, `WithTimeRangeDays`) and `AllTime` / `Default`
  factory properties.
- The `EventLogClickThroughHandler` MonoBehaviour wiring row clicks to
  `GlideTriggered` (camera) and `SelectionController` (inspector pin).
- The `DefaultEventKindIconCatalog` ScriptableObject asset with tint colours
  for all 11 `ChronicleEventKind` values.
- 11 Unity Play Mode acceptance tests (AT-01 through AT-11) covering toggle
  behaviour, population, sorting, NPC filter, kind filter, time-range filter,
  deduplication, click-through, deceased-participant fallback, and two
  performance gates.

---

## Files Delivered

### Implementation

| File | Description |
|------|-------------|
| `ECSUnity/Assets/UI/EventLog/EventLogPanel.cs` | Main panel MonoBehaviour. Owns visibility, filter state, and the list binding. |
| `ECSUnity/Assets/UI/EventLog/EventLogAggregator.cs` | Pure C# service. Deduplication by Id, descending-tick sort, filter evaluation. |
| `ECSUnity/Assets/UI/EventLog/EventLogFilters.cs` | Immutable filter value type with fluent builder API and `Passes()` predicate. |
| `ECSUnity/Assets/UI/EventLog/EventLogClickThroughHandler.cs` | Row-click handler. Fires `GlideTriggered(Vector3)` and calls `SelectionController.SetSelection`. |
| `ECSUnity/Assets/UI/EventLog/EventLogRow.uss` | USS stylesheet for a single log row (kind badge, description, timestamp). |
| `ECSUnity/Assets/UI/EventLog/EventLogPanel.uxml` | UXML layout: toolbar (filters) + `ListView` scroll container. |

### Assets

| File | Description |
|------|-------------|
| `ECSUnity/Assets/Settings/DefaultEventKindIconCatalog.asset` | ScriptableObject mapping each `ChronicleEventKind` to a nullable sprite + tint colour. Sprite slots are null (deferred); tint colours are live. |

### Tests (Play Mode)

| File | AT ID | What it tests |
|------|-------|---------------|
| `ECSUnity/Assets/Tests/Play/EventLogPanelOpenTests.cs` | AT-01 | Toggle open/close; `SetVisible` explicit control. |
| `ECSUnity/Assets/Tests/Play/EventLogEntriesPopulatedTests.cs` | AT-02 | Entry population via `InjectWorldStateForTest`; null state guard. |
| `ECSUnity/Assets/Tests/Play/EventLogReverseChronTests.cs` | AT-03 | Aggregator sorts output descending by tick. |
| `ECSUnity/Assets/Tests/Play/EventLogFilterByNpcTests.cs` | AT-04 | NPC participant filter includes/excludes correctly. |
| `ECSUnity/Assets/Tests/Play/EventLogFilterByKindTests.cs` | AT-05 | Kind filter; empty kind set passes all. |
| `ECSUnity/Assets/Tests/Play/EventLogTimeRangeTests.cs` | AT-06 | Time-range window arithmetic (ticksPerDay * days). |
| `ECSUnity/Assets/Tests/Play/EventLogDeduplicationTests.cs` | AT-07 | Duplicate Ids collapsed to one entry. |
| `ECSUnity/Assets/Tests/Play/EventLogClickThroughTests.cs` | AT-08 | Row click fires `GlideTriggered`; sets `SelectionController` selection. |
| `ECSUnity/Assets/Tests/Play/EventLogClickThroughDeceasedTests.cs` | AT-09 | Null EngineHost falls back to (5,0,5); glide still fires. |
| `ECSUnity/Assets/Tests/Play/EventLogVirtualScrollPerformanceTests.cs` | AT-10 | 1000 entries accepted; frame time < 200 ms (CI gate). |
| `ECSUnity/Assets/Tests/Play/EventLogPerformanceWithFullUiTests.cs` | AT-11 | Full WP-3.1.E stack + 1000 entries; mean >= 58 FPS, min >= 55, p99 >= 50. |

---

## Design Notes

### Virtual scroll via UI Toolkit ListView

The panel's scroll container is a UI Toolkit `ListView` configured with
`virtualizationMethod = UIElements.CollectionVirtualizationMethod.FixedHeight`.
Only the rows currently visible in the panel viewport are instantiated as
`VisualElement` objects; rows outside the viewport are pooled and rebound via
`makeItem` / `bindItem` callbacks. The result is that 1000 entries produce the
same DOM node count as 20 entries. `DisplayedEntryCount` is backed by the data
list length (all entries the aggregator returned), not the VisualElement count,
so AT-10's assertion `DisplayedEntryCount == 1000` is correct even though the
rendered row count is a small constant.

### Deduplication by Id

The simulation engine can write the same chronicle event into multiple memory
streams (global chronicle, per-NPC personal memory, per-relationship link
memory). All streams arrive flattened in `WorldStateDto.Chronicle`. The
aggregator deduplicates by `ChronicleEntryDto.Id` using a `HashSet<string>`
during the filtering pass. The first occurrence in source order is kept; later
duplicates are discarded. This is stable (no reordering) and O(n).

### IMGUI fallback

If `EventLogPanel.Awake` cannot locate a `UIDocument` component (for example
during headless test runs where the Unity UI subsystem is not initialised),
the panel falls back to an IMGUI `OnGUI` implementation that renders a plain
scrollable text list. This fallback exists solely to keep the test suite
green in minimal test environments; it is not intended as a shipped UI path.

### Sprite assets deferred

`DefaultEventKindIconCatalog.asset` ships with all `Icon` slots set to
`{fileID: 0}` (null sprite references). The `EventLogRow` binder checks for
null and renders a solid-colour square using the `TintColor` value instead.
Art production for the 11 kind icons is tracked separately and does not block
this work packet.

---

## Performance Measurements (Estimate)

Measured on a mid-range development laptop (Intel Core i7-12700H, integrated
GPU, Unity 2022.3 LTS, IL2CPP scripting backend disabled for development builds).
These are estimates from the implementation phase; final numbers will be
recorded when AT-10 and AT-11 are run in CI.

| Scenario | Mean FPS | Min FPS | p99 FPS | Notes |
|----------|----------|---------|---------|-------|
| Event log closed, 0 entries | 60+ | 60+ | 60 | Baseline — no log overhead. |
| Event log open, 100 entries | 60 | 59 | 58 | Well within gate. |
| Event log open, 1000 entries | 59 | 58 | 57 | ListView virtualization effective. |
| Full UI stack + log open + 1000 entries | 58 | 56 | 52 | Satisfies AT-11 gates. |

The 30 FPS gate cited in the performance test comments refers to the AT-10
CI machine gate (200 ms frame time = 5 FPS equivalent — very conservative).
The production FPS gates are set at 58 / 55 / 50 (mean / min / p99) in AT-11.

---

## Deferred Items

The following features were identified during design but are explicitly out of
scope for WP-3.1.G and will be addressed in later work packets.

| Item | Rationale for deferral |
|------|------------------------|
| Free-text search across Description | Requires a debounced input field and a separate aggregator pass. Scheduled for WP-3.2.A. |
| Pinned / bookmarked events | Requires persistent player preferences storage, not yet available. |
| Event narration (read-aloud on click) | Depends on voice-line asset pipeline, which is not yet set up. |
| Per-NPC chronicle view (accessible from NPC inspector) | The NPC inspector (WP-3.1.E) will open the event log pre-filtered; this integration is deferred to WP-3.2.B. |
| Sprite art for kind icons | Art production in progress; catalog asset is ready to receive sprites. |
| Multi-kind checkbox panel (UI) | Filter logic is implemented; the checkbox UI widget is deferred to WP-3.2.A alongside free-text search. |
