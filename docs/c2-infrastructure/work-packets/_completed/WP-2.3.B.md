# WP-2.3.B — memory-persistence-mapping — Completion Note

**Executed by:** sonnet-1
**Branch:** feat/wp-2.1.B
**Started:** 2026-04-26T00:00:00Z
**Ended:** 2026-04-26T00:30:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Extended `MemoryRecordingSystem.IsPersistent` with two new switch cases for the Wave 3 narrative kinds. `MaskSlip` was already added by WP-2.5.A during that packet's implementation — only `OverdueTask => true` and `TaskCompleted => false` were missing. Both were added in the switch table immediately after `MaskSlip`, preserving the existing order.

`TaskCompleted => false` is technically redundant (the wildcard default is also false) but is present explicitly so future readers can see the kind was deliberately classified rather than accidentally omitted.

The new test class `MemoryPersistenceWaveThreeMappingTests` covers AT-01 through AT-04 with four facts/theories: one per new kind plus a regression theory over the two pre-Wave-3 persistent kinds (`WillpowerCollapse`, `LeftRoomAbruptly`). The existing `MemoryRecordingSystemPersistenceTests` was not modified — it covers only the Phase-1 kinds and remains a valid AT-08 regression fixture.

Full suite: 959 tests, 0 failures, 0 warnings.

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | OK | `IsPersistent(MaskSlip)` → true. Covered by `MaskSlip_IsPersistent`. |
| AT-02 | OK | `IsPersistent(OverdueTask)` → true. Covered by `OverdueTask_IsPersistent`. |
| AT-03 | OK | `IsPersistent(TaskCompleted)` → false. Covered by `TaskCompleted_IsNotPersistent`. |
| AT-04 | OK | `WillpowerCollapse` and `LeftRoomAbruptly` still true. Covered by `ExistingPersistentKinds_StillPersistent`. |
| AT-05 | OK | Full suite 959 tests, 0 failures. All Wave 1–3 tests green. |
| AT-06 | OK | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-07 | OK | `dotnet test ECSSimulation.sln` (excluding RunCommandEndToEndTests.AT01) — 959 passed. |

---

## Files added

```
APIFramework.Tests/Systems/MemoryPersistenceWaveThreeMappingTests.cs
```

## Files modified

```
APIFramework/Systems/MemoryRecordingSystem.cs — Added OverdueTask => true and TaskCompleted => false to IsPersistent switch.
```

## Diff stats

`2 files changed, 34 insertions(+), 0 deletions(-)`

(1 file modified: 2 insertions in MemoryRecordingSystem.cs; 1 file added: 32 lines in MemoryPersistenceWaveThreeMappingTests.cs)

## Followups

- Per-archetype persistence weights — Cynic forgets most things; Recovering remembers everything. Deferred to playtest.
- Magnitude-aware persistence — low-intensity MaskSlip may not warrant persistence; wire IntensityHint. Deferred.
- Time-decay of persistence flag — demote old persistent memories to ephemeral as consolidation pass. Speculative.
