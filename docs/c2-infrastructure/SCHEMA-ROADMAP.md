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

### v0.2 — Social pillar

**Driver:** post-Phase-0 Phase 2 (social systems). Lands when the engine adds social drives, relationships, and memories.

Schema additions:

- `world-state` minor bump: `entities[].social` optional object containing `socialDrives` (belonging, status, affection, irritation, attraction, trust, suspicion, jealousy, loneliness), `personalityTraits[]`, `currentMood`. All fields optional under the `social` slot — v0.1 consumers ignore the slot entirely.
- `world-state` minor bump: top-level `relationships[]` array of relationship entities (per-pair model — see `00-SRD.md` §8.3). Each carries `participantA`, `participantB`, `kind` (rivalry/romance/friendship/professional/etc), `intensity`, `history[]` of references to memory events.
- `world-state` minor bump: top-level `memoryEvents[]` array. Per-pair memories live on the relationship; office-wide memories (Kevin's chili) live here with `scope: "global"`.
- New `world-config-delta` schema or extension: covers social tuning values (relationship-decay rate, gossip-spread coefficient, mood-volatility) without polluting the balance-test command list.

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
