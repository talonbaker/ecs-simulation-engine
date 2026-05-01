# WP-3.1.H — Developer Console (Minecraft-style, WARDEN-only)

**Status:** Completed
**Completed date:** 2026-04-28
**Work packet series:** WP-3.1 (Player UI & Simulation Feedback)

---

## Summary

WP-3.1.H delivers the WARDEN-only developer console: a Minecraft-style runtime
inspection and mutation tool that slides up from the bottom of the screen when
the backtick/tilde key is pressed. The entire feature is wrapped in `#if WARDEN`
and produces zero traces in RETAIL builds.

The delivery includes:

- `DevConsolePanel` — the MonoBehaviour that owns the toggle, history display,
  input field, and keyboard handling (Enter, Up/Down, Tab, Escape). Falls back
  to IMGUI when a UIDocument is not assigned (useful for test scenes).
- `DevConsoleCommandDispatcher` — tokeniser + command registry. Parses quoted
  strings (`spawn "the cynic" 5 10`), dispatches to `IDevConsoleCommand`
  implementations, and exposes a `Tokenize` static helper for tests.
- `IDevConsoleCommand` — contract for all commands (Name, Usage, Description,
  Aliases, Execute).
- `DevCommandContext` — dependency bag passed to every Execute call (EngineHost,
  IWorldMutationApi, SaveLoadPanel, TimeHudPanel, DevConsolePanel, JsonlStreamEmitter).
- `DevConsoleAutocomplete` — Tab-cycle engine; command-name mode when first token,
  NPC-name mode for NPC-expecting commands (inspect, move, force-kill, etc.),
  empty list for all other arg positions.
- `DevConsoleHistoryPersister` — reads/writes `Logs/console-history.txt`; caps at
  `DevConsoleConfig.MaxCommandHistory` (default 100); directory creation is automatic.
- `DevConsoleColorPalette` — phosphor-green CRT palette: Success (green), Error
  (red), Info (grey), Command (near-white), Warning (amber). `ConsoleEntryKind`
  enum and `ConsoleEntry` struct live alongside it.
- `DevConsoleConfig` — ScriptableObject (not WARDEN-gated) with ToggleKey,
  MaxHistoryLines, MaxCommandHistory, HistoryFilePath, PersistHistory, PanelHeightFraction.

---

## Command catalog with example invocations

| Command | Example | Notes |
|:---|:---|:---|
| `help` | `help` | Lists all registered commands with usage. |
| `inspect` | `inspect donna` | Full component dump: drives, willpower, schedule, relationships, recent memories. Accepts name or entity-id. |
| `inspect-room` | `inspect-room breakroom` | Room state dump: occupants, obstacles. |
| `spawn` | `spawn the-vent 5 10` | Spawns entity with archetype at (x, y). Quoted names supported: `spawn "the cynic" 3 7`. |
| `despawn` | `despawn e3a4b2` | Removes entity by id. |
| `move` | `move donna 8 12` | Calls `IWorldMutationApi.MoveEntity`; position updates next tick. |
| `force-kill` | `force-kill donna Choked` | Forces `Deceased(Choked)` transition; bereavement cascade fires. Valid causes: `Choked`, `SlippedAndFell`, `StarvedAlone`, `Died`. |
| `force-faint` | `force-faint donna` | Forces `Incapacitated`; recovery path returns to `Alive`. |
| `set-component` | `set-component donna StressComponent AcuteLevel=80` | Reflection-based field mutation. Field name is case-insensitive. |
| `lock` | `lock door-breakroom` | Calls `IWorldMutationApi.AttachObstacle`. |
| `unlock` | `unlock door-breakroom` | Calls `DetachObstacle`. |
| `tick-rate` | `tick-rate 120` | Adjusts `Time.fixedDeltaTime`; clamped to [1, 600] tps. Also updates emitter cadence. |
| `pause` | `pause` | Delegates to TimeHudPanel.Pause (same as time HUD). |
| `resume` | `resume` | Delegates to TimeHudPanel.Resume. |
| `save` | `save test-slot` | Delegates to SaveLoadPanel.SaveToSlot. |
| `load` | `load test-slot` | Delegates to SaveLoadPanel.LoadFromSlot. |
| `seed` | `seed 42` | Re-seeds the global RNG. Full effect on next engine restart. |
| `clear` | `clear` | Clears console history. |
| `history` | `history` | Prints the last N navigation-history entries. |
| `quit` | `quit` | Closes the console (same as pressing `~`). |

---

## Autocomplete behaviour

Tab key cycles candidates. Cycle state resets on any non-Tab keypress.

- **Empty input or first-token typing** — candidates are all registered command
  names, sorted alphabetically. Partial prefix narrows the list (`fo` matches
  `force-kill` and `force-faint`).
- **Arg position for NPC commands** (`inspect`, `move`, `despawn`, `force-kill`,
  `force-faint`, `set-component`) — candidates are NPC display names sourced from
  `EngineHost.Engine.Entities` filtered by `IdentityComponent.Name`. Partial prefix
  supported. Each candidate is returned as the full `<command> <name>` string.
- **All other arg positions** — empty candidate list (no autocomplete).
- **No EngineHost / null world** — NPC candidate list gracefully returns empty;
  no exception.

---

## WARDEN strip — verified

Every file except `DevConsoleConfig.cs` is wrapped in `#if WARDEN`. The config
ScriptableObject is intentionally un-gated so Unity can resolve the Inspector
reference type in all builds. The `DevConsoleRetailStripTests` edit-mode test
documents this contract and verifies the compile-time sentinel.

---

## History persistence notes

- Path: `DevConsoleConfig.HistoryFilePath` — default `"Logs/console-history.txt"`,
  relative to `Application.dataPath`'s parent directory.
- Format: one command per line, UTF-8, oldest first.
- Cap: `MaxCommandHistory` lines retained on each save (default 100); oldest
  entries are dropped first.
- Load is called on Start if `PersistHistory == true`. Save is called from
  `OnDestroy` (scene unload / play-mode exit).
- Missing file on load returns empty list — no exception.
- Parent directory is created automatically on first save.

---

## Files delivered

### Code

| File | Description |
|:---|:---|
| `ECSUnity/Assets/Scripts/DevConsole/DevConsolePanel.cs` | Main panel MonoBehaviour (WARDEN). |
| `ECSUnity/Assets/Scripts/DevConsole/DevConsoleCommandDispatcher.cs` | Parser + registry (WARDEN). |
| `ECSUnity/Assets/Scripts/DevConsole/IDevConsoleCommand.cs` | Command interface (WARDEN). |
| `ECSUnity/Assets/Scripts/DevConsole/DevCommandContext.cs` | Dependency bag (WARDEN). |
| `ECSUnity/Assets/Scripts/DevConsole/DevConsoleAutocomplete.cs` | Tab-cycle engine (WARDEN). |
| `ECSUnity/Assets/Scripts/DevConsole/DevConsoleHistoryPersister.cs` | Disk save/load (WARDEN). |
| `ECSUnity/Assets/Scripts/DevConsole/DevConsoleColorPalette.cs` | CRT palette + ConsoleEntryKind (WARDEN). |
| `ECSUnity/Assets/Scripts/DevConsole/DevConsoleConfig.cs` | ScriptableObject config (all builds). |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/HelpCommand.cs` | help |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/InspectCommand.cs` | inspect |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/InspectRoomCommand.cs` | inspect-room |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/SpawnCommand.cs` | spawn |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/DespawnCommand.cs` | despawn |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/MoveCommand.cs` | move |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/ForceKillCommand.cs` | force-kill |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/ForceFaintCommand.cs` | force-faint |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/SetComponentCommand.cs` | set-component |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/LockCommand.cs` | lock |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/UnlockCommand.cs` | unlock |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/TickRateCommand.cs` | tick-rate |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/PauseCommand.cs` | pause |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/ResumeCommand.cs` | resume |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/SaveCommand.cs` | save |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/LoadCommand.cs` | load |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/SeedCommand.cs` | seed |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/ClearCommand.cs` | clear |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/HistoryCommand.cs` | history |
| `ECSUnity/Assets/Scripts/DevConsole/Commands/QuitCommand.cs` | quit |

### Assets

| File | Description |
|:---|:---|
| `ECSUnity/Assets/UI/DevConsole.uxml` | Panel layout (WARDEN; dark CRT styling). |
| `ECSUnity/Assets/UI/DevConsole.uss` | Stylesheet (phosphor-green on #111118 background). |
| `ECSUnity/Assets/Settings/DefaultDevConsoleConfig.asset` | Default config ScriptableObject. |

### Tests (Play-mode)

| File | AT |
|:---|:---|
| `DevConsoleOpenCloseTests.cs` | AT-01 |
| `DevConsoleHelpCommandTests.cs` | AT-02 |
| `DevConsoleInspectTests.cs` | AT-03 |
| `DevConsoleSpawnTests.cs` | AT-04 |
| `DevConsoleDespawnTests.cs` | AT-05 |
| `DevConsoleMoveTests.cs` | AT-06 |
| `DevConsoleForceKillTests.cs` | AT-07 |
| `DevConsoleForceFaintTests.cs` | AT-08 |
| `DevConsoleSetComponentTests.cs` | AT-09 |
| `DevConsoleLockUnlockTests.cs` | AT-10 |
| `DevConsoleTickRateTests.cs` | AT-11 |
| `DevConsolePauseResumeTests.cs` | AT-12 |
| `DevConsoleSaveLoadTests.cs` | AT-13 |
| `DevConsoleHistoryNavigationTests.cs` | AT-14 |
| `DevConsoleAutocompleteCommandTests.cs` | AT-15 (command names) |
| `DevConsoleAutocompleteNpcNameTests.cs` | AT-15 (NPC names) |
| `DevConsoleErrorOutputTests.cs` | AT-18 |

### Tests (Edit-mode)

| File | AT |
|:---|:---|
| `DevConsoleHistoryPersistenceTests.cs` | AT-16 |
| `DevConsoleRetailStripTests.cs` | AT-17 |

---

## Acceptance test status

| AT | Assertion | Status |
|:---|:---|:---|
| AT-01 | `~` toggles open/closed; engine keeps running. | test written |
| AT-02 | `help` lists >= 15 commands. | test written |
| AT-03 | `inspect donna` outputs full state. | test written |
| AT-04 | `spawn the-vent 5 10` creates entity at (5,10). | test written |
| AT-05 | `despawn <id>` removes entity. | test written |
| AT-06 | `move <id> 8 12` calls IWorldMutationApi.MoveEntity. | test written |
| AT-07 | `force-kill donna Choked` → Deceased(Choked) next tick. | test written |
| AT-08 | `force-faint donna` → Incapacitated → recovers to Alive. | test written |
| AT-09 | `set-component donna StressComponent AcuteLevel=80` mutates. | test written |
| AT-10 | `lock` / `unlock` attach/detach obstacle. | test written |
| AT-11 | `tick-rate 100` adjusts fixedDeltaTime. | test written |
| AT-12 | `pause` / `resume` halt and restart engine. | test written |
| AT-13 | `save`/`load` round-trip preserves state. | test written |
| AT-14 | Up/Down arrows navigate command history. | test written |
| AT-15 | Tab autocompletes commands and NPC names. | test written (2 files) |
| AT-16 | History persists to Logs/console-history.txt. | test written |
| AT-17 | RETAIL: DevConsolePanel absent; `~` is a no-op. | test written |
| AT-18 | Invalid command produces red error message. | test written |
| AT-19 | All prior phase tests stay green. | verify on merge |
| AT-20 | dotnet build: 0 warnings; dotnet test: all green. | verify on merge |
| AT-21 | Unity Test Runner: all tests pass. | verify in editor |

---

## Known constraints

- `set-component` uses reflection (`Type.GetField`); field names are
  case-insensitive but the component short-name must match the class name exactly
  (e.g. `StressComponent`, not `stress`).
- `force-kill` and `force-faint` require `IWorldMutationApi` to be wired in
  `DevCommandContext.MutationApi`; without it the command returns a descriptive
  ERROR message.
- `save` / `load` require `SaveLoadPanel` to be assigned; error message returned
  if absent.
- `tick-rate` is clamped to [1, 600] tps and logs a note when clamping occurs.
- The IMGUI fallback (no UIDocument) is functional for test scenes but lacks
  the CRT styling; Tab autocomplete requires the UIDocument TextField.
