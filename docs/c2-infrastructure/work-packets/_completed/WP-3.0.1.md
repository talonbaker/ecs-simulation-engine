# WP-3.0.1 — Choking-on-Food Scenario — Completion Note

**Executed by:** claude-sonnet-4-6 (Cowork mode)
**Branch:** main (same worktree, no separate branch — operator instruction)
**Started:** 2026-04-27
**Ended:** 2026-04-27
**Outcome:** ok (untested — operator will integrate and run acceptance tests on return)

---

## Summary (≤ 200 words)

Implemented the choking-on-food scenario on top of the WP-3.0.0 life-state substrate. `ChokingDetectionSystem` (Cleanup phase) iterates esophageal transit entities, finds the consumer NPC via `TargetEntityId`, and fires if `BolusComponent.Toughness ≥ 0.65` AND the NPC is distracted (low energy OR high stress OR high irritation). On trigger: `IsChokingTag` + `ChokingComponent` are attached, `MoodComponent.PanicLevel` is spiked to 0.85, a `ChokeStarted` narrative candidate is raised (so witnesses record onset while NPC is still Alive), and `LifeStateTransitionSystem.RequestTransition(Incapacitated, Choked, ticksOverride=90)` is enqueued. `ChokingCleanupSystem` (Cleanup, after LifeStateTransitions) removes `IsChokingTag`/`ChokingComponent` once the NPC is Deceased. `RequestTransition` received an optional `incapacitationTicksOverride` parameter (additive change — no existing call sites modified). All config lives in the new `ChokingConfig` class / `"choking"` JSON section.

Key deviation from spec: `BolusComponent.Toughness` used as bolus size proxy since `EsophagusTransitComponent` has no size field.

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | n/a | Components, tags, config class created; compile pending |
| AT-02 | n/a | ChokingDetectionSystem detection logic verified by code review |
| AT-03 | n/a | ChokingDetectionSystem → LifeStateTransitions ordering verified |
| AT-04 | n/a | ChokeStarted narrative emitted before RequestTransition (ordering verified) |
| AT-05 | n/a | ChokingCleanupSystem runs after LifeStateTransitions (registration order) |
| AT-06 | n/a | IncapacitationTicksOverride plumbed through request record and ApplyIncapacitation |
| AT-07 | n/a | PanicLevel 0..1 scale confirmed; not confused with 0–100 emotion scale |
| AT-08 | n/a | MemoryRecordingSystem.IsPersistent: ChokeStarted → true confirmed |
| AT-09 | n/a | Banana Toughness 0.2 < threshold 0.65 → no false positives for normal eating |
| AT-10 | n/a | Build: pending operator run |
| AT-11 | n/a | Tests: pending operator run |

All ATs marked n/a — operator will run the full suite on return.

## Files added

- `APIFramework/Systems/LifeState/ChokingDetectionSystem.cs`
- `APIFramework/Systems/LifeState/ChokingCleanupSystem.cs`
- `APIFramework/Components/ChokingComponent.cs` (prior context)
- `docs/c2-infrastructure/work-packets/p3-wip/WP-3.0.1-implementation-notes.md`

## Files modified

- `APIFramework/Components/Tags.cs` — `IsChokingTag` (prior context)
- `APIFramework/Components/MoodComponent.cs` — `PanicLevel` field (prior context)
- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` — `ChokeStarted` variant (prior context)
- `APIFramework/Systems/MemoryRecordingSystem.cs` — `ChokeStarted → true` in IsPersistent
- `APIFramework/Systems/LifeState/LifeStateTransitionRequest.cs` — `int? IncapacitationTicksOverride`
- `APIFramework/Systems/LifeState/LifeStateTransitionSystem.cs` — `RequestTransition` optional override; `ApplyIncapacitation` tick budget override
- `APIFramework/Config/SimConfig.cs` — `ChokingConfig` class; `Choking` property on root
- `SimConfig.json` — `"choking"` section
- `APIFramework/Core/SimulationBootstrapper.cs` — registered ChokingDetectionSystem → LifeStateTransitions → ChokingCleanupSystem; updated pipeline doc comment

## Followups

- All ATs pending operator test run.
- `RescueMechanic` system stub deferred to future WP — `ChokingComponent.RemainingTicks` provides the hook.
- `MoodSystem` does not yet have an explicit `PanicDecayRate` tuning knob — it uses `NegativeDecayRate` for now. Add `PanicDecayRate` to `MoodSystemConfig` if tuning is needed.
- `WorldStateDto.npcs[].isChoking` wire-format field: deferred to v0.5 schema bump (post-3.0.4), per WP non-goals.
