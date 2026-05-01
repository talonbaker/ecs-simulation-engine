# WP-3.2.3 — Chore Rotation System

> **STATUS:** SHIPPED to staging 2026-04-30. Retained because pending packets depend on this spec: WP-3.2.4, WP-3.2.5.

> **DO NOT DISPATCH UNTIL ALL OF PHASE 3.1.x IS MERGED.**
> Couples to schedule (WP-2.2.A), workload (WP-2.6.A), memory (WP-2.3.A), bereavement / stress (WP-3.0.2).

**Tier:** Sonnet
**Depends on:** Phase 0/1/2 (schedule, workload, memory), Phase 3.0.x (life-state for guard pattern)
**Parallel-safe with:** WP-3.2.2 (physics), WP-3.2.4 (rescue), WP-3.2.5 (per-archetype tuning)
**Timebox:** 130 minutes
**Budget:** $0.55

---

## Goal

Per the **NPC-driven challenge surface memory:** the office's challenge isn't task overload — it's NPC management. The chore rotation is the canonical example: trash duty, fridge cleaning, microwave cleaning. Somebody has to do it. Nobody wants to. The wrong NPC doing it does a bad job. The right NPC over-rotated burns out. Donna always cleans the microwave; she's annoyed about it; one week she snaps and refuses; the office has to figure out who covers.

After this packet:

- A `ChoreEntity` family represents recurring office duties: clean-microwave, clean-fridge, clean-bathroom, take-out-trash, refill-water-cooler, restock-supply-closet, replace-toner. Each chore has a frequency (every N game-days), a duration, a cleanliness contribution.
- A `ChoreAssignmentSystem` rotates the chore assignment per archetype-acceptance-bias. Some archetypes accept; some quietly do it; some refuse loudly.
- A `ChoreExecutionSystem` runs when an NPC works on the chore. Quality = function of acceptance-willingness × time-spent × stress × overrotation-history.
- Bad-quality execution leaves the chore "incomplete" — microwave stays dirty, fall-risk stains stay, trash bin overflows. Other NPCs notice.
- Right NPC over-rotated → `ChoreOverrotationStress` source counter on `StressComponent`; eventually they refuse a chore.
- Persistent memory entries: "Donna refused to clean the microwave on Day 17"; "Frank did the trash badly three times in a row"; "Greg quietly handled the supply closet without telling anyone."

---

## Reference files

- **NPC-driven challenge memory** — `project_challenge_surface_npc_driven.md`. Read for framing.
- `docs/c2-content/world-bible.md` — named anchors. Each chore targets a specific named anchor (Microwave, Fridge, Supply Closet).
- `docs/c2-content/cast-bible.md` — archetype acceptance varies. Climber accepts to be seen; Cynic accepts because nobody else will; Founder's Nephew refuses; Recovering quietly handles.
- `docs/c2-infrastructure/work-packets/_completed/WP-2.2.A.md` — schedule layer. Chores fire during scheduled blocks.
- `docs/c2-infrastructure/work-packets/_completed/WP-2.6.A.md` — workload + tasks. Chores parallel tasks.
- `docs/c2-infrastructure/work-packets/_completed/WP-2.3.A.md` — memory recording.
- `docs/c2-content/ux-ui-bible.md` — chores surface diegetically (stink lines / dust motes).

---

## Non-goals

- Do **not** ship player-facing chore assignment UI. Player observes; doesn't directly assign.
- Do **not** ship per-chore content authoring beyond 7 listed kinds at v0.1.
- Do **not** ship visible "stink lines on dirty microwave" rendering — host renders environmental iconography per UX §1.6.
- Do **not** ship NPC-on-NPC reprisal mechanics.
- Do **not** modify schedule or workload systems beyond reading.
- Do **not** retry, recurse, or self-heal.

---

## Design notes

### `ChoreComponent`

```csharp
public struct ChoreComponent
{
    public ChoreKind Kind;
    public float CompletionLevel;           // 0..1; below 0.5 = "dirty"
    public float QualityOfLastExecution;    // 0..1
    public long  LastDoneTick;
    public long  NextScheduledTick;
    public Guid  CurrentAssigneeId;         // Guid.Empty = unassigned
    public Guid  TargetAnchorId;            // microwave entity id, etc.
}

public enum ChoreKind
{
    CleanMicrowave = 0,
    CleanFridge = 1,
    CleanBathroom = 2,
    TakeOutTrash = 3,
    RefillWaterCooler = 4,
    RestockSupplyCloset = 5,
    ReplaceToner = 6,
}
```

### `ChoreHistoryComponent` (per-NPC)

```csharp
public struct ChoreHistoryComponent
{
    public Dictionary<ChoreKind, int> TimesPerformed;
    public Dictionary<ChoreKind, int> TimesRefused;
    public Dictionary<ChoreKind, float> AverageQuality;
    public long LastRefusalTick;
}
```

### `StressComponent` extension

Add `ChoreOverrotationEventsToday` source counter. `StressSystem` adds branch parallel to existing source counters.

### `ChoreAssignmentSystem`

PreUpdate phase, runs once per game-day at `choreCheckHour`. For each `ChoreComponent` whose `NextScheduledTick <= currentTick`:

1. Compute candidate set: Alive NPCs not already assigned.
2. Score each: `acceptanceBias[archetype][choreKind] - overrotationPenalty[npc][choreKind] + qualityBonus[npc][choreKind]`.
3. Highest score wins. If no candidate above `minChoreAcceptanceBias`, chore stays unassigned.
4. Emit `NarrativeEventKind.ChoreAssigned`.

### `ChoreExecutionSystem`

Cleanup phase, after WorkloadSystem. For each NPC with `IntendedAction.Kind == ChoreWork`:

1. Verify proximity (NPC in same room as `chore.TargetAnchorId`).
2. Advance `chore.CompletionLevel += baseRate × physiologyMult × stressMult × acceptanceBiasMult`.
3. On completion: emit `ChoreCompleted` narrative; update `ChoreHistoryComponent`; reset chore.

### Action selection candidate

New candidate source: when `chore.CurrentAssigneeId == this.Id` AND `CompletionLevel < 1.0` AND not in conversation, emit `ChoreWork(choreEntityId)` with `choreActionBaseWeight = 0.35`.

If archetype-acceptance-bias < threshold, candidate not emitted (silent refusal).

### Persistent memory routing

New `NarrativeEventKind` values (additive):
- `ChoreAssigned` — persistent: false (routine).
- `ChoreCompleted` — persistent: false (routine).
- `ChoreRefused` — persistent: true.
- `ChoreBadlyDone` — persistent: true.
- `ChoreOverrotation` — persistent: true.

### SimConfig additions

```jsonc
{
  "chores": {
    "choreCheckHourOfDay":         18.0,
    "frequencyTicks": {
      "cleanMicrowave":           7200000,
      "cleanFridge":              14400000,
      "cleanBathroom":            3600000,
      "takeOutTrash":             1440000,
      "refillWaterCooler":        2880000,
      "restockSupplyCloset":      28800000,
      "replaceToner":             14400000
    },
    "choreActionBaseWeight":       0.35,
    "choreOverrotationThreshold":  3,
    "choreOverrotationWindowGameDays": 7,
    "choreOverrotationStressGain": 1.5,
    "choreCompletionRatePerSecond": 0.0001,
    "minChoreAcceptanceBias":      0.20
  }
}
```

### `chore-archetype-acceptance-bias.json`

```jsonc
{
  "schemaVersion": "0.1.0",
  "biases": {
    "the-old-hand":         {"cleanMicrowave": 0.85, "cleanFridge": 0.70, "takeOutTrash": 0.50},
    "the-cynic":            {"cleanMicrowave": 0.60, "cleanFridge": 0.55, "takeOutTrash": 0.65},
    "the-newbie":           {"cleanMicrowave": 0.95, "cleanFridge": 0.90, "takeOutTrash": 0.85},
    "the-founders-nephew":  {"cleanMicrowave": 0.05, "cleanFridge": 0.05, "takeOutTrash": 0.05},
    "the-climber":          {"cleanMicrowave": 0.40, "cleanFridge": 0.30, "takeOutTrash": 0.20},
    "the-recovering":       {"cleanMicrowave": 0.70, "cleanFridge": 0.65, "takeOutTrash": 0.55},
    "the-vent":             {"cleanMicrowave": 0.30, "cleanFridge": 0.25, "takeOutTrash": 0.20},
    "the-hermit":           {"cleanMicrowave": 0.50, "cleanFridge": 0.45, "takeOutTrash": 0.40}
  }
}
```

### Tests

- `ChoreComponentTests.cs`, `ChoreHistoryComponentTests.cs` — construction.
- `ChoreAssignmentSystemTests.cs`, `ChoreAssignmentNoEligibleTests.cs`.
- `ChoreActionSelectionCandidateTests.cs`, `ChoreActionSelectionRefusalTests.cs`.
- `ChoreExecutionProgressTests.cs`, `ChoreCompletionTests.cs`.
- `ChoreOverrotationTests.cs`, `ChoreBadQualityMemoryTests.cs`, `ChoreRefusalMemoryTests.cs`.
- `ChoreDeterminismTests.cs` — 5000-tick run with chores: byte-identical.
- `ChoreAcceptanceBiasJsonTests.cs` — JSON loads; all archetypes covered.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/ChoreComponent.cs` | Chore. |
| code | `APIFramework/Components/ChoreHistoryComponent.cs` | Per-NPC history. |
| code | `APIFramework/Components/StressComponent.cs` (modified) | Add `ChoreOverrotationEventsToday`. |
| code | `APIFramework/Components/IntendedActionComponent.cs` (modified) | Add `ChoreWork`. |
| code | `APIFramework/Systems/Chores/ChoreAssignmentSystem.cs` | Daily assignment. |
| code | `APIFramework/Systems/Chores/ChoreExecutionSystem.cs` | Per-tick execution. |
| code | `APIFramework/Systems/Chores/ChoreInitializerSystem.cs` | Spawn-time init. |
| code | `APIFramework/Systems/ActionSelectionSystem.cs` (modified) | Add ChoreWork candidate source. |
| code | `APIFramework/Systems/StressSystem.cs` (modified) | Read overrotation counter. |
| code | `APIFramework/Systems/Narrative/NarrativeEventKind.cs` (modified) | Add ChoreAssigned/Completed/Refused/BadlyDone/Overrotation. |
| code | `APIFramework/Systems/MemoryRecordingSystem.cs` (modified) | Persistent flags for chore narratives. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register chore systems. |
| code | `APIFramework/Config/SimConfig.cs` (modified) | `ChoreConfig`. |
| config | `SimConfig.json` (modified) | `chores` section. |
| data | `docs/c2-content/chores/chore-archetype-acceptance-bias.json` | Per-archetype biases. |
| test | (~13 test files) | Comprehensive coverage. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.2.3.md` | Completion note. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `ChoreComponent`, `ChoreHistoryComponent`, new enum values compile. | unit-test |
| AT-02 | Chore due → assigned to highest-acceptance-bias Alive NPC. | integration-test |
| AT-03 | All NPCs refuse → chore stays unassigned. | integration-test |
| AT-04 | Assigned NPC at-desk-or-near-anchor → `ChoreWork` candidate emitted. | integration-test |
| AT-05 | Bias < `minChoreAcceptanceBias` → no candidate; emit `ChoreRefused` narrative. | integration-test |
| AT-06 | Execution: NPC at chore anchor → `CompletionLevel` advances. | integration-test |
| AT-07 | At `CompletionLevel ≥ 1.0` → `ChoreCompleted` narrative; chore resets. | integration-test |
| AT-08 | Overrotation: NPC does same chore 4 times in 7 game-days → `ChoreOverrotationEventsToday` increments; stress accrues. | integration-test |
| AT-09 | Low-quality execution → `ChoreBadlyDone` persistent narrative emitted. | integration-test |
| AT-10 | Refusal → `ChoreRefused` persistent narrative; affected colleagues' relationship-memory records refusal. | integration-test |
| AT-11 | Determinism: 5000 ticks two seeds: byte-identical chore state. | integration-test |
| AT-12 | `chore-archetype-acceptance-bias.json` loads; all cast-bible archetypes covered. | unit-test |
| AT-13 | All Phase 0/1/2/3.0.x/3.1.x and prior 3.2.x tests stay green. | regression |
| AT-14 | `dotnet build` warning count = 0; `dotnet test` all green. | build + test |

---

## Followups (not in scope)

- Visible chore consequence iconography (stink lines, trash overflow). Host-side rendering follows engine state.
- NPC-on-NPC reprisal. Future emergent.
- Chore quality tier (perfect / good / passable / sloppy). Future tiering.
- Per-chore unique consequence (uncleaned bathroom → stain hazard; uncleaned fridge → NPC eats spoiled food). Future content.
- Player-influence on chore visibility. Couples to build mode; future.
- Chore swap negotiation ("I'll do trash if you do microwave"). Future.
- Per-archetype quality-bias. Future tuning.
- Plague-week chore accumulation. Couples to future sickness packet.


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
