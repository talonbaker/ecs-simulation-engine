# Phase 1 — Comprehensive Test Guide

> Authored by Opus at Phase 1 closure. This guide tells you, step by step, how to verify that everything Phase 1 built actually works. Each step lists what to run, what to expect, and what to do if it goes red. Read once end-to-end, then run the steps in order.
>
> Companion docs: the runbook (`RUNBOOK.md`) covers operational workflows in general; this guide is specifically the Phase 1 acceptance check. Run the runbook's first-time setup (§4) once on a new machine before the steps below.

---

## 0. Prerequisites

```powershell
dotnet --version          # expect 8.0.x
git --version             # any recent
echo $env:ANTHROPIC_API_KEY.Length    # expect ~108 if you intend to run real-API steps
```

If `ANTHROPIC_API_KEY` is empty and you intend to run §6 below, set it:

```powershell
$env:ANTHROPIC_API_KEY = "sk-ant-..."
# Or persistently:
[System.Environment]::SetEnvironmentVariable('ANTHROPIC_API_KEY', 'sk-ant-...', 'User')
```

Confirm the API key works against Anthropic directly before consuming the orchestrator's budget (one-line sanity ping):

```powershell
$h = @{ "x-api-key"=$env:ANTHROPIC_API_KEY; "anthropic-version"="2023-06-01"; "content-type"="application/json" }
$b = '{"model":"claude-sonnet-4-6","max_tokens":10,"messages":[{"role":"user","content":"ping"}]}'
Invoke-RestMethod -Uri "https://api.anthropic.com/v1/messages" -Method Post -Headers $h -Body $b
```

A response with `id`, `content`, and `usage` confirms the key + model + network path. If this fails, do not proceed to §6 until it works (see Troubleshooting at the bottom).

---

## 1. Build the solution

From the repo root:

```powershell
cd C:\repos\_ecs-simulation-engine
dotnet restore ECSSimulation.sln
dotnet build ECSSimulation.sln -c Release
```

**Expected output:** all 13 projects build, `Build succeeded`, **0 warnings, 0 errors**. Wall-clock 30–90 seconds depending on machine.

If warnings appear, write them down — they're not blocking but they're new and the next Opus should know.

If errors appear, the merge state on `staging` is broken. `git status` to see what's modified locally; `git diff` against `origin/staging` to see if a recent local change introduced the error.

---

## 2. Run the unit test suite

```powershell
dotnet test ECSSimulation.sln -c Release --logger "console;verbosity=normal"
```

**Expected output:** every test project reports `Passed: N, Failed: 0, Skipped: 0`. Total wall-clock 1–4 minutes.

The thirteen test projects and what each covers:

| Project | Coverage |
|:---|:---|
| `APIFramework.Tests` | ECS core, all components (physiology, social, spatial, lighting, movement, dialog, chronicle), all systems including their per-tick behavior and determinism (5000-tick byte-identical-trajectory tests). The largest suite. |
| `APIFramework.Tests/Bootstrap` | World loader (`WorldDefinitionLoader`), cast generator (`CastGenerator`), archetype catalog. |
| `Warden.Contracts.Tests` | DTO round-trips, schema validation against every sample (v0.1, v0.2.1, v0.3, v0.4 world states; corpus; archetypes; world-definition; opus-to-sonnet; sonnet-result; sonnet-to-haiku; haiku-result; ai-command-batch). |
| `Warden.Telemetry.Tests` | Projector population of social, spatial, chronicle state on the wire. Schema-version stamp at `0.4.0`. |
| `Warden.Anthropic.Tests` | HTTP client message + batch shape, error mapping, cache_control header serialization. |
| `Warden.Orchestrator.Tests` | Cache manager (Sonnet vs Haiku role frame selection), batch scheduler (deduplication, JSON extraction, schema validation), cost ledger arithmetic, budget guard, concurrency controller, chain-of-thought persistence, fail-closed escalator (every outcome/reason combination), report aggregator (every section), banned-pattern detector, dispatcher integration. |
| `ECSCli.Tests` | The `ai describe`, `ai snapshot`, `ai stream`, `ai inject`, `ai replay`, `ai narrative-stream` verbs. |

**If any test fails:** read the test output verbatim. Most likely causes:

- A schema sample (`Warden.Contracts.Tests/Samples/*.json`) drifted from its schema. Check the sample against the corresponding schema in `docs/c2-infrastructure/schemas/`.
- An engine determinism test failed — look for the seed value, then run that test in isolation (`dotnet test --filter <name>`) for a stack trace.
- A projector test asserts on `SchemaVersion` — if it expects `"0.1.0"` but the code emits `"0.4.0"`, the test was missed during one of the projector update packets and needs updating.

---

## 3. Verify the engine fact sheet is current

```powershell
dotnet run --project ECSCli -- ai describe --out docs/engine-fact-sheet.md
```

**Expected output:** writes a markdown fact sheet listing every registered system, its phase, its order, and every `SimConfig` key. Wall-clock under 5 seconds. Then verify:

```powershell
Get-Item docs/engine-fact-sheet.md | Select-Object Length, LastWriteTime
Get-Content docs/engine-fact-sheet.md | Select-Object -First 10
```

Length should be 10–20KB. The header should show `**Generated:**` matching the current date and time. The "Registered Systems" table should list 30+ systems including the new ones from Phase 1: `SunSystem`, `LightSourceStateSystem`, `IlluminationAccumulationSystem`, `RoomMembershipSystem`, `ProximityEventSystem`, `DriveDynamicsSystem`, `WillpowerSystem`, `PathfindingTriggerSystem`, `StepAsideSystem`, `IdleMovementSystem`, `FacingSystem`, `LightingToDriveCouplingSystem`, `NarrativeEventDetector`, `PersistenceThresholdDetector`, `DialogContextDecisionSystem`, `DialogFragmentRetrievalSystem`, `DialogCalcifySystem`.

If the fact sheet is missing systems or tiny (<5KB), `ECSCli ai describe` failed to enumerate the engine — likely a `SimulationBootstrapper` registration gap from a partially-merged packet.

---

## 4. Snapshot the engine to JSON

```powershell
dotnet run --project ECSCli -- ai snapshot --out world.json --pretty
```

**Expected output:** writes `world.json` (a pretty-printed `WorldStateDto`). Wall-clock under 3 seconds. Then:

```powershell
Get-Content world.json | Select-Object -First 30
```

Header should show `"schemaVersion": "0.4.0"`. The structure should include the new top-level surfaces from Phase 1: `entities[].social`, `relationships[]`, `memoryEvents[]`, `rooms[]`, `lightSources[]`, `lightApertures[]`, `clock.sun`, `chronicle[]`. If the engine has been booted with the `office-starter.json` world definition (next step), entities and rooms will be populated; otherwise the arrays will be empty/absent (also valid under the v0.4 schema).

---

## 5. Boot the engine from the world definition + run a stream

```powershell
dotnet run --project ECSCli -- ai snapshot `
  --world-definition docs/c2-content/world-definitions/office-starter.json `
  --out world-with-rooms.json --pretty
```

**Expected output:** the snapshot now contains the rooms, light sources, apertures, and NPC slots from the starter world definition. The `rooms[]` array should have ≥6 entries (from the world bible's named anchors), `lightSources[]` ≥8, `lightApertures[]` ≥2, `npcSlots`-derived entities ≥5 (cast generator hasn't spawned full NPCs yet — those are slots).

```powershell
dotnet run --project ECSCli -- ai stream `
  --world-definition docs/c2-content/world-definitions/office-starter.json `
  --interval 600 --duration 3600 --out world.jsonl
```

**Expected output:** writes `world.jsonl` (one JSON line per tick, 6 lines for the configured 1-hour-game-time interval). Wall-clock 5–15 seconds. Tail the file:

```powershell
Get-Content world.jsonl -Tail 1 | ConvertFrom-Json | Select-Object schemaVersion, tick
```

Should show `schemaVersion: 0.4.0` and tick value matching the simulation's progress.

---

## 6. The orchestrator smoke mission (real-API)

This is the canonical end-to-end pipeline test. Costs roughly $0.02–$0.20 depending on cache warmth.

```powershell
dotnet run --project Warden.Orchestrator -- run `
  --mission examples/smoke-mission.md `
  --specs "examples/smoke-specs/spec-smoke-01.json" `
  --budget-usd 1.00
```

**Expected stdout:**

```
[run] <runId>: dispatching 1 Sonnet(s)…
[run] Dispatching 1 Haiku scenario(s)…
[run] <runId>: complete. Report: runs\<runId>\report.md
```

**Expected exit code: 0.** Wall-clock 2–6 minutes (Haiku batch poll dominates).

Then verify the report:

```powershell
$run = Get-ChildItem runs/ | Sort LastWriteTime -Desc | Select -First 1
Get-Content "$($run.FullName)/report.md"
```

Header must show:
- `Terminal outcome: ok`
- `Exit code: 0`
- `Total spend:` between $0.02 and $0.20

Tier 2 table must show one Sonnet row with `outcome: ok` and `1/1` ATs passed.

Tier 3 table currently says "No Haiku scenarios run" even though one was dispatched — this is a known cosmetic aggregator-path bug (see §10). Verify the Haiku actually ran by checking the result file:

```powershell
Get-Content "$($run.FullName)/haiku-01/result.json"
```

Should show a `HaikuResult` JSON with `outcome: "ok"` (or `failed`/`blocked` per the assertion verdict), populated `assertionResults[]` with `id`, `passed`, `observedValue`, `note` fields, and a `tokensUsed` block.

Cost ledger:

```powershell
Get-Content "$($run.FullName)/cost-ledger.jsonl"
```

Two lines: one Sonnet entry with real `inputTokens`, `cachedReadTokens`, `outputTokens`, and `usdTotal`; one Haiku entry currently showing 0 tokens (known cosmetic — orchestrator doesn't pull batch usage from the API yet; see §10).

**If exit code is anything other than 0:** open the report's Sonnet row and read the `Notes` column, then read `runs/<runId>/sonnet-01/result.json` for the structured outcome. Most likely block reasons and their meanings are in the runbook §11.

---

## 7. Mock-mode pipeline test (zero API spend)

```powershell
dotnet run --project Warden.Orchestrator -- run `
  --mission examples/smoke-mission.md `
  --specs "examples/smoke-specs/spec-smoke-01.json" `
  --mock-anthropic
```

**Expected exit code: 0.** Wall-clock under 5 seconds. Same report shape as §6, but cost ledger entries are zero. Useful before any real-API run to confirm pipeline integrity without spending tokens.

---

## 8. Cast-validate mission (known to block — architectural finding)

```powershell
dotnet run --project Warden.Orchestrator -- run `
  --mission examples/smoke-mission-cast-validate.md `
  --specs "examples/smoke-specs/cast-validate.json" `
  --budget-usd 2.00
```

**Expected exit code: 2.** Cost ~$0.02. The Sonnet emits `outcome=blocked, reason=missing-reference-file` because the cast-validate spec asks for files that the orchestrator's API path doesn't make available to the worker. This is a content-design mismatch identified at Phase 1 closure, not an infrastructure failure.

The clean blocked exit (no crash, structured report, fail-closed semantics intact) is the success criterion here. A crash would mean the `FailClosedEscalator` patch from Phase 1 closure regressed.

The two real fixes for cast-validate (reserved for Phase 2):

- **Inline mode:** pre-read the files listed in `inputs.referenceFiles` and prepend them to the user turn. Cleanest fix.
- **Snapshot mode:** boot the engine, run cast generator, project a `WorldStateDto`, and pass that as the spec's input. The Sonnet validates the projected world directly.

---

## 9. The CLI verbs (every `ai` subcommand)

Each runs in seconds. None require API spend.

```powershell
dotnet run --project ECSCli -- ai describe --out tmp-fact.md
dotnet run --project ECSCli -- ai snapshot --out tmp-world.json --pretty
dotnet run --project ECSCli -- ai stream --interval 600 --duration 600 --out tmp-stream.jsonl
dotnet run --project ECSCli -- ai narrative-stream --interval 600 --duration 1800 --out tmp-narrative.jsonl
dotnet run --project ECSCli -- ai replay --seed 42 --duration 3600 --out tmp-replay.jsonl
dotnet run --project ECSCli -- ai inject --in examples/inject-sample.json    # if the sample exists
```

Each should write its named output file and exit 0. The `narrative-stream` is new in Phase 1 — its output file will be a JSONL stream of `NarrativeEventCandidate` records (drive spikes, willpower collapses, conversation events, abrupt departures). Skim a few lines:

```powershell
Get-Content tmp-narrative.jsonl | Select-Object -First 5
```

Each line is one candidate; expect `kind`, `tick`, `participantIds`, `detail`. If the file is empty after 30 sim-minutes, the simulation is too quiet — check that the world definition produces interactions (NPC slots within proximity range, drive baselines elevated, etc.).

---

## 10. Known issues at Phase 1 close (carry forward to Phase 2)

These are real bugs found during real-API testing. Documented here so the next Opus's first reading is informed:

1. **Aggregator doesn't show Haiku results in the Tier 3 table.** The path-doubling bug for Haiku CoT files was fixed (Patch 6 — `RunCommand.cs` now passes `root` instead of `runDir` to `InfraCoT`), but the report aggregator (`Warden.Orchestrator/Reports/ReportAggregator.cs`) still scans the old expected path under `sonnet-NN/haiku-NN/`. Result files exist at `runs/<runId>/haiku-NN/` and validate clean; aggregator just doesn't find them. Fix: update aggregator's scan path. Cosmetic — actual work is captured in `result.json` and the cost ledger.

2. **Haiku ledger entry shows 0 tokens.** The role frame instructs Haikus to placeholder `tokensUsed` to zeros, expecting the orchestrator to overwrite from the batch API response headers. The orchestrator never does the overwrite. Anthropic Console reflects real spend; the local ledger doesn't. Fix: in `BatchScheduler.ParseSucceeded`, after deserialising `HaikuResult`, copy `succeeded.Message.Usage` into `parsed.TokensUsed` before persisting.

3. **Cast-validate mission can't run via the orchestrator's API path.** Section 8 above. Architectural mismatch between Sonnet-via-Claude-Code (file access) and Sonnet-via-API (no file access). Fix: inline mode or snapshot mode per §8.

4. **PromptCacheManager parameterless constructor was overused.** Phase 0's `RunCommand` used `new PromptCacheManager()` for both mock and real-API modes; this produced empty cached slabs that Anthropic rejected with `cache_control cannot be set for empty text blocks`. Patches 1+2 fixed this in production code. Add an integration test that exercises the real-API path with the mock client to catch future regressions.

5. **Fail-closed escalator's switch was incomplete.** The escalator threw `InvalidOperationException` on `BlockReason.MissingReferenceFile` until Phase 1 closure (Patch 9). Catch-all `(OutcomeCode.Blocked, _)` branch added so future enum additions don't crash. Add a test that enumerates every `BlockReason` value through the escalator.

6. **Engine fact sheet placeholder warning.** The shipped `docs/engine-fact-sheet.md` was a Phase 0 placeholder; running `ECSCli ai describe` regenerates it. Add a build step or CI check that fails when the fact sheet's `Generated` timestamp is older than the engine source files.

---

## 11. What "Phase 1 green" means

Pass all of the following and you can sign off on Phase 1:

- §1 build: 0 warnings, 0 errors.
- §2 unit tests: every project passes, 0 failures.
- §3 fact sheet: regenerated, includes the 16+ new Phase-1 systems.
- §4–5 snapshots: emit `schemaVersion 0.4.0` with the new top-level surfaces.
- §6 orchestrator smoke run (real-API): exit 0, ledger populated for at least the Sonnet entry, report's Tier 2 row shows `ok` 1/1.
- §7 mock-mode pipeline test: exit 0, free, structurally identical report.
- §8 cast-validate: exit 2 (blocked, NOT crash), fail-closed semantics demonstrated.
- §9 CLI verbs: all six write their output files and exit 0.

The known issues in §10 do not block Phase 1 acceptance. They are the explicit punch list for Phase 2.

---

## 12. Troubleshooting

**`ANTHROPIC_API_KEY environment variable is not set`** — you set it in a different shell or session-scoped only. `$env:ANTHROPIC_API_KEY = "sk-ant-..."` for the current shell; persistent setter from §0.

**`Unable to create '.git/index.lock': File exists`** — stale git lock. `del .git\index.lock` from PowerShell (or `rm` on Mac/Linux).

**`error: cannot delete branch 'feat/...' used by worktree at ...`** — orphaned worktree metadata. `git worktree prune --expire=now -v` then retry the branch delete.

**Orchestrator exits 2 with "0 Sonnet specs"** — the `--specs` glob matched zero files OR the spec failed schema validation. Print the spec count: the runbook expects `dispatching N Sonnet(s)…` in stdout; if N is not what you expected, the glob is wrong.

**Orchestrator exits 2 fast with no spend, no cost-ledger** — pre-Patch-1/Patch-2 symptom (cache_control empty text block, or Sonnet without system prompt). If you're seeing this on the current code, either the patches were reverted or there's a new bug. Reach out before the next API spend.

**Sonnet response is markdown instead of JSON** — the cached corpus role frame regressed or the Sonnet model drifted. The defensive `ExtractJsonObject` in `SonnetDispatcher` should handle most cases; if it doesn't, paste the raw response text to the next Opus.

**Haiku response is SonnetResult-shaped instead of HaikuResult** — the tier-selection logic in `PromptCacheManager.BuildRequest` regressed (it picks the role frame based on `model.Name.Contains("haiku")`). Check that branch hasn't been removed.

**`FileNotFoundException` at `CachedPrefixSource` startup** — one of the files in `cached-corpus.manifest.json` is missing. Most often `docs/engine-fact-sheet.md` — regenerate per §3.
