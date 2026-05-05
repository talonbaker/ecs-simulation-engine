# Runbook — ECS Simulation Engine C2 Infrastructure

This is the operational guide for running the Warden 1-5-25 workflow against the ECS Simulation Engine. It covers the two distinct workflows (build-the-factory and operate-the-factory), how to set up a new machine, how to read the outputs, and how to recover from common problems.

If this is your first time, read §1 and §2, then skip to the workflow you need.

---

## 1. What this project is

A headless C# ECS simulation engine (`APIFramework`, `ECSCli`, `ECSVisualizer`) and a build-time C2 (Command & Control) layer (`Warden.*`) that orchestrates Anthropic API calls across three tiers — one Opus "General", five Sonnet "Engineers", and twenty-five Haiku "Grunts" — to generate and validate content for the eventual game.

**Key architectural axioms** (see `00-SRD.md` §8 for the full list):

- The Anthropic API is a design-time tool. The runtime game makes zero LLM calls.
- Charm is curated, not simulated. Dust ships pre-installed.
- Save/load reuses `WorldStateDto`. Replay and save/load are different concepts on the same storage shape.

---

## 2. What Phase 0 built

Thirteen projects in `ECSSimulation.sln`:

**Original engine (unchanged in Phase 0):**
- `APIFramework/` — ECS core.
- `APIFramework.Tests/` — engine tests.
- `ECSCli/` — the headless CLI, now with a new `ai` verb tree (WP-04).
- `ECSVisualizer/` — Avalonia visualizer (not used in Phase 0).

**New Warden C2 layer (`Warden.*`):**
- `Warden.Contracts/` (WP-02) — DTOs, JSON schemas, and a minimal validator.
- `Warden.Contracts.Tests/` — schema round-trip tests.
- `Warden.Telemetry/` (WP-03) — projects `SimulationSnapshot` into the wire-format `WorldStateDto`; dispatches `AiCommandBatch` mutations into a live engine.
- `Warden.Telemetry.Tests/`
- `Warden.Anthropic/` (WP-05) — thin Messages + Batches HTTP client.
- `Warden.Anthropic.Tests/`
- `Warden.Orchestrator/` (WP-06/07/08/09/10/11/12) — the factory: `PromptCacheManager`, `BatchScheduler`, `CostLedger`, `ConcurrencyController`, `ChainOfThoughtStore`, `FailClosedEscalator`, `ReportAggregator`, `Program.cs`.
- `Warden.Orchestrator.Tests/`
- `ECSCli.Tests/` (WP-04) — integration tests for the `ai` verb tree.

---

## 3. Prerequisites

On the machine you'll run this on:

- **.NET 8 SDK.** `dotnet --version` should report `8.0.x`.
- **Git for Windows** (or Git on whatever OS you're on). Required for the build-the-factory workflow's `git worktree` setup.
- **An Anthropic API key** from `console.anthropic.com` with a non-zero credit balance. Store it as the environment variable `ANTHROPIC_API_KEY`.
- **Claude Code** (for the build-the-factory workflow only — not needed to operate the factory). Install via `irm https://claude.ai/install.ps1 | iex` on Windows, or follow the current instructions at `https://docs.claude.com/en/docs/claude-code/setup`.

---

## 4. First-time setup on a new machine

```powershell
# 1. Clone
git clone <repo-url> ecs-simulation-engine
cd ecs-simulation-engine

# 2. Build
dotnet restore ECSSimulation.sln
dotnet build ECSSimulation.sln -c Release

# 3. Test
dotnet test ECSSimulation.sln

# 4. Regenerate the engine fact sheet (required for real-API runs)
dotnet run --project ECSCli -- ai describe --out docs/engine-fact-sheet.md

# 5. Persistent API key (optional but recommended)
[System.Environment]::SetEnvironmentVariable('ANTHROPIC_API_KEY', 'sk-ant-...', 'User')
# Close and reopen PowerShell after this.
```

If all four commands succeed and `docs/engine-fact-sheet.md` is no longer a placeholder, the machine is ready.

---

## 5. Two workflows

This project has two distinct operational modes and they should not be confused.

**Build-the-factory** (§6) is how you extend the infrastructure or add new features. You dispatch a Work Packet to a Sonnet-level Claude Code session, it writes code, you review, merge, and move on. This is the mode used for Phase 0 and will be used for every subsequent phase.

**Operate-the-factory** (§7) is how you actually *run* a mission through the built orchestrator. You write a mission brief, Opus produces specs, the orchestrator dispatches Sonnets and Haikus on your behalf, and you get a report. This is what Phase 0 made possible.

---

## 6. Build-the-factory workflow

### 6.1 Overview

One Work Packet at a time. Each packet is a self-contained brief in `docs/c2-infrastructure/work-packets/WP-NN-<slug>.md`. Dependencies are declared in the packet header and enforced by the operator (you).

Parallelism is bounded by the dependency graph. Two packets can run in parallel only if neither depends on the other *and* their file footprints don't overlap. See `README.md`'s dependency DAG.

### 6.2 Dispatch procedure

For each packet `WP-NN-<slug>`:

```powershell
# 1. Worktree for isolated edits
cd C:\repos\_ecs-simulation-engine  # or wherever your main clone is
git worktree add ../sim-wp-NN -b feat/wp-NN
cd ../sim-wp-NN

# 2. Claude Code session with Sonnet
$env:Path += ";C:\Users\$env:USERNAME\.local\bin"   # only if Claude Code isn't on PATH persistently
$env:ANTHROPIC_API_KEY = "sk-ant-..."                # only if not set persistently
claude --model sonnet
```

Then paste the dispatch prompt (from `docs/c2-infrastructure/prompts/opus-sonnet-bootstrap.md`, with `WP-NN-<slug>` substituted) as the first message.

### 6.3 After the Sonnet finishes

1. Read `docs/c2-infrastructure/work-packets/_completed/WP-NN.md`. If `outcome` is not `ok`, the packet was blocked — read the `blockingArtifact` field, fix the underlying cause (usually in the packet text itself), and re-dispatch.
2. From inside the worktree, run the acceptance tests: `dotnet build` and `dotnet test`. Expected state: zero warnings, zero failures.
3. If green, merge back to main:

    ```powershell
    cd C:\repos\_ecs-simulation-engine
    git merge --no-ff feat/wp-NN
    git worktree remove ../sim-wp-NN
    git branch -d feat/wp-NN
    ```

4. If a parallel-sibling packet is still running, pull the merged main into its worktree so it sees the new code:

    ```powershell
    cd C:\repos\sim-wp-MM
    git rebase main    # or merge, whichever your workflow prefers
    ```

### 6.4 What *not* to do

- Don't iterate with the Sonnet inside a dispatched session. The handshake is one-shot. If the result is wrong, edit the packet, throw the session away, and re-dispatch.
- Don't let two Sonnets edit the same folder in parallel. Parallelism is per-packet, per-branch, per-file-footprint.
- Don't skip the completion note. The audit trail is the union of all the notes.
- Don't run real-API acceptance tests (like `WP-09` AT-08) without confirming your Anthropic account has credit.

---

## 7. Operate-the-factory workflow

### 7.1 Prerequisite: regenerate the engine fact sheet

The cached-prefix corpus includes `docs/engine-fact-sheet.md`, which ships as a placeholder. Before any real-API mission, regenerate it so Sonnets see the current engine structure:

```powershell
dotnet run --project ECSCli -- ai describe --out docs/engine-fact-sheet.md
```

Mock runs (`--mock-anthropic`) don't strictly need this, but it's harmless to run either way.

### 7.2 Dry-run (no API calls, no tokens spent)

```powershell
dotnet run --project Warden.Orchestrator -- run `
  --mission examples/smoke-mission.md `
  --specs "examples/smoke-specs/*.json" `
  --dry-run
```

This prints the assembled prompts for each tier and exits 0 without calling Anthropic. Useful for confirming the cache manager is building the right corpus.

### 7.3 Mock run (no real API calls, full pipeline test)

```powershell
dotnet run --project Warden.Orchestrator -- run `
  --mission examples/smoke-mission.md `
  --specs "examples/smoke-specs/*.json" `
  --mock-anthropic
```

Returns canned responses from `examples/mocks/`. Produces a full `./runs/<runId>/` directory with a real cost ledger (zeroed out) and a real report. Use this to validate the pipeline end-to-end without spending money.

Exit codes: 0 = all ok, 1 = at least one worker failed, 2 = at least one worker blocked, 3 = budget exceeded, 4 = orchestrator error.

### 7.4 Real-API run (the real thing)

```powershell
dotnet run --project Warden.Orchestrator -- run `
  --mission examples/smoke-mission.md `
  --specs "examples/smoke-specs/*.json" `
  --budget-usd 1.00
```

`--budget-usd` is a hard ceiling. When running spend + the next projected call would exceed it, the orchestrator halts mid-run with exit code 3 and writes a partial report. A missing `--budget-usd` is rejected at startup to prevent accidental unbounded spend.

Typical smoke-mission cost at the current pricing is $0.08–$0.15. See `02-cost-model.md` for the full math.

### 7.5 Resume a crashed run

```powershell
dotnet run --project Warden.Orchestrator -- resume --run-id <runId>
```

Inspects `./runs/<runId>/` and re-dispatches only workers that lack a valid `result.json`. Any worker with a schema-invalid `result.json` is treated as incomplete and re-dispatched.

A `--run-id` collision (two concurrent runs, same id) is rejected at startup.

### 7.6 Validate schemas without running

```powershell
dotnet run --project Warden.Orchestrator -- validate-schemas
```

Parses every schema in `docs/c2-infrastructure/schemas/` and every sample under `examples/`. Useful before committing a schema change.

### 7.7 Preview the cost model

```powershell
dotnet run --project Warden.Orchestrator -- cost-model
```

Prints the per-model rate table and a worked example mission. If Anthropic's pricing changed, this is the fastest way to spot it — the test in `CostRatesTests` will fail and `cost-model` will show the stale numbers.

---

## 8. Anatomy of a run directory

After a run, `./runs/<runId>/` contains:

```
runs/20260424-143200-smoke/
+-- mission.md                  # copy of the input mission brief
+-- events.jsonl                # state transitions, one per line, append-only
+-- cost-ledger.jsonl           # one line per API call, append-only, fsync'd
+-- report.md                   # human-readable mission report
+-- report.json                 # machine-parseable mirror of report.md
+-- sonnet-01/
|   +-- spec.json               # the Opus → Sonnet SpecPacket
|   +-- prompt.txt              # assembled prompt with slab boundary markers
|   +-- response.raw.json       # exact Anthropic response body
|   +-- result.json             # validated SonnetResult (the worker's verdict)
|   +-- worktree/               # git worktree the Sonnet edited
|   +-- haiku-batch.json        # the ScenarioBatch this Sonnet emitted
|       +-- haiku-01/
|           +-- scenario.json
|           +-- prompt.txt
|           +-- response.raw.json
|           +-- result.json     # validated HaikuResult
|           +-- telemetry.jsonl # telemetry the Haiku's ECSCli replay produced
```

**Write-order invariant:** per worker, files are written `prompt.txt → response.raw.json → result.json` in strict order. A worker is considered "done" if and only if `result.json` is present and valid. Resume uses this invariant.

---

## 9. Reading a report

Every run produces `report.md` in a fixed format. The section order is pinned — see `WP-12` for the authoritative template. The idea is that once you learn to skim one, you can skim every future one.

Top-of-report header tells you:

- Run id and mission id.
- Start, end, duration.
- **Terminal outcome** (`ok` / `failed` / `blocked` / `budget-exceeded`) and exit code. This is the first thing to look at.
- **Total spend** and remaining budget.

Then per-tier sections: Sonnet results, Haiku results grouped by parent spec, cost summary, notable events (timeline), artifact links.

If a run outcome is `failed` or `blocked`, the Notes column of the relevant tier table cites the failing acceptance test or block reason. That is your starting point for debugging.

---

## 10. Budget and cost observability

Spend flows through the ledger in real time. While a run is live, you can `tail -f runs/<runId>/cost-ledger.jsonl` in a second window to watch spend accumulate.

Post-run, the cost summary in `report.md` should equal the sum of the ledger (within 1¢ rounding). If they disagree, the `WP-12` AT-02 test should have caught it; if it didn't, treat it as a bug to investigate before trusting future reports.

Per-mission cost expectations (from `02-cost-model.md`):

- Tiny (config tuning, 1 Sonnet, 5 Haiku): ~$0.11.
- Standard (5 Sonnet, 25 Haiku): ~$0.77.
- Heavy (3× cycles): ~$2.20.

If you see a run materially exceed its expected profile, the likely causes are (a) a cache miss across the 5-minute TTL boundary, (b) variable content accidentally placed in a cached slab, or (c) Sonnet output that ballooned due to an ambiguous spec. `PromptCacheManagerTests` AT-01/04/05 guard (a) and (b); (c) is a content issue that shows up in the Sonnet's result JSON.

---

## 11. Troubleshooting

### `claude: The term 'claude' is not recognized`

Claude Code isn't on your PATH. Either add `C:\Users\<you>\.local\bin` to your User PATH persistently, or for the current session only: `$env:Path += ";C:\Users\$env:USERNAME\.local\bin"`.

### `There's an issue with the selected model (sonnet-4-6)`

Use the short alias: `claude --model sonnet` or `/model sonnet` inside a running session.

### `ANTHROPIC_API_KEY is not set`

Either the env var isn't set, or you're in a new PowerShell that doesn't see a previously-set session-scoped var. For session: `$env:ANTHROPIC_API_KEY = "sk-ant-..."`. For persistent: `[System.Environment]::SetEnvironmentVariable('ANTHROPIC_API_KEY', 'sk-ant-...', 'User')` then reopen the shell.

### `FileNotFoundException` at `CachedPrefixSource` startup

One of the files in `Warden.Orchestrator/Cache/cached-corpus.manifest.json` does not exist. Most commonly this is `docs/engine-fact-sheet.md` — regenerate it with `ECSCli ai describe`.

### `Run id collision`

Two runs with the same `--run-id` cannot coexist. Either delete the existing `./runs/<runId>/` or pass a different id.

### Real-API run exits 3 immediately

Budget too low for even the first call. Raise `--budget-usd` — smoke needs at least $0.10, standard at least $1.00.

### A Sonnet dispatched via `claude` appears to hang

It's usually thinking. The UI shows a `Pondering...` indicator with a token counter. A typical mid-sized WP takes 2–6 minutes. If no activity for >15 minutes, interrupt with Ctrl-C, inspect the diff (may be partially correct), and re-dispatch.

---

## 12. Known gaps (Phase 0 end-of-phase state)

These are gaps discovered during the Phase 0 close-out. None of them block Phase 0 acceptance mechanically, but each is worth addressing before Phase 1 work begins.

### 12.1 WP-03 completion note is missing

`docs/c2-infrastructure/work-packets/_completed/WP-03.md` does not exist. The code WP-03 was supposed to produce (`Warden.Telemetry/`) is present and tested, so the work was done — but the audit trail has a hole. SRD §6 acceptance criterion 6 requires one completion note per packet.

**Recommended fix:** re-dispatch a tiny Sonnet packet that reads the `Warden.Telemetry` code, the test results, and writes a retrospective completion note. Or hand-author the note yourself and mark it `reconstructed-retrospectively` in the header so readers know it wasn't the original Sonnet's self-report.

### 12.2 Banned-pattern detector is not wired into the dispatcher

`FailClosedEscalator.Evaluate(SonnetResult, string? worktreeDiff)` exists and is tested (WP-11). `BannedPatternDetector` exists and is tested. But `SonnetDispatcher.RunAsync` (WP-09) does not retrieve the worktree diff and does not pass it to the escalator. The banned-pattern check therefore never fires at runtime.

**Why it matters:** a Sonnet that introduces a new `HttpClient` inside `APIFramework`, or a new `AnthropicClient` outside the orchestrator, would not currently be caught by the escalator in a live mission. The fail-closed property is documented but not enforced.

**Recommended fix:** a small Phase-1 packet (`WP-13-dispatcher-banned-pattern-wiring` or similar) that:

1. Adds a `GitDiffRetriever` to `Warden.Orchestrator` that runs `git diff main...HEAD` against the Sonnet's worktree.
2. Updates `SonnetDispatcher.RunAsync` to call the retriever and pass the result into `FailClosedEscalator.Evaluate(result, diff)`.
3. Adds an integration test: a mock Sonnet that returns `outcome=ok` with a banned-pattern diff is escalated to `blocked`.

### 12.3 Engine fact sheet is a placeholder

`docs/engine-fact-sheet.md` is the pre-regeneration placeholder. The cache manifest references it; its presence is enough to pass the cache-manager's startup check, but its content is not useful until `ECSCli ai describe` regenerates it. Add a note to your own onboarding to always run the regeneration after pulling a fresh clone.

---

## 13. What happens after Phase 0

The factory is built. The next deliverables are content, not infrastructure:

1. `docs/c2-content/world-bible.md` — the office concept.
2. `docs/c2-content/cast-bible.md` — 8–12 NPC archetypes.
3. `docs/c2-content/aesthetic-bible.md` — pixel-art + lighting style commitments.

Phase 1 dispatches packets against those bibles. See `SCHEMA-ROADMAP.md` for the planned schema evolution from v0.1 (now) through v0.5 (rooms, characters, social state, chronicle).

The bibles themselves are author-driven — see `../c2-content/README.md` for structure suggestions.

---

## 14. Quick reference: command cheat sheet

```powershell
# Build + test
dotnet build ECSSimulation.sln -c Release
dotnet test ECSSimulation.sln

# ai verbs
dotnet run --project ECSCli -- ai describe --out docs/engine-fact-sheet.md
dotnet run --project ECSCli -- ai snapshot --out world.json --pretty
dotnet run --project ECSCli -- ai stream --out world.jsonl --interval 600 --duration 1800
dotnet run --project ECSCli -- ai inject --in some-command-batch.json
dotnet run --project ECSCli -- ai replay --seed 42 --duration 3600 --out replay.jsonl

# Orchestrator
dotnet run --project Warden.Orchestrator -- run --mission <md> --specs "<glob>" --mock-anthropic
dotnet run --project Warden.Orchestrator -- run --mission <md> --specs "<glob>" --budget-usd 1.00
dotnet run --project Warden.Orchestrator -- run --mission <md> --specs "<glob>" --dry-run
dotnet run --project Warden.Orchestrator -- resume --run-id <runId>
dotnet run --project Warden.Orchestrator -- validate-schemas
dotnet run --project Warden.Orchestrator -- cost-model

# Build-the-factory dispatch
git worktree add ../sim-wp-NN -b feat/wp-NN
cd ../sim-wp-NN
claude --model sonnet
# then paste the bootstrap prompt from docs/c2-infrastructure/prompts/opus-sonnet-bootstrap.md
```
