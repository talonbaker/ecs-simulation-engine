# WP-2.2.A — Schedule Layer

**Tier:** Sonnet
**Depends on:** WP-2.1.A (action-selection scaffold), WP-1.8.A (cast generator + `NpcArchetypeComponent`), WP-1.7.A (named anchors)
**Parallel-safe with:** WP-2.0.C (different project), WP-2.3.A (different system surface), WP-2.4.A (different system surface)
**Timebox:** 90 minutes
**Budget:** $0.40

---

## Goal

Give every NPC a daily routine. Without a schedule layer, NPCs only act when drives push them — and most drives sit below the action threshold most of the day, so NPCs default to `Idle`. The world looks frozen. The schedule layer attaches a per-archetype routine to each NPC at spawn, and the routine produces low-priority Approach intents that ActionSelectionSystem treats as one more candidate among the drive-driven ones. When drives are quiet, NPCs follow the schedule; when a drive spikes, ActionSelectionSystem's existing scoring overrides the schedule on the same tick.

The schedule layer is what turns "Donna's drives drift around 50" into "Donna goes to her desk at 8am, drifts to the breakroom at 10:30, eats lunch outside at noon" — and what makes a drive spike *interesting*, because now it's a deviation from the routine the player has already learned.

This is the second of three Wave 2 systems that build directly on WP-2.1.A's seam. Memory recording (WP-2.3.A) and stress (WP-2.4.A) are the others; each ships independently and parallel-safe with this one.

---

## Reference files

- `docs/c2-content/world-bible.md` — gameplay loop ("orders come in, nobody told you"); named anchors; persistence threshold. Schedules pull NPCs *toward* anchors; the work loop pulls them *away* via drive pressure.
- `docs/c2-content/cast-bible.md` — archetype catalog. Each archetype carries a "schedule shape" hint in prose ("The Climber goes office-to-office; The Hermit stays in the IT closet"). This packet codifies those hints into structured data.
- `docs/c2-content/action-gating.md` — the action-selection thesis. Schedule candidates are *low-weight* — they win when drives are quiet, lose when drives are loud. That's the design intent for every system that produces IntendedAction candidates.
- `docs/c2-infrastructure/00-SRD.md` §8.1 (no runtime LLM), §8.5 (social state is first-class).
- `docs/c2-infrastructure/work-packets/_completed/WP-2.1.A.md` — **read first.** The shipped surfaces are the contract: `IntendedActionComponent` shape, `ActionSelectionSystem` candidate enumeration, the `Cognition = 30` phase. Schedule writes upstream of Cognition.
- `APIFramework/Components/IntendedActionComponent.cs` — the shape ActionSelection writes. Schedule does NOT write this directly; it produces inputs to ActionSelection.
- `APIFramework/Systems/ActionSelectionSystem.cs` — the consumer. Schedule writes a new `ScheduledHintComponent` that ActionSelection reads as one extra candidate enumeration source. Add the candidate to the existing enumeration loop; do not redesign the system.
- `APIFramework/Components/CastSpawnComponents.cs` — `NpcArchetypeComponent.ArchetypeId` is the link to the archetype catalog; schedule layer reads this at spawn to pick the right routine.
- `APIFramework/Components/NamedAnchorComponent.cs` — anchors are the destinations a schedule references. The schedule names them by string id (matching `NamedAnchorComponent.AnchorId`); ScheduleSystem resolves the name to the entity each tick.
- `APIFramework/Components/PositionComponent.cs` — anchor entities have positions; ActionSelection's Approach candidate uses the anchor entity's `Guid` as `MovementTargetComponent.TargetEntityId`.
- `APIFramework/Core/SimulationClock.cs` — `GameHour` (float, 0..24) is the schedule's clock input. World starts at 6:00 AM. Schedule blocks are time-of-day intervals on this clock.
- `APIFramework/Core/SystemPhase.cs` — `Cognition = 30` exists. Schedule runs at `Condition = 20` so its hints are visible before ActionSelection enumerates.
- `APIFramework/Core/SimulationBootstrapper.cs` — system registration site. Two new systems register here.
- `APIFramework/Core/SeededRandom.cs` — for any tie-breaking. No `System.Random` ever.
- `docs/c2-content/archetypes/archetypes.json` — the existing catalog. Schedule data is a *separate* file (per Design notes) so this packet doesn't touch the existing catalog.
- `SimConfig.json` — runtime tuning lives here, not in code.
- `docs/c2-infrastructure/SCHEMA-ROADMAP.md` — confirm there's no v0.5+ promise that this packet inadvertently jumps. Schedule data is content, not a wire format; no schema bump.

## Non-goals

- Do **not** write to `IntendedActionComponent` from `ScheduleSystem`. ActionSelectionSystem is the single writer of intents — schedule is one of its enumeration *sources*, not a peer producer. Multiple writers of the same component break determinism guarantees.
- Do **not** modify the existing cast generator (`CastGenerator.cs` from WP-1.8.A). The schedule layer is bolt-on: `ScheduleSpawnerSystem` runs once at simulation boot to attach schedules to any NPC that lacks one. This keeps WP-1.8.A's cast-generator surface unchanged.
- Do **not** add weekday or seasonal awareness. The world bible flags weekly/seasonal cadence as an open question; until Talon answers it, every day's schedule is identical. `DayNumber` is available on the clock; this packet ignores it.
- Do **not** modify `archetypes.json`. The schedule data lives in a separate file (`docs/c2-content/schedules/archetype-schedules.json`) so this packet's data footprint is isolated. A future packet may merge them; not v0.1.
- Do **not** introduce per-NPC schedule deviation, override commands, or player-controlled scheduling. Each NPC follows its archetype's default routine; deviation is what drives produce. Player-driven schedule editing is a Phase 3+ concern.
- Do **not** reduce ActionSelectionSystem's candidate enumeration. Add one new candidate source ("scheduled hint"); do not reweight or remove existing candidates.
- Do **not** add `ActivityKind` semantics that the engine doesn't yet enforce (e.g., a `Meeting` activity that locks two NPCs together). At v0.1, `ActivityKind` is a label that the schedule emits; no system reads it for behaviour. Future packets can add behaviour around specific kinds.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (SRD §8.1.)
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing. Use `SeededRandom`, `SimulationClock` advanced manually.

---

## Design notes

### Two new components

```csharp
public enum ScheduleActivityKind
{
    AtDesk,        // sit at assigned cubicle/office
    Break,         // breakroom / smoking bench / window
    Meeting,       // conference room
    Lunch,         // outside / breakroom
    Outside,       // parking lot / smoking bench
    Roaming,       // wander between floors
    Sleeping       // off-shift
}

/// <summary>
/// One block in an NPC's daily routine: between StartHour and EndHour, the NPC
/// prefers being at the named anchor performing the named activity. Hours are
/// floats on SimulationClock.GameHour (0..24). Blocks within a schedule must not
/// overlap; an NPC's schedule covers the full 24h day with explicit blocks (the
/// "Sleeping" or "Outside" blocks fill the off-hours).
/// </summary>
public readonly record struct ScheduleBlock(
    float StartHour,
    float EndHour,
    string AnchorId,
    ScheduleActivityKind Activity);

/// <summary>Per-NPC routine. Lives on the NPC; populated at spawn.</summary>
public struct ScheduleComponent
{
    public IReadOnlyList<ScheduleBlock> Blocks;
}

/// <summary>
/// Updated each tick by ScheduleSystem from ScheduleComponent + SimulationClock.
/// Read by ActionSelectionSystem during candidate enumeration.
/// </summary>
public struct CurrentScheduleBlockComponent
{
    public int                      ActiveBlockIndex;   // -1 if no active block
    public Guid                     AnchorEntityId;     // Guid.Empty if none
    public ScheduleActivityKind     Activity;
}
```

`ScheduleComponent` is set once at spawn and never mutated. `CurrentScheduleBlockComponent` is overwritten each tick (small, cheap).

### Two new systems

**`ScheduleSpawnerSystem`** runs at simulation boot (or each tick checking for NPCs without `ScheduleComponent`; spawn-time injection is cleaner if reliably triggerable). For each NPC with `NpcArchetypeComponent` and no `ScheduleComponent`, it reads `archetype-schedules.json`, finds the matching `archetypeId`, and attaches a `ScheduleComponent` populated from the file's blocks. Also adds an empty `CurrentScheduleBlockComponent` so other systems can read it without null-checking.

**`ScheduleSystem`** runs at `Condition = 20` phase (before `Cognition = 30`). For each NPC with both components:

1. Read `SimulationClock.GameHour`.
2. Find the block where `StartHour ≤ GameHour < EndHour`. Use a small per-NPC cached binary search if profiling shows it matters; v0.1 can iterate the (typically 4–6) blocks linearly.
3. If the active block changed since last tick, resolve `block.AnchorId` to a `Guid` by querying entities with `NamedAnchorComponent.AnchorId == block.AnchorId`. Cache that `Guid` in the component until the block changes again.
4. Write `CurrentScheduleBlockComponent { ActiveBlockIndex = i, AnchorEntityId = anchorGuid, Activity = block.Activity }`.

If no block matches (gap in the schedule), write `ActiveBlockIndex = -1, AnchorEntityId = Guid.Empty`. Schedules should cover the full 24h, but be defensive.

### Modification to ActionSelectionSystem

ActionSelectionSystem's existing candidate enumeration adds **one** new branch: when an NPC has `CurrentScheduleBlockComponent.AnchorEntityId != Guid.Empty`, enumerate one extra `Approach` candidate toward the anchor with weight `scheduleAnchorBaseWeight` (default 0.30). The candidate competes on the existing scoring axis; drive-driven candidates can outrank it.

Concretely, the addition is:

```csharp
// inside ActionSelectionSystem.Update, after drive-driven enumeration:
if (npc.Has<CurrentScheduleBlockComponent>())
{
    var sched = npc.Get<CurrentScheduleBlockComponent>();
    if (sched.AnchorEntityId != Guid.Empty)
    {
        candidates.Add(new Candidate {
            Kind = IntendedActionKind.Approach,
            TargetEntityId = WillpowerSystem.EntityIntId(<resolved anchor entity>),
            Weight = config.ScheduleAnchorBaseWeight,
            Source = CandidateSource.Schedule,
            Context = DialogContextValue.None
        });
    }
}
```

The `CandidateSource` enum is a new field on `Candidate` for diagnostics — distinguishes Drive / Schedule / Idle origins. (If `Candidate` is currently un-tagged with source, this packet adds the tag; debug telemetry benefits.)

When the schedule's `Activity` is `AtDesk`, `Sleeping`, or anything where the NPC is meant to *stay* at the anchor rather than journey, emit a `Linger` candidate instead of `Approach`. Specifically:

| Activity | Candidate kind |
|:---|:---|
| AtDesk | Linger (when at anchor) / Approach (when far) |
| Break, Meeting, Lunch, Outside, Roaming | Approach |
| Sleeping | Linger |

"At anchor" is `distance_to_anchor < 2 cells` in the spatial index. The Sonnet picks a sensible threshold; 2 cells is a starting point.

### Schedule data file

`docs/c2-content/schedules/archetype-schedules.json`:

```jsonc
{
  "schemaVersion": "0.1.0",
  "archetypeSchedules": [
    {
      "archetypeId": "the-vent",
      "blocks": [
        {"startHour":  6.0, "endHour":  8.0, "anchorId": "parking-lot",   "activity": "outside"},
        {"startHour":  8.0, "endHour": 10.5, "anchorId": "vent-desk",     "activity": "atDesk"},
        {"startHour": 10.5, "endHour": 10.75,"anchorId": "the-microwave", "activity": "break"},
        {"startHour": 10.75,"endHour": 12.0, "anchorId": "vent-desk",     "activity": "atDesk"},
        {"startHour": 12.0, "endHour": 12.75,"anchorId": "the-window",    "activity": "lunch"},
        {"startHour": 12.75,"endHour": 17.0, "anchorId": "vent-desk",     "activity": "atDesk"},
        {"startHour": 17.0, "endHour":  6.0, "anchorId": "parking-lot",   "activity": "sleeping"}
      ]
    },
    // ... 9 more archetypes
  ]
}
```

The `endHour < startHour` case (the trailing `17.0 → 6.0` "sleeping" block) handles the off-shift wrap. ScheduleSystem treats it as `endHour < startHour` → block matches when `GameHour >= startHour OR GameHour < endHour`.

The Sonnet authors blocks for all 10 archetypes from the cast bible:

- `the-vent` — chatty admin; cycles desk → breakroom → window
- `the-hermit` — IT closet most of the day, parking lot for shift bookends
- `the-climber` — desk + conference room rotation; walks executive corridors
- `the-cynic` — desk-bound; smoking bench breaks
- `the-newbie` — desk; copies the local pattern (use the-vent's shape)
- `the-old-hand` — desk + breakroom + window; predictable rhythm
- `the-affair` — desk + supply closet (the affair's anchor); walks the long way
- `the-recovering` — desk; bathroom and outside breaks; isolates at lunch
- `the-founders-nephew` — top-floor office; long lunches
- `the-crush` — same shape as the local archetype role; lingers near the crush target's anchor

The Sonnet picks reasonable hour blocks (5–7 per archetype); exact values are tunable later. Anchor IDs must match real entities in `office-starter.json` — the Sonnet verifies this by reading that file (a few KB).

### SimConfig additions

```jsonc
{
  "schedule": {
    "scheduleAnchorBaseWeight":     0.30,   // weight assigned to schedule-driven candidates
    "scheduleLingerThresholdCells": 2.0     // distance below which the candidate becomes Linger instead of Approach
  }
}
```

### Determinism

`ScheduleSystem` is deterministic by construction (reads clock + components, writes one component, no RNG). `ScheduleSpawnerSystem` reads a JSON file in a deterministic order and processes NPCs in `EntityIntId` ascending order. ActionSelectionSystem's existing determinism (5000-tick AT-10 from WP-2.1.A) must remain intact — the new candidate doesn't introduce non-determinism because its weight and selection are deterministic.

### Tests

- `ScheduleComponentTests.cs` — construction; block invariants (no overlap allowed at construction); 24h coverage check (the spawner enforces it; component itself just stores blocks).
- `ScheduleSystemTests.cs` — given a 4-block schedule and an advancing clock, the system writes the correct `CurrentScheduleBlockComponent` at each hour boundary. End-of-day wrap (`endHour < startHour`) handled correctly.
- `ScheduleSpawnerSystemTests.cs` — given an NPC with `NpcArchetypeComponent("the-vent")` and a schedule file containing that archetype, the spawner attaches a `ScheduleComponent` with the correct block count.
- `ActionSelectionScheduleIntegrationTests.cs` — integration: an NPC with a schedule pointing at an anchor and *quiet* drives produces an `Approach(anchorEntity)` intent. Same NPC with `Irritation = 80` produces a `Dialog(LashOut)` intent (drive overrides schedule). This is the shape-defining test.
- `ScheduleDeterminismTests.cs` — 5000 ticks, two seeds with the same world: byte-identical intent stream. (Same shape as WP-2.1.A's AT-10.)
- `ArchetypeSchedulesJsonValidationTests.cs` — load `archetype-schedules.json`; assert all 10 archetypes present, all blocks have valid anchor IDs that resolve in `office-starter.json`, every archetype's blocks cover 24h with no gaps and no overlaps.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/ScheduleComponent.cs` | `ScheduleBlock` record, `ScheduleActivityKind` enum, `ScheduleComponent` struct, `CurrentScheduleBlockComponent` struct. |
| code | `APIFramework/Systems/ScheduleSystem.cs` | Per-tick block-resolver; updates `CurrentScheduleBlockComponent`. Runs at `Condition = 20`. |
| code | `APIFramework/Systems/ScheduleSpawnerSystem.cs` | Reads `archetype-schedules.json` once at boot (cached); attaches `ScheduleComponent` + empty `CurrentScheduleBlockComponent` to NPCs lacking one. Runs at boot phase or first tick. |
| code | `APIFramework/Systems/ActionSelectionSystem.cs` (modified) | Add the schedule-candidate enumeration source per Design notes. Existing candidates and scoring untouched. Add `CandidateSource` enum if not already present. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `ScheduleSpawnerSystem` (boot or first-tick) and `ScheduleSystem` (Condition phase). |
| code | `APIFramework/Config/SimConfig.cs` (modified) | Add `ScheduleConfig` class + property with the two new keys. |
| code | `SimConfig.json` (modified) | Add `schedule` section per Design notes. |
| data | `docs/c2-content/schedules/archetype-schedules.json` | All 10 archetypes from the cast bible with 4–7 blocks each. Anchor IDs verified against `office-starter.json`. |
| code | `APIFramework.Tests/Components/ScheduleComponentTests.cs` | Construction, invariants. |
| code | `APIFramework.Tests/Systems/ScheduleSystemTests.cs` | Block-resolver + end-of-day wrap. |
| code | `APIFramework.Tests/Systems/ScheduleSpawnerSystemTests.cs` | Spawner attaches schedule from archetype id. |
| code | `APIFramework.Tests/Integration/ActionSelectionScheduleIntegrationTests.cs` | Schedule wins under quiet drives; drive wins under spike. |
| code | `APIFramework.Tests/Systems/ScheduleDeterminismTests.cs` | 5000-tick byte-identical intent stream. |
| code | `APIFramework.Tests/Data/ArchetypeSchedulesJsonValidationTests.cs` | JSON loads; all archetypes present; coverage + anchor validity. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-2.2.A.md` | Completion note. Standard template. Confirm SimConfig defaults that survived; confirm the 10 archetypes in the JSON; note any anchor-id discoveries (e.g., if an archetype's bible-described anchor doesn't exist in `office-starter.json`, document the substitution). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `ScheduleComponent` and `CurrentScheduleBlockComponent` compile, instantiate, round-trip equality. `ScheduleBlock` record validates 0 ≤ StartHour, EndHour ≤ 24. | unit-test |
| AT-02 | `ScheduleSystem` given a 4-block schedule and a clock advanced through each block produces the correct `ActiveBlockIndex` at each hour boundary. | unit-test |
| AT-03 | `ScheduleSystem` handles end-of-day wrap (`endHour < startHour`): a Sleeping block from 22.0 to 6.0 is active at 23.0, 0.5, and 5.5; not active at 7.0. | unit-test |
| AT-04 | `ScheduleSpawnerSystem` attaches a `ScheduleComponent` matching the archetype's blocks to an NPC with `NpcArchetypeComponent("the-vent")`. | unit-test |
| AT-05 | `ScheduleSpawnerSystem` is idempotent: running it twice produces the same `ScheduleComponent` and does not duplicate blocks. | unit-test |
| AT-06 | Integration: NPC with quiet drives + schedule pointing at the breakroom anchor produces `IntendedAction(Approach, breakroomEntityIntId)`. | integration-test |
| AT-07 | Integration: same NPC with `Irritation = 80` and a coworker in proximity produces `IntendedAction(Dialog, LashOut)` instead — drive overrides schedule. | integration-test |
| AT-08 | Integration: NPC at the breakroom anchor (within `scheduleLingerThresholdCells`) with the breakroom as its current schedule anchor produces `IntendedAction(Linger)` rather than `Approach`. | integration-test |
| AT-09 | Determinism: 5000-tick run, two seeds with the same world definition: byte-identical intent stream. | unit-test |
| AT-10 | `archetype-schedules.json` loads cleanly, contains all 10 archetypes from the cast bible, every block's `anchorId` resolves to a real entity in `office-starter.json`, every archetype's blocks cover 24h with no gaps or overlaps. | unit-test |
| AT-11 | All WP-2.1.A acceptance tests stay green — the schedule modification to ActionSelectionSystem is additive only. | regression |
| AT-12 | All WP-1.x tests stay green. | regression |
| AT-13 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-14 | `dotnet test ECSSimulation.sln --filter "FullyQualifiedName!~RunCommandEndToEndTests.AT01"` — every existing test stays green; new tests pass. (The AT01 exclusion is the bug WP-2.0.C fixes; once that lands, drop the filter.) | build |

---

## Followups (not in scope)

- **Weekday and seasonal awareness** — the world bible flags this as an open question. When Talon answers, schedules gain Day-of-week / Friday-feeling shape.
- **Per-NPC schedule deviation** — an NPC's archetype schedule is the *default*; events should occasionally rewrite it ("Donna has a doctor's appointment Tuesday"). Rare-event injection is later.
- **Activity-driven behaviour** — `ScheduleActivityKind.Meeting` should bind two NPCs together for the block's duration; `Sleeping` should suppress most drive activity. v0.1 emits the label; semantics come later.
- **Integration with cast generator** — Phase 3 polish: roll `ScheduleSpawnerSystem` into the cast generator so spawn produces a fully-populated NPC in one pass. Currently bolt-on; refactor when both surfaces are stable.
- **Schedule visualisation** — the aesthetic bible's deferred 2.5D renderer should highlight an NPC's current scheduled activity (icon overlay or status text). Renderer-phase concern.
