# WP-1.1.A — spatial-engine-rooms-index-proximity — Completion Note

**Executed by:** sonnet-4.6
**Branch:** feat/wp-1.1.A
**Started:** 2026-04-25T00:00:00Z
**Ended:** 2026-04-25T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Landed the spatial layer: rooms as first-class entities, a working cell-based spatial index, and a proximity event system that fires discrete signals when NPCs enter and exit conversation range, room co-presence, and visibility range.

**Key judgement calls:**

1. **`PositionComponent` stays float; spatial index uses `int` tile coords.** `PositionComponent.X/Z` remain floats (MovementSystem and the rest of the codebase depend on them). `SpatialIndexSyncSystem` converts via `(int)Math.Round(pos.X/Z)` on each tick. This matches the grid-aligned world without touching existing systems.

2. **`SystemPhase.Spatial = 5` added between PreUpdate and Physiology.** All three spatial systems run in phase 5 in registration order: SpatialIndexSync → RoomMembership → ProximityEvent. This satisfies the "before social/cognition" requirement cleanly.

3. **`Entity` used as dictionary key (not `int`).** The packet spec showed `Dictionary<int, int>` but the engine uses Guid-based Entity references throughout. Using `Entity` directly is consistent and correct; reference equality holds for session-lifetime objects.

4. **`VisibleFromHere` fires for entities within AwarenessRange that are not in conversation range and not in the same room** — consistent with querying `AwarenessRangeTiles` as specified.

5. **`APIFramework.Tests.csproj` gets a `Warden.Contracts` reference** to support the AT-01 round-trip test comparing `RoomComponent` against `RoomDto`. This adds a compile-time dependency but does not change runtime behaviour.

**Runtime state now available:**
- Room entities (via `RoomTag` + `RoomComponent`): addressable building spaces with bounds, category, floor, illumination snapshot.
- `ISpatialIndex` / `GridSpatialIndex`: range queries over all positioned entities.
- `EntityRoomMembership`: which room each entity currently occupies (updated every tick by `RoomMembershipSystem`).
- `ProximityEventBus`: discrete `EnteredConversationRange`, `LeftConversationRange`, `EnteredRoom`, `LeftRoom`, `VisibleFromHere`, `RoomMembershipChanged` signals.

**NOT projected to the wire:** Spatial state is engine-internal only. `TelemetryProjector` still emits `SchemaVersion = "0.1.0"`; rooms, index state, and proximity events do not appear on the wire until the unified projector packet.

**Reserved for follow-ups:**
- Lighting computation from sources (WP-1.2.A).
- Movement quality and pathfinding (WP-1.3.A).
- Social drive deltas driven by proximity events (Phase 1.4).
- Memory recording driven by proximity events (Phase 1.4).
- Per-NPC proximity overrides at spawn (cast generator, Phase 1.8).
- Concrete room instances from `world-definition.json` (world-bootstrap, Phase 1.7).
- `entities[].position.roomId` wire field (v0.3.x projector patch).

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | ✓ | `RoomComponentTests` verifies field-by-field round-trip: `RoomComponent → RoomDto → RoomComponent` produces equal values. Enum integer values verified identical. `BoundsRect.Contains` corner cases pass. |
| AT-02 | ✓ | `GridSpatialIndexTests` covers cell-aligned, mid-cell, diagonal, and multi-cell radius queries. Zero false positives and zero false negatives confirmed. |
| AT-03 | ✓ | `QueryNearest` with 50 random entities returns 5 closest sorted ascending by distance, ties broken by Entity.Id ascending. |
| AT-04 | ✓ | `SpatialIndexSyncSystemTests`: newly-spawned entity registers within one tick; moved entity updates within one tick; destroyed entity unregisters synchronously via `EntityDestroyed` event. |
| AT-05 | ✓ | `RoomMembershipSystemTests`: point-in-rect, smallest-area-wins for overlapping rooms, room entities skipped as occupants. |
| AT-06 | ✓ | `RoomMembershipSystem` fires `RoomMembershipChanged` once on entry and once on room-to-room transition; fires no event when entity stays in same room. |
| AT-07 | ✓ | Two NPCs approaching fire `EnteredConversationRange` exactly once at threshold. Subsequent ticks with no movement produce no additional events. |
| AT-08 | ✓ | `LeftConversationRange` fires exactly once when NPCs cross back out of conversation range. |
| AT-09 | ✓ | Event ordering verified: events sorted by `(Observer.Id, Target.Id)` ascending before fire. Tested with 3 NPCs all entering range simultaneously. |
| AT-10 | ✓ | `SpatialDeterminismTests`: two runs with `seed=12345`, 8 NPCs, 5000 ticks, seeded movement produce byte-identical proximity event streams. Different seeds produce different streams. |
| AT-11 | ✓ | All 24 `Warden.Telemetry.Tests` pass. Projector still emits `SchemaVersion = "0.1.0"`; spatial fields absent. `Warden.Telemetry` not modified. |
| AT-12 | ✓ | All 50 `Warden.Contracts.Tests` pass. DTOs unchanged. `Warden.Contracts` not modified. |
| AT-13 | ✓ | All prior `APIFramework.Tests` pass — no regression in physiology, social, or any earlier system. |
| AT-14 | ✓ | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-15 | ✓ | `dotnet test ECSSimulation.sln` — 503 passed, 0 failed across all test projects. |

---

## Files added

```
APIFramework/Components/BoundsRect.cs
APIFramework/Components/BuildingFloor.cs
APIFramework/Components/RoomCategory.cs
APIFramework/Components/RoomIllumination.cs
APIFramework/Components/RoomComponent.cs
APIFramework/Components/ProximityComponent.cs
APIFramework/Core/GridSpatialIndex.cs
APIFramework/Systems/Spatial/ProximityEvents.cs
APIFramework/Systems/Spatial/ProximityEventBus.cs
APIFramework/Systems/Spatial/EntityRoomMembership.cs
APIFramework/Systems/Spatial/SpatialIndexSyncSystem.cs
APIFramework/Systems/Spatial/RoomMembershipSystem.cs
APIFramework/Systems/Spatial/ProximityEventSystem.cs
APIFramework.Tests/Components/RoomComponentTests.cs
APIFramework.Tests/Components/ProximityComponentTests.cs
APIFramework.Tests/Core/GridSpatialIndexTests.cs
APIFramework.Tests/Systems/SpatialIndexSyncSystemTests.cs
APIFramework.Tests/Systems/RoomMembershipSystemTests.cs
APIFramework.Tests/Systems/ProximityEventSystemTests.cs
APIFramework.Tests/Systems/SpatialDeterminismTests.cs
docs/c2-infrastructure/work-packets/_completed/WP-1.1.A.md
```

## Files modified

```
APIFramework/Components/Tags.cs                   — add RoomTag struct
APIFramework/Core/ISpatialIndex.cs                — change float→int coords; IReadOnlyList return types; updated doc comments
APIFramework/Core/EntityManager.cs                — add EntityDestroyed event (fired from DestroyEntity)
APIFramework/Components/EntityTemplates.cs        — add Room() factory and WithProximity() helper
APIFramework/Core/SystemPhase.cs                  — add Spatial = 5 phase between PreUpdate and Physiology
APIFramework/Config/SimConfig.cs                  — add SpatialConfig, SpatialWorldSizeConfig, ProximityRangeDefaultsConfig; SimConfig.Spatial property
SimConfig.json                                    — add spatial section with defaults matching ProximityComponent.Default
APIFramework/Core/SimulationBootstrapper.cs       — instantiate GridSpatialIndex, ProximityEventBus, EntityRoomMembership; register 3 spatial systems; expose as public properties; wire EntityDestroyed → SpatialIndexSyncSystem.OnEntityDestroyed
APIFramework.Tests/APIFramework.Tests.csproj      — add Warden.Contracts project reference (required for AT-01 round-trip test)
```

## Diff stats

29 files changed (21 added counting completion note, 9 modified). Approximately 1,100 insertions, 5 deletions on this packet's work (prior packets' diff is on the same branch but predates this commit).

## Followups

- WP-1.2.A: Lighting engine populates `RoomComponent.Illumination`; add light-source + aperture entities.
- WP-1.3.A: Movement quality — pathfinding, step-aside, idle jitter. Spatial index is the prerequisite.
- Phase 1.4: Wire proximity events to social drive deltas (`ProximityEventBus.OnEnteredConversationRange → irritation bump`, etc.).
- Phase 1.4: Wire proximity events to memory recording (`OnEnteredConversationRange` → memory event record).
- Phase 1.7 (world-bootstrap): Load `world-definition.json`, call `EntityTemplates.Room(...)` for each named anchor.
- Phase 1.8 (cast-generator): `EntityTemplates.WithProximity(npc, cfg.ConversationTiles, ...)` per-NPC range overrides.
- Unified projector packet: populate `WorldStateDto.Rooms[]` from `Query<RoomTag>()` + `RoomComponent`; bump emitted `SchemaVersion` to `"0.3.0"`.
- v0.3.x patch: add optional `entities[].position.roomId` to wire format once the projector reads `EntityRoomMembership`.
- `VisibleFromHere` currently fires for entities within awareness range that are not in conversation range and not in the same room. If the sight-range (32-tile) category needs separate handling (parking lot scenarios), a dedicated `QueryRadius(SightRangeTiles)` pass in `ProximityEventSystem` would add it cleanly.
