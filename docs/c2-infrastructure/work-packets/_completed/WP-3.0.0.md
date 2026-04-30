# WP-3.0.0 — LifeStateComponent + Cause-of-Death Events — Completion Note

**Executed by:** claude-sonnet-4-6 (Cowork mode)
**Branch:** main (same worktree, no separate branch — operator instruction)
**Started:** 2026-04-27
**Ended:** 2026-04-27
**Outcome:** ok (untested — operator will integrate and run acceptance tests on return)

---

## Summary (≤ 200 words)

Implemented the full death substrate. Every NPC now gets `LifeStateComponent { State=Alive }` at boot via `LifeStateInitializerSystem` (PreUpdate). `LifeStateTransitionSystem` (Cleanup) is the sole writer of `State` and the sole attacher of `CauseOfDeathComponent`; it drains a request queue per tick in ascending NpcId order, emits cause-of-death narrative candidates before flipping state (so MemoryRecordingSystem routes to Alive participants), and counts down `IncapacitatedTickBudget` to auto-complete Incapacitated→Deceased transitions.

The guard sweep touched 43 files. Group A (`IsAlive`) covers all cognitive, social, volitional, and autonomous-action systems. Group B (`IsBiologicallyTicking`) covers all physiology systems plus `SpatialIndexSyncSystem`. `InvariantSystem` branches on `LifeStateComponent.State == Deceased`, running a reduced set of checks on corpses. `EsophagusSystem` guards the consumer entity at the deposit site (not the transit entity) so a bolus headed for a Deceased NPC is discarded without deposit — Incapacitated consumers still receive it, which is what WP-3.0.1's choking scenario requires.

Key deviation from spec: `CauseOfDeathComponent.LocationRoomId` is `string?` (not `Guid`) because `RoomComponent.Id` is a string UUID in this codebase. `EntityRoomMembership` service is used throughout.

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | n/a | Components and enums created; compile/equality tests pending operator run |
| AT-02 | n/a | LifeStateInitializerSystem logic verified by code review; test pending |
| AT-03 | n/a | Transition logic implemented per spec; test pending |
| AT-04 | n/a | Incapacitated countdown implemented; test pending |
| AT-05 | n/a | Witness selection implemented; test pending |
| AT-06 | n/a | Narrative emit before state flip implemented; test pending |
| AT-07 | n/a | MemoryRecordingSystem IsPersistent=true cases added; integration test pending |
| AT-08 | n/a | Group A guard sweep complete; integration test pending |
| AT-09 | n/a | Group B guard sweep complete; integration test pending |
| AT-10 | n/a | Incapacitated drives frozen (Group A), bladder fills (Group B); test pending |
| AT-11 | n/a | Movement freeze: MovementSystem has IsAlive guard; test pending |
| AT-12 | n/a | RelationshipLifecycleSystem: rows preserved, no garbage collection; test pending |
| AT-13 | n/a | InvariantSystem branching implemented; test pending |
| AT-14 | n/a | Non-resurrection logic: Deceased→Alive silently dropped; test pending |
| AT-15 | n/a | Determinism: queue drained OrderBy(NpcId); no RNG; test pending |
| AT-16 | n/a | No archetype JSON files modified; verified by scope review |
| AT-17 | n/a | Guard pattern adds `continue` at loop top; no existing logic changed; regression pending |
| AT-18 | n/a | Build: pending operator run |
| AT-19 | n/a | Tests: pending operator run |
| AT-20 | n/a | engine-fact-sheet.md regen: deferred to operator (ECSCli ai describe) |

All ATs marked n/a because operator will run the full test suite on return. Code logic has been verified by code review against the work packet specification.

## Files added

- `APIFramework/Components/LifeStateComponent.cs`
- `APIFramework/Systems/LifeState/LifeStateGuard.cs`
- `APIFramework/Systems/LifeState/LifeStateTransitionRequest.cs`
- `APIFramework/Systems/LifeState/LifeStateInitializerSystem.cs`
- `APIFramework/Systems/LifeState/LifeStateTransitionSystem.cs`
- `docs/c2-infrastructure/work-packets/p3-wip/WP-3.0.0-implementation-notes.md`

## Files modified

- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` — added `Choked`, `SlippedAndFell`, `StarvedAlone`, `Died` at end of enum (additive only)
- `APIFramework/Systems/MemoryRecordingSystem.cs` — added four new kinds to `IsPersistent`, all `true`
- `APIFramework/Config/SimConfig.cs` — added `LifeStateConfig` class and `LifeState` property
- `SimConfig.json` — added `"lifeState"` section
- `APIFramework/Core/SimulationBootstrapper.cs` — registered `LifeStateInitializerSystem` (PreUpdate) and `LifeStateTransitionSystem` (Cleanup); exposed `LifeStateTransitions` public property
- `APIFramework/Systems/InvariantSystem.cs` — deceased-invariant branch; added `CheckLifeStateComponent` and `CheckCauseOfDeath`
- `APIFramework/Systems/ActionSelectionSystem.cs` — Group A guard
- `APIFramework/Systems/WillpowerSystem.cs` — Group A guard
- `APIFramework/Systems/MoodSystem.cs` — Group A guard
- `APIFramework/Systems/BrainSystem.cs` — Group A guard
- `APIFramework/Systems/ScheduleSystem.cs` — Group A guard
- `APIFramework/Systems/StressSystem.cs` — Group A guard
- `APIFramework/Systems/SocialMaskSystem.cs` — Group A guard
- `APIFramework/Systems/MaskCrackSystem.cs` — Group A guard
- `APIFramework/Systems/WorkloadSystem.cs` — Group A guard
- `APIFramework/Systems/TaskGeneratorSystem.cs` — Group A guard (LINQ filter on assignment pool)
- `APIFramework/Systems/MovementSystem.cs` — Group A guard
- `APIFramework/Systems/Dialog/DialogContextDecisionSystem.cs` — Group A guard (speaker and listener)
- `APIFramework/Systems/DesireSystem.cs` — Group A guard
- `APIFramework/Systems/Coupling/LightingToDriveCouplingSystem.cs` — Group A guard
- `APIFramework/Systems/Movement/IdleMovementSystem.cs` — Group A guard
- `APIFramework/Systems/Movement/StepAsideSystem.cs` — Group A guard
- `APIFramework/Systems/Movement/MovementSpeedModifierSystem.cs` — Group A guard
- `APIFramework/Systems/Movement/PathfindingTriggerSystem.cs` — Group A guard
- `APIFramework/Systems/Movement/FacingSystem.cs` — Group A guard
- `APIFramework/Systems/RelationshipLifecycleSystem.cs` — Group A guard
- `APIFramework/Systems/Dialog/DialogFragmentRetrievalSystem.cs` — Group A guard
- `APIFramework/Systems/Dialog/DialogCalcifySystem.cs` — Group A guard
- `APIFramework/Systems/PhysiologyGateSystem.cs` — Group A guard
- `APIFramework/Systems/DriveDynamicsSystem.cs` — Group A guard
- `APIFramework/Systems/FeedingSystem.cs` — Group A guard
- `APIFramework/Systems/DrinkingSystem.cs` — Group A guard
- `APIFramework/Systems/SleepSystem.cs` — Group A guard
- `APIFramework/Systems/UrinationSystem.cs` — Group A guard (autonomous trigger)
- `APIFramework/Systems/DefecationSystem.cs` — Group A guard (autonomous trigger)
- `APIFramework/Systems/InteractionSystem.cs` — Group A guard
- `APIFramework/Systems/Spatial/ProximityEventSystem.cs` — Group A guard (observer loop)
- `APIFramework/Systems/Spatial/RoomMembershipSystem.cs` — Group A guard (preserve deceased's last room)
- `APIFramework/Systems/EnergySystem.cs` — Group B guard
- `APIFramework/Systems/MetabolismSystem.cs` — Group B guard
- `APIFramework/Systems/BladderFillSystem.cs` — Group B guard
- `APIFramework/Systems/BladderSystem.cs` — Group B guard
- `APIFramework/Systems/EsophagusSystem.cs` — Group B guard (on consumer entity in transit deposit block)
- `APIFramework/Systems/DigestionSystem.cs` — Group B guard
- `APIFramework/Systems/SmallIntestineSystem.cs` — Group B guard
- `APIFramework/Systems/LargeIntestineSystem.cs` — Group B guard
- `APIFramework/Systems/ColonSystem.cs` — Group B guard
- `APIFramework/Systems/BiologicalConditionSystem.cs` — Group B guard
- `APIFramework/Systems/Spatial/SpatialIndexSyncSystem.cs` — Group B guard (Deceased position frozen; index entry remains from prior tick)

## Diff stats

Approximately 50 files changed. Exact counts pending `git diff --stat`.

## Followups

- AT-01 through AT-19: all tests are authored by the operator per the deliverables table in WP-3.0.0; none were written in this session (operator instruction: skip tests, will integrate and confirm on return).
- AT-20: `ECSCli ai describe --out engine-fact-sheet.md` should be run by operator to regen with `LifeStateInitializerSystem` and `LifeStateTransitionSystem` listed.
- `RotSystem.cs`: no guard added — iterates food entities, not NPCs; food continues to rot by design. Guard would be a no-op.
- `SocialDriveAccumulator.cs`: no guard added — not an ISystem; guard lives at call site in the social drive system that calls it.
- Test files: deferred to operator per explicit instruction.
- `WorldStateDto.npcs[].lifeState` wire-format field: deferred to v0.5 schema bump packet (post-3.0.4), per WP non-goals.
