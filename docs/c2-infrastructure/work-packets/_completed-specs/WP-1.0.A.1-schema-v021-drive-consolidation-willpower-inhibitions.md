# WP-1.0.A.1 — Schema v0.2.1 Drive Consolidation + Willpower + Inhibitions

**Tier:** Sonnet
**Depends on:** WP-1.0.A (must already be merged — this packet patches its output)
**Timebox:** 75 minutes
**Budget:** $0.30

---

## Goal

Patch the v0.2 social schema landed in WP-1.0.A. Two corrections plus one addition, all on `world-state.schema.json` and the corresponding DTOs in `Warden.Contracts`:

1. **Consolidate drives onto the entity.** All eight drives — `belonging`, `status`, `affection`, `irritation`, `attraction`, `trust`, `suspicion`, `loneliness` — move onto `entities[].social.drives`. Each drive is `{current, baseline}`, both `0–100` integers. The cast bible commits to drives as self-state values that differ in *amount* between NPCs and in *amount over time* for one NPC; nothing is pair-targeted at the drive layer.

2. **Remove `pairDrives` from the relationship.** The four-drive object (`attraction, trust, suspicion, jealousy`) is dropped from `relationships[].pairDrives` because it has no consumers — the projector never populated it, no engine system reads it, no test depended on its values. Deletion is safe.

3. **Add willpower and inhibitions.** Two reserved surfaces on `entities[].social`:
   - `willpower` — `{current, baseline}` integer object, both `0–100`. Self-state meta-resistance that depletes with sustained suppression and regenerates with rest.
   - `inhibitions[]` — array of `{class, strength, awareness}` entries, `maxItems: 8`. `class` is an enum (see Design notes); `strength` is `0–100`; `awareness` is the enum `known | hidden`. Inhibitions are hard blockers on action classes, regardless of drive level.

Bump `schemaVersion` enum from `["0.1.0", "0.2.0"]` to `["0.1.0", "0.2.1"]` (drop 0.2.0 — see Design notes). Update `SCHEMA-ROADMAP.md` to reflect the corrected v0.2 shape and to note the corrective bump. v0.1 samples must continue to round-trip cleanly. The existing v0.2 sample (`world-state-v02.json`) gets its drive layout corrected and its file renamed to `world-state-v021.json` to match the new version stamp.

This is the second-to-last contract bump before the spatial v0.3 packet. Engine-side population of any of these fields is **not** in scope — that's the social-engine packet later in Phase 1.

---

## Reference files

- `docs/c2-content/DRAFT-action-gating.md` — the design source for willpower, inhibitions, approach-avoidance, and physiology-overridable-by-inhibition. **Read this first.** It is what the schema is encoding.
- `docs/c2-content/DRAFT-cast-bible.md` — the eight-drive catalog. Confirms drives are entity-level self-state, not pair-targeted.
- `docs/c2-infrastructure/00-SRD.md` §8.5 (social state is first-class).
- `docs/c2-infrastructure/SCHEMA-ROADMAP.md` §v0.2 — the entry that describes the *current* (post-WP-1.0.A) shape; this packet rewrites it.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.0.A.md` — the completion note for the packet this one patches. Read its "Files added" and "Files modified" sections to know exactly what is on disk.
- `docs/c2-infrastructure/schemas/world-state.schema.json` — current v0.2 file to patch.
- `Warden.Contracts/SchemaValidation/world-state.schema.json` — embedded resource; must stay in sync with the canonical file.
- `Warden.Contracts/Telemetry/SocialStateDto.cs`, `RelationshipDto.cs`, `MemoryEventDto.cs` — DTOs to revise.
- `Warden.Contracts/SchemaValidation/Schema.cs` — version constants.
- `Warden.Contracts/SchemaValidation/WorldStateReferentialChecker.cs` — referential checker; the `participantA != participantB` and pair-dedup invariants stay; the `pairDrives` checks (if any) are removed.
- `Warden.Contracts.Tests/Samples/world-state-v02.json` — the v0.2 sample to be revised and renamed.
- `Warden.Contracts.Tests/SchemaRoundTripTests.cs` — test file to extend.
- `docs/c2-infrastructure/work-packets/WP-1.0.A-schema-v02-social-additions.md` — for format reference only; do not re-execute its instructions.

## Non-goals

- Do **not** modify the engine. No new components, no system wiring. Schema and DTO bump only.
- Do **not** implement the action-selection logic that uses willpower or inhibitions. The action-gating doc describes design intent; implementation is the social-engine packet (Phase 1.4).
- Do **not** populate the new fields in `Warden.Telemetry` projector. Projector still emits `SchemaVersion = "0.1.0"` and absent/empty social state; existing projector tests must stay green.
- Do **not** touch `relationships[].patterns`, `relationships[].intensity`, `relationships[].historyEventIds`, or any field of `relationships[]` other than the removal of `pairDrives`. Those structures are correct as landed in WP-1.0.A.
- Do **not** touch `memoryEvents[]`. That entire surface stays as WP-1.0.A landed it.
- Do **not** re-add `selfDrives` as a separate object alongside `drives`. There is one drive object; it has all eight drives as direct sub-fields; no nested `selfDrives` / `pairDrives` partitioning.
- Do **not** add a `jealousy` drive. The cast bible commits to eight drives. `jealousy` was a roadmap-era reservation that the cast bible deprecated. Drop it cleanly.
- Do **not** add per-class willpower (sexual willpower, dietary willpower, etc.). One global `willpower` object, period. Domain specificity lives in inhibitions.
- Do **not** introduce a NuGet dependency. The minimal in-house validator stays the only validator.
- Do **not** retry, recurse, or "self-heal" on schema-validation failure. Fail closed per SRD §4.1.
- Do **not** preserve `schemaVersion: "0.2.0"` in the enum. Drop it. v0.2.0 was a transient that no producer ever stamped (the projector still emits 0.1.0); collapsing it into 0.2.1 keeps the contract coherent.
- Do **not** introduce per-NPC catchphrases, dialogue lines, or canned utterances anywhere. (Standing rule per cast bible.)
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)

---

## Design notes

### One drive object, eight fields

`entities[].social.drives` is a single object with eight sub-fields, in the canonical order from the cast bible: `belonging`, `status`, `affection`, `irritation`, `attraction`, `trust`, `suspicion`, `loneliness`. Each sub-field is itself an object with `current` (`0–100` integer) and `baseline` (`0–100` integer). The pattern is:

```json
"drives": {
  "belonging":  { "current": 60, "baseline": 50 },
  "status":     { "current": 40, "baseline": 35 },
  "affection":  { "current": 55, "baseline": 50 },
  "irritation": { "current": 70, "baseline": 25 },
  "attraction": { "current": 30, "baseline": 30 },
  "trust":      { "current": 50, "baseline": 60 },
  "suspicion":  { "current": 35, "baseline": 20 },
  "loneliness": { "current": 65, "baseline": 40 }
}
```

`current` is today's value; `baseline` is the typical resting value the engine's drive-dynamics system pulls toward. The split is what makes "Donna feels different on Wednesday than Monday" representable on the wire.

All eight sub-fields are required when `drives` is present. `social` itself remains optional on the entity.

### Willpower: one number, two columns

`entities[].social.willpower` is `{current, baseline}`, both `0–100` integers. Same shape as a drive sub-field for parsing simplicity. Both fields required when `willpower` is present. `willpower` itself is optional.

There is no per-domain willpower. The action-gating doc commits to single-global willpower with inhibition `class` carrying domain specificity.

### Inhibitions: small array, three fields per entry

`entities[].social.inhibitions` is an array, `maxItems: 8`. Each entry is an object with three required fields:

- `class`: enum, eight starter values: `infidelity, confrontation, bodyImageEating, publicEmotion, physicalIntimacy, interpersonalConflict, riskTaking, vulnerability`. `additionalProperties: false` at the entry level.
- `strength`: `0–100` integer. Action-selection consults this; 100 is absolute, 0 is no-op.
- `awareness`: enum, two values: `known, hidden`. Whether the NPC could explain this inhibition if asked.

The `inhibitions[]` array is optional and may be empty. An NPC with no inhibitions is permitted by the schema (rare in practice; archetype catalogs will populate at least one for most NPCs).

### Why 0.2.0 collapses into 0.2.1

`schemaVersion` enum becomes `["0.1.0", "0.2.1"]`. WP-1.0.A bumped to 0.2.0; that bump never had a producer that stamped 0.2.0 (the projector still emits 0.1.0; no v0.2.0 wire messages exist in the wild). Keeping 0.2.0 in the enum would mean documents stamped "0.2.0" — none exist, but if any showed up — would also have to satisfy the new shape (no `pairDrives`, all eight drives on the entity). Cleaner to drop the version that was never stamped and treat 0.2.1 as the first wire-format version to ship. The roadmap entry is rewritten to reflect this.

### Pair-drive removal: safe

The completion note for WP-1.0.A confirms `pairDrives` was added to the schema and DTO but never wired into a producer or consumer. `Warden.Telemetry` does not populate it; no engine system reads from a `RelationshipDto` at all. Removing it is a contract simplification with no downstream consumers to migrate. Tests that asserted on `pairDrives` shape are deleted, not migrated.

### Referential checker: one rule removed, others kept

`WorldStateReferentialChecker` keeps:
- `participantA != participantB`
- canonical pair ordering and dedup (`"duplicate-pair"` reason)
- `participants` and `relationshipId` references resolve
- `memoryEvents[].scope == "global"` rejected with `"global-scope-reserved-for-v0.3"`

`WorldStateReferentialChecker` does not need any changes for the new fields. Drives, willpower, and inhibitions are entity-local and don't reference other entities, so referential integrity is unaffected.

If WP-1.0.A introduced any `pairDrives`-specific assertions in the checker, those are deleted. (The completion note doesn't mention any, but the Sonnet should grep to be sure.)

### Sample file rename

`Warden.Contracts.Tests/Samples/world-state-v02.json` → `Warden.Contracts.Tests/Samples/world-state-v021.json`. Update the test reference. Inside the sample: `schemaVersion: "0.2.1"`; replace the entity's `social.selfDrives` with the eight-field `social.drives` (set sensible 0–100 values, vary `current` from `baseline` for at least one drive to demonstrate the split); add a `willpower` block; add at least one entry in `inhibitions[]` (one `known`, ideally one more `hidden` for coverage); remove `pairDrives` from the relationship.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `docs/c2-infrastructure/schemas/world-state.schema.json` (modified) | `schemaVersion` enum becomes `["0.1.0", "0.2.1"]`. `entities[].social.selfDrives` removed; `entities[].social.drives` added (eight required sub-fields, each `{current, baseline}`). `entities[].social.willpower` added (`{current, baseline}` optional). `entities[].social.inhibitions[]` added (optional, `maxItems: 8`, entries with `class`/`strength`/`awareness`). `relationships[].pairDrives` removed; `relationships` `required` array updated to no longer include `pairDrives`. The `$defs` blocks for `selfDrives` and `pairDrives` are removed; new `$defs` for `drives`, `driveValue`, `willpower`, `inhibition` added. Every new object carries `additionalProperties: false`; every new array has explicit `maxItems`; every numeric field has explicit `minimum`/`maximum`. |
| code | `Warden.Contracts/SchemaValidation/world-state.schema.json` (modified) | Embedded-resource mirror — must match canonical bit-for-bit (per WP-1.0.A's lesson). |
| code | `Warden.Contracts/Telemetry/SocialStateDto.cs` (modified) | `SelfDrivesDto` removed. New `DriveValueDto(int Current, int Baseline)`. New `DrivesDto(DriveValueDto Belonging, DriveValueDto Status, DriveValueDto Affection, DriveValueDto Irritation, DriveValueDto Attraction, DriveValueDto Trust, DriveValueDto Suspicion, DriveValueDto Loneliness)`. New `WillpowerDto(int Current, int Baseline)`. New enums `InhibitionClass` (eight values, camelCase JSON) and `InhibitionAwareness` (`Known`, `Hidden`). New `InhibitionDto(InhibitionClass Class, int Strength, InhibitionAwareness Awareness)`. `SocialStateDto` revised to carry `DrivesDto Drives`, `WillpowerDto? Willpower`, `IReadOnlyList<InhibitionDto> Inhibitions`, plus the existing `PersonalityTraits`, `CurrentMood`, `VocabularyRegister`. |
| code | `Warden.Contracts/Telemetry/RelationshipDto.cs` (modified) | `PairDrivesDto` deleted. `RelationshipDto` no longer carries `PairDrives`. All other fields unchanged. |
| code | `Warden.Contracts/SchemaValidation/Schema.cs` (modified) | `SchemaVersions.WorldState` becomes `"0.2.1"`. |
| code | `Warden.Contracts/SchemaValidation/WorldStateReferentialChecker.cs` (modified) | Remove any references to `pairDrives` if present; otherwise no functional changes. Verify with grep. |
| code | `Warden.Contracts.Tests/Samples/world-state-v021.json` (renamed + modified from `world-state-v02.json`) | Sample updated to v0.2.1 shape. The old `world-state-v02.json` file is **deleted** (not kept around). |
| code | `Warden.Contracts.Tests/SchemaRoundTripTests.cs` (modified) | Update tests that loaded `world-state-v02.json` to load `world-state-v021.json`. Delete tests that asserted on `selfDrives` or `pairDrives` shape. Add tests covering the new shape: drives round-trip, willpower round-trip, inhibitions round-trip with both `known` and `hidden` awareness, inhibitions array `maxItems: 8` enforcement, inhibition class enum rejection on bad value. The v0.1 sample must still round-trip clean (additive compatibility). |
| code | `Warden.Contracts.Tests/SchemaValidatorTests.cs` (modified, if needed) | If any test asserted on the removed `pairDrives` or `selfDrives` shapes, delete those test cases. |
| doc | `docs/c2-infrastructure/SCHEMA-ROADMAP.md` (modified) | Rewrite §v0.2 to describe the actual landed shape post-WP-1.0.A.1. The "Deliberate variances" subsection is rewritten to reflect: drives consolidated to entity, no pair-drives, willpower and inhibitions added, `currentMood` still a free string, `scope: "global"` still reserved for v0.3. Note the 0.2.0→0.2.1 collapse with a one-sentence rationale. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.0.A.1.md` | Completion note. Use the standard template. Explicitly enumerate: (a) what was removed (selfDrives, pairDrives, jealousy, schemaVersion 0.2.0), (b) what was added (drives, willpower, inhibitions), (c) test deltas. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `world-state.schema.json` declares `schemaVersion` enum `["0.1.0", "0.2.1"]` exactly. No `"0.2.0"`. | unit-test |
| AT-02 | The pre-existing v0.1 sample (unchanged) round-trips clean under the v0.2.1 schema and DTOs. | unit-test |
| AT-03 | The renamed `world-state-v021.json` round-trips clean: schema validates, DTO deserialises, re-serialises to JSON semantically equal to the input. | unit-test |
| AT-04 | A v0.2.1 sample with `entities[].social.drives.belonging.current = 101` is rejected with a specific `maximum` error. | unit-test |
| AT-05 | A v0.2.1 sample with `entities[].social.willpower.baseline = -1` is rejected with a specific `minimum` error. | unit-test |
| AT-06 | A v0.2.1 sample with `inhibitions[].class = "notARealClass"` is rejected with a specific enum error. | unit-test |
| AT-07 | A v0.2.1 sample with nine `inhibitions[]` entries fails `maxItems: 8`. | unit-test |
| AT-08 | A v0.2.1 sample with `inhibitions[].awareness = "hidden"` round-trips clean (covers the field that the action-gating doc treats as load-bearing). | unit-test |
| AT-09 | A v0.2.1 sample with `entities[].social.drives` missing one of the eight required sub-fields is rejected. (Drives object is all-or-nothing.) | unit-test |
| AT-10 | A v0.2.1 sample with `relationships[0].pairDrives = {...}` is rejected by `additionalProperties: false`. | unit-test |
| AT-11 | The DTO graph contains no `SelfDrivesDto`, no `PairDrivesDto`, no `Jealousy` field. (Static assertion via reflection or grep.) | unit-test |
| AT-12 | `Warden.Telemetry.Tests` all pass — projector still emits `SchemaVersion = "0.1.0"`. | build + unit-test |
| AT-13 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-14 | `dotnet test ECSSimulation.sln` — every existing test outside this packet's scope stays green; tests inside this packet's scope reflect the new shape. | build |

---

## Followups (not in scope)

- Engine-side `Social` component family (`drives`, `willpower`, `inhibitions`) and the systems that mutate them — the social-engine packet (Phase 1.4).
- Action-selection logic that consults willpower + inhibitions per the action-gating doc — same packet.
- Drive-dynamics system (circadian shape, decay-toward-baseline, per-archetype volatility) — same packet.
- `Warden.Telemetry` projector populating `social`, `relationships`, `memoryEvents` — same packet.
- Cast-generator packet adding `willpowerBaseline` ranges and starter inhibition sets to each archetype.
- v0.3 chronicle packet: turn on `scope: "global"` and remove the `WorldStateReferentialChecker` rejection guard.
- Auto-sync canonical schema → embedded resource (carry-over followup from WP-1.0.A).
