# Mod API Candidates Ledger

> **Authority:** SRD §8.8 (added 2026-05-02). API surfaces evolve incrementally; modder-extension points are recognized over time, not designed up front.
>
> **Purpose:** Track candidate extension points as they emerge from organic Phase 4+ development. This ledger grows slowly. Entries graduate to the formal Mod API surface only when they have proven stable across multiple consumers and the project enters its Mod API sub-phase (Phase 5 or 6 territory).
>
> **What this is not:** A design document for the Mod API. The full API gets authored as a deliberate work package later. This is the prep work — recognition before formalization.

---

## How this ledger works

Each candidate entry records:

- **What:** the extension point — the contract shape, the registration surface, the data type.
- **Where:** file paths or system names where the candidate lives.
- **Why a candidate:** what makes this surface plausibly modder-relevant (a sneeze mod would touch it; a custom NPC type would touch it; a custom render effect would touch it).
- **Stability:** *fresh* (just landed, may change), *stabilizing* (used by 2+ consumers, likely settling), *stable* (used widely, no breaking changes in N packets — graduation candidate).
- **Source packet:** which packet introduced or last meaningfully touched the candidate.

When a candidate stabilizes enough to graduate, the formal Mod API sub-phase will fold it into a public registration contract. Until then, internal users can use it normally; we just track that it's a future-API surface.

---

## Adding entries

When a packet introduces or significantly modifies a surface that *might* be modder-relevant, append a candidate entry below. **Bias toward inclusion** — this ledger is cheap, and it's easier to retire candidates than to remember missed ones. Each Phase 4+ packet's "Mod API surface" section is the source of new ledger entries.

When refining: add a `Revised: YYYY-MM-DD — <what changed>` line.

When killing: replace the body with `Killed: YYYY-MM-DD — <reason>`. Keep the entry id reserved.

---

## Existing surfaces — already good Mod API candidates from Phase 0–3

These were not designed as Mod API surfaces, but they have the right shape: data-driven, schema-validated, registered through clean enums or catalogs. The Mod API sub-phase will revisit each.

### MAC-001: Per-archetype tuning JSONs

- **What:** Catalog of per-archetype behavior multipliers (choke bias, slip bias, bereavement bias, chore acceptance bias, rescue bias, mass, memory persistence). Loaded at boot via `TuningCatalog`.
- **Where:** `docs/c2-content/tuning/archetype-*.json`; `APIFramework/Systems/Tuning/TuningCatalog.cs`.
- **Why a candidate:** The most obvious modder surface. A modder adds a new archetype JSON and the engine consumes it without code changes. Only thing missing: a documented schema and a "register a new archetype" path that adds to the cast bible's archetype enum.
- **Stability:** stabilizing (used by 7 systems; consistent file pattern; schema validation in tests).
- **Source packet:** WP-3.2.5.

### MAC-002: NarrativeEventKind enum

- **What:** Discriminated union of narrative event kinds (`Choked`, `SlippedAndFell`, `StarvedAlone`, `Died`, `BereavementWitnessed`, `ChoreRefused`, `ChoreCompleted`, `MicrowaveCleaned`, `ItemBroken`, `GlassShattered`, `RescueAttempted`, `RescueSucceeded`, `RescueFailed`, plus 10+ from Phase 1–2). Consumed by chronicle, persistent memory, dialog corpus, event log.
- **Where:** `APIFramework/Components/NarrativeEventKind.cs` (or equivalent).
- **Why a candidate:** A modder adding a sneeze mechanic adds `Sneezed` to the enum and registers handlers downstream. Today this is a closed enum; the Mod API would expose it as an open registry.
- **Stability:** stable (additive-only growth across Phase 1, 2, 3; no breaking changes).
- **Source packet:** WP-3.0.0 + cumulative.

### MAC-003: SoundTriggerKind enum

- **What:** Audio trigger vocabulary (`Cough`, `ChairSqueak`, `BulbBuzz`, `Footstep`, `SpeechFragment`, `Crash`, `Glass`, `Thud`, `Heimlich`, `DoorUnlock`). Engine emits via `SoundTriggerBus`; Unity host synthesises.
- **Where:** `APIFramework/Audio/SoundTriggerKind.cs`; `APIFramework/Audio/SoundTriggerBus.cs`.
- **Why a candidate:** A modder adds `Sneeze` audio by extending the enum, mapping it to a clip in the host's synth catalog, and emitting it from their new SneezeSystem. Mod API would formalize the registration.
- **Stability:** stabilizing (10 entries; consistent emit pattern; one host consumer).
- **Source packet:** WP-3.2.1.

### MAC-004: Animation state vocabulary

- **What:** Silhouette animation states (`Idle`, `Walking`, `Eating`, `Drinking`, `Working`, `Crying`, `CoughingFit`, `Heimlich`). Driven by NPC state; rendered by silhouette renderer.
- **Where:** `ECSUnity/Assets/Scripts/Render/SilhouetteAnimator.cs` (or equivalent); `APIFramework/Components/AnimationStateComponent.cs`.
- **Why a candidate:** A modder adding a sneeze adds a `Sneezing` state with its own sprite frames and frame-timing. Mod API would formalize state registration + asset binding.
- **Stability:** fresh (recently expanded in 3.2.6; pattern still settling — 4.0.E will polish).
- **Source packet:** WP-3.2.6.

### MAC-005: SimConfig section pattern

- **What:** Each engine subsystem owns a section in `SimConfig.json` (`lifeState`, `choking`, `bereavement`, `slipAndFall`, `lockout`, `livemutation`, `pathfindingCache`, `physics`, `chores`, `rescue`, `soundTriggers`, ~12+ total). Loaded at boot; runtime-tunable.
- **Where:** `docs/c2-content/SimConfig.json`; per-system config readers.
- **Why a candidate:** Modders add their own subsystem config sections. Pattern is well-established and the loader is uniform.
- **Stability:** stable (used across all Phase 1–3 systems without breaking changes).
- **Source packet:** Phase 0 substrate, cumulative growth.

### MAC-006: WorldStateDto schema (versioned)

- **What:** The serialized world state. Consumed by Unity projector, save/load, AI agent prompts. Schema-versioned per `SCHEMA-ROADMAP.md` (currently v0.5).
- **Where:** `Warden.Contracts/SchemaValidation/world-state.schema.json`; `APIFramework/Projection/WorldStateDto.cs`.
- **Why a candidate:** This is the canonical "what the world looks like right now." Mod-aware tooling (a save inspector, a custom analytics dashboard, an external visualizer) consumes this. Mod API would formalize schema-extension rules so modders' new components round-trip cleanly.
- **Stability:** stable (versioned; additive-minor discipline held across Phase 1–3).
- **Source packet:** WP-3.2.0 hardened; cumulative.

### MAC-007: IWorldMutationApi (structural mutation)

- **What:** The structural mutation contract — adding/removing entities, walls, doors; moving structural items. Emits `StructuralChangeEvent` to `StructuralChangeBus`; pathfinding cache invalidates on emit.
- **Where:** `APIFramework/World/IWorldMutationApi.cs`; `APIFramework/World/StructuralChangeBus.cs`.
- **Why a candidate:** A modder building a new build-mode tool, a runtime hazard spawner, or a procedural-room-generator uses this. Today it's an internal contract; Mod API would publish it as a stable interface.
- **Stability:** stabilizing (used by build mode, dev console, scenario verbs; one new consumer per phase).
- **Source packet:** WP-3.0.4.

### MAC-008: AsciiMapProjector (read-side observability)

- **What:** Pure C# Unicode-box-drawing floor-plan projector. Renders `WorldStateDto` to a text floor plan readable by humans + LLMs.
- **Where:** `Warden.Telemetry/AsciiMap/AsciiMapProjector.cs`.
- **Why a candidate:** A modder building a console-mode viewer, an AI integration, or a debug tool reuses this directly. Already pure; already strips at ship per `#if WARDEN`. Possibly graduates as a Warden-side public API rather than a runtime API.
- **Stability:** stabilizing (one consumer at first; second consumer in Haiku slab factory; pattern proven).
- **Source packet:** WP-3.0.W.

---

## Pending Mod API surfaces — Phase 4 wave 1 candidates

These will land in the foundational polish wave (WP-4.0.A through WP-4.0.H). Each packet ships with a "Mod API surface" section that adds an entry here.

### MAC-009: ICameraRenderPass (custom render passes)

- **What:** Interface for modder-registered camera render passes (post-process effects layered on top of the pixel-art shader). A modder adds a CRT-scanline effect, a film-grain effect, an outline effect, etc.
- **Where:** WP-4.0.A introduces. Likely `ECSUnity/Assets/Scripts/Render/ICameraRenderPass.cs`.
- **Why a candidate:** Visual mods are one of the most common mod categories in management sims. Render-pass extension is the surface they need.
- **Stability:** fresh (lands with WP-4.0.A).
- **Source packet:** WP-4.0.A (pending).

### MAC-010: PersonalSpaceComponent + spatial-behavior tuning

- **What:** Component family for soft NPC repulsion. Modders add per-archetype radius / repulsion-strength tuning. Could later host introvert/extrovert traits, social-distancing behavior, illness-distancing.
- **Where:** WP-4.0.B introduces. `APIFramework/Components/PersonalSpaceComponent.cs`; `APIFramework/Systems/Movement/SpatialBehaviorSystem.cs`.
- **Why a candidate:** Spatial-behavior tuning is a natural per-archetype data extension; couples to MAC-001.
- **Stability:** fresh (lands with WP-4.0.B).
- **Source packet:** WP-4.0.B (pending).

### MAC-011: BuildFootprintComponent + footprint-aware drop

- **What:** Per-prop occupancy footprint (the tile area a prop covers, surface heights, stack-on-top compatibility). The substrate the build mode v2 BUG-001 fix needs.
- **Where:** WP-4.0.C introduces. `APIFramework/Components/BuildFootprintComponent.cs`; build-mode drop logic consumes.
- **Why a candidate:** Modders adding new props need a uniform way to declare footprint. Future custom-furniture mods, custom-room-template mods all touch this.
- **Stability:** fresh (lands with WP-4.0.C).
- **Source packet:** WP-4.0.C (pending).

### MAC-012: Particle effect vocabulary

- **What:** Trigger vocabulary for visual particle effects (steam from coffee, smoke from fire, sparks, dust kicked up). Engine emits triggers; Unity host spawns/manages particle systems. Parallel structure to SoundTriggerKind (MAC-003).
- **Where:** WP-4.0.H introduces. `APIFramework/Visual/ParticleTriggerKind.cs`; `Warden.Telemetry/...` host consumer.
- **Why a candidate:** Visual mods (a sneeze mod adds sneeze-mist particles; a fire mod adds custom flame), parallel to audio mods.
- **Stability:** fresh (lands with WP-4.0.H).
- **Source packet:** WP-4.0.H (pending).

---

## Maintenance notes

- **Adding entries:** When a Phase 4+ packet ships its "Mod API surface" section, append candidate entries here with the next MAC-NNN id (zero-padded, monotonic).
- **Updating stability:** When a candidate gains a second or third consumer, bump it from *fresh* → *stabilizing*. When it has been used widely without breaking changes for 5+ packets, bump to *stable*.
- **Graduating:** When the Mod API sub-phase opens, *stable* candidates are the first batch folded into the public contract. *Stabilizing* candidates may follow if their shape is judged settled.
- **Killing:** Only if the underlying surface is removed from the engine. Replace the body with `Killed: YYYY-MM-DD — <reason>`. Keep the id reserved.
- **No reorganization without reason.** This ledger is a flat append-only log; do not reorganize by category, alphabet, or stability unless the ledger has grown past 50 entries and the disorganization is causing real friction.

This ledger is one of the cheapest forms of architecture work. It costs minutes per packet and pays back when the Mod API sub-phase has a settled list of surfaces to formalize instead of a discovery exercise across the entire codebase.
