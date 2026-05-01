# Naming Conventions — C2 Infrastructure

**Purpose.** Keep the new projects distinguishable from the existing engine, and make namespace ownership obvious at a glance. The engine is the machine. The C2 layer is the thing that points the machine. They should never look like the same codebase.

---

## 1. Umbrella name: Warden

Every new project and every new namespace lives under `Warden.*`. Rationale: the engine projects use no prefix (`APIFramework`, `ECSCli`, `ECSVisualizer`); a dedicated prefix makes "is this part of the sim, or the C2 layer?" a one-glance decision.

Do not propose alternative umbrella names without updating every document in this folder. The umbrella is now a load-bearing identifier.

---

## 2. Project layout

```
ECSSimulation.sln                                   (existing)
│
├── APIFramework/                                   (existing; do not modify in Phase 0
├── APIFramework.Tests/                              except as explicitly named in WPs)
├── ECSCli/
├── ECSVisualizer/
│
├── Warden.Contracts/                               (new — WP-02)
│     ├── Telemetry/WorldStateDto.cs                    serialisable DTOs mirroring the
│     ├── Telemetry/EntityStateDto.cs                   `SimulationSnapshot` record tree
│     ├── Handshake/OpusSpecPacket.cs                   Opus → Sonnet contract
│     ├── Handshake/SonnetResult.cs                     Sonnet → orchestrator contract
│     ├── Handshake/ScenarioBatch.cs                    Sonnet → Haiku contract (≤25 scenarios)
│     ├── Handshake/HaikuResult.cs                      Haiku → orchestrator contract
│     ├── Handshake/AiCommandBatch.cs                   orchestrator → ECSCli whitelisted mutations
│     ├── Handshake/OutcomeCode.cs                      the strict enum everyone uses
│     ├── Handshake/BlockReason.cs                      union of Sonnet/Haiku block reasons
│     ├── JsonOptions.cs                                canonical JsonSerializerOptions
│     ├── SchemaValidation/SchemaValidator.cs           loads schemas from embedded resources
│     └── *.schema.json                                 the JSON schemas, embedded
│
├── Warden.Telemetry/                               (new — WP-03)
│     ├── TelemetryProjector.cs                         SimulationSnapshot → WorldStateDto
│     ├── TelemetrySerializer.cs                        wire JSON writer (System.Text.Json)
│     └── CommandDispatcher.cs                          applies AICommandBatch to engine
│
├── Warden.Anthropic/                               (new — WP-05)
│     ├── AnthropicClient.cs                            thin Messages + Batches HTTP client
│     ├── CacheControl.cs                               ephemeral cache marker type
│     ├── CostRates.cs                                  per-model pricing constants
│     ├── ModelId.cs                                    the three allowed model ids, as a
│     │                                                 closed enum-like record
│     └── BatchJobHandle.cs                             typed wrapper over a batch id
│
├── Warden.Orchestrator/                            (new — WP-09)
│     ├── Program.cs                                    console entry point
│     ├── Dispatcher/ConcurrencyController.cs           SemaphoreSlim + Task.WhenAll
│     ├── Dispatcher/FailClosedEscalator.cs             enforces the state machine
│     ├── Cache/PromptCacheManager.cs                   assembles cached prefix
│     ├── Cache/PromptSlab.cs                           the four-slab prompt model
│     ├── Batch/BatchScheduler.cs                       submit + poll
│     ├── Persistence/ChainOfThoughtStore.cs            writes ./runs/<runId>/
│     ├── Persistence/CostLedger.cs                     writes cost-ledger.jsonl
│     ├── Reports/ReportAggregator.cs                   writes report.md + report.json
│     └── Mocks/MockAnthropic.cs                        offline test harness
│
├── Warden.Orchestrator.Tests/                      (new — WP-09)
│     └── (xunit tests for every orchestrator component)
│
├── Warden.Contracts.Tests/                         (new — WP-02)
│     └── (schema round-trip + validator tests)
│
├── Warden.Telemetry.Tests/                         (new — WP-03)
│     └── (projector + command-dispatcher tests)
│
├── Warden.Anthropic.Tests/                         (new — WP-05)
│     └── (HttpMessageHandler-stubbed client tests)
│
└── ECSCli.Tests/                                   (new — WP-04)
      └── (integration tests for `ai` verbs)
```

---

## 3. Namespaces

```
Warden.Contracts.Telemetry
Warden.Contracts.Handshake
Warden.Contracts.SchemaValidation
Warden.Telemetry
Warden.Anthropic
Warden.Orchestrator
Warden.Orchestrator.Dispatcher
Warden.Orchestrator.Cache
Warden.Orchestrator.Batch
Warden.Orchestrator.Persistence
Warden.Orchestrator.Reports
Warden.Orchestrator.Mocks
```

One namespace per folder, one public type per file (records and their associated DTOs may share a file when tightly coupled).

---

## 4. Project reference rules

The dependency graph is strict:

```
APIFramework           ── no new deps ──
ECSCli                 ── may reference Warden.Telemetry + Warden.Contracts
Warden.Contracts       ── depends on nothing except System.Text.Json
Warden.Telemetry       ── depends on APIFramework + Warden.Contracts
Warden.Anthropic       ── depends on Warden.Contracts (for model ids + cost rates)
Warden.Orchestrator    ── depends on Warden.Contracts + Warden.Anthropic
                          + Warden.Telemetry (#if WARDEN — ASCII map prompt injection)
                          NOT on APIFramework directly
```

**The orchestrator must not reference APIFramework directly.** Giving it a compile-time handle on `APIFramework` would create a temptation to reach in and manipulate simulation state directly — which would break determinism and the process-boundary story in `01-architecture-diagram.md`.

`Warden.Telemetry` is permitted as of WP-3.0.W.1. `Warden.Telemetry.AsciiMap.AsciiMapProjector` is a pure-function text renderer with no simulation-state side effects. The WARDEN-gated `MapSlabFactory` in `Warden.Orchestrator.Prompts` uses it to inject ASCII floor-plan context into Sonnet/Haiku prompts. This does not give the orchestrator a handle on live simulation state.

A Sonnet that wants to read live telemetry does so by running `ECSCli ai snapshot`, not by linking `APIFramework`.

---

## 5. File naming

- C# files: `PascalCase.cs`, one public type per file.
- JSON schemas: `kebab-case.schema.json`, matching the DTO's singular noun.
- Markdown: `kebab-case.md` in `docs/`, `WP-NN-kebab-case.md` for work packets.
- Run artifacts: `runs/<isoDate-slug>/` where slug is lower-kebab.

---

## 6. Type-naming micro-rules

- DTOs end in `Dto` when they are the wire-format twin of an internal type (`WorldStateDto` ↔ `SimulationSnapshot`).
- Internal records that happen to be serialised but are not wire contracts do **not** get `Dto`.
- Schemas match the DTO name in kebab-case: `WorldStateDto` ↔ `world-state.schema.json`.
- Result types end in `Result` (`SonnetResult`, `HaikuResult`), never `Response`. "Response" is an HTTP word and we reserve it for the Anthropic HTTP layer.
- Enums are closed. Use `public readonly record struct` with a private constructor and static factory members when you need more control than C# enums give.

---

## 7. What not to name things

- No `Manager` without a real state machine behind it. (`PromptCacheManager` passes — it owns a cache lifecycle. `JsonManager` does not pass.)
- No `Helper`, `Utility`, `Common`, `Misc`. A class that wants one of those names is a class that hasn't been decomposed yet.
- No `Warden.Core` either. "Core" is where undesigned grab-bags go to die. Everything has a home; this document is that home.
