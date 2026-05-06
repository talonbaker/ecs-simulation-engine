# Dev Console Quick Reference

> WARDEN-only. Press backtick (`` ` ``) in PlaytestScene to open. Type a command, press Enter. Up/down arrows browse history.
>
> **Heads-up:** while the console is open, keyboard shortcuts in the rest of the game (space-pause, F-recenter, B-build, F5/F9-save/load, 1/2/3-time-scale) are suppressed so you can type without bleed.

---

## How to discover targets

You usually want an NPC name to target. Two ways:

```
scenario list-npcs
```

Prints every NPC with name, life state, and short id. Use the **name** in subsequent commands — it's case-insensitive and case is forgiving.

You can also click an NPC in the scene to see their name in the inspector once BUG-004 is fully resolved.

---

## Top-level commands

| Command | What it does |
|---|---|
| `help` | List every registered command. `help <name>` for usage of one. |
| `history` | Print the last commands entered (also Up/Down arrow). |
| `clear` | Wipe the visible console output. |
| `quit` | Close the console (same as backtick). |

## Time control

| Command | What it does |
|---|---|
| `pause` | Set engine `Time.timeScale = 0`. |
| `resume` | Resume after pause. |
| `tick-rate <hz>` | Set engine tick rate. Adjusts `Time.fixedDeltaTime` + JSONL cadence. |

## Save / load

| Command | What it does |
|---|---|
| `save <slot-name>` | Write WorldStateDto JSON to a named slot. Multi-word slot names allowed (`save before experiment`). Path printed in response. |
| `load <slot-name>` | Restore from named slot. Engine restart required for full state restore (v0.1 limitation). |

Save files live at: `%USERPROFILE%\AppData\LocalLow\<Company>\<ProjectName>\Saves\<slot>.json`. F5 quick-saves, F9 quick-loads — both gated on console NOT being open.

## Inspect / observe

| Command | What it does |
|---|---|
| `inspect <name\|id>` | Full component dump for an NPC. |
| `inspect-room <name\|id>` | Room state (bounds, illumination, occupants). |

## Topology mutation

| Command | What it does |
|---|---|
| `move <name\|id> <x> <z>` | Teleport entity to tile (X, Z). Floats are rounded to int tile coords. |
| `lock <door-id\|name>` | Attach `LockedTag`. |
| `unlock <door-id\|name>` | Remove `LockedTag`. |
| `spawn <archetype> <x> <z>` | Spawn a new NPC. |
| `despawn <name\|id>` | Remove an entity entirely. |

## Engine internals (advanced)

| Command | What it does |
|---|---|
| `set-component <id\|name> <Component> <field>=<value>` | Mutate any component field via reflection. Power tool. |
| `seed <int>` | Note desired RNG seed. Full effect needs engine restart. |

## Legacy aliases (preserved for muscle memory)

| Alias | New form |
|---|---|
| `force-kill <name>` | `scenario kill <name>` |
| `force-faint <name>` | `scenario faint <name>` |

---

## Scenario subverbs

`scenario` (no args) lists them all. `scenario help <subverb>` for one.

| Subverb | What it does | Example |
|---|---|---|
| `list-npcs` | Print every NPC's name + life state + short id. **Use this first** when you don't know names. | `scenario list-npcs` |
| `kill <name> [cause]` | Push NPC to Deceased. `[cause]`: `Choked` / `SlippedAndFell` / `StarvedAlone` / `Died`. Bereavement cascade fires on witnesses. | `scenario kill Donna` |
| `faint <name>` | Push NPC to Incapacitated via faint. They recover on their own over time. | `scenario faint Frank` |
| `choke <name\|--random> [--bolus-size <small\|medium\|large>]` | Attach `ChokingComponent`. Choke timer starts; rescue window opens. | `scenario choke Donna --bolus-size large` |
| `slip <name>` | Spawn a stain on the NPC's tile/path; trigger `SlipAndFallSystem`. | `scenario slip Frank` |
| `lockout <name>` | Lock all doors of the room the NPC is in; lockout starvation timer starts. | `scenario lockout Donna` |
| `rescue <victim> [--rescuer <name>]` | Trigger a rescue intent toward victim. Kind inferred from victim state (Heimlich / CPR / DoorUnlock). | `scenario rescue Donna --rescuer Frank` |
| `chore-microwave-to <name>` | Force this game-week's microwave-cleaning chore to the named NPC. | `scenario chore-microwave-to Donna` |
| `throw <prop-id> at <target>` | Apply `ThrownVelocityComponent` toward target. Target = NPC name OR `x,y,z` coords OR `--here`. | `scenario throw mug at Donna` |
| `sound <SoundTriggerKind> [at <x,y,z>]` | Emit a sound trigger directly. Kinds: `Cough`, `ChairSqueak`, `BulbBuzz`, `Footstep`, `SpeechFragment`, `Crash`, `Glass`, `Thud`, `Heimlich`, `DoorUnlock`. (Currently silent — BUG-009 — host-side audio synth not built.) | `scenario sound Cough` |
| `set-time <time>` | Jump sim wall-clock. Accepts `morning` (08:00), `midday` (12:00), `dusk` (18:00), `night` (22:00), or `HH:MM`. | `scenario set-time dusk` |
| `seed-stains <count>` | Spawn N slip-hazard stains at random walkable tiles. | `scenario seed-stains 5` |
| `seed-bereavement <name> <count>` | Pre-populate `BereavementHistoryComponent` with N synthetic mourned ids. Tests long-arc grief without N actual deaths. | `scenario seed-bereavement Donna 3` |

---

## Workflow recipes

### Trigger a death + bereavement cascade

```
scenario list-npcs
scenario kill Donna
```
Witnesses should grieve (chibi-emotion cues, log entries). If chibi cues don't appear: BUG-013 is open (chibi populator not in scene yet).

### Trigger a choke, then rescue

```
scenario choke Donna
scenario rescue Donna
```
Donna gets `ChokingComponent`. Heimlich animation should play; nearest bystander (or named `--rescuer`) runs over to perform Heimlich; Donna recovers.

### Verify slip mechanic

```
scenario seed-stains 8
scenario list-npcs
```
Walk an NPC over a stain (use `move` if needed). `SlipAndFallSystem` fires.

### Force lockout starvation

```
scenario lockout Donna
scenario set-time night
```
Donna trapped in her current room until rescued via `unlock <door>` or `scenario rescue Donna`.

### Trigger build-mode-related ambient sound (currently no audible output)

```
scenario sound BulbBuzz
```
Engine emits the trigger; host has no synth listener (BUG-009) so nothing audible. Will become useful once BUG-009 lands.

---

## What's broken right now

The dev console works (commands execute, output appears, history is preserved). Several engine-side surfaces don't yet reflect their bound output visibly — track these in `docs/known-bugs.md`:

- **BUG-009** — no audio synth listener; sound triggers fire engine-side but produce no audible output
- **BUG-010** — selection chain (NPCs need both `SelectableTag` and `NpcSelectableTag`); inspector + camera-glide depend on it
- **BUG-011** — keyboard bleed-through (now mitigated; verify with PT-002)
- **BUG-012** — build mode toggles state but no visible palette (BuildPaletteUI unwired refs)
- **BUG-013** — bereavement cascade fires events but chibi-emotion cues not rendered (ChibiEmotionPopulator not in PlaytestScene)

Use this doc as a quick reference during sessions. Append new commands here when they land in `WP-*` packets.
