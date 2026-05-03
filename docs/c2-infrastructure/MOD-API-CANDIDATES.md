# Mod API Candidates

This file tracks mod API surface that has been designed or partially implemented and
is a strong candidate for formal stabilization in a future Mod SDK release.

Entries follow the format: **MAC-NNN: Short name**

---

## MAC-001: WorldMutationApi (IWorldMutationApi)
- **What:** Runtime interface for moving NPCs, spawning/despawning entities, and modifying room bounds. Covers the core "player can affect the simulation" surface.
- **Where:** `APIFramework/Core/IWorldMutationApi.cs`
- **Why a candidate:** Player mods that add new interaction verbs need a stable mutation surface.
- **Stability:** maturing (in use since WP-3.1.D).
- **Source packet:** WP-3.1.D

---

## MAC-002: WorldStateDto schema
- **What:** The serialized snapshot of simulation state emitted each tick (`Warden.Contracts.Telemetry`).
- **Where:** `Warden.Contracts/Telemetry/WorldStateDto.cs`
- **Why a candidate:** External tools (dashboards, analytics, replays) consume this over JSONL.
- **Stability:** maturing (schema versioned since WP-1.0.A).
- **Source packet:** WP-1.0.A

---

## MAC-003: SimConfig (SimConfigAsset)
- **What:** ScriptableObject exposing tick rate, world-definition path, and renderer toggles.
- **Where:** `ECSUnity/Assets/Scripts/Engine/SimConfigAsset.cs`
- **Why a candidate:** Mod launchers and test harnesses need a stable way to configure the sim.
- **Stability:** maturing.
- **Source packet:** WP-3.1.A

---

## MAC-004: WorldDefinition schema (world-definition.json)
- **What:** JSON format for declaring rooms, light sources, NPC slots, and anchor objects.
- **Where:** `docs/c2-infrastructure/schemas/world-definition.schema.json`; `APIFramework/Bootstrap/WorldDefinitionLoader.cs`
- **Why a candidate:** Custom map mods need a stable world-definition format.
- **Stability:** maturing (schema versioned).
- **Source packet:** WP-1.7.A

---

## MAC-005: Build Palette Catalog (build-palette-catalog.json)
- **What:** JSON catalog of placeable objects exposed in build mode. Categories: Structural, Furniture, Props, NamedAnchor.
- **Where:** `docs/c2-content/build-palette-catalog.json`; `ECSUnity/Assets/Scripts/Build/BuildPaletteCatalog.cs`
- **Why a candidate:** Content mods adding new placeable items need a stable catalog format.
- **Stability:** maturing (in use since WP-3.1.D).
- **Source packet:** WP-3.1.D

---

## MAC-006: SilhouetteAssetCatalog (silhouette-catalog.json)
- **What:** JSON + ScriptableObject mapping NPC archetype IDs to silhouette sprite layers (body, hair, headwear, item) and dominant tint color.
- **Where:** `docs/c2-content/silhouette-catalog.json`; `ECSUnity/Assets/Scripts/Render/SilhouetteAssetCatalog.cs`
- **Why a candidate:** Visual mods replacing NPC appearances need a stable sprite-slot format.
- **Stability:** maturing.
- **Source packet:** WP-3.1.B

---

## MAC-007: RoomCategory enum
- **What:** Enum of room functional categories (`CubicleGrid`, `Bathroom`, `Hallway`, etc.) used throughout the engine and render layers.
- **Where:** `APIFramework/Components/RoomCategory.cs`
- **Why a candidate:** Room-type mods extending the category vocabulary need a stable base enum.
- **Stability:** maturing.
- **Source packet:** WP-1.0.B

---

## MAC-008: SoundTriggerBus
- **What:** In-process event bus for audio cue emission (`SoundTriggerEvent`). Systems emit sound events; Unity audio layer subscribes.
- **Where:** `APIFramework/Systems/Audio/SoundTriggerBus.cs`
- **Why a candidate:** Sound-pack mods need to subscribe to the bus to trigger custom audio.
- **Stability:** maturing (in use since WP-3.2.1).
- **Source packet:** WP-3.2.1

---

## MAC-009: ArchetypeSchedules (archetype-schedules.json)
- **What:** Per-archetype JSON schedule definitions (work hours, break slots, chore frequency).
- **Where:** `docs/c2-content/data/archetype-schedules.json`
- **Why a candidate:** Behavioral mods adjusting NPC daily rhythms need a stable schedule format.
- **Stability:** maturing.
- **Source packet:** WP-2.2.A

---

## MAC-010: ChronicleEntry schema
- **What:** Persistent narrative chronicle format for NPC memory events.
- **Where:** `Warden.Contracts/Telemetry/ChronicleEntryDto.cs`
- **Why a candidate:** Story mods reading or replaying the chronicle need a stable schema.
- **Stability:** fresh.
- **Source packet:** WP-1.9.A

---

## MAC-011: DevConsole scenario verbs
- **What:** Named scenario verbs callable via the dev console (`scenario choke`, `scenario slip`, etc.) for test and modding hooks.
- **Where:** `ECSUnity/Assets/Scripts/UI/DevConsole/DevConsoleScenarioCommands.cs`
- **Why a candidate:** QA and modders need stable scenario-trigger names.
- **Stability:** maturing.
- **Source packet:** WP-PT.1

---

## MAC-012: ChibiEmotionSlot API
- **What:** Emotion-slot API for NPC emotional state visualization (panic, irritation, sleep, etc.) via chibi bubble icons.
- **Where:** `ECSUnity/Assets/Scripts/Render/ChibiEmotionSlot.cs`; `ECSUnity/Assets/Scripts/Render/ChibiEmotionPopulator.cs`
- **Why a candidate:** Emotional state visual mods need a stable slot API to inject custom icons.
- **Stability:** fresh.
- **Source packet:** WP-4.0.E (NPC visual state communication)

---

## MAC-013: NpcVisualStateCatalog (npc-visual-state-catalog.json)
- **What:** JSON catalog mapping NPC visual states (emotion categories, severity thresholds) to sprite assets and animation parameters.
- **Where:** `docs/c2-content/npc-visual-state-catalog.json`
- **Why a candidate:** Visual mods replacing NPC emotion icons need a stable catalog format.
- **Stability:** fresh.
- **Source packet:** WP-4.0.E

---

## MAC-014: Room visual identity catalog (JSON-driven)
- **What:** JSON catalog mapping `RoomCategory` to floor/wall/door materials + trim. Modders adding a new room category (e.g., "Server Room", "Reception") add an entry; modders authoring custom material packs (e.g., a "concrete brutalist" material pack) reference custom materials in catalog overrides.
- **Where:** `docs/c2-content/world-definitions/room-visual-identity.json`; `ECSUnity/Assets/Scripts/Render/RoomVisualIdentityLoader.cs`.
- **Why a candidate:** Visual mods + room-type mods are common categories. Data-driven extension consistent with MAC-001 / MAC-005 / MAC-013.
- **Stability:** fresh (lands with WP-4.0.D).
- **Source packet:** WP-4.0.D.
