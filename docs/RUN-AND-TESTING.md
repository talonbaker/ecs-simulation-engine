# ECS Simulation Engine — Run Instructions & Dev Testing Guide

**Applies to:** v0.7.1 and later  
**Last updated:** April 2026

---

## Prerequisites

You need the .NET 8 SDK installed on your machine. Verify with:

```
dotnet --version
```

This should print `8.x.x` or higher. If dotnet is missing, download it from
`https://dot.net` — install the **SDK** (not just the Runtime).

The visualizer (ECSVisualizer) requires a desktop environment to render the
Avalonia window. It will not run headless. The CLI (ECSCli) is fully headless
and is the primary tool for testing and balancing.

---

## Repository Layout

```
ECSSimulation.sln           ← solution file: build everything from here
SimConfig.json              ← ALL tuning values live here (edit this to balance)
CHANGELOG.md                ← version history
docs/
  HANDOFF-v0.7.md           ← architecture reference for this codebase
  ROADMAP-v1.0.md           ← planned path to v1.0
  RUN-AND-TESTING.md        ← this file

APIFramework/               ← core ECS library (no UI, no entry point)
  Components/               ← all component structs + tags + NutrientProfile
  Systems/                  ← all ISystem implementations
  Config/                   ← SimConfig.cs (typed config classes)
  Core/                     ← EntityManager, SimulationEngine, Bootstrapper, Clock

ECSCli/                     ← headless console runner (the primary test harness)
ECSVisualizer/              ← Avalonia desktop GUI
```

---

## Building

Always build from the solution root. This compiles all three projects and catches
any cross-project type errors.

```
cd C:\repos\_ecs-simulation-engine
dotnet build ECSSimulation.sln
```

A clean build prints:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

If the build fails, fix errors before doing anything else. The HANDOFF documents
note that each version ships grep-verified but not always build-verified — so the
first thing you do after pulling changes is always `dotnet build`.

---

## Running the CLI

The CLI is the main tool. It runs the simulation headlessly, prints live snapshots
to stdout, and produces a balancing report at the end. It reads `SimConfig.json`
automatically by searching upward from the working directory.

### Basic invocations

Run from the solution root or from the `ECSCli/` folder:

```
dotnet run --project ECSCli
```

This runs forever (until Ctrl+C) with snapshots every 10 game-minutes.

---

### CLI flags reference

```
--timescale  -t  <n>    Override TimeScale. Default: from SimConfig.json (120).
                         At 120x: 1 real-second = 2 game-minutes.
                         At 1200x: 1 real-second = 20 game-minutes.

--duration   -d  <n>    Stop after N game-seconds.
                         86400  = 1 game-day
                         172800 = 2 game-days
                         604800 = 1 game-week

--ticks          <n>    Stop after exactly N ticks. Alternative to --duration.
                         Good for benchmarks where you want a fixed workload.

--snapshot   -s  <n>    Print a state snapshot every N game-seconds.
                         Default: 600 (every 10 game-minutes).
                         --snapshot 3600 prints once per game-hour.
                         --snapshot 0    disables mid-run snapshots.

--quiet      -q         Suppress all mid-run output including live violations.
                         Only the final report prints. Good for long overnight runs.

--no-report             Skip the end-of-run balancing report.
                         Use with --ticks for pure throughput benchmarks.

--no-violations         Don't print invariant violations as they happen.
                         They still appear in the end-of-run report.

--help       -h         Print the help text and exit.
```

### Useful combinations

```
# Quick smoke test — one game-day, snapshot every game-hour, full report
dotnet run --project ECSCli -- -d 86400 -s 3600

# 2-day balancing run with frequent snapshots
dotnet run --project ECSCli -- -d 172800 -s 600

# Quiet 3-day run for report only
dotnet run --project ECSCli -- -d 259200 -q

# Benchmark: 200k ticks, no output overhead
dotnet run --project ECSCli -- --ticks 200000 --no-report -q

# Run at 1x timescale to watch things move slowly (useful for debugging timing)
dotnet run --project ECSCli -- -t 1 -s 60
```

> **Hot-reload tip:** Start a long run, then edit and save `SimConfig.json` while
> the simulation is running. The watcher picks up the change within a second and
> prints `[Hot-reload] Change detected...`. The new values take effect on the next
> tick. No restart needed. Use this to tune drain rates, absorption fractions, and
> thresholds while watching the output react live.

---

## Running the Visualizer

```
dotnet run --project ECSVisualizer
```

A dark-themed Avalonia window opens. The time-scale slider in the top-right
controls speed (0–480x). Use this for visual inspection, not for long automated
runs — the GUI render loop adds overhead and slows the simulation at high tick rates.

The visualizer is also a good sanity check that the observable properties bound
in `EntityViewModel` are actually updating. If a panel is missing or frozen, there
is likely a binding mismatch between the XAML property name and the generated
observable property name.

---

## Understanding the CLI Output

### Snapshot header

```
──────────────────────────────────────────────────────────────────
  ECS Simulation Engine  v0.7.1
  TICK 43200      Day 1 — 10:00 AM         [day]  WALL 6.0s  (119x)
──────────────────────────────────────────────────────────────────
```

- **TICK** — cumulative ticks since start. At 60 ticks/s (dt = 1/60), 43200 ticks
  = 720 real-seconds = 12 real-minutes.
- **Day/time display** — in-game clock. 6 AM start, 24-hour game-day.
- **WALL** — real elapsed seconds since the simulation started.
- **actual speed** — how many game-seconds per real-second. Should be close to
  your TimeScale setting; if it's much lower, the machine is CPU-bound.

### Entity section

```
  ◈ Human  [a1b2c3]
    Tags      HUNGRY  ·  IRRITABLE
    Satiation  [████████░░░░░░]   58.3%    hunger  41.7%
    Hydration  [████████████░░]   84.1%    thirst  15.9%
    Energy     [████████████░░]   83.5%    awake
    Sleepiness [███░░░░░░░░░░░]   22.1%    circadian ×0.42
    Brain      dominant EAT     eat 0.42  drink 0.16  sleep 0.24
```

- **Tags** — active biological state flags. Watch for STARVING, DEHYDRATED,
  EXHAUSTED as signs that the simulation is unbalanced.
- **dominant** — what the BrainSystem has decided Billy should do right now.
  Under normal conditions this cycles: EAT → NONE → DRINK → NONE → SLEEP.
- **circadian** — the time-of-day multiplier on sleep urgency. Above 1.0 at night,
  below 0.5 in the morning.

### Digestive pipeline section (v0.7.1+)

```
    Stomach    [████░░░░░░░░░░]   26.0%    (260/1000 ml)
               queued    117 kcal  water  89.0ml  carbs 27.0g  prot  1.3g  fat  0.4g

    SmallInt   [████████░░░░░░]   50.0%    (100.0/200 ml)
               absorbing    117 kcal  water 89.0ml  carbs 27.0g  prot  1.3g  fat  0.4g  fiber  3.1g

    LargeInt   [░░░░░░░░░░░░░░]    1.5%    (7.5/500 ml)   waste  1.1ml

    ── Nutrient Stores ───────────────────────────────
    Calories         234 kcal
    Macros       carbs  52.9g  prot   2.4g  fat   0.7g  fiber   0.0g
    Water            178 ml
    Vitamins     A 0.0  B 0.7  C 17.0  D 0.0  E 0.0  K 0.0  (mg)
    Minerals     Na 0  K 844  Ca 0  Fe 0.0  Mg 64  (mg)
```

The SmallInt and LargeInt rows only appear when those organs are active (non-empty).
Between meals they are hidden. This is by design — the UI should only show what
is currently interesting.

---

## End-of-Run Balancing Report

The report prints automatically at the end of any run unless you pass `--no-report`.
It has three sections:

**Invariants** — counts of impossible-state violations caught and clamped by
`InvariantSystem`. A healthy run shows `0 violations`. Repeated violations of the
same field every tick mean a system is producing bad values continuously; fix that
system before tuning anything else.

**Lifecycle events** — first hunger, first thirst, first sleep, etc., with timestamps.
Use these to verify the timing feels right. First hunger at 9–10 AM is good;
first hunger at 6:05 AM means the drain rate is too high.

**Resource ranges** — min/mean/max for Satiation, Hydration, Energy, Sleepiness,
Stomach fill over the run. Min values near 0 for long periods indicate the system
isn't recovering fast enough. Max values stuck at 100 mean the system is barely
challenged.

**Balancing hints** — automated flags for common problems. Read these first.

---

## Manual Dev Testing Checklist — v0.7.1

Work through these checks after every build that touches the digestive pipeline.
Run with `dotnet run --project ECSCli -- -d 86400 -s 1800` (1 game-day,
snapshot every 30 game-minutes) for the first pass. Then check the report.

### Build

- [ ] `dotnet build ECSSimulation.sln` produces **0 errors, 0 warnings**.

### System ordering sanity

Look at the first snapshot (around 6:00 AM game-time, before Billy has eaten).

- [ ] SmallInt and LargeInt rows are **absent** in the first snapshot (both are
  empty at spawn; they should not appear until food has transited the stomach).
- [ ] Stomach shows 0% fill at the very start (entity spawns with an empty stomach).
- [ ] NUTRIENTS panel is absent or all zeros at start (NutrientStores = empty at spawn).

### First meal transit

After Billy eats for the first time (watch for `dominant EAT` then Satiation rising),
verify the pipeline cascade across subsequent snapshots:

- [ ] **Stomach fills** — CurrentVolumeMl increases; NutrientsQueued shows kcal.
- [ ] **Stomach drains** — over the next 30–60 game-minutes the stomach fill drops.
- [ ] **SmallInt appears** — once DigestionSystem starts releasing chyme, the SmallInt
  row appears in the snapshot. Fill rises as the stomach releases, then drops as
  SmallIntestineSystem absorbs.
- [ ] **SmallInt contents make sense** — the `absorbing` line should show decreasing
  kcal/carbs/protein/fat over time as the SI processes the batch. Fiber should be
  present (it doesn't absorb, so it persists longer than macros).
- [ ] **LargeInt appears** — shortly after SmallInt starts filling, LargeInt should
  show a small fill (15% of SI processed volume per tick). Fill is very small
  compared to the SI (residue fraction is 15%; SI capacity is 200 ml vs LI's 500 ml).
- [ ] **WasteReadyMl grows** — the `waste X.Xml` figure on the LargeInt row should
  tick upward slowly as LargeIntestineSystem desiccates content. It never goes down
  in v0.7.1 (the rectum drain is v0.7.3).
- [ ] **NutrientStores fills** — the Nutrient Stores panel values should increase after
  SmallIntestineSystem runs. Specifically: Carbs, Protein, Fat, Water, VitaminC,
  VitaminB, Potassium, Magnesium should all show positive values after a banana.
  Fiber does NOT appear in NutrientStores (it's indigestible).

### Satiation/Hydration feel preserved

The critical invariant from v0.7.0 must hold. Compare pre- and post-meal values:

- [ ] **A single banana gives ~35 satiation** — watch Satiation before and after
  a full banana transit. The gain should be approximately 35 percentage points.
  If it's much lower, SatiationPerCalorie or the banana's caloric content changed.
  If it's much higher, something is double-counting.
- [ ] **A single water gulp gives ~30 hydration** — same check for Hydration after
  a drink. Should be approximately 30 points. DigestionSystem controls this (the
  Satiation/Hydration conversion was intentionally kept there, not moved to SI).
- [ ] **NutrientStores.Water grows from SmallIntestineSystem** — water in stores
  comes from SI (50% of chyme water absorbed there) plus LargeIntestineSystem
  (90% of residue water recaptured). Stores water should be less than the raw
  water consumed (some is lost to stool), but close.

### Invariant report check

At the end of the run:

- [ ] **0 invariant violations** in a normal 1-day run. The most likely sources of
  new violations after v0.7.1 are `SmallIntestineComponent.CurrentVolumeMl` going
  slightly negative (floating-point drift on the last absorption tick before empty)
  or `LargeIntestineComponent.CurrentVolumeMl` same. These should be caught and
  clamped by InvariantSystem with a count of 1–2 total, not hundreds.
- [ ] If you see many violations on `SmallIntestineComponent.Contents.*`, a negative
  nutrient value is propagating through the absorption calculation. Check the
  `batch - absorbed` subtraction in SmallIntestineSystem — absorbed should never
  exceed batch for any field.

### Timing sanity (at default 120x timescale)

These are approximate. The goal is the right order of magnitude:

- [ ] **Stomach empties** in roughly 50–60 game-minutes after a meal.
  (50 ml / 0.017 ml/s ÷ 60 = ~49 game-minutes)
- [ ] **SmallInt clears** in roughly 3–4 game-hours after a meal.
  (50 ml / 0.004 ml/s ÷ 3600 = ~3.5 game-hours)
- [ ] **LargeInt processes residue** in roughly 1–2 game-hours.
  (7.5 ml residue / 0.002 ml/s ÷ 3600 = ~1 game-hour for water extraction)
- [ ] **WasteReadyMl per banana** — after the LI has finished processing, expect
  roughly 0.75 ml of compacted waste (7.5 ml residue × 10% not recaptured).
  Over a full day with 3 meals: ~2–3 ml total. This is smaller than reality
  but correct for the volume model used.

### Biological cycle check (run a 2-day simulation)

```
dotnet run --project ECSCli -- -d 172800 -s 3600
```

Look at the lifecycle events in the report:

- [ ] Billy eats **2–3 times per game-day** (FeedEvents 4–7 over 2 days).
- [ ] Billy drinks **5–8 times per game-day** (DrinkEvents 10–16 over 2 days).
- [ ] Billy sleeps **once per game-day**, roughly 8 game-hours (SleepCycles = 2).
- [ ] No STARVING or DEHYDRATED tags sustained for more than 2–3 game-hours.
- [ ] Satiation min > 10% (if min hits 0, the drain rate or hunger threshold is off).
- [ ] Hydration min > 15% (same reasoning).

### Hot-reload check

- [ ] Start a run: `dotnet run --project ECSCli -- -d 172800`
- [ ] While it's running, open `SimConfig.json` and change
  `"absorptionRate"` under `"smallIntestine"` from `0.004` to `0.008`.
- [ ] Save the file.
- [ ] Within 1–2 seconds the console should print:
  `[Hot-reload] Change detected in SimConfig.json — applying next tick...`
  followed by `SmallIntestineSystemConfig.AbsorptionRate  0.004 → 0.008`
- [ ] Subsequent SmallInt fill bars should drop more quickly.
- [ ] Revert to `0.004` and save again to restore defaults.

---

## Troubleshooting Common Problems

**SmallInt never appears in snapshots**  
Check that `EntityTemplates.SpawnHuman` initializes `SmallIntestineComponent`.
Check that `DigestionSystem` is not skipping entities due to the
`if (!entity.Has<SmallIntestineComponent>()) continue;` guard — this guard
requires the component to be present on all digestive entities.

**NutrientStores stays at zero**  
`SmallIntestineSystem` writes to NutrientStores; `DigestionSystem` no longer does.
If stores are zero, SmallIntestineSystem is not running. Check the system pipeline
in `SimulationBootstrapper.RegisterSystems` — both intestine systems must be
registered after `DigestionSystem`.

**Satiation gain is much lower than ~35 per banana**  
DigestionSystem's Satiation/Hydration conversion was kept in `DigestionSystem`,
not moved to `SmallIntestineSystem`. If you see lower-than-expected satiation,
check that `DigestionSystem` still applies `released.Calories * SatiationPerCalorie`.
The `released` variable should be non-zero each tick the stomach is non-empty.

**Invariant violations on SmallIntestineComponent.CurrentVolumeMl every tick**  
This means a system is setting CurrentVolumeMl to a value above `CapacityMl` (200 ml)
continuously. Check whether the `DigestionSystem` backpressure gate is working:
`receivableVolume = CapacityMl - si.CurrentVolumeMl` — if this is always positive,
the cap is not being hit. If violations are of the form `actual 210, clamped to 200`,
the digestive rate exceeds the SI absorption rate and content is piling up. Reduce
`DigestionRate` or increase `AbsorptionRate` in SimConfig.json.

**Build error: type or namespace not found for SmallIntestineComponent**  
The component file is in `APIFramework/Components/SmallIntestineComponent.cs`. The
project uses implicit usings; if the namespace resolution fails, check the
`APIFramework.csproj` includes all files in the Components/ folder (it should via
`<Compile Include="**\*.cs" />` by default in SDK-style projects).

---

## About the Sandbox (for AI-assisted sessions)

This project is developed with AI assistance (Claude in Cowork mode). The AI agent
has read/write access to the repo folder but runs in a sandboxed Linux environment
that does not have the .NET SDK installed. Outbound network is proxied and blocked,
so the SDK cannot be downloaded or installed during a session.

This means:

- **The AI can read, write, and edit all source files** — it can implement new
  systems, fix bugs, update configs, and write documentation.
- **The AI cannot run `dotnet build` or `dotnet run`** — all build verification
  must be done by you locally after a session.
- **The AI verifies changes with grep** — after writing code, it checks that all
  new type names appear in the right files, that removed references are gone, and
  that config classes match their JSON keys. This catches most structural errors
  but not compile errors from type mismatches.

**The workflow is therefore:**

1. AI session: implement a feature, grep-verify consistency.
2. You: `dotnet build` locally. Fix any compile errors (usually minor).
3. You: run the manual testing checklist above.
4. Both: if something is broken, paste the error and continue in the next session.

The HANDOFF docs (`docs/HANDOFF-v0.7.md` and future handoffs) capture everything
needed to restart an AI session with full context. The AI reads these at the start
of each session to pick up where the previous one left off.

---

## Quick Reference Card

```
BUILD          dotnet build ECSSimulation.sln

RUN CLI        dotnet run --project ECSCli
RUN GUI        dotnet run --project ECSVisualizer

SMOKE TEST     dotnet run --project ECSCli -- -d 86400 -s 1800
2-DAY BALANCE  dotnet run --project ECSCli -- -d 172800 -s 3600
QUIET RUN      dotnet run --project ECSCli -- -d 172800 -q
BENCHMARK      dotnet run --project ECSCli -- --ticks 200000 -q --no-report

HOT-RELOAD     edit SimConfig.json while CLI is running → saves apply live

KEY FLAGS      -t  timescale override   (e.g. -t 1200 for faster game-time)
               -d  duration in game-s   (86400 = 1 day, 172800 = 2 days)
               -s  snapshot interval    (600 = every 10 game-min)
               -q  quiet mode           (final report only)
```
