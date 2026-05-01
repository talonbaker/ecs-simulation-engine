# WP-1.7.A — World Bootstrap: world-definition.json + Loader

**Tier:** Sonnet
**Depends on:** WP-1.1.A (rooms as engine entities — merged), WP-1.2.A (light sources, apertures — merged), WP-1.4.A (social components — merged for NPC entity templates).
**Parallel-safe with:** WP-1.5.A (Lighting-to-drive — different file footprint), WP-1.2.B (Spatial projector — different file footprint).
**Timebox:** 105 minutes
**Budget:** $0.50

---

## Goal

Land the world-bootstrap layer that lets the engine boot from a data file rather than from hardcoded `EntityTemplates`. Three pieces:

1. **`world-definition.schema.json`** — a new schema describing the shape of a world definition: floor layout, rooms, light sources, light apertures, named-anchor metadata, starting state per anchor (smell, stains, notes the world bible commits to). Lives alongside the other schemas in `docs/c2-infrastructure/schemas/`.

2. **`WorldDefinitionLoader`** — a new class in `APIFramework` that reads a `world-definition.json` file at engine boot, validates it against the schema, and instantiates the corresponding entities (rooms, light sources, light apertures, NPC slots). The loader is the new world-spawn path; it replaces hardcoded `EntityTemplates.SpawnDefaultOffice()`-style flows for non-test scenarios. Test scenarios continue to use `EntityTemplates` directly for tight setups.

3. **A starter `world-definition.json`** that describes the office from the world bible — three floors, the named anchors (the Microwave, the Window, the Smoking Bench, etc.), light sources representative of the era. **The starter is illustrative and authored by the Sonnet from the bible's named-anchor list**, not exhaustively populated. Talon (or later content packets) will iterate on the world content. The starter exists to validate the loader and provide a real-world data point.

What this packet does **not** do: spawn NPCs (cast generator is WP-1.8.A); persist any chronicle state (chronicle is WP-1.9.A); render or visualize the world (visual is deferred per the aesthetic bible); modify the wire-format schema (this is a *new* schema for a *new* file type, separate from `world-state.schema.json`).

---

## Reference files

- `docs/c2-content/DRAFT-world-bible.md` — **read first**. The named anchors and the three-floor structure this packet's loader populates.
- `docs/c2-content/DRAFT-aesthetic-bible.md` — light source kinds and states the loader instantiates.
- `docs/c2-infrastructure/00-SRD.md` §8.6 (visual target — engine exposes spatial structure).
- `docs/c2-infrastructure/work-packets/_completed/WP-1.1.A.md` — confirms `RoomComponent`, `RoomTag`, `EntityTemplates.Room()` factory exist.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.2.A.md` — confirms `LightSourceComponent`, `LightApertureComponent`, factories exist.
- `docs/c2-infrastructure/SCHEMA-ROADMAP.md` — for understanding existing schema versioning conventions.
- `docs/c2-infrastructure/schemas/world-state.schema.json` — pattern reference for the new schema. The new schema follows the same draft, formatting, and `additionalProperties: false` discipline.
- `Warden.Contracts/SchemaValidation/SchemaValidator.cs` — the validator the loader uses.
- `APIFramework/Components/EntityTemplates.cs` — existing factory site. The loader uses these factories, not raw component creation.
- `APIFramework/Core/SimulationBootstrapper.cs` — the boot path. Loader is invoked here when a world-definition path is provided.
- `APIFramework/Core/SeededRandom.cs` — RNG for any random world variation (e.g., randomly placing the supply closet's "graveyard of obsolete office equipment").
- `Warden.Contracts/JsonOptions.cs` — the canonical JsonSerializerOptions. Loader uses these for parsing.

## Non-goals

- Do **not** spawn NPCs. The world definition reserves "NPC slots" (positions where the cast generator will place NPCs) but does not populate them. Cast generator is WP-1.8.A.
- Do **not** add chronicle state to the world definition. Spill-stays-spilled is v0.4 chronicle work.
- Do **not** modify `world-state.schema.json` (the wire format). The new schema is a separate file for a separate purpose.
- Do **not** modify any file under `Warden.Telemetry/`. The loader is engine-side; projection comes from the projector when entities exist.
- Do **not** modify any file under `Warden.Contracts/` other than possibly `SchemaValidation/world-definition.schema.json` if the loader uses an embedded resource for runtime validation.
- Do **not** make the loader the *only* world-spawn path. `EntityTemplates`-driven test setups must continue to work — the loader is an *additional* path, invoked when a definition file is supplied.
- Do **not** populate every room and named anchor exhaustively in the starter file. A representative subset (≥ 6 rooms covering all three floors, ≥ 4 named anchors, ≥ 8 light sources, ≥ 2 apertures) is enough to validate the loader.
- Do **not** invent named anchors not in the world bible. The starter only includes anchors the bible names.
- Do **not** introduce a NuGet dependency.
- Do **not** use `System.Random`. `SeededRandom` only.
- Do **not** retry, recurse, or "self-heal" on validation failure. If the world definition fails validation, throw a clear exception with the validator's error list. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)

---

## Design notes

### Schema shape

The new schema `world-definition.schema.json` is structurally similar to `world-state.schema.json` but represents **authoring-time** content (what the world *starts as*) rather than **runtime** state. Top-level fields:

- `schemaVersion` — string const, starts at `"0.1.0"`. (This schema gets its own version line independent of the wire format's.)
- `worldId` — string, identifier for this world.
- `name` — string, human-readable name.
- `seed` — integer, the RNG seed the engine should boot with when loading this world.
- `floors[]` — list of floor objects (basement, first, top, exterior). Each carries `id`, `name`, `floorEnum` (matching the wire-format `BuildingFloor`).
- `rooms[]` — list of room definitions. Each carries everything `RoomComponent` needs: `id` (UUID), `name`, `category`, `floorId`, `bounds`, `initialIllumination`, plus authoring-only fields: `namedAnchorTag` (optional — e.g., `"the-microwave"`, `"the-window"`), `description` (optional, max 280 chars — the world bible's small bundle of state), `smellTag` (optional — `"old-microwave"`, `"copy-machine-warmth"`), `notesAttached[]` (optional — a list of post-it-style strings the world bible commits to).
- `lightSources[]` — list of source definitions matching `LightSourceComponent` shape, plus `roomId` reference and `initialIntensity`.
- `lightApertures[]` — list of aperture definitions matching `LightApertureComponent` shape.
- `npcSlots[]` — list of NPC spawn positions. Each carries `id`, `position` (room-id-or-coordinate), and optional `archetypeHint` (a string like `"the-vent"`, `"the-cynic"` — the cast generator reads this hint when populating). At v0.1 of the world-definition schema, slots are *positions only* — actual NPC spawn is the cast generator's job.
- `objectsAtAnchors[]` — list of authored objects placed at named anchors (the box of floppy disks in the supply closet, the tupperware in the back of the fridge). Each carries `id`, `roomId`, `description`, `physicalState` (smaller enum: `present`, `present-degraded`, `present-greatly-degraded`, etc.).

`additionalProperties: false` everywhere. Every array has explicit `maxItems`. Every numeric field has explicit `minimum`/`maximum`.

### The starter content

The starter `world-definition.json` populates:

- All three floors per the bible.
- ≥ 6 rooms covering each floor (e.g., `first-floor-breakroom`, `first-floor-cubicle-grid-east`, `first-floor-it-closet`, `top-floor-conference`, `basement-shipping`, `outdoor-parking-lot`).
- ≥ 4 named anchors from the bible: the Microwave, the Window, the Fridge, the Supply Closet, the IT Closet, the Parking Lot, the Smoking Bench, the Conference Room, Cubicle 12. Pick at least 4. Each gets its `description`, `smellTag`, and 0–3 `notesAttached` strings drawn from the bible's flavor.
- ≥ 8 light sources distributed across floors: overhead fluorescents in cubicle areas, desk lamps in select offices, the IT closet's LED bank, the breakroom's flickering strip.
- ≥ 2 apertures: the first-floor north Window (the bible's gossip spot), one or two top-floor office windows.
- ≥ 5 NPC slots positioned across floors, with archetype hints from the bible's archetype catalog (`the-vent`, `the-hermit`, `the-climber`, etc.). The cast generator will spawn into these.
- Objects at anchors: the box of floppy disks (supply closet), the year-old tupperware (fridge), the laminated NO FIGHTING sign (parking lot), the dust on Cubicle 12 (Mark's empty cubicle).

The starter is **illustrative and reviewable** — content authors will iterate on it. The starter exists so the loader has something real to validate against.

### Loader implementation

`WorldDefinitionLoader.LoadFromFile(string path, EntityManager entityManager, SeededRandom rng) : LoadResult`:

- Reads the file as JSON via `Warden.Contracts.JsonOptions`.
- Validates against the embedded `world-definition.schema.json` via `SchemaValidator`. If validation fails, throws a typed exception (`WorldDefinitionInvalidException`) with the validator's error list. This is the fail-closed boundary.
- Iterates `floors[]` (no entities created — floors are metadata for room grouping).
- Iterates `rooms[]`. For each: calls `EntityTemplates.Room(...)` with the appropriate parameters; if the room has `namedAnchorTag`, adds an additional `NamedAnchorComponent` (new in this packet — small component carrying the tag and description). If the room has `notesAttached[]`, attach each as a `NoteComponent` (new — small, carries the note text).
- Iterates `lightSources[]`. For each: calls `EntityTemplates.LightSource(...)`.
- Iterates `lightApertures[]`. For each: calls `EntityTemplates.LightAperture(...)`.
- Iterates `npcSlots[]`. For each: creates an empty `NpcSlot` entity (a marker entity with `NpcSlotComponent` carrying position and archetype hint). Cast generator later replaces these with full NPC entities.
- Iterates `objectsAtAnchors[]`. For each: creates a `WorldObject` entity (existing pattern) at the specified position, with a description-bearing component.
- Returns a `LoadResult` summarizing entities created (counts per kind, validation status, seed used).

The loader is invoked by `SimulationBootstrapper` when a `--world-definition <path>` flag is supplied. Without the flag, the bootstrapper continues using `EntityTemplates`-based defaults (test scenarios stay unchanged).

### CLI integration

Add a `--world-definition <path>` flag to relevant `ECSCli ai` verbs (`stream`, `replay`). When supplied, the bootstrapper uses the loader. Without it, default templates.

### Determinism

The loader is deterministic given the same input file and the same seed. Tests verify a load produces byte-identical entity counts and component values across two runs.

### Schema-version handshake

`world-definition.schema.json` starts at `"0.1.0"`. It versions independently of the wire-format schema. Future bumps when content shapes evolve (e.g., when chronicle adds `initialChronicleEntries[]` to the world definition).

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| schema | `docs/c2-infrastructure/schemas/world-definition.schema.json` | New schema per Design notes. `schemaVersion: "0.1.0"`. `additionalProperties: false` discipline throughout. |
| schema | `Warden.Contracts/SchemaValidation/world-definition.schema.json` | Embedded-resource mirror of the canonical schema. Sonnet ensures the two stay in sync. |
| code | `Warden.Contracts/SchemaValidation/Schema.cs` (modified) | Add `SchemaVersions.WorldDefinition = "0.1.0"`. |
| code | `APIFramework/Bootstrap/WorldDefinitionLoader.cs` | The loader per Design notes. |
| code | `APIFramework/Bootstrap/WorldDefinitionInvalidException.cs` | Typed exception carrying validation errors. |
| code | `APIFramework/Bootstrap/LoadResult.cs` | Result record — entities created, validation status, seed. |
| code | `APIFramework/Components/NamedAnchorComponent.cs` | `(string Tag, string Description)`. Lives on rooms that are named anchors. |
| code | `APIFramework/Components/NoteComponent.cs` | `(string Text)`. Lives on entities that carry post-it notes (multiple per entity allowed via the existing component-list pattern, or one-note-per-component-with-N-components — Sonnet picks the cleaner approach). |
| code | `APIFramework/Components/NpcSlotComponent.cs` | `(int X, int Y, string? ArchetypeHint, string? RoomId)`. Marker for cast-generator spawn. |
| code | `APIFramework/Components/Tags.cs` (modified) | Add `NpcSlotTag`. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Accept an optional world-definition path; if supplied, invoke the loader during boot. |
| code | `APIFramework/Components/EntityTemplates.cs` (modified) | Add `EntityTemplates.WorldObject(...)` factory if not already present. Add helpers used by the loader. |
| code | `ECSCli/Ai/AiCommand.cs` (modified) | Add `--world-definition <path>` flag plumbing for relevant verbs (`stream`, `replay`, `snapshot`). |
| data | `docs/c2-content/world-definitions/office-starter.json` | The starter world definition per Design notes. ≥ 6 rooms, ≥ 4 named anchors, ≥ 8 light sources, ≥ 2 apertures, ≥ 5 NPC slots, several objects at anchors. Drawn from the world bible. |
| code | `Warden.Contracts.Tests/SchemaValidation/WorldDefinitionSchemaTests.cs` | Schema-validation tests: starter file validates clean; intentionally-malformed examples are rejected with specific reasons. |
| code | `APIFramework.Tests/Bootstrap/WorldDefinitionLoaderTests.cs` | Loader tests: starter file produces expected entity counts; invalid file throws with structured errors; deterministic output across two runs with same seed. |
| code | `APIFramework.Tests/Bootstrap/LoaderIntegrationTests.cs` | Integration: bootstrapper boot with `--world-definition` flag and the starter file produces a running simulation that ticks without error for 100 ticks. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.7.A.md` | Completion note. Standard template. Enumerate (a) what the loader can populate, (b) what's deferred (NPCs, chronicle, custom-authored anchors), (c) starter content scope. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `world-definition.schema.json` declares `schemaVersion: "0.1.0"` and follows the project's schema discipline (every object `additionalProperties: false`, every array has `maxItems`, every numeric has bounds). | unit-test |
| AT-02 | The starter `office-starter.json` validates clean against the schema. | unit-test |
| AT-03 | `WorldDefinitionLoader.LoadFromFile` with the starter file produces ≥ 6 rooms, ≥ 8 light sources, ≥ 2 apertures, ≥ 5 NPC slots in the entity manager. | unit-test |
| AT-04 | Each `RoomTag` entity has a `RoomComponent` with the correct fields from the source JSON. | unit-test |
| AT-05 | Rooms with `namedAnchorTag` populate a `NamedAnchorComponent` with the bible's flavor description (smelltag, notes). | unit-test |
| AT-06 | A malformed file (missing required field, out-of-range numeric) throws `WorldDefinitionInvalidException` with a specific error message including the failing path. | unit-test |
| AT-07 | A file with `npcSlots[].archetypeHint = "not-a-real-archetype"` validates clean (the schema doesn't enforce archetype-hint values; the cast generator decides what to do). | unit-test |
| AT-08 | Two loads of the same file with the same seed produce byte-identical entity component values. | unit-test |
| AT-09 | The bootstrapper boot path with `--world-definition` flag and the starter file produces a simulation that ticks without error for 100 ticks. | unit-test |
| AT-10 | Without `--world-definition`, the bootstrapper continues using `EntityTemplates`-based defaults (existing test scenarios still pass). | build + unit-test |
| AT-11 | All existing `APIFramework.Tests`, `Warden.Telemetry.Tests`, `Warden.Contracts.Tests` stay green. | build + unit-test |
| AT-12 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-13 | `dotnet test ECSSimulation.sln` — every existing test stays green; new tests pass. | build |

---

## Followups (not in scope)

- WP-1.8.A — Cast generator reads `npcSlots[]` and spawns full NPC entities in their place.
- WP-1.9.A — Chronicle additions: `initialChronicleEntries[]` field on the world definition (a v0.2 of `world-definition.schema.json`).
- A more complete starter file — full 15-NPC office with all bible-named anchors. Content packet, not engine.
- Per-author or per-procedural variation: a "small office" definition vs "large office" definition; loader can pick by parameter.
- Validation tooling: a CLI verb `ECSCli world validate <path>` that runs schema + referential validation and reports.
- World-state save/load (per SRD §8.2 — saves are serialised `WorldStateDto`, not `WorldDefinitionDto`). The loader is for the *initial boot*; runtime saves are different concept and use the existing wire format.
