# WP-3.1.H — Developer Console (Minecraft-style, WARDEN-only)

> **DO NOT DISPATCH UNTIL WP-3.1.A IS MERGED.**
> The dev console requires the Unity scaffold and engine-host bridge from 3.1.A. Recommended (not blocking): 3.1.E (player UI substrate). Without 3.1.E, the console functions but lacks integration with the inspector for command output formatting.
>
> **WARDEN-build only.** All deliverables are gated behind `#if WARDEN`. RETAIL builds strip the entire console.

**Tier:** Sonnet
**Depends on:** WP-3.1.A (Unity scaffold)
**Recommended-not-blocking:** WP-3.1.E (UI substrate)
**Parallel-safe with:** WP-3.1.B, WP-3.1.C, WP-3.1.D, WP-3.1.F, WP-3.1.G
**Timebox:** 110 minutes
**Budget:** $0.50

---

## Goal

A Minecraft-style runtime console for inspecting and mutating the engine. Toggle key: `~` (tilde) or `\`` (backtick). Slides up from the bottom of the screen. Command-line input. Output history scrollable. Autocomplete. WARDEN-only — RETAIL builds have zero traces.

After this packet:

- Press `~` → console opens. World keeps running (or the player can pause from the time HUD).
- Type `inspect <npcId|name>` → prints full component dump for the NPC.
- Type `spawn <archetype> <x> <y>` → spawns a new NPC at the location.
- Type `force-kill <npcId> <cause>` → forces a death transition. Useful for testing 3.0.x death scenarios.
- Type `force-faint <npcId>` → forces fainting (Incapacitated → Alive recovery path). Useful for testing the fainting scenario.
- Type `set-component <npcId> <componentName> <field>=<value>` → mutates engine state directly. (Cheating, but it's a dev tool.)
- Type `tick-rate <ticks-per-second>` → changes engine tick rate. Couples to 3.1.F's cadence tuner.
- Type `save <slot-name>` / `load <slot-name>` → manual save/load.
- Type `help` → list all commands.
- Up/down arrows: history navigation.
- Tab: autocomplete.

This is the dev's Swiss Army knife — for testing scenarios, debugging weird behavior, reproducing player reports, and surfacing internal state. It exists for the developer and the WARDEN-build user only.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` §8.7 — engine host-agnostic; telemetry / dev tools build-conditional. The console is a WARDEN-only tool.
- `docs/c2-content/ux-ui-bible.md` §1.6 (CRT-era iconography vocabulary; the console fits this aesthetic naturally), §5.2 (creative mode unlocks may overlap; the console is *more* than creative mode — it's developer mode).
- `docs/c2-infrastructure/work-packets/WP-3.1.A-unity-scaffold-and-baseline-render.md` — `EngineHost`.
- `APIFramework/Core/EntityManager.cs` — query and mutation API.
- `APIFramework/Mutation/IWorldMutationApi.cs` (from WP-3.0.4) — public mutation surface.
- `APIFramework/Systems/LifeState/LifeStateTransitionSystem.cs` (from WP-3.0.0) — death/incap requests for `force-kill` / `force-faint` commands.

---

## Non-goals

- Do **not** ship in RETAIL. WARDEN-only.
- Do **not** ship a full scripting language. Commands are simple verb + arg parser.
- Do **not** ship a network REPL.
- Do **not** ship multiplayer / shared-console.
- Do **not** modify engine surface; the console *consumes* the engine API and `IWorldMutationApi`.
- Do **not** retry, recurse, or "self-heal."

---

## Design notes

### `DevConsolePanel`

UI Toolkit document, opens with `~` key. Layout:
- Top scrollable history (output of past commands, color-coded: green = success, red = error, gray = info).
- Bottom input field with prompt prefix `>`.

### Command dispatch

`DevConsoleCommandDispatcher` parses input:
- Tokenize on whitespace (respecting quoted strings).
- First token = command name; rest = args.
- Command registry maps name → `IDevConsoleCommand` implementation.
- Invoke; capture output (stdout + errors) into history.

### Command catalog

| Command | Args | Description |
|:---|:---|:---|
| `help` | — | List all commands. |
| `inspect` | `<npcId\|name>` | Full component dump. |
| `inspect-room` | `<roomId\|name>` | Room state. |
| `spawn` | `<archetype> <x> <y>` | Spawn NPC at location. |
| `despawn` | `<entityId>` | Despawn entity. |
| `move` | `<entityId> <x> <y>` | Move entity (calls `IWorldMutationApi.MoveEntity`). |
| `force-kill` | `<npcId> <cause>` | Force `Deceased` transition; cause is one of `Choked`, `SlippedAndFell`, `StarvedAlone`, `Died`. |
| `force-faint` | `<npcId>` | Force `Incapacitated` (non-fatal); enters fainting recovery path. |
| `set-component` | `<npcId> <component> <field>=<value>` | Direct mutation; e.g., `set-component donna StressComponent AcuteLevel=80`. |
| `lock` | `<doorId>` | Calls `IWorldMutationApi.AttachObstacle`. |
| `unlock` | `<doorId>` | `DetachObstacle`. |
| `tick-rate` | `<ticks-per-second>` | Adjust engine tick rate. |
| `pause` | — | Pause engine (same as time HUD). |
| `resume` | — | Resume. |
| `save` | `<slot-name>` | Manual save. |
| `load` | `<slot-name>` | Load slot. |
| `seed` | `<int>` | Re-seed RNG (engine restart required for full effect). |
| `clear` | — | Clear history. |
| `history` | — | Print history. |
| `quit` | — | Close console (also `~`). |

Argument parsing handles quoted strings (`spawn "the cynic" 5 10`), reflection-based component lookup (`set-component`), and friendly NPC-name resolution (`inspect donna` works as well as `inspect e3a4b...`).

### Autocomplete

Tab cycles through:
- Command names (when input is empty or first token).
- NPC names (when arg position expects NPC).
- Component names (when arg position expects a component).

Implementation: `DevConsoleAutocomplete` builds candidate lists at runtime from engine state.

### Output formatting

Inspect output is structured: name, archetype, position, room, life state, top-3 drives, willpower, schedule, top-3 relationships, recent persistent memory entries (3 most recent). Color-coded per kind (drives green, relationships blue, etc.).

### History persistence

Console history (last 100 commands) persists to `Logs/console-history.txt` so re-opening sessions doesn't lose context. WARDEN-only.

### Tests

- `DevConsoleOpenCloseTests.cs` — `~` toggles open/closed.
- `DevConsoleHelpCommandTests.cs` — `help` lists ≥ 15 commands.
- `DevConsoleInspectTests.cs` — `inspect donna` outputs full component dump including drives, willpower, schedule.
- `DevConsoleSpawnTests.cs` — `spawn the-vent 5 10` adds new entity at (5,10) with archetype `the-vent`.
- `DevConsoleDespawnTests.cs` — `despawn <entityId>` removes entity from engine.
- `DevConsoleMoveTests.cs` — `move <entityId> 8 12` calls `IWorldMutationApi.MoveEntity`; position updates.
- `DevConsoleForceKillTests.cs` — `force-kill donna Choked` causes Donna to enter `Deceased(Choked)` next tick.
- `DevConsoleForceFaintTests.cs` — `force-faint donna` causes Donna to enter `Incapacitated`; on recovery, returns to `Alive`.
- `DevConsoleSetComponentTests.cs` — `set-component donna StressComponent AcuteLevel=80` mutates the value.
- `DevConsoleLockUnlockTests.cs` — `lock <doorId>` attaches `LockedTag`; `unlock` detaches.
- `DevConsoleTickRateTests.cs` — `tick-rate 100` doubles engine tick rate (verify via clock advancement over 1 real-second).
- `DevConsolePauseResumeTests.cs` — `pause` halts engine; `resume` restarts.
- `DevConsoleSaveLoadTests.cs` — `save test-slot`, mutate world, `load test-slot` — state restored.
- `DevConsoleHistoryNavigationTests.cs` — up/down arrow navigates history.
- `DevConsoleAutocompleteCommandTests.cs` — tab on empty input cycles command names.
- `DevConsoleAutocompleteNpcNameTests.cs` — tab in arg position cycles NPC names.
- `DevConsoleHistoryPersistenceTests.cs` — close + reopen scene → console history reloads from `Logs/console-history.txt`.
- `DevConsoleRetailStripTests.cs` — RETAIL build (no `WARDEN`): `DevConsolePanel` not present; `~` does nothing.
- `DevConsoleErrorOutputTests.cs` — invalid command → red error message.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSUnity/Assets/Scripts/DevConsole/DevConsolePanel.cs` | UI panel (WARDEN-only). |
| code | `ECSUnity/Assets/Scripts/DevConsole/DevConsoleCommandDispatcher.cs` | Parser + dispatcher. |
| code | `ECSUnity/Assets/Scripts/DevConsole/IDevConsoleCommand.cs` | Command interface. |
| code | `ECSUnity/Assets/Scripts/DevConsole/Commands/*.cs` | One file per command (HelpCommand, InspectCommand, SpawnCommand, ForceKillCommand, ForceFaintCommand, SetComponentCommand, LockCommand, etc.). |
| code | `ECSUnity/Assets/Scripts/DevConsole/DevConsoleAutocomplete.cs` | Autocomplete engine. |
| code | `ECSUnity/Assets/Scripts/DevConsole/DevConsoleHistoryPersister.cs` | Save/load history.txt. |
| code | `ECSUnity/Assets/Scripts/DevConsole/DevConsoleColorPalette.cs` | CRT-styled colors. |
| asset | `ECSUnity/Assets/UI/DevConsole.uxml` + `.uss` | Layout + CRT-styling. |
| code | `ECSUnity/Assets/Scripts/DevConsole/DevConsoleConfig.cs` | Config. |
| asset | `ECSUnity/Assets/Settings/DefaultDevConsoleConfig.asset` | Defaults. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleOpenCloseTests.cs` | Open/close. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleHelpCommandTests.cs` | Help. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleInspectTests.cs` | Inspect. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleSpawnTests.cs` | Spawn. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleDespawnTests.cs` | Despawn. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleMoveTests.cs` | Move. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleForceKillTests.cs` | Force-kill. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleForceFaintTests.cs` | Force-faint. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleSetComponentTests.cs` | Set-component. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleLockUnlockTests.cs` | Lock/unlock. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleTickRateTests.cs` | Tick rate. |
| test | `ECSUnity/Assets/Tests/Play/DevConsolePauseResumeTests.cs` | Pause/resume. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleSaveLoadTests.cs` | Save/load. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleHistoryNavigationTests.cs` | History nav. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleAutocompleteCommandTests.cs` | Autocomplete. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleAutocompleteNpcNameTests.cs` | NPC autocomplete. |
| test | `ECSUnity/Assets/Tests/Edit/DevConsoleHistoryPersistenceTests.cs` | History persists. |
| test | `ECSUnity/Assets/Tests/Edit/DevConsoleRetailStripTests.cs` | RETAIL strip. |
| test | `ECSUnity/Assets/Tests/Play/DevConsoleErrorOutputTests.cs` | Error output. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.1.H.md` | Completion note. Full command catalog with one example invocation each. Autocomplete behavior notes. Verified WARDEN strip. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `~` toggles console open/closed; engine continues running while console is open (unless `pause` issued). | play-mode test |
| AT-02 | `help` lists ≥ 15 commands. | play-mode test |
| AT-03 | `inspect donna` outputs full Donna state (drives, willpower, schedule, etc.). | play-mode test |
| AT-04 | `spawn the-vent 5 10` creates entity at (5,10) with archetype the-vent. | play-mode test |
| AT-05 | `despawn <entityId>` removes entity. | play-mode test |
| AT-06 | `move <entityId> 8 12` calls `IWorldMutationApi.MoveEntity`. | play-mode test |
| AT-07 | `force-kill donna Choked` → Donna enters Deceased(Choked) next tick; bereavement cascade fires. | integration test |
| AT-08 | `force-faint donna` → Donna enters Incapacitated; recovers to Alive within budget. | integration test |
| AT-09 | `set-component donna StressComponent AcuteLevel=80` mutates value. | play-mode test |
| AT-10 | `lock <doorId>` attaches LockedTag; cache invalidates. `unlock` detaches. | integration test |
| AT-11 | `tick-rate 100` doubles engine tick rate. | play-mode test |
| AT-12 | `pause` / `resume` halt and restart engine. | play-mode test |
| AT-13 | `save test-slot` + `load test-slot` round-trip preserves state. | integration test |
| AT-14 | Up/down arrows navigate history. | play-mode test |
| AT-15 | Tab autocompletes commands and NPC names. | play-mode test |
| AT-16 | History persists to `Logs/console-history.txt`. | edit-mode test |
| AT-17 | RETAIL build (no WARDEN): DevConsolePanel absent; `~` no-op. | edit-mode test |
| AT-18 | Invalid command outputs red error message. | play-mode test |
| AT-19 | All Phase 0/1/2/3.0.x and 3.1.A tests stay green. | regression |
| AT-20 | `dotnet build` warning count = 0; `dotnet test` all green. | build + test |
| AT-21 | Unity Test Runner: all tests pass. | unity test runner |

---

## Followups (not in scope)

- **Scriptable command extensions.** Add new commands by dropping a file in `Commands/` — already supported by interface; future polish includes editor-time tooling.
- **Network REPL.** Connect from external IDE / agent. Future.
- **Macro recording.** Record a sequence of commands; play back. Future.
- **Console alias.** `alias <name> <command-sequence>`. Future.
- **Inspect into history.** Click a past command in history → re-run with same args. Future polish.
- **Color theme settings.** Currently CRT-green-on-black. Future polish toggle.
- **Inline output formatting.** JSON / table / markdown output modes. Future.
- **Cross-machine command sharing.** Share recipes; community library. Far future.
