# WP-1.1.A — Spatial Engine: Rooms + Spatial Index + Proximity Events

**Tier:** Sonnet
**Depends on:** WP-1.0.B (v0.3 spatial schema must be merged — already true on `staging` via PR #36)
**Parallel-safe with:** Nothing. This is the **foundation packet** for Phase 1.1+. Dispatch solo.
**Timebox:** 120 minutes
**Budget:** $0.50

---

## Goal

Land the spatial layer of the engine that the v0.3 wire format describes. Three things come live in `APIFramework`:

1. **Rooms as first-class entities.** A `RoomComponent` shape mirrored from the v0.3 `RoomDto`. Rooms have id, name, category, floor, bounds, illumination state. The world's three floors and ~25 rooms are addressable as proper engine entities, not derived geometry.

2. **A working spatial index.** The `ISpatialIndex` interface in `APIFramework/Core/ISpatialIndex.cs` is currently a stub with no implementation. This packet ships a real one — a cell-based grid implementation suitable for the building's scale (a few hundred entities, building bounds well under 512×512 tiles per floor). Range queries become O(cells touched + entities in those cells) instead of O(N).

3. **Proximity awareness and events.** A `ProximityComponent` per NPC carries the three ranges from the aesthetic bible (conversation, awareness, sight). A `ProximityEventBus` fires discrete signals — `EnteredConversationRange`, `LeftRoom`, `VisibleFromHere` — that future systems subscribe to. Most social drive updates and most narrative-event detection ride on this bus.

What this packet does **not** do: lighting (Phase 1.2), movement quality (Phase 1.3), telemetry projection of spatial state (deferred until a unified projector packet later in Phase 1, when WP-1.8's smoke mission needs the wire format populated). Until that projector packet, the engine produces real spatial state but the wire format keeps emitting v0.1-shaped data — which is fine because no consumer reads spatial state yet.

---

## Reference files

- `docs/c2-content/DRAFT-aesthetic-bible.md` — priority-2 (proximity) commits the engine to three ranges per NPC and the proximity-event signal set. **Read first.**
- `docs/c2-content/DRAFT-world-bible.md` — the three-floor building, the named anchors, the room categories. The engine doesn't author named anchors here (that's the world-bootstrap packet, Phase 1.7) but the schema for what a Room *is* must support what those anchors will need.
- `docs/c2-infrastructure/00-SRD.md` §8.6 (visual target — engine exposes spatial structure for AI-tier reasoning about places).
- `docs/c2-infrastructure/work-packets/_completed/WP-1.0.B.md` — confirms the v0.3 DTO surface this packet's components mirror in shape.
- `Warden.Contracts/Telemetry/RoomDto.cs` — the wire-format shape for a room. The new `RoomComponent` must align field-by-field, by name and unit, with the DTO. Differences will cause projector pain later.
- `Warden.Contracts/Telemetry/PositionStateDto.cs` (or the existing `PositionStateDto`) — the wire-format position. Verify it carries `(x, y)` already; if there's an optional `roomId` field on it, leave it null at projection time for this packet (room-membership-on-position is a deferred index — see Design notes).
- `APIFramework/Core/ISpatialIndex.cs` — the interface this packet implements. The doc comments propose a cell-based grid; honor that.
- `APIFramework/Components/PositionComponent.cs` — the existing position component the spatial index syncs with.
- `APIFramework/Core/SimulationBootstrapper.cs` — system + service registration site.
- `APIFramework/Core/SystemPhase.cs` — phase enum. Spatial-sync runs early (before social, lighting, etc.); proximity-event emission runs after sync, before any consumer system.
- `APIFramework/Components/Tags.cs` — for new `RoomTag`.
- `APIFramework/Core/SeededRandom.cs` — RNG source. Required for any random behavior; never `System.Random`.
- `APIFramework.Tests/Systems/*.cs` — pattern reference for system-test layout.
- `SimConfig.json` — runtime tuning lives here, not in code.

## Non-goals

- Do **not** modify `Warden.Telemetry/TelemetryProjector.cs` or any other file under `Warden.Telemetry/`. The wire format keeps emitting v0.1-shaped data; populating spatial state on the wire is a deferred unified-projector packet, not this one. (The same parallel-safety rationale that applied to WP-1.4.A: keep one file out of the contention path.)
- Do **not** modify any file under `Warden.Contracts/`. The DTOs landed in WP-1.0.B; this packet builds the engine that will eventually populate them.
- Do **not** modify any file under `docs/c2-infrastructure/schemas/`. No schema bump in this packet.
- Do **not** implement lighting state propagation (sun → window beams → floor tiles). That's WP-1.2.A. This packet stores per-room illumination as a *snapshot value* (ambientLevel, colorTemperatureK, optional dominantSourceId reference) but does not compute it from any source. Lighting fills the values in 1.2.
- Do **not** implement movement quality (pathfinding refinements, step-aside, idle jitter). That's WP-1.3.A. The existing `MovementSystem` continues to operate unchanged; this packet adds the spatial index that future movement work will consume, but does not refactor movement itself.
- Do **not** implement memory recording or social-drive deltas in response to proximity events. The event *bus* ships in this packet; the *consumers* are later. Proximity events fire correctly and a future packet wires them to drive deltas.
- Do **not** add `entities[].position.roomId` projection on the wire. Room-membership is computed at runtime by `RoomMembershipSystem` and exposed as `EntityRoomMembership` in-engine state; the wire-format optimization (denormalizing `roomId` onto position) is deferred to a v0.3.x patch.
- Do **not** populate any specific named anchors (the Microwave, the Window, etc.). The world-bootstrap packet (Phase 1.7) reads `world-definition.json` and instantiates concrete rooms. This packet only ships the *capability*, not the *content*.
- Do **not** add light source or aperture entities. Those are 1.2.A. The room's `Illumination` field is set by tests with literal values; lighting systems populate it later.
- Do **not** add a NuGet dependency.
- Do **not** use `System.Random` anywhere. `SeededRandom` only.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)

---

## Design notes

### Room as a first-class entity

A room is an entity. The `EntityManager` already supports arbitrary entities — rooms ride that. Components on the room entity:

**`RoomComponent`** (one per room entity):
- `Id: string` — UUID, mirrors wire format.
- `Name: string` — slug, max 64 chars.
- `Category: RoomCategory` — enum mirroring `RoomDto.Category` (sixteen values).
- `Floor: BuildingFloor` — enum: `Basement`, `First`, `Top`, `Exterior`.
- `Bounds: BoundsRect` — `(int X, int Y, int Width, int Height)` in tile units.
- `Illumination: RoomIllumination` — `(int AmbientLevel 0–100, int ColorTemperatureK 1000–10000, string? DominantSourceId)`.

A `RoomTag` marker component lets systems iterate room entities cheaply.

The `RoomComponent` field shapes mirror `RoomDto` exactly — same names, same units, same orderings. This is enforced by a sanity test: a serialization round-trip from `RoomComponent` to `RoomDto` to JSON to `RoomDto` to a new `RoomComponent` produces an equal value. (The test exists for the future projector's benefit; the projector doesn't ship in this packet.)

### The spatial index — cell-based grid

The existing `ISpatialIndex` doc-comments propose a cell-based grid as the default implementation. Honor that:

**`GridSpatialIndex`** in `APIFramework/Core/GridSpatialIndex.cs`:
- Configurable cell size (default 4 tiles per cell — read from `SimConfig.SpatialCellSizeTiles`).
- Configurable world bounds (default 512×512 — `SimConfig.SpatialWorldSize`).
- `Register(Entity, x, y)` — stores entity in the cell containing (x, y). Multiple entities per cell allowed.
- `Unregister(Entity)` — removes from whatever cell it's in.
- `Update(Entity, newX, newY)` — moves entity to the cell containing (newX, newY); cheap if same cell, otherwise a list-shuffle.
- `QueryRadius(x, y, r)` — returns all entities within `r` of `(x, y)`. Implementation: visits the cells the radius could overlap (a (2 ⌈r/cellSize⌉ + 1)² block), filters by exact distance. Result is a `List<Entity>` materialized eagerly for determinism (enumerator order is deterministic if cells are visited in row-major order and entities within a cell maintain insertion order).
- `QueryNearest(x, y, n)` — same outer structure but maintains a min-heap of distance-bounded candidates and returns the n nearest. Returns fewer if fewer exist.

Coordinate system commitment: positions are `(int x, int y)` in tile units, **not** floats. This is a deviation from the doc-comment proposal (which suggested `(float x, float y)` in metres). Tile units match the v0.3 schema's bound integers and the existing `PositionComponent`. Rationale: the early-2000s grid-aligned office is grid-aligned. Sub-tile precision is unnecessary at this fidelity.

Update the `ISpatialIndex` interface to use `int x, int y` accordingly. Existing consumers of the interface (if any — most likely none, since it's a stub) update to match. The interface's doc-comments are updated to reflect the tile-integer commitment.

**One singleton instance** registered in DI by `SimulationBootstrapper`. All systems that need range queries read the same index. The instance is wrapped behind `ISpatialIndex` so a future quadtree or BVH swap is non-breaking.

### Spatial-sync system

`SpatialIndexSyncSystem` runs early in the tick pipeline (before any social/proximity/movement consumer). For every entity that has a `PositionComponent`:

- If the entity is not yet in the index, `Register` it.
- If the entity's position changed since last tick (a small `LastSyncedPosition` cache lives in the system, not on the component), `Update` it.

Entity destruction is handled via `EntityManager`'s existing destruction signals — the system subscribes (or the bootstrapper wires the unsubscribe call). If `EntityManager` doesn't expose a destruction signal yet, add a minimal one — a single `event Action<Entity> EntityDestroyed` is enough. Non-goal sanity check: this is *infrastructure*, not behavior; keep the addition tiny.

### Room-membership system

`RoomMembershipSystem` runs after `SpatialIndexSyncSystem`. For each entity with a `PositionComponent` and not a `RoomTag` itself:

- Compute which `Room` entity contains the position (point-in-rect against `RoomComponent.Bounds`).
- Cache the result in an in-engine `EntityRoomMembership` service (a `Dictionary<int entityId, int roomEntityId>` keyed by entity, value is the containing room — null if no room contains the position, e.g., the entity is outside the building's interior bounds).
- If the result changed since last tick, fire a `RoomMembershipChanged(entity, oldRoom, newRoom)` event on the proximity-event bus.

Overlapping rooms: per the v0.3 schema, room bounds may overlap (a hallway under a balcony). When overlap occurs, the system selects the room with the *smallest* `Bounds.Width * Bounds.Height` — the more specific containment wins. Determined by sorting overlap candidates by area and picking the smallest.

### Proximity component and ranges

`ProximityComponent` (one per NPC):
- `ConversationRangeTiles: int` — default 2, per the bible.
- `AwarenessRangeTiles: int` — default 8 (covers a typical room).
- `SightRangeTiles: int` — default 32 (across an open area like a parking lot).

These default values are seed values; `SimConfig.ProximityRangeDefaults` carries them so tuning doesn't require code changes. Per-NPC overrides happen at spawn time (later, by the cast generator).

### Proximity event bus

`ProximityEventBus` is a thin singleton:

```csharp
public sealed class ProximityEventBus
{
    public event Action<ProximityEnteredConversationRange>? OnEnteredConversationRange;
    public event Action<ProximityLeftConversationRange>? OnLeftConversationRange;
    public event Action<ProximityEnteredRoom>? OnEnteredRoom;
    public event Action<ProximityLeftRoom>? OnLeftRoom;
    public event Action<ProximityVisibleFromHere>? OnVisibleFromHere;
    public event Action<RoomMembershipChanged>? OnRoomMembershipChanged;

    public void RaiseEnteredConversationRange(...);  // etc.
}
```

Events are *records* (value types preferred, but readonly-record-struct only if all fields fit cleanly; otherwise plain record). Each event carries the entity ids involved, the tick, and any range-specific context. Order of event firing within a tick is determined by entity-id ordering — small ids first — to preserve determinism.

### Proximity event system

`ProximityEventSystem` runs after `RoomMembershipSystem`. For each NPC entity with a `ProximityComponent`:

- Query `ISpatialIndex.QueryRadius(x, y, AwarenessRangeTiles)` to get nearby entities.
- For each nearby entity that's also an NPC: compare with the previous tick's neighbor set (cached in the system). Newly-in-range fires `EnteredConversationRange` (if within conversation range) or `EnteredRoom` (if newly in same room) or `VisibleFromHere` (if within sight range only). Newly-out-of-range fires the corresponding `Left*` event.
- The events are batched-and-emitted at end-of-tick (not mid-tick) so a system reading the bus sees a consistent snapshot.

This system is the **highest-volume** event source in the engine after physiology. The bible commits us to events firing every tick — that's correct as a design intent, but the system batches per-tick to avoid per-entity-per-tick allocation churn. Use struct events and a pooled list for the batch.

### Determinism

Every event order, every iteration order, every query result order is deterministic. No `HashSet<int>` iteration without sorting. No `Dictionary<int, ...>` iteration without sorting keys. `List<Entity>` is the default container. Tests verify that two runs with the same seed produce byte-identical event streams across 5000 ticks.

### SimConfig additions

```jsonc
{
  "spatial": {
    "cellSizeTiles": 4,
    "worldSize": { "width": 512, "height": 512 },
    "proximityRangeDefaults": {
      "conversationTiles": 2,
      "awarenessTiles":   8,
      "sightTiles":      32
    }
  }
}
```

### What's expressly deferred

- Lighting computation (sun → window beams → floor illumination). 1.2.A.
- Light source state machines (flickering, dying). 1.2.A.
- Light source and aperture entities. 1.2.A.
- Movement quality (pathfinding refinements, step-aside, idle jitter). 1.3.A.
- Telemetry projection of any spatial state on the wire. Deferred unified projector packet.
- Memory recording driven by proximity events. Phase 1.4 follow-up.
- Social drive deltas driven by proximity events. Phase 1.4 follow-up.
- Per-NPC proximity range customization at spawn. Cast generator (Phase 1.8).

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Core/ISpatialIndex.cs` (modified) | Update interface signatures from `float x, float y` to `int x, int y`. Update doc comments to reflect tile-integer commitment. |
| code | `APIFramework/Core/GridSpatialIndex.cs` | The cell-based grid implementation. Configurable cell size and world bounds (read from SimConfig). Implements all `ISpatialIndex` methods. |
| code | `APIFramework/Components/RoomComponent.cs` | The struct/record. Fields mirror `RoomDto` shape. |
| code | `APIFramework/Components/RoomIllumination.cs` | `(int AmbientLevel, int ColorTemperatureK, string? DominantSourceId)` — separate file or nested type, Sonnet's choice. |
| code | `APIFramework/Components/BoundsRect.cs` | `(int X, int Y, int Width, int Height)` value type. |
| code | `APIFramework/Components/RoomCategory.cs` (new enum file) | Sixteen values mirroring schema. |
| code | `APIFramework/Components/BuildingFloor.cs` (new enum file) | Four values. |
| code | `APIFramework/Components/ProximityComponent.cs` | Three int range fields. |
| code | `APIFramework/Components/Tags.cs` (modified) | Add `RoomTag`. |
| code | `APIFramework/Core/EntityManager.cs` (modified, minimal) | Add `event Action<Entity> EntityDestroyed` if not already present. Tiny; just enough to let the spatial-sync system unregister destroyed entities. |
| code | `APIFramework/Systems/Spatial/SpatialIndexSyncSystem.cs` | Per-tick: register new positioned entities, update moved entities, unregister destroyed entities. |
| code | `APIFramework/Systems/Spatial/RoomMembershipSystem.cs` | Per-tick: compute which room each entity is in; emit `RoomMembershipChanged` on transitions. |
| code | `APIFramework/Systems/Spatial/ProximityEventSystem.cs` | Per-tick: emit conversation-range, awareness-range, sight-range entry/exit events. |
| code | `APIFramework/Systems/Spatial/ProximityEventBus.cs` | The singleton event bus with the six events. |
| code | `APIFramework/Systems/Spatial/ProximityEvents.cs` | The six event record types. |
| code | `APIFramework/Systems/Spatial/EntityRoomMembership.cs` | The `Dictionary<int, int?>` service exposing the cached room-membership map; queryable by other systems. |
| code | `APIFramework/Components/EntityTemplates.cs` (modified) | Add `EntityTemplates.Room(...)` factory and a `WithProximity(...)` helper. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `ISpatialIndex` (singleton, `GridSpatialIndex`), the three new systems in the appropriate phase, the `ProximityEventBus`, the `EntityRoomMembership` service. Make sure the phase order is: SpatialIndexSync → RoomMembership → ProximityEvent → (everything else). |
| code | `APIFramework/Core/SystemPhase.cs` (modified, if needed) | If the existing phase enum doesn't have a slot for "early spatial sync", add one. Likely fits in an existing phase; defer to the Sonnet's judgment. |
| code | `SimConfig.json` (modified) | Add the `spatial` section per Design notes. |
| code | `APIFramework.Tests/Components/RoomComponentTests.cs` | Field-mirror sanity test (compares to RoomDto), bounds-rect validity. |
| code | `APIFramework.Tests/Components/ProximityComponentTests.cs` | Range bounds; default values from SimConfig. |
| code | `APIFramework.Tests/Core/GridSpatialIndexTests.cs` | Register/unregister/update; QueryRadius accuracy at multiple scales (cell-aligned, mid-cell, edge cases); QueryNearest correctness with 1, 5, 50 entities; insertion-order determinism. |
| code | `APIFramework.Tests/Systems/SpatialIndexSyncSystemTests.cs` | Newly-spawned entities register; moved entities update; destroyed entities unregister. |
| code | `APIFramework.Tests/Systems/RoomMembershipSystemTests.cs` | Point-in-rect; smallest-area-wins for overlapping rooms; transitions fire `RoomMembershipChanged` events. |
| code | `APIFramework.Tests/Systems/ProximityEventSystemTests.cs` | Two NPCs approaching produce `EnteredConversationRange` once at the threshold tile; receding produces `LeftConversationRange` once. Multiple-NPC scenarios. Determinism with seeded movement. |
| code | `APIFramework.Tests/Systems/SpatialDeterminismTests.cs` | Two runs over 5000 ticks with the same seed produce byte-identical proximity event streams. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.1.A.md` | Completion note. Standard template. Explicitly enumerate (a) what runtime state is now available (rooms, spatial index, proximity events), (b) what's NOT projected to the wire (deferred projector packet), (c) what consumers are reserved for follow-ups (lighting, movement, social drive deltas, memory recording). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | All new components compile, instantiate with sensible defaults, and field-mirror sanity tests pass (RoomComponent ↔ RoomDto round-trip equal). | unit-test |
| AT-02 | `GridSpatialIndex.QueryRadius` returns the exact set of entities within radius for several test scenarios (cell-aligned, mid-cell, radius spanning multiple cells). False positives and false negatives both = 0. | unit-test |
| AT-03 | `GridSpatialIndex.QueryNearest(x, y, 5)` with 50 randomly placed entities returns the 5 closest, sorted ascending by distance. Ties broken by entity id ascending (deterministic). | unit-test |
| AT-04 | `SpatialIndexSyncSystem` registers a newly-spawned positioned entity within one tick, updates a moved entity within one tick, unregisters a destroyed entity within one tick. | unit-test |
| AT-05 | `RoomMembershipSystem` correctly classifies entities into rooms via point-in-rect, including the smallest-area-wins rule for overlapping rooms. | unit-test |
| AT-06 | `RoomMembershipSystem` fires `RoomMembershipChanged(entity, old, new)` once per transition; fires no event when an entity stays in the same room across ticks. | unit-test |
| AT-07 | `ProximityEventSystem` fires `EnteredConversationRange` exactly once when two NPCs cross the conversation-range threshold approaching each other. | unit-test |
| AT-08 | `ProximityEventSystem` fires `LeftConversationRange` exactly once when those NPCs cross back out. | unit-test |
| AT-09 | `ProximityEventSystem` fires events in entity-id-ascending order within a tick (determinism contract). | unit-test |
| AT-10 | `SpatialDeterminismTests` produce byte-identical event streams across two runs with the same seed over 5000 ticks. | unit-test |
| AT-11 | `Warden.Telemetry.Tests` all pass — projector still emits `SchemaVersion = "0.1.0"` and the spatial fields are absent (this packet does not modify the projector). | build + unit-test |
| AT-12 | `Warden.Contracts.Tests` all pass — DTOs unchanged. | build + unit-test |
| AT-13 | All existing `APIFramework.Tests` stay green — no regression in physiology, social, or any prior system. | build + unit-test |
| AT-14 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-15 | `dotnet test ECSSimulation.sln` — every existing test stays green; new tests pass. | build |

---

## Followups (not in scope)

- Unified projector packet (after WP-1.2.A merges): update `Warden.Telemetry/TelemetryProjector.cs` to populate spatial state (rooms, light sources, apertures, sun) AND social state (drives, willpower, etc.) from runtime engine state, bumping emitted `SchemaVersion` to `"0.3.0"`.
- WP-1.2.A — Lighting engine: sun-position system, light apertures (windows), light sources with state machines, illumination-field accumulation onto rooms.
- WP-1.3.A — Movement quality: pathfinding refinements, step-aside-in-hallways, idle jitter, mood-modulated speed, facing-direction.
- Social drive deltas in response to proximity events (a Phase 1.4 follow-up): Donna's irritation bumps when she crosses Frank's path; Bob's loneliness drops when he enters a populated room.
- Memory recording driven by proximity events (Phase 1.4 follow-up): notable interactions in conversation-range produce memory event records.
- Cast-generator (Phase 1.8) populates per-NPC proximity-range overrides.
- World-bootstrap (Phase 1.7) loads `world-definition.json` and instantiates concrete rooms with bounds matching the world bible's named anchors.
- A v0.3.x schema patch that adds optional `entities[].position.roomId` to the wire format, populated by the projector after this packet lands. Deferred so this packet stays small.
