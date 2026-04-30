# WP-3.1.G — Player-facing Event Log (CDDA-style Chronicle Reader)

> **DO NOT DISPATCH UNTIL WP-3.1.A AND WP-3.1.E ARE MERGED.**
> The event log requires the Unity scaffold (3.1.A) and the player UI's selection / inspector / canvas substrate (3.1.E) to anchor click-throughs. Both must be on `main` before dispatch.

**Tier:** Sonnet
**Depends on:** WP-3.1.A (Unity scaffold), WP-3.1.E (player UI substrate, inspector for click-through), Phase 1 chronicle (`PersistenceThresholdDetector`, `Warden.Telemetry` events)
**Parallel-safe with:** WP-3.1.H (dev console — separate panel, separate input gate)
**Timebox:** 100 minutes
**Budget:** $0.45

---

## Goal

The player has been away from the simulation. They open the event log to see what happened. After this packet:

- A scrollable panel opens via a small notebook-icon button (CRT-styled).
- Lists persistent events in reverse chronological order: deaths, affairs, fights, betrayals, kindnesses-in-crises, fired/promoted, broken-window, the chair-mark-on-the-breakroom-wall, the relationship shifts that mattered.
- Filtering: by NPC, by event kind, by time range. Default view = "last seven game-days, all events."
- Click an entry → camera glides to the participants' last known position; inspector pins to the primary participant at that moment in time. (3.1.E inspector reused.)
- Always-accessible via the notebook icon; never modal.

The event log is the player's tool for catching up after they've been away — paused-but-AFK, or wanting to know what happened across the last week. It does **not** include UI to undo, edit, or restart events. It is read-only.

---

## Reference files

- `docs/c2-content/ux-ui-bible.md` §3.3 — event log surface commitment. CDDA-style scrollable. Filtering. Click-through to inspector. Read-only.
- `docs/c2-infrastructure/work-packets/WP-3.1.A-unity-scaffold-and-baseline-render.md` — Unity scaffold.
- `docs/c2-infrastructure/work-packets/WP-3.1.E-player-ui-inspector-time-controls-notifications.md` — inspector for click-through; canvas substrate.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.9.A.md` — persistent chronicle. `PersistenceThresholdDetector` decides which narrative events get chronicled. The event log reads chronicle entries.
- `docs/c2-infrastructure/work-packets/_completed/WP-2.3.A.md` and `_completed/WP-2.3.B.md` — memory recording. Persistent personal-memory and per-pair-memory entries also surface in the log.
- `APIFramework/Systems/Chronicle/ChronicleEntry.cs`, `ChronicleService.cs`, `ChronicleEventKind.cs` — chronicle structures.
- `APIFramework/Components/PersonalMemoryComponent.cs`, `RelationshipMemoryComponent.cs` — persistent memory entries.
- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` — full kind vocabulary including all the death kinds, MaskSlip, ChokeStarted, OverdueTask, TaskCompleted, BereavementImpact.
- `Warden.Telemetry/Projectors/*` — `WorldStateDto.events[]` (or `WorldStateDto.chronicle[]`) — what the renderer consumes.

---

## Non-goals

- Do **not** ship event editing / undo. Read-only.
- Do **not** ship event search by free-text. Filter by NPC + kind + time-range only at v0.1.
- Do **not** ship cross-save event log (events from other save files). Per-save-game only.
- Do **not** ship event-export as CSV / image. Future.
- Do **not** modify chronicle / memory engine code.
- Do **not** ship pinned events / favorites. Future polish.
- Do **not** retry, recurse, or "self-heal."

---

## Design notes

### `EventLogPanel`

UI Toolkit document. Opens via notebook-icon button on the persistent UI bar.

Layout:
- Header: filters (NPC dropdown, kind dropdown, time-range slider).
- Body: scrollable list of `EventLogRow` entries, newest at top.
- Footer: count of entries shown / total.

Each `EventLogRow`:
- Game-day badge (e.g., "Day 14, 2:47 PM").
- Event kind icon (CRT-style; per-kind icon — heart for affection, anger lines for fight, sweat-drop for stress event, skull-style for death, etc.).
- One-line summary text (e.g., "Donna and Frank fought at the microwave" — generated from event participants + kind).
- Click → `EventLogClickThrough`.

### Event aggregation

The log aggregates from three sources:

1. **Chronicle entries** (`ChronicleEntry`). Already persistent. The chronicle is the global event log; chronicle entries are office-wide.
2. **Persistent personal memory** (`PersonalMemoryComponent` entries with `Persistent = true`).
3. **Persistent relationship memory** (`RelationshipMemoryComponent` entries with `Persistent = true`).

A single underlying event may surface in multiple sources (a death event chronicles globally AND lands in the witness's personal memory AND lands in per-pair memory between deceased and witness). The aggregator de-duplicates by `eventId` (each chronicle / memory entry should reference the same source event id).

### Filters

- **By NPC:** dropdown of all NPCs (alive + deceased) in the world. Selecting filters to events where the NPC is a participant.
- **By kind:** dropdown of `NarrativeEventKind`. Multi-select supported.
- **By time range:** slider with presets ("Last hour", "Today", "Last 7 days", "Last 30 days", "All time"). Default "Last 7 days."

### Click-through

Click an event → `EventLogClickThroughHandler`:

1. Glide camera to the location where the event happened (read `event.LocationRoomId` if present, else use the primary participant's last-known position).
2. Pin inspector to the primary participant.
3. The inspector tier surfaces a "Show event context" link in the deep tier — clicking expands the narrative event entry.

If the participant is deceased and removed from world, the camera glides to their last-known room. If the room is gone (basement removed in build mode, hypothetically), default to a top-down zoom of the office.

### Time range computation

Time range is in game-days, not real-time. "Last 7 days" = events with `Tick >= currentTick - (7 * ticksPerDay)`. `ticksPerDay` from SimConfig.

### Performance

For a typical session of ~30 game-days, expect ~100-500 chronicle + memory events. List rendering uses a virtual-scroll pattern (only render visible rows + small buffer). At 30 NPCs running for 30 game-days, the log handles ~1500-3000 entries without UI lag.

### Tests

- `EventLogPanelOpenTests.cs` — click notebook icon → panel visible; click again → panel hidden.
- `EventLogEntriesPopulatedTests.cs` — boot scene; advance 7 game-days; assert ≥ 5 entries in panel (chronicle + memory).
- `EventLogReverseChronTests.cs` — entries listed newest-first.
- `EventLogFilterByNpcTests.cs` — select Donna → only Donna-participant events visible.
- `EventLogFilterByKindTests.cs` — select MaskSlip → only mask-slip events.
- `EventLogTimeRangeTests.cs` — slide range to "Last 1 day" → entries with tick ≥ currentTick - ticksPerDay.
- `EventLogDeduplicationTests.cs` — single death event surfaces ONCE in the log even though it appears in chronicle + personal-memory + per-pair-memory.
- `EventLogClickThroughTests.cs` — click an event row → camera glides to event location; inspector pins to primary participant.
- `EventLogClickThroughDeceasedTests.cs` — click event whose primary participant is deceased → camera glides to last-known room; inspector shows deceased's last state.
- `EventLogVirtualScrollPerformanceTests.cs` — populate with 1000 entries → rendered rows ≤ 30 (virtual-scroll bounded); scroll smoothly at 60 FPS.
- `EventLogPerformanceWithFullUiTests.cs` — 30 NPCs + event log open + 1000 events: FPS gate preserved.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSUnity/Assets/Scripts/UI/EventLog/EventLogPanel.cs` | Main panel. |
| code | `ECSUnity/Assets/Scripts/UI/EventLog/EventLogRow.cs` | Per-entry row UI. |
| code | `ECSUnity/Assets/Scripts/UI/EventLog/EventLogAggregator.cs` | Pulls from chronicle + memory; dedupes. |
| code | `ECSUnity/Assets/Scripts/UI/EventLog/EventLogFilters.cs` | Filter state. |
| code | `ECSUnity/Assets/Scripts/UI/EventLog/EventLogClickThroughHandler.cs` | Camera glide + inspector pin. |
| code | `ECSUnity/Assets/Scripts/UI/EventLog/EventKindIconCatalog.cs` | Kind → icon mapping. |
| asset | `ECSUnity/Assets/UI/EventLog.uxml` + `.uss` | Layout + styling. |
| asset | `ECSUnity/Assets/Sprites/EventKindIcons/*.png` | CRT-style per-kind icons. |
| asset | `ECSUnity/Assets/Settings/DefaultEventKindIconCatalog.asset` | Mapping. |
| test | `ECSUnity/Assets/Tests/Play/EventLogPanelOpenTests.cs` | Open/close. |
| test | `ECSUnity/Assets/Tests/Play/EventLogEntriesPopulatedTests.cs` | Populated. |
| test | `ECSUnity/Assets/Tests/Play/EventLogReverseChronTests.cs` | Newest first. |
| test | `ECSUnity/Assets/Tests/Play/EventLogFilterByNpcTests.cs` | NPC filter. |
| test | `ECSUnity/Assets/Tests/Play/EventLogFilterByKindTests.cs` | Kind filter. |
| test | `ECSUnity/Assets/Tests/Play/EventLogTimeRangeTests.cs` | Time range. |
| test | `ECSUnity/Assets/Tests/Play/EventLogDeduplicationTests.cs` | Dedup. |
| test | `ECSUnity/Assets/Tests/Play/EventLogClickThroughTests.cs` | Click-through. |
| test | `ECSUnity/Assets/Tests/Play/EventLogClickThroughDeceasedTests.cs` | Deceased click-through. |
| test | `ECSUnity/Assets/Tests/Play/EventLogVirtualScrollPerformanceTests.cs` | Virtual scroll. |
| test | `ECSUnity/Assets/Tests/Play/EventLogPerformanceWithFullUiTests.cs` | **FPS preserved.** |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.1.G.md` | Completion note. SimConfig defaults. Per-kind icon decisions. Performance measurements with 1000 entries. Whether dedup actually triggered during testing. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | Click notebook icon → panel visible. Click again → hidden. | play-mode test |
| AT-02 | After 7 game-days, panel has ≥ 5 entries (assumes typical scenario activity). | play-mode test |
| AT-03 | Entries are listed newest first. | play-mode test |
| AT-04 | Filter by Donna → only events where Donna is a participant. | play-mode test |
| AT-05 | Filter by MaskSlip → only mask-slip events. | play-mode test |
| AT-06 | Time range "Last 1 day" → entries within last `ticksPerDay` ticks. | play-mode test |
| AT-07 | A death event surfaces ONCE in the log, not three times (dedup). | play-mode test |
| AT-08 | Click event row → camera glides to participant location; inspector pins to participant. | play-mode test |
| AT-09 | Click event row for deceased participant → camera glides to last-known room; inspector shows deceased state. | play-mode test |
| AT-10 | Virtual scroll: 1000 entries, only ≤ 30 rendered at any time; smooth scroll at 60 FPS. | play-mode test |
| AT-11 | **Performance gate.** 30 NPCs + event log open + 1000 events: min ≥ 55, mean ≥ 58, p99 ≥ 50. | play-mode test |
| AT-12 | All Phase 0/1/2/3.0.x and 3.1.A and 3.1.E tests stay green. | regression |
| AT-13 | `dotnet build` warning count = 0; `dotnet test` all green. | build + test |
| AT-14 | Unity Test Runner: all tests pass. | unity test runner |

---

## Followups (not in scope)

- **Free-text search.** Future polish; needs full-text indexing.
- **Cross-save event log.** Future; might never ship.
- **Event-export.** CSV / image of the log. Future.
- **Pinned events / favorites.** Future.
- **Event "details" expansion** — clicking an entry expands inline rather than opening inspector. Alternative UX. Future toggle.
- **Per-NPC event log view.** From inspector deep-tier → "show all events for this NPC". Trivial follow-up.
- **Event narration generation.** Sim writes a story from the log: "On Day 14, Donna fought with Frank by the microwave. The next day, she avoided his cubicle..." Couples to design-time LLM (Sonnet authoring narrative templates). Future content packet.
- **Anniversary surfacing.** "On this day last week, Mark died." Soft cue. Future.
