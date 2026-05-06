# WP-3.0.3 — Slip-and-Fall + Locked-In-and-Starved

> **STATUS:** SHIPPED to staging 2026-04-30. Retained because pending packets depend on this spec: WP-3.0.5, WP-3.1.S.2, WP-3.2.0, WP-3.2.2, WP-3.2.4, WP-3.2.5.

> **DO NOT DISPATCH UNTIL WP-3.0.0 AND WP-3.0.4 ARE MERGED.**
> This packet calls `LifeStateTransitionSystem.RequestTransition` (WP-3.0.0) and reads the pathfinding service's reachability check that depends on the structural change bus + cache from WP-3.0.4. Both must be on `main` before dispatch. Fail-closed at build time otherwise.

**Tier:** Sonnet
**Depends on:** WP-3.0.0 (LifeStateTransitionSystem, NarrativeEventKind values, LifeStateGuard), WP-3.0.4 (IWorldMutationApi, StructuralChangeBus, PathfindingCache), WP-1.9.A (Stain entities, PhysicalManifestSpawner), WP-1.7.A (world bootstrap)
**Parallel-safe with:** WP-3.0.1 (choking — disjoint death-trigger surface), WP-3.0.2 (corpse + bereavement — both consume the death narrative this packet emits, no direct conflict)
**Timebox:** 120 minutes
**Budget:** $0.50

---

## Goal

Two more emergent death scenarios using the substrate from 3.0.0 and 3.0.4. Together they prove the engine can produce death from causes that are *not* a single failure mode (choking) but environmental: a stain that should have been cleaned, a door that shouldn't have locked.

**Slip-and-fall.** A stain entity (water puddle, blood, the oil patch from the basement loading dock) is currently passive — it sits on a tile and persists through the chronicle. This packet adds `FallRiskLevel` to `StainComponent` and a `SlipAndFallSystem` that, each tick an NPC's tile contains a fall-risk stain, rolls a deterministic-seeded check against the NPC's `MovementSpeedFactor` and stress state. On hit, the NPC enters `Deceased(SlippedAndFell)` — sudden death, no Incapacitated phase. (The lethal threshold is high; routine slips don't kill. v0.1 ships only the lethal tier; non-lethal severity tiers are a follow-up.)

**Locked-in-and-starved.** A door entity gains a `LockedTag`. A `LockoutDetectionSystem` runs at end-of-game-day, queries the pathfinding service: from each Alive NPC's current room, is there a walkable path to any building exit / outdoor area? If no AND the NPC's hunger is at maximum, attach a `LockedInComponent` with a `StarvationTickBudget` (long — game-days, not minutes). On budget expiry, transition the NPC to `Deceased(StarvedAlone)`.

After this packet, the engine produces three causes of death (Choked, SlippedAndFell, StarvedAlone). The chronicle records them. Donna, Greg, Frank, the Newbie, the Old Hand — anyone can die in any of three ways, and the office can be observed reacting through the bereavement system 3.0.2 ships.

This packet is **engine-internal at v0.1.** No wire-format change. No orchestrator change.

---

## Reference files

- `docs/c2-infrastructure/work-packets/WP-3.0.0-life-state-component-and-cause-of-death-events.md` — `LifeStateTransitionSystem.RequestTransition`, `CauseOfDeath.SlippedAndFell`, `CauseOfDeath.StarvedAlone`, `LifeStateGuard.IsAlive`.
- `docs/c2-infrastructure/work-packets/WP-3.0.4-live-mutation-hardening.md` — **read second.** `IWorldMutationApi.AttachObstacle / DetachObstacle` are how doors get locked / unlocked at runtime; `StructuralChangeBus` is what the pathfinding cache subscribes to; the cache makes lockout detection cheap (we don't recompute reachability every tick — only when topology changes).
- `docs/c2-content/world-bible.md` — the Smoking Bench is outside; the Parking Lot is outside; the loading dock is outside. These are the "exits" the lockout-detection system checks reachability against.
- `docs/c2-content/aesthetic-bible.md` — proximity for witness selection; movement quality (NPCs walk faster when stressed → higher slip risk).
- `APIFramework/Components/StainComponent.cs` — **modify.** Add `FallRiskLevel : float 0..1` (default 0).
- `APIFramework/Components/BrokenItemComponent.cs` — broken glass / shattered ceramic should also support fall-risk. Add the same `FallRiskLevel` field, or attach a separate `FallRiskComponent` (Sonnet picks; the latter is more reusable, recommended).
- `APIFramework/Components/Tags.cs` — **add `LockedTag`** at end. **Coordinate with WP-3.0.1's `IsChokingTag` and WP-3.0.2's `CorpseTag` and WP-3.0.4's `StructuralTag`/`MutableTopologyTag`.**
- `APIFramework/Components/WorldObjectComponents.cs` — `DoorComponent` (or whatever the door entity carries). Read for the lock surface; this packet doesn't *modify* doors per-se, just adds a tag mechanism.
- `APIFramework/Systems/Movement/MovementSystem.cs` — read; this packet adds `SlipAndFallSystem` running parallel, not modifying movement.
- `APIFramework/Systems/Movement/PathfindingService.cs` (with WP-3.0.4's cache) — `ComputePath` is used by `LockoutDetectionSystem` for reachability queries. Reachability check: from NPC's tile to *any tile in the set of "exit" tiles*; pseudocode `bool HasReachable(npc) => exitTiles.Any(et => ComputePath(npcPos, et, lockoutSeed).Count > 0 || (npcPos == et))`.
- `APIFramework/Systems/Spatial/RoomMembershipSystem.cs` — used for "what room is the NPC in" queries.
- `APIFramework/Components/RoomComponent.cs` — the `Bounds` define rooms; the "exit" set is defined by named rooms tagged "Outdoors" or by named anchors (Parking Lot, Smoking Bench, loading dock outside).
- `APIFramework/Components/NamedAnchorComponent.cs` — read; the "exit" set is built from named anchors flagged as outdoor.
- `APIFramework/Components/MetabolismComponent.cs` (or `HungerComponent` if separate) — the hunger threshold for lockout-starvation. Read existing surface; do not modify.
- `APIFramework/Mutation/IWorldMutationApi.cs` (from WP-3.0.4) — `AttachObstacle(doorId)` is how a door gets locked at runtime; emits a `StructuralChangeEvent` which invalidates the path cache, so reachability becomes "stale" and `LockoutDetectionSystem` re-checks naturally on the next end-of-day cycle.
- `APIFramework/Systems/LifeState/LifeStateTransitionSystem.cs` (from WP-3.0.0) — `RequestTransition` calls.
- `APIFramework/Systems/Narrative/NarrativeEventBus.cs`, `NarrativeEventKind.cs` — `SlippedAndFell`, `StarvedAlone` already exist (WP-3.0.0). This packet does **not** add new kinds; it triggers existing ones.
- `APIFramework/Components/MovementComponent.cs` — `MovementSpeedFactor` is the NPC's current speed multiplier (mood-driven); fast NPCs slip more.
- `APIFramework/Components/StressComponent.cs` — high `AcuteLevel` increases slip risk (panic-walking).
- `APIFramework/Core/SeededRandom.cs` — for the slip roll.
- `APIFramework/Core/SimulationClock.cs` — `DayNumber` for end-of-day lockout detection cadence.
- `APIFramework/Core/SimulationBootstrapper.cs` (modified) — register `SlipAndFallSystem` (Cleanup, after MovementSystem), `LockoutDetectionSystem` (PreUpdate, runs once per game-day at a configured hour). Conflict: 3.0.1, 3.0.2 also touch this file.
- `APIFramework/Config/SimConfig.cs` (modified) — `SlipAndFallConfig`, `LockoutConfig`.
- `SimConfig.json` (modified) — corresponding sections.
- New JSON: `docs/c2-content/hazards/stain-fall-risk.json` — per-stain-kind fall-risk defaults.

---

## Non-goals

- Do **not** ship non-lethal slip outcomes (Bruise, Concussion, Embarrassment) at v0.1. Only fatal `SlippedAndFell`. The severity tier surface is a documented follow-up.
- Do **not** add NPC-autonomous lock / unlock behavior (an NPC locking another in a room as malice). At v0.1, locks come from world-bootstrap, from runtime `IWorldMutationApi.AttachObstacle` calls (player-driven via 3.1.D when that lands), or from broken-latch failures (broken-latch is a future packet). NPC-driven locking is mature-themes adjacent and deferred.
- Do **not** add starvation-from-no-food (the cafeteria has nothing). Hunger-100 + no-food-source is a different scenario; this packet only models hunger-100 + no-walkable-path. The two could combine in a future packet.
- Do **not** add rescue / break-out from a locked room. An NPC who notices a colleague is locked-in and unlocks the door. Substrate exists (`IWorldMutationApi.DetachObstacle`); the trigger is its own packet.
- Do **not** add fall-risk reactions per-archetype. The Old Hand may step around stains, the Newbie barrels through them. JSON tuning surface reserved for follow-up.
- Do **not** modify the existing stain spawn rules in `PhysicalManifestSpawner` beyond reading. Stains are spawned by chronicle events; this packet just adds a property to the resulting entities.
- Do **not** modify `WorldStateDto`, `Warden.Telemetry`, `Warden.Orchestrator`, `Warden.Anthropic`, `ECSCli`. Engine project (`APIFramework`) and tests only.
- Do **not** retry, recurse, or "self-heal." Fail closed per SRD §4.1.
- Do **not** add a runtime LLM call. (SRD §8.1.)
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing. Use `SeededRandom` and `SimulationClock`.

---

## Design notes

### `FallRiskComponent` (preferred over extending StainComponent)

```csharp
public struct FallRiskComponent
{
    public float RiskLevel;   // 0..1; multiplier on the slip roll
}
```

Attached to:
- Stain entities (water, blood, oil) with risk values from `stain-fall-risk.json`.
- Broken-item entities (broken mug, shattered glass) with default risk per kind.
- Future: ice patches, banana peels, polished-just-now floors.

Reasoning for a separate component over extending `StainComponent`: not all fall risks are stains, and not all stains are fall risks (a coffee stain on the carpet has near-zero risk). Decoupling keeps the surface clean.

### `LockedTag`

```csharp
// Tags.cs additive
public struct LockedTag {}
```

Attached to door entities when they're locked. Path-cost at the locked door's tile becomes infinite (treated as obstacle) by `PathfindingService`. This requires a small modification to `PathfindingService.BuildObstacleSet()` (in WP-3.0.4 the obstacle set rebuilds on cache miss): include `LockedTag` doors as obstacles.

### `SlipAndFallSystem`

Cleanup phase, after `MovementSystem`. Iterates Alive NPCs:

```csharp
foreach (var npc in em.Query<PositionComponent>().OrderBy(e => e.Id))
{
    if (!LifeStateGuard.IsAlive(npc)) continue;

    var pos = npc.Get<PositionComponent>();
    int tileX = (int)Math.Round(pos.X);
    int tileY = (int)Math.Round(pos.Z);

    // entities at this tile with FallRiskComponent
    var hazardsHere = em.Query<FallRiskComponent>()
        .Where(h => {
            if (!h.Has<PositionComponent>()) return false;
            var hp = h.Get<PositionComponent>();
            return (int)Math.Round(hp.X) == tileX && (int)Math.Round(hp.Z) == tileY;
        })
        .OrderBy(h => h.Id);

    foreach (var hazard in hazardsHere)
    {
        float risk = hazard.Get<FallRiskComponent>().RiskLevel;
        float speed = npc.Has<MovementComponent>() ? npc.Get<MovementComponent>().SpeedFactor : 1.0f;
        float stressMult = npc.Has<StressComponent>() && npc.Get<StressComponent>().AcuteLevel >= cfg.StressDangerThreshold ? cfg.StressSlipMultiplier : 1.0f;

        float slipChance = risk * speed * stressMult * cfg.GlobalSlipChanceScale;

        // single-shot per (npc, hazard, tick): SeededRandom roll
        if (rng.NextFloat(seed: HashTuple(npc.Id, hazard.Id, clock.CurrentTick)) < slipChance)
        {
            // FATAL SLIP — sudden death, no Incapacitated phase
            transitionSystem.RequestTransition(npc.Id, LifeState.Deceased, CauseOfDeath.SlippedAndFell);
            // do not break — only one slip per NPC per tick (move to next NPC)
            break;
        }
    }
}
```

**Determinism contract:** `rng.NextFloat(seed: HashTuple(npc.Id, hazard.Id, clock.CurrentTick))` is a stateless deterministic hash; identical inputs produce identical output. The 5000-tick test confirms.

The `GlobalSlipChanceScale` defaults to a low value (e.g., 0.001) so even high-risk stains rarely kill — but a panicked NPC running through a blood pool is a real risk. Tunable.

### `LockoutDetectionSystem`

PreUpdate phase, but **gated**: only runs on the first tick of each game-day (or at a configured `lockoutCheckHour`). Iterates Alive NPCs:

```csharp
foreach (var npc in em.Query<LifeStateComponent>().OrderBy(e => e.Id))
{
    if (!LifeStateGuard.IsAlive(npc)) continue;
    if (!npc.Has<MetabolismComponent>()) continue;
    if (npc.Get<MetabolismComponent>().HungerLevel < cfg.LockoutHungerThreshold) continue;

    // reachability check via pathfinding service
    bool canReachExit = exitTiles.Any(et =>
        pathService.ComputePath(npcTileX, npcTileY, et.X, et.Y, lockoutSeed).Count > 0
    );

    if (canReachExit)
    {
        // clear any pending lockout
        if (npc.Has<LockedInComponent>()) npc.Remove<LockedInComponent>();
        continue;
    }

    // can't reach exit + hungry = locked in
    if (!npc.Has<LockedInComponent>())
    {
        npc.Add(new LockedInComponent {
            FirstDetectedTick = clock.CurrentTick,
            StarvationTickBudget = cfg.StarvationTicks
        });
    }
    else
    {
        var li = npc.Get<LockedInComponent>();
        li.StarvationTickBudget--;
        npc.Set(li);

        if (li.StarvationTickBudget <= 0)
        {
            transitionSystem.RequestTransition(npc.Id, LifeState.Deceased, CauseOfDeath.StarvedAlone);
            npc.Remove<LockedInComponent>();
        }
    }
}
```

The exit-tile set is built once at boot from named-anchor entities tagged outdoor (`NamedAnchorComponent.IsOutdoor`) — Parking Lot, Smoking Bench, loading dock area, etc. The set updates only on `StructuralChangeBus.EntityAdded / EntityRemoved` for outdoor anchors (rare).

The `StarvationTickBudget` decrements **once per game-day** when the system runs, not once per tick. So `StarvationTicks` here is "game-days the NPC can survive without food", not real ticks. Default: 5 (5 game-days locked in = death). Document the units in the SimConfig comments.

### Reachability and the path cache

`LockoutDetectionSystem` makes ~15 `ComputePath` calls per Alive NPC per game-day (one per exit tile). 15 NPCs × 5 exits × 1 game-day = 75 path queries. With WP-3.0.4's cache, identical queries against the same `TopologyVersion` hit cache; topology only changes on door lock/unlock, room bounds change, structural moves. So lockout detection costs almost nothing on a stable world, and re-runs cleanly when the player locks a door.

### `LockedInComponent`

```csharp
public struct LockedInComponent
{
    public long FirstDetectedTick;
    public int  StarvationTickBudget;   // counts down once per game-day, not per tick
}
```

Attached on first detection; removed when the NPC regains exit reachability OR transitions to Deceased.

### Witness selection

For `SlippedAndFell`: the slip transition emits `NarrativeEventKind.SlippedAndFell` (already in WP-3.0.0); `LifeStateTransitionSystem` handles the witness selection at the `Deceased` transition moment. **This packet does not duplicate that logic** — by calling `RequestTransition`, all witness/memory/bereavement wiring runs through 3.0.0 and 3.0.2 naturally.

For `StarvedAlone`: same. The deceased is alone (locked in) — `WitnessedByNpcId` will resolve to `Guid.Empty` per WP-3.0.0's selection. The bereavement cascade in 3.0.2 fires for relationship-bearing colleagues even without a direct witness.

### Stress feeds for the slip roll

A stressed NPC walking through a hazard is more likely to slip:

```jsonc
{
  "slipAndFall": {
    "globalSlipChanceScale":   0.001,   // tune carefully; balance default
    "stressDangerThreshold":   60,      // AcuteLevel above which stressSlipMultiplier kicks in
    "stressSlipMultiplier":    2.0,     // doubles slip chance under stress
    "fallRiskBrokenItemDefault": 0.50,
    "fallRiskWaterDefault":      0.40,
    "fallRiskBloodDefault":      0.60,
    "fallRiskOilDefault":        0.85
  },
  "lockout": {
    "lockoutCheckHour":         18.0,   // 6 PM — end of office day
    "lockoutHungerThreshold":   95,     // hunger above which lockout-detection cares
    "starvationTicks":          5,      // game-DAYS the NPC can survive (note units)
    "exitNamedAnchorTag":       "outdoor"
  }
}
```

### `stain-fall-risk.json`

```jsonc
{
  "schemaVersion": "0.1.0",
  "stainKindFallRisk": [
    {"kind": "water",       "fallRiskLevel": 0.40},
    {"kind": "blood",       "fallRiskLevel": 0.60},
    {"kind": "oil",         "fallRiskLevel": 0.85},
    {"kind": "coffee",      "fallRiskLevel": 0.30},
    {"kind": "vomit",       "fallRiskLevel": 0.55},
    {"kind": "urine",       "fallRiskLevel": 0.45},
    {"kind": "broken-glass","fallRiskLevel": 0.70}
  ]
}
```

`PhysicalManifestSpawner` (or a small extension to it) reads this catalog and attaches `FallRiskComponent` to the spawned stain with the appropriate risk level. Stain-kind defaults that aren't in the catalog default to `0.0` (no fall risk).

### Determinism

- `SlipAndFallSystem`: deterministic seeded roll keyed by `(npc.Id, hazard.Id, clock.CurrentTick)`. Iteration in `OrderBy(EntityIntId)`.
- `LockoutDetectionSystem`: `pathService.ComputePath` is deterministic (same inputs = same output, cached or not). `exitTiles` set built deterministically at boot.
- 5000-tick test confirms.

### Tests

- `FallRiskComponentTests.cs` — construction.
- `LockedInComponentTests.cs` — construction.
- `LockedTagTests.cs` — tag attach/detach semantics.
- `SlipAndFallSystemHighRiskTests.cs` — NPC at high-risk stain tile, high speed, high stress: slip occurs deterministically (seeded). After slip: `RequestTransition(npc, Deceased, SlippedAndFell)` queued.
- `SlipAndFallSystemLowRiskTests.cs` — NPC at low-risk stain tile, calm, slow: no slip across 1000 ticks.
- `SlipAndFallSystemDeceasedSkipTests.cs` — already-Deceased NPCs not iterated.
- `SlipAndFallSystemNoStainTests.cs` — NPC walking on tiles with no FallRiskComponent: no slip.
- `LockoutDetectionPathReachableTests.cs` — NPC with hunger 100 in a room with door open: no lockout, no `LockedInComponent`.
- `LockoutDetectionPathUnreachableTests.cs` — NPC with hunger 100 in a room with all doors `LockedTag`: gains `LockedInComponent` on next end-of-day.
- `LockoutDetectionStarvationProgressionTests.cs` — over 5 game-days locked in, `StarvationTickBudget` decrements; on day 5+1, transition to `Deceased(StarvedAlone)`.
- `LockoutDetectionRecoveryTests.cs` — NPC locked in for 3 days; player `IWorldMutationApi.DetachObstacle(doorId)` unlocks the door; on next end-of-day, `LockedInComponent` removed; NPC survives.
- `LockoutDetectionLowHungerTests.cs` — NPC with no path to exit but hunger only 50: no `LockedInComponent` (the threshold gates the system).
- `LockoutBereavementIntegrationTests.cs` — once `StarvedAlone` fires, 3.0.2's bereavement cascade applies (assumes 3.0.2 is merged when this test runs; document if not).
- `SlipAndFallStressMultiplierTests.cs` — same NPC, same stain, same speed: stressed NPC slips at higher rate (statistical over N seeds).
- `SlipAndFallDeterminismTests.cs` — 5000-tick run with one slip event at tick 1500: byte-identical state across two seeds.
- `LockoutDeterminismTests.cs` — 5000-tick run with a lock event on day 1, NPC with hunger 100 from day 2 onward: byte-identical across two seeds; death on the same tick.
- `StainFallRiskJsonTests.cs` — catalog loads, all stain kinds present, fall-risk values in 0..1.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/FallRiskComponent.cs` | Per-tile fall risk. |
| code | `APIFramework/Components/LockedInComponent.cs` | Per-NPC lockout state. |
| code | `APIFramework/Components/Tags.cs` (modified) | Add `LockedTag`. **Coordinate with WP-3.0.1 (`IsChokingTag`), 3.0.2 (`CorpseTag`), 3.0.4 (`StructuralTag`/`MutableTopologyTag`).** |
| code | `APIFramework/Systems/Movement/PathfindingService.cs` (modified) | `BuildObstacleSet()` includes `LockedTag` doors. |
| code | `APIFramework/Systems/LifeState/SlipAndFallSystem.cs` | Per-tick slip detection. |
| code | `APIFramework/Systems/LifeState/LockoutDetectionSystem.cs` | End-of-day reachability + starvation. |
| code | `APIFramework/Systems/Chronicle/PhysicalManifestSpawner.cs` (modified) | Read `stain-fall-risk.json` and attach `FallRiskComponent` to spawned stain entities. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `SlipAndFallSystem` (Cleanup, after Movement) and `LockoutDetectionSystem` (PreUpdate, gated by clock). **Conflict with 3.0.1 / 3.0.2 — keep all.** |
| code | `APIFramework/Config/SimConfig.cs` (modified) | `SlipAndFallConfig`, `LockoutConfig`. |
| code | `SimConfig.json` (modified) | Two new sections. |
| data | `docs/c2-content/hazards/stain-fall-risk.json` | Per-kind fall-risk defaults. |
| code | `APIFramework.Tests/Components/FallRiskComponentTests.cs` | Component shape. |
| code | `APIFramework.Tests/Components/LockedInComponentTests.cs` | Component shape. |
| code | `APIFramework.Tests/Systems/LifeState/SlipAndFallSystemHighRiskTests.cs` | High-risk slip path. |
| code | `APIFramework.Tests/Systems/LifeState/SlipAndFallSystemLowRiskTests.cs` | No-slip baseline. |
| code | `APIFramework.Tests/Systems/LifeState/SlipAndFallSystemNoStainTests.cs` | No-hazard path. |
| code | `APIFramework.Tests/Systems/LifeState/SlipAndFallStressMultiplierTests.cs` | Stress amplifies. |
| code | `APIFramework.Tests/Systems/LifeState/SlipAndFallSystemDeceasedSkipTests.cs` | Deceased not iterated. |
| code | `APIFramework.Tests/Systems/LifeState/LockoutDetectionPathReachableTests.cs` | Open door = no lockout. |
| code | `APIFramework.Tests/Systems/LifeState/LockoutDetectionPathUnreachableTests.cs` | Locked door = lockout. |
| code | `APIFramework.Tests/Systems/LifeState/LockoutDetectionStarvationProgressionTests.cs` | Day-by-day countdown. |
| code | `APIFramework.Tests/Systems/LifeState/LockoutDetectionRecoveryTests.cs` | Unlock = recovery. |
| code | `APIFramework.Tests/Systems/LifeState/LockoutDetectionLowHungerTests.cs` | Threshold gates. |
| code | `APIFramework.Tests/Integration/LockoutBereavementIntegrationTests.cs` | Bereavement cascade fires. |
| code | `APIFramework.Tests/Determinism/SlipAndFallDeterminismTests.cs` | 5000-tick byte-identical. |
| code | `APIFramework.Tests/Determinism/LockoutDeterminismTests.cs` | Same. |
| code | `APIFramework.Tests/Data/StainFallRiskJsonTests.cs` | JSON validation. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.0.3.md` | Completion note. SimConfig defaults. The first observed end-to-end slip-and-fall and starvation deaths (with seeds). Whether `BuildObstacleSet()` extension required modification of cache key shape (it shouldn't — `LockedTag` flips obstacle status, which fires `StructuralChangeEvent` via the obstacle-attach verb in `IWorldMutationApi`, which invalidates the cache; document this chain). `ECSCli ai describe` regen + `FactSheetStalenessTests` regen. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `FallRiskComponent`, `LockedInComponent`, `LockedTag` compile and instantiate. | unit-test |
| AT-02 | `PhysicalManifestSpawner` attaches `FallRiskComponent` to stain entities with risk values from the catalog; non-cataloged kinds get 0. | integration-test |
| AT-03 | Pathfinding treats `LockedTag` doors as obstacles; `ComputePath` returns no path through a locked door (or, equivalently, returns a path that goes around it). | unit-test |
| AT-04 | `SlipAndFallSystem`: NPC on a tile with `FallRiskComponent.RiskLevel = 0.85` at `MovementSpeedFactor = 2.0` and `StressComponent.AcuteLevel = 80`: slip occurs deterministically (seeded) within reasonable bounds. | unit-test |
| AT-05 | `SlipAndFallSystem`: NPC on tile with `FallRiskComponent.RiskLevel = 0.10` at `MovementSpeedFactor = 1.0`, calm: no slip across 5000 ticks. | unit-test |
| AT-06 | `SlipAndFallSystem`: NPC on tile with no `FallRiskComponent`: no slip. | unit-test |
| AT-07 | `SlipAndFallSystem` calls `RequestTransition(npc, Deceased, SlippedAndFell)` on slip; LifeStateTransitionSystem flips state next tick. | integration-test |
| AT-08 | Already-Deceased NPCs are not iterated by `SlipAndFallSystem`. | unit-test |
| AT-09 | Stress amplification: same NPC + stain + speed; stressed NPC has 2× slip probability over N seeds (statistical). | unit-test |
| AT-10 | `LockoutDetectionSystem`: NPC with hunger 100 in a room with one door (unlocked): no `LockedInComponent`. | integration-test |
| AT-11 | `LockoutDetectionSystem`: NPC with hunger 100 in a room with all doors `LockedTag`: on next end-of-day tick, `LockedInComponent` attached with `StarvationTickBudget = starvationTicks`. | integration-test |
| AT-12 | `LockoutDetectionSystem`: over `starvationTicks` game-days locked in, the budget decrements per game-day; on the day after expiry, transition to `Deceased(StarvedAlone)`. | integration-test |
| AT-13 | Recovery: NPC locked in for 3 days; player calls `IWorldMutationApi.DetachObstacle(doorId)` to remove `LockedTag`; on next end-of-day, the NPC has reachability, `LockedInComponent` removed, NPC stays Alive. | integration-test |
| AT-14 | Low-hunger gate: NPC with no path to exit but hunger 50 (below threshold): no `LockedInComponent`. | unit-test |
| AT-15 | Bereavement cascade fires when `StarvedAlone` triggers (assumes WP-3.0.2 merged; otherwise verifies the narrative emits with `WitnessedByNpcId == Guid.Empty`). | integration-test |
| AT-16 | Path cache invalidation: locking a door (via `IWorldMutationApi.AttachObstacle` from WP-3.0.4) invalidates the path cache, so the next `LockoutDetectionSystem` run sees the new topology. | integration-test |
| AT-17 | `stain-fall-risk.json` loads cleanly, all 7 kinds present, all values 0..1. | unit-test |
| AT-18 | Determinism: 5000-tick run with scripted slip and lockout events: byte-identical state across two seeds. | unit-test |
| AT-19 | All Phase 0, 1, 2, WP-3.0.0, WP-3.0.4 tests stay green. | regression |
| AT-20 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-21 | `dotnet test ECSSimulation.sln` — all green, no exclusions. | build + unit-test |
| AT-22 | `ECSCli ai describe` regenerates with two new systems and the new components/tags listed; `FactSheetStalenessTests` updated. | build + unit-test |

---

## Followups (not in scope)

- **Severity tier for slip-and-fall.** `Bruise | Concussion | Death`. v0.1 ships only Death. Future packet adds non-lethal outcomes — concussion as an Incapacitated transition that recovers; bruise as a small health/mood decrement. Will require expanding `LifeStateComponent` semantics (Incapacitated-from-bruising vs Incapacitated-from-choking) — this packet's design preserves room for it.
- **Per-archetype slip biasing.** The Old Hand sees the stain and steps over it; the Newbie barrels through. JSON `archetype-slip-bias.json`.
- **Cleanup behaviour.** An NPC notices a stain and gets a `Clean(stain)` candidate in action-selection. Couples with workload (dedicated cleaner archetype: Donna's Microwave Reluctant Crew). Future packet.
- **Rescue from lockout.** An NPC notices a colleague locked in (via dialog memory or schedule mismatch) and goes to unlock. Substrate exists; trigger is its own packet.
- **Active locking.** NPC autonomously locks a door for solitude / privacy. Mature-themes adjacent (Donna locks the Women's Bathroom, refuses to come out). Future.
- **Broken-latch failure.** A door entity has a small probability of a broken-latch event that locks it from outside without intent. Couples to the broken-item / repair systems. Speculative.
- **Multi-floor lockout.** Stairwells as exits; an NPC on the top floor has a path to the parking lot through a stairwell. When multi-floor lands (currently single-floor v0.1 per memory), this extends naturally — the exit set expands to include all outdoor anchors across all floors.
- **Window as escape.** A first-floor NPC could climb out a window during a lockout. Currently windows are non-walkable. Future polish.
- **The plague-week starvation case.** A locked-in NPC during the plague week (when nobody comes to work) — the existing lockout system handles it correctly (no exit + hunger-100 → starvation), assuming the building auto-locks when nobody's present. Couples to a future "after-hours building lockdown" mechanic.
- **Stain decay over time.** Stains currently persist indefinitely per `WP-1.9.A`. A future packet may decay fall-risk over time (oil eventually wears off; blood is removed by janitorial). Not in scope.
- **Player-visible UI.** Diegetic per the design philosophy — visible stain on the floor (the host renders it), visible lock indicator on the door (the host renders it). Notifications about a death come through the bereavement system 3.0.2 ships. UX/UI bible-driven; 3.1.E.


---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT NEEDED

This is a Track 1 (engine) packet. All verification is handled by the xUnit test suite. Once `dotnet test` returns green for `APIFramework.Tests` (and any other affected test project), the packet is ready to push and PR. **No Unity Editor steps required.**

The Sonnet executor's pipeline:

1. Implement the spec.
2. Add or update xUnit tests to cover all acceptance criteria.
3. Run `dotnet test` from the repo root. Must be green.
4. Run `dotnet build` to confirm no warnings introduced.
5. Stage all changes including the self-cleanup deletion (see below).
6. Commit on the worktree's feature branch.
7. Push the branch and open a PR against `staging`.
8. Stop. Do **not** merge. Talon merges after review.

If a test fails or compile fails, fix the underlying cause. Do **not** skip tests, do **not** mark expected-failures, do **not** push a red branch.

### Cost envelope (1-5-25 Claude army)

Target: **$0.50–$1.20** per packet wall-time on the orchestrator. Timebox is stated above in the packet header. If the executing Sonnet observes its own cost approaching the upper bound without nearing acceptance criteria, **escalate to Talon** by stopping work and committing a `WP-X-blocker.md` note to the worktree explaining what burned the budget. Do not silently exceed the envelope.

Cost-discipline rules of thumb:
- Read reference files at most once per session — cache content in working memory rather than re-reading.
- Run `dotnet test` against the focused subset (`--filter`) during iteration; full suite only at the end.
- If a refactor is pulling far more files than the spec named, stop and re-read the spec; the spec may be wrong about scope.

### Self-cleanup on merge

The active `docs/c2-infrastructure/work-packets/` directory should contain only **pending** packets. Shipped packets are deleted, not archived to `_completed-specs/` (Talon's convention from 2026-04-30 forward).

Before opening the PR, the executing Sonnet must:

1. **Check downstream dependents** with this command from the repo root:
   ```bash
   git grep -l "<THIS-PACKET-ID>" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```
   Replace `<THIS-PACKET-ID>` with this packet's identifier (e.g., `WP-3.0.4`).

2. **If the grep returns no results** (no other pending packet references this one): include `git rm docs/c2-infrastructure/work-packets/<this-packet-filename>.md` in the staging set. The deletion ships in the same commit as the implementation. Add the line `Self-cleanup: spec file deleted, no pending dependents.` to the commit message.

3. **If the grep returns one or more pending packets**: leave the spec file in place. Add a one-line status header to the top of this spec file (immediately under the H1):
   ```markdown
   > **STATUS:** SHIPPED to staging YYYY-MM-DD. Retained because pending packets depend on this spec: <list>.
   ```
   Add the line `Self-cleanup: spec retained, dependents: <list>.` to the commit message.

4. **Do not touch** files under `_completed/` or `_completed-specs/` — those are historical artifacts from earlier phases.

5. The git history (commit message + PR body) is the historical record. The spec file itself is ephemeral once shipped without dependents.
