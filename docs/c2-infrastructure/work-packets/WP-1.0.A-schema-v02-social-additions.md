# WP-1.0.A — Schema v0.2 Social Additions

**Tier:** Sonnet
**Depends on:** WP-02 (Warden.Contracts), WP-09 (Orchestrator.Core — for validator wiring sanity)
**Timebox:** 90 minutes
**Budget:** $0.30
**Status:** Draft — awaiting Talon's calibration pass before dispatch.

---

## Goal

Land the v0.2 minor bump on `world-state.schema.json` — the **first move on the social axis** committed by SRD §8.5. This packet adds three optional surfaces to the wire format: `entities[].social` (per-actor self-state), top-level `relationships[]` (per-pair entity carrying pair-targeted state and patterns), and top-level `memoryEvents[]` (the chronicle stub — pair-scoped now, with `scope: "global"` reserved for v0.3). The existing `Warden.Contracts` DTOs grow to match. The schema-version constant moves from `0.1.0` to `0.2.0`. v0.1 samples must continue to round-trip cleanly under the v0.2 schema (additive compatibility per `SCHEMA-ROADMAP.md`). No engine code changes; no projector changes beyond compile-clean. This is a pure contract bump that opens the runway for the lighting/proximity/movement packets that follow.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` §8.3 (per-pair primary, global thin), §8.5 (social state is first-class)
- `docs/c2-infrastructure/SCHEMA-ROADMAP.md` §Versioning rules and §v0.2
- `docs/c2-content/DRAFT-cast-bible.md` — source of social drive list, vocabulary register, and relationship-pattern library
- `docs/c2-infrastructure/schemas/world-state.schema.json` — current v0.1 file to extend
- `Warden.Contracts/Telemetry/WorldStateDto.cs` — DTO surface to extend
- `Warden.Contracts/SchemaValidation/Schema.cs` — version constants
- `Warden.Contracts.Tests/Samples/world-state.json` — v0.1 sample, to be preserved untouched
- `docs/c2-infrastructure/work-packets/WP-02-warden-contracts.md` — format reference for round-trip discipline

## Non-goals

- Do **not** modify the engine. No new components, no system wiring. v0.2 is a wire-format bump only; engine-side population is a later packet.
- Do **not** project social state in `Warden.Telemetry`. The projector still emits v0.1-shaped data; the new arrays default to absent / empty. The projector must compile and its existing tests must stay green.
- Do **not** touch `ai-command-batch.schema.json`. New command kinds (e.g., `narrative-event-emit`, `place-character`) are v0.3 / v0.4 — not this packet.
- Do **not** author content. No archetype catalog file, no NPC instance data, no relationship instances. This packet adds the *types* that hold social state; population is the cast-bible-generator packet.
- Do **not** introduce per-NPC catchphrases, dialogue lines, or canned utterances anywhere. Voice emerges from gameplay (cast bible).
- Do **not** bump the major version. v0.1 consumers must still parse v0.2 messages by ignoring the new optional fields.
- Do **not** add an enum for `currentMood`. Use a free-form short string (`maxLength: 32`) — premature enumeration locks out emergent moods.
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)
- Do **not** introduce NuGet dependencies. The minimal in-house validator stays the only validator (per WP-02).
- Do **not** retry, recurse, or "self-heal" on schema-validation failure. Fail closed per SRD §4.1.

---

## Design notes

### Drive split: self-state on the entity, pair-state on the relationship

The cast-bible drive catalog is eight drives. Five describe **self-state** (`belonging`, `status`, `affection`, `irritation`, `loneliness`) and three describe **a posture toward a specific other NPC** (`attraction`, `trust`, `suspicion`). The architectural axiom (§8.3) says per-pair state lives on the relationship entity. Therefore:

- `entities[].social.selfDrives` carries the five self-drives (each 0–100).
- `relationships[].pairDrives` carries the pair-targeted drives (`attraction`, `trust`, `suspicion`, plus `jealousy` reserved as a pair-targeted derived drive — sourced from the schema-roadmap's original list, kept here because it is structurally pair-targeted; if we later decide jealousy belongs elsewhere, that's a v0.3 patch, not a major bump).

**This is a deliberate variance from `SCHEMA-ROADMAP.md` §v0.2's drafted layout, which listed all nine drives on `entities[].social`.** The cast bible (authored after the roadmap) forces the split; the roadmap entry is updated by this packet to match. Flagged in the calibration check at the bottom — Talon should confirm the split before dispatch.

### Personality traits

Big Five only. Each NPC carries `personalityTraits: [{dimension, value}]` where `dimension ∈ {openness, conscientiousness, extraversion, agreeableness, neuroticism}` and `value ∈ -2..+2` integer. No "vibe" axes, no homebrew dimensions — keeps the surface auditable and reasoning-tractable for Sonnets/Haikus.

### Vocabulary register

Enum: `formal | casual | crass | clipped | academic | folksy`. From the cast bible. Read by the (future) dialogue subsystem; carried on the wire so Haikus can reason about voice consistency without needing the full archetype.

### Relationships are entities, not edges

Per SRD §8.3, the relationship is a **first-class entity**, not a property of either NPC. This packet models it as a top-level array with a stable `id`. Memories reference the relationship by id; behavior systems will eventually subscribe to relationship-state changes. Both directions of a pair share **one** relationship row — `participantA` and `participantB` are interchangeable for query purposes (validator must enforce `participantA != participantB` and reject duplicate pairs differing only in participant order; canonical ordering is alphabetical-by-entity-id).

### Patterns are an enumerated set, capped at 2

From the cast bible's pattern library: `rival, oldFlame, activeAffair, secretCrush, mentor, mentee, bossOf, reportTo, friend, alliesOfConvenience, sleptWithSpouse, confidant, theThingNobodyTalksAbout`. `maxItems: 2` matches the bible's "zero, one, or two patterns active" rule. New patterns are easy to add later; an unbounded list would be unbounded prompt.

### Memory events: pair-scope only at v0.2

The cast bible and SRD §8.3 both anticipate a global chronicle — but that's v0.3 territory (persistent narrative). At v0.2 we add the array shape with a `scope` enum (`pair | global`), validate that v0.2 emitters use only `scope: "pair"`, and reserve `global` for v0.3's chronicle packet. This avoids two minor bumps when one will do.

### Referential integrity

Schema validation alone can't enforce that `participantA` exists in `entities[].id`. Add a sibling helper `WorldStateReferentialChecker.Check(WorldStateDto)` that validates: every relationship's participants resolve to entities; every memory event's participants resolve to entities; every memory event's `relationshipId` (when present) resolves to a relationship. Errors return a `ValidationResult` consistent with the existing schema validator — so callers handle one error shape.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `docs/c2-infrastructure/schemas/world-state.schema.json` (modified) | Bump `schemaVersion` const to `"0.2.0"`. Add `entities[].social` optional object (see Design notes). Add top-level optional `relationships[]` (`maxItems: 200`). Add top-level optional `memoryEvents[]` (`maxItems: 4096`). All new objects carry `additionalProperties: false`. Every new array has explicit `maxItems`. Every new numeric field has explicit `minimum`/`maximum`. |
| code | `Warden.Contracts/Telemetry/SocialStateDto.cs` | Records: `SocialStateDto(SelfDrivesDto SelfDrives, IReadOnlyList<PersonalityTraitDto> PersonalityTraits, string? CurrentMood, VocabularyRegister? VocabularyRegister)`; `SelfDrivesDto(int Belonging, int Status, int Affection, int Irritation, int Loneliness)`; `PersonalityTraitDto(BigFiveDimension Dimension, int Value)`. Enums: `VocabularyRegister`, `BigFiveDimension`. CamelCase JSON via existing `JsonOptions`. |
| code | `Warden.Contracts/Telemetry/RelationshipDto.cs` | `RelationshipDto(string Id, string ParticipantA, string ParticipantB, IReadOnlyList<RelationshipPattern> Patterns, PairDrivesDto PairDrives, int Intensity, IReadOnlyList<string> HistoryEventIds)`; `PairDrivesDto(int Attraction, int Trust, int Suspicion, int Jealousy)`; enum `RelationshipPattern` covering the 13 cast-bible patterns. |
| code | `Warden.Contracts/Telemetry/MemoryEventDto.cs` | `MemoryEventDto(string Id, long Tick, IReadOnlyList<string> Participants, string Kind, MemoryScope Scope, string Description, bool Persistent, string? RelationshipId)`; enum `MemoryScope { Pair, Global }`. |
| code | `Warden.Contracts/Telemetry/WorldStateDto.cs` (modified) | Add optional `Social` to `EntityStateDto`. Add `Relationships` and `MemoryEvents` to `WorldStateDto`. All optional, default null/empty. v0.1 round-trip preserved. |
| code | `Warden.Contracts/SchemaValidation/Schema.cs` (modified) | Update embedded-resource version constants: `WorldStateSchemaVersion = "0.2.0"`. Other schemas unchanged — they stay at 0.1.0 until their respective packets. |
| code | `Warden.Contracts/SchemaValidation/WorldStateReferentialChecker.cs` | Helper covering: distinct entity ids; `participantA != participantB`; canonical pair ordering and dedup; participants/relationships resolve. Returns `ValidationResult` (reused). At v0.2, also rejects any `memoryEvent.scope == "global"` with reason `"global-scope-reserved-for-v0.3"`. |
| code | `Warden.Contracts.Tests/Samples/world-state-v02.json` | New canonical v0.2 sample: ≥2 entities, one carrying full `social` block, one minimal; one relationship with two patterns and non-zero pair drives; one pair-scoped memory event referenced from the relationship's `historyEventIds`. |
| code | `Warden.Contracts.Tests/SchemaRoundTripTests.cs` (modified) | Add: (a) the existing v0.1 sample round-trips clean under v0.2 schema (additive compatibility); (b) the new v0.2 sample round-trips clean; (c) a v0.2 sample with `relationships[].patterns` of length 3 fails `maxItems: 2`; (d) a v0.2 sample with `memoryEvents[].description` > 280 chars fails `maxLength`; (e) `memoryEvents[].scope == "global"` is rejected by the referential checker with the specific reason; (f) a v0.2 sample with a `participantA` not present in `entities[].id` fails the referential checker with a specific reason. |
| doc | `docs/c2-infrastructure/SCHEMA-ROADMAP.md` (modified) | Update §v0.2 to reflect the actual landed shape: drives are split self-vs-pair; `currentMood` is a short string not an enum; memory `scope: "global"` is reserved for v0.3. Mark v0.2 as "landed in WP-1.0.A". |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.0.A.md` | Completion note. Per the WP-02 template. Must explicitly enumerate the deliberate variances from the original SCHEMA-ROADMAP §v0.2 wording so the audit trail is honest. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `world-state.schema.json` declares `schemaVersion` const `"0.2.0"`. The three new top-level / nested surfaces are all `additionalProperties: false`, every new array has explicit `maxItems`, every new numeric field has explicit `minimum`/`maximum`. | unit-test |
| AT-02 | The pre-existing v0.1 sample (unchanged byte-for-byte) round-trips clean under the v0.2 schema and DTOs — additive compatibility holds. | unit-test |
| AT-03 | The new v0.2 sample round-trips clean: schema validates, DTO deserialises, re-serialises to JSON that is semantically equal to the input (canonical-form compare). | unit-test |
| AT-04 | A v0.2 sample with `relationships[].patterns` length 3 fails `maxItems: 2` with a specific, actionable validator error. | unit-test |
| AT-05 | A v0.2 sample with `memoryEvents[].description` of length 281 fails `maxLength: 280`. | unit-test |
| AT-06 | A v0.2 sample whose `memoryEvents[].scope == "global"` is rejected by `WorldStateReferentialChecker` with reason `"global-scope-reserved-for-v0.3"`. | unit-test |
| AT-07 | A v0.2 sample with a `relationships[].participantA` that does not appear in `entities[].id` is rejected by `WorldStateReferentialChecker` with a specific reason. | unit-test |
| AT-08 | A v0.2 sample with two relationships sharing the same unordered pair (e.g., `(A,B)` and `(B,A)`) is rejected by `WorldStateReferentialChecker` with reason `"duplicate-pair"`. | unit-test |
| AT-09 | `Warden.Telemetry` projector compiles and its existing test suite stays green — projector still emits v0.1-shaped data; new fields default to absent/empty. | build + unit-test |
| AT-10 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-11 | `dotnet test ECSSimulation.sln` passes — every existing test stays green. | build |

---

## Followups (not in scope)

- Engine-side `Social` component family and the systems that mutate it (later Phase 1 packet).
- `Warden.Telemetry` projector populating the new fields (later Phase 1 packet).
- Cast-bible generator implementation that spawns characters into these DTOs (Phase 1.4 in the priority list).
- v0.3 chronicle packet (turns on `scope: "global"`).
- `world-config-delta` schema for social tuning values (mentioned in roadmap §v0.2; deferred to its own small packet so it can be reviewed without the noise of these contract changes).
