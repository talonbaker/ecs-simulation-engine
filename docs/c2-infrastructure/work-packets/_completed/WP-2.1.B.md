# WP-2.1.B — physiology-inhibition-veto — Completion Note

**Executed by:** sonnet-4-6
**Branch:** feat/wp-2.1.B
**Started:** 2026-04-26T00:00:00Z
**Ended:** 2026-04-26T01:00:00Z
**Outcome:** ok

---

## Summary

Implemented the second half of the "drives are necessary but not sufficient" thesis: social inhibitions can veto autonomous physiology actions. The work introduces `BlockedActionsComponent` (the per-NPC veto set) and `PhysiologyGateSystem` (the Cognition-phase system that computes it).

Key judgement calls:

**FeedingSystem, not EatingSystem.** The packet names `EatingSystem.cs` but the actual file is `FeedingSystem.cs`. Confirmed by inspection; the guard was added there.

**IReadOnlySet not available in netstandard2.1.** The packet spec declares `public IReadOnlySet<BlockedActionClass> Blocked` but the target framework (`netstandard2.1`) does not include this interface. Used `HashSet<BlockedActionClass>` as the backing store exposed via `IReadOnlyCollection<BlockedActionClass>`, with a convenience `Contains(cls)` method for O(1) membership testing. Tests and consumers use `Contains` exclusively.

**SleepSystem veto shape.** The packet says "guard the action firing only — drives should still drift, urges still build, but the visible action doesn't fire while the veto holds." SleepSystem has two transitions: fall-asleep and wake-up. The veto was applied only to the fall-asleep branch (the `if (!energy.IsSleeping && brainWantsSleep)` block) via an early `continue` that skips the entire iteration when the entity is not yet sleeping. Wake-up is always allowed — an NPC who was already asleep before the veto installed can still wake up normally.

**engine-fact-sheet.md regenerated.** The ECSCli staleness test gates on `dotnet run -- ai describe` output matching the committed fact sheet. After adding `PhysiologyGateSystem` and `PhysiologyGateConfig`, the fact sheet was regenerated to keep the staleness test green.

**Inhibition → physiology class mapping survived unchanged** from the packet's v0.1 commitment: Eat ← BodyImageEating, Sleep ← Vulnerability, Urinate ← PublicEmotion, Defecate ← PublicEmotion.

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | OK | `BlockedActionsComponent` and `BlockedActionClass` compile, instantiate, `Contains` round-trips. |
| AT-02 | OK | Hunger max, `bodyImageEating: 90`, willpower 80 → 0 eats over 1000 ticks. |
| AT-03 | OK | Same NPC with willpower 5 → eats at least once in 1000 ticks (gate breaks). |
| AT-04 | OK | Exhausted NPC, `vulnerability: 80`, willpower 80 → does not fall asleep over 1000 ticks. |
| AT-05 | OK | Bladder critical, `publicEmotion: 90`, willpower 70 → no urination over 500 ticks. |
| AT-06 | OK | `bodyImageEating: 90`, willpower 80, `AcuteLevel: 90` → eats (stress relaxation breaks gate). |
| AT-07 | OK | NPC without `InhibitionsComponent` → never vetoed; FeedingSystem behaves as before. |
| AT-08 | OK | 5000-tick run with fixed inputs → byte-identical `BlockedActionsComponent` state across two runs. |
| AT-09 | OK | All WP-1.x and Wave 1/Wave 2 ATs stay green (795 APIFramework tests passed). |
| AT-10 | OK | `dotnet build ECSSimulation.sln` → 0 warnings. |
| AT-11 | OK | `dotnet test ECSSimulation.sln` → all green (795 APIFramework + 19 ECSCli + 66 Warden.Contracts + 17 Warden.Anthropic + 51 Warden.Telemetry + 136 Warden.Orchestrator). |

---

## Files added

```
APIFramework/Components/BlockedActionsComponent.cs
APIFramework/Systems/PhysiologyGateSystem.cs
APIFramework.Tests/Components/BlockedActionsComponentTests.cs
APIFramework.Tests/Systems/PhysiologyGateSystemTests.cs
APIFramework.Tests/Systems/EatingSystemVetoTests.cs
APIFramework.Tests/Systems/SleepSystemVetoTests.cs
APIFramework.Tests/Systems/BladderSystemVetoTests.cs
APIFramework.Tests/Systems/PhysiologyVetoDeterminismTests.cs
docs/c2-infrastructure/work-packets/_completed/WP-2.1.B.md
```

## Files modified

```
APIFramework/Config/SimConfig.cs            — Added PhysiologyGateConfig class + PhysiologyGate property on SimConfig.
APIFramework/Core/SimulationBootstrapper.cs — Registered PhysiologyGateSystem at Cognition phase; updated pipeline comment.
APIFramework/Systems/FeedingSystem.cs       — One-line veto guard before autonomous-eat trigger.
APIFramework/Systems/SleepSystem.cs         — Veto guard on fall-asleep branch only; wake-up always allowed.
APIFramework/Systems/UrinationSystem.cs     — One-line veto guard before autonomous-urinate trigger.
APIFramework/Systems/DefecationSystem.cs    — One-line veto guard before autonomous-defecate trigger.
SimConfig.json                              — Added physiologyGate section with three tuning keys.
docs/engine-fact-sheet.md                   — Regenerated to include PhysiologyGateSystem and PhysiologyGateConfig.
```

## Diff stats

17 files changed (new files + modified files listed above).

## Followups

- Stake-aware vetos: `stakeFactor = 1.0` throughout; bladder veto should soften when NPC is alone. Needs spatial context.
- PublicEmotion veto should relax when no observers are nearby (spatial integration deferred).
- Veto-break event: when gate breaks open (NPC eats despite `bodyImageEating`), weakly reduce the inhibition strength (therapeutic-experience pathway). Deferred to per-event inhibition packet.
- Surface `BlockedActionsComponent` in telemetry DTO for design-time observability. Deferred to v0.5+ schema bump.
- `ColonSystem` / `DefecationSystem` in scope, but `ColonSystem` only manages tags; `DefecationSystem` is the action trigger. Veto was added to `DefecationSystem` (correct).
