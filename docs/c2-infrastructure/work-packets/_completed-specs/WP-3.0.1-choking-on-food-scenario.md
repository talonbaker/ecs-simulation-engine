# WP-3.0.1 — Choking-on-Food Scenario

> **DO NOT DISPATCH UNTIL WP-3.0.0 IS MERGED.**
> This packet's first line of code calls `LifeStateTransitionSystem.RequestTransition`, a class that doesn't exist until WP-3.0.0 lands. Dispatching prior to merge will fail-closed at build time per SRD §4.1 — no money wasted, but a wasted dispatch slot.

**Tier:** Sonnet
**Depends on:** WP-3.0.0 (LifeStateComponent, LifeStateTransitionSystem, four new NarrativeEventKind values, LifeStateGuard), WP-2.1.B (BlockedActions / physiology vetoes), WP-1.6.A (narrative event bus)
**Parallel-safe with:** WP-3.0.2 (corpse handling — disjoint concerns; both subscribe to the death narrative), WP-3.0.3 (slip-and-fall — disjoint concerns)
**Timebox:** 120 minutes
**Budget:** $0.50

---

## Goal

The world's first concrete death. Mark sits at his desk eating a sandwich at 11:47 AM. He's exhausted (slept five hours), distracted (overdue task lit up his stress source counter this morning), and shoving the sandwich in faster than he should. The bolus is too big. It lodges. He can't breathe. He paws at his throat for ninety seconds. Donna is two cubicles away with headphones in. Frank is in a meeting on the top floor. Nobody walks by. By 11:49 AM, Mark's `LifeStateComponent.State` has flipped from `Alive → Incapacitated → Deceased(Choked)`. Nobody finds him until 1:00 PM, when Greg comes back from the IT closet to refill his thermos and notices Mark hasn't moved.

This packet ships the engine surface that produces that scenario. It's the proof of concept that everything Phase 1 and Phase 2 built — physiology, action-selection, schedule, willpower, stress, memory, narrative — interacts to produce a story. It tests every system at once.

This packet ships:

1. A new `IsChokingTag` and `ChokingComponent { long ChokeStartTick, int RemainingTicks, float BolusSize }` attached to a choking NPC for the duration of the choke event.
2. A `ChokingDetectionSystem` that, each tick, checks NPCs with `EsophagusTransitComponent` for choke conditions: bolus size above threshold AND eating-while-distracted (low energy OR high stress OR fast-eating mode), and emits a one-shot start.
3. A new `NarrativeEventKind.ChokeStarted` value (additive at end of enum) — emitted when an NPC begins to choke. Subscribers (witnesses, future rescue mechanic) receive it. Persistent: true.
4. The transition wiring: `ChokingDetectionSystem` calls `LifeStateTransitionSystem.RequestTransition(npc, Incapacitated, Choked)` at the moment of choke; `LifeStateComponent.PendingDeathCause = Choked` carries the cause through the budget countdown; on budget expiry, the transition system flips to `Deceased(Choked)` and emits `Choked` per WP-3.0.0's contract.
5. A `MoodComponent` panic spike on the choking NPC (mood reads `Panic` for the duration; existing facing system reads this and freezes facing forward).
6. SimConfig tuning surface: bolus size threshold, distraction thresholds, incapacitation budget, panic mood intensity.

After this packet, the choking-on-food scenario is reproducible end to end. A 1000-tick scripted scenario with one NPC, one sandwich, one set of low-energy + high-stress conditions, no proximity help: the NPC reliably dies of choking, witnesses (if any) record a persistent memory of the event, and the cause-of-death narrative chain fires correctly.

This packet is **engine-internal at v0.1.** No wire-format change. No orchestrator change. The cost ledger and reports are unaffected.

---

## Reference files

- `docs/c2-infrastructure/work-packets/WP-3.0.0-life-state-component-and-cause-of-death-events.md` — **read first.** This packet calls into WP-3.0.0's contract. `LifeStateGuard.IsAlive` is the early-return; `LifeStateTransitionSystem.RequestTransition` is the only valid death entry point; `PendingDeathCause` is the cause-carrier through the Incapacitated phase; `NarrativeEventKind.Choked` already exists.
- `docs/c2-content/world-bible.md` — the cubicle-12-Mark dynamic. The death this packet enables is the canonical first death the kickoff brief calls "the proof of concept that everything Phase 1 and Phase 2 built can produce a story."
- `docs/c2-content/aesthetic-bible.md` — proximity-as-witness rules; observability is essential (the choke is observable to a witness in conversation range, who records a persistent memory).
- `docs/c2-content/cast-bible.md` — archetypes to verify: at v0.1, no per-archetype choke biasing (deferred to followup). The Old Hand, the Newbie, all the others have the same baseline choke probability.
- `APIFramework/Components/LifeStateComponent.cs` (from WP-3.0.0) — read `PendingDeathCause`. **Set it to `CauseOfDeath.Choked` at the moment of incapacitation request.**
- `APIFramework/Systems/LifeState/LifeStateTransitionSystem.cs` (from WP-3.0.0) — `RequestTransition(npcId, LifeState.Incapacitated, CauseOfDeath.Choked)` is the entry point; the budget countdown and `Deceased` transition are entirely owned by 3.0.0; this packet does not modify either.
- `APIFramework/Systems/LifeState/LifeStateGuard.cs` (from WP-3.0.0) — `IsAlive` is the gate at the top of `ChokingDetectionSystem.Update`. Don't choke a deceased NPC; don't choke an already-incapacitated NPC.
- `APIFramework/Components/EsophagusTransitComponent.cs` — the existing component that tracks bolus-in-transit. Read `BolusSize` (or compute from referenced Bolus entity). The choke trigger reads this.
- `APIFramework/Components/BolusComponent.cs` — bolus entity holds size and composition. The packet does **not** modify this; it only reads.
- `APIFramework/Systems/EsophagusSystem.cs` — bolus advancement happens here. The choke detection runs *parallel* to esophagus advancement, not inside it (single-responsibility — esophagus advances, choking checks).
- `APIFramework/Systems/FeedingSystem.cs` — the entry point that puts a bolus into the esophagus. **Read for context only;** this packet does not modify it. The `eating-while-distracted` heuristic reads NPC state at the moment the bolus enters the esophagus (cached in `EsophagusTransitComponent` at insertion time).
- `APIFramework/Components/EnergyComponent.cs` — `Energy < chokingEnergyThreshold` is one of the distraction conditions.
- `APIFramework/Components/StressComponent.cs` — `AcuteLevel ≥ chokingStressThreshold` is another.
- `APIFramework/Components/SocialDrivesComponent.cs` — `Irritation ≥ chokingIrritationThreshold` is the third (frustrated eating).
- `APIFramework/Components/Tags.cs` — **add `IsChokingTag`** at end. Additive; coordinate with WP-3.0.4 (which adds `StructuralTag` and `MutableTopologyTag`) by appending after their additions if 3.0.4 has merged first; if not, add at the current end and expect a "keep both" merge.
- `APIFramework/Components/MoodComponent.cs` — the `Panic` mood (or whatever the existing engine names panic in `MoodKind`). If a `Panic` value doesn't exist, **add it** as an additive enum value; coordinate at merge.
- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` (modified) — add `ChokeStarted` at end. Additive only. **Critical:** this is the same file WP-3.0.0 added `Choked`, `SlippedAndFell`, `StarvedAlone`, `Died` to. After 3.0.0 merges, append `ChokeStarted` after them.
- `APIFramework/Systems/MemoryRecordingSystem.cs` (modified) — add `ChokeStarted` to `IsPersistent` switch returning `true`. Witnessing the start of a choke is remembered, even if rescue arrives.
- `APIFramework/Systems/Narrative/NarrativeEventBus.cs` — emit point.
- `APIFramework/Components/ProximityComponent.cs` — `ConversationRange` is the witness range. Same selection logic as WP-3.0.0's `FindClosestWitness`; reuse helper or duplicate (Sonnet picks; if duplicating, document in completion note).
- `APIFramework/Components/FacingComponent.cs` — choking NPC's facing freezes (the existing facing system already reads from `MoodComponent` and `Panic` mood freezes facing forward; verify in code).
- `APIFramework/Core/SimulationBootstrapper.cs` (modified) — register `ChokingDetectionSystem` at Cleanup phase, **after** `EsophagusSystem` (so the bolus has had its chance to advance) and **before** `LifeStateTransitionSystem` (so the request reaches the queue this tick). Conflict warning: 3.0.2 and 3.0.3 also touch this file. Resolution: "keep all".
- `APIFramework/Config/SimConfig.cs` (modified) — `ChokingConfig` class + property.
- `SimConfig.json` (modified) — `choking` section.
- `APIFramework.Tests/Systems/LifeState/` (from WP-3.0.0) — pattern for `ChokingDetectionSystemTests.cs`.

---

## Non-goals

- Do **not** implement a rescue mechanic (Heimlich, CPR, "another NPC notices and helps"). At v0.1, choke = death. The substrate (`PendingDeathCause` clearing on `RequestTransition(npc, Alive, Unknown)` from Incapacitated) is in place from 3.0.0; the *trigger* — what makes another NPC notice and act — is its own future packet. Document this clearly in the completion note as the most-important deferred follow-up.
- Do **not** add per-archetype choke biasing. The Old Hand may be more careful eaters; the Newbie may panic more. v0.1 ships uniform thresholds. JSON tuning surface is reserved for followup.
- Do **not** modify the existing eating pipeline (`FeedingSystem`, `EsophagusSystem`, `BolusComponent`) beyond reading. The choke detection is *parallel*, not inline.
- Do **not** add a coughing-fits sub-system. NPCs don't recover-by-coughing at v0.1 — they choke or they don't. Cough sound triggers (per the design philosophy memory) are emitted but they don't gate the death.
- Do **not** introduce homicide-by-choke. No external `RequestTransition(targetNpc, Deceased, Choked)` from another NPC is a valid v0.1 vector. Future packet, if ever.
- Do **not** modify `WorldStateDto`, `Warden.Telemetry`, `Warden.Orchestrator`, `Warden.Anthropic`, `ECSCli`. Engine project (`APIFramework`) and its tests only.
- Do **not** add UI / player-facing notification of the choke at engine layer. Notification surface is a UX/UI bible concern handled by 3.1.E. (When that lands, the `ChokeStarted` narrative is the trigger the host listens to.)
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM call anywhere. (SRD §8.1.)
- Do **not** introduce a NuGet dependency.
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing. Use `SeededRandom` and `SimulationClock`.

---

## Design notes

### `IsChokingTag` and `ChokingComponent`

```csharp
// Tags.cs (additive)
public struct IsChokingTag {}

// new file ChokingComponent.cs
public struct ChokingComponent
{
    public long  ChokeStartTick;
    public int   RemainingTicks;     // counts down each tick; mirror of LifeStateComponent.IncapacitatedTickBudget for clarity
    public float BolusSize;          // size that triggered the choke; for telemetry / completion-note tuning
    public CauseOfDeath PendingCause;// always Choked at v0.1; future expansion when other choke-like causes land
}
```

The component duplicates some of `LifeStateComponent`'s state (`RemainingTicks` mirrors `IncapacitatedTickBudget`). This is **intentional**: `ChokingComponent` survives only while `IsChokingTag` is present. Other systems that want to react to "this NPC is currently choking" can `Has<ChokingComponent>` cheaply rather than checking both `LifeStateComponent.State == Incapacitated` AND `LifeStateComponent.PendingDeathCause == Choked`.

### `ChokingDetectionSystem`

Cleanup phase, **after** `EsophagusSystem` and **before** `LifeStateTransitionSystem`. Iterates NPCs with `EsophagusTransitComponent`:

```csharp
public sealed class ChokingDetectionSystem : ISystem
{
    private readonly LifeStateTransitionSystem _transition;
    private readonly NarrativeEventBus         _narrative;
    private readonly SimulationClock           _clock;
    private readonly ChokingConfig             _cfg;

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var npc in em.Query<EsophagusTransitComponent>().OrderBy(e => e.Id))
        {
            if (!LifeStateGuard.IsAlive(npc)) continue;
            if (npc.Has<IsChokingTag>()) continue;       // already choking; transition system handles countdown

            var transit  = npc.Get<EsophagusTransitComponent>();
            float bolus  = transit.BolusSize;            // or load via transit.BolusEntityId → BolusComponent
            if (bolus < _cfg.BolusSizeThreshold) continue;

            // distraction check — at least one of the three must hold
            bool distracted =
                (npc.Has<EnergyComponent>()      && npc.Get<EnergyComponent>().Energy        < _cfg.EnergyThreshold)
             || (npc.Has<StressComponent>()     && npc.Get<StressComponent>().AcuteLevel    >= _cfg.StressThreshold)
             || (npc.Has<SocialDrivesComponent>()&& npc.Get<SocialDrivesComponent>().Irritation >= _cfg.IrritationThreshold);
            if (!distracted) continue;

            // CHOKE FIRES
            npc.Add(new IsChokingTag());
            npc.Add(new ChokingComponent {
                ChokeStartTick = _clock.CurrentTick,
                RemainingTicks = _cfg.IncapacitationTicks,
                BolusSize      = bolus,
                PendingCause   = CauseOfDeath.Choked
            });
            // panic mood (one-shot spike; existing mood system applies decay)
            if (npc.Has<MoodComponent>())
            {
                var mood = npc.Get<MoodComponent>();
                mood.PanicLevel = MathF.Max(mood.PanicLevel, _cfg.PanicMoodIntensity);
                npc.Set(mood);
            }
            // narrative emit BEFORE transition request (so subscribers see Alive at this instant)
            _narrative.Emit(new NarrativeEventCandidate {
                Kind = NarrativeEventKind.ChokeStarted,
                Participants = ParticipantsWithWitness(npc),
                Tags = new[] { "choke", "start" },
                Tick = _clock.CurrentTick
            });
            // hand off to LifeStateTransitionSystem; PendingDeathCause carries through
            _transition.RequestTransition(npc.Id, LifeState.Incapacitated, CauseOfDeath.Choked);
        }
    }
}
```

Determinism: `OrderBy(e.Id)`, no RNG. The threshold comparisons are exact; the "at least one of three" distraction check is short-circuiting in deterministic order.

The system **does not** count down `ChokingComponent.RemainingTicks` itself — that's `LifeStateTransitionSystem`'s job (via `IncapacitatedTickBudget`). `ChokingComponent` mirrors the budget for read convenience; the canonical countdown is in `LifeStateComponent`.

### Cleanup at transition to Deceased

When `LifeStateTransitionSystem` flips an NPC from `Incapacitated → Deceased`, this packet adds a one-line cleanup: remove `IsChokingTag` and `ChokingComponent`. The deceased NPC no longer "is choking" — they have died of choking, which is recorded by `CauseOfDeathComponent`.

The cleanup happens in **a new tiny system** `ChokingCleanupSystem` that runs at the *end* of Cleanup phase (after `LifeStateTransitionSystem`), iterating NPCs with `IsChokingTag`: if state is `Deceased`, remove tag + component. This avoids extending `LifeStateTransitionSystem` (single-writer pattern preserved) and keeps the cleanup local to this packet.

### Witness selection

Reuse the same helper from WP-3.0.0 if exposed; otherwise implement locally as `ParticipantsWithWitness(Entity npc)`:

- `[npc.Id]` if no NPC in `ProximityComponent.ConversationRange` and Alive.
- `[npc.Id, witnessId]` where `witnessId` is the smallest-`EntityIntId` Alive NPC in conversation range.

Document in the completion note whether the helper was extracted to a shared file or duplicated.

### SimConfig additions

```jsonc
{
  "choking": {
    "bolusSizeThreshold":      0.65,    // 0..1, fraction of esophagus capacity
    "energyThreshold":         30,      // Energy below this = distracted-tired
    "stressThreshold":         60,      // AcuteLevel above this = distracted-stressed
    "irritationThreshold":     70,      // Irritation drive above this = frustrated-eating
    "incapacitationTicks":     180,     // ~3 game-minutes at current tick rate
    "panicMoodIntensity":      0.85,    // 0..1, mood-axis level set on choke
    "emitChokeStartedNarrative": true   // dev kill-switch; never disable in production
  }
}
```

`incapacitationTicks` is the same *concept* as WP-3.0.0's `defaultIncapacitatedTicks` but per-cause. The transition-system queue stamps the NPC's budget with whichever cause-specific value applies. WP-3.0.0 ships the default; this packet overrides for choke specifically. Document the override pattern in the completion note for 3.0.3 to follow.

### Determinism

- `ChokingDetectionSystem` iterates in `OrderBy(e.Id)`.
- All thresholds are scalar comparisons.
- No `System.Random`. The "panic mood spike" is set to a fixed value, not sampled.
- The 5000-tick test confirms: a scripted scenario where one NPC is set up at tick 1000 to choke (configurable thresholds, prepared bolus) produces byte-identical state across two seeds and dies at the same tick.

### Tests

- `IsChokingTagTests.cs` — tag presence/absence semantics.
- `ChokingComponentTests.cs` — construction, defaults.
- `ChokingDetectionSystemTriggerTests.cs` — bolus + low energy → tag added, narrative emitted, RequestTransition called with `(npc, Incapacitated, Choked)`. Probe via test-only seam on `LifeStateTransitionSystem.PeekQueue` if exposed; else assert on `LifeStateComponent.PendingDeathCause` after one tick.
- `ChokingDetectionSystemNoTriggerTests.cs` — small bolus → no choke. Big bolus + well-rested NPC → no choke. Already-Incapacitated NPC → not re-triggered.
- `ChokingDetectionSystemDistractionTests.cs` — each of the three distraction conditions (low energy, high stress, high irritation) independently triggers; combinations also trigger; absence of all does not.
- `ChokingNarrativeEmitTests.cs` — `ChokeStarted` emitted with correct participants (single-participant alone case; two-participant witnessed case).
- `ChokingTransitionTests.cs` — choke + N ticks (where N = `incapacitationTicks`) → `LifeStateComponent.State == Deceased`, `CauseOfDeathComponent.Cause == Choked`. Validates the WP-3.0.0 contract still holds end-to-end.
- `ChokingCleanupTests.cs` — at `Deceased`, `IsChokingTag` and `ChokingComponent` are removed.
- `ChokingMemoryPersistenceTests.cs` — witness's `PersonalMemoryComponent` records `ChokeStarted` with `Persistent = true` AND `Choked` (the death event from 3.0.0) with `Persistent = true`. Per-pair memory between deceased and witness records both as well.
- `ChokingMoodPanicTests.cs` — at choke trigger, `MoodComponent.PanicLevel` is set to `panicMoodIntensity`; facing freezes (assertion via `FacingComponent.Direction` unchanged across N ticks).
- `ChokingDeterminismTests.cs` — 5000-tick run with a scripted choke at tick 1000: byte-identical state at recorded intervals and at tick 5000.
- `ChokingNoRescueAtV01Tests.cs` — explicit assertion that no rescue mechanic intervenes; the budget countdown completes; the NPC dies. Document the test as the v0.1 contract.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/ChokingComponent.cs` | New component. |
| code | `APIFramework/Components/Tags.cs` (modified) | Add `IsChokingTag`. **Coordinate with WP-3.0.4 if it merged first.** |
| code | `APIFramework/Components/MoodComponent.cs` (modified) | Add `PanicLevel` field if not already present, and `Panic` to `MoodKind` if used. |
| code | `APIFramework/Systems/LifeState/ChokingDetectionSystem.cs` | Detector + emit + transition request. |
| code | `APIFramework/Systems/LifeState/ChokingCleanupSystem.cs` | Tag/component removal at Deceased. |
| code | `APIFramework/Systems/Narrative/NarrativeEventKind.cs` (modified) | Add `ChokeStarted` at end. Coordinate with WP-3.0.0's four additions. |
| code | `APIFramework/Systems/MemoryRecordingSystem.cs` (modified) | Add `ChokeStarted` → `true` in `IsPersistent`. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `ChokingDetectionSystem` (Cleanup, after Esophagus, before LifeStateTransition); `ChokingCleanupSystem` (Cleanup, last). **Conflict with 3.0.2 / 3.0.3 — keep all.** |
| code | `APIFramework/Config/SimConfig.cs` (modified) | `ChokingConfig` class + property. |
| code | `SimConfig.json` (modified) | `choking` section. |
| code | `APIFramework.Tests/Components/ChokingComponentTests.cs` | Component shape. |
| code | `APIFramework.Tests/Systems/LifeState/ChokingDetectionSystemTriggerTests.cs` | Positive trigger paths. |
| code | `APIFramework.Tests/Systems/LifeState/ChokingDetectionSystemNoTriggerTests.cs` | Negative trigger paths. |
| code | `APIFramework.Tests/Systems/LifeState/ChokingDetectionSystemDistractionTests.cs` | Three distraction conditions. |
| code | `APIFramework.Tests/Systems/LifeState/ChokingNarrativeEmitTests.cs` | Narrative emit shape. |
| code | `APIFramework.Tests/Integration/ChokingTransitionTests.cs` | Choke → Incap → Deceased end-to-end. |
| code | `APIFramework.Tests/Integration/ChokingCleanupTests.cs` | Tag removal on Deceased. |
| code | `APIFramework.Tests/Integration/ChokingMemoryPersistenceTests.cs` | Witness memory persistence. |
| code | `APIFramework.Tests/Integration/ChokingMoodPanicTests.cs` | Panic mood + facing freeze. |
| code | `APIFramework.Tests/Determinism/ChokingDeterminismTests.cs` | 5000-tick byte-identical. |
| code | `APIFramework.Tests/Integration/ChokingNoRescueAtV01Tests.cs` | v0.1 contract: no rescue. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.0.1.md` | Completion note. SimConfig defaults. Witness-selection helper: extracted vs duplicated (and where). Per-cause incapacitation budget pattern (for 3.0.3 to follow). The first observed end-to-end choke death (with the seed and config, so it can be reproduced). `ECSCli ai describe` regen + `FactSheetStalenessTests` regen. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `IsChokingTag`, `ChokingComponent`, `NarrativeEventKind.ChokeStarted` compile and instantiate. | unit-test |
| AT-02 | NPC with bolus ≥ threshold AND any one of (low energy, high stress, high irritation) gains `IsChokingTag` and `ChokingComponent` on the next tick. | unit-test |
| AT-03 | NPC with sub-threshold bolus does **not** gain the tag, regardless of distraction state. | unit-test |
| AT-04 | NPC with above-threshold bolus and zero distraction does **not** gain the tag. | unit-test |
| AT-05 | At choke trigger, `LifeStateTransitionSystem` receives a `RequestTransition(npc, Incapacitated, Choked)`; on the following tick, `LifeStateComponent.State == Incapacitated`, `PendingDeathCause == Choked`, `IncapacitatedTickBudget == choking.incapacitationTicks`. | integration-test |
| AT-06 | `NarrativeEventKind.ChokeStarted` is emitted with `participants[0] == npc.Id`; if a witness is in conversation range, `participants[1] == witnessId`; otherwise participants has length 1. | unit-test |
| AT-07 | `MemoryRecordingSystem` routes `ChokeStarted` to per-pair memory (deceased ↔ witness, if present) and to witness's personal memory; `Persistent == true` in both. | integration-test |
| AT-08 | `MoodComponent.PanicLevel` is set to `panicMoodIntensity` at choke; `FacingComponent.Direction` does not change across the next 100 ticks (panic-frozen). | integration-test |
| AT-09 | At tick `chokeStart + incapacitationTicks`, `LifeStateComponent.State == Deceased`, `CauseOfDeathComponent.Cause == Choked`. | integration-test |
| AT-10 | At Deceased transition, `IsChokingTag` and `ChokingComponent` are removed. `CauseOfDeathComponent` remains. | integration-test |
| AT-11 | No-rescue contract: from `Incapacitated`, no system in v0.1 produces a `RequestTransition(npc, Alive, *)`; the budget runs to completion deterministically. | integration-test |
| AT-12 | Already-Incapacitated NPC is not re-triggered for choking next tick (single-shot). | unit-test |
| AT-13 | Already-Deceased NPC fails the `LifeStateGuard.IsAlive` early-return; not iterated by detection. | unit-test |
| AT-14 | Witness who is themselves Incapacitated or Deceased is **not** selected as the witness participant. | unit-test |
| AT-15 | Determinism: 5000-tick run with a scripted distraction setup at tick 1000: byte-identical state across two seeds; choke fires at the same tick; death registers at the same tick. | unit-test |
| AT-16 | All Phase 0, 1, 2, and WP-3.0.0 tests stay green. | regression |
| AT-17 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-18 | `dotnet test ECSSimulation.sln` — all green, no exclusions. | build + unit-test |
| AT-19 | `ECSCli ai describe` regenerates with two new systems and two new tags/components; `FactSheetStalenessTests` updated. | build + unit-test |

---

## Followups (not in scope)

- **Rescue mechanic** — the most-anticipated follow-up. Another NPC notices a choking NPC in conversation range, action-selection emits a `Rescue(targetNpc)` candidate, the rescue executes a Heimlich, and `LifeStateTransitionSystem.RequestTransition(npc, Alive, Unknown)` clears the Incapacitated state. The substrate exists (transition system accepts the upgrade); the trigger is its own packet. Probably WP-3.1.x or 3.2.x, depending on whether it's player-driven or NPC-autonomous first.
- **Per-archetype choke biasing.** The Newbie panics more under workload pressure; the Old Hand chews carefully. JSON tuning surface in `archetype-choke-baselines.json`. Trivial follow-up.
- **Coughing recovery.** A mid-budget coughing fit clears the bolus and returns the NPC to Alive, leaving them with a +stress and a memory entry. More forgiving than v0.1; reads more realistic.
- **Severity tier.** Distinguish `MinorChoke` (recovered alone), `ChokeRequiringHelp` (needs proximity rescue), and `Fatal` (current v0.1 default). Couples to rescue mechanic packet.
- **Sound trigger emission.** A `Cough`, `Gasp`, `Wheeze` sound trigger emitted at choke start (and during the budget countdown) for the host to synthesise. Per the design philosophy memory's trigger model. Either this packet adds a `SoundTriggerBus` (substantial new substrate) or defers entirely to a future "engine sound triggers" packet.
- **Player-facing UI surface.** Diegetic (per the design philosophy memory) — a coughing animation, a small audio cue if camera in range, and a notification only via the in-world surfaces (phone in your office rings if HR catches it). UX/UI bible-driven; 3.1.E.
- **Choke-on-non-food.** Choking on a pen cap, on water (silent aspiration), on an inhaled object. Out of scope.
- **Homicide-by-choke.** Force-feeding scenarios. Mature-themes adjacent; deferred until the bibles take a position on intentional NPC-NPC death.
- **Choking event metadata for the chronicle.** The death event is already chronicled via 3.0.0; a richer entry that names the food item ("Mark choked on a turkey sandwich") requires the Bolus → Food provenance to survive the death; trivial extension via `ChokingComponent.BolusContents` field. Future packet.
