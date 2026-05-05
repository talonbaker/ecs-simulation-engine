# SRD — C2 Infrastructure for the 1-5-25 Claude Army

**Project:** ECS Simulation Engine (headless C# / .NET 8)
**Phase:** 0 — Infrastructure
**Author role:** Principal Systems Architect, Tier-1 (Opus)
**Consumers:** Tier-2 Sonnet engineering agents
**Status:** Baseline — awaiting implementation

---

## 0. What this document is, and is not

This is the Systems Requirement Document for the Command & Control infrastructure that will host the 1-5-25 workflow against the existing ECS engine. It defines **what** must be built, **why** it must exist, and the **contracts** that prevent the tiers from drifting apart.

It is not a design document for game mechanics. It is not an implementation guide. Implementation lives in the Work Packets (`work-packets/WP-01 … WP-12`). This document is the frame the packets hang on.

---

## 1. Strategic goals and how the architecture serves them

| Goal (as stated) | Architectural consequence |
|:---|:---|
| **G1 — C# first, Anthropic via API with a key.** | The orchestrator is a .NET 8 console app. It talks to `api.anthropic.com` over HTTPS with a custom thin `Warden.Anthropic` client. No external CLI invocation, no subprocess hops. |
| **G2 — Cheapest possible calls.** | Three independent cost levers, all mandatory: aggressive **prompt caching** of the static engine docs; **Message Batches API** for every Tier-3 Haiku job (50% discount); **fail-closed** workers that never recurse, never ask for help, never burn tokens debugging themselves. |
| **G3 — Agentic, walk-away-safe.** | The orchestrator is idempotent and resumable. Chain-of-thought persists to disk. Any worker crash is a logged outcome, not a cascade. You can kick it off, close your laptop, come back later. |
| **G4 — 1-5-25 tier topology.** | Three distinct tier contracts (Opus→Sonnet spec, Sonnet→Haiku scenario, Haiku→parent result), each backed by a strict JSON Schema. No tier can emit data another tier cannot parse. |
| **G5 — Readable reports before/during/after.** | Every run produces a single Markdown report plus structured JSON. The report is designed to be skimmed in 90 seconds. Detailed data is linked, not inlined. |

Every technical choice below traces back to one or more of these goals. If you find a requirement that does not, flag it for removal.

---

## 2. The four pillars

### Pillar A — AI Telemetry (the Eyes)

**What it is.** A deterministic, versioned JSON representation of the live simulation world, plus a CLI surface that lets a non-Claude process load, snapshot, stream, and mutate that world.

**Why it has to be separate from the existing `SimulationSnapshot`.** The engine's `SimulationSnapshot` already exists and is correctly scoped for UI frontends (`ECSVisualizer` reads it). It is a **C# type**, not a wire format. If we teach it to serialise itself, we couple the engine's internal record shape to the AI's contract. The moment the engine refactors a field, every Haiku run breaks. Instead, we introduce a **telemetry DTO** in a separate project (`Warden.Telemetry`) that **projects** `SimulationSnapshot` into a versioned wire format. Two types, two reasons to change, two owners.

**The CLI additions.**

```
ECSCli ai snapshot --out world.json [--pretty]
    Captures a single SimulationSnapshot, projects it to the telemetry DTO,
    serialises to JSON. Exit code 0 on success, 2 if invariants violated.

ECSCli ai stream  --interval <gs> --out world.jsonl [--duration <gs>]
    Emits one JSON-line-per-tick (JSONL) telemetry frame. Designed for a
    Haiku agent to tail while the sim runs. No pretty-print. Flushed every line.

ECSCli ai inject  --in commands.json
    Applies an AICommandBatch (see schemas/) to the live engine BEFORE the
    next tick. Only mutations whitelisted in the schema are accepted.
    Everything else fails closed with exit code 3 and a structured error.

ECSCli ai replay  --seed <n> --commands commands.jsonl --duration <gs> --out report.json
    Deterministic replay. Given a seed and a timestamped command log, the
    engine must produce byte-identical telemetry. This is how Haikus
    reproduce each other's findings without re-running wall-time simulations.

ECSCli ai describe --out engine-fact-sheet.md
    Dumps the engine's structural fact sheet: every component type, every
    system, every phase, every SimConfig key and its type. This file is
    what gets prompt-cached across all Sonnet and Haiku calls.
```

Notice what is **not** there: no `ai chat`, no direct API hook inside the CLI. The CLI is deliberately a dumb lens. The intelligence lives in the orchestrator.

**Determinism contract.** The engine must be seedable. Given the same `--seed`, the same `SimConfig.json`, and the same command log, two runs must produce telemetry streams that diff clean. If any system uses `System.Random` without going through a seeded RNG source, that is a bug the infrastructure phase fixes (see `WP-04`).

**Versioning.** The telemetry DTO is stamped with `schemaVersion: "0.1.0"`. Breaking changes bump the major version. The Sonnet and Haiku prompts are told which version they're on. An orchestrator refuses to consume telemetry whose major version does not match its contract.

### Pillar B — The C# Orchestrator (the Brain)

**What it is.** `Warden.Orchestrator` — a standalone .NET 8 console application that runs the entire 1-5-25 workflow. It is the only process that holds the Anthropic key. It is the only process that issues API calls. It is the only process that persists chain-of-thought.

**Core responsibilities, in required order of execution:**

1. **Authenticate and boot.** Load the key from env var `ANTHROPIC_API_KEY` (never a file on disk). Refuse to start if missing.
2. **Load the mission.** Read the top-level vision brief (either piped stdin, `--mission mission.md`, or a queued mission from `./missions/`).
3. **Brief the Sonnets.** Produce between one and five Opus→Sonnet spec packets (Opus does this; the orchestrator's job is dispatch, not strategy). Persist each spec with a deterministic id.
4. **Dispatch concurrently.** `Task.WhenAll` the Sonnet calls through a `SemaphoreSlim(max: 5)`. Sonnets write their diffs to isolated git worktrees under `./runs/<runId>/sonnet-<n>/worktree/`.
5. **Receive Sonnet outputs.** Each Sonnet emits a `SonnetResult` JSON. The orchestrator validates against the schema, persists, and either forwards to Haiku dispatch or halts per the fail-closed policy.
6. **Dispatch Haikus.** For each Sonnet result that asks for balance validation, produce a `HaikuScenarioBatch` (up to 25 scenarios) and submit through the Message Batches API. Poll every 60s. Do not long-live a thread-per-worker.
7. **Aggregate.** Roll Haiku results up to the owning Sonnet, Sonnets up to the mission. Write the mission report.
8. **Ledger.** Every API request/response updates the cost ledger. Running spend and projected spend are visible in the live run console and the final report.

**Concurrency model.**

- Sonnet tier: `SemaphoreSlim(initialCount: 5, maxCount: 5)` wrapping `Task.WhenAll`. Five is the hard cap even if Opus briefs fewer, to make headroom predictable.
- Haiku tier: **does not** run interactively. All 25 go through a single Batch API submission. The orchestrator holds zero open HTTP connections while batches process. It wakes on a timer and polls for results.
- Retry: Polly policy — three retries, exponential backoff starting at 2s, on 429 and 5xx only. Never retry a 400-class error. Never retry tool-use errors; those escalate.
- Cancellation: one top-level `CancellationTokenSource`. Ctrl-C cancels all in-flight requests, flushes the ledger, and writes a partial report before exit.

**Chain-of-thought persistence.** Every tier call records, atomically:

```
./runs/<runId>/
  mission.md                      (Opus's original brief)
  cost-ledger.jsonl               (one line per API request)
  sonnet-<n>/
    spec.json                     (Opus→Sonnet spec)
    prompt.txt                    (final assembled prompt with cache markers)
    response.json                 (raw Anthropic response)
    result.json                   (validated SonnetResult)
    worktree/                     (git worktree containing the diff)
    haiku-batch.json              (the scenario batch this Sonnet spawned)
    haiku-<m>/
      scenario.json
      result.json
      telemetry.jsonl
  report.md                       (final human-readable report)
  report.json                     (machine-readable mirror of report.md)
```

This structure is itself part of the contract. A later Opus call can be handed the entire `./runs/<runId>/` directory and reconstruct the full thought process. Nothing lives only in memory.

### Pillar C — The Intelligence Handshake

**What it is.** The set of strict JSON Schemas that govern every cross-tier message. If a message does not validate, the receiving tier refuses to proceed. Malformed output is the single biggest source of token waste in multi-agent systems; schemas eliminate it.

**Four schemas, four contracts:**

1. `world-state.schema.json` — The telemetry DTO (Pillar A output).
2. `opus-to-sonnet.schema.json` — A SpecPacket. One engineering objective, typed inputs, typed outputs, explicit acceptance tests, explicit non-goals.
3. `sonnet-to-haiku.schema.json` — A ScenarioBatch. N scenarios (1 ≤ N ≤ 25), each with a seed, a `SimConfig` delta, an optional command log, and a list of assertions the Haiku must evaluate.
4. `haiku-result.schema.json` — A Haiku's verdict: passed/failed assertions, key metrics, telemetry digest, a free-form note capped at 2,000 characters.

**Schema discipline rules the implementation must enforce:**

- `additionalProperties: false` on every object.
- No free-form strings where an enum will do (dominant drive, outcome code, etc.).
- Every numeric field has `minimum`/`maximum` or is called out as unbounded with a rationale.
- Every array has a `maxItems` — no unbounded lists ever, because an unbounded list is an unbounded prompt.
- Every schema carries a top-level `schemaVersion` string. Version is part of the validation.

The schemas live in `docs/c2-infrastructure/schemas/` and are compiled into C# record types via `WP-02`. A Sonnet agent that "forgets" the schema has its response rejected at parse time — it cannot silently drift.

### Pillar D — ROI Optimisation

**What it is.** A multi-pronged cost strategy that makes the 1-5-25 workflow financially boring. Every pronged technique below is mandatory; this is not an a-la-carte menu.

**D.1 — Prompt Caching.**

The static context that every Sonnet and every Haiku needs — engine fact sheet, ECS architecture guide, engineering guide, all four JSON schemas, current `SimConfig.json` — runs to ~25–40k tokens. Sent as plain input on every call, that is the single biggest line item in the budget. Sent once as a cached prefix, it costs 125% on the first call of a 5-minute window and 10% on every subsequent call.

The rule is:

```
System prompt         = [ role framing                ]   (uncached)
System prompt (cont.) = [ engine fact sheet + docs    ]   (cache_control: ephemeral)
System prompt (cont.) = [ mission-specific context    ]   (cache_control: ephemeral)
User turn             = [ this tier's specific task   ]   (uncached, varies per call)
```

The orchestrator assembles prompts from four distinct "slabs" that are concatenated at call time. Slabs 1–3 carry `cache_control` markers. Slab 4 is the only variable per request. This is what keeps the Sonnet fan-out cheap: five Sonnets issued within five minutes share the cache write of slab 1 and pay read prices.

The 1-hour beta TTL is used for missions expected to run longer than five minutes from first dispatch (the common case for Haiku batching, where poll cycles often exceed five minutes). The orchestrator automatically picks TTL based on projected batch latency.

**D.2 — Message Batches API for Tier-3.**

All Haiku calls go through the batches endpoint. 50% discount on input and output, up to 24h turnaround, 100,000 requests per batch. The orchestrator never interactively invokes Haiku. If a Haiku job is so urgent that it cannot wait 5–15 minutes, that is a design smell that gets escalated, not patched by bypassing the batch API.

**D.3 — Fail-Closed Workers.**

Workers never recurse. Workers never spawn helpers. Workers never attempt to fix their own environment. If a Sonnet cannot proceed (missing file, failed build, ambiguous spec, tool error, unexpected exception, schema mismatch on its own output), it emits a `result.json` with `outcome: "blocked"`, a structured reason code, and exits. The orchestrator is the only thing allowed to decide what to do about a block. This is worth saying plainly: the cheapest token is the one never spent.

**D.4 — Model Selection Discipline.**

| Tier | Model | Why this one |
|:---|:---|:---|
| 1 (General) | `claude-opus-4-6` | You (the human) invoke it. Highest reasoning cost, lowest call volume. One call per mission. |
| 2 (Engineer) | `claude-sonnet-4-6` | Best code-editing model at its price point. Five calls per mission. Cached prefix keeps unit cost low. |
| 3 (Grunt) | `claude-haiku-4-5-20251001` | Fast, cheap, batch-friendly. Twenty-five calls per mission. Batched + cached puts them at rounding-error cost. |

No tier is allowed to "upgrade itself" by calling a more expensive model. The orchestrator enforces model per tier at the client layer.

**D.5 — Cost Ledger with Budgets.**

Every request logs a `LedgerEntry`:

```json
{
  "runId": "2026-04-23T1423-mission01",
  "tier": "sonnet",
  "workerId": "sonnet-02",
  "model": "claude-sonnet-4-6",
  "inputTokens":       1240,
  "cachedReadTokens": 32180,
  "cacheWriteTokens":     0,
  "outputTokens":       720,
  "usdInput":   0.00372,
  "usdCachedRead": 0.00966,
  "usdCacheWrite": 0.0,
  "usdOutput":  0.01080,
  "usdTotal":   0.02418,
  "timestamp": "2026-04-23T14:23:07Z"
}
```

The orchestrator takes a `--budget-usd <n>` flag. When running spend exceeds the budget, further dispatch halts and the orchestrator writes a `budget-exceeded` report. This is another fail-closed surface: you can hand it a $5 ceiling and walk away.

**See `02-cost-model.md`** for the full math, including worked examples of a typical mission cost with and without each lever.

---

## 3. Topology reference

```
                         +-------------------------+
                         |  Tier 1 — Opus (human-  |
                         |  invoked, outside loop) |
                         +------------+------------+
                                      | mission.md
                                      ▼
                         +-------------------------+
                         |  Warden.Orchestrator    |
                         |  (C# .NET 8 console)    |
                         |                         |
                         |  - PromptCacheManager   |
                         |  - BatchScheduler       |
                         |  - ConcurrencyController|
                         |  - CostLedger           |
                         |  - FailClosedEscalator  |
                         |  - ChainOfThoughtStore  |
                         |  - ReportAggregator     |
                         +---+-----------------+---+
                             |                 |
          5× parallel        |                 |       25× batched
          +------------------+                 +------------------+
          ▼                                                       ▼
  +------------------+                                 +------------------+
  | Tier 2 — Sonnet  |                                 | Tier 3 — Haiku   |
  | (Engineer)       |                                 | (Grunt / Tester) |
  |                  |                                 |                  |
  | reads: SpecPacket|                                 | reads: Scenario  |
  | edits: worktree  |                                 | invokes: ECSCli  |
  | writes: Result   |                                 | writes: Result   |
  +--------+---------+                                 +--------+---------+
           |                                                    |
           | diff + tests                                       | pass/fail + metrics
           ▼                                                    ▼
        +----------------------------------------------------------+
        |               ECSCli + APIFramework                       |
        |          (the simulation, unchanged in Phase 0            |
        |           except for new `ai` verbs)                      |
        +----------------------------------------------------------+
```

Full diagram with data flows in `01-architecture-diagram.md`.

---

## 4. Cross-cutting policies

### 4.1 Fail-closed escalation (restated with teeth)

Every worker conforms to this state machine:

```
   [running] --success--► [done: emit result.outcome="ok"]
       |
       +--ambiguity--► [done: outcome="blocked", reason="ambiguous-spec"]
       +--build fail-► [done: outcome="blocked", reason="build-failed"]
       +--test fail--► [done: outcome="failed", reason="tests-red"]
       +--tool error-► [done: outcome="blocked", reason="tool-error"]
       +--exception--► [done: outcome="blocked", reason="exception"]
```

`blocked` and `failed` both halt downstream dispatch for that branch. Neither retries. Both carry enough context (logs, stderr, the exact failing assertion) that a human or a follow-up Opus brief can act on them without asking questions.

What is explicitly banned:

- A Sonnet reading its own stderr and "trying again."
- A Haiku tweaking `SimConfig.json` to make its scenario pass.
- Any tier invoking the orchestrator or another tier directly.
- Any tier spawning subagents of its own.

### 4.2 Determinism

Every Haiku run is deterministic given (seed, config, commands). This matters because:

- It lets the orchestrator dedupe identical scenarios across batches (massive cost saving).
- It lets a human rerun any Haiku result locally and see exactly what it saw.
- It makes differential balancing possible (tweak one config value, rerun 25 Haikus, diff the results).

### 4.3 Idempotency

The orchestrator can be killed mid-run and restarted with `--resume <runId>`. On resume it inspects `./runs/<runId>/` and only dispatches work that has no persisted result. This is the laptop-lid-close guarantee.

### 4.4 Observability

Every run emits:

- `report.md` — human-readable summary.
- `report.json` — same data, machine-parseable.
- `cost-ledger.jsonl` — append-only, one record per API call.
- `events.jsonl` — append-only, one record per state transition (worker started, worker finished, batch submitted, batch completed, budget warning, etc.).

No metric lives only in stdout. Stdout is for humans; files are the record of truth.

---

## 5. What is explicitly out of scope for Phase 0

- Any component, system, or config value that is not already in the engine. We do not author simulation features here.
- Self-healing, self-optimising, or learned-from-prior-runs orchestration. All policy is static and declared.
- A GUI for the orchestrator. The Avalonia visualizer already exists for the engine; the orchestrator is a CLI.
- Rate limit accounting beyond Polly retries. We assume Anthropic's published limits are the ceiling; if we hit them we back off and keep going.
- Support for models other than the three named in D.4.
- Streaming responses. The orchestrator uses non-streaming `messages.create` and batch APIs exclusively. Streaming adds complexity for a UX we don't need.

These become candidate Phase-1 items, not Phase-0 surprises.

---

## 6. Acceptance criteria for Phase 0 as a whole

Phase 0 is "done" when all of the following are true:

1. `dotnet build ECSSimulation.sln` succeeds.
2. `dotnet test ECSSimulation.sln` passes, including new test projects added by `WP-01` and `WP-09`.
3. `ECSCli ai describe`, `ai snapshot`, `ai inject`, `ai stream`, and `ai replay` each run successfully against the current engine.
4. `Warden.Orchestrator run --mission ./examples/smoke-mission.md --mock-anthropic` completes without any real API call and produces a valid `report.md` + `report.json` + `cost-ledger.jsonl`.
5. `Warden.Orchestrator run --mission ./examples/smoke-mission.md` against the real API, with `--budget-usd 1.00`, completes one Sonnet call and one 5-Haiku batch, produces a valid report, and spends under $1.00 (real-world smoke number; recorded in the ledger).
6. Every Work Packet has a corresponding completion note in `docs/c2-infrastructure/work-packets/_completed/WP-NN.md`, written by the Sonnet that executed it.

Until all six hold, Phase 1 does not start.

---

## 7. How a Sonnet picks up work from here

See `prompts/opus-sonnet-bootstrap.md` for the exact bootstrap prompt. In summary:

1. You (the human) cue a Sonnet with: "Execute `WP-XX` from `docs/c2-infrastructure/work-packets/`. Read the SRD and the named packet. Do not read other packets unless this one references them."
2. The Sonnet reads exactly those documents, plus whatever source files the packet points to.
3. The Sonnet writes code, tests, and its completion note.
4. If it cannot proceed, it emits `blocked` and stops. It does not message you. It does not retry.
5. You review, clear the block if needed, and cue it again or move on.

That loop is what makes this walk-away-safe.

---

## 8. Architectural axioms beyond Phase 0

These are forward-looking commitments that bind every packet from Phase 1 on. They are recorded here, not in a packet, because they constrain the *shape* of every future packet. See also `SCHEMA-ROADMAP.md` for the versioned plan that operationalises them.

### 8.1 Anthropic API is a design-time tool only

Sonnet and Haiku calls happen during *content generation* — building catalogs, authoring characters, validating balance, generating dialogue templates. They do **not** happen during the player's gameplay session. The runtime engine carries no dependency on `api.anthropic.com`, `Warden.Anthropic`, or any LLM. NPC behaviour at runtime is template-driven and deterministic, with the templates produced by Sonnets at build time.

This is the axiom that makes the game offline-playable, fast, and free at the point of play. It is also the axiom that allows the orchestrator to remain a build-time tool, never a runtime dependency. Any future packet that proposes a runtime LLM call is rejected on architectural grounds.

### 8.2 Save/load reuses WorldStateDto

The player's save game is a serialised `WorldStateDto` at the current tick, plus the persistent-event chronicle (v0.3 and beyond — see `SCHEMA-ROADMAP.md`). There is no second save format. The format introduced by `WP-03` *is* the format the player's save file uses. Every future schema bump automatically extends the save format, with the same versioning rules.

This is **not** the same as `ECSCli ai replay`, which is for deterministic test scenarios on bounded inputs. Replay tests determinism; save/load preserves a player's world. Two concepts, one storage format.

### 8.3 Memory model: per-pair primary, global thin

NPC memory is stored on the *relationship entity* between the two participants ("Donna remembers Frank insulted her" lives on the Donna↔Frank relationship). For events the whole office knows about ("Kevin's chili"), a thin global chronicle (v0.3 in `SCHEMA-ROADMAP.md`) carries the shared memory, and per-NPC memory entries reference it by id rather than duplicating its body. This avoids both per-NPC memory bloat and global-chronicle write-amplification.

### 8.4 Charm is curated, not simulated

The 30-year-office feel is *authored* into the world at game start: dust, stacked boxes, the cubicle of dead monitors, the parking-lot "no fighting" sign. The engine does not simulate dust accumulation over months. It ships with dust already there. A small subset of in-game events do persist (spilled coffee → stain that stays, NPC argument → ongoing relationship rift), and those events flow through the v0.3 chronicle channel.

This axiom keeps the engine cheap to run, the content tractable to author, and the gameplay pacing in the hands of the player rather than an entropy clock.

### 8.5 Social state is a first-class component family

The eventual gameplay is Sims/Rimworld/Prison Architect territory: drives, relationships, memories, schedules, romance, office politics. Social state will be the *largest* live component family in the engine, not an afterthought layered on top of physiology. Phase-0 schemas reserve no fields for it (intentional — v0.1 contracts only what exists today), but the v0.2 minor bump in `SCHEMA-ROADMAP.md` is the first move on this axis.

### 8.6 The visual target is 2.5D top-down management sim

The player is a ghost-camera, not a character. They pan, zoom, and follow workers around an office floor. The headless engine doesn't render, but it must expose enough spatial structure for the AI tier to reason about *places*, not just float coordinates. The v0.5 room-overlay bump is the first move on this axis.
