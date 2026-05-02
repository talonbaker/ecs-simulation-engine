# WP-PT.1 — WARDEN Dev-Console Scenario Verbs

> **DO NOT DISPATCH UNTIL WP-PT.0 IS MERGED.**
> Reason: scenario verbs are only meaningfully testable inside PlaytestScene, which WP-PT.0 ships.

**Track:** 2 (Unity — script-only; no scene mods, no prefab mods)
**Phase:** Playtest Program (parallel; see `docs/PLAYTEST-PROGRAM-KICKOFF-BRIEF.md`)
**Author:** Opus, 2026-05-01
**Sonnet executor:** assigned by Talon
**Branch:** `sonnet-wp-pt.1`
**Worktree:** `.claude/worktrees/sonnet-wp-pt.1/`
**Timebox:** 60–90 minutes
**Cost envelope:** $0.30–$0.50
**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** scenario-verb effect timing (does `scenario choke` produce a *visible* choke within 1 second? does `scenario sound Cough` route to the host synth correctly? does `scenario set-time dusk` jump the lighting tint smoothly?). PT-001 covers these alongside PlaytestScene's general feel acceptance.

---

## Goal

Add a `scenario` command surface to the existing WARDEN-only dev console (`#if WARDEN`-gated, strips at ship per SRD §8.7). The verb set lets Talon trigger any death / rescue / chore / sound / build / time event on demand inside PlaytestScene, eliminating the wait for organic occurrence during sessions.

This is a **single dispatchable command with sub-verbs** — `scenario <subverb> <args>` — not 11 separate top-level commands. Reasons: cleaner autocomplete (one entry rather than eleven), namespace-isolated, easy to extend.

---

## Non-goals

- Do not modify `MainScene.unity`, `PlaytestScene.unity`, or any prefab. Pure script + dispatcher-registration.
- Do not touch engine-side substrate (`APIFramework/`). All scenarios drive existing engine APIs (`IWorldMutationApi`, `LifeStateTransitionSystem`, `SoundTriggerBus`, etc.).
- Do not remove or rename existing dev-console commands (`force-kill`, `force-faint`, `lock`, `unlock`, etc.). Preserve them as **deprecated aliases** that route to the new scenario verbs internally — Talon's muscle memory and existing tests must keep working.
- Do not add a parser. The existing `DevConsoleCommandDispatcher` tokenizer is sufficient; sub-verb parsing happens inside the `ScenarioCommand.Execute` body.

---

## Verb specification

All verbs `#if WARDEN`-gated. Output uses the existing `INFO:` / `ERROR:` / silent-success conventions (see `DevConsoleCommandDispatcher.cs`).

### Lethal / incapacitating scenarios

| Verb | Effect | Notes |
|---|---|---|
| `scenario choke <npc-name\|--random> [--bolus-size <small\|medium\|large>]` | Attaches `ChokingComponent` with the specified bolus size; choke timer starts; rescue window opens. | Default bolus = `medium`. `--random` picks any alive non-Incapacitated NPC. |
| `scenario slip <npc-name>` | Spawns a stain on the NPC's tile (or next tile in their path); triggers `SlipAndFallSystem` evaluation. | If NPC is stationary, stain spawns under them. |
| `scenario faint <npc-name>` | Forces `LifeStateTransitionSystem` push to Incapacitated via faint cause. | Alias for existing `force-faint <npc>` — preserve original. |
| `scenario lockout <npc-name>` | Locks all doors of the room the NPC is currently in; starts lockout starvation timer. | If NPC is not in a room with doors, errors with `INFO: <name> is not in a lockable room`. |
| `scenario kill <npc-name> [cause]` | Pushes NPC to Deceased with the specified `CauseOfDeathKind` (default `Died`). Triggers bereavement cascade. | Alias for existing `force-kill` — preserve original. Valid causes: `Choked`, `SlippedAndFell`, `StarvedAlone`, `Died`. |

### Recovery scenarios

| Verb | Effect | Notes |
|---|---|---|
| `scenario rescue <victim-name> [--rescuer <name>]` | Triggers a `RescueIntent` from `<rescuer>` toward `<victim>` (auto-selects nearest alive bystander if `--rescuer` omitted). Kind inferred from victim state (Choked → Heimlich; locked-in → DoorUnlock; cardiac → CPR). | Errors if victim is not in a rescuable state. |

### Chores

| Verb | Effect | Notes |
|---|---|---|
| `scenario chore-microwave-to <npc-name>` | Forces this game-week's microwave-cleaning chore assignment to the named NPC, regardless of rotation order. | Tests refusal-bias deterministically (e.g., assign to Donna and observe). |

### Physics

| Verb | Effect | Notes |
|---|---|---|
| `scenario throw <prop-id> at <target>` | Applies `ThrownVelocityComponent` toward the target. Target = NPC name OR `x,y,z` tile coords OR `--here` (random nearby tile). | If prop is BreakableComponent and impact energy ≥ threshold, `ItemBroken` / `GlassShattered` fires per existing system. |

### Sound

| Verb | Effect | Notes |
|---|---|---|
| `scenario sound <SoundTriggerKind> [at <x,y,z>]` | Emits the named `SoundTriggerKind` directly to the bus. Default position = camera focus. | Valid kinds: `Cough`, `ChairSqueak`, `BulbBuzz`, `Footstep`, `SpeechFragment`, `Crash`, `Glass`, `Thud`, `Heimlich`, `DoorUnlock`. |

### Time

| Verb | Effect | Notes |
|---|---|---|
| `scenario set-time <time>` | Jumps sim wall-clock to the specified time. `<time>` accepts: `morning` (08:00), `midday` (12:00), `dusk` (18:00), `night` (22:00), or `HH:MM` (24-hour). | Re-evaluates lighting, schedule blocks, and build-mode-window state on next tick. |

### Seed loaders

| Verb | Effect | Notes |
|---|---|---|
| `scenario seed-stains <count>` | Spawns N slip-hazard stains at random walkable tiles. | For slip-rate testing without manual placement. |
| `scenario seed-bereavement <npc-name> <count>` | Pre-populates `<npc>`'s `BereavementHistoryComponent` with N synthetic mourned ids. | For testing long-arc grief mood without N actual deaths. |

### Discoverability

| Verb | Effect |
|---|---|
| `scenario` (no args) | Prints the full subverb list with one-line descriptions (auto-generated from the registry inside `ScenarioCommand`). |
| `scenario help <subverb>` | Prints detailed help for the named subverb (synopsis + arg shapes + examples). |

---

## Implementation pattern

Follow the existing dev-console command pattern. Read `ECSUnity/Assets/Scripts/DevConsole/Commands/ForceKillCommand.cs` and `MoveCommand.cs` for the canonical shape.

### File layout

```
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/
├── ScenarioCommand.cs                   (top-level IDevConsoleCommand; dispatches to subverbs)
├── ScenarioSubverbRegistry.cs           (maps subverb name → handler)
├── IScenarioSubverb.cs                  (interface for each subverb)
├── ChokeSubverb.cs
├── SlipSubverb.cs
├── FaintSubverb.cs
├── LockoutSubverb.cs
├── KillSubverb.cs
├── RescueSubverb.cs
├── ChoreMicrowaveToSubverb.cs
├── ThrowSubverb.cs
├── SoundSubverb.cs
├── SetTimeSubverb.cs
├── SeedStainsSubverb.cs
└── SeedBereavementSubverb.cs
```

`ScenarioCommand` registers in the same place existing commands register (find the registration site by grepping for `RegisterCommand(new ForceKillCommand(`).

### Aliases (preserve existing muscle memory)

| Existing command | New canonical scenario form | Behavior |
|---|---|---|
| `force-kill <name>` | `scenario kill <name>` | `force-kill` continues to work. Both routes through the same handler internally. |
| `force-faint <name>` | `scenario faint <name>` | `force-faint` continues to work. |

The existing `ForceKillCommand` and `ForceFaintCommand` files **must not be deleted**. Either keep them as thin shims that route to the new subverb handlers, or extract their logic into the subverb and have the existing command delegate. Talon's existing tests (`DevConsoleForceKillTests.cs`, `DevConsoleForceFaintTests.cs`) must continue to pass.

### Argument parsing — convention

Subverbs accept positional + flag args. Use a tiny helper class (e.g., `ScenarioArgParser`) inside the Scenario folder to handle:
- Required positional args (e.g., `<npc-name>`)
- Optional flags (e.g., `--bolus-size medium`, `--random`, `--rescuer <name>`)
- The `--random` and `--here` sentinels

Don't pull in a CLI library. The existing dev-console arg-handling pattern (manual index access into `string[] args`) is sufficient for ≤ 4 args per subverb.

### Output conventions

- Success: human-readable confirmation (`"Donna is choking on a medium bolus."`).
- Error: `"ERROR: ..."` prefix.
- Silent success: return null (rare; usually a confirmation is helpful).

---

## Acceptance criteria

### A — Verbs registered and discoverable

A1. After this packet ships, in PlaytestScene with the dev console open, typing `scenario` shows the full subverb list. Typing `help` lists `scenario` as one of the available top-level commands. Tab-autocomplete on `scenario ` (trailing space) shows the subverbs.

A2. `scenario help <subverb>` produces detailed help for every subverb in the table above.

### B — Each subverb produces the correct effect

For each subverb, the corresponding xUnit Play-mode test (in `ECSUnity/Assets/Tests/Play/`) confirms:
- The subverb routes through the dispatcher correctly.
- Invoking it produces the expected component / event / state change in the engine within ≤ 2 simulated seconds.
- Invalid args produce `ERROR:` output without throwing.

### C — Existing aliases continue to work

C1. `force-kill <name>` continues to function identically. `DevConsoleForceKillTests.cs` passes.
C2. `force-faint <name>` continues to function identically. `DevConsoleForceFaintTests.cs` passes.

### D — RETAIL strip verified

D1. Compile under non-WARDEN scripting defines. Confirm: no `Scenario*` files compile in (the `#if WARDEN` guard strips them); the dev console itself strips per existing `DevConsoleRetailStripTests.cs`.

### E — xUnit tests added

For every subverb, add a Play-mode test:
- `DevConsoleScenarioChokeTests.cs`
- `DevConsoleScenarioSlipTests.cs`
- `DevConsoleScenarioFaintTests.cs`
- `DevConsoleScenarioLockoutTests.cs`
- `DevConsoleScenarioKillTests.cs`
- `DevConsoleScenarioRescueTests.cs`
- `DevConsoleScenarioChoreMicrowaveToTests.cs`
- `DevConsoleScenarioThrowTests.cs`
- `DevConsoleScenarioSoundTests.cs`
- `DevConsoleScenarioSetTimeTests.cs`
- `DevConsoleScenarioSeedStainsTests.cs`
- `DevConsoleScenarioSeedBereavementTests.cs`

Each test: ~10 lines, asserts the correct engine state change after dispatching the verb.

Plus one Edit-mode test:
- `DevConsoleScenarioRegistrationTests.cs` — asserts all 12 subverbs are registered, asserts `scenario` (no args) returns help text, asserts `scenario unknown-subverb` returns `ERROR:`.

### F — `dotnet test` and `dotnet build` green

No regressions to any existing test, including the alias tests in §C.

---

## First-light recipe addition

This packet adds a section to `Assets/Scenes/PlaytestScene.md` (the recipe shipped by WP-PT.0). Append:

```markdown
## Scenario verbs (3 minutes — added by WP-PT.1)

1. Open dev console (`~`).
2. `scenario` — full subverb list appears.
3. `scenario kill <some-npc>` — they die. Bereavement cascade fires.
4. `scenario choke <some-other-npc>` — they cough; choke timer visible if you select them.
5. `scenario rescue <choking-npc>` — nearest bystander runs over, Heimlich animation plays, victim recovers.
6. `scenario set-time dusk` — sim clock jumps; lighting shifts; office quiets.
7. `scenario seed-stains 5` — 5 stains appear on random tiles.
8. `scenario sound BulbBuzz` — bulb-buzz plays at camera focus.

If any of these errors out or produces no visible effect, file as BUG-NNN.
```

---

## Files to author / modify

### New files

```
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/ScenarioCommand.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/ScenarioSubverbRegistry.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/IScenarioSubverb.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/ChokeSubverb.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/SlipSubverb.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/FaintSubverb.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/LockoutSubverb.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/KillSubverb.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/RescueSubverb.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/ChoreMicrowaveToSubverb.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/ThrowSubverb.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/SoundSubverb.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/SetTimeSubverb.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/SeedStainsSubverb.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/SeedBereavementSubverb.cs
ECSUnity/Assets/Scripts/DevConsole/Commands/Scenario/ScenarioArgParser.cs
ECSUnity/Assets/Tests/Edit/DevConsoleScenarioRegistrationTests.cs
ECSUnity/Assets/Tests/Play/DevConsoleScenarioChokeTests.cs
ECSUnity/Assets/Tests/Play/DevConsoleScenarioSlipTests.cs
ECSUnity/Assets/Tests/Play/DevConsoleScenarioFaintTests.cs
ECSUnity/Assets/Tests/Play/DevConsoleScenarioLockoutTests.cs
ECSUnity/Assets/Tests/Play/DevConsoleScenarioKillTests.cs
ECSUnity/Assets/Tests/Play/DevConsoleScenarioRescueTests.cs
ECSUnity/Assets/Tests/Play/DevConsoleScenarioChoreMicrowaveToTests.cs
ECSUnity/Assets/Tests/Play/DevConsoleScenarioThrowTests.cs
ECSUnity/Assets/Tests/Play/DevConsoleScenarioSoundTests.cs
ECSUnity/Assets/Tests/Play/DevConsoleScenarioSetTimeTests.cs
ECSUnity/Assets/Tests/Play/DevConsoleScenarioSeedStainsTests.cs
ECSUnity/Assets/Tests/Play/DevConsoleScenarioSeedBereavementTests.cs
```

### Modified files

```
ECSUnity/Assets/Scripts/DevConsole/Commands/ForceKillCommand.cs    (route through KillSubverb internally; preserve external behavior)
ECSUnity/Assets/Scripts/DevConsole/Commands/ForceFaintCommand.cs   (route through FaintSubverb internally; preserve external behavior)
ECSUnity/Assets/Scripts/DevConsole/DevConsolePanel.cs               (or wherever ScenarioCommand gets registered — find the registration site)
ECSUnity/Assets/Scenes/PlaytestScene.md                             (append the §"Scenario verbs" section)
```

---

## Dependencies

- **Hard:** WP-PT.0 must be merged. PlaytestScene must exist for the recipe additions to make sense and for in-scene testing.
- **Soft:** none.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

This is a Track 2 packet. xUnit covers the contract correctness; the visual layer (does the choke animation play? does the bereavement cascade fire visibly? does `scenario set-time dusk` produce the correct lighting tint?) requires Talon in PlaytestScene.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm you are in `.claude/worktrees/sonnet-wp-pt.1/` on branch `sonnet-wp-pt.1` based on recent `origin/staging` (which must include WP-PT.0). If WP-PT.0 is not yet merged, **stop and notify Talon** — this packet is gated.
1. Implement the spec.
2. Add xUnit tests per §E.
3. Run `dotnet test` and `dotnet build`. Both green. Existing alias tests in §C must remain green.
4. Stage all changes.
5. Commit on `sonnet-wp-pt.1`.
6. Push.
7. Stop. Final commit message line: `READY FOR VISUAL VERIFICATION — append to Assets/Scenes/PlaytestScene.md scenario-verbs section`.

Talon's pipeline:

1. Open Editor on `sonnet-wp-pt.1` branch with PlaytestScene.
2. Run the new "Scenario verbs" section of the first-light recipe.
3. If passes: open PR, merge.
4. If fails: file BUG-NNN per playtest README severity rubric.

### Feel-verified-by-playtest acceptance flag

**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** scenario-verb effect timing — does `scenario choke` produce a visible, animation-driven choke within 1 second? does `scenario sound Cough` actually play through the host synthesiser without dropping? does `scenario set-time dusk` shift lighting smoothly or flicker? does `scenario rescue` pick a sensible rescuer (not the NPC across the building)? PT-001 evaluates these.

### Cost envelope

Target: **$0.30–$0.50**. Timebox 60–90 minutes. The scope is large by file count but each subverb is a thin wrapper around an existing engine API — the per-file effort is low.

Cost-discipline:
- Reuse the existing argument-parsing pattern from `ForceKillCommand.cs`. Don't reinvent.
- Test files are mostly copy-paste from `DevConsoleForceKillTests.cs` with the verb swapped.
- Resist the temptation to refactor the dispatcher. The existing dispatcher is sufficient.

### Self-cleanup on merge

After Talon's visual verification passes:

1. Check downstream dependents:
   ```bash
   git grep -l "WP-PT.1" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. If grep returns no pending packets: include `git rm docs/c2-infrastructure/work-packets/WP-PT.1-dev-console-scenario-verbs.md` in the staging set. Add `Self-cleanup: spec file deleted, no pending dependents.` to the commit message.

3. If grep returns dependents (unlikely; this packet has no known downstream): leave the spec in place with a `> **STATUS:** SHIPPED` header.

4. After this packet merges, also check whether **WP-PT.0**'s spec can be cleaned up (if its only dependent was this packet — the grep at WP-PT.0 merge time retained it). If so, file a follow-up cleanup commit deleting `WP-PT.0-unified-playtest-scene.md`.

5. **Do not touch** files under `_completed/` or `_completed-specs/`.
