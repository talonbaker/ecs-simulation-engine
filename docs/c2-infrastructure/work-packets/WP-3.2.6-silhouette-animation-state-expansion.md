# WP-3.2.6 — Silhouette Animation State Expansion

> **DO NOT DISPATCH UNTIL WP-3.1.B AND WP-3.2.1 ARE MERGED.**
> This packet expands the animator state machine shipped in WP-3.1.B with new states. Some states emit sound triggers via WP-3.2.1's bus.

**Tier:** Sonnet
**Depends on:** WP-3.1.B (animator skeleton), WP-3.2.1 (sound trigger bus for animation-driven sounds)
**Parallel-safe with:** WP-3.2.5 (per-archetype tuning)
**Timebox:** 110 minutes
**Budget:** $0.50

---

## Goal

WP-3.1.B's animator shipped six states: Idle, Walk, Sit, Talk, Panic, Sleep, Dead. The engine has many more behaviors that need visual differentiation. Eating, drinking, defecating-in-cubicle, sleeping-at-desk, working, crying — these all happen, and currently they read identically to Idle.

After this packet:

- 8 new animator states added: `Eating`, `Drinking`, `DefecatingInCubicle`, `SleepingAtDesk`, `Working`, `Crying`, `CoughingFit`, `Heimlich`.
- Each state has placeholder sprite frames + transitions to/from Idle/Walk.
- Animation transitions emit sound triggers (per 3.2.1) at the right moments — Chew per Eating-cycle, Slurp per Drinking-cycle, KeyboardClack per Working-cycle, Cough per Coughing-cycle.
- Per-archetype animation timing — Hermit's walk is slower; Climber's walk is faster; Old Hand's eating is slower (chews carefully).

This is content + integration polish.

---

## Reference files

- `docs/c2-infrastructure/work-packets/_completed/WP-3.1.B.md` — animator skeleton; 6 base states; chibi slot.
- `docs/c2-infrastructure/work-packets/WP-3.2.1-sound-trigger-bus.md` — sound triggers.
- `docs/c2-content/cast-bible.md` — silhouette / animation per-archetype cues.
- `docs/c2-content/ux-ui-bible.md` §3.8 — emotional iconography.
- `APIFramework/Components/MetabolismComponent.cs`, `EnergyComponent.cs`, `BladderComponent.cs`, `ColonComponent.cs` — physiology states.
- `APIFramework/Components/IntendedActionComponent.cs` — drives state transitions.
- `APIFramework/Components/MoodComponent.cs` — Sad / Grief drives Crying.
- `ECSUnity/Assets/Scripts/Animation/NpcAnimatorController.cs` (from 3.1.B) — extended in this packet.

---

## Non-goals

- Do **not** ship final animation art. Placeholder sprites; final art is content/art-pipeline.
- Do **not** modify engine state surfaces. Animator reads from existing components.
- Do **not** add new chibi-emotion overlays — those land in 3.1.E or content packets.
- Do **not** retry, recurse, or self-heal.

---

## Design notes

### New animator states

| State | Trigger condition | Visual | Sound trigger emission |
|:---|:---|:---|:---|
| Eating | `IntendedAction.Kind == Eat` AND in proximity of food | Body sits/stands at table; arm-to-mouth cycle | `Chew` per cycle |
| Drinking | `IntendedAction.Kind == Drink` AND has liquid container | Body holds container, tilt-back motion | `Slurp` per cycle |
| DefecatingInCubicle | `IntendedAction.Kind == Defecate` AND no available bathroom path | Body sits at desk in awkward pose; longer than Sit | (No sound — silent shame) |
| SleepingAtDesk | `Energy < 25` AND `Activity == AtDesk` AND no scheduled sleep | Body slumps at desk; chibi SleepZ slot active | (No sound) |
| Working | `IntendedAction.Kind == Work` AND `Activity == AtDesk` | Body sits at desk; subtle keyboard-typing arm motion | `KeyboardClack` per cycle |
| Crying | `MoodComponent.GriefLevel ≥ 0.7` | Body slumps, hand-to-face, occasional shudder | `Sigh` periodically |
| CoughingFit | `IsChokingTag` (early phase) OR `BiologicalCondition.IsSick` | Body bent over, hand to mouth, repeated cough motion | `Cough` per cycle |
| Heimlich | NPC executing `IntendedAction.Kind == Rescue` AND `RescueKind == Heimlich` | Behind-victim hugging-chest motion | `Cough` (from victim) interleaved |

### Per-archetype animation timing

`archetype-animation-timing.json`:

```jsonc
{
  "schemaVersion": "0.1.0",
  "archetypeAnimationTiming": [
    {"archetype": "the-old-hand",        "walkSpeedMult": 0.85, "eatSpeedMult": 0.80, "talkGesturalRate": 0.90},
    {"archetype": "the-newbie",          "walkSpeedMult": 1.15, "eatSpeedMult": 1.20, "talkGesturalRate": 1.25},
    {"archetype": "the-climber",         "walkSpeedMult": 1.20, "eatSpeedMult": 1.10, "talkGesturalRate": 1.15},
    {"archetype": "the-hermit",          "walkSpeedMult": 0.80, "eatSpeedMult": 0.95, "talkGesturalRate": 0.70}
  ]
}
```

Animation playback rate multiplied by these per-NPC.

### Animator state machine

Unity Animator Controller with all 14 states (6 from 3.1.B + 8 new). Transitions:
- Idle/Walk → specific state when intent changes.
- Any state → Idle when intent clears.
- Hard transitions for emergency states: any → Panic (when IsChokingTag set), any → Dead (when LifeState = Deceased).

Transition latency: ~0.2 seconds for soft transitions; 0 for emergency.

### Sound emission integration

Each state has `OnEnter`, `OnLoop`, `OnExit` hooks. Sound triggers fire at appropriate moments:

```csharp
public sealed class AnimationSoundTriggerEmitter : MonoBehaviour
{
    [SerializeField] EngineHost _host;
    SoundTriggerBus _soundBus;

    public void OnEatingCycle()
    {
        var npc = GetComponent<NpcSilhouetteInstance>();
        var pos = npc.transform.position;
        _soundBus.Emit(SoundTriggerKind.Chew, npc.EntityId, pos.x, pos.z, 0.6f, _host.Engine.Clock.CurrentTick);
    }
}
```

Engine-side emit takes precedence; animator emits only when engine hasn't emitted in last N ticks (host de-duplicates).

### Tests

- `EatingStateTransitionTests.cs` — set `IntendedAction.Kind = Eat` → animator state Eating within 0.2s.
- `DrinkingStateTransitionTests.cs`, `DefecatingInCubicleTransitionTests.cs`, `SleepingAtDeskTransitionTests.cs`, `WorkingStateTransitionTests.cs`, `CryingStateTransitionTests.cs`, `CoughingFitTransitionTests.cs`, `HeimlichTransitionTests.cs`.
- `EmergencyTransitionTests.cs` — any → Panic when IsChokingTag set; any → Dead when Deceased.
- `AnimationSoundTriggerEatChewTests.cs` — Eating state emits Chew at expected cadence.
- `AnimationSoundTriggerWorkClackTests.cs` — Working state emits KeyboardClack.
- `PerArchetypeAnimationTimingTests.cs` — Old Hand walks slower; Newbie eats faster.
- `AnimationStateDeterminismTests.cs` — same engine state two seeds → same animator state sequence.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSUnity/Assets/Scripts/Animation/NpcAnimatorController.cs` (modified) | New states + transitions. |
| code | `ECSUnity/Assets/Scripts/Animation/AnimationSoundTriggerEmitter.cs` | Sound emission per state cycle. |
| code | `ECSUnity/Assets/Scripts/Animation/AnimationTimingCatalog.cs` | Per-archetype timing lookup. |
| asset | `ECSUnity/Assets/Animations/NpcAnimator.controller` (modified) | Unity asset with new states. |
| asset | `ECSUnity/Assets/Sprites/Silhouettes/States/*.png` | Placeholder sprites for new states. |
| asset | `ECSUnity/Assets/Settings/AnimationTimingCatalog.asset` | Loaded at runtime. |
| data | `docs/c2-content/animation/archetype-animation-timing.json` | Per-archetype timing. |
| test | (~13 test files) | Comprehensive coverage. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.2.6.md` | Completion note. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | All 8 new animator states defined; transitions wired. | edit-mode test |
| AT-02 | Eat intent → animator state Eating within 0.2s. | play-mode test |
| AT-03 | Drink intent → Drinking. | play-mode test |
| AT-04 | Bladder full + no bathroom path → DefecatingInCubicle. | play-mode test |
| AT-05 | Energy < 25 + at desk → SleepingAtDesk. | play-mode test |
| AT-06 | Work intent + at desk → Working. | play-mode test |
| AT-07 | High grief → Crying. | play-mode test |
| AT-08 | IsChokingTag → CoughingFit. | play-mode test |
| AT-09 | Rescue + RescueKind.Heimlich → Heimlich on rescuer. | play-mode test |
| AT-10 | Eating state emits Chew at configured cadence. | integration test |
| AT-11 | Working state emits KeyboardClack at configured cadence. | integration test |
| AT-12 | Old Hand walks slower than Newbie (configurable timing). | integration test |
| AT-13 | Determinism: same engine state two seeds → same animator state sequence. | integration test |
| AT-14 | Performance gate from 3.1.A still passes: 30 NPCs at 60 FPS with all new animation states active. | play-mode test |
| AT-15 | All Phase 0/1/2/3.0.x/3.1.x and prior 3.2.x tests stay green. | regression |
| AT-16 | `dotnet build` warning count = 0; `dotnet test` all green. | build + test |
| AT-17 | Unity Test Runner: all tests pass. | unity test runner |

---

## Followups (not in scope)

- Final animation art. Placeholder ships; final hand-drawn pixel art is content/art-pipeline.
- More state subdivision (walking-while-distressed; sitting-while-bored; eating-while-talking). Future polish.
- Per-NPC animation variants. Future polish.
- Inverse kinematics. Out of scope.
- Cross-state blending. Future polish.
- Per-archetype voice profile. UX bible §3.7; future packet.


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
