# CLI Reference — ECSCli

**Audience:** anyone running the simulation engine outside the Unity host — devs running balance loops, Sonnet/Haiku validators, you (Talon) inspecting state, anyone debugging a scenario.

**What ECSCli is:** the headless, .NET-based command-line interface to the simulation engine. Same engine the Unity host uses; same `WorldStateDto` projection. No graphics, no scene, no input — just the simulation loop and structured stdout/stderr output.

---

## Prerequisites

- **.NET 8 SDK** installed (`dotnet --version` reports 8.x.x).
- **Repo cloned** to `C:\repos\_ecs-simulation-engine` (or your equivalent). All commands below assume the repo root as the working directory.
- **`SimConfig.json`** present at the repo root. The CLI hot-reloads this file while running — edit and save to retune live.
- **Optional:** the `ECSCli` binary on `PATH`. If not, prefix every command with `dotnet run --project ECSCli --` instead of just `ECSCli`. The reference below uses the short form.

To build once before first use:

```
dotnet build ECSSimulation.sln
```

---

## Quickstart

| I want to… | Run this |
|---|---|
| Run the sim forever, snapshot every 10 game-min | `ECSCli` |
| Run for one full game-day | `ECSCli -d 86400` |
| Run a 100k-tick benchmark, no overhead | `ECSCli --ticks 100000 --no-report` |
| See the current floor plan | `ECSCli world map` |
| Capture a one-shot WorldStateDto JSON | `ECSCli ai snapshot --out snap.json` |
| Stream WorldStateDto JSONL for an AI agent | `ECSCli ai stream --out stream.jsonl --interval 60` |
| Regenerate the engine fact sheet | `ECSCli ai describe --out engine-fact-sheet.md` |
| Replay a deterministic scenario | `ECSCli ai replay --seed 42 --duration 600 --out replay.jsonl` |

---

## Top-level invocation — running the simulation

```
ECSCli [options]
```

Boots `SimulationBootstrapper` from `SimConfig.json`, runs the tick loop until a stop condition fires, prints a final report.

### Options

| Flag | Short | Argument | Default | Effect |
|---|---|---|---|---|
| `--timescale` | `-t` | float | from `SimConfig.json` | Override game-time multiplier per tick. Higher = more sim-time per real-time. |
| `--duration` | `-d` | game-seconds | unbounded | Stop after N game-seconds. `86400` = one game-day. |
| `--ticks` | — | integer | unbounded | Stop after exactly N ticks. Mutually exclusive with `--duration` in practice. |
| `--snapshot` | `-s` | game-seconds | `600` (10 game-min) | Print a state snapshot every N game-seconds. `0` = suppress. |
| `--quiet` | `-q` | — | off | Suppress mid-run snapshots AND live invariant violations. Final report only. |
| `--no-report` | — | — | off | Skip end-of-run metrics report. |
| `--no-violations` | — | — | off | Don't print invariant violations as they happen (still in final report). |
| `--help` | `-h` | — | — | Print help and exit. |

### Stop conditions

The sim runs until any one of these fires:

1. `--duration` value reached.
2. `--ticks` value reached.
3. `Ctrl+C` pressed (graceful — finishes the current tick, then stops).

Without `--duration` or `--ticks`, the sim runs forever until `Ctrl+C`.

### Hot-reload

`SimConfig.json` is watched for changes during the run. Edit and save the file; the new config applies on the next tick (no restart required, no torn-tick state). The console prints `[Hot-reload] Change detected …` when this happens. This is the **balance-loop substrate** — tweak a number, save, watch the next snapshot to see the effect.

### Examples

```
# Run forever, snapshot every 10 game-min
ECSCli

# One full game-day, snapshot every game-hour
ECSCli -d 86400 -s 3600

# Two game-days, terminal stays clean (only final report)
ECSCli -d 172800 --quiet

# Benchmark: 100k ticks, no report overhead
ECSCli --ticks 100000 --no-report

# Tune at 16x speed live
ECSCli -t 1920 -d 86400
```

---

## `world map` — ASCII floor plan

```
ECSCli world map [options]
```

Renders the current world snapshot as a Unicode box-drawing floor plan to stdout. The shared spatial substrate that humans, Sonnets, and Haikus can all read directly. Booted from `SimulationBootstrapper` with default config; not the same process as a long-running `ECSCli` invocation.

### Options

| Flag | Argument | Default | Effect |
|---|---|---|---|
| `--floor` | int | `0` | Floor index (0 = Basement, 1 = First, 2 = Top — single-floor at v0.1, only `0` exists). |
| `--no-legend` | — | off | Omit the LEGEND section. Compact form. |
| `--no-hazards` | — | off | Omit hazard glyphs (stains, fire, corpses). |
| `--no-furniture` | — | off | Omit furniture glyphs (desks, microwaves, etc.). |
| `--no-npcs` | — | off | Omit NPC glyphs. Floor plan only. |
| `--watch` | int | omitted (one-shot) | Re-render every N ticks. Press `Ctrl+C` to stop. |

### Glyph contract

| Glyph | Meaning |
|---|---|
| `╔ ═ ╗ ║ ╚ ╝ ╦ ╩ ╠ ╣` | Exterior wall (double-line) |
| `┌ ─ ┐ │ └ ┘ ┬ ┴ ├ ┤ ┼` | Interior wall (single-line) |
| `·` | Open door |
| `+` | Closed door |
| `░` | Corridor / hallway |
| `▒` | Kitchen / break room |
| `▓` | Bathroom (privacy-shaded) |
| `D C M F T S B O` | Furniture: Desk, Chair, Microwave, Fridge/Fountain, Toilet, Sink, Bed/couch, Other |
| `d f g …` | NPCs (lowercase first letter of name) |
| `*` | Stain — or NPC tile collision; legend disambiguates |
| `~ ! x ?` | Hazards: water, fire, corpse, unknown |

**Z-order:** NPC > hazard > furniture > floor shading > wall.

### Examples

```
# One-shot snapshot, full output
ECSCli world map

# Floor plan only (no NPCs, no furniture, no hazards)
ECSCli world map --no-npcs --no-furniture --no-hazards

# Live tail every 10 ticks
ECSCli world map --watch 10

# Capture for sharing in a Claude/Haiku conversation
ECSCli world map > current-map.txt
```

### When to reach for it

- **Triage** — "where is Donna right now?" Skim the legend.
- **Pathing bugs** — `--watch 10`, watch routes evolve. `*` glyphs flag stain hazards.
- **Bug repro snapshots** — `world map > before.txt`, trigger the bug, `world map > after.txt`, diff.
- **Spec authoring** — paste maps into spec docs to ground spatial assertions.
- **Snapshot tests** — capture a fixture file; future regressions show as text diffs.

### Exit codes

`0` — map printed successfully.
`1` — unexpected error (printed to stderr).

---

## `ai describe` — engine fact sheet

```
ECSCli ai describe --out <path>
```

Generates the Markdown engine fact sheet enumerating every system, component, tag, and event the engine ships. Used by the WP-2.9.A staleness CI to catch documentation drift.

### Options

| Flag | Argument | Required | Effect |
|---|---|---|---|
| `--out` | path | yes | Path to write the Markdown fact sheet to. |

### When to reach for it

- After adding a new system, component, or event — regenerate to keep `engine-fact-sheet.md` in sync. CI fails if the file drifts from current engine state.
- For Sonnets/Haikus that need the engine surface as context — feed them the regenerated fact sheet.

```
ECSCli ai describe --out engine-fact-sheet.md
```

---

## `ai snapshot` — one-shot WorldStateDto

```
ECSCli ai snapshot --out <path> [--pretty]
```

Boots a sim, captures a single `WorldStateDto`, writes it as JSON, exits. The atomic primitive for AI tier inputs — Sonnet specs typically operate on a snapshot.

### Options

| Flag | Argument | Required | Effect |
|---|---|---|---|
| `--out` | path | yes | Where to write the JSON. |
| `--pretty` | — | no | Pretty-print (default is compact). |

### Examples

```
# Compact (default — what the orchestrator uses)
ECSCli ai snapshot --out snap.json

# Pretty (for human inspection)
ECSCli ai snapshot --out snap.json --pretty
```

---

## `ai stream` — JSONL WorldStateDto stream

```
ECSCli ai stream --out <path> --interval <game-seconds> [--duration <s>] [--world-definition <path>]
```

Runs the sim and emits a `WorldStateDto` per frame (every N game-seconds) as line-delimited JSON. The dev-time observability surface that AI agents tail in real time.

### Options

| Flag | Argument | Required | Effect |
|---|---|---|---|
| `--out` | path | yes | Path to write the JSONL stream to. |
| `--interval` | game-seconds | yes | Emit a frame every N game-seconds. |
| `--duration` | game-seconds | no | Stop after N game-seconds. Omit = run until Ctrl+C. |
| `--world-definition` | path | no | Override the default cast/world spawn with a custom JSON. Otherwise defaults are used. |

### Examples

```
# Stream every game-minute for one game-day
ECSCli ai stream --out stream.jsonl --interval 60 --duration 86400

# Stream forever from a custom world def
ECSCli ai stream --out stream.jsonl --interval 30 --world-definition docs/c2-content/world-definitions/office-starter.json
```

---

## `ai narrative-stream` — JSONL narrative-event stream

```
ECSCli ai narrative-stream [--out <path>] [--interval <s>] [--duration <s>] [--seed <n>]
```

Streams the narrative event candidates the engine produces — choking, slips, mask-slips, deaths, bereavement, chore refusals. Cheaper than full state stream when you only care about events. Stdout if `--out` omitted.

### Options

| Flag | Argument | Required | Effect |
|---|---|---|---|
| `--out` | path | no | Where to write JSONL (omit for stdout). |
| `--interval` | game-seconds | no | Minimum game-seconds between flushes. `0` = immediate. |
| `--duration` | game-seconds | no | Stop after N seconds. Omit = until Ctrl+C. |
| `--seed` | int | no | RNG seed for deterministic replay (default `0`). |

### Examples

```
# Tail narrative events live
ECSCli ai narrative-stream --interval 0

# Capture deterministic narrative events for one game-day
ECSCli ai narrative-stream --out narrative.jsonl --duration 86400 --seed 42
```

---

## `ai inject` — feed AiCommandBatch into the sim

```
ECSCli ai inject --in <path>
```

Parses an `AiCommandBatch` JSON file and applies the whitelisted mutations to the running sim. The intent is balance-tuning experiments where an AI tier proposes commands; the engine applies them deterministically.

### Options

| Flag | Argument | Required | Effect |
|---|---|---|---|
| `--in` | path | yes | Path to the AiCommandBatch JSON. |

### Examples

```
ECSCli ai inject --in experiments/raise-irritation-batch.json
```

---

## `ai replay` — deterministic scenario replay

```
ECSCli ai replay --seed <n> --duration <s> --out <path> [--commands <path>] [--world-definition <path>]
```

The deterministic-replay primitive used by Haiku scenario validation. Boots a sim with a fixed seed, optionally applies a timestamped command log, runs for a fixed duration, emits the JSONL telemetry stream. Two replays with the same inputs produce byte-identical output.

### Options

| Flag | Argument | Required | Effect |
|---|---|---|---|
| `--seed` | int | yes | RNG seed for deterministic replay. |
| `--duration` | game-seconds | yes | Run for N game-seconds and stop. |
| `--out` | path | yes | JSONL telemetry output path. |
| `--commands` | path | no | Optional timestamped command log to apply during the run. |
| `--world-definition` | path | no | Optional world definition override. |

### Examples

```
# Pure replay, no commands, default world
ECSCli ai replay --seed 1234 --duration 600 --out replay-run.jsonl

# Scripted replay
ECSCli ai replay --seed 1234 --duration 1800 --out scripted-run.jsonl \
                 --commands experiments/donna-skips-lunch.json
```

---

## Common workflows

### Balance-loop iteration

The reason hot-reload exists. Hold this rhythm:

```
1. Start a long run:
   ECSCli --duration 172800           # 2 game-days

2. While it runs, edit SimConfig.json — tweak a number, save.
   The terminal prints [Hot-reload] confirmation.

3. Watch the next snapshot. Did the rhythm improve?

4. Iterate. End-of-run report shows aggregate metrics.
```

### Capture a snapshot for a Claude/Haiku conversation

```
ECSCli ai snapshot --out snap.json --pretty
ECSCli world map > floorplan.txt
# Paste both into the conversation. Snap = data, map = spatial intuition.
```

### Repro a flaky scenario deterministically

```
# Capture seed of the bad run from the snapshot's metadata.
# Re-run with that seed:
ECSCli ai replay --seed <captured-seed> --duration <captured-elapsed> --out repro.jsonl

# Diff repro.jsonl against the original — find the divergent tick.
```

### Regenerate the engine fact sheet (CI dependency)

```
ECSCli ai describe --out engine-fact-sheet.md
```

Run after every packet that adds systems, components, tags, or narrative events. The `FactSheetStalenessTests` xUnit suite (WP-2.9.A) fails CI if this drifts.

---

## Output layout

| Channel | Where | What |
|---|---|---|
| stdout | terminal | Live snapshots, hot-reload notices, the `world map` rendering, ASCII metrics. Pipe-friendly. |
| stderr | terminal | Invariant-violation warnings (when `--no-violations` is off), errors, unknown-flag diagnostics. |
| `--out <path>` | file | JSON, JSONL, or Markdown depending on the verb. AI tiers consume these. |
| `runs/<runId>/` | dir | Orchestrator-managed run artifacts (cost ledger, batch results). Created by `Warden.Orchestrator run`, **not** by `ECSCli` directly. |

---

## Troubleshooting

### "SimConfig.json not found — hot-reload disabled."

You're running outside the repo root. The CLI walks up 8 parent directories looking for `SimConfig.json`. Either run from the repo root or copy the config into your CWD.

### "Unknown argument: --foo"

A flag was misspelled or doesn't apply to the verb you're running. Top-level flags (`--duration`, `--ticks`, etc.) only work on `ECSCli`, not on `ECSCli ai snapshot`. Sub-verbs have their own flag set.

### Determinism check fails

Two runs with the same seed should be byte-identical. If they aren't:
- Confirm `--seed` was actually passed (default is `0`, but if a verb has a different default, check).
- Confirm no source of non-determinism leaked in (system clock reads, unseeded RNG, parallel tasks). The engine is single-threaded by design.
- Confirm `SimConfig.json` was identical across the two runs.

### `world map` shows an empty map

The minimal sim it boots has `humanCount: 1` and a default world. If you expected your full cast or world definition, this verb doesn't load them at v0.1 — the `ai stream` and `ai replay` verbs do.

### Output is garbled in PowerShell / cmd

Box-drawing characters need a UTF-8-capable terminal. Run `chcp 65001` once to set the codepage, or use Windows Terminal / a Linux shell.

---

## See also

- [Build & Dev Setup](04-build-and-dev-setup.md) — prerequisites, dotnet commands, branch flow.
- [Testing Guide](05-testing-guide.md) — xUnit test discipline, fixture patterns.
- [SimConfig Tuning & Game Balance](07-simconfig-tuning-and-game-balance.md) — every config field, hot-reload mechanics, balance-loop best practices.
- [AI Prompting Guide (1-5-25)](08-ai-prompting-guide-1-5-25.md) — Opus / Sonnet / Haiku roles; how the orchestrator uses these CLI outputs.
- `docs/c2-infrastructure/work-packets/WP-3.0.W-ascii-floor-plan-projector.md` — the ASCII map projector spec.
- `docs/c2-infrastructure/work-packets/WP-3.0.W.1-haiku-prompt-ascii-map-integration.md` — pending wire-up of the projector into Haiku validate prompts.
