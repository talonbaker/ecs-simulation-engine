# WP-4.2.6 — Fire / Disaster (Cross-Zone Evacuation Stress Test)

> **Phase 4.2.x emergent gameplay (post-reorg) — capstone scenario.** A fire breaks out in a zone. NPCs must evacuate via transition triggers to a designated safe zone (parking lot). Fire spreads tile-by-tile until extinguished by a sprinkler trigger or burns out a room. **This packet is the deliberate stress test for the zone substrate (WP-4.2.0) AND the LoD system (WP-4.2.2)** — many NPCs moving across zone boundaries quickly, while an active simulation event continues in the source zone.

> **DO NOT DISPATCH UNTIL WP-4.2.0 + WP-4.2.2 ARE MERGED** — this scenario is designed to exercise both systems under load. It will likely surface perf issues that the LoD packet alone won't.

**Tier:** Sonnet
**Depends on:** WP-4.2.0 (zones), WP-4.2.2 (LoD), WP-3.2.1 (sound triggers — alarm, crackling), WP-4.0.H (particles — smoke, flame), WP-3.2.4 (rescue — helping incapacitated coworkers escape).
**Parallel-safe with:** Other 4.2.x scenarios.
**Timebox:** 180 minutes
**Budget:** $0.85
**Feel-verified-by-playtest:** YES (urgency feel, zone-switch readability under chaos)
**Surfaces evaluated by next PT-NNN:** Does the fire feel urgent? Does the camera handle players switching between the burning zone and the parking lot smoothly? Are NPCs visibly making different choices (running vs panicking vs helping)? Does the LoD system hold up when 30+ NPCs all need to evacuate cross-zone in a few seconds of game time?

---

## Goal

Add a fire/disaster scenario:

1. **Trigger:** scenario-scripted OR random low-probability (an electrical fault per game-month, calibrated rarely).
2. **Onset:** `FireComponent` on a tile (typically near electronics — server room, microwave); `FireAlarmTriggerSoundEvent` emitted; particle smoke + flame from MAC-012 vocabulary.
3. **Spread:** fire ticks outward to adjacent tiles based on flammability of furnishings; spreads ~1 tile per second initially, accelerating.
4. **NPC behavior:** NPCs in the same zone as the fire enter `EvacuationIntent`; they pathfind to the **nearest zone transition** that doesn't pass through fire; they exit to the dest zone.
5. **Rescue cascade:** NPCs adjacent to incapacitated coworkers (existing rescue infrastructure from WP-3.2.4) attempt to drag them out before evacuating.
6. **Resolution:** fire burns out when no flammable adjacent tiles remain OR a player-placed sprinkler activates. Damage tally; chronicle entries; NPCs may suffer smoke-inhalation `FaintingComponent` if delayed.
7. **Cross-zone stress:** the parking-lot zone fills up with evacuated NPCs; fire stays in the original zone; LoD ticks the burning zone at full fidelity (Bucket B systems still active even after no NPCs remain — fire continues).

After this packet, fire is the project's first **multi-zone-active simulation event** (the burning zone matters even when not actively viewed).

---

## Reference files

- `docs/c2-content/world-bible.md` — fire risk references; sprinkler placement context.
- `docs/c2-content/aesthetic-bible.md` — flame / smoke visual style notes.
- `APIFramework/Systems/LifeState/FaintingRecoverySystem.cs` — pattern for time-pressure recovery.
- `APIFramework/Systems/Movement/PathfindingService.cs` — needs a "find tile NOT in obstacle set X" extension for fire-avoidance pathing.
- `APIFramework/Audio/SoundTriggerKind.cs` — adds `FireAlarm`, `FireCrackle` (additive enum).
- `APIFramework/Visual/ParticleTriggerKind.cs` — `SmokeFromFire` already exists (WP-4.0.H); `Flame` extends.
- `APIFramework/Mutation/IWorldMutationApi.cs` — uses existing `SpawnStain` pattern for placing fire entities.
- `WP-4.2.0` zone substrate + `WP-4.2.2` LoD — full understanding required.

---

## Non-goals

- Do **not** model property damage economic cost (couples to FF-008 economy).
- Do **not** model insurance / repair contractor scenarios.
- Do **not** ship fire-specific visuals beyond particle vocabulary + crackle sound.
- Do **not** model permanent room destruction (burned room becomes uninhabitable). v0.1: fire damages furniture (uses existing BreakableComponent pattern); rooms remain usable post-extinguish.
- Do **not** model multiple simultaneous fires across zones.
- Do **not** ship fire-suppression items as build-mode placeable. v0.1: hard-code one sprinkler trigger per fireable zone (sprinkler activates if fire reaches a sprinklered tile; build-mode placeable sprinklers is future content).

---

## Design notes

### `FireComponent`

```csharp
public struct FireComponent
{
    public int    TileX           { get; init; }
    public int    TileY           { get; init; }
    public string ZoneId          { get; init; }
    public float  Intensity       { get; init; }   // 0-1; spread rate scales
    public long   IgnitionTick    { get; init; }
    public long   NextSpreadTick  { get; init; }
}
```

Tile-level entity (`StructuralTag` + `ObstacleTag` + `FireComponent`). Pathfinding sees fire tiles as obstacles; NPCs route around.

### `EvacuationIntent` component

```csharp
public struct EvacuationIntent
{
    public string TargetZoneId   { get; init; }
    public bool   AssistingOther { get; init; }   // true = dragging an incapacitated NPC
}
```

Spawned by `EvacuationOnsetSystem` on every NPC in the burning zone when fire onset happens.

### Pathfinding extension

`PathfindingService` needs a method:

```csharp
public IReadOnlyList<(int X, int Y)> ComputePathAvoiding(int fromX, int fromY, int toX, int toY, int seed,
                                                         IReadOnlySet<(int X, int Y)> avoidTiles);
```

NPCs use this to route around fire tiles. Cost: marginal extension of A* obstacle-set check.

### Cross-zone evacuation

Each NPC's evacuation target is computed once per onset:
1. Find all `ZoneTransitionComponent` entities in the NPC's current zone.
2. Filter: exclude transitions whose dest zone is a "do not evacuate to" zone (configurable; e.g., "stairwell" might not be a safe zone for fire).
3. Pick nearest by path-distance avoiding fire tiles.
4. Pathfind to that transition tile; on arrival, the existing zone-transition mechanism (WP-4.2.0/4.2.1) teleports the NPC to the dest zone.

### LoD interaction

Critical: **Fire spread runs in ALL zones at full fidelity.** The fire is a Bucket B (`FullFidelityAlways`) system because:
- Player must see consistent damage if they switch back to the zone.
- NPCs evacuating from a burning zone must have a real fire to flee.
- Time-stops-while-you're-away semantics break narrative: "I was in the parking lot for a minute, came back, the fire was somehow paused."

Cost: fire spread is cheap (per-tile adjacency check). 100 NPCs + an active fire across 4 zones must still hit the 60-FPS gate. **This is the single biggest stress on the LoD system.**

### Sprinkler trigger

Pre-placed `SprinklerComponent` at room centers. When a fire tile is within radius 3 of a sprinkler, sprinkler activates: fire intensity decays per tick to 0; fire entities removed.

### Rescue under fire

Existing rescue infrastructure (WP-3.2.4) extends: an NPC with `RescueIntent` (helping an incapacitated NPC) carries the incap NPC's position with their movement; on reaching evacuation transition, both teleport.

### Performance

This packet is the **single biggest perf risk** in Phase 4.2.x. The perf gate test:

`PerformanceGate100NpcFireEvacuationTests` — 100 NPCs across 4 zones; fire spawns in zone 2; all 30 NPCs in zone 2 evacuate to zone 1 (parking lot); fire continues to spread for ~30 game-seconds; verify ≥ 60 FPS throughout.

If this fails: fire spread system may need to coarsen tile-spread checks (run every 4th tick), OR pathfinding cache needs zone-aware invalidation tuning.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/FireComponent.cs` (new) | Tile-level fire state. |
| code | `APIFramework/Components/EvacuationIntent.cs` (new) | NPC evacuation goal. |
| code | `APIFramework/Components/SprinklerComponent.cs` (new) | Pre-placed suppression trigger. |
| code | `APIFramework/Systems/Fire/FireOnsetSystem.cs` (new) | Trigger logic. |
| code | `APIFramework/Systems/Fire/FireSpreadSystem.cs` (new, `[ZoneLod(FullFidelityAlways)]`) | Tile-by-tile spread. |
| code | `APIFramework/Systems/Fire/EvacuationOnsetSystem.cs` (new) | Tags NPCs in fire zone with EvacuationIntent. |
| code | `APIFramework/Systems/Fire/EvacuationMovementSystem.cs` (new) | Pathfinds NPCs to nearest safe transition. |
| code | `APIFramework/Systems/Fire/SprinklerActivationSystem.cs` (new) | Suppresses fire when in range. |
| code | `APIFramework/Systems/Movement/PathfindingService.cs` (modification) | Add `ComputePathAvoiding(...)` method. |
| code | `APIFramework/Audio/SoundTriggerKind.cs` (modification — additive) | `FireAlarm`, `FireCrackle`. |
| code | `APIFramework/Visual/ParticleTriggerKind.cs` (modification — additive) | `Flame` (smoke already exists). |
| code | `APIFramework/Components/NarrativeEventKind.cs` (modification — additive) | `FireOutbroke`, `EvacuationCompleted`, `FireExtinguished`. |
| code | `APIFramework/Config/SimConfig.cs` (modification) | `FireConfig` section. |
| test | 8+ test files covering fire spread, evacuation pathing, sprinkler activation, rescue-under-fire, cross-zone evacuation, and the headline perf test. | unit + integration + perf |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Note `FireComponent` + sprinkler as latent surfaces. |

---

## Acceptance tests

| ID | Assertion |
|:---|:---|
| AT-01 | Fire onset spawns FireComponent at the trigger tile; alarm + smoke emitted. |
| AT-02 | Fire spreads to flammable adjacent tiles every N ticks. |
| AT-03 | NPCs in fire zone get EvacuationIntent within 5 ticks of onset. |
| AT-04 | Pathfinding avoids fire tiles. |
| AT-05 | NPCs reach nearest non-fire-blocked transition; teleport to safe zone. |
| AT-06 | Rescue scenario: NPC drags incapacitated coworker to safety. |
| AT-07 | Sprinkler within range suppresses fire over time. |
| AT-08 | **100 NPCs across 4 zones with active fire hold ≥ 60 FPS.** Headline perf gate. |
| AT-09 | Fire continues to spread in inactive zones (Bucket B FullFidelityAlways respected). |
| AT-10 | Chronicle records fire onset, evacuation completion, extinguishment. |
| AT-11 | All Phase 0–3 + 4.0.A–L + 4.2.0 + 4.2.2 tests stay green. |
| AT-12 | `dotnet build` warning count = 0; all tests green. |

---

## Followups (not in scope)

- Building-wide property damage economics.
- Player-placeable sprinklers / fire extinguishers.
- Multiple simultaneous fires.
- Burned-out rooms (permanent damage state).
- Smoke-inhalation as a long-term effect (not just transient fainting).

---

## Completion protocol

Standard. Visual verification helpful. **Cost target $0.85** — perf-test infrastructure is non-trivial.
