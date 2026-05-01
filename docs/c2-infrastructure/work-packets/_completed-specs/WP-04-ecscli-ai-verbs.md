# WP-04 — ECSCli `ai` Verbs + Deterministic Seeding

**Tier:** Sonnet
**Depends on:** WP-03
**Timebox:** 90 minutes
**Budget:** $0.35

---

## Goal

Give the CLI a structured `ai` verb tree that an AI agent can drive, and ensure the simulation can be run deterministically from a seed plus a command log. This is Pillar A (the Eyes) of the SRD made real.

---

## Reference files

- `ECSCli/Program.cs`
- `ECSCli/CliOptions.cs`
- `APIFramework/Core/SimulationBootstrapper.cs`
- `APIFramework/Core/SimulationEngine.cs`
- `Warden.Telemetry/TelemetryProjector.cs` (from WP-03)
- `Warden.Telemetry/CommandDispatcher.cs` (from WP-03)

## Non-goals

- Do not change default CLI behaviour. Running `ECSCli` with no args must produce byte-identical output to the current behaviour.
- Do not migrate the existing flag parser to `System.CommandLine`. Keep the existing parser for top-level flags; introduce `System.CommandLine` **only** for the `ai` verb subtree. Interop happens in `Program.cs`.
- Do not invoke Anthropic from the CLI. The CLI is a lens, not a brain.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSCli/Ai/AiCommand.cs` | Root `Command` that registers the five subcommands. |
| code | `ECSCli/Ai/AiDescribeCommand.cs` | `ai describe --out <path>`. Emits the engine fact sheet markdown: every component type, every system registration, every `SimConfig` key with its type and current value. |
| code | `ECSCli/Ai/AiSnapshotCommand.cs` | `ai snapshot --out <path> [--pretty]`. Boots, runs one update, captures, projects, writes. |
| code | `ECSCli/Ai/AiStreamCommand.cs` | `ai stream --out <path> --interval <gs> [--duration <gs>]`. JSONL stream, line-flushed. |
| code | `ECSCli/Ai/AiInjectCommand.cs` | `ai inject --in <path>`. Loads an `AiCommandBatch`, dispatches, prints the `DispatchResult`. Exit code 0 on full apply, 3 on any rejection. |
| code | `ECSCli/Ai/AiReplayCommand.cs` | `ai replay --seed <n> [--commands <path>] --duration <gs> --out <path>`. Deterministic JSONL emission tied to seed. |
| code | `APIFramework/Core/SeededRandom.cs` | Deterministic RNG source. `public sealed class SeededRandom { ctor(int seed); public float NextFloat(); public int NextInt(int maxExclusive); ... }`. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Constructor accepts optional `int? seed` parameter. `SeededRandom` instance exposed as `public SeededRandom Random { get; }`. Internal default = 0 if not supplied. |
| code | `APIFramework/Systems/*.cs` — **only where needed** | Any system currently using `System.Random` directly must switch to `sim.Random`. If none exists, no changes. Document every file you touched in the completion note. |
| code | `ECSCli/Program.cs` (modified) | If `args[0] == "ai"`, delegate to `AiCommand.Root.InvokeAsync(args[1..])`. Otherwise existing behaviour unchanged. |
| code | `ECSCli.Tests/` — new project | Integration tests for each `ai` verb. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-04.md` | Completion note. Must list every `System.Random` call site you modified (empty list is fine). |

Update `ECSSimulation.sln` with `ECSCli.Tests`.

---

## Determinism test (the hard one)

Write a test that:

1. Runs `ECSCli ai replay --seed 42 --duration 3600 --out run-a.jsonl`.
2. Runs `ECSCli ai replay --seed 42 --duration 3600 --out run-b.jsonl`.
3. Asserts `run-a.jsonl` and `run-b.jsonl` are byte-for-byte identical after stripping the `capturedAt` field from each line.

If this test fails, the simulation contains nondeterminism and WP-04 is not done. Go find the source. Candidates: wall-clock time, `Guid.NewGuid()`, LINQ operations that rely on hash ordering, parallel loops. Fix the source; do not mask the symptom.

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `ECSCli` (no args) exit code matches pre-WP behaviour (0 on clean shutdown). | cli-exit-code |
| AT-02 | `ECSCli ai describe --out x.md` produces a non-empty markdown file covering all 19+ registered systems and every `SimConfig` key. | manual-review |
| AT-03 | `ECSCli ai snapshot --out x.json` produces JSON that validates against `world-state.schema.json`. | schema-validation |
| AT-04 | `ECSCli ai stream --out x.jsonl --interval 600 --duration 3600` produces at least 6 lines, each validating against `world-state.schema.json`. | schema-validation |
| AT-05 | `ECSCli ai inject --in valid-batch.json` returns exit code 0; `--in invalid-batch.json` returns exit code 3. | cli-exit-code |
| AT-06 | Deterministic replay test (see above) passes. | unit-test |
| AT-07 | `--help` at every level of the `ai` subtree prints usage. | cli-exit-code |
| AT-08 | `dotnet test ECSSimulation.sln` passes every existing test (no regression). | unit-test |
