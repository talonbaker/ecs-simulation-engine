# APIFramework.Mutation

Public contract for all runtime structural topology mutations. Originally defined in WP-3.0.4 to keep the `StructuralChangeBus` + pathfinding cache invariants intact; extended in WP-4.0.J + WP-4.0.K with author-mode operations (rooms, lights, apertures, NPCs).

## What's in here

| Type | Purpose |
|:---|:---|
| `IWorldMutationApi` | The public contract. All structural mutations after WP-3.0.4 flow through here. |
| `WorldMutationApi` | Default impl. Two constructors: 2-arg (em + bus) for basic structural ops; 5-arg (em + bus + archetype catalog + cast config + RNG) enables author-mode `CreateNpc`. |
| `RoomDespawnPolicy` | Enum: `OrphanContents` (delete only the room; lights/anchors keep their RoomId pointing at nothing) / `CascadeDelete` (delete the room AND its referenced lights / apertures / anchor objects; NPCs always preserved). |

## Operations

### Original (WP-3.0.4 — structural mutations)
| Op | Notes |
|:---|:---|
| `MoveEntity(id, x, y)` | Requires `MutableTopologyTag` (fail-closed). |
| `SpawnStructural(x, y)` | Spawns a new structural entity. |
| `DespawnStructural(id)` | |
| `AttachObstacle(id)` / `DetachObstacle(id)` | |
| `ChangeRoomBounds(id, bounds)` | |
| `ThrowEntity(id, vx, vz, vy, decay)` | Attaches `ThrownVelocityComponent` + `ThrownTag`. |
| `SpawnStain(template, x, y)` | From `StainTemplates`. |

### Author mode — rooms / lights / apertures (WP-4.0.J)
| Op | Notes |
|:---|:---|
| `CreateRoom(category, floor, bounds, name?)` | Sensible default illumination (50 ambient, 4000K) — required to satisfy schema on save. |
| `DespawnRoom(id, RoomDespawnPolicy)` | NPCs never cascade-deleted regardless of policy (deliberate invariant). |
| `CreateLightSource(roomId, x, y, kind, state, intensity, colorTempK)` | Validates intensity 0–100, temp 1000–10000K. |
| `TuneLightSource(id, state, intensity, colorTempK)` | In-place mutation; same validation. |
| `CreateLightAperture(roomId, x, y, facing, areaSqTiles)` | Validates area 0.5–64.0. |
| `DespawnLight(id)` | Despawns a light source OR an aperture. |

### Author mode — NPCs (WP-4.0.K)
| Op | Notes |
|:---|:---|
| `CreateNpc(roomId, x, y, archetypeId, name)` | **Requires the 5-arg ctor** with cast deps. Calls `CastGenerator.SpawnNpc` internally so the NPC has the same archetype-derived components as a boot-time NPC. Throws if `archetypeId` is unknown. |
| `DespawnNpc(id)` | Requires `NpcArchetypeComponent`. |
| `RenameNpc(id, newName)` | Updates `IdentityComponent.Name`; preserves `Value` field. |

## Design discipline

- All structural mutations emit a `StructuralChangeEvent` on `StructuralChangeBus` so the pathfinding cache invalidates correctly.
- Light operations don't emit (lights don't affect pathfinding).
- Direct `entity.Set(new PositionComponent(...))` on a `StructuralTag` entity outside this API is a code smell — bypasses bus emission.
- Boot-time spawns (`WorldDefinitionLoader`, `CastGenerator.SpawnAll`) are exempt from going through this API: they happen before the bus has subscribers.

## Typical use

```csharp
// Basic (existing systems):
var api = new WorldMutationApi(em, bus);
api.MoveEntity(deskId, newX, newY);

// Author-mode (Wave 4):
var catalog = ArchetypeCatalog.LoadDefault();
var api2    = new WorldMutationApi(em, bus, catalog, new CastGeneratorConfig(), new SeededRandom(seed));
var roomId  = api2.CreateRoom(RoomCategory.Office, BuildingFloor.First, new BoundsRect(0, 0, 6, 6));
var roomKey = em.GetAllEntities().First(e => e.Id == roomId).Get<RoomComponent>().Id;
var lightId = api2.CreateLightSource(roomKey, 3, 3, LightKind.DeskLamp, LightState.On, 60, 3800);
var npcId   = api2.CreateNpc(roomKey, 1, 1, "the-vent", "Donna");
```

## Consumers

- **`Warden.DevConsole`** — runtime console commands (lock door, etc.).
- **`ECSUnity/Assets/Scripts/BuildMode/BuildModeController`** — player-facing build mode (3.1.D).
- **`ECSUnity/Assets/Scripts/BuildMode/AuthorModeController`** — WARDEN-only author mode (Wave 4).
- **(future) zone substrate (WP-4.2.0)** — adds `CreateZoneTransition`.

## See also

- `docs/c2-infrastructure/MOD-API-CANDIDATES.md#MAC-007` (base contract) and `#MAC-015` (author-mode extensions).
- `APIFramework.Tests/Mutation/` — 50+ unit tests.
- `APIFramework.Tests/Integration/AuthoringLoopEndToEndTests.cs` — end-to-end round-trip exercising the full mutation API.
