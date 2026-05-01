# WP-03 — Warden.Telemetry (SimulationSnapshot → WorldStateDto)

**Tier:** Sonnet
**Depends on:** WP-02
**Timebox:** 60 minutes
**Budget:** $0.25

---

## Goal

Build the projection layer that turns the engine's internal `SimulationSnapshot` into the wire-format `WorldStateDto`, plus the inverse-direction `AiCommandBatch` dispatcher that safely applies whitelisted commands to a running simulation. Own both sides of the AI/engine boundary in one project so they evolve together.

---

## Reference files

- `APIFramework/Core/SimulationSnapshot.cs`
- `APIFramework/Core/SimulationBootstrapper.cs` (for `Capture()` semantics)
- `APIFramework/Components/` (to understand which entity tags map to which enum values)
- `Warden.Contracts/Telemetry/WorldStateDto.cs` (from WP-02)
- `Warden.Contracts/Handshake/AiCommandBatch.cs` (from WP-02)

## Non-goals

- Do not modify `APIFramework`. This is a projection, not a refactor.
- Do not add new `[Component]` types. Surface what exists.
- Do not pretty-print. The telemetry JSON is machine-readable; pretty-print is a CLI flag in WP-04, not a library concern.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Telemetry/TelemetryProjector.cs` | `public static WorldStateDto Project(SimulationSnapshot snap, long tick, int seed, string simVersion)`. Pure function. No I/O. |
| code | `Warden.Telemetry/TelemetrySerializer.cs` | Two helpers: `SerializeSnapshot(WorldStateDto) : string` (single JSON object) and `SerializeFrame(WorldStateDto) : string` (JSONL-compatible, one line, newline-terminated). Uses `Warden.Contracts.JsonOptions`. |
| code | `Warden.Telemetry/CommandDispatcher.cs` | `public DispatchResult Apply(SimulationBootstrapper sim, AiCommandBatch batch)`. Each command kind gets a private handler. Every handler must be infallible in the sense of never corrupting sim state on error — validate first, apply second. |
| code | `Warden.Telemetry/DispatchResult.cs` | Record: `Applied int, Rejected int, Errors IReadOnlyList<string>`. |
| code | `Warden.Telemetry/SpeciesClassifier.cs` | Internal helper that maps an `Entity` to `"human" | "cat" | "unknown"` based on the components the engine attaches during `EntityTemplates.SpawnHuman`/`SpawnCat`. |
| code | `Warden.Telemetry/WorldObjectKindClassifier.cs` | Internal helper that maps `WorldObjectSnapshot` to the `kind` enum (`fridge`/`sink`/`toilet`/`bed`/`other`). |
| code | `APIFramework.Tests/` — **do not add**. Add a new `Warden.Telemetry.Tests/` project instead. | Keep test ownership tied to project ownership. |
| code | `Warden.Telemetry.Tests/Warden.Telemetry.Tests.csproj` | xunit. |
| code | `Warden.Telemetry.Tests/TelemetryProjectorTests.cs` | See acceptance tests. |
| code | `Warden.Telemetry.Tests/CommandDispatcherTests.cs` | See acceptance tests. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-03.md` | Completion note. |

Update `ECSSimulation.sln` to include `Warden.Telemetry.Tests`.

---

## Design notes

**Determinism.** The projector takes `seed` and `tick` as explicit inputs. It never calls `DateTime.UtcNow` or `Guid.NewGuid`. `capturedAt` is passed through from the caller, because two runs with the same seed should produce telemetry that differs only in that one field.

**Command dispatch safety.** Before any mutation, validate the full `AiCommandBatch` against the schema. Reject the batch atomically if any command fails validation — never apply a partial batch. Inside each handler, take defensive copies of entity state so a partially-applied command never leaves the ECS index inconsistent.

**`set-config-value` is special.** It mutates `SimConfig` via the existing `SimulationBootstrapper.ApplyConfig` path so it is observable through the same hot-reload mechanism. Do not reach into system constructors directly.

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `TelemetryProjector.Project` on a fresh `SimulationBootstrapper` produces JSON that validates against `world-state.schema.json` via the validator from WP-02. | unit-test |
| AT-02 | Determinism: two `Project` calls at the same `(tick, seed, simVersion, snap)` produce byte-identical JSON. | unit-test |
| AT-03 | Entity `species` resolves correctly for at least one human and one cat (spawn one of each with `EntityTemplates`). | unit-test |
| AT-04 | `CommandDispatcher.Apply` with an invalid batch returns `Rejected > 0`, `Applied == 0`, and does not mutate the sim (entity count unchanged). | unit-test |
| AT-05 | `CommandDispatcher.Apply` with a valid `spawn-food` command results in a new bolus entity visible in the next `Project` call. | unit-test |
| AT-06 | `CommandDispatcher.Apply` with a valid `set-config-value` command changes the running `SimConfig` (verified via `sim.Config.Systems.Brain.SleepMaxScore` read-back). | unit-test |
| AT-07 | No invariant violations are produced by dispatching any single whitelisted command in isolation. | unit-test |
