# WP-1.0.A — Schema v0.2 Social Additions — Completion Note

**Executed by:** sonnet-1
**Branch:** ecs-p1-initial
**Started:** 2026-04-24T10:00:00Z
**Ended:** 2026-04-24T10:45:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Landed the v0.2 minor bump on `world-state.schema.json`. The three new wire-format surfaces — `entities[].social`, top-level `relationships[]`, and top-level `memoryEvents[]` — are all `additionalProperties: false`, with explicit `maxItems` on every array and explicit `minimum`/`maximum` on every numeric field.

The `schemaVersion` constraint changed from `const: "0.1.0"` to `enum: ["0.1.0", "0.2.0"]` to satisfy the additive-compatibility requirement (AT-02): v0.1 samples must round-trip clean under the v0.2 schema. The TelemetryProjector explicitly emits `SchemaVersion = "0.1.0"` and continues to do so unchanged; the projector's existing tests all pass.

`WorldStateReferentialChecker` covers duplicate-pair detection (normalises to canonical form before dedup, so `(A,B)` and `(B,A)` both produce `"duplicate-pair"`), missing-participant resolution, and the v0.2 reservation of `scope: "global"`.

The SCHEMA-ROADMAP §v0.2 section was updated in place to reflect the actual landed shape and to record the three deliberate variances from the original draft (drive split, mood as free string, global-scope reservation).

**Key judgement call:** `schemaVersion` uses `enum` rather than remaining a `const`. This is the only way to satisfy AT-02 (v0.1 compatibility) while also stamping v0.2 documents correctly. The alternative would require two schema files, which adds complexity without benefit.

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | OK | All new surfaces carry `additionalProperties: false`; `maxItems` and `minimum`/`maximum` present on every new array/numeric field. Verified by `dotnet build` (0 warnings) and schema inspection. |
| AT-02 | OK | `WorldState_V01SampleRoundTripsUnderV02Schema` and the existing `WorldState_RoundTrips` both pass. |
| AT-03 | OK | `WorldState_V02SampleRoundTrips` passes full round-trip (validate → deserialise → re-serialise → validate → idempotency check). |
| AT-04 | OK | `WorldState_V02_RelationshipThreePatterns_FailsMaxItems` passes. |
| AT-05 | OK | `WorldState_V02_MemoryEventDescriptionTooLong_FailsMaxLength` passes. |
| AT-06 | OK | `WorldState_V02_GlobalMemoryScope_RejectedByReferentialChecker` passes with exact reason `"global-scope-reserved-for-v0.3"`. |
| AT-07 | OK | `WorldState_V02_RelationshipParticipantMissing_RejectedByReferentialChecker` passes. |
| AT-08 | OK | `WorldState_V02_DuplicateUnorderedPair_RejectedByReferentialChecker` passes with exact reason `"duplicate-pair"`. |
| AT-09 | OK | All 24 `Warden.Telemetry.Tests` pass. Projector still emits `SchemaVersion = "0.1.0"`; new fields absent from output. |
| AT-10 | OK | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-11 | OK | `dotnet test ECSSimulation.sln` — 0 failures across all test projects. |

## Files added

```
Warden.Contracts/Telemetry/SocialStateDto.cs
Warden.Contracts/Telemetry/RelationshipDto.cs
Warden.Contracts/Telemetry/MemoryEventDto.cs
Warden.Contracts/SchemaValidation/WorldStateReferentialChecker.cs
Warden.Contracts.Tests/Samples/world-state-v02.json
docs/c2-infrastructure/work-packets/_completed/WP-1.0.A.md
```

## Files modified

```
docs/c2-infrastructure/schemas/world-state.schema.json      — canonical schema bumped to v0.2
Warden.Contracts/SchemaValidation/world-state.schema.json   — embedded resource (mirrors canonical)
Warden.Contracts/SchemaValidation/Schema.cs                 — added SchemaVersions static class
Warden.Contracts/Telemetry/WorldStateDto.cs                 — added Social to EntityStateDto; Relationships/MemoryEvents to WorldStateDto
Warden.Contracts.Tests/SchemaRoundTripTests.cs              — added AT-02 through AT-08 tests plus builder helpers
docs/c2-infrastructure/SCHEMA-ROADMAP.md                    — §v0.2 updated to reflect actual landed shape and variances
```

## Diff stats

11 files changed (6 added, 5 modified, excluding the completion note itself).

## Deliberate variances from SCHEMA-ROADMAP §v0.2 original draft

1. **Drive split (self vs pair).** The cast bible, authored after the roadmap, separates drives into self-state (`belonging`, `status`, `affection`, `irritation`, `loneliness`) and pair-targeted (`attraction`, `trust`, `suspicion`, plus `jealousy` as a reserved derived pair drive). The original roadmap put all drives on `entities[].social`. The split is the correct architecture per SRD §8.3 and is documented in `SCHEMA-ROADMAP.md`.

2. **`currentMood` is a free-form string, not an enum.** Pre-enumerating moods at v0.2 would constrain emergent mood vocabulary the simulation has not yet generated. `maxLength: 32` is the only constraint.

3. **`scope: "global"` is reserved, not active.** The array shape is present (avoiding a second minor bump at v0.3), but any v0.2 emitter sending `scope: "global"` is rejected at runtime by `WorldStateReferentialChecker` with reason `"global-scope-reserved-for-v0.3"`.

4. **`schemaVersion` uses `enum` not `const`.** Changing to `const: "0.2.0"` would break v0.1 consumers (AT-02 requires v0.1 samples to round-trip under v0.2 schema). Using `enum: ["0.1.0", "0.2.0"]` satisfies both constraints simultaneously.

5. **Two copies of the schema file.** Discovered during implementation that `docs/c2-infrastructure/schemas/world-state.schema.json` (canonical) and `Warden.Contracts/SchemaValidation/world-state.schema.json` (embedded resource, used at runtime) are separate files. Both were updated to match. A future packet should consider automating this sync.

## Followups

- Engine-side `Social` component family and systems that mutate drives — Phase 1 engine packet.
- `Warden.Telemetry` projector population of `social`, `relationships`, `memoryEvents` — later Phase 1 packet.
- v0.3 chronicle packet: turn on `scope: "global"` and remove the `WorldStateReferentialChecker` rejection guard.
- Consider CI or build step to auto-copy the canonical schema to the embedded resource path to eliminate the sync risk noted above.
- `world-config-delta` schema for social tuning values — deferred per Non-goals; needs its own small packet.
