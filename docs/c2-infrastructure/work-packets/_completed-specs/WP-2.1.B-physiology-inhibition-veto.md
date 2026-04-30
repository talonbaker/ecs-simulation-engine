# WP-2.1.B — Physiology-Overridable-by-Inhibition

**Tier:** Sonnet
**Depends on:** WP-2.1.A (action-selection scaffold), WP-1.4.A (inhibitions component)
**Parallel-safe with:** WP-2.5.A (social mask), WP-2.6.A (workload) — touches different systems
**Timebox:** 60 minutes
**Budget:** $0.25

---

## Goal

Land the second half of the action-gating bible's "drives are necessary but not sufficient" thesis: physiological actions can be vetoed by social inhibitions. Sally's hunger meter at 120% with `bodyImageEating: 90` produces no eating action — her body says yes, the social/self-image system says no, the social system wins. Same shape for sleep (NPC working through exhaustion because their `vulnerability` inhibition won't let them be the person who couldn't make the deadline) and bladder (holding it through a meeting because `publicEmotion` is high). This is what makes the simulation adult: real offices contain people whose physiology is overridden by their psychology.

WP-2.1.A delivered the social/dialog/movement gate. This packet adds the same gate over physiology by introducing a tiny `BlockedActionsComponent` veto that physiology systems consult before triggering an autonomous action. The veto is computed once per tick by a new system that mirrors `ActionSelectionSystem`'s logic for biological action classes.

After this packet, an NPC with hunger 120 + `bodyImageEating: 90` does not eat — they get visibly weaker through the day, exactly as the bible commits to.

---

## Reference files

- `docs/c2-content/action-gating.md` — **read first.** Section "Physiology overridable by inhibition" is the design source. The whole document is essential context; read it end to end.
- `docs/c2-content/cast-bible.md` — archetype bible's inhibition starter sets. The Recovering and Affair archetypes are most likely to exhibit physiology overrides.
- `docs/c2-infrastructure/00-SRD.md` §8.5 (social state is first-class).
- `docs/c2-infrastructure/work-packets/_completed/WP-2.1.A.md` — the action-selection seam. This packet mirrors its scoring shape on the physiology side.
- `APIFramework/Components/InhibitionsComponent.cs` — `Inhibition { Class, Strength, Awareness }`. Strength 0–100. Same surface this packet's veto consumes.
- `APIFramework/Components/StressComponent.cs` — at high stress, willpower-leakage relaxes the veto (per the same gate-breaks-open mechanic action selection uses). Read for the field shape; consume `AcuteLevel` and `ChronicLevel`.
- `APIFramework/Components/WillpowerComponent.cs` — same.
- `APIFramework/Systems/EatingSystem.cs`, `APIFramework/Systems/SleepSystem.cs`, `APIFramework/Systems/BladderFillSystem.cs` (or whatever the actual file names are — confirm before editing) — the consumer surfaces. Each gains a one-line check on `BlockedActionsComponent` before triggering its autonomous action.
- `APIFramework/Systems/BrainSystem.cs` (if present) — the brain may produce action intents that the new veto needs to consult. Confirm during reading; modify only if necessary.
- `APIFramework/Core/SystemPhase.cs` — confirm available phases. The new `PhysiologyGateSystem` runs at `Cognition = 30` (same phase as ActionSelectionSystem) so its output is visible to physiology systems running in later phases.
- `APIFramework/Core/SimulationBootstrapper.cs` — register the new system.
- `APIFramework/Config/SimConfig.cs` — add tuning.

## Non-goals

- Do **not** modify `ActionSelectionSystem`. The social/dialog/movement gate is unchanged. This packet adds a parallel gate for physiology only.
- Do **not** modify `InhibitionsComponent` or its `InhibitionClass` enum. The existing classes (`Infidelity`, `Confrontation`, `BodyImageEating`, `PublicEmotion`, `PhysicalIntimacy`, `InterpersonalConflict`, `RiskTaking`, `Vulnerability`) are sufficient for the v0.1 mappings.
- Do **not** modify `WillpowerEventQueue` or its signal types. The veto reads existing state; it does not push new event kinds.
- Do **not** modify `IntendedActionComponent`, `IntendedActionKind`, or `DialogContextValue`. Physiology actions are not surfaced through `IntendedAction` — they fire from their own systems with the new component as a veto.
- Do **not** redesign the physiology systems. Each gets exactly one `if (npc.Has<BlockedActionsComponent>() && blocked.Contains(<class>)) return;` early-return and nothing else.
- Do **not** add new physiology drives, override the existing drive shapes, or rebalance the existing physiology constants. Veto presence/absence is the whole behaviour.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (SRD §8.1.)
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### The new component

```csharp
public enum BlockedActionClass
{
    Eat,        // EatingSystem will not autonomously trigger
    Sleep,      // SleepSystem will not autonomously trigger
    Urinate,    // BladderSystem will not autonomously trigger
    Defecate    // ColonSystem will not autonomously trigger (if applicable)
}

/// <summary>
/// Per-NPC veto set. Written each tick by PhysiologyGateSystem; read by
/// physiology systems before they trigger an autonomous action. Empty (default)
/// = no vetoes; the physiology system runs as it always has.
/// </summary>
public struct BlockedActionsComponent
{
    public IReadOnlySet<BlockedActionClass> Blocked;
}
```

### The new system

`PhysiologyGateSystem` runs at the existing `Cognition = 30` phase, after `ActionSelectionSystem`. For each NPC with `InhibitionsComponent`:

1. For each candidate physiology class `(Eat, Sleep, Urinate, Defecate)`:
   - Find the matching inhibition class via the mapping table below.
   - Compute a veto strength = `inhibitionStrength × stakeFactor × (1 - willpowerLeakage)` where:
     - `inhibitionStrength = inhibition.Strength / 100.0`
     - `stakeFactor` is 1.0 by default; in v0.1 we don't yet vary it by social context
     - `willpowerLeakage = max(0, (lowWillpowerThreshold - willpower.Current) / lowWillpowerThreshold) * stressGateRelaxation`
   - If veto strength ≥ `vetoStrengthThreshold` (default 0.50), include the class in the blocked set.
2. Write the resulting `BlockedActionsComponent` to the NPC. Empty set = remove the component (or write empty; consumers check `Has<>()`).

### Inhibition → physiology class mapping

| Physiology class | Inhibition class |
|:---|:---|
| Eat | `BodyImageEating` |
| Sleep | `Vulnerability` (the "I can't be the person who couldn't make the deadline" arc) |
| Urinate | `PublicEmotion` (holding it through a public-facing scenario rather than disrupt it) |
| Defecate | `PublicEmotion` |

Vulnerability is also the gate on physical-intimacy actions, but this packet does not address those — they're not yet driven by physiology systems. The mapping above is the v0.1 commitment; later packets can add more nuance (e.g., `Confrontation` interfering with eating in front of a rival).

### Willpower leakage and stress relaxation

When willpower is low or stress is high, the veto weakens — same mechanic action selection uses. Concretely:

```csharp
double LowWillpowerLeakage(int willpowerCurrent, int lowThreshold = 30)
    => willpowerCurrent >= lowThreshold ? 0.0
        : (lowThreshold - willpowerCurrent) / (double)lowThreshold;

double StressLeakageMult(StressComponent stress, double maxRelaxation = 0.7)
    => 1.0 - (stress.AcuteLevel / 100.0) * maxRelaxation;

// Combined:
double effectiveStrength = inhibitionStrength
    * (1.0 - LowWillpowerLeakage(...))
    * StressLeakageMult(...);
```

Result: an NPC with `bodyImageEating: 90`, willpower 80, stress 0 has veto strength 0.9 — definitely vetoed. Same NPC at willpower 5 has leakage `(30-5)/30 = 0.83`, effective strength `0.9 × 0.17 = 0.15` — under threshold, not vetoed. The gate breaks open after sustained suppression. Acute stress weakens the veto further.

### The physiology system touch points

Each consumer system gets the same one-line guard at the top of its per-NPC loop:

```csharp
// EatingSystem.Update (or wherever the autonomous-eat-trigger lives):
if (entity.Has<BlockedActionsComponent>() &&
    entity.Get<BlockedActionsComponent>().Blocked.Contains(BlockedActionClass.Eat))
    continue;
```

Confirm the actual class names and trigger sites by reading each file before editing. If a system has multiple trigger sites (e.g., urge promotion vs. action firing), guard the action firing only — drives should still drift, urges still build, but the *visible action* doesn't fire while the veto holds.

### SimConfig additions

```jsonc
{
  "physiologyGate": {
    "vetoStrengthThreshold":     0.50,
    "lowWillpowerLeakageStart":  30,
    "stressMaxRelaxation":       0.7
  }
}
```

### Tests

- `BlockedActionsComponentTests.cs` — construction, equality.
- `PhysiologyGateSystemTests.cs` — for each (drive, inhibition, willpower, stress) combination, verify the predicted veto outcome.
- `EatingSystemVetoTests.cs` — NPC with hunger 120 + `bodyImageEating: 90` + willpower 80 does not eat over 1000 ticks; same NPC with willpower 5 eats once per the existing eating cadence.
- `SleepSystemVetoTests.cs` — analogous for sleep + `vulnerability`.
- `BladderSystemVetoTests.cs` — analogous for urination + `publicEmotion`.
- `PhysiologyVetoDeterminismTests.cs` — 5000-tick byte-identical state across two seeds.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/BlockedActionsComponent.cs` | The struct + `BlockedActionClass` enum. |
| code | `APIFramework/Systems/PhysiologyGateSystem.cs` | Per-tick veto computation. |
| code | `APIFramework/Systems/EatingSystem.cs` (modified) | One-line guard at autonomous trigger. |
| code | `APIFramework/Systems/SleepSystem.cs` (modified) | Same. |
| code | `APIFramework/Systems/BladderFillSystem.cs` (modified) | Same. (Confirm exact filename — may be `BladderSystem.cs`.) |
| code | `APIFramework/Systems/ColonSystem.cs` (modified, if exists) | Same. Skip if no autonomous-defecate exists at v0.1. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `PhysiologyGateSystem` at `Cognition` phase. |
| code | `APIFramework/Config/SimConfig.cs` (modified) | `PhysiologyGateConfig` class + property. |
| code | `SimConfig.json` (modified) | `physiologyGate` section. |
| code | `APIFramework.Tests/Components/BlockedActionsComponentTests.cs` | Construction, equality. |
| code | `APIFramework.Tests/Systems/PhysiologyGateSystemTests.cs` | Veto-formula coverage. |
| code | `APIFramework.Tests/Systems/EatingSystemVetoTests.cs` | Eat-veto integration. |
| code | `APIFramework.Tests/Systems/SleepSystemVetoTests.cs` | Sleep-veto integration. |
| code | `APIFramework.Tests/Systems/BladderSystemVetoTests.cs` | Urinate-veto integration. |
| code | `APIFramework.Tests/Systems/PhysiologyVetoDeterminismTests.cs` | 5000-tick byte-identical. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-2.1.B.md` | Completion note. Confirm the inhibition→physiology mapping that survived; SimConfig defaults; any physiology systems that turned out not to have a single autonomous-trigger point (and how that was handled). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `BlockedActionsComponent` and `BlockedActionClass` compile, instantiate, equality round-trip. | unit-test |
| AT-02 | NPC with hunger 120, `bodyImageEating: 90`, willpower 80 → over 1000 ticks, eats 0 times. | integration-test |
| AT-03 | Same NPC with willpower 5 → eats per the existing eating cadence (gate breaks). | integration-test |
| AT-04 | NPC with energy 5 (exhausted), `vulnerability: 80`, willpower 80 → does not sleep over 1000 ticks. | integration-test |
| AT-05 | NPC with bladder critical, `publicEmotion: 90`, willpower 70 → does not urinate autonomously over 500 ticks. | integration-test |
| AT-06 | High stress relaxation: NPC with hunger 120, `bodyImageEating: 90`, willpower 80, `AcuteLevel: 90` → eats (stress relaxation breaks the gate). | integration-test |
| AT-07 | NPC with no `InhibitionsComponent` → never vetoed; physiology systems behave as Phase 1. | regression |
| AT-08 | Determinism: 5000-tick run with seeded inputs, two runs → byte-identical `BlockedActionsComponent` state. | unit-test |
| AT-09 | All WP-1.x and Wave 1/Wave 2 acceptance tests stay green. | regression |
| AT-10 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-11 | `dotnet test ECSSimulation.sln` — all green, no exclusions. | build + unit-test |

---

## Followups (not in scope)

- **Stake-aware vetos.** Currently `stakeFactor = 1.0`. Future tuning: bladder veto stronger when in conference room with superiors than when alone in cubicle. Needs spatial context awareness; defer.
- **Public visibility coupling.** `publicEmotion` veto should soften when the NPC is alone (no observers in proximity). v0.1 ignores observers; spatial integration is later polish.
- **Inhibition install on broken-veto event.** When a veto breaks (NPC eats despite `bodyImageEating`), the bible suggests this should weaken the inhibition slightly — the therapeutic-experience pathway. Defer to a per-event-driven inhibition packet.
- **Surface `BlockedActionsComponent` in telemetry.** Useful for design-time observability. Deferred to a v0.5+ schema bump.
