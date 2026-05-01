# WP-2.5.A — Social Mask System

**Tier:** Sonnet
**Depends on:** WP-1.4.A (willpower + drives), WP-2.1.A (action-selection + dialog context enum), WP-2.4.A (stress component), WP-1.10.A (dialog stack), WP-1.6.A (narrative event bus), WP-2.3.A (memory recording — consumes mask-slip events)
**Parallel-safe with:** WP-2.1.B (different system surface), WP-2.6.A (different component surface)
**Timebox:** 75 minutes
**Budget:** $0.35

---

## Goal

Make the gap between performed and felt emotion observable. Real office workers don't show what they feel; they perform a version of themselves the room expects. Maintaining that performance costs willpower. When willpower runs low — usually under stress — the mask slips, and the slip is one of the most narratively significant events the office produces: the moment Donna can't keep smiling, the moment Greg accidentally says what he actually thinks, the moment the Recovering archetype's controlled exterior cracks and something raw comes out.

This packet ships:

1. `SocialMaskComponent` per NPC that tracks the gap between *felt* drive state and *performed* drive state.
2. `SocialMaskSystem` that grows the gap when an NPC is in high-exposure space with elevated drives, and computes per-tick mask-strength.
3. A mask-crack detector that fires when willpower is depleted and mask delta is large enough — emitting a `MaskSlip` candidate onto the narrative bus and writing a one-tick `IntendedAction(Dialog, MaskSlip)` so the existing dialog stack produces an unusually candid utterance.
4. A new `MaskSlip` value on `DialogContextValue` and a small starter set of mask-slip fragments added to the dialog corpus.

After this packet, sustained suppression (the loop WP-2.4.A closed) eventually produces visible mask cracks — the bible's "the argument finally erupts, the crying finally happens — and now it's at a desk in front of everyone."

---

## Reference files

- `docs/c2-content/cast-bible.md` — **read first.** Section "Open questions for revision" includes the mask-system proposal and its archetype tie-ins (Recovering, Affair). The cast bible's silhouette / register / archetype frame is the surface this packet's mask-strength is computed on top of.
- `docs/c2-content/dialog-bible.md` — Open questions: "Mask-slip as a dialog context" — the packet implements it. Section "What ships in the implementation packet" describes the corpus addition shape this packet extends.
- `docs/c2-content/new-systems-ideas.md` — Section 3 (SocialMaskSystem + SocialMaskComponent) is the design source.
- `docs/c2-content/action-gating.md` — willpower + suppression mechanic. The mask is what willpower is being spent on; that's why willpower depletes.
- `docs/c2-infrastructure/00-SRD.md` §8.5 (social state is first-class).
- `docs/c2-infrastructure/work-packets/_completed/WP-2.1.A.md` — the dialog context enum and the IntendedActionComponent shape this packet adds to.
- `docs/c2-infrastructure/work-packets/_completed/WP-2.4.A.md` — the stress component this packet reads.
- `docs/c2-infrastructure/work-packets/_completed/WP-2.3.A.md` — the memory recording system that consumes the new MaskSlip narrative candidate.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.10.A.md` — dialog corpus + retrieval pipeline.
- `APIFramework/Components/IntendedActionComponent.cs` — `DialogContextValue` enum gains one new value: `MaskSlip`. Add to the end (between `Apologise` and the close brace) — additive only, no value renumbering.
- `APIFramework/Components/StressComponent.cs` — read `AcuteLevel` and `BurningOutTag` presence as mask amplifiers.
- `APIFramework/Components/WillpowerComponent.cs` — read `Current` for crack threshold.
- `APIFramework/Components/PersonalityComponent.cs` — `Neuroticism` and `Extraversion` modulate mask sensitivity.
- `APIFramework/Components/SocialDrivesComponent.cs` — felt drives. Performed drives are derived per-tick by mask system; not a stored component.
- `APIFramework/Components/RoomIllumination.cs`, `APIFramework/Components/RoomComponent.cs`, `APIFramework/Components/ProximityComponent.cs` — **observability inputs** for mask-load computation. The bible's "exposure" intuition: high illumination + many observers = high mask load.
- `APIFramework/Systems/Narrative/NarrativeEventBus.cs`, `NarrativeEventCandidate.cs`, `NarrativeEventKind.cs` — add a new `MaskSlip` enum value and emit candidates from the mask system. `MemoryRecordingSystem` (WP-2.3.A) will route them to per-pair memory automatically.
- `APIFramework/Systems/ActionSelectionSystem.cs` — **does not need modification.** The mask-crack moment writes directly to `IntendedActionComponent` via a `Cleanup`-phase write that happens *after* ActionSelection ran for the tick. The mask-slip override is intentional: under crack conditions, action-selection's choice is overridden by the slip.
- `APIFramework/Systems/Dialog/DialogContextDecisionSystem.cs` — already reads `IntendedActionComponent.Context`. The new `MaskSlip` value flows through unchanged.
- `APIFramework/Core/SystemPhase.cs` — uses existing `Cognition = 30` and `Cleanup` phases.
- `APIFramework/Core/SimulationBootstrapper.cs` — register `SocialMaskSystem` at `Cognition` phase (computes mask state), `MaskCrackSystem` at `Cleanup` phase (acts on it after ActionSelection ran).
- `docs/c2-content/dialog/corpus-starter.json` — the corpus this packet extends with mask-slip fragments. **Read it** to learn the file format before editing.

## Non-goals

- Do **not** make masking a full performed-drive parallel system. v0.1 derives "performed" lazily from "felt minus mask delta"; no separate `PerformedSocialDrivesComponent` is added. (A future refactor may; not now.)
- Do **not** modify `ActionSelectionSystem`. The mask-crack override is a peer producer that runs at `Cleanup` (after ActionSelection has written its intent). The race is intentional: when the crack fires, it wins for that tick; when it doesn't, ActionSelection's choice stands.
- Do **not** modify `WillpowerComponent` or `WillpowerEventQueue`. The mask system reads willpower; cracking does NOT push more willpower events. (Stress amplification, per WP-2.4.A, already covers the willpower depletion under sustained suppression.)
- Do **not** modify `StressComponent`. Read-only.
- Do **not** modify the dialog calcify mechanism (WP-1.10.A). Mask-slip fragments enter the corpus as new entries with `context: maskSlip`; the existing retrieval and calcify systems handle them via the new context value.
- Do **not** modify `SocialDrivesComponent`. The "felt" half lives there; the "performed" half is computed, not stored.
- Do **not** add player-facing mask UI, debug overlays, or per-NPC mask inspection. v0.1 is engine-internal.
- Do **not** modify the cast generator (WP-1.8.A). Mask baseline is per-NPC and computed from existing personality/archetype state — no spawn-time injection beyond ensuring `SocialMaskComponent` is added (which a tiny `MaskInitializerSystem` handles, parallel to WP-2.4.A's `StressInitializerSystem` pattern).
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (SRD §8.1.)
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### The mask delta — what's being tracked

For each NPC, four key social drives have a `feltVsPerformed` delta:

```csharp
public struct SocialMaskComponent
{
    /// <summary>0..100; how much each drive is being suppressed below felt level.</summary>
    public int IrritationMask;     // suppressed irritation: hiding annoyance
    public int AffectionMask;      // suppressed affection: hiding fondness
    public int AttractionMask;     // suppressed attraction: hiding crush
    public int LonelinessMask;     // suppressed loneliness: pretending fine

    /// <summary>0..100; aggregate willpower load this mask currently demands per tick.</summary>
    public int CurrentLoad;

    /// <summary>0..100; baseline mask strength derived from personality (Conscientiousness +).</summary>
    public int Baseline;

    /// <summary>Tick when last MaskSlip fired for this NPC (0 = never).</summary>
    public long LastSlipTick;
}
```

Only four drives mask: irritation, affection, attraction, loneliness. The other four (belonging, status, trust, suspicion) are either too internally directed (suspicion, trust) or too socially-rewarded-when-shown (belonging, status) to be normally suppressed. The Sonnet may add more if the bibles strongly suggest one — but adds at most two more.

### Mask growth (per tick, in `SocialMaskSystem`)

For each masked drive, the mask grows when:
- The drive's `Current` is elevated (above neutral baseline)
- The NPC is in a high-exposure context (room illumination + nearby observer count)
- Personality permits/requires the suppression (high Conscientiousness, low Extraversion)

```csharp
double exposureFactor = (roomIllumination / 100.0) * 0.5
                      + Math.Min(nearbyNpcCount, 4) / 4.0 * 0.5;   // 0..1

double driveLoad = Math.Max(0, drive.Current - 50) / 50.0;          // 0..1, only above neutral

double personalityMaskBias = (1.0 + ConsciousnessFactor * personalityMaskScale)
                             * (1.0 - ExtraversionFactor * personalityExtraversionScale);

int maskGain = (int)(driveLoad * exposureFactor * personalityMaskBias * maskGainPerTick);
mask = Math.Clamp(mask + maskGain, 0, 100);

// Decay when alone or in low-exposure context:
if (exposureFactor < lowExposureThreshold)
    mask = Math.Max(0, mask - maskDecayPerTick);
```

The aggregate `CurrentLoad` is the sum of the four masks divided by 4, clamped 0..100.

### Mask-driven willpower drain (already done by WP-2.4.A)

WP-2.4.A's stress system already accumulates from `SuppressionTick` events pushed by ActionSelection. **This packet does NOT push additional willpower events** — pushing twice would double-count. Instead, the mask system writes its `CurrentLoad` to a public read surface; a future stress refinement packet may use it as a multiplier on suppression event magnitude. v0.1 keeps the willpower path clean and lets stress accumulate via the existing channel.

### Mask cracks (per tick, in `MaskCrackSystem`, runs at `Cleanup` phase)

```
For each NPC with SocialMaskComponent:
    if (lastSlipTick + slipCooldownTicks > currentTick) continue   // sticky cooldown

    crackPressure = mask.CurrentLoad / 100.0
                  + max(0, (lowWillpowerThreshold - willpower.Current)) / lowWillpowerThreshold
                  + (stress.AcuteLevel / 100.0) * stressCrackContribution
                  + (npc.Has<BurningOutTag>() ? burnoutCrackBonus : 0)

    if (crackPressure < crackThreshold) continue

    // Crack fires
    var dominantMaskedDrive = argmax of (irritation, affection, attraction, loneliness) masks
    Emit narrative candidate: NarrativeEventKind.MaskSlip,
        participants = [npc.Id, ... nearbyObserverIds(up to 3)]
        detail = "<NPC> mask slipped: <dominantMaskedDrive>"
    Write to npc: IntendedActionComponent(Dialog, /*targetEntityId*/ nearestObserver, MaskSlip, intensityHint = (int)(crackPressure * 100))
    Set mask.LastSlipTick = currentTick
    Reset mask.<dominantMaskedDrive>Mask to 0     // pressure released
```

Sticky cooldown (default 1800 ticks ≈ 30 game-minutes) prevents back-to-back slips. The dominant masked drive's mask resets after the slip — the suppressed feeling came out, the gap closed.

### Why MaskCrackSystem runs at Cleanup, not Cognition

ActionSelection already ran at Cognition; its `IntendedAction` write reflects the NPC's *intentional* choice. The mask crack is *unintentional* — it overrides whatever ActionSelection chose. Running at Cleanup ensures the overwrite wins on this tick, and the NPC speaks the mask-slip fragment instead of the planned utterance. Next tick, ActionSelection runs again and the NPC resumes intentional behaviour.

### Narrative bus integration

`NarrativeEventKind` gains one new value: `MaskSlip`. Add it at the end of the enum (additive only). Emit candidates with:
- `Kind = NarrativeEventKind.MaskSlip`
- `ParticipantIds = [NPC's int id, observer1, observer2, ...]` (NPC first; up to 3 observers)
- `RoomId = NPC's current room id`
- `Detail = "{npcId} mask slipped: {dominantMaskedDrive}"` (under 280 chars)

`MemoryRecordingSystem` (WP-2.3.A) will route this automatically — it'll be a high-noteworthiness per-pair entry on each (NPC ↔ observer) relationship. WP-2.3.A's `Persistent` mapping table will need `MaskSlip` added; see this packet's followups for who picks that up.

### Dialog corpus extension

Add ~10–15 mask-slip fragments to `docs/c2-content/dialog/corpus-starter.json`. Each fragment:
- `context: "maskSlip"`
- `noteworthiness: 70+` (mask slips are memorable by definition)
- Spread across registers (formal, casual, crass) — different archetypes break differently
- Examples (the Sonnet authors a fuller set):
  - `"You know what? No. I'm not fine."` (casual, irritation)
  - `"I can't keep doing this."` (clipped, exhaustion)
  - `"I really care about you. There. I said it."` (casual, affection)
  - `"I cannot — sorry, I just need a minute."` (formal, overwhelm)

The Sonnet authors fragments that span registers and the four masked drives. Quality bar: each fragment should feel like something a real coworker has actually said in real-life mask-slip moment.

### MaskInitializerSystem

Parallel to WP-2.4.A's pattern: a tiny boot-time system that attaches `SocialMaskComponent` to each NPC with `NpcArchetypeComponent` and no existing component. Initial values:
- All four mask values: 0
- `CurrentLoad`: 0
- `Baseline`: derived from personality (Conscientiousness × 10 + Extraversion × -5 + 30, clamped 0..100)
- `LastSlipTick`: 0

No JSON file needed (unlike stress/schedules); baseline is derived from existing personality.

### SimConfig additions

```jsonc
{
  "socialMask": {
    "maskGainPerTick":              0.5,
    "maskDecayPerTick":              0.3,
    "lowExposureThreshold":          0.30,
    "personalityMaskScale":          0.20,
    "personalityExtraversionScale":  0.10,
    "crackThreshold":                1.50,
    "stressCrackContribution":       0.50,
    "burnoutCrackBonus":             0.30,
    "lowWillpowerThreshold":         30,
    "slipCooldownTicks":             1800
  }
}
```

### Determinism

All RNG via `SeededRandom`. The crack-pressure formula is fully deterministic. The 5000-tick determinism test confirms byte-identical mask state.

### Tests

- `SocialMaskComponentTests.cs` — construction, clamping (0..100), equality.
- `SocialMaskSystemTests.cs` — mask grows under elevated drives + high exposure; decays in low exposure; personality biases hold (high-Conscientiousness NPC accumulates faster than low).
- `MaskCrackSystemTests.cs` — crack fires when pressure ≥ threshold; sticky cooldown holds; dominant mask resets after slip; intent override survives the tick.
- `MaskCrackNarrativeIntegrationTests.cs` — when crack fires, a `MaskSlip` candidate appears on the narrative bus with correct participants and room; the NPC's `IntendedActionComponent` is overridden to `Dialog(MaskSlip)`.
- `MaskCrackToDialogIntegrationTests.cs` — `Dialog(MaskSlip)` flows through `DialogContextDecisionSystem` and produces a `maskSlip`-context fragment from the corpus.
- `MaskCrackMemoryIntegrationTests.cs` — the new `MaskSlip` candidate routed by `MemoryRecordingSystem` writes per-pair memory entries on each NPC↔observer relationship.
- `SocialMaskDeterminismTests.cs` — 5000-tick byte-identical state.
- `CorpusMaskSlipFragmentValidationTests.cs` — corpus loads; new mask-slip fragments validate (`context: maskSlip`, `noteworthiness ≥ 70`, register coverage).

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/SocialMaskComponent.cs` | The component + struct. |
| code | `APIFramework/Components/IntendedActionComponent.cs` (modified) | Add `MaskSlip` to `DialogContextValue` enum (additive at end of enum). |
| code | `APIFramework/Systems/SocialMaskSystem.cs` | Per-tick mask growth + decay. Runs at `Cognition`. |
| code | `APIFramework/Systems/MaskCrackSystem.cs` | Per-tick crack detection + override + narrative emit. Runs at `Cleanup`. |
| code | `APIFramework/Systems/MaskInitializerSystem.cs` | Spawn-time component attachment with personality-derived baseline. Runs at boot/first-tick. |
| code | `APIFramework/Systems/Narrative/NarrativeEventKind.cs` (modified) | Add `MaskSlip` enum value (additive at end). |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register all three new systems in correct phases. |
| code | `APIFramework/Config/SimConfig.cs` (modified) | `SocialMaskConfig` class + property. Add at file end after the other Wave 2 configs. |
| code | `SimConfig.json` (modified) | `socialMask` section. Add at file end after the other Wave 2 sections. |
| data | `docs/c2-content/dialog/corpus-starter.json` (modified) | Add 10–15 new fragments with `context: maskSlip`, `noteworthiness ≥ 70`, spread across registers. |
| code | `APIFramework.Tests/Components/SocialMaskComponentTests.cs` | Construction, clamping. |
| code | `APIFramework.Tests/Systems/SocialMaskSystemTests.cs` | Growth + decay + personality bias. |
| code | `APIFramework.Tests/Systems/MaskCrackSystemTests.cs` | Crack threshold + cooldown + override. |
| code | `APIFramework.Tests/Integration/MaskCrackNarrativeIntegrationTests.cs` | Bus + intent override wired correctly. |
| code | `APIFramework.Tests/Integration/MaskCrackToDialogIntegrationTests.cs` | Dialog stack picks up the override. |
| code | `APIFramework.Tests/Integration/MaskCrackMemoryIntegrationTests.cs` | Memory recording captures the slip. |
| code | `APIFramework.Tests/Systems/SocialMaskDeterminismTests.cs` | 5000-tick byte-identical. |
| code | `APIFramework.Tests/Data/CorpusMaskSlipFragmentValidationTests.cs` | Corpus validation for new fragments. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-2.5.A.md` | Completion note. List the 4 (or 5–6) drives that mask; SimConfig defaults; the fragment count and register distribution; whether WP-2.3.A's `Persistent` mapping for `MaskSlip` was added or punted to a follow-up. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `SocialMaskComponent` and the new `MaskSlip` enum value compile, instantiate, equality round-trip. | unit-test |
| AT-02 | `SocialMaskSystem` over 1000 ticks: NPC with `Irritation = 80`, in a bright room with 3 observers, accumulates `IrritationMask` to ≥ 30. | unit-test |
| AT-03 | Same NPC moved to an empty dark room: mask decays toward 0 over 1000 ticks. | unit-test |
| AT-04 | High-Conscientiousness NPC accumulates mask faster than low-Conscientiousness NPC under identical inputs (paired comparison). | unit-test |
| AT-05 | `MaskCrackSystem` fires when `crackPressure ≥ crackThreshold`: emits exactly one `MaskSlip` narrative candidate per crack, overrides `IntendedActionComponent` with `Dialog(MaskSlip)`. | unit-test |
| AT-06 | Sticky cooldown: a second crack within `slipCooldownTicks` does not fire. | unit-test |
| AT-07 | Dominant masked drive resets to 0 after the slip; other masks unchanged. | unit-test |
| AT-08 | Integration: `Dialog(MaskSlip)` flows through `DialogContextDecisionSystem` → `DialogFragmentRetrievalSystem` selects a `maskSlip`-context fragment from the corpus. | integration-test |
| AT-09 | Integration: `MaskSlip` narrative candidate routed by `MemoryRecordingSystem` writes per-pair memory entries on each NPC↔observer relationship. | integration-test |
| AT-10 | Determinism: 5000-tick run, two seeds with the same world: byte-identical mask state across runs. | unit-test |
| AT-11 | Corpus extension: new `maskSlip` fragments load, validate against `corpus.schema.json`, span at least 3 registers, all have `noteworthiness ≥ 70`. | unit-test |
| AT-12 | All Wave 1, Wave 2 acceptance tests stay green. | regression |
| AT-13 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-14 | `dotnet test ECSSimulation.sln` — all green, no exclusions. | build + unit-test |

---

## Followups (not in scope)

- **`Persistent` mapping for `MaskSlip` in WP-2.3.A's MemoryRecordingSystem.** If WP-2.3.A's mapping table doesn't list `MaskSlip` (because the kind didn't exist yet), this packet should leave it producing `Persistent: false` by default. A trivial follow-up adds it as `Persistent: true` (mask slips are memorable). Document explicitly in completion note whether this packet patched WP-2.3.A's mapping or left the follow-up open.
- **Player-facing mask observability.** Eventually a player should be able to see when an NPC is "holding it together" (high mask CurrentLoad) vs about to crack. v0.1 is engine-internal; player surface is later.
- **Per-listener mask differentiation.** The cast bible's Affair archetype masks differently with the affair partner vs others. v0.1 uses the same mask values for all observers; per-listener mask is a polish packet.
- **Mask-aware action selection.** ActionSelection currently doesn't know about mask. A high-mask NPC might prefer to *avoid* observers (to prevent crack). Cross-system polish; defer to playtest.
- **Stress-mask amplification loop.** This packet does not push extra willpower events from mask load (to avoid double-counting). A later refinement could couple `mask.CurrentLoad` into the stress-system's suppression event magnitude as a multiplier. Speculative.
- **MaskSlip dialog calcify.** A frequent mask-slipper would calcify mask-slip fragments into recurring tics. The existing calcify mechanism handles this automatically because mask-slip fragments are just regular corpus entries with a new context — no extra work needed; document in completion note that this happens.
