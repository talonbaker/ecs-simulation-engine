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
- **Stability:** stable (pattern peer-confirmed by MAC-012 parallel bus; two buses, identical shape, no breaking changes across WP-3.2.1 → WP-4.0.H).
- **Source packet:** WP-3.2.1. **Revised: 2026-05-03 — WP-4.0.H ships ParticleTriggerBus as a parallel peer, demonstrating the bus pattern is settled. Stability bumped *stabilizing* → *stable*.**

### MAC-004: Animation state vocabulary

- **What:** Silhouette animation states (`Idle`, `Walk`, `Eating`, `Drinking`, `Working`, `Crying`, `CoughingFit`, `Heimlich`, + 7 more in enum). Driven by NPC state; rendered by silhouette renderer.
- **Where:** `ECSUnity/Assets/Scripts/Render/SilhouetteAnimator.cs`; `ECSUnity/Assets/Scripts/Animation/NpcAnimatorController.cs`; `APIFramework/Components/NpcAnimationState.cs`; per-state data in `docs/c2-content/animation/visual-state-catalog.json` (MAC-013).
- **Why a candidate:** A modder adding a sneeze adds a `Sneezing` state with its own sprite frames and frame-timing. The catalog (MAC-013) absorbs per-state data without code changes; the enum and Animator controller need extending. Mod API would formalize state registration + asset binding.
- **Stability:** stabilizing (2 consumers: WP-3.2.6 substrate + WP-4.0.E polish layer with data-driven catalog; pattern now settled around SilhouetteAnimator + NpcVisualStateCatalog).
- **Source packet:** WP-3.2.6. **Revised: 2026-05-03 — WP-4.0.E adds second consumer (SilhouetteAnimator + NpcVisualStateCatalog); stability bumped from *fresh* to *stabilizing*.**

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

### MAC-009: URP ScriptableRendererFeature pattern (standardized)

- **What:** Unity's documented `ScriptableRendererFeature` extension surface. Modders implement a `ScriptableRendererFeature` subclass and add it to the URP renderer data asset's Feature list. Each feature owns `ScriptableRenderPass` instances that inject into URP's frame at named injection points.
- **Where:** Built into URP (`com.unity.render-pipelines.universal`). Project's URP setup in `ECSUnity/Assets/Settings/URP-PipelineAsset.asset` + `URP-PipelineAsset_Renderer.asset`. First in-project consumer: `PixelArtRendererFeature` from WP-4.0.A1.
- **Why a candidate:** Visual mods (CRT scanline, film grain, outline pass, custom emotion-cue overlay shader) are a common mod category. URP's ScriptableRendererFeature is the standard, documented, transferable extension surface. Better than a custom interface — modders learn a Unity-standard skill, we don't carry a bespoke maintenance burden.
- **Stability:** fresh (foundation lands with WP-4.0.A; first consumer with WP-4.0.A1; bumps to *stabilizing* when a third consumer lands — likely WP-4.0.D/E adding a rim-light or tile-edge pass).
- **Source packets:** WP-4.0.A (foundation — URP migration), WP-4.0.A1 (first consumer — pixel-art feature).
- **Revision history:** *Originally drafted 2026-05-02 as "ICameraRenderPass (custom interface)"; revised same day after Talon's URP-from-the-start architectural call. Custom interface deprecated in favor of URP standard.*

### MAC-010: PersonalSpaceComponent + spatial-behavior tuning

- **What:** Component family for soft NPC repulsion. Modders add per-archetype radius / repulsion-strength tuning. Could later host introvert/extrovert traits, social-distancing behavior, illness-distancing.
- **Where:** `APIFramework/Components/PersonalSpaceComponent.cs`; `APIFramework/Systems/Spatial/SpatialBehaviorSystem.cs`; `APIFramework/Systems/Spatial/SpatialBehaviorInitializerSystem.cs`; tuning JSON at `docs/c2-content/archetypes/archetype-personal-space.json`.
- **Why a candidate:** Spatial-behavior tuning is a natural per-archetype data extension; couples to MAC-001.
- **Stability:** landed 2026-05-02 (WP-4.0.B).
- **Source packet:** WP-4.0.B (landed 2026-05-02).

### MAC-011: BuildFootprintComponent + footprint-aware drop

- **What:** Per-prop occupancy footprint (the tile area a prop covers, surface heights, stack-on-top compatibility). The substrate the build mode v2 BUG-001 fix needs.
- **Where:** WP-4.0.C introduces. `APIFramework/Components/BuildFootprintComponent.cs`; build-mode drop logic consumes.
- **Why a candidate:** Modders adding new props need a uniform way to declare footprint. Future custom-furniture mods, custom-room-template mods all touch this.
- **Stability:** fresh (landed with WP-4.0.C; shipped to staging 2026-05-02).
- **Source packet:** WP-4.0.C (shipped 2026-05-02).

### MAC-012: Particle effect vocabulary

- **What:** Trigger vocabulary for visual particle effects (steam from coffee, smoke from fire, sparks, dust kicked up, water splash, bulb flicker, cleaning mist, speech bubble puff, breath puff). Engine emits typed triggers via `ParticleTriggerBus`; Unity host spawns VFX Graph instances at trigger location. Parallel structure to `SoundTriggerKind` (MAC-003). JSON catalog at `docs/c2-content/visual/particle-trigger-catalog.json` maps enum → VFX asset + spawn params; modder-extensible.
- **Where:** `APIFramework/Systems/Visual/ParticleTriggerKind.cs`; `APIFramework/Systems/Visual/ParticleTriggerBus.cs`; `APIFramework/Systems/Visual/ParticleTriggerEvent.cs`; `ECSUnity/Assets/Scripts/Render/Visual/ParticleTriggerSpawner.cs`; `ECSUnity/Assets/Scripts/Render/Visual/ParticleTriggerCatalog.cs`.
- **Why a candidate:** Visual mods (a sneeze mod adds sneeze-mist particles; a fire mod adds custom flame), parallel to audio mods. Modders extend the enum, author a VFX Graph asset, register in catalog JSON — no engine code change.
- **Stability:** fresh (one consumer: WP-4.0.H; shipped 2026-05-03).
- **Source packet:** WP-4.0.H. **Revised: 2026-05-03 — shipped. 10 particle kinds, 5 immediate producers (Sparks, WaterSplash, BulbFlicker, CleaningMist, SpeechBubblePuff), 5 stubs. Catalog JSON at `docs/c2-content/visual/particle-trigger-catalog.json`.**

### MAC-013: NPC visual state catalog (animation states + chibi cues + transitions)

- **What:** JSON catalog (`docs/c2-content/animation/visual-state-catalog.json`) describing per-state visual treatment (frame timing, accent color, cue affinity), per-cue rendering parameters (sprite asset, anchor offset, fade altitude, scale multiplier), and per-pair state transition smoothing (intermediate frames, total duration). Modders adding new animation states + emotion cues extend the JSON.
- **Where:** WP-4.0.E introduces. `docs/c2-content/animation/visual-state-catalog.json`; `ECSUnity/Assets/Scripts/Render/NpcVisualStateCatalogLoader.cs`; consumed by `SilhouetteAnimator` + `ChibiEmotionPopulator`.
- **Why a candidate:** A sneeze mod adds the `Sneezing` state with frame timing + a sneeze cue; the catalog absorbs both without code changes. Pattern is consistent with MAC-001 (per-archetype tuning) and MAC-005 (SimConfig sections) — data-driven extension.
- **Stability:** fresh (landed with WP-4.0.E, 2026-05-03).
- **Source packet:** WP-4.0.E. **Revised: 2026-05-03 — shipped. Catalog has 15 state entries, 9 cue entries, 6 transition entries. `IconKind` extended with `RedFaceFlush` + `GreenFaceNausea` (enum values 9–10).**

### MAC-014: Room visual identity catalog (floor / wall / door materials per RoomKind)

- **What:** JSON catalog (`docs/c2-content/world-definitions/room-visual-identity.json`) mapping `RoomKind` to default floor/wall/door materials + boundary trim. Modders adding new room categories (e.g., "Server Room", "Reception") add an entry; modders authoring custom material packs reference custom materials.
- **Where:** WP-4.0.D introduces. `docs/c2-content/world-definitions/room-visual-identity.json`; `ECSUnity/Assets/Scripts/Render/RoomVisualIdentityLoader.cs`.
- **Why a candidate:** Visual mods + room-type mods are common categories. Data-driven extension consistent with MAC-001 / MAC-005 / MAC-013.
- **Stability:** fresh (lands with WP-4.0.D).
- **Source packet:** WP-4.0.D (pending).

### MAC-015: Author-mode palette + IWorldMutationApi authoring extensions

- **What:** Modder-extensible palette of in-game authoring tools (rooms / light sources / light apertures / NPC archetypes), plus the `IWorldMutationApi` operations that back them: `CreateRoom`, `DespawnRoom` (with `RoomDespawnPolicy`), `CreateLightSource`, `TuneLightSource`, `CreateLightAperture`, `DespawnLight`, `CreateNpc`, `DespawnNpc`, `RenameNpc`. Catalog JSON at `docs/c2-content/build/author-mode-palette.json` is the data extension surface (9 room kinds, 9 light kinds, 4 aperture sizes, archetype list auto-discovered from `TuningCatalog`). Modders adding a new room kind extend the palette JSON + the loader's `RoomKind` parser + the room visual identity catalog (MAC-014) — three coordinated additions, all data-driven.
- **Where:** `APIFramework/Mutation/IWorldMutationApi.cs` (extensions); `APIFramework/Mutation/WorldMutationApi.cs` (5-arg constructor with cast deps); `APIFramework/Mutation/RoomDespawnPolicy.cs`; `APIFramework/Build/AuthorModePaletteData.cs`; `APIFramework/Build/AuthorModePaletteLoader.cs`; `APIFramework/Bootstrap/CastNamePool.cs`; `docs/c2-content/build/author-mode-palette.json`; `ECSUnity/Assets/Scripts/BuildMode/AuthorModeController.cs`.
- **Why a candidate:** Author-mode tools are the most direct community-contribution surface — a level designer authoring a custom office is the canonical mod use case Talon called out 2026-05-03. Palette is data-driven (consistent with MAC-001 / MAC-005 / MAC-013 / MAC-014 / MAC-016). Mutation operations build on MAC-007 (`IWorldMutationApi`) — same emission discipline (StructuralChangeEvent on bus where pathfinding-relevant), same fail-closed validation.
- **Stability:** fresh (engine substrate landed with WP-4.0.J + WP-4.0.K, 2026-05-03; user-facing palette UI deferred to Editor follow-up).
- **Source packets:** WP-4.0.J (room/light/aperture mutations + AuthorModeController + palette catalog), WP-4.0.K (NPC mutations + CastNamePool + NameHint round-trip).

### MAC-016: World-definition file format (load + write round-trip)

- **What:** JSON schema describing a complete authorable scene — floors, rooms, light sources, light apertures, NPC slots (with `nameHint` and `archetypeHint`), anchor objects. `WorldDefinitionLoader` spawns from JSON; `WorldDefinitionWriter` (WP-4.0.I) serializes the live ECS world back to JSON. Round-trip-validated against `office-starter.json` and `playtest-office.json`. Modders authoring custom scenes (or scene packs) add JSON files under `docs/c2-content/world-definitions/`; the existing dev-console / author-mode Save toolbar makes them live without recompile.
- **Where:** `APIFramework/Bootstrap/WorldDefinitionLoader.cs`; `APIFramework/Bootstrap/WorldDefinitionWriter.cs`; `APIFramework/Bootstrap/WorldDefinitionDto.cs`; `docs/c2-content/world-definitions/*.json`.
- **Why a candidate:** The clearest modder surface in the project. A "custom office" mod is a single JSON file; a "scene pack" mod is a directory of them. Pattern is consistent with MAC-001 / MAC-005 / MAC-013 / MAC-014 / MAC-017 — data-driven, schema-validated, registered through file discovery.
- **Stability:** stabilizing (loader stable since Phase 1; writer + round-trip discipline lands with WP-4.0.I; second consumer = author-mode UI in WP-4.0.J Save toolbar).
- **Source packets:** Phase 1 substrate (loader); WP-4.0.I (writer + round-trip discipline); WP-4.0.K (NameHint round-trip — repaired the loader's silent dropping of the field).

### MAC-017: Cast name data catalog (probabilistic six-tier name + title generator)

- **What:** JSON catalog (`docs/c2-content/cast/name-data.json`) feeding the `CastNameGenerator` library. Six-tier rarity model — Common 55% / Uncommon 27% / Rare 12% / Epic 4% / Legendary 1.5% / Mythic 0.5% — producing first name + surname + optional title with per-tier structure (vanilla / fused / hyphenated / corp-titled / divine-rooted / "Executive VP Kratos, The Doom-Slayer"). Tier thresholds are JSON-tunable; modders can tilt rarity curves without code changes. A "fantasy office" mod replaces the catalog content (elves / dwarves / wizards) and the engine consumes them transparently. The tier-on-result also seeds the future "reroll for a better hire" loot-box mechanic — the deterministic seedable `Generate(Random)` overload is the seam.
- **Where:** `docs/c2-content/cast/name-data.json`; `APIFramework/Cast/CastNameDataLoader.cs`; `APIFramework/Cast/CastNameGenerator.cs`; `APIFramework/Cast/CastNameTier.cs`; `APIFramework/Cast/CastNameResult.cs`.
- **Why a candidate:** Names + titles are universal across mods. Pattern is consistent with MAC-001 / MAC-005 / MAC-013 / MAC-014 — data-driven, schema-validated. The companion `badgeFlair` and `departmentStamps` blocks are preserved in the catalog for the future badge-generation packet (see WP-4.0.M followups).
- **Stability:** fresh (landed with WP-4.0.M, 2026-05-03; second consumer = WP-4.0.K NPC palette auto-naming when that packet lands).
- **Source packet:** WP-4.0.M.
- **Lineage:** ported from Talon's HTML/JS roster generator at `~/talonbaker.github.io/name-face-gen/` (`data.js` as source-of-truth).

---

## Maintenance notes

- **Adding entries:** When a Phase 4+ packet ships its "Mod API surface" section, append candidate entries here with the next MAC-NNN id (zero-padded, monotonic).
- **Updating stability:** When a candidate gains a second or third consumer, bump it from *fresh* → *stabilizing*. When it has been used widely without breaking changes for 5+ packets, bump to *stable*.
- **Graduating:** When the Mod API sub-phase opens, *stable* candidates are the first batch folded into the public contract. *Stabilizing* candidates may follow if their shape is judged settled.
- **Killing:** Only if the underlying surface is removed from the engine. Replace the body with `Killed: YYYY-MM-DD — <reason>`. Keep the id reserved.
- **No reorganization without reason.** This ledger is a flat append-only log; do not reorganize by category, alphabet, or stability unless the ledger has grown past 50 entries and the disorganization is causing real friction.

This ledger is one of the cheapest forms of architecture work. It costs minutes per packet and pays back when the Mod API sub-phase has a settled list of surfaces to formalize instead of a discovery exercise across the entire codebase.
