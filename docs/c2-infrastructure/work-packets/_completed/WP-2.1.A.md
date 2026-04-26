# WP-2.1.A Completion Note
## Action-Selection Scaffold

**Completed:** 2026-04-26  
**Branch:** `feat/wp-2.1.A`  
**All tests:** 512 passed, 0 failed

---

### Deliverables

| Artifact | Status |
|----------|--------|
| `APIFramework/Components/IntendedActionComponent.cs` | Created |
| `APIFramework/Systems/ActionSelectionSystem.cs` | Created |
| `APIFramework/Systems/Dialog/DialogContextDecisionSystem.cs` | Modified (Path 1 + fallback) |
| `APIFramework/Core/SimulationBootstrapper.cs` | Modified (ActionSelectionSystem registered at Cognition phase) |
| `APIFramework/Core/SystemPhase.cs` | Modified (added `Cognition = 30`) |
| `APIFramework/Config/SimConfig.cs` | Modified (added `ActionSelectionConfig` class + property) |
| `SimConfig.json` | Modified (added `actionSelection` section) |
| `APIFramework.Tests/Systems/ActionSelectionSystemTests.cs` | Created (AT-01–AT-03, AT-06) |
| `APIFramework.Tests/Systems/ApproachAvoidanceInversionTests.cs` | Created (AT-04, AT-05) |
| `APIFramework.Tests/Systems/SuppressionEventEmissionTests.cs` | Created (AT-07) |
| `APIFramework.Tests/Systems/ActionSelectionDeterminismTests.cs` | Created (AT-10) |
| `APIFramework.Tests/Integration/ActionSelectionToDialogTests.cs` | Created (AT-08) |
| `APIFramework.Tests/Integration/ActionSelectionToMovementTests.cs` | Created (AT-09) |

---

### Architecture decisions

**Candidate struct inlined:** `Candidate` is a private struct inside `ActionSelectionSystem.cs` (no separate file needed at this scope).

**Phase placement:** `Cognition = 30` inserted between `Condition = 20` and `Behavior = 40`. ActionSelectionSystem runs after `DriveDynamicsSystem` and before `WillpowerSystem`, `DialogContextDecisionSystem`, and the movement stack.

**Deterministic ordering:** NPCs processed by ascending `EntityIntId`. Within each NPC, candidates are sorted by `(DriveOrdinal, TargetEntityId)` before the cap is applied. Tiny seeded jitter (`1e-4 × NextDouble()`) breaks weight ties deterministically.

**Flee entity management:** For Avoid intents, ActionSelectionSystem creates an ephemeral entity positioned at the flee point (NPC position + avoidStandoffDistance × flee direction). The entity gets a new Guid each tick, forcing PathfindingTriggerSystem to recompute. Stale flee entities are destroyed at end of Update.

**DialogContextDecisionSystem two-path:** Path 1 reads `IntendedActionComponent.Kind == Dialog` and maps `DialogContextValue → corpus context string` via `MapContextValue()`. Path 2 (existing heuristic) fires only when Path 1 doesn't apply. All WP-1.10.A tests remain green.

**Suppression event:** At most one `SuppressionTick` per NPC per tick — the highest-suppressed loser only. Condition: `rawPush > winner.Weight AND rawPush - winner.Weight < suppressionEpsilon`.

---

### Candidate-enumeration table (committed)

| Drive | InhibitionClass gating | Kind | DialogContextValue |
|-------|------------------------|------|--------------------|
| Irritation | Confrontation | Dialog | LashOut |
| Irritation | InterpersonalConflict | Dialog | Complain |
| Affection | Vulnerability | Dialog | Share |
| Affection | Vulnerability | Approach | — |
| Attraction | Vulnerability | Approach / Avoid¹ | — |
| Loneliness | Vulnerability | Approach | — |
| Loneliness | PublicEmotion | Dialog | Greet |
| Belonging | — | Linger | — |
| Suspicion | — | Linger | — |
| Trust | Vulnerability | Dialog | Share |
| Status | PublicEmotion | Dialog | Encourage |
| Status | PublicEmotion | Dialog | Acknowledge |

¹ Approach-avoidance inversion fires when `stake > inversionStakeThreshold` AND `inhibition > inversionInhibitionThreshold`.

---

### SimConfig defaults (survived)

| Key | Default | Notes |
|-----|---------|-------|
| `driveCandidateThreshold` | 60 | Drives below this do not enumerate candidates |
| `idleScoreFloor` | 0.20 | Idle wins when nothing else exceeds this |
| `inversionStakeThreshold` | 0.55 | Stake above this enables the approach-avoidance flip |
| `inversionInhibitionThreshold` | 0.50 | Inhibition above this (with stake check) completes the flip |
| `suppressionGiveUpFactor` | 0.30 | How much low-willpower leaks suppressed pushes |
| `suppressionEpsilon` | 0.10 | Closeness window that marks a candidate as actively suppressed |
| `suppressionEventMagnitudeScale` | 5 | Willpower cost scaling for suppression events |
| `personalityTieBreakWeight` | 0.05 | Per-point nudge from Conscientiousness / Openness |
| `maxCandidatesPerTick` | 32 | Hard cap on enumerated candidates per NPC per tick |
| `avoidStandoffDistance` | 4 | Cells the avoidance flee point is pushed by |

---

### Acceptance test results

- AT-01 `IntendedActionComponent` compiles, instantiates, round-trips equality: **PASS**
- AT-02 Irritation=80, coworker nearby → LashOut or Complain within 50 ticks: **PASS**
- AT-03 Attraction=80, Vulnerability=20, willpower=60 → Approach: **PASS**
- AT-04 Attraction=80, Vulnerability=80, willpower=60 → Avoid (inversion): **PASS**
- AT-05 Attraction=90, Vulnerability=80, willpower=5 → Approach (gate breaks): **PASS**
- AT-06 All drives ≤ threshold → Idle; no SocialDrivesComponent → no intent: **PASS**
- AT-07 Suppressed candidate → ≤ 1 SuppressionTick event, correct magnitude: **PASS**
- AT-08 IntendedAction(Dialog, LashOut) → DialogContextDecisionSystem → DialogFragmentRetrievalSystem → SpokenFragmentEvent: **PASS**
- AT-09 Approach → MovementTargetComponent set → PathfindingTriggerSystem produces path; Avoid → flee entity offset west → path leads away: **PASS**
- AT-10 Same seed, 5000 ticks → byte-identical intent streams; different seeds → different streams: **PASS**
- AT-11 All WP-1.x tests remain green: **PASS** (512 total, 0 failed)
- AT-12 Build warning count = 0: **PASS**
- AT-13 Full test suite green: **PASS**

---

### API mismatches resolved

**`ProximityComponent.NearbyEntityIds` does not exist.** WP-1.1.A shipped proximity as events + `ISpatialIndex` queries, not a property list. ActionSelectionSystem uses `ISpatialIndex.QueryRadius()` directly.

**`MovementTargetComponent.TargetEntityId` is `Guid`, not `int`.** Approach writes the target entity's `Guid`; Avoid writes a newly-created flee entity's `Guid` (ephemeral, new each tick so PathfindingTriggerSystem always recomputes).

---

### Deferred

- **WP-2.1.B** — Physiology-overridable-by-inhibition: `BlockedActionsComponent` veto on `EatingSystem` / `SleepSystem` / `BladderSystem`.
- **WP-2.2.A** — Schedule layer: per-archetype anchor intents that action-selection considers as low-priority candidates.
- **WP-2.3.A** — Memory recording: wire `NarrativeEventDetector` candidates into relationship history surfaces.
- **WP-2.1.C** — Archetype-specific scoring weights: per-archetype tuning table after playtest data exists.
- **Heuristic-path retirement** in `DialogContextDecisionSystem`: delete the WP-1.10.A fallback once action-selection is the sole producer in practice.
- **Action-selection telemetry projector**: DTO digest for design-time observability (future schema bump).
- **Distance-based stake falloff**: v0.1 uses binary proximity presence; smooth distance falloff is a tuning packet.
