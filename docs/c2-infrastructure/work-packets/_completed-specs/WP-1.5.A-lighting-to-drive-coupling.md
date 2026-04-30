# WP-1.5.A — Lighting-to-Drive Coupling

**Tier:** Sonnet
**Depends on:** WP-1.2.A (lighting state on rooms — merged on `staging`), WP-1.4.A (social drive components — merged), WP-1.1.A (room membership — merged).
**Parallel-safe with:** WP-1.2.B (Spatial projector — different file footprint), WP-1.7.A (World bootstrap — different file footprint). Only shared files are `SimulationBootstrapper.cs` and `SimConfig.json`; conflicts there are sectional and auto-mergeable.
**Timebox:** 75 minutes
**Budget:** $0.30

---

## Goal

Wire the aesthetic bible's lighting → behavior mappings into the engine. The bible commits to specific drive nudges for each lighting condition NPCs experience:

- **Flickering fluorescent** → small `irritation` bump per minute exposed; sustained → `loneliness` rises slightly.
- **Warm desk lamp** → small `belonging` and `affection` nudges in close range.
- **Dark hallway / off-hours dim** → `suspicion` rises; NPCs walk faster (already wired via `MovementSpeedModifierSystem` from WP-1.3.A — this packet adds the lighting input to that modifier).
- **Server-room LED glow** → neutral, slightly disorienting (slight `irritation` increment, but Greg's archetype tolerates it — handled by per-archetype tolerance in a later packet, not here).
- **Sunlight from window** → mood lift, small drive recovery (decrement on `loneliness`, increment on `belonging`).
- **No lighting at all (basement corner with dead fluorescent)** → `belonging` decay, `loneliness` rise.

This packet ships **one new system**: `LightingToDriveCouplingSystem`. Per tick, for each NPC, it reads the NPC's current room's `Illumination` state plus the dominant light source's `State`, looks up the drive deltas for that combination from `SimConfig.lighting.driveCouplings`, and writes deltas through the existing drive-modification path.

What this packet does **not** do: per-archetype tolerance (Greg's server-room indifference, etc.) — that's a follow-up after the cast generator lands. Per-tile lighting (currently per-room) — also future. Lighting affects movement speed beyond the existing speed modifier — defer.

---

## Reference files

- `docs/c2-content/DRAFT-aesthetic-bible.md` §"Lighting → behavior mapping" — **read first**. The starting baselines this packet implements.
- `docs/c2-content/DRAFT-action-gating.md` — context on how drive deltas factor into action selection. This packet only writes drive deltas; downstream action effects come from existing systems.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.2.A.md` — confirms `RoomComponent.Illumination`, `LightSourceComponent.State`, sun state are queryable.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.4.A.md` — confirms `SocialDrivesComponent` exists with the eight drives in canonical order.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.1.A.md` — confirms `EntityRoomMembership` service exposes which entity is in which room.
- `APIFramework/Components/RoomComponent.cs`, `RoomIllumination`, `LightSourceComponent.cs`, `LightState.cs`, `LightKind.cs` — the lighting data this system reads.
- `APIFramework/Systems/Spatial/EntityRoomMembership.cs` — the room-membership service.
- `APIFramework/Components/SocialDrivesComponent.cs` — the drive component this system writes deltas to.
- `APIFramework/Core/SimulationBootstrapper.cs` — system registration site.
- `APIFramework/Core/SystemPhase.cs` — phase enum. Coupling runs after lighting (so illumination is current) and before drive dynamics (so deltas factor into the per-tick drift).
- `SimConfig.json` — runtime tuning lives here.
- `APIFramework.Tests/Systems/*.cs` — pattern reference.

## Non-goals

- Do **not** modify `Warden.Telemetry/*` or `Warden.Contracts/*`. No DTO changes; lighting-to-drive deltas are engine-internal.
- Do **not** modify any file under `docs/c2-infrastructure/schemas/`. No schema bump.
- Do **not** add per-archetype tolerance to the lighting mapping. The bible mentions "Greg has gotten used to it" — that's a per-NPC modifier the cast generator will populate. This packet ships only the global mapping.
- Do **not** add per-tile illumination. Per-room is what `RoomComponent.Illumination` exposes. Per-tile is a future enhancement.
- Do **not** modify `MovementSpeedModifierSystem`. The bible's "dark hallway → walk faster" effect manifests through `irritation` deltas (which already increase walking speed via the existing modifier). No new movement coupling needed.
- Do **not** add a new drive. The eight drives are stable per WP-1.0.A.1. All deltas write to existing drives.
- Do **not** apply deltas to NPCs without a room (e.g., NPCs outside building bounds). If `EntityRoomMembership` returns null for an NPC, that NPC gets no lighting delta this tick.
- Do **not** add per-frame allocations. Reuse buffers; the system runs every tick on every NPC.
- Do **not** introduce a NuGet dependency.
- Do **not** use `System.Random`. `SeededRandom` only (the system itself is deterministic — no RNG needed — but if any future addition needs noise, channel through `SeededRandom`).
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)

---

## Design notes

### The mapping table

A `LightingDriveCouplingTable` is loaded from `SimConfig.lighting.driveCouplings` at boot. The table is a list of entries. Each entry matches a *condition* (combination of room category + dominant light source state + ambient-level range) and produces a list of *per-tick drive deltas*. Pseudo-shape:

```jsonc
{
  "lighting": {
    "driveCouplings": [
      {
        "condition": {
          "roomCategoryAny":   ["cubicleGrid", "office", "hallway"],
          "dominantSourceState": "flickering"
        },
        "deltasPerTick": {
          "irritation": 0.08,
          "loneliness": 0.02
        }
      },
      {
        "condition": {
          "roomCategoryAny":   ["cubicleGrid", "office"],
          "dominantSourceKind": "deskLamp",
          "ambientLevelMin":   30
        },
        "deltasPerTick": {
          "belonging": 0.05,
          "affection": 0.04
        }
      },
      {
        "condition": {
          "roomCategoryAny":     ["hallway", "stairwell"],
          "ambientLevelMax":     20,
          "dayPhaseAny":          ["evening", "dusk", "night"]
        },
        "deltasPerTick": {
          "suspicion":  0.10,
          "irritation": 0.04
        }
      },
      {
        "condition": {
          "apertureBeamPresent": true
        },
        "deltasPerTick": {
          "loneliness": -0.05,
          "belonging":   0.03
        }
      },
      {
        "condition": {
          "ambientLevelMax":  5
        },
        "deltasPerTick": {
          "belonging":  -0.03,
          "loneliness":  0.06
        }
      }
    ]
  }
}
```

The Sonnet picks the exact number of entries and starting values; the above is a starter set drawn from the bible. Deltas are floating-point per-tick (so over a sim-minute of 60 ticks, an irritation delta of 0.08 produces ~5 points — meaningful but not sudden).

### Condition matching

Each entry's `condition` is an `AND` of all sub-clauses present. A clause not present is "any" (always matches). Clauses supported:

- `roomCategoryAny`: NPC's current room's category is one of the listed values.
- `dominantSourceState`: the room's `Illumination.DominantSourceId` resolves to a source whose `State` matches.
- `dominantSourceKind`: same but matches `Kind`.
- `ambientLevelMin` / `ambientLevelMax`: room's `Illumination.AmbientLevel` is within range.
- `dayPhaseAny`: current `SunStateService.DayPhase` is one of the listed values.
- `apertureBeamPresent`: at least one `LightApertureTag` entity in the same room is currently emitting a non-zero beam (read from the `ApertureBeamState` cache from WP-1.2.A).

The first matching entry wins — entries are evaluated in declaration order. If none match, no deltas this tick.

### Applying deltas

The system iterates NPC entities (`NpcTag`). For each:

1. Query `EntityRoomMembership` for the entity's current room. If null, skip.
2. Read the room's `RoomComponent.Illumination` and the dominant source's state/kind (resolve via `Illumination.DominantSourceId` if present).
3. Walk `LightingDriveCouplingTable.Entries` in order; first match wins.
4. For each drive delta in the matched entry, *increment* the NPC's `SocialDrivesComponent.<drive>.Current` by `delta` (clamped to 0–100 per drive after the increment).
5. The drive-dynamics system later in the tick handles decay/baseline pull; this system only writes deltas.

Floating-point accumulators: drive `Current` is an `int` per the v0.2.1 schema. Per-tick deltas are floating-point. Use a per-NPC, per-drive `float` accumulator service (`SocialDriveAccumulator`) that buffers fractional deltas across ticks and flushes integer increments when accumulators cross 1.0. This avoids losing fractional information from sub-1.0 per-tick deltas while keeping the wire format integer.

The accumulator is engine-internal; it does not appear on the wire. It's a `Dictionary<(int entityId, DriveKind drive), float>` reset never (persists across save/load — actually, since saves use `WorldStateDto` per axiom 8.2 and that doesn't carry accumulators, accumulators reset on load. Acceptable: a few sub-1.0 fractional points lost per save is invisible).

### Phase ordering

Slot `LightingToDriveCouplingSystem` after the lighting phase (illumination is fresh) and before `DriveDynamicsSystem` (so deltas are present before decay/circadian). In `SystemPhase` terms: between `Lighting` (existing from WP-1.2.A) and the social-engine systems from WP-1.4.A.

### Determinism

Deterministic: no RNG. Iteration over NPCs in entity-id ascending order. Two runs with the same seed produce byte-identical drive trajectories.

### What about per-archetype tolerance

The bible mentions "Greg has gotten used to it" for the server-room LED — Greg's `Hermit` archetype is tolerant of the disorienting glow. This packet does **not** implement archetype-based tolerance. The cast generator (WP-1.8.A) will populate per-NPC `LightingToleranceComponent` modifiers; a follow-up packet (WP-1.5.B, small) will read those modifiers and scale the deltas. For now: every NPC experiences the same delta in the same lighting condition.

### SimConfig additions

The full `lighting.driveCouplings` section per Design notes. Five entries minimum (covering the bible's five named conditions); Sonnet may add more for completeness as the bible suggests.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Systems/Coupling/LightingToDriveCouplingSystem.cs` | The new per-tick system. |
| code | `APIFramework/Systems/Coupling/LightingDriveCouplingTable.cs` | The table data structure. Loaded from SimConfig at boot. |
| code | `APIFramework/Systems/Coupling/CouplingCondition.cs` | The condition matcher record + matcher logic. |
| code | `APIFramework/Systems/Coupling/SocialDriveAccumulator.cs` | The fractional-delta accumulator service. Singleton. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register the table (loaded from SimConfig), the accumulator (singleton), the new system in correct phase order. |
| code | `SimConfig.json` (modified) | Add the `lighting.driveCouplings` section per Design notes. At least five entries covering the bible's named conditions. |
| code | `APIFramework.Tests/Systems/Coupling/LightingToDriveCouplingSystemTests.cs` | (1) Flickering source in a hallway produces irritation delta on NPC. (2) Warm desk lamp in office produces belonging + affection delta. (3) Dim hallway after-hours produces suspicion + irritation delta. (4) Sun beam present produces belonging + loneliness recovery. (5) Pitch-dark room produces belonging decay + loneliness rise. (6) NPC outside any room receives no delta. (7) Sub-1.0 deltas accumulate across ticks and produce integer drive increments at the right rate. (8) First-match-wins is honored when an NPC's situation matches multiple entries. |
| code | `APIFramework.Tests/Systems/Coupling/CouplingDeterminismTests.cs` | Two runs, same seed, 5000 ticks → byte-identical drive trajectories under varied lighting conditions. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.5.A.md` | Completion note. Standard template. Enumerate (a) which bible mappings are now wired, (b) which deltas are deferred (per-archetype tolerance, per-tile illumination). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `LightingToDriveCouplingSystem` runs without error against an empty world (no NPCs, no rooms). | unit-test |
| AT-02 | An NPC in a hallway with a flickering dominant light source receives an `irritation` increment per tick that, accumulated over 60 ticks, produces ≥ 4 points of integer increment. | unit-test |
| AT-03 | An NPC in an office with a warm desk lamp receives `belonging` + `affection` increments. | unit-test |
| AT-04 | An NPC in a dim hallway during evening/dusk/night receives `suspicion` + `irritation` increments. | unit-test |
| AT-05 | An NPC in a room with a sun-beam-present aperture receives a `loneliness` decrement and `belonging` increment. | unit-test |
| AT-06 | An NPC in a pitch-dark room receives `belonging` decay and `loneliness` rise. | unit-test |
| AT-07 | An NPC with no room membership (outside building bounds) receives no delta this tick. | unit-test |
| AT-08 | First-match-wins: when an NPC's situation matches both entry 0 and entry 2, only entry 0's deltas apply. | unit-test |
| AT-09 | Sub-1.0 per-tick deltas are accumulated; integer increments to `SocialDrivesComponent.X.Current` happen at the expected rate (delta-per-tick × tick-count rounded down). | unit-test |
| AT-10 | Drive `Current` clamps at 100 even when sustained increments would push higher. | unit-test |
| AT-11 | Drive `Current` clamps at 0 even when sustained decrements would push lower. | unit-test |
| AT-12 | `CouplingDeterminismTests` produce byte-identical drive trajectories across two runs over 5000 ticks. | unit-test |
| AT-13 | All existing `APIFramework.Tests` stay green (lighting, social, spatial, movement, narrative, physiology). | build + unit-test |
| AT-14 | `Warden.Telemetry.Tests` and `Warden.Contracts.Tests` all pass. | build + unit-test |
| AT-15 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-16 | `dotnet test ECSSimulation.sln` — every existing test stays green; new tests pass. | build |

---

## Followups (not in scope)

- WP-1.5.B (small): per-archetype lighting tolerance — Greg's `Hermit` tolerates server-room glow; sun-loving Climbers get extra mood lift from windows. Reads a `LightingToleranceComponent` populated by the cast generator.
- Per-tile illumination (deferred): a system queries "is this exact tile lit/shadowed" for fine-grained behavior. Currently per-room.
- Lighting affects movement-speed multiplier directly (not just through irritation): when "irritation → faster" reads as too indirect, add a direct dim-hallway speed modifier.
- Per-floor lighting variations: the basement is darker than the first floor; the top floor has natural light. Today these are mapping-table differences; could become floor-level constants.
- Light-source NPC interactions (an NPC turns on a desk lamp because their belonging is low). Action-selection territory; later.
