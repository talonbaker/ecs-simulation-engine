# WP-1.0.A.1 — schema-v021-drive-consolidation-willpower-inhibitions — Completion Note

**Executed by:** sonnet-4.6
**Branch:** feat/wp-1.0.A.1
**Started:** 2026-04-25T00:00:00Z
**Ended:** 2026-04-25T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Landed v0.2.1 on `world-state.schema.json` and the corresponding `Warden.Contracts` DTOs. Three concrete changes:

1. **Drives consolidated to entity.** Replaced `selfDrives` (five-drive flat object) with `drives` (eight-drive object, each sub-field `{current, baseline}`). The pair-targeted `pairDrives` object was removed from `relationship` entirely — no producer ever populated it and no consumer ever read it.

2. **`pairDrives` removed and `jealousy` dropped.** The cast bible commits to eight drives; `jealousy` was a roadmap-era reservation. Both deleted cleanly.

3. **Willpower and inhibitions added.** `entities[].social.willpower` is `{current, baseline}`. `entities[].social.inhibitions[]` (`maxItems: 8`) carries the action-gating surfaces described in `DRAFT-action-gating.md` (`class`, `strength`, `awareness`).

`schemaVersion: "0.2.0"` was collapsed into `"0.2.1"` — no producer ever stamped `"0.2.0"` on the wire, so keeping it in the enum would be noise.

The branch required merging `ecs-p1-initial` (which contained the packet file) and `staging` before work could begin. Both merges were clean.

`SCHEMA-ROADMAP.md` §v0.2 was rewritten to reflect the actual landed shape.

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | ✓ | `world-state.schema.json` enum is `["0.1.0", "0.2.1"]` — verified by unit test and schema inspection. |
| AT-02 | ✓ | `WorldState_V01SampleRoundTripsUnderV021Schema` passes; v0.1 sample round-trips clean. |
| AT-03 | ✓ | `WorldState_V021SampleRoundTrips` passes full round-trip. |
| AT-04 | ✓ | `WorldState_V021_DriveCurrentOver100_FailsMaximum` — 101 rejected with maximum error. |
| AT-05 | ✓ | `WorldState_V021_WillpowerBaselineNegative_FailsMinimum` — -1 rejected with minimum error. |
| AT-06 | ✓ | `WorldState_V021_InhibitionBadClass_FailsEnum` — unknown class rejected with enum error. |
| AT-07 | ✓ | `WorldState_V021_NineInhibitions_FailsMaxItems` — 9 entries rejected. |
| AT-08 | ✓ | `WorldState_V021_InhibitionHiddenAwareness_RoundTripsClean` — `awareness: "hidden"` round-trips and deserialises correctly. |
| AT-09 | ✓ | `WorldState_V021_DriveMissingSubField_FailsRequired` — drives object missing `loneliness` rejected. |
| AT-10 | ✓ | `WorldState_V021_RelationshipPairDrives_RejectedByAdditionalProperties` — `pairDrives` rejected. |
| AT-11 | ✓ | `DtoGraph_ContainsNo_SelfDrivesDto_PairDrivesDto_JealousyField` — reflection confirms types absent. |
| AT-12 | ✓ | All 24 `Warden.Telemetry.Tests` pass; projector still emits `SchemaVersion = "0.1.0"`. |
| AT-13 | ✓ | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-14 | ✓ | `dotnet test ECSSimulation.sln` — 388 passed, 0 failed across all test projects. |

## Files added

```
Warden.Contracts.Tests/Samples/world-state-v021.json
docs/c2-infrastructure/work-packets/_completed/WP-1.0.A.1.md
```

## Files modified

```
docs/c2-infrastructure/schemas/world-state.schema.json      — v0.2.1 shape (drives, willpower, inhibitions; no pairDrives/selfDrives)
Warden.Contracts/SchemaValidation/world-state.schema.json   — embedded resource mirror (must match canonical)
Warden.Contracts/Telemetry/SocialStateDto.cs                — replaced SelfDrivesDto with DrivesDto/DriveValueDto/WillpowerDto/InhibitionDto/enums
Warden.Contracts/Telemetry/RelationshipDto.cs               — removed PairDrivesDto and PairDrives property
Warden.Contracts/SchemaValidation/Schema.cs                 — SchemaVersions.WorldState = "0.2.1"
Warden.Contracts/Telemetry/WorldStateDto.cs                 — default SchemaVersion = "0.2.1", doc comment updated
Warden.Contracts.Tests/SchemaRoundTripTests.cs              — updated existing tests, added AT-03 through AT-11
docs/c2-infrastructure/SCHEMA-ROADMAP.md                    — §v0.2 rewritten to reflect v0.2.1 actual shape
```

## Files deleted

```
Warden.Contracts.Tests/Samples/world-state-v02.json         — replaced by world-state-v021.json
```

## Diff stats

10 files changed (2 added counting completion note, 1 deleted, 9 modified).

## Deliberate variances from packet spec

1. **`worldStateReferentialChecker.cs` — no changes made.** Packet said "if WP-1.0.A introduced any `pairDrives`-specific assertions in the checker, those are deleted." Grepped: there were none. File unchanged.

2. **Old tests renamed rather than "deleted".** The packet said delete tests asserting on `selfDrives`/`pairDrives` shape. WP-1.0.A had introduced tests named `WorldState_V02_*`. These were renamed to `WorldState_V021_*` with updated behaviour rather than deleted, since the underlying scenarios (patterns-maxItems, memoryEvent-description-maxLength, referential-checker tests) remain valid assertions — only the version stamp and pairDrives references changed.

## Followups

- Engine-side `Social` component family (`drives`, `willpower`, `inhibitions`) — social-engine packet (Phase 1.4).
- `Warden.Telemetry` projector population of social state — same packet.
- Cast-generator packet adding `willpowerBaseline` ranges and starter inhibitions to archetypes.
- v0.3 chronicle packet: remove the `global-scope-reserved-for-v0.3` rejection guard in `WorldStateReferentialChecker`.
- Auto-sync canonical schema → embedded resource (carry-over from WP-1.0.A; both files still updated manually).
- `world-config-delta` schema for social tuning values — deferred per Non-goals; needs its own packet.
