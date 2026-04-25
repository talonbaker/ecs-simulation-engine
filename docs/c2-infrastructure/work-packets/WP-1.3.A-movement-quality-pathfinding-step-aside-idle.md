# WP-1.3.A — Movement Quality: Pathfinding + Step-Aside + Idle Jitter + Facing Direction

**Tier:** Sonnet
**Depends on:** WP-1.1.A (spatial index, room membership, proximity events). Already merged on `staging`.
**Parallel-safe with:** WP-1.2.A (Lighting), WP-1.6.A (Narrative telemetry). Different file footprints; only `SimulationBootstrapper.cs` and `SimConfig.json` are commonly touched and conflicts there are sectional/auto-mergeable.
**Timebox:** 100 minutes
**Budget:** $0.50

---

## Goal

Land the movement-quality layer the aesthetic bible commits to: movement that "feels intentional, not robotic, even at low fidelity." Five things upgrade in `APIFramework`:

1. **Pathfinding that prefers natural paths.** A grid A* implementation that avoids obstacles, prefers doorways and hallways, and adds a small per-NPC randomness so two NPCs taking the same trip don't trace identical lines.

2. **Step-aside-in-hallways.** When two NPCs approach head-on within a narrow hallway, both shift slightly to one side. The side they pick is consistent for that NPC (a `LeftSideHandedness | RightSideHandedness` personality micro-trait stored on the entity) — always-pass-on-left vs always-pass-on-right reads as character.

3. **Idle micro-movement when stationary.** Stationary NPCs are not perfectly still. Small position jitter, occasional posture shifts, looking-around behavior. The bible commits to this: stillness reads as broken.

4. **Movement speed varies with mood.** High `irritation` walks faster, high `affection` slower, tired walks slower. Observable from a distance; part of what makes the world readable. Speed modulation reads from social drives plus existing energy state and applies to the existing `MovementSystem` velocity.

5. **Facing direction as queryable state.** A `FacingComponent` exposes which direction an entity is looking — toward their conversation partner, toward their work, toward the door if they're about to leave. The bible says facing is meaningful. Other systems will read this; this packet ships the *state* and keeps it consistent with movement.

What this packet does **not** do: visualization (the bible is explicit that movement quality is engine concern, not render concern; the headless engine can be alive in a debug renderer). No telemetry projection of facing-direction or step-aside on the wire (the v0.3 schema doesn't model those — when a future schema bump reserves the surface, projector follows).

---

## Reference files

- `docs/c2-content/DRAFT-aesthetic-bible.md` — priority-3 (movement). Source of every commitment in this packet. **Read first.** Section "What the engine commits to" enumerates what to ship.
- `docs/c2-content/DRAFT-cast-bible.md` — pattern reference: handedness as a personality micro-trait belongs to the same family of small character markers as silhouette and the "deal."
- `docs/c2-infrastructure/work-packets/_completed/WP-1.1.A.md` — confirms spatial index and room membership are available.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.4.A.md` — confirms `SocialDrivesComponent` exists and exposes the `Irritation` and `Affection` drives that modulate speed.
- `APIFramework/Systems/MovementSystem.cs` — the existing movement system. This packet **extends** it; it does not rewrite it from scratch. Read its structure to know what a velocity vector and a movement target look like in this engine.
- `APIFramework/Components/MovementComponent.cs`, `MovementTargetComponent.cs`, `PositionComponent.cs` — existing surfaces. Modifications are additive.
- `APIFramework/Core/ISpatialIndex.cs`, `APIFramework/Core/GridSpatialIndex.cs` — pathfinding consults the spatial index for obstacle queries; head-on detection consults it for the pathfinding-aware step-aside logic.
- `APIFramework/Components/EnergyComponent.cs` — existing energy state. Tired NPCs walk slower; this packet wires that.
- `APIFramework/Components/SocialDrivesComponent.cs` — from WP-1.4.A. Irritation and affection modulate speed.
- `APIFramework/Core/SimulationBootstrapper.cs` — system + service registration site.
- `APIFramework/Core/SystemPhase.cs` — phase enum.
- `APIFramework/Core/SeededRandom.cs` — RNG source for path randomness, idle jitter, posture shifts. Required for determinism.
- `SimConfig.json` — runtime tuning lives here.
- `APIFramework.Tests/Systems/MovementSystemTests.cs` (if it exists) — pattern reference for movement test layout.

## Non-goals

- Do **not** modify `Warden.Telemetry/TelemetryProjector.cs` or any file under `Warden.Telemetry/`. Projector population is deferred. **This is the parallel-safety contract with WP-1.2.A and WP-1.6.A.**
- Do **not** modify any file under `Warden.Contracts/`. No DTO changes; facing direction is engine-internal until a future schema bump.
- Do **not** modify any file under `docs/c2-infrastructure/schemas/`. No schema bump.
- Do **not** rewrite `MovementSystem` from scratch. The existing system handles velocity-based motion correctly. This packet adds pathfinding *upstream* of it (computing the next waypoint), step-aside as a velocity *modifier*, idle jitter as an *additional* movement source for stationary entities, and facing as a *byproduct* of velocity direction. The existing system's contract stays intact.
- Do **not** implement collision response or push-back physics. Step-aside is preventive (computed before movement to avoid head-on); collisions are not modeled.
- Do **not** add multi-agent path coordination (cooperative pathfinding). Each NPC plans its own path independently; coincidence collisions are handled by step-aside, not by global coordination.
- Do **not** implement long-term schedules ("at 9am Donna goes to the breakroom"). Schedule-driven movement targets are a later content packet (probably part of cast-generator or a separate behavior packet). This packet ships the *quality* of movement; *what* an NPC moves toward is given.
- Do **not** read or modify lighting state. WP-1.5.A wires lighting-affects-movement-speed if needed; this packet uses only mood + energy.
- Do **not** add pathfinding-cost penalties for crossing rooms (sticking to one room costs less than traversing). Pathfinding is room-agnostic; the world is grid-tiled and room boundaries are not walls. (When walls become real geometry, a follow-up adds room-edge penalties.)
- Do **not** add a NuGet dependency.
- Do **not** use `System.Random`. `SeededRandom` only.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)

---

## Design notes

### Pathfinding — grid A* with light noise

`PathfindingService` (singleton in DI):

- `ComputePath(int fromX, int fromY, int toX, int toY, int seed) : IReadOnlyList<(int X, int Y)>` — returns the sequence of tile waypoints. `seed` lets the caller produce a slightly different path on a re-plan if the previous path failed.
- Implementation: standard A* on the tile grid. Heuristic is Manhattan distance (the world is grid-aligned, no diagonals at v0.3 — diagonals are a follow-up if the bible's "natural paths" preference reads weakly without them).
- Obstacle query: `ISpatialIndex.QueryRadius(x, y, 0.5)` for each tile candidate; if any entity has an `ObstacleTag` (new in this packet, marking immovable furniture, walls), the tile is impassable. NPCs are not obstacles to each other (they handle each other via step-aside).
- Path randomness: small tie-break noise on equal-f-cost neighbors. Apply `seed`-derived perturbation so the same input + seed produces deterministic output, but two NPCs taking the same trip with different seeds get slightly different paths. This is the "natural paths" element — repeated trips don't trace identical lines.
- Doorway preference: tiles that are within a `RoomComponent.Bounds` *and* within 1 tile of another room's bounds are doorway tiles. A small cost discount (`-1` from f-cost) makes paths prefer doorways. A door registry is built once per tick at the start of pathfinding from current room geometry; cached for re-use within a tick.

A new `PathfindingSystem` doesn't run per-tick; it's invoked on demand by `MovementTargetUpdateSystem` (existing or new) when an NPC's `MovementTargetComponent` changes. Cached paths live on a new `PathComponent` on the NPC; the existing `MovementSystem` advances the NPC along the cached path's waypoints.

### Obstacles

Add an `ObstacleTag` component for entities that block pathfinding. The world bible's named anchors (the Microwave, the Fridge, the Conference Room TV, etc.) are obstacles when they exist as entities — but those don't exist yet (world-bootstrap is Phase 1.7). For this packet, the tag exists; tests place obstacle entities manually to validate pathfinding avoids them.

### Step-aside-in-hallways

Per NPC, a `HandednessComponent` carries `Side: HandednessSide` (`LeftSidePass | RightSidePass`). Set at NPC spawn (later, by cast generator). For now, default to `RightSidePass` and tests vary it.

`StepAsideSystem` (per-tick, after pathfinding-system, before MovementSystem):

- For each NPC moving along a path, query the spatial index for other NPCs within `SimConfig.movementStepAsideRadius` (default 3 tiles).
- For each other NPC nearby, compute the relative vector. If the two are approaching head-on (both moving toward each other within a 30° cone), apply a perpendicular shift to each NPC's *next-tick velocity* of `SimConfig.movementStepAsideShift` tiles in the direction matching their handedness. `LeftSidePass`'s perpendicular shift goes left of their motion vector; `RightSidePass`'s goes right.
- The shift is a velocity *modification*, not a path edit. The next tick's MovementSystem applies the modified velocity; the path replan happens organically as positions drift.
- Hallway-only is enforced by checking whether the two NPCs are in a `Hallway`-category room. NPCs in open rooms (breakroom, parking lot) don't step aside; they just brush past each other.

### Idle jitter

`IdleMovementSystem` (per-tick, after MovementSystem):

- For each NPC entity that has no active movement target (`MovementTargetComponent.IsActive == false`), apply a small random position perturbation with magnitude `SimConfig.movementIdleJitterTiles` (default 0.05 — sub-tile, just enough to read as "they shifted").
- Posture shifts: with probability `SimConfig.movementIdlePostureShiftProb` per tick (default 0.005, so once every ~3 minutes of game time at 1Hz tick), pick a random direction within ±90° of current facing and rotate facing to it. The result is the looking-around behavior the bible commits to.
- Use `SeededRandom`. Determinism preserved.

### Movement speed modulation

`MovementSpeedModifierSystem` (per-tick, before MovementSystem):

- For each NPC, compute a speed multiplier:
  - Base `1.0`.
  - + `irritation_current / 200` (irritation 100 adds 0.5x; irritation 0 adds 0.0x).
  - − `affection_current / 300` (affection 100 subtracts 0.33x; affection 0 subtracts 0.0x).
  - − `(100 - energy_current) / 200` (low energy subtracts; high energy doesn't).
- Clamp to `[0.3, 2.0]` so an NPC never goes too slow (frozen) or too fast (running), since at this fidelity the simulation is walking-only.
- Write the multiplier to a transient `SpeedModifier` field on `MovementComponent`. The existing `MovementSystem` reads this and scales the velocity.
- The existing component schema may not have a `SpeedModifier` field; add one with default 1.0. Backward-compatible; existing tests stay green.

### Facing direction

`FacingComponent`:
- `DirectionDeg: float` — `0..360`, 0 = north, 90 = east. (Same convention as sun azimuth in WP-1.2.A.)
- `Source: FacingSource` enum — `MovementVelocity, ConversationPartner, FixedTarget, Idle` — describes *why* the entity is facing this way.

`FacingSystem` (per-tick, after MovementSystem):

- For each NPC with a `FacingComponent` and a non-zero velocity, set `DirectionDeg` from velocity vector and `Source = MovementVelocity`.
- For each NPC with a non-zero velocity AND a `ProximityComponent` showing an in-conversation-range partner, override: face the partner. `Source = ConversationPartner`. (Future packets will refine "partner" — currently use the proximity-bus's most recent `EnteredConversationRange` partner.)
- For idle NPCs (no movement, no conversation partner), `IdleMovementSystem`'s posture-shift logic owns facing updates.

### Phase ordering

In `SystemPhase`, after spatial / lighting:
- (existing) SpatialIndexSync → RoomMembership → SunSystem → LightSourceStateSystem → ApertureBeamSystem → IlluminationAccumulationSystem → ProximityEventSystem
- **NEW: PathfindingTriggerSystem** — checks `MovementTargetComponent` changes, requests path from `PathfindingService`, writes to `PathComponent`.
- **NEW: MovementSpeedModifierSystem** — computes per-NPC speed multiplier from drives + energy.
- **NEW: StepAsideSystem** — applies head-on step-aside velocity modifications.
- (existing) MovementSystem — advances positions along velocity (now scaled and shifted).
- **NEW: FacingSystem** — updates facing from velocity / conversation partner.
- **NEW: IdleMovementSystem** — applies jitter and posture shifts to idle NPCs.

### Determinism

All RNG goes through `SeededRandom`. Two runs with the same seed produce byte-identical movement trajectories. Tests verify with a 5000-tick determinism check.

### SimConfig additions

```jsonc
{
  "movement": {
    "stepAsideRadius": 3,
    "stepAsideShift": 0.4,
    "idleJitterTiles": 0.05,
    "idlePostureShiftProb": 0.005,
    "speedModifier": {
      "irritationGainPerPoint":  0.005,
      "affectionLossPerPoint":   0.0033,
      "lowEnergyLossPerPoint":   0.005,
      "minMultiplier":           0.3,
      "maxMultiplier":           2.0
    },
    "pathfinding": {
      "doorwayDiscount":  1.0,
      "tieBreakNoiseScale": 0.1
    }
  }
}
```

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/HandednessComponent.cs` | Single field `HandednessSide Side`. Enum: `LeftSidePass, RightSidePass`. |
| code | `APIFramework/Components/HandednessSide.cs` | The enum. |
| code | `APIFramework/Components/FacingComponent.cs` | `(float DirectionDeg, FacingSource Source)`. |
| code | `APIFramework/Components/FacingSource.cs` | Enum: `MovementVelocity, ConversationPartner, FixedTarget, Idle`. |
| code | `APIFramework/Components/PathComponent.cs` | `IReadOnlyList<(int X, int Y)> Waypoints`, `int CurrentWaypointIndex`. |
| code | `APIFramework/Components/Tags.cs` (modified) | Add `ObstacleTag`. |
| code | `APIFramework/Components/MovementComponent.cs` (modified) | Add `float SpeedModifier` field with default 1.0. |
| code | `APIFramework/Systems/Movement/PathfindingService.cs` | Singleton. A* implementation with doorway preference and seeded tie-break noise. |
| code | `APIFramework/Systems/Movement/PathfindingTriggerSystem.cs` | Per-tick: detects `MovementTargetComponent` changes, requests new path, writes `PathComponent`. |
| code | `APIFramework/Systems/Movement/MovementSpeedModifierSystem.cs` | Per-tick: computes per-NPC speed multiplier from drives + energy; writes `MovementComponent.SpeedModifier`. |
| code | `APIFramework/Systems/Movement/StepAsideSystem.cs` | Per-tick: applies head-on step-aside velocity modifications. |
| code | `APIFramework/Systems/Movement/FacingSystem.cs` | Per-tick: updates facing from velocity / conversation partner. |
| code | `APIFramework/Systems/Movement/IdleMovementSystem.cs` | Per-tick: idle jitter and posture shifts for stationary NPCs. |
| code | `APIFramework/Systems/MovementSystem.cs` (modified) | Read `SpeedModifier` and scale velocity accordingly. Read `PathComponent` waypoints (if present) and treat as path-following instead of direct-target. Existing direct-target behavior preserved when `PathComponent` is absent. |
| code | `APIFramework/Components/EntityTemplates.cs` (modified) | Add `WithMovementQuality(...)` helper that adds `HandednessComponent` + `FacingComponent` + ensures `MovementComponent` has the new field defaulted. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `PathfindingService` (singleton), six new systems in correct phase order. |
| code | `APIFramework/Core/SystemPhase.cs` (modified, if needed) | Add a `Movement` phase if cleaner than slotting into existing. |
| code | `SimConfig.json` (modified) | Add the `movement` section per Design notes. |
| code | `APIFramework.Tests/Systems/Movement/PathfindingServiceTests.cs` | A* finds shortest path on a clean grid; routes around obstacles; prefers doorways with the configured discount; seeded tie-break noise produces deterministic-but-varied paths across seeds. |
| code | `APIFramework.Tests/Systems/Movement/StepAsideSystemTests.cs` | Two NPCs in a Hallway approaching head-on each receive a perpendicular shift in their handedness direction. NPCs in a Breakroom (non-hallway) do not step aside. |
| code | `APIFramework.Tests/Systems/Movement/MovementSpeedModifierSystemTests.cs` | High irritation increases multiplier; high affection decreases; low energy decreases; clamps at min and max bounds. |
| code | `APIFramework.Tests/Systems/Movement/IdleMovementSystemTests.cs` | Idle NPCs receive small position jitter; posture shifts occur at expected probability; NPCs with active movement targets receive no jitter. |
| code | `APIFramework.Tests/Systems/Movement/FacingSystemTests.cs` | Facing follows velocity; facing overrides to conversation partner when in conversation range; idle facing handled by IdleMovementSystem. |
| code | `APIFramework.Tests/Systems/Movement/MovementDeterminismTests.cs` | Two runs, same seed, 5000 ticks of varied movement scenarios → byte-identical position and facing trajectories. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.3.A.md` | Completion note. Standard template. Enumerate (a) what runtime quality is now visible (paths, step-aside, idle, speed modulation, facing), (b) what's deferred (lighting-affects-speed, multi-agent coordination, schedules). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | All new components compile, instantiate with sensible defaults, pass invariant checks. | unit-test |
| AT-02 | `PathfindingService` finds the shortest path on a clean grid (verified by step count). | unit-test |
| AT-03 | `PathfindingService` routes around an `ObstacleTag` entity. | unit-test |
| AT-04 | `PathfindingService` with seed `A` and seed `B` produces different but valid paths between the same endpoints when multiple equal-cost paths exist. | unit-test |
| AT-05 | `PathfindingService` with the same seed produces identical paths across two calls. | unit-test |
| AT-06 | `StepAsideSystem`: two NPCs approaching head-on in a Hallway-category room each get a perpendicular shift in their handedness direction. | unit-test |
| AT-07 | `StepAsideSystem`: two NPCs in a Breakroom-category room do not get a step-aside shift. | unit-test |
| AT-08 | `MovementSpeedModifierSystem`: an NPC with `irritation = 100` walks faster than the same NPC with `irritation = 0`. | unit-test |
| AT-09 | `MovementSpeedModifierSystem`: an NPC with `energy = 0` walks slower than the same NPC with `energy = 100`. | unit-test |
| AT-10 | `MovementSpeedModifierSystem`: speed multiplier is clamped to `[0.3, 2.0]`. | unit-test |
| AT-11 | `IdleMovementSystem`: an NPC with no active movement target receives a tiny jitter shift each tick on average within `SimConfig.movementIdleJitterTiles`. | unit-test |
| AT-12 | `IdleMovementSystem`: an NPC with an active movement target receives zero jitter (idle is mutually exclusive with goal-directed movement). | unit-test |
| AT-13 | `FacingSystem`: facing direction matches velocity vector when an NPC is moving and not in conversation range with anyone. | unit-test |
| AT-14 | `FacingSystem`: facing direction overrides to point at conversation partner when in conversation range. | unit-test |
| AT-15 | `MovementDeterminismTests` produce byte-identical trajectories across two runs with the same seed over 5000 ticks. | unit-test |
| AT-16 | `Warden.Telemetry.Tests` all pass — projector unchanged. | build + unit-test |
| AT-17 | All existing `APIFramework.Tests` stay green (rooms, social, lighting, physiology). | build + unit-test |
| AT-18 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-19 | `dotnet test ECSSimulation.sln` — every existing test stays green; new tests pass. | build |

---

## Followups (not in scope)

- WP-1.5.A overlap: lighting-affects-speed (NPCs walk slower in dim hallways) — small modifier hooked into MovementSpeedModifierSystem reading `RoomComponent.Illumination`.
- Diagonal movement (currently grid-only) — when "natural paths" reads weakly, add 8-direction motion.
- Long-term schedules ("at 9am Donna goes to the breakroom") — schedule-driven `MovementTargetComponent` updates. Likely part of cast-generator or its own behavior packet.
- Multi-agent path coordination (cooperative pathfinding) — when single-agent + step-aside reads as insufficient.
- Smooth turning (facing rotates over multiple ticks instead of snapping) — small visual-quality polish.
- Pathfinding around moving NPCs (treating other NPCs as soft obstacles with avoidance penalty) — current model treats them as pass-through with step-aside; future could add proper avoidance.
- Walls and doors as proper geometry (with thickness, swing direction, lockable state). Currently doorways are inferred from room-bounds adjacency.
- Per-NPC variable handedness based on personality (most people go right; a contrarian goes left).
