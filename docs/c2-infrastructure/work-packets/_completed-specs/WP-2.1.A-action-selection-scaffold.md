# WP-2.1.A — Action-Selection Scaffold

**Tier:** Sonnet
**Depends on:** WP-1.4.A (social components + WillpowerEventQueue), WP-1.1.A (spatial + proximity events), WP-1.10.A (dialog corpus + retrieval pipeline)
**Parallel-safe with:** any packet that does not touch `ActionSelectionSystem`, `DialogContextDecisionSystem`, or `MovementTargetComponent`
**Timebox:** 150 minutes
**Budget:** $0.60

---

## Goal

Ship `ActionSelectionSystem` — the seam between social state and observable behaviour. Per tick, for each NPC, the system reads drives + willpower + inhibitions + nearby-NPC context and writes a single `IntendedActionComponent` describing what the NPC has decided to do this tick. Existing downstream systems pick the intent up and execute it: the dialog stack consumes `DialogIntent`, the movement stack consumes `MovementIntent`, the idle case is a no-op.

This is the layer the action-gating bible's "drives are necessary but not sufficient for action" thesis lives in. Without it, Phase-1's drives drift, willpower depletes during sleep, and inhibitions decay — but no NPC ever *acts*. After this packet, an NPC with elevated `irritation` near a coworker emits a lash-out dialog intent; an NPC with elevated `attraction` toward an in-proximity target emits an approach intent when their `vulnerability` inhibition is low and an *avoidance* intent when the same inhibition is high. The approach-avoidance inversion is the shape-defining test.

The packet is deliberately scoped to the social/dialog/movement axes. **Physiology-overridable-by-inhibition is deferred to WP-2.1.B** — that path needs a `BlockedActionsComponent` veto on the existing physiology systems and is its own focused change. Schedule integration (WP-2.2.A) and memory recording (WP-2.3.A) are likewise out of scope; this scaffold is what they all hang on.

---

## Reference files

- `docs/c2-content/action-gating.md` — **read this first.** The entire packet implements the design described there. Section "How the systems compose" is the reference equation. The approach-avoidance inversion section is the AT-04 design source.
- `docs/c2-content/cast-bible.md` — drive catalog, register list, archetype hints. Does not introduce per-archetype tuning here, but the system must correctly read the existing components archetype generation populates.
- `docs/c2-content/dialog-bible.md` — context enum (lash-out, share, deflect, etc.) and the consumer side of `DialogIntent`. Action-selection produces the `context` field; the existing `DialogContextDecisionSystem` consumes it.
- `docs/c2-content/world-bible.md` — gameplay loop and emote-more-than-speak tone. Most NPC ticks should produce *idle* intents, not dialog. Tune scoring thresholds so observable behaviour reads as a real office, not a chattering crowd.
- `docs/c2-infrastructure/00-SRD.md` §8.1 (no runtime LLM), §8.5 (social state is first-class).
- `APIFramework/Components/SocialDrivesComponent.cs` — the eight-drive component. Read `Current` for push, `Baseline` for stake context (a drive that's `Current=80, Baseline=80` is a chronically-elevated state, not a fresh shock).
- `APIFramework/Components/WillpowerComponent.cs` — `Current` is the gate; `Baseline` is the stat-like reference.
- `APIFramework/Components/InhibitionsComponent.cs` — list of `Inhibition { Class, Strength, Awareness }`. Awareness is *not* read by selection (hidden inhibitions block exactly the same as known ones); it exists for future surface presentation.
- `APIFramework/Components/PersonalityComponent.cs` — used for tie-breaking only (Conscientiousness biases toward routine, Openness biases toward novel).
- `APIFramework/Components/RelationshipComponent.cs` — relationships as entities (not properties). Action-selection reads these to enumerate "approach toward NPC X" candidates only when a relationship exists or the two are in proximity.
- `APIFramework/Components/ProximityComponent.cs` — the per-NPC list of nearby NPCs maintained by `ProximityEventSystem` (WP-1.1.A). This is where action-selection finds candidate targets.
- `APIFramework/Systems/WillpowerEventQueue.cs` + `WillpowerEventSignal.cs` — action-selection becomes the missing producer. When it chooses to suppress a high-drive candidate (drive push exceeded by inhibition cost + willpower draw), it pushes a `SuppressionTick` event proportional to the suppressed drive's magnitude. This closes the loop WP-1.4.A reserved.
- `APIFramework/Systems/DialogContextDecisionSystem.cs` (modified) — currently uses heuristics from drive deltas + proximity events to derive context. After this packet, it reads from `IntendedActionComponent` when present and falls back to the existing heuristic only when no intent is set (transitional). The existing fallback path stays green for tests that don't yet exercise action-selection.
- `APIFramework/Components/MovementTargetComponent.cs` — already exists. Action-selection writes a target into it for `Approach` / `Avoid` intents. Movement systems already consume this.
- `APIFramework/Systems/PathfindingTriggerSystem.cs` — the consumer of `MovementTargetComponent`. Read to confirm the existing wiring; no modification expected in this packet.
- `APIFramework/Core/SimulationBootstrapper.cs` — system registration site. Action-selection runs *after* `DriveDynamicsSystem` (so it sees the up-to-date drive vector) and *before* `WillpowerSystem`, `DialogContextDecisionSystem`, and the movement systems (so its intent + emitted suppression events are visible the same tick).
- `APIFramework/Core/SystemPhase.cs` — the phase enum. Likely needs a new `Cognition` phase between `Social` and `Behaviour`, or action-selection lands in an existing late-`Social` slot. Sonnet picks the cleanest seam without adding more than one new phase value.
- `APIFramework/Core/SeededRandom.cs` — RNG source. All tie-breaking and weighted picks go through this. No `System.Random`.
- `SimConfig.json` — the new `actionSelection` tuning section lives here, not in code.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.4.A.md`, `WP-1.10.A.md`, `WP-1.1.A.md` — completion notes for the three direct dependencies. Confirm component shapes and consumer surfaces against what those packets actually shipped (not just what their packets specified).

---

## Non-goals

- Do **not** implement physiology-overridable-by-inhibition. No `BlockedActionsComponent`, no veto path through `EatingSystem` / `SleepSystem` / `BladderSystem`. The action-gating bible commits to it; WP-2.1.B will land it. If the Sonnet feels itself starting to reach into physiology systems, stop — that's out of scope.
- Do **not** add a schedule layer. NPCs without schedules look idle most of the day in this packet's output. That is the correct intermediate state.
- Do **not** record memories. The narrative event detector (WP-1.6.A) will eventually feed a memory recorder; this packet does not write to relationship history surfaces.
- Do **not** install or strengthen inhibitions in response to in-game events. Inhibition installation is its own follow-up packet (timing TBD).
- Do **not** introduce per-archetype tuning. The catalog of candidates and their scoring weights are global at v0.1. Archetype-specific weighting (The Climber prefers `flirt` candidates; The Hermit prefers `brush-off`) is a tuning packet that follows playtest data.
- Do **not** modify the dialog corpus, the dialog retrieval system, or the calcify mechanism. The dialog bible's `DialogContextDecisionSystem` is the only dialog-stack file this packet touches, and only to read `IntendedActionComponent` when present.
- Do **not** modify any file under `Warden.Contracts/`, `Warden.Telemetry/`, or `docs/c2-infrastructure/schemas/`. No schema bump. Action intents are engine-internal at v0.1; a future projector packet will surface a digest if telemetry consumers need one.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or self-heal on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (SRD §8.1.)
- Do **not** include any test that depends on `DateTime.Now` or `System.Random`. All tests use seeded RNG and fixed tick counts. Determinism is enforced.
- Do **not** modify `WillpowerEventSignal` or `WillpowerEventQueue` shapes. Action-selection is a *producer* that pushes existing signal types; do not introduce new signal kinds in this packet.

---

## Design notes

### The intent shape

A single new component on the NPC entity:

```csharp
public readonly record struct IntendedActionComponent(
    IntendedActionKind Kind,
    int TargetEntityId,            // 0 when not applicable
    DialogContextValue Context,    // None when Kind != Dialog
    int IntensityHint              // 0–100, used by consumers as a soft modulator
);

public enum IntendedActionKind
{
    Idle,        // explicit; the NPC chose to do nothing observable this tick
    Dialog,      // emit a fragment via the existing dialog stack
    Approach,    // move toward TargetEntityId
    Avoid,       // move away from TargetEntityId (approach-avoidance inversion)
    Linger       // hold position; consumers may modulate facing or idle jitter
}
```

`DialogContextValue` is a new enum mirroring the dialog bible's context list (`LashOut`, `Share`, `Flirt`, `Deflect`, `BrushOff`, `Acknowledge`, `Greet`, `Refuse`, `Agree`, `Complain`, `Encourage`, `Thanks`, `Apologise`, `None`). It lives next to `IntendedActionComponent` because action-selection is its only producer.

The component is overwritten each tick. There is no queue and no history; consumers read the latest snapshot. The previous tick's intent is gone. (If a future system wants intent history for memory, it observes during the consumer phase and persists what it cares about — not action-selection's job.)

### Candidate enumeration

For each NPC the system enumerates candidates from three sources:

1. **Drive-driven candidates.** For each of the eight social drives whose `Current` exceeds `SimConfig.actionSelection.driveCandidateThreshold` (default 60), produce one or more candidate actions: `Irritation` → `Dialog(LashOut)` and `Dialog(Complain)`; `Affection` → `Dialog(Share)` and `Approach`; `Attraction` → `Approach` and (for high stake) `Avoid`; `Loneliness` → `Approach` toward nearest known relationship or `Dialog(Greet)`; `Belonging` → `Linger` near group; `Suspicion` → `Linger` with facing-target; `Trust` → `Dialog(Share)`; `Status` → `Dialog(Encourage)` toward subordinate or `Dialog(Acknowledge)` toward superior. Mappings are tabular; the table lives in code as a `static readonly` because changing it is a code change, not a config change.

2. **Proximity-driven candidates.** Each NPC in `ProximityComponent.NearbyEntityIds` becomes a potential target for any drive-driven candidate that needs one. A candidate without a target (e.g., `Linger`) does not multiply by proximity.

3. **Idle candidate.** Always present, always low-weight. Wins when nothing else exceeds threshold.

The cardinality stays bounded: at most `8 drives × 3 candidates × max(proximity_count, 1)` per NPC, capped at 32 candidates per tick by `SimConfig.actionSelection.maxCandidatesPerTick`.

### Scoring

For each candidate, the score is:

```
push       = drive.Current / 100.0
inhibition = max strength / 100 across inhibitions whose Class matches the candidate's action class, else 0
willpower  = current.Willpower / 100.0
stake      = proximity_factor * observability_factor   // both 0..1, see below

// approach-avoidance inversion
if (candidate.Kind == Approach && stake > inversionStakeThreshold && inhibition > inversionInhibitionThreshold)
    candidate.Kind = Avoid

// raw weight
weight = push * (1 - inhibition) + (1 - willpower) * push * suppressionGiveUpFactor
```

The `suppressionGiveUpFactor` term is the willpower break-the-gate behaviour: when willpower is low, the *suppressed* push leaks through and raises the weight. This is what produces the "the argument finally erupts" moment after sustained suppression.

Random tie-breaking with `SeededRandom`. Tie-breaking that survives ε via personality:
- Conscientiousness > 0 nudges weights toward `Linger` and `Idle` (the routine choice).
- Openness > 0 nudges weights toward `Dialog(Share)` and `Approach` (the novel choice).
- The nudge magnitude is `personalityTieBreakWeight` (default 0.05) times the trait value.

### Stake = proximity × observability

`proximity_factor` rises as the candidate target gets nearer. Distance comes from the existing `SpatialIndex` query — `NearbyEntityIds` already filters to a configured radius; this packet uses presence in that list as the binary signal at v0.1 and reserves a smoother distance falloff for tuning.

`observability_factor` rises with how visible the NPC is. Two inputs at v0.1: (1) the `RoomIllumination.Current` value at the NPC's room (high illumination = high observability), (2) the count of nearby NPCs (more witnesses = higher observability). Capped at 1.0. The aesthetic bible's "noticeability" open question is intentionally not committed here — this is a deliberate v0.1 sketch that playtest data will tune.

### Suppression event emission (closing the WillpowerEventQueue loop)

When a candidate's `weight * (1 - inhibition)` exceeds the winning candidate's weight by less than `suppressionEpsilon` (default 0.10), action-selection treats the loser as "actively suppressed" and pushes one `SuppressionTick` event into `WillpowerEventQueue` with magnitude proportional to the suppressed drive's `Current / 100 * suppressionEventMagnitudeScale` (default 5).

This is the missing producer side of the willpower system: action-selection chooses to defer a high-drive action, the willpower cost gets paid, and over time the gate weakens. Without this hook, willpower only depletes via the sleep regen path — the wrong direction.

At most one suppression event per NPC per tick (the highest-suppressed loser only). Higher-fidelity multi-event suppression is tunable later; v0.1 keeps the queue shallow.

### Idle is a real choice

Most ticks should emit `Idle`. The world bible's "emote more than speak" commitment requires that the *visible* output rate of dialog and movement intents stays sparse — the player sees the office breathing, not chattering. Tune `driveCandidateThreshold` and `idleScoreFloor` so the smoke-mission run produces dialog intents on the order of one per NPC per several minutes of game time, not every tick.

### Modifying `DialogContextDecisionSystem`

The existing `DialogContextDecisionSystem` (WP-1.10.A) derives context heuristically from drive deltas + proximity events. After this packet it has two paths:

1. **If the NPC has an `IntendedActionComponent` with `Kind == Dialog`,** use `IntendedAction.Context` directly and skip the heuristic.
2. **Otherwise,** fall back to the existing heuristic (preserves all WP-1.10.A tests).

This is the cleanest seam — it does not delete the heuristic path, so existing tests stay green, but action-selection takes over once it's wiring inputs in. Long-term the heuristic path will be retired; that's a follow-up.

### Why no movement-system modification

`MovementTargetComponent` is already the consumer surface. Action-selection writes the target entity's position into `MovementTargetComponent` for `Approach` and the inverse direction for `Avoid` (computed as the NPC's current position pushed away from the target by `avoidStandoffDistance`, default 4 cells). The pathfinding system reads `MovementTargetComponent` already; no modification needed in this packet. Test that the wiring works end-to-end via integration tests.

### Determinism

Every weighted pick goes through `SeededRandom`. The candidate enumeration order is deterministic (stable sort by `(driveOrdinal, targetEntityId)`). The same NPC + same world state + same seed produces byte-identical intent across runs. AT-10 enforces this over 5000 ticks.

### SimConfig additions

```jsonc
{
  "actionSelection": {
    "driveCandidateThreshold": 60,                     // drives below this don't enumerate candidates
    "idleScoreFloor": 0.20,                            // idle wins when nothing else exceeds this
    "inversionStakeThreshold": 0.55,                   // stake above this enables approach-avoidance flip
    "inversionInhibitionThreshold": 0.50,              // matched inhibition above this completes the flip
    "suppressionGiveUpFactor": 0.30,                   // how much low-willpower leaks suppressed pushes
    "suppressionEpsilon": 0.10,                        // closeness that marks a candidate as actively suppressed
    "suppressionEventMagnitudeScale": 5,               // willpower cost scaling for suppression events
    "personalityTieBreakWeight": 0.05,                 // per-point nudge from Conscientiousness / Openness
    "maxCandidatesPerTick": 32,                        // hard cap on enumerated candidates per NPC
    "avoidStandoffDistance": 4                         // cells the avoidance target is pushed by
  }
}
```

Defaults are starting points; tuning happens in a balance-validation packet that runs Haikus over scenarios (the cast-validate path, once the architectural mismatch from PHASE-1-HANDOFF §4 is fixed).

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/IntendedActionComponent.cs` | The record + `IntendedActionKind` and `DialogContextValue` enums. |
| code | `APIFramework/Systems/ActionSelectionSystem.cs` | The system: enumerate, score, write intent, push suppression events. |
| code | `APIFramework/Systems/ActionSelectionCandidate.cs` | Internal candidate record (struct in the system file is also fine; Sonnet picks). |
| code | `APIFramework/Systems/DialogContextDecisionSystem.cs` (modified) | Read `IntendedActionComponent` when present; fall back to existing heuristic otherwise. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `ActionSelectionSystem`. Phase placement: after `DriveDynamicsSystem`, before `WillpowerSystem` and `DialogContextDecisionSystem` and the movement systems. |
| code | `APIFramework/Core/SystemPhase.cs` (modified, only if needed) | Add a `Cognition` phase value if no existing slot fits cleanly. |
| code | `APIFramework/Config/SimConfig.cs` (modified) | `ActionSelectionConfig` class + property. |
| code | `SimConfig.json` (modified) | Add `actionSelection` section per Design notes. |
| code | `APIFramework.Tests/Systems/ActionSelectionSystemTests.cs` | Per-AT unit tests below. Uses seeded RNG and synthetic NPC + proximity scaffolding. |
| code | `APIFramework.Tests/Systems/ApproachAvoidanceInversionTests.cs` | The shape-defining test for approach-avoidance flip. Three ladder scenarios (low / mid / high `vulnerability` strength) verified for `Approach` → `Approach` → `Avoid` outputs. |
| code | `APIFramework.Tests/Systems/ActionSelectionDeterminismTests.cs` | 5000-tick byte-identical intent-stream test across two seeds. |
| code | `APIFramework.Tests/Integration/ActionSelectionToDialogTests.cs` | NPC produces `IntendedAction(Dialog, LashOut)` → existing `DialogContextDecisionSystem` consumes it → `DialogFragmentRetrievalSystem` selects a register-appropriate lash-out fragment. |
| code | `APIFramework.Tests/Integration/ActionSelectionToMovementTests.cs` | NPC produces `IntendedAction(Approach, target)` → `MovementTargetComponent` is set → existing `PathfindingTriggerSystem` produces a path toward the target. Also: `Avoid` produces a target away from the source. |
| code | `APIFramework.Tests/Systems/SuppressionEventEmissionTests.cs` | When a candidate is actively suppressed, exactly one `SuppressionTick` event lands in `WillpowerEventQueue` with the expected magnitude. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-2.1.A.md` | Completion note. Standard template. Explicitly enumerate (a) the candidate-enumeration table the Sonnet committed to, (b) the SimConfig defaults that survived, (c) what's deferred (physiology integration, schedule, memory, archetype-specific tuning). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `IntendedActionComponent` and its enums compile, instantiate, and round-trip through equality. | unit-test |
| AT-02 | NPC with `Irritation.Current = 80`, willpower 50, no inhibitions, one coworker in proximity → emits `IntendedAction(Dialog, LashOut)` (or `Dialog(Complain)` per the table) within 50 ticks. | unit-test |
| AT-03 | NPC with `Attraction.Current = 80` toward in-proximity target, `Vulnerability` inhibition strength 20, willpower 60 → emits `IntendedAction(Approach, target)`. | unit-test |
| AT-04 | Same NPC, `Vulnerability` inhibition strength 80 instead → emits `IntendedAction(Avoid, target)`. *Approach-avoidance inversion.* | unit-test |
| AT-05 | NPC with `Attraction.Current = 90` toward in-proximity target, `Vulnerability` strength 80, willpower 5 → the gate breaks; emits `Approach` (the suppressed action leaks through). | unit-test |
| AT-06 | NPC with all drives `Current ≤ driveCandidateThreshold` → emits `IntendedAction(Idle)`. | unit-test |
| AT-07 | When a candidate is actively suppressed (within `suppressionEpsilon` of the winner but inhibition-blocked), exactly one `SuppressionTick` event is pushed to `WillpowerEventQueue` with magnitude `≈ drive.Current/100 * suppressionEventMagnitudeScale`. | unit-test |
| AT-08 | Integration: NPC with `IntendedAction(Dialog, LashOut)` → `DialogContextDecisionSystem` reads the intent → `DialogFragmentRetrievalSystem` picks a register-appropriate lash-out fragment. | integration-test |
| AT-09 | Integration: NPC with `IntendedAction(Approach, target)` → `MovementTargetComponent` is set to the target's position → `PathfindingTriggerSystem` produces a non-empty path. Same setup with `Avoid` → target is offset by `avoidStandoffDistance` away from target's position. | integration-test |
| AT-10 | Determinism: two runs with the same seed over 5000 ticks produce byte-identical `IntendedActionComponent` snapshots at every tick boundary. | unit-test |
| AT-11 | Existing tests stay green: `WP-1.4.A` social tests, `WP-1.10.A` dialog tests (heuristic fallback path), `WP-1.1.A` proximity tests, `WP-1.3.A` movement tests. | build + unit-test |
| AT-12 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-13 | `dotnet test ECSSimulation.sln` — every existing test stays green; new tests pass. | build |

---

## Followups (not in scope)

- **WP-2.1.B — Physiology-overridable-by-inhibition.** Add a `BlockedActionsComponent` (or extend `IntendedActionComponent` with a vetoed-classes set); thread a one-line check into `EatingSystem` / `SleepSystem` / `BladderSystem` so high physiological drive plus a matching inhibition produces the override the action-gating bible commits to (Sally not eating at hunger 120% with `bodyImageEating: 90`). ~$0.25.
- **WP-2.2.A — Schedule layer.** Per-archetype schedule shapes from the archetype catalog. Schedule writes lower-priority `IntendedAction(Approach, anchor)` candidates that action-selection considers; high-drive social candidates can override schedules under sufficient pressure. The "Donna at her desk 8am–10:30, breakroom 10:30–10:45" loop.
- **WP-2.3.A — Memory recording.** Wire `NarrativeEventDetector` (WP-1.6.A) candidates into per-relationship history surfaces. Currently the schema reserves `historyEventIds[]`; nothing populates it.
- **WP-2.1.C — Archetype-specific scoring weights.** Once playtest data exists, add a per-archetype tuning table (The Climber prefers `flirt`, The Hermit prefers `brush-off`).
- **Heuristic-path retirement in `DialogContextDecisionSystem`.** Once action-selection is the sole producer in practice, delete the WP-1.10.A heuristic fallback. Pending playtest evidence that the heuristic is genuinely unused.
- **Cast-validate orchestrator fix** (PHASE-1-HANDOFF §4) — required before the tuning packet runs Haikus over balance scenarios.
- **Action-selection telemetry projector update.** A small DTO digest of the most recent intent per NPC, surfaced through a future `Warden.Telemetry` schema bump for design-time observability. Not needed at v0.1.
