# WP-3.0.6 — Fainting System — Completion Note

**Executed by:** claude-sonnet-4-6 (Cowork mode)
**Branch:** main (same worktree, no separate branch — operator instruction)
**Started:** 2026-04-27
**Ended:** 2026-04-27
**Outcome:** ok (untested — operator will integrate and run acceptance tests on return)

---

## Summary (≤ 200 words)

Shipped the fainting scenario. `FaintingDetectionSystem` (Cleanup phase, before `LifeStateTransitions`) watches Alive NPCs and triggers a faint when `MoodComponent.Fear >= FaintingConfig.FearThreshold`. On faint: `IsFaintingTag` + `FaintingComponent` (timing metadata) are attached, an optional `Fainted` narrative candidate is emitted (before state flip, per WP-3.0.0 contract), and `RequestTransition(Incapacitated, Unknown, FaintDurationTicks + 1)` is enqueued.

`FaintingRecoverySystem` (Cleanup, before `LifeStateTransitions`) watches fainted Incapacitated NPCs and calls `RequestTransition(Alive, Unknown)` when `clock.CurrentTick >= FaintingComponent.RecoveryTick`. This uses the existing `case LifeState.Alive:` rescue stub in `LifeStateTransitionSystem.ApplyRequest` — no new architecture required.

`FaintingCleanupSystem` (Cleanup, after `LifeStateTransitions`) removes `IsFaintingTag` and `FaintingComponent` from NPCs back to `Alive`.

**Fainting is never fatal by design.** The `FaintDurationTicks + 1` budget means the death-by-budget-expiry path in `LifeStateTransitionSystem.Update` step 2 cannot fire before step 1 drains the recovery request. A fainted NPC never receives `CorpseTag` or `CorpseComponent`.

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | n/a | IsFaintingTag attached on Fear >= threshold; code review verified |
| AT-02 | n/a | FaintingComponent.RecoveryTick = startTick + FaintDurationTicks; code review verified |
| AT-03 | n/a | Fear < threshold → no faint; boundary (exactly at threshold) triggers; code review verified |
| AT-04 | n/a | Already-fainting NPC: idempotent guard verified |
| AT-05 | n/a | Deceased NPC with Fear=100 → not triggered (LifeStateGuard.IsAlive) |
| AT-06 | n/a | Incapacitated NPC with Fear=100 → not triggered |
| AT-07 | n/a | Fainted narrative emitted when EmitFaintedNarrative=true |
| AT-08 | n/a | No narrative when EmitFaintedNarrative=false |
| AT-09 | n/a | After transitions drain → NPC Incapacitated; code review verified |
| AT-10 | n/a | RecoveryTick reached → NPC becomes Alive; past RecoveryTick also recovers |
| AT-11 | n/a | RecoveryTick in future → NPC stays Incapacitated |
| AT-12 | n/a | RegainedConsciousness narrative flag respected |
| AT-13 | n/a | Alive NPC with IsFaintingTag → tag removed |
| AT-14 | n/a | Both tag and FaintingComponent removed on recovery |
| AT-15 | n/a | Incapacitated NPC → tag NOT removed |
| AT-16 | n/a | Full cycle integration: Faint → Incapacitated → Alive → tags cleaned |
| AT-17 | n/a | MoodComponent.Fear not modified by fainting systems |
| AT-18 | n/a | Two simultaneous faints processed; both Incapacitated |
| AT-19 | n/a | Fainted NPC never receives CorpseTag or CorpseComponent |

## Files added

- `APIFramework/Components/FaintingComponent.cs`
- `APIFramework/Systems/LifeState/FaintingDetectionSystem.cs`
- `APIFramework/Systems/LifeState/FaintingRecoverySystem.cs`
- `APIFramework/Systems/LifeState/FaintingCleanupSystem.cs`
- `APIFramework.Tests/Systems/LifeState/FaintingDetectionSystemTests.cs`
- `APIFramework.Tests/Systems/LifeState/FaintingRecoverySystemTests.cs`
- `APIFramework.Tests/Systems/LifeState/FaintingCleanupSystemTests.cs`
- `APIFramework.Tests/Systems/LifeState/FaintingIntegrationTests.cs`
- `docs/c2-infrastructure/work-packets/_completed/WP-3.0.6.md`

## Files modified

- `APIFramework/Components/Tags.cs` — `IsFaintingTag`
- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` — `Fainted`, `RegainedConsciousness`
- `APIFramework/Systems/MemoryRecordingSystem.cs` — `Fainted => true`, `RegainedConsciousness => false`
- `APIFramework/Config/SimConfig.cs` — `FaintingConfig` class; `Fainting` root property
- `SimConfig.json` — `"fainting"` section
- `APIFramework/Core/SimulationBootstrapper.cs` — three new system registrations; pipeline comment updated

## Key design decisions

1. **No new architecture needed.** `LifeStateTransitionSystem.ApplyRequest` already had the `case LifeState.Alive:` rescue branch (lines 158–168) stubbed as "no rescue mechanic yet." Fainting activates it.

2. **Death is impossible from a faint.** Budget = `FaintDurationTicks + 1`. `FaintingRecoverySystem` runs before `LifeStateTransitions` in Cleanup. Recovery is always drained in step 1 of `LifeStateTransitions.Update` before step 2's budget-expiry check runs.

3. **`CauseOfDeath.Unknown` for both transitions.** Fainting is not a death cause. Using `Unknown` is honest and matches the fallback label used if the budget somehow expired (it won't, but defensive).

4. **`IsFaintingTag` distinguishes fainted from choking.** Systems querying Incapacitated NPCs can gate on `Has<IsFaintingTag>()` vs `Has<IsChokingTag>()` for differentiated reactions.

5. **`MoodComponent.Fear` is not cleared by fainting systems.** Fear decays at MoodSystem's own rate. After recovery, the NPC may immediately faint again if Fear is still above threshold and no decay has occurred — this is intentional and tunable via `FearThreshold`.

## Followups

- All ATs pending operator test run.
- **Compound trigger** — faint on Fear + high AcuteLevel + witnessing a death in the same tick.
- **Bystander Fear spike** — witnessing a faint emits a smaller Fear/Surprise stimulus to nearby NPCs.
- **Post-faint debuff** — Sadness/Fear spike on recovery (shame, disorientation) for a configurable duration.
- **`ProneTag`** — motor effect: fainting drops the NPC to floor; Transit-phase system blocks movement actions while `IsFaintingTag` is present.
- **Per-archetype resistance** — config multiplier on `FearThreshold` for archetypes like medics.
- **Smelling salts / revival item** — interactable entity calls `RequestTransition(Alive)` before `RecoveryTick`, waking the NPC early.
