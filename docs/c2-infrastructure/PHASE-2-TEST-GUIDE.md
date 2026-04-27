# Phase 2 — Comprehensive Test Guide

> Authored by Opus at Phase 2 closure. Mirrors `PHASE-1-TEST-GUIDE.md`; updated for Phase 2 surfaces. Run on a fresh clone or after a cold pull from staging to confirm the engine is healthy before Phase 3 work begins.

---

## 0. Prerequisites

```powershell
dotnet --version          # expect 8.0.x
git --version             # any recent
echo $env:ANTHROPIC_API_KEY.Length    # expect ~108 if running real-API steps
```

API key sanity ping:

```powershell
$h = @{ "x-api-key"=$env:ANTHROPIC_API_KEY; "anthropic-version"="2023-06-01"; "content-type"="application/json" }
$b = '{"model":"claude-sonnet-4-6","max_tokens":10,"messages":[{"role":"user","content":"ping"}]}'
Invoke-RestMethod -Uri "https://api.anthropic.com/v1/messages" -Method Post -Headers $h -Body $b
```

---

## 1. Build the solution

```powershell
cd C:\repos\_ecs-simulation-engine
dotnet restore ECSSimulation.sln
dotnet build ECSSimulation.sln -c Release
```

**Expected:** all 13 projects build, **0 warnings, 0 errors**, wall-clock 30–90s.

---

## 2. Run the unit test suite

```powershell
dotnet test ECSSimulation.sln -c Release --logger "console;verbosity=normal"
```

**Expected:** every project passes, **~1,036 total tests**, 0 failures, 0 skipped. Wall-clock 5–15 minutes (the Warden.Orchestrator suite has long batch-poll integration tests).

Per-project rough counts (will drift slightly as Phase 3 lands):

| Project | Approx test count |
|:---|---:|
| `APIFramework.Tests` | ~750 (largest — every system, every component, every determinism test, every integration) |
| `Warden.Orchestrator.Tests` | ~136 |
| `Warden.Contracts.Tests` | ~66 |
| `Warden.Telemetry.Tests` | ~48 |
| `ECSCli.Tests` | ~19 (includes the new `FactSheetStalenessTests` from WP-2.9.A) |
| `Warden.Anthropic.Tests` | ~17 |

**No filter exclusions are needed.** The pre-existing `RunCommandEndToEndTests.AT01_MockRun*` flake from Phase 1's §6 backlog was resolved by WP-2.0.B's CostLedger fix + WP-2.0.C's BatchScheduler dedup fix. The full suite is green.

If the `FactSheet_IsCurrent_ForCurrentEngineState` test fails, the engine grew systems and the fact sheet wasn't regenerated. The failure message tells you the exact command:

```powershell
dotnet run --project ECSCli -- ai describe --out docs/engine-fact-sheet.md
```

Then commit the result. This is by design — WP-2.9.A's CI-ish staleness check.

---

## 3. Verify the engine fact sheet is current

```powershell
dotnet run --project ECSCli -- ai describe --out docs/engine-fact-sheet.md
git diff docs/engine-fact-sheet.md
```

**Expected:** `git diff` shows **no changes** if the fact sheet is current. If it shows changes, commit them — the engine grew systems since the last regeneration.

The fact sheet should list **48 systems and ~147 component types** at Phase 2 close, including all Phase 2 additions:

- Cognition phase (new): `ActionSelectionSystem`, `PhysiologyGateSystem`
- Cleanup phase: `StressSystem`, `WorkloadSystem`, `MaskCrackSystem`
- PreUpdate phase: `StressInitializerSystem`, `MaskInitializerSystem`, `WorkloadInitializerSystem`, `TaskGeneratorSystem`, `ScheduleSpawnerSystem`
- Condition phase: `ScheduleSystem`
- Off-phase event subscriber: `MemoryRecordingSystem`

---

## 4. Snapshot the engine to JSON

```powershell
dotnet run --project ECSCli -- ai snapshot --out world.json --pretty
```

**Expected:** `world.json` with `"schemaVersion": "0.4.0"`. Phase 2 did not bump the schema; the existing v0.4 surfaces were populated.

The snapshot includes the new Phase 2 fields populated by the projector:

- `entities[].social.intendedAction` (engine-internal as of v0.4 — may be promoted to wire format at next bump)
- `relationships[].historyEventIds[]` (now populated by `MemoryRecordingSystem`)
- `worldState.memoryEvents[]` (now populated)

---

## 5. Boot from world definition + run a stream

```powershell
dotnet run --project ECSCli -- ai snapshot `
  --world-definition docs/c2-content/world-definitions/office-starter.json `
  --out world-with-cast.json --pretty
```

**Expected:** the snapshot includes ≥10 NPCs spawned by the cast generator (WP-1.8.A) with WP-2.7.A's name pool — every NPC has `IdentityComponent.Name` populated. Verify by skimming `world-with-cast.json` for names from `cast/name-pool.json` (Donna, Greg, Frank, etc. should appear plausibly).

Each NPC also has all Phase 2 components attached: `ScheduleComponent`, `WorkloadComponent`, `StressComponent`, `SocialMaskComponent`. The initializer systems run at PreUpdate before any tick advances.

```powershell
dotnet run --project ECSCli -- ai stream `
  --world-definition docs/c2-content/world-definitions/office-starter.json `
  --interval 600 --duration 3600 --out world.jsonl
```

**Expected:** 6 JSONL frames over 1 game-hour. Tail and inspect:

```powershell
Get-Content world.jsonl -Tail 1 | ConvertFrom-Json | Select-Object schemaVersion, tick
```

---

## 6. The orchestrator smoke mission (real-API)

```powershell
dotnet run --project Warden.Orchestrator -- run `
  --mission examples/smoke-mission.md `
  --specs "examples/smoke-specs/spec-smoke-01.json" `
  --budget-usd 1.00
```

**Expected:** exit 0, wall-clock 2–6 minutes. Spend ~$0.02 if cache-warm, ~$0.14 if cache-cold.

Verify the report:

```powershell
$run = Get-ChildItem runs/ | Sort LastWriteTime -Desc | Select -First 1
Get-Content "$($run.FullName)/report.md"
```

**Phase 2 baseline expectations** (the post-closure orchestrator fixes hold):

- `Total spend`: between $0.02 and $0.20
- Tier 2 table: one Sonnet row, `outcome: ok`, `1/1` ATs passed
- **Tier 3 table populated** (no longer "No Haiku scenarios run") — shows `sc-01` with assertion count, duration, spend
- Aggregate key metrics rendered (entitiesActive, systemsRun, ticksExecuted, worldObjectsPresent)

Cost ledger:

```powershell
Get-Content "$($run.FullName)/cost-ledger.jsonl"
```

Two lines, both with **non-zero token counts**. The Haiku entry should show real `inputTokens`, `cachedReadTokens`, `outputTokens`, and `usdTotal` matching what Anthropic Console shows.

The persisted `haiku-01/result.json` `parentBatchId` should be the Sonnet-side batch id (matches `^batch-[a-z0-9-]{1,48}$`), not an Anthropic `msgbatch_*` id.

---

## 7. Mock-mode pipeline test

```powershell
dotnet run --project Warden.Orchestrator -- run `
  --mission examples/smoke-mission.md `
  --specs "examples/smoke-specs/spec-smoke-01.json" `
  --mock-anthropic
```

**Expected:** exit 0, wall-clock under 5 seconds. Free.

---

## 8. Cast-validate mission (now functional)

```powershell
dotnet run --project Warden.Orchestrator -- run `
  --mission examples/smoke-mission-cast-validate.md `
  --specs "examples/smoke-specs/cast-validate.json" `
  --budget-usd 2.00
```

**Phase 1 expectation was exit 2 (blocked).** **Phase 2 expectation is exit 0 (ok)** — WP-2.0.A's inline-files mode pre-reads the spec's `inputs.referenceFiles[]` and prepends them to the user turn. The Sonnet sees `office-starter.json` and `archetypes.json` content inline; produces a real validation result.

Cost ~$0.40 (Sonnet + Haiku batch, cache-cold).

---

## 9. The CLI verbs

Each runs in seconds, no API spend.

```powershell
dotnet run --project ECSCli -- ai describe --out tmp-fact.md
dotnet run --project ECSCli -- ai snapshot --out tmp-world.json --pretty
dotnet run --project ECSCli -- ai stream --interval 600 --duration 600 --out tmp-stream.jsonl
dotnet run --project ECSCli -- ai narrative-stream --interval 600 --duration 1800 --out tmp-narrative.jsonl
dotnet run --project ECSCli -- ai replay --seed 42 --duration 3600 --out tmp-replay.jsonl
```

The `narrative-stream` output should now include Phase 2's new event kinds when conditions trigger them: `MaskSlip`, `OverdueTask`, `TaskCompleted`. Empty streams are valid (the world bible's "most exchanges are passive and forgettable" — narrative events are sparse by design).

---

## 10. What "Phase 2 green" means

Pass all of the following and you can sign off on Phase 2:

- §1 build: 0 warnings, 0 errors.
- §2 unit tests: every project passes, **0 failures, 0 skipped, no filter exclusions**. ~1,036 total tests.
- §3 fact sheet: regenerated, lists 48 systems including all Phase 2 additions; `git diff` clean after regeneration.
- §4–5 snapshots: emit `schemaVersion 0.4.0`; cast NPCs have names; relationship history and memory events populate.
- §6 orchestrator smoke run (real-API): exit 0, **both Sonnet and Haiku ledger entries non-zero**, Tier 3 table populated, aggregate key metrics rendered.
- §7 mock-mode pipeline test: exit 0, free, structurally identical report.
- **§8 cast-validate: exit 0** (was exit 2 in Phase 1; WP-2.0.A unblocked it).
- §9 CLI verbs: all five write their output files and exit 0.

If all ten pass, the engine is in the state Phase 3 starts from.

---

## 11. Known issues at Phase 2 close

**None blocking Phase 3.** The Phase 1 §10 punch list closed cleanly:

| Phase 1 issue | Phase 2 resolution |
|:---|:---|
| Aggregator missing Tier 3 | Resolved (post-closure pass — `BatchScheduler.ParseSucceeded` stamps `ParentBatchId`) |
| Haiku ledger 0 tokens | Resolved (same fix — `TokensUsed` from `succeeded.Message.Usage`) |
| Cast-validate blocked | Resolved (WP-2.0.A inline-files mode) |
| `RunCommandEndToEndTests.AT01_MockRun*` flake | Resolved (WP-2.0.B mutex + WP-2.0.C dedup) |
| Engine fact sheet auto-regeneration | Resolved (WP-2.9.A staleness test) |
| `PromptCacheManager` parameterless overuse | Resolved Phase 1 (no regression) |
| `FailClosedEscalator` switch incompleteness | Resolved Phase 1 (no regression) |

Carry-forward items for Phase 3:

- **The `Entity._components: Dictionary<Type, object>` boxing issue.** Documented in `Entity.cs` as accepted for v0.7.x with the fix deferred. At 30+ NPCs in Unity, GC pressure produces frame hitches. Fix is WP-3.0.5 (`ComponentStore<T>` typed arrays).
- **Animation hint surface design.** Open question whether the engine emits `IntendedAnimationHint` (Sneezing, Tripping) or Unity derives from observed state changes. To be answered when Phase 3.1.B begins.
- **`Persistent` flag nuances.** Should magnitude affect persistence? Per-archetype biases? Decay over game-time? Defer to playtest.

---

## 12. Troubleshooting

**`ANTHROPIC_API_KEY environment variable is not set`** — `$env:ANTHROPIC_API_KEY = "sk-ant-..."` for the current shell.

**Stale `.git/index.lock`** — `del .git\index.lock` from PowerShell.

**`error: cannot delete branch 'feat/...'`** — `git worktree prune --expire=now -v` then retry.

**FactSheet test fails** — engine grew systems; regenerate per §3 and commit.

**Orchestrator exits 2 fast with no spend** — pre-Patch-1/Patch-2 symptom (cache_control empty text block). If you're seeing this on current code, those patches were reverted; restore from staging.

**Sonnet response is markdown instead of JSON** — defensive `ExtractJsonObject` should handle most drift; if it doesn't, paste the raw response to the next Opus.

**Composite `custom_id` exceeds 64 chars** — a Sonnet emitted a too-long `scenarioBatch.batchId`. WP-2.0.C validates this at submission time; the failure is a clean diagnostic exception. Shorten the batch id in the role frame or the spec.
