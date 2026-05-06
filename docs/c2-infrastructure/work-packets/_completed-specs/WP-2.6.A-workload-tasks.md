# WP-2.6.A — Workload + Tasks System

**Tier:** Sonnet
**Depends on:** WP-2.1.A (action-selection seam), WP-2.2.A (schedule layer for time-of-day), WP-2.4.A (stress system — receives overdue-task signals), WP-1.7.A (world definition — task entities live in the same world)
**Parallel-safe with:** WP-2.1.B (different system surface), WP-2.5.A (different component surface)
**Timebox:** 120 minutes
**Budget:** $0.50

---

## Goal

Give the office something to do. Right now NPCs drift through their schedules, drives flicker, dialog happens, masks crack — but nobody is *working*. The world bible's gameplay loop ("orders come in, nobody told you, you're not prepared") needs a concrete substrate: tasks as entities, deadlines that matter, work-progress modulated by physiology and stress, and a feedback loop where overdue tasks generate stress that further degrades work-progress. This is the closing piece of the alive-feeling foundation: physiology + social state + spatial state + work pressure now interact.

This packet ships:

1. `TaskComponent` on a new family of task entities (separate from NPCs; a task is an entity with effort, deadline, priority, progress, quality, and an assigned NPC).
2. `WorkloadComponent` per NPC tracking active tasks, capacity, current load.
3. `WorkloadSystem` that advances assigned tasks each tick, with progress-rate modulated by physiology + stress.
4. `TaskGeneratorSystem` that synthesizes incoming tasks at a configurable rate (the bible's "orders come in, nobody told you" — initially scripted; player-driven assignment is later).
5. New `IntendedActionKind.Work` value and a candidate enumeration source in `ActionSelectionSystem` so an assigned NPC at their desk produces a `Work(taskEntityId)` intent.
6. A new `OverdueTask` narrative event kind that feeds the stress system (overdue → stress source per the WP-2.4.A loop).

After this packet, the world generates work, NPCs work on it (when their drives, willpower, schedule, and physiology cooperate), missed deadlines accumulate into the stress they cause — and the bible's central gameplay loop has its substrate.

---

## Reference files

- `docs/c2-content/world-bible.md` — **read first.** Gameplay loop ("orders come in, nobody told you"); tone (work as the source of stress and the thing that interferes with every biological need at every wrong moment).
- `docs/c2-content/cast-bible.md` — archetypes vary in capacity. The Climber works hard; the Founder's Nephew works at 40%; the Newbie can be overwhelmed. Tasks-per-archetype tuning lives in this packet's data file.
- `docs/c2-content/new-systems-ideas.md` — Section 2 (WorkloadSystem + TaskComponent) is the design source. The bullet "tasks as entities, deadlines, NPC capacity, work-progress modulated by physiology" is the v0.1 commitment.
- `docs/c2-infrastructure/00-SRD.md` §8.1 (no runtime LLM), §8.5 (social state is first-class).
- `docs/c2-infrastructure/work-packets/_completed/WP-2.1.A.md` — `IntendedActionKind` enum to extend; `IntendedActionComponent` shape; `ActionSelectionSystem` enumeration patterns to add a `Work` candidate to.
- `docs/c2-infrastructure/work-packets/_completed/WP-2.2.A.md` — schedule layer. Tasks should be worked on during scheduled `AtDesk` blocks; this packet adds candidate emission gated on schedule activity.
- `docs/c2-infrastructure/work-packets/_completed/WP-2.4.A.md` — stress system. This packet adds a new stress source: each overdue task an NPC owns contributes `+overdueTaskStressGain` per tick. The stress system's existing source-counting machinery is extended to count overdue tasks.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.6.A.md` — narrative event bus. New `OverdueTask` and `TaskCompleted` candidate kinds emit here.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.7.A.md` — world definition loader. Tasks are entities; this packet adds a programmatic generator (no JSON definition needed at v0.1; tasks are runtime-generated).
- `APIFramework/Components/IntendedActionComponent.cs` — extend `IntendedActionKind` with `Work`. Add at end of enum, additive only. **Coordinate with WP-2.5.A**: that packet adds `MaskSlip` to `DialogContextValue` (different enum). The two packets touch different enums in the same file — coordinate the diff with care, but they don't logically conflict.
- `APIFramework/Components/SocialDrivesComponent.cs`, `WillpowerComponent.cs`, `StressComponent.cs`, `EnergyComponent.cs`, `MetabolismComponent.cs`, `BladderComponent.cs` — read for the per-tick progress-rate modifier inputs.
- `APIFramework/Components/PersonalityComponent.cs` — Conscientiousness modulates work-progress quality; high Conscientiousness = better quality, slightly slower throughput.
- `APIFramework/Components/CastSpawnComponents.cs` — `NpcArchetypeComponent.ArchetypeId` keys per-archetype capacity defaults.
- `APIFramework/Components/Tags.cs` — add `TaskTag` and `OverdueTag` (for tasks); `BurnedOutFromWorkloadTag` (NPC) — purely a query helper.
- `APIFramework/Systems/Narrative/NarrativeEventBus.cs`, `NarrativeEventCandidate.cs`, `NarrativeEventKind.cs` — add `OverdueTask` and `TaskCompleted` enum values.
- `APIFramework/Systems/StressSystem.cs` (modified) — extend the source-counting machinery to count `OverdueTask` candidates per NPC per tick and apply the per-tick stress gain. **One small modification only**; if the source set is already pluggable, add a new entry; if not, add a parallel source counter in `StressComponent` and a one-line scaling in the per-tick update.
- `APIFramework/Systems/ActionSelectionSystem.cs` (modified) — add a `Work` candidate enumeration source: when the NPC has a `WorkloadComponent` with active assigned tasks AND `CurrentScheduleBlockComponent.Activity == AtDesk`, emit a `Work(taskEntityId)` candidate with weight `workActionBaseWeight` (default 0.40 — slightly above schedule's 0.30 because work-during-desk-hours is what the schedule means).
- `APIFramework/Components/CurrentScheduleBlockComponent.cs` — read `Activity` and `AnchorEntityId` for the schedule gating above.
- `APIFramework/Core/SimulationClock.cs` — `TotalTime` is the deadline reference for tasks. Use `DayNumber` for daily task generation rate.
- `APIFramework/Core/SimulationBootstrapper.cs` — register all new systems.
- `APIFramework/Core/SeededRandom.cs` — for task generation. No `System.Random`.

## Non-goals

- Do **not** add player-facing task assignment or a player-driven UI. Tasks generate from `TaskGeneratorSystem` per a configured rate; assignment is round-robin / capacity-based at v0.1.
- Do **not** model task content (what the task *is*). Each task is `Effort hours, Deadline tick, Priority, Progress, Quality, AssignedNpcId` — no semantic tags, no task categories. The Climber doesn't get "exec" tasks vs "shipping" tasks. v0.1 is generic.
- Do **not** model collaboration. A task has one assignee. Multi-NPC tasks (the bible's "Meeting" activity) are deferred to a follow-up.
- Do **not** model task dependencies, blocking, or chains. A task is independent; complete in any order.
- Do **not** add a "task quality" gameplay loop where low-quality work has knock-on consequences (rework, reputation, etc.). v0.1 records `QualityLevel` but no system reacts to it. Future packets can.
- Do **not** modify the existing physiology systems (`EatingSystem`, `SleepSystem`, etc.). `WorkloadSystem` reads physiology component values directly; it does not call into other systems.
- Do **not** modify `SocialDrivesComponent`, `WillpowerComponent`, or `InhibitionsComponent`. Workload reads them; does not write.
- Do **not** modify `MemoryRecordingSystem` (WP-2.3.A). The new narrative kinds emit naturally onto the bus; WP-2.3.A routes them based on participants. Per the v0.1 design, `OverdueTask` is solo (1 participant, the NPC) — goes to `PersonalMemoryComponent`. `TaskCompleted` is also solo — same. No code change required in WP-2.3.A; the `Persistent` mapping for these new kinds defaults to `false` until a follow-up patches it.
- Do **not** reduce or restructure ActionSelectionSystem's existing candidate enumeration. Add one new source; do not reweight or remove existing ones.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (SRD §8.1.)
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### The new components

```csharp
public struct TaskComponent
{
    public float EffortHours;       // total work needed, in game-hours
    public long  DeadlineTick;      // SimulationClock tick when overdue
    public int   Priority;          // 0..100; influences candidate weight
    public float Progress;          // 0..1; fraction complete
    public float QualityLevel;      // 0..1; degrades under poor conditions
    public Guid  AssignedNpcId;     // Guid.Empty = unassigned
    public long  CreatedTick;
}

public struct WorkloadComponent
{
    public IReadOnlyList<Guid> ActiveTasks;     // task entity ids
    public int Capacity;                         // max simultaneous active tasks (per archetype)
    public int CurrentLoad;                      // 0..100; computed as activeTasks.count / capacity * 100
}
```

`TaskTag` marks task entities so systems can iterate them. `OverdueTag` is added/removed by `WorkloadSystem` as deadlines pass.

### The new systems

**`TaskGeneratorSystem`** runs at `PreUpdate` phase (existing). Each game-day at a configurable hour (default 8:00 AM) it generates `taskGenerationCountPerDay` (default 5) new tasks. For each:
- `EffortHours` = random 0.5..6.0
- `DeadlineTick` = current tick + random `(deadlineMinHours, deadlineMaxHours)` × ticks-per-game-hour
- `Priority` = random 30..80
- `AssignedNpcId` = round-robin over NPCs with available `WorkloadComponent.Capacity`; if all at capacity, the task generates unassigned (`Guid.Empty`).
- Other fields default.

Determinism: `SeededRandom`, processed in deterministic NPC order (ascending `EntityIntId`).

**`WorkloadSystem`** runs at `Cleanup` phase (after action selection has decided what each NPC is doing). For each NPC with `WorkloadComponent`:
1. Update `CurrentLoad = ActiveTasks.Count * 100 / Capacity`.
2. If the NPC's `IntendedActionComponent.Kind == Work` AND target is one of their active tasks:
   - Compute `progressRate = baseProgressRate × physiologyMult × stressMult × consciousnessMult`
   - Increment `task.Progress += progressRate × deltaSeconds`
   - Update `task.QualityLevel` (decays under low energy, high stress, hunger; recovers slowly under good conditions).
3. For each task whose `Progress ≥ 1.0`:
   - Emit `NarrativeEventKind.TaskCompleted` for the assignee NPC.
   - Remove from `ActiveTasks`. Destroy or mark the task entity (Sonnet picks; destruction is simpler).
4. For each task whose `DeadlineTick < currentTick AND !task.Has<OverdueTag>()`:
   - Add `OverdueTag`.
   - Emit `NarrativeEventKind.OverdueTask` for the assignee NPC.

```csharp
double physiologyMult = 1.0
    * (energy.Energy / 100.0)                              // tired NPCs work slower
    * (npc.Has<HungryTag>() ? 0.7 : 1.0)
    * (npc.Has<DehydratedTag>() ? 0.6 : 1.0)
    * (npc.Has<BladderCriticalTag>() ? 0.3 : 1.0);

double stressMult = npc.Has<OverwhelmedTag>() ? 0.5
                  : npc.Has<StressedTag>() ? 0.8
                  : 1.0;

double consciousnessMult = 1.0 + (personality.Conscientiousness * conscientiousnessProgressBias);
```

### Per-archetype capacity defaults

A new file `docs/c2-content/archetypes/archetype-workload-capacity.json`:

```jsonc
{
  "schemaVersion": "0.1.0",
  "archetypeCapacity": [
    {"archetypeId": "the-vent",            "capacity": 3},
    {"archetypeId": "the-hermit",          "capacity": 2},
    {"archetypeId": "the-climber",         "capacity": 5},
    {"archetypeId": "the-cynic",           "capacity": 3},
    {"archetypeId": "the-newbie",          "capacity": 2},
    {"archetypeId": "the-old-hand",        "capacity": 4},
    {"archetypeId": "the-affair",          "capacity": 3},
    {"archetypeId": "the-recovering",      "capacity": 2},
    {"archetypeId": "the-founders-nephew", "capacity": 1},
    {"archetypeId": "the-crush",           "capacity": 3}
  ]
}
```

A `WorkloadInitializerSystem` (parallel to `StressInitializerSystem` and the others) attaches `WorkloadComponent` with the per-archetype capacity at boot.

### ActionSelectionSystem touch

Add ONE new candidate source (additive, parallel to WP-2.2.A's schedule source):

```csharp
// inside ActionSelectionSystem.Update, alongside other enumeration sources:
if (npc.Has<WorkloadComponent>() && npc.Has<CurrentScheduleBlockComponent>())
{
    var workload = npc.Get<WorkloadComponent>();
    var schedule = npc.Get<CurrentScheduleBlockComponent>();
    if (workload.ActiveTasks.Count > 0 && schedule.Activity == ScheduleActivityKind.AtDesk)
    {
        // pick highest-priority active task
        var topTask = workload.ActiveTasks
            .Select(id => entityManager.Get(id))
            .Where(t => t != null)
            .OrderByDescending(t => t.Get<TaskComponent>().Priority)
            .FirstOrDefault();
        if (topTask != null)
        {
            candidates.Add(new Candidate {
                Kind = IntendedActionKind.Work,
                TargetEntityId = WillpowerSystem.EntityIntId(topTask),
                Weight = config.WorkActionBaseWeight,
                Source = CandidateSource.Workload,
                Context = DialogContextValue.None
            });
        }
    }
}
```

`CandidateSource.Workload` is a new enum value (additive, parallel to `Drive`, `Schedule`, `Idle`, etc.). `IntendedActionKind.Work` is the new IntendedAction value.

### Stress system extension

Add an `OverdueTaskEventsToday` counter to `StressComponent`. The `StressSystem`'s per-tick update gets one new branch (parallel to `SuppressionEventsToday`, `DriveSpikeEventsToday`, `SocialConflictEventsToday`):

```csharp
// In StressSystem source counting:
int overdueCount = npc.Has<WorkloadComponent>()
    ? npc.Get<WorkloadComponent>().ActiveTasks
        .Select(id => entityManager.Get(id))
        .Count(t => t != null && t.Has<OverdueTag>())
    : 0;
stress.OverdueTaskEventsToday += overdueCount;
acuteGain += overdueCount * config.OverdueTaskStressGain * neuroFactor;
```

Default `OverdueTaskStressGain = 1.0` (per overdue task, per tick).

### SimConfig additions

```jsonc
{
  "workload": {
    "taskGenerationHourOfDay":     8.0,
    "taskGenerationCountPerDay":   5,
    "taskEffortHoursMin":          0.5,
    "taskEffortHoursMax":          6.0,
    "taskDeadlineHoursMin":        4.0,
    "taskDeadlineHoursMax":        48.0,
    "taskPriorityMin":             30,
    "taskPriorityMax":             80,
    "baseProgressRatePerSecond":   0.0001,
    "conscientiousnessProgressBias": 0.10,
    "qualityDecayPerStressedTick": 0.0002,
    "qualityRecoveryPerGoodTick":  0.0001,
    "workActionBaseWeight":        0.40,
    "overdueTaskStressGain":       1.0
  }
}
```

### Determinism

All RNG via `SeededRandom`. Task generation is deterministic. Round-robin assignment is deterministic (NPCs sorted by `EntityIntId`). Quality decay is deterministic. The 5000-tick test confirms byte-identical task and workload state.

### Tests

- `TaskComponentTests.cs` — construction, clamping (`Progress` 0..1, `QualityLevel` 0..1).
- `WorkloadComponentTests.cs` — construction, capacity enforcement.
- `TaskGeneratorSystemTests.cs` — N tasks generated at the configured hour; round-robin assignment; deterministic.
- `WorkloadSystemProgressTests.cs` — assigned NPC at desk advances task progress; well-rested fed NPC progresses faster than tired hungry one; stressed NPC progresses slower.
- `WorkloadSystemCompletionTests.cs` — task at `Progress ≥ 1.0` removed from `ActiveTasks`, `TaskCompleted` candidate emitted.
- `WorkloadSystemOverdueTests.cs` — task past `DeadlineTick` gains `OverdueTag` and emits `OverdueTask` candidate (once, not every tick).
- `WorkloadStressIntegrationTests.cs` — NPC with 3 overdue tasks accumulates stress per the configured gain; verifies the new source counter on `StressComponent`.
- `ActionSelectionWorkIntegrationTests.cs` — NPC with active task + `AtDesk` schedule → `IntendedAction(Work, taskEntity)`.
- `ActionSelectionWorkOverridingScheduleTests.cs` — high-drive condition still overrides work (drive candidate weight > work weight).
- `WorkloadDeterminismTests.cs` — 5000-tick byte-identical task + workload state.
- `ArchetypeWorkloadCapacityJsonTests.cs` — JSON loads, all 10 archetypes present, capacity 1..10.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/TaskComponent.cs` | Task entity component. |
| code | `APIFramework/Components/WorkloadComponent.cs` | Per-NPC workload tracker. |
| code | `APIFramework/Components/IntendedActionComponent.cs` (modified) | Add `Work` to `IntendedActionKind` enum (additive at end). **Coordinate with WP-2.5.A** which adds `MaskSlip` to `DialogContextValue` in the same file; the changes are to different enums and don't logically collide. |
| code | `APIFramework/Components/StressComponent.cs` (modified) | Add `OverdueTaskEventsToday` counter field. |
| code | `APIFramework/Components/Tags.cs` (modified) | Add `TaskTag`, `OverdueTag`, `BurnedOutFromWorkloadTag`. |
| code | `APIFramework/Systems/TaskGeneratorSystem.cs` | Per-day task generation. |
| code | `APIFramework/Systems/WorkloadSystem.cs` | Per-tick progress + completion + overdue detection. |
| code | `APIFramework/Systems/WorkloadInitializerSystem.cs` | Spawn-time `WorkloadComponent` attachment. |
| code | `APIFramework/Systems/ActionSelectionSystem.cs` (modified) | Add `Work` candidate source. |
| code | `APIFramework/Systems/StressSystem.cs` (modified) | Add overdue-task source counter and stress gain. |
| code | `APIFramework/Systems/Narrative/NarrativeEventKind.cs` (modified) | Add `OverdueTask`, `TaskCompleted` enum values. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `TaskGeneratorSystem` (PreUpdate), `WorkloadSystem` (Cleanup), `WorkloadInitializerSystem` (boot/first-tick). |
| code | `APIFramework/Config/SimConfig.cs` (modified) | `WorkloadConfig` class + property. |
| code | `SimConfig.json` (modified) | `workload` section. |
| data | `docs/c2-content/archetypes/archetype-workload-capacity.json` | 10 archetypes with capacity. |
| code | `APIFramework.Tests/Components/TaskComponentTests.cs` | Construction, clamping. |
| code | `APIFramework.Tests/Components/WorkloadComponentTests.cs` | Construction, capacity. |
| code | `APIFramework.Tests/Systems/TaskGeneratorSystemTests.cs` | Generation rate + assignment determinism. |
| code | `APIFramework.Tests/Systems/WorkloadSystemProgressTests.cs` | Per-tick progress modulated by physiology. |
| code | `APIFramework.Tests/Systems/WorkloadSystemCompletionTests.cs` | Completion + narrative emission. |
| code | `APIFramework.Tests/Systems/WorkloadSystemOverdueTests.cs` | Overdue detection + tag + candidate. |
| code | `APIFramework.Tests/Integration/WorkloadStressIntegrationTests.cs` | Overdue tasks accumulate stress. |
| code | `APIFramework.Tests/Integration/ActionSelectionWorkIntegrationTests.cs` | Work candidate fires under correct conditions. |
| code | `APIFramework.Tests/Integration/ActionSelectionWorkOverridingScheduleTests.cs` | High-drive overrides work. |
| code | `APIFramework.Tests/Systems/WorkloadDeterminismTests.cs` | 5000-tick byte-identical. |
| code | `APIFramework.Tests/Data/ArchetypeWorkloadCapacityJsonTests.cs` | JSON validation. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-2.6.A.md` | Completion note. SimConfig defaults; archetype capacity table; whether `WP-2.3.A`'s `Persistent` mapping for `OverdueTask` and `TaskCompleted` was added or punted. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `TaskComponent`, `WorkloadComponent`, new tags + enum values compile, instantiate, equality round-trip. | unit-test |
| AT-02 | `TaskGeneratorSystem` at the configured hour generates exactly `taskGenerationCountPerDay` tasks; round-robin assignment respects per-NPC capacity; 0 tasks generated outside the trigger hour. | unit-test |
| AT-03 | `WorkloadSystem` advances task progress when NPC has `IntendedAction(Work, task)`; over N ticks, progress increases by `baseProgressRate × N × deltaSeconds × multipliers`. | unit-test |
| AT-04 | Tired hungry NPC progresses slower than well-rested fed NPC under identical task conditions (statistical comparison over 1000 ticks). | unit-test |
| AT-05 | Task at `Progress ≥ 1.0` is removed from `ActiveTasks`; `TaskCompleted` candidate emitted with the assignee as participant. | unit-test |
| AT-06 | Task past `DeadlineTick` gains `OverdueTag` exactly once; `OverdueTask` candidate emitted exactly once at the transition (not every subsequent tick). | unit-test |
| AT-07 | Integration: NPC with 3 overdue tasks accumulates `StressComponent.AcuteLevel` at the configured rate (`overdueCount × overdueTaskStressGain` per tick, modulo neuroticism). | integration-test |
| AT-08 | Integration: NPC with active task + `AtDesk` schedule + quiet drives produces `IntendedAction(Work, task)`. | integration-test |
| AT-09 | Integration: same NPC with `Irritation = 80` and a coworker in proximity produces `IntendedAction(Dialog, LashOut)` instead — drive overrides work, work overrides idle. | integration-test |
| AT-10 | Determinism: 5000-tick run, two seeds with the same world: byte-identical task + workload state. | unit-test |
| AT-11 | `archetype-workload-capacity.json` loads cleanly, all 10 archetypes present, capacities in 1..10. | unit-test |
| AT-12 | All Wave 1, Wave 2, and other Wave 3 tests stay green. | regression |
| AT-13 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-14 | `dotnet test ECSSimulation.sln` — all green, no exclusions. | build + unit-test |

---

## Followups (not in scope)

- **Player-driven assignment.** Player should be able to drag-drop tasks onto NPCs, override priorities, escalate. Phase 3 UI work.
- **Task content / categories.** v0.1 tasks are anonymous effort blobs. Future packet adds task kinds (shipping, paperwork, meeting, code) and per-archetype affinity (the Climber excels at high-visibility tasks; the Hermit at deep work).
- **Multi-NPC tasks.** Meeting-shaped tasks that bind two or more NPCs together for the block's duration. Couples to schedule layer's `Meeting` activity.
- **Task dependencies / chains.** Project decomposition. Speculative.
- **Quality gameplay loop.** Low-quality work has knock-on consequences (rework, reputation). v0.1 tracks `QualityLevel` but no consumer reacts.
- **PIP (Performance Improvement Plan) arc.** From `new-systems-ideas.md`: NPC's quality output tracked over days; threshold flag; nobody talks about it directly. Long-arc gameplay; Phase 3+.
- **The all-nighter.** Sleep drive maxed, NPC working through it on a critical deadline. Intersects WP-2.1.B's vetos and WP-2.4.A's stress in interesting ways. Currently each system fires independently; a polish packet could orchestrate the all-nighter as a recognised state.
- **`Persistent` flag patches in WP-2.3.A's mapping** for `OverdueTask` (probably true — overdue tasks are remembered) and `TaskCompleted` (probably false — routine). Trivial follow-up.
- **The "task that's been in progress for two years"** from the deal catalog. A single task generator-spawned at world boot with `EffortHours = 8000`, `DeadlineTick` perpetually deferred. Cute easter egg; later.
