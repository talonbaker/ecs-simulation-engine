# Schema Roadmap — C2 Infrastructure

The Phase-0 contracts (`schemas/*.schema.json`) are pinned at `schemaVersion: "0.1.0"` and intentionally narrow. They cover only what the *current* engine exposes: physiology, drives, world items, world objects, transit. They do not yet cover social state, persistent narrative events, room membership, character definitions, or any of the gameplay surface this project is ultimately about.

This document is the forward plan. It exists so Phase-1+ Sonnet packets land in a known schema-versioning regime instead of inventing one. Update it whenever a phase changes scope.

---

## Versioning rules

`MAJOR.MINOR.PATCH`.

- **PATCH** — clarification or doc change, no shape change. Validators pass without modification.
- **MINOR** — additive change: new optional field, new enum value (consumers tolerate via null-safe parsing), new schema file. v0.1 consumers can read v0.2 messages by ignoring the new fields. v0.2 consumers reading v0.1 messages must treat the new fields as absent.
- **MAJOR** — breaking change: required field added, existing field renamed, type narrowed. Consumers from prior major refuse to parse. Discouraged; only used when no additive path exists.

The orchestrator validates `schemaVersion` on every cross-tier message and refuses to consume a *major* mismatch. Minor mismatches are tolerated with a warning logged to `events.jsonl`.

---

## Roadmap by version

### v0.1 — Phase 0 (current)

Schemas already in `schemas/`:

- `world-state` — physiology, drives, world items, world objects, transit, invariants.
- `opus-to-sonnet` — SpecPacket.
- `sonnet-result` — SonnetResult.
- `sonnet-to-haiku` — ScenarioBatch.
- `haiku-result` — HaikuResult.
- `ai-command-batch` — six whitelisted mutations for balance testing only.

Out of scope at v0.1: anything social, anything persistent-narrative, any rooms.

### v0.2.1 — Social pillar _(landed in WP-1.0.A.1; WP-1.0.A was a transient that was never stamped on the wire)_

**Driver:** post-Phase-0 Phase 1 (social systems). Wire-format bump only; engine-side population is a later packet. `schemaVersion: "0.2.0"` was added by WP-1.0.A but no producer ever stamped it (the projector still emits `"0.1.0"`), so WP-1.0.A.1 collapsed it into `"0.2.1"`. The accepted enum is now `["0.1.0", "0.2.1"]` — `"0.2.0"` has been dropped.

Schema additions (all new fields optional; v0.1 consumers ignore them):

- `world-state` minor bump: `entities[].social` optional object. Contains:
  - `drives` — all eight drives consolidated on the entity: `belonging`, `status`, `affection`, `irritation`, `attraction`, `trust`, `suspicion`, `loneliness`. Each drive is `{current, baseline}`, both 0–100 integers. All eight sub-fields are required when `drives` is present.
  - `willpower` — `{current, baseline}` optional object, both 0–100 integers. Global self-state reservoir; depletes with suppression, regenerates with rest.
  - `inhibitions[]` — optional array, `maxItems: 8`. Each entry: `{class, strength, awareness}`. `class` is enum (`infidelity | confrontation | bodyImageEating | publicEmotion | physicalIntimacy | interpersonalConflict | riskTaking | vulnerability`); `strength` 0–100; `awareness` enum (`known | hidden`).
  - `personalityTraits[]` — Big Five dimensions, value −2 to +2. `maxItems: 5`.
  - `currentMood` — free-form short string (`maxLength: 32`). **Not an enum** — premature enumeration locks out emergent moods.
  - `vocabularyRegister` — enum: `formal | casual | crass | clipped | academic | folksy`.
- `world-state` minor bump: top-level `relationships[]` array (`maxItems: 200`). Each carries `participantA`, `participantB` (both UUID entity refs), `patterns[]` (`maxItems: 2`, see pattern enum), `intensity` 0–100, and `historyEventIds[]`. No `pairDrives` — drives are entity-level only.
- `world-state` minor bump: top-level `memoryEvents[]` array (`maxItems: 4096`). Each carries `id`, `tick`, `participants[]`, `kind`, `scope` (`pair | global`), `description` (`maxLength: 280`), `persistent`, optional `relationshipId`. At v0.2.1 only `scope: "pair"` is valid; `scope: "global"` is reserved for v0.3.
- New `world-config-delta` schema: covers social tuning values. Deferred to its own packet.

**Deliberate variances from the original §v0.2 draft (as corrected by WP-1.0.A.1):**

1. **Drives consolidated to entity; no pair-drives.** All eight drives (`belonging`, `status`, `affection`, `irritation`, `attraction`, `trust`, `suspicion`, `loneliness`) live on `entities[].social.drives`. The WP-1.0.A split of self-drives vs `relationships[].pairDrives` has been removed — the action-gating bible confirms drives are entity-level self-state values. `jealousy` (a roadmap-era reservation) has been dropped entirely; the cast bible commits to eight drives.

2. **Willpower and inhibitions added.** The action-gating design doc (`DRAFT-action-gating.md`) specifies willpower as a global self-state reservoir and inhibitions as hard action blockers. Both surfaces are reserved on the wire here; engine-side population is the social-engine packet.

3. **`currentMood` is a free-form string, not an enum.** `maxLength: 32` is the only constraint.

4. **`scope: "global"` is reserved, not live.** Any v0.2.1 emitter sending `scope: "global"` is rejected by `WorldStateReferentialChecker` with reason `"global-scope-reserved-for-v0.3"`.

### v0.3 — Persistent narrative chronicle

**Driver:** post-Phase-0 Phase 4 (spill-stays-spilled mechanic).

Schema additions:

- `world-state` minor bump: `chronicle[]` array of authored-or-emergent narrative events with `id`, `kind`, `participants[]`, `location`, `tick`, `description`, `persistent: bool`. Persistent entries also exist as concrete entities (e.g., a `Stain` entity or a `BrokenItem` entity); the chronicle is the *narrative* index, the entity tree is the *spatial* index. Both must agree, enforced by an invariant check.
- New `narrative-event-emit` command kind on `ai-command-batch`. **Design-time content authoring only — never runtime** (`00-SRD.md` §8.1).

### v0.4 — Character definitions and customization

**Driver:** post-Phase-0 Phase 3 (curated cast) and Phase 7+ (player customization).

Schema additions:

- New `character-definition` schema — cosmetic fields, personality traits, starting drives, starting relationships, signature behaviors, signature catchphrases, archetype tag. *This is data-driven character creation*; the runtime instantiates entities from definitions rather than hardcoded `EntityTemplates`.
- New `character-catalog` schema — a list of `character-definition` entries plus a deduplication policy and authorship metadata.
- New `place-character` command kind on `ai-command-batch` for content authoring.

### v0.5 — Spatial / room overlay

**Driver:** post-Phase-0 Phase 1 (narrative telemetry needs room queries — "who is in the breakroom").

Schema additions:

- `world-state` minor bump: top-level `rooms[]` array. Each room has `id`, `name`, `boundsPolygon` (or `boundsRect` if grid-aligned), `category` (breakroom/bathroom/cubicle-grid/parking-lot/hallway/etc).
- `world-state` minor bump: `entities[].position` gains optional `roomId` for fast room-membership queries without re-running point-in-polygon at telemetry time. Engine-side responsibility.

---

## What is *not* in the roadmap

- Anything that would require a `2.0.0` major bump. We don't yet anticipate one.
- Streaming telemetry deltas (versus full snapshots). That's an *implementation* concern in `WP-03`'s projector and a future `WP-NN` follow-up, not a schema change.
- Render hints for the eventual 2.5D visualizer. Visual concerns stay in `ECSVisualizer`'s domain, not on the wire.
- Any field used for runtime LLM calls. The architectural axiom is design-time AI only — see `00-SRD.md` §8.1.

---

## Process for proposing a new schema version

1. Open a new packet (`WP-NN`) describing the change and its motivation.
2. Update this document under the relevant version heading.
3. Implement the schema change in `Warden.Contracts/`. Bump `schemaVersion` constants.
4. Update `SchemaValidator` and round-trip tests.
5. Update the cached-prefix corpus manifest (`WP-06`'s `cached-corpus.manifest.json`) if the new schema is part of the cached system prompt.
6. Bump cost-model coefficients in `02-cost-model.md` if the schema growth materially increases cached-prefix size.
