# WP-2.3.B — memory-persistence-mapping — Completion Note

**Executed by:** sonnet-1
**Branch:** feat/wp-2.3.B
**Started:** 2026-04-26T00:00:00Z
**Ended:** 2026-04-26T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Extended `MemoryRecordingSystem.IsPersistent` with two missing Wave 3 `NarrativeEventKind` cases. On first dispatch the branch was behind staging — WP-2.5.A and WP-2.6.A had not been merged, so `MaskSlip`, `OverdueTask`, and `TaskCompleted` were absent from the enum. After pulling and fast-forwarding to staging, the enum had all three values and WP-2.5.A's Sonnet had already added `MaskSlip => true` to the switch table, leaving only `OverdueTask => true` and `TaskCompleted => false` as outstanding work for this packet.

Added the two cases per the design notes. `TaskCompleted => false` is technically redundant with the default but is written explicitly so future readers can see the kind was considered and deliberately classified. Wrote `MemoryPersistenceWaveThreeMappingTests.cs` with five `[Theory]` / `[InlineData]` entries covering AT-01 through AT-04 (including both regression cases). All 1042 tests across the full solution pass; build is warning-free.

**NarrativeEventKind → IsPersistent table (complete after this packet):**

| Kind | Persistent | Rationale |
|:---|:---:|:---|
| `WillpowerCollapse` | true | Phase-1 kind; already mapped. |
| `LeftRoomAbruptly` | true | Phase-1 kind; already mapped. |
| `MaskSlip` | true | Boss cracking in public is remembered for months; added by WP-2.5.A. |
| `OverdueTask` | true | Missed deadlines stick for the person who missed and anyone affected. |
| `TaskCompleted` | false | Routine completions don't deserve a memory slot. |
| `DriveSpike` | false | Ephemeral physiological event. |
| `WillpowerLow` | false | Ephemeral physiological event. |
| `ConversationStarted` | false | Routine interaction. |

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | ✓ | `IsPersistent(MaskSlip)` → `true`. |
| AT-02 | ✓ | `IsPersistent(OverdueTask)` → `true`. |
| AT-03 | ✓ | `IsPersistent(TaskCompleted)` → `false`. |
| AT-04 | ✓ | `WillpowerCollapse` and `LeftRoomAbruptly` still return `true`. |
| AT-05 | ✓ | 1042 tests across solution — all green. |
| AT-06 | ✓ | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-07 | ✓ | `dotnet test ECSSimulation.sln` (excluding RunCommandEndToEndTests.AT01) — 1042 passed. |

---

## Files added

```
APIFramework.Tests/Systems/MemoryPersistenceWaveThreeMappingTests.cs
docs/c2-infrastructure/work-packets/_completed/WP-2.3.B.md
```

## Files modified

```
APIFramework/Systems/MemoryRecordingSystem.cs  — Added OverdueTask => true and TaskCompleted => false to IsPersistent switch.
```

## Diff stats

`1` file modified (2 insertions). `1` test file added (~22 lines). `1` completion note added.

## Followups

- Per-archetype persistence weights (Cynic remembers less, Recovering remembers everything) — deferred to playtest evidence per packet.
- Magnitude-aware persistence — `MaskSlip` at intensity 30 may not deserve persistence; wire `IntensityHint` into classification — deferred per packet.
- Time-decay / demotion of persistent memories as a consolidation pass — speculative, deferred per packet.
