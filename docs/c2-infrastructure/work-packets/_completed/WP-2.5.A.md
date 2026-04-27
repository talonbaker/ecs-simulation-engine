# WP-2.5.A — social-mask-system — Completion Note

**Executed by:** claude-sonnet-4-6
**Branch:** feat/wp-2.5.A
**Started:** 2026-04-26T00:00:00Z
**Ended:** 2026-04-26T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Implemented the Social Mask System — the gap between an NPC's felt vs. performed emotional state. Three cooperating systems:

`MaskInitializerSystem` (PreUpdate=0) attaches `SocialMaskComponent` to every NPC with a personality-derived baseline: `Clamp(C*10 + E*(-5) + 30, 0, 100)`.

`SocialMaskSystem` (Cognition=30) grows each of the four suppressible drives (irritation, affection, attraction, loneliness) when the NPC is in a high-exposure context (bright room + observers). Exposure factor: `(illumination/100)*0.5 + min(nearbyCount,4)/4*0.5`. Growth scales by personality bias `(1+C*maskScale)*(1-E*extraversionScale)`. Fractional growth accumulators (same pattern as DriveDynamicsSystem) prevent sub-integer loss. Mask decays when exposure falls below LowExposureThreshold; single shared decay accumulator per entity.

`MaskCrackSystem` (Cleanup=80) computes `crackPressure = pressureMask + pressureWillpower + pressureStress + pressureBurnout`. When pressure ≥ CrackThreshold (1.5), the dominant masked drive is reset to 0, a `NarrativeEventCandidate(MaskSlip)` is raised on the bus, and `IntendedActionComponent(Dialog, MaskSlip)` is written on the cracking NPC — overriding any prior ActionSelection intent.

Downstream patches: `MaskSlip` added to `DialogContextValue`, `NarrativeEventKind`, `MapContextValue`, `IsPersistent`, and the corpus schema context enum. 15 maskSlip corpus fragments added across 5 registers.

All 14 ATs verified. Build: 0 warnings. Tests: 672 pass (103 new), 0 fail, 0 regressions.

## SimConfig defaults that survived

| Key | Default |
|:---|:---:|
| MaskGainPerTick | 0.5 |
| MaskDecayPerTick | 0.3 |
| LowExposureThreshold | 0.30 |
| PersonalityMaskScale | 0.20 |
| PersonalityExtraversionScale | 0.10 |
| CrackThreshold | 1.50 |
| StressCrackContribution | 0.50 |
| BurnoutCrackBonus | 0.30 |
| LowWillpowerThreshold | 30 |
| SlipCooldownTicks | 1800 |

All defaults match the packet's design notes without adjustment.

## Design decisions

- **Corpus schema update required:** `maskSlip` is a new dialog context value. Both `Warden.Contracts/SchemaValidation/corpus.schema.json` and `docs/c2-infrastructure/schemas/corpus.schema.json` needed the new value added to their context enum. The packet deliverables table didn't list the schema, but without this addition the corpus file fails validation on load.
- **`DialogContextDecisionSystem` patched for MaskSlip:** The packet states the system "already reads IntendedActionComponent.Context. The new MaskSlip value flows through unchanged." Without adding `DialogContextValue.MaskSlip => "maskSlip"` to `MapContextValue`, unknown enum values default to "greeting". Added to make AT-08 pass and deliver correct corpus routing.
- **`IsPersistent` patch in MemoryRecordingSystem:** WP-2.3.A was written before MaskSlip existed. Added `NarrativeEventKind.MaskSlip => true` in the same packet to avoid deferring a one-liner fix that would cause AT-09 to fail.
- **No RNG in MaskCrackSystem:** Fully deterministic — pressure formula, observer selection (sorted by EntityIntId), dominant drive selection (fixed declaration order). AT-10 determinism test verifies this over 5000 ticks.
- **LastSlipTick=0 sentinel:** 0 means "never cracked"; the cooldown check is `mask.LastSlipTick > 0 && ...`. This allows cracking on tick 1 of any new simulation run.
- **Cleanup=80 phase reused from WP-2.4.A:** MaskCrackSystem runs after ActionSelectionSystem (Cognition=30) so it can unconditionally override the intent.

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | pass | SocialMaskComponent defaults; baseline formula from personality — 10 tests |
| AT-02 | pass | Mask grows under elevated drive + high exposure |
| AT-03 | pass | Mask decays in low-exposure context; no decay in high-exposure |
| AT-04 | pass | High-C grows faster; high-E grows slower |
| AT-05 | pass | Crack fires above threshold; IntendedAction written; narrative candidate emitted |
| AT-06 | pass | Cooldown blocks re-crack; LastSlipTick=0 bypasses cooldown |
| AT-07 | pass | Dominant drive reset; others unchanged |
| AT-08 | pass | Dialog(MaskSlip) intent → DialogContextDecisionSystem Path 1 → "maskSlip" context |
| AT-09 | pass | Solo → PersonalMemory; pair → RelationshipMemory; Persistent=true |
| AT-10 | pass | 5000-tick determinism — byte-identical MaskSlip candidate stream |
| AT-11 | pass | 15 maskSlip corpus fragments; all noteworthiness≥70; 5 registers |
| AT-12 | pass | All prior-wave tests pass (0 regressions) |
| AT-13 | pass | Build: 0 warnings |
| AT-14 | pass | Full solution test suite green |

## Files added

- `APIFramework/Components/SocialMaskComponent.cs`
- `APIFramework/Systems/MaskInitializerSystem.cs`
- `APIFramework/Systems/SocialMaskSystem.cs`
- `APIFramework/Systems/MaskCrackSystem.cs`
- `APIFramework.Tests/Components/SocialMaskComponentTests.cs`
- `APIFramework.Tests/Systems/SocialMaskSystemTests.cs`
- `APIFramework.Tests/Systems/MaskCrackSystemTests.cs`
- `APIFramework.Tests/Integration/MaskCrackNarrativeIntegrationTests.cs`
- `APIFramework.Tests/Integration/MaskCrackToDialogIntegrationTests.cs`
- `APIFramework.Tests/Integration/MaskCrackMemoryIntegrationTests.cs`
- `APIFramework.Tests/Systems/SocialMaskDeterminismTests.cs`
- `APIFramework.Tests/Data/CorpusMaskSlipFragmentValidationTests.cs`

## Files modified

- `APIFramework/Components/IntendedActionComponent.cs` — added `MaskSlip` to `DialogContextValue`
- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` — added `MaskSlip`
- `APIFramework/Config/SimConfig.cs` — added `SocialMaskConfig` class and `SocialMask` property
- `APIFramework/Systems/Dialog/DialogContextDecisionSystem.cs` — added `MaskSlip => "maskSlip"` to `MapContextValue`
- `APIFramework/Systems/MemoryRecordingSystem.cs` — added `MaskSlip => true` to `IsPersistent`
- `APIFramework/Core/SimulationBootstrapper.cs` — registered `MaskInitializerSystem`, `SocialMaskSystem`, `MaskCrackSystem`
- `SimConfig.json` — added `"socialMask"` section with all 10 config keys
- `Warden.Contracts/SchemaValidation/corpus.schema.json` — added `"maskSlip"` to context enum
- `docs/c2-infrastructure/schemas/corpus.schema.json` — added `"maskSlip"` to context enum
- `docs/c2-content/dialog/corpus-starter.json` — added 15 maskSlip fragments (5 registers)

## Followups

- **WP-2.6.A / mask-driven movement:** When an NPC cracks in a high-exposure room, MaskCrackSystem currently only fires a Dialog intent. A future packet could also queue an `LeftRoomAbruptly` action (flee the scene after the slip).
- **Register-matched MaskSlip dialog:** DialogFragmentRetrievalSystem already performs register matching. The 15 new maskSlip fragments cover all 5 common registers; if a PersonalityComponent register has no match, the system falls back to first-available — acceptable for now.
- **Mask decay on sleep:** SleepingTag causes willpower regen but not mask decay. A future packet could add an explicit mask decay bonus during sleep (solitude + low exposure satisfies the existing formula automatically; no code change needed unless a sleep bonus is desired).
