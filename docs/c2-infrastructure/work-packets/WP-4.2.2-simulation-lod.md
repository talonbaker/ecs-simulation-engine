# WP-4.2.2 — Simulation Level-of-Detail (per-zone tick fidelity)

> **Phase 4.2.x wave 1 (post-reorg) — performance-critical packet.** WP-4.2.0 + 4.2.1 give the project the structural ability to host multiple zones; this packet delivers the **performance work** that makes multi-zone scenes actually scale. Inactive-zone NPCs tick at coarser intervals — a 4× to 8× reduction depending on system — letting the existing 30-NPC FPS gate extend to 100+ NPCs across many zones with only ~30 visible at full fidelity. **This is the load-bearing perf packet for everything Phase 4.2.x ships afterward.**

> **DO NOT DISPATCH UNTIL WP-4.2.0 IS MERGED** — depends on `ZoneIdComponent` + `ZoneRegistry`. Can land in parallel with WP-4.2.1 (disjoint files; the LoD scheduler reads zone state without modifying it).

**Tier:** Sonnet
**Depends on:** WP-4.2.0 (zone substrate — required).
**Parallel-safe with:** WP-4.2.1 (active zone camera — disjoint), WP-4.2.3+ (scenarios — these become beneficiaries of LoD without modifying it).
**Timebox:** 180 minutes (largest packet in Phase 4.2.x; the design conversation is real).
**Budget:** $0.85
**Feel-verified-by-playtest:** YES (perf calibration is gameplay; if LoD makes inactive-zone NPCs feel stuck/dead the player notices on zone re-entry)
**Surfaces evaluated by next PT-NNN:** Does the active zone hold 60 FPS with 30 NPCs visible AND 70 NPCs running in 3 inactive zones? When the player switches to a previously-inactive zone, do the NPCs there feel alive (drives have evolved, they've moved positions) — or do they look frozen-in-time? Are there any unfair gameplay moments (an NPC died unwitnessed) that LoD created?

---

## Goal

Today, every system in the simulation runs every NPC every tick. Per the existing performance budget that's safe up to ~30 NPCs at 60 FPS (the existing gate). Adding zones (4.2.0) lets the project model 100+ NPCs across many physical areas, but if every NPC ticks at full fidelity that 30-NPC gate becomes the global ceiling — defeating the point of zones.

This packet adds **per-zone simulation level-of-detail** (LoD): inactive-zone NPCs tick at lower fidelity. Concretely:

| Tier | Tick rate | Systems run |
|:---|:---|:---|
| **Active (current zone)** | every tick (60 Hz) | All systems — drives, social, willpower, inhibitions, choking, fainting, rescue, chore, slip, lockout, schedule, dialog, animation, pathfinding, physics |
| **Adjacent (transition-connected to active)** | every 4th tick (15 Hz) | Most systems run, but per-tick costs are sampled; pathfinding cache hits dominate |
| **Distant (no transition path to active in N hops)** | every 16th tick (3.75 Hz) | Coarsened systems only — drives decay, schedule cursor advance, idle movement; high-fidelity systems (choking detection, rescue) skipped entirely OR run at full fidelity (configurable per-system, see Design notes) |

The classification is recomputed on every active-zone change. v0.1 ships only **Active vs Inactive** (two tiers) — adjacent-zone tier is a future extension if perf headroom requires it.

Concrete perf target: **100 NPCs distributed across 4 zones (one active with ~30 visible, three inactive with ~25 each) hold ≥ 60 FPS on the existing test rig.** That's a 3.3× capacity gain without compromising the visible-zone feel.

---

## The hard design conversation: which systems can safely coarsen?

This is the **load-bearing design question** for the entire packet. Each engine system in `APIFramework/Systems/` falls into one of three buckets:

### Bucket A: SAFE TO COARSEN — runs at low fidelity for inactive zones

Systems whose state evolves on long time scales relative to the simulation tick. Coarsening them by 16× has imperceptible gameplay effect because they were already integrating slow-changing state.

- **DriveDecaySystem** — drives decay over minutes; 3.75 Hz vs 60 Hz is invisible.
- **SocialDriveAccumulationSystem** — same.
- **ScheduleCursorSystem** — schedule blocks are minutes-long; cursor advance is tolerant of jitter.
- **IdleMovementSystem** — NPCs wandering; coarse positions update is fine.
- **ChoreEligibilityRefreshSystem** — re-evaluates which NPCs can take a chore; re-evaluation latency of ~4s when the player isn't watching is fine.
- **MoodAggregationSystem** — derives mood from drives; downstream of A-tier inputs anyway.
- **WillpowerRecoverySystem** — recovery is slow; coarse ticks fine.
- **MemoryDecaySystem** (per-pair / personal / chronicle thinning) — operates on day-scale; coarse fine.

### Bucket B: UNSAFE TO COARSEN — must run at full fidelity for ALL NPCs always

Systems with **fairness contracts** to the player: events the player should be able to intervene in. Coarsening these would create "unwitnessed death" gameplay moments that feel unfair.

- **ChokingDetectionSystem** — choking has a rescue-window contract; coarsening means the player might switch to the breakroom and find Donna already-deceased without any chance to intervene.
- **RescueDetectionSystem / RescueAttemptSystem** — rescue must be available immediately when the player switches to a zone with an in-progress emergency.
- **LifeStateTransitionSystem** — transitions to Deceased must happen at known cadence so the chronicle accurately records death time.
- **NarrativeEventEmitterSystem** — narrative events are the chronicle's source of truth; coarsening creates ordering ambiguity.

These systems run at full fidelity for every NPC regardless of zone. The cost is bounded — these systems are cheap per NPC (mostly checks + rare emissions); 100 NPCs at 60 Hz is well within budget.

### Bucket C: SOMETIMES COARSEN — design choice, configurable

Systems that *could* coarsen safely but where the design choice is non-obvious. v0.1 ships them at full fidelity (safe default); future tuning packets calibrate.

- **PathfindingTriggerSystem** — pathfinding is expensive; coarsening saves real CPU. But an NPC mid-path who skips ticks looks frozen on screen if you switch back to that zone. v0.1: full fidelity for active zone; **inactive zones skip pathfinding entirely** (NPCs in inactive zones don't move while you're not watching them, they continue at the moment the zone re-activates from where they were). This is intentional — feels like time-pause-while-you're-away rather than time-passes-while-you're-away. Tune per-zone via `ZoneSimulationConfig` if needed.
- **ConversationAdvanceSystem** — conversations evolve slowly; could coarsen to 15 Hz. v0.1: full fidelity (cheap; not a bottleneck).
- **AnimationStateAdvanceSystem** — visual; only matters for visible NPCs. v0.1: skip entirely for inactive-zone NPCs.
- **PhysicsTickSystem** — physics (thrown objects). Skip entirely for inactive zones. (No active player input → no thrown objects.)

### The "Donna died in the breakroom" question

Specifically: if the player is in the parking lot and an NPC in the breakroom chokes, what happens?

**v0.1 default (this packet):** Choking detection runs at full fidelity in all zones. The chokes happens; rescue detection runs; nearby NPCs (in the breakroom) attempt rescue per their archetype-driven biases (chore rotation, rescue mechanics, etc.). The player doesn't see it directly — but when they switch back to the breakroom, they see the outcome (Donna alive and shaken, or Donna deceased and a bereavement cascade in flight).

**Why this is the right default:** the office sim is a "you can't be everywhere" simulation. The player's emotional response to "while I was somewhere else, Donna almost died and Greg saved her" is the *exact* gameplay outcome we want — it's the office-sim equivalent of returning to your Sims to find someone burned the kitchen down. Coarsening choking detection to suppress these moments would *remove the soul* of the genre.

**The tradeoff:** full-fidelity choking in 100 NPCs at 60 Hz is more expensive than 30 NPCs at 60 Hz. The packet's perf test verifies this still hits the FPS gate. If it doesn't, the conversation re-opens — but it should hit it; choking detection is cheap (a per-NPC bolus check + a probability roll).

---

## Reference files

- `docs/PHASE-4.2-REORGANIZATION-2026-05-03.md` — design rationale.
- `docs/c2-infrastructure/work-packets/WP-4.2.0-zone-substrate.md` — the substrate this builds on.
- `APIFramework/Core/SimulationBootstrapper.cs` — read in full. Houses the per-tick system invocation order. The new `ZoneSimulationLodScheduler` wraps the existing system tick loop.
- `APIFramework/Core/SystemPhase.cs` — read for the existing system-phase enum + tick ordering.
- `APIFramework/Systems/Drives/DriveDecaySystem.cs` — read for an example of a Bucket A system.
- `APIFramework/Systems/LifeState/ChokingDetectionSystem.cs` — read for an example of a Bucket B system.
- `APIFramework/Systems/Movement/PathfindingTriggerSystem.cs` — read for an example of a Bucket C system.
- `APIFramework/Tests/Performance/` — the existing perf-gate test pattern; extend with the LoD variant.

---

## Non-goals

- Do **not** introduce per-NPC priority (e.g., "this VIP NPC always full-fidelity"). v0.1 LoD is per-zone only.
- Do **not** introduce three-tier (Active / Adjacent / Distant) classification. v0.1 ships two tiers (Active / Inactive) with all inactive zones treated the same. Three-tier is a future tuning packet if perf demands it.
- Do **not** introduce dynamic LoD (per-frame perf-budget-driven coarsening). v0.1 is static (config-driven).
- Do **not** modify Bucket B systems' implementations. They run as today, just on a wider set of NPCs.
- Do **not** introduce a per-tick "skip flag" on individual entities. The scheduler operates at the system level (skip the system call entirely for the inactive subset) rather than per-entity gating inside each system. This is faster (no hot-path branch in 60 systems) and simpler to reason about.
- Do **not** modify the existing system-phase enum or the bootstrapper's system order. The LoD scheduler wraps the existing tick loop; inside that loop, system order is unchanged.
- Do **not** ship a per-NPC "frozen" visual indicator. NPCs in inactive zones aren't visible (camera is on the active zone); no visual is needed.
- Do **not** modify `WorldStateDto` snapshot. The snapshot continues to capture every NPC's state regardless of LoD; inactive-zone snapshots just have less recent updates.
- Do **not** ship per-zone time-warp (different in-game time speeds per zone). v0.1 LoD is about update *frequency*, not in-game time.

---

## Design notes

### `ZoneSimulationLodScheduler`

The new scheduler wraps the existing tick loop. Each tick:

1. Compute the tick number `t`.
2. For each system, check its **LoD bucket**:
   - **Bucket A (CoarsenInInactive)**: invoke for active-zone entities every tick; invoke for inactive-zone entities only when `t % 16 == 0`.
   - **Bucket B (FullFidelityAlways)**: invoke for all entities every tick (current behavior, no change).
   - **Bucket C (SkipInInactive)**: invoke for active-zone entities every tick; **don't invoke for inactive-zone entities at all**.
3. For systems that take an entity collection, the scheduler partitions by zone and invokes the system twice (once with the active subset, once with the inactive subset, conditional on tick).

Implementation strategy: each system declares its LoD bucket via a marker attribute or interface:

```csharp
[ZoneLod(ZoneLodMode.CoarsenInInactive)]
public sealed class DriveDecaySystem : ISystem { ... }

[ZoneLod(ZoneLodMode.FullFidelityAlways)]
public sealed class ChokingDetectionSystem : ISystem { ... }

[ZoneLod(ZoneLodMode.SkipInInactive)]
public sealed class PathfindingTriggerSystem : ISystem { ... }
```

Systems without the attribute default to **`FullFidelityAlways`** (safe default — adding LoD to a new system requires explicit opt-in).

The scheduler runs as a wrapper around the existing `SimulationBootstrapper`'s tick. It does NOT replace the per-system `Update(...)` calls; it just gates which entities each system sees.

### How the scheduler partitions entities

For each tick:

1. Read `ZoneRegistry.ActiveZoneId`.
2. Walk all entities with `ZoneIdComponent`; partition into `_activeBucket` and `_inactiveBucket`.
3. Pass the appropriate bucket to each system based on its LoD attribute.

Cost of partitioning: O(N) per tick. With 100 NPCs that's negligible. To avoid per-tick allocation, partitioning uses pre-allocated lists cleared and refilled each tick.

### Per-system entity injection

Existing systems take `EntityManager` and query internally (e.g., `em.Query<DriveComponent>()`). The LoD scheduler can't easily intercept those queries.

**v0.1 strategy: introduce `IZoneAwareSystem` (optional interface).** Systems that opt-in implement it; the scheduler passes them a zone filter:

```csharp
public interface IZoneAwareSystem : ISystem
{
    void TickForZoneFilter(ZoneFilter filter);
}

public readonly struct ZoneFilter
{
    public bool IncludeActive   { get; init; }
    public bool IncludeInactive { get; init; }
    public string ActiveZoneId  { get; init; }
    // Helper:
    public bool MatchesEntity(Entity e) {
        if (!e.Has<ZoneIdComponent>()) return IncludeActive;  // un-tagged → treat as active
        var z = e.Get<ZoneIdComponent>().ZoneId;
        bool isActive = z == ActiveZoneId;
        return isActive ? IncludeActive : IncludeInactive;
    }
}
```

Systems that DON'T implement `IZoneAwareSystem` continue to run as today (every entity, every tick). The LoD wrapper applies only to systems that opted in.

This keeps the migration tractable: convert systems one at a time, prioritizing the expensive ones (pathfinding, animation, physics) for `SkipInInactive`. The cheap ones can stay full-fidelity even when "in" Bucket A — coarsening them isn't worth the conversion effort.

**Initial conversions in this packet (must-have):**
- `PathfindingTriggerSystem` → `SkipInInactive` (biggest cost saving)
- `AnimationStateAdvanceSystem` → `SkipInInactive` (visual-only)
- `PhysicsTickSystem` → `SkipInInactive`
- `DriveDecaySystem` → `CoarsenInInactive`
- `IdleMovementSystem` → `SkipInInactive`

**Deferred conversions (future tuning packets):**
- Schedule, social drives, willpower — adopt CoarsenInInactive when calibration shows the perf gain matters.

### `ZoneSimulationConfig`

A new section in `SimConfig`:

```jsonc
"zoneSimulation": {
  "enabled": true,
  "inactiveCoarsenInterval": 16,         // ticks between coarsened-system invocations
  "activeZoneFilter": {
    "includeUntagged": true              // for tests + transitional code
  }
}
```

Modder-tunable. Default `enabled: true` once the system ships — but if a modder ships a zone-heavy mod where LoD assumptions break, they can disable it.

### Performance verification

Three new tests (extending the existing perf-gate pattern):

1. `PerformanceGate30NpcSingleZoneLodTests` — 30 NPCs in one zone with LoD enabled. Must hit the existing FPS gate (regression check; LoD shouldn't slow down single-zone setups).
2. `PerformanceGate100NpcFourZonesLodTests` — 100 NPCs across 4 zones, one active with 30. Must hit the FPS gate. **This is the headline perf test.**
3. `PerformanceGate60NpcTwoZonesLodTests` — 60 NPCs across 2 zones (30 each). Must hit the FPS gate. (Intermediate scaling.)

If test 2 fails: the LoD bucket assignments are wrong (Bucket B too greedy), or the scheduler overhead is too high. Iterate.

### "Time stops while you're away" feel test

A behavioral test (not a perf test) — `ZoneTimePauseSemanticsTests`:

1. Spawn an NPC in a non-active zone with `IdleMovementSystem` skip-in-inactive.
2. Tick 100 times.
3. Assert: NPC's position is unchanged (system was skipped).
4. Switch active zone to that zone.
5. Tick 100 more times.
6. Assert: NPC's position has changed (system now runs).

This codifies the "time pauses for inactive-zone NPCs that depend on `SkipInInactive` systems." The semantic IS designed — see the design conversation above.

### Save / load

LoD is a runtime concern. Saved state captures every entity's full state regardless of LoD; loading restores everything. No save/load changes.

### Backwards compatibility

Single-zone setups: `ZoneRegistry.ActiveZoneId == "default-zone"`, every entity's `ZoneIdComponent.ZoneId == "default-zone"`, every entity matches `IncludeActive`. LoD is effectively a no-op. Behavior is identical to today.

Tests without zone tagging: entities default to active (no `ZoneIdComponent`); systems without `IZoneAwareSystem` run as today. No test regressions expected.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Systems/Lod/IZoneAwareSystem.cs` (new) | Marker interface + `ZoneFilter` struct. |
| code | `APIFramework/Systems/Lod/ZoneLodAttribute.cs` (new) | `[ZoneLod(ZoneLodMode.X)]` attribute + `ZoneLodMode` enum. |
| code | `APIFramework/Systems/Lod/ZoneSimulationLodScheduler.cs` (new) | Per-tick partition + scheduling logic. |
| code | `APIFramework/Config/SimConfig.cs` (modification) | Add `ZoneSimulationConfig` section. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modification) | Register scheduler; route per-tick calls through it when `enabled: true`. |
| code | `APIFramework/Systems/Movement/PathfindingTriggerSystem.cs` (modification) | Implement `IZoneAwareSystem`; `[ZoneLod(SkipInInactive)]`. |
| code | `APIFramework/Systems/Animation/AnimationStateAdvanceSystem.cs` (modification — locate exact filename) | Implement `IZoneAwareSystem`; `[ZoneLod(SkipInInactive)]`. |
| code | `APIFramework/Systems/Physics/PhysicsTickSystem.cs` (modification — locate exact filename) | `[ZoneLod(SkipInInactive)]`. |
| code | `APIFramework/Systems/Drives/DriveDecaySystem.cs` (modification) | Implement `IZoneAwareSystem`; `[ZoneLod(CoarsenInInactive)]`. |
| code | `APIFramework/Systems/Movement/IdleMovementSystem.cs` (modification) | `[ZoneLod(SkipInInactive)]`. |
| test | `APIFramework.Tests/Systems/Lod/ZoneSimulationLodSchedulerTests.cs` (new) | Per-bucket invocation correctness. |
| test | `APIFramework.Tests/Systems/Lod/ZoneFilterTests.cs` (new) | Filter matches correct entities. |
| test | `APIFramework.Tests/Systems/Lod/ZoneTimePauseSemanticsTests.cs` (new) | Skip-in-inactive systems pause; tick rate resumes on zone switch. |
| test | `APIFramework.Tests/Systems/Lod/CoarsenIntervalTests.cs` (new) | CoarsenInInactive systems run on schedule (every 16th tick). |
| test | `APIFramework.Tests/Performance/PerformanceGate30NpcSingleZoneLodTests.cs` (new) | LoD doesn't slow single-zone setups. |
| test | `APIFramework.Tests/Performance/PerformanceGate60NpcTwoZonesLodTests.cs` (new) | Intermediate scaling. |
| test | `APIFramework.Tests/Performance/PerformanceGate100NpcFourZonesLodTests.cs` (new) | Headline perf gate — 100 NPCs across 4 zones at 60 FPS. |
| test | `APIFramework.Tests/Systems/Lod/ChokingFullFidelityAcrossZonesTests.cs` (new) | Bucket B verification: choking detection runs in inactive zones. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Add MAC-019 (Zone simulation LoD as Mod API surface — modders' systems can opt into LoD). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | A system attributed `[ZoneLod(SkipInInactive)]` is NOT invoked for inactive-zone entities. | unit-test |
| AT-02 | A system attributed `[ZoneLod(CoarsenInInactive)]` is invoked for inactive-zone entities exactly once per `inactiveCoarsenInterval` ticks. | unit-test |
| AT-03 | A system attributed `[ZoneLod(FullFidelityAlways)]` (or no attribute) is invoked for ALL entities every tick. | unit-test |
| AT-04 | `ChokingDetectionSystem` (Bucket B) detects a choke in an inactive zone within 5 ticks of onset. | unit-test |
| AT-05 | `IdleMovementSystem` (SkipInInactive) does NOT update position for inactive-zone NPCs across 100 ticks; updates resume on zone switch. | unit-test |
| AT-06 | `PathfindingTriggerSystem` (SkipInInactive) does NOT issue path queries for inactive-zone NPCs. | unit-test |
| AT-07 | `DriveDecaySystem` (CoarsenInInactive) decays drives across the inactive period (lower granularity, but cumulative effect matches). | unit-test |
| AT-08 | LoD disabled in config → all systems run for all entities every tick (regression-safe escape). | integration-test |
| AT-09 | 30 NPCs single zone LoD enabled → ≥ 60 FPS (regression). | perf-test |
| AT-10 | 60 NPCs two zones (30/30) → ≥ 60 FPS. | perf-test |
| AT-11 | **100 NPCs across 4 zones (one active w/ 30, three inactive w/ ~25 each) → ≥ 60 FPS.** Headline gate. | perf-test |
| AT-12 | Bucket-B systems (choking, rescue, life-state, narrative emit) all run for every NPC every tick regardless of zone. | integration-test (audit) |
| AT-13 | All Phase 0–3 + Phase 4.0.A–L + WP-4.2.0/4.2.1 tests stay green (LoD doesn't break existing semantics). | regression |
| AT-14 | `dotnet build` warning count = 0; all tests green. | build + test |
| AT-15 | MAC-019 added to `MOD-API-CANDIDATES.md`. | review |

---

## Mod API surface

This packet introduces **MAC-019: Zone simulation LoD attribute system**. Append to `MOD-API-CANDIDATES.md`:

> **MAC-019: Zone simulation LoD (per-system tick fidelity opt-in)**
> - **What:** A modder authoring a custom system declares its LoD behavior with `[ZoneLod(ZoneLodMode.X)]` (default: `FullFidelityAlways`, the safe option). The `ZoneSimulationLodScheduler` then applies the right gating — `SkipInInactive` (system never runs for inactive-zone NPCs) or `CoarsenInInactive` (runs every Nth tick). Modders adding heavy expensive systems (a complex AI planner; a custom physics layer) can opt their system into LoD and immediately benefit from per-zone perf scaling.
> - **Where:** `APIFramework/Systems/Lod/IZoneAwareSystem.cs`, `ZoneLodAttribute.cs`, `ZoneSimulationLodScheduler.cs`; `APIFramework/Config/SimConfig.cs#ZoneSimulationConfig`.
> - **Why a candidate:** Performance is the project's #1 priority (per Talon's wave-4-reorg framing). The LoD substrate is the canonical perf seam mods can plug into. Pattern is consistent with MAC-005 (SimConfig sections) for the config side.
> - **Stability:** fresh (lands with WP-4.2.2; second consumer = scenario systems in 4.2.3+).
> - **Source packet:** WP-4.2.2.

---

## Followups (not in scope)

- Three-tier classification (Active / Adjacent / Distant) if perf headroom needs more granularity.
- Dynamic LoD (per-frame budget-driven coarsening). Useful if perf varies by scene complexity.
- Per-NPC fidelity overrides (a "VIP" tag that always runs full-fidelity even in inactive zones). For specific narrative scenarios.
- Per-zone time-warp (different in-game time speeds per zone). Probably bad UX but worth keeping the option open.
- Profiling dashboard (which systems are spending the most time). Useful for tuning future LoD additions.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT required for unit-tests; perf-test results ARE the verification

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm worktree at `.claude/worktrees/sonnet-wp-4.2.2/` on branch `sonnet-wp-4.2.2` based on recent `origin/staging` (which now includes WP-4.2.0, may include WP-4.2.1).
1. Implement the spec.
2. Run `dotnet test`. All must stay green.
3. **Confirm AT-11 passes.** This is the load-bearing perf test. If it fails, the LoD bucket assignments need iteration (likely too many systems are FullFidelityAlways; convert one more in the SkipInInactive bucket and re-test).
4. Stage all changes.
5. Commit + push.
6. Notify Talon: `READY FOR REVIEW — perf gate AT-11 passes at <FPS> FPS for 100 NPCs across 4 zones; LoD scheduler operational.`

### Cost envelope

Target: **$0.85**. Largest 4.2.x packet — the design conversation has weight, the perf test setup is involved, the system-by-system migration takes time. If cost approaches $1.40, escalate via `WP-4.2.2-blocker.md`.

Cost-discipline:
- Migrate only the listed must-have systems (5 systems). Don't try to migrate everything.
- Don't re-architect the bootstrapper's tick loop; wrap it.
- The perf tests are the gate; the unit tests are the safety net. Don't over-engineer the unit tests.

### Self-cleanup on merge

Standard. Check for `WP-4.2.3+` as dependents (scenarios benefit from LoD).
