# WP-2.3.A — memory-recording — Completion Note

**Executed by:** sonnet-1
**Branch:** feat/wp-2.3.A
**Started:** 2026-04-26T00:00:00Z
**Ended:** 2026-04-26T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Added `MemoryRecordingSystem` — a subscriber on the `NarrativeEventBus` that routes each narrative event candidate to the appropriate memory surface: two-participant candidates write a `MemoryEntry` to a `RelationshipMemoryComponent` on the canonical relationship entity (auto-created at `Intensity=50` if none exists); solo candidates write to a `PersonalMemoryComponent` on the participant; three-or-more-participant candidates fan out to every participant's personal log. Both components implement bounded ring buffers (capacity enforced by the system, not the struct): 32 for relationship memory, 16 for personal. Memory IDs are deterministic: `mem-{tick:D8}-{Kind}-{firstParticipant:D8}-{count}`.

The `TelemetryProjector` now populates `relationships[].historyEventIds[]` with persistent-only entry IDs and the top-level `worldState.memoryEvents[]` with all entries (persistent + ephemeral), deduplicated by ID. No schema change was required — v0.4 already reserved these fields. The `MemoryConfig` class and `SimConfig.memory` JSON section were added for runtime tuning of both capacities.

**NarrativeEventKind → Persistent mapping committed to:**

| Kind | Persistent |
|:---|:---:|
| `WillpowerCollapse` | true |
| `LeftRoomAbruptly`  | true |
| `DriveSpike`        | false |
| `WillpowerLow`      | false |
| `ConversationStarted` | false |

The design notes listed `RelationshipShift`, `ProlongedConflict`, `SharedSecret`, and `Affair` as persistent kinds — none of these exist in the actual `NarrativeEventKind` enum at this point. The mapping above covers all five actual values.

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | OK | MemoryEntryTests: construction, id determinism, JSON round-trip. |
| AT-02 | OK | MemoryRecordingSystemTests: system subscribed and receives candidates. |
| AT-03 | OK | MemoryRecordingSystemTests: pair candidate → relationship entity, canonical order. |
| AT-04 | OK | MemoryRecordingSystemTests: auto-creates relationship entity at Intensity=50. |
| AT-05 | OK | MemoryRecordingSystemTests: solo candidate → PersonalMemoryComponent. |
| AT-06 | OK | MemoryRecordingSystemTests: 3+ participants → fan-out to all personal logs. |
| AT-07 | OK | MemoryRecordingSystemBufferTests: capacity-32 overflow keeps 32 most recent; capacity-16 personal overflow verified. |
| AT-08 | OK | MemoryRecordingSystemPersistenceTests: all 5 NarrativeEventKind values mapped and verified. |
| AT-09 | OK | MemoryProjectionTests: historyEventIds contains only persistent IDs; ephemeral absent. |
| AT-10 | OK | MemoryProjectionTests: MemoryEvents count matches engine-side; dedup by ID verified. |
| AT-11 | OK | MemoryDeterminismTests: 5000-tick run, seed 42 × 2: byte-identical memory state. |
| AT-12 | OK | Full suite (821 tests): 0 failures; narrative bus and chronicle continue to operate. |
| AT-13 | OK | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-14 | OK | `dotnet test --filter "FullyQualifiedName!~RunCommandEndToEndTests.AT01"` — 821 passed. |

---

## Files added

```
APIFramework/Components/MemoryEntry.cs
APIFramework/Components/RelationshipMemoryComponent.cs
APIFramework/Components/PersonalMemoryComponent.cs
APIFramework/Systems/MemoryRecordingSystem.cs
APIFramework.Tests/Components/MemoryEntryTests.cs
APIFramework.Tests/Components/RelationshipMemoryComponentTests.cs
APIFramework.Tests/Components/PersonalMemoryComponentTests.cs
APIFramework.Tests/Systems/MemoryRecordingSystemTests.cs
APIFramework.Tests/Systems/MemoryRecordingSystemBufferTests.cs
APIFramework.Tests/Systems/MemoryRecordingSystemPersistenceTests.cs
APIFramework.Tests/Systems/MemoryDeterminismTests.cs
Warden.Telemetry.Tests/MemoryProjectionTests.cs
```

## Files modified

```
APIFramework/Config/SimConfig.cs           — Added MemoryConfig class; added Memory property to SimConfig root.
APIFramework/Core/SimulationBootstrapper.cs — Registered MemoryRecordingSystem at SystemPhase.Narrative.
SimConfig.json                             — Added "memory" section with maxRelationshipMemoryCount=32, maxPersonalMemoryCount=16.
Warden.Telemetry/TelemetryProjector.cs     — Added using for NarrativeEventKind; ProjectRelationships populates HistoryEventIds; added ProjectMemoryEvents, ToMemoryEventDto, NarrativeKindToString; Project method wires MemoryEvents.
```

## Diff stats

`4 files modified` (83 insertions, 1 deletion). `12 files added`.

## Followups

- Cross-pair memory propagation — gossip mechanic needed; dialog system not yet available.
- Memory decay over game-time — ring-buffer-only eviction; time-based decay deferred to playtest evidence.
- Affinity / avoidance scoring — count positive vs negative memories for ActionSelectionSystem; phase 3.
- Pattern transition triggers — RelationshipLifecycleSystem transition table has no trigger conditions; memory aggregates could fire transitions once gameplay testing begins.
- Memory-driven dialog selection — calcify mechanism ignores RelationshipMemoryComponent; deferred to phase 3+.
- Long-term consolidation ("sleep tick") — compress N short-term entries into 1 long-term summary; speculative.
- `MemoryConfig` not wired into `ApplyConfig` hot-reload — the packet didn't specify this and capacity changes mid-run are an edge case; add if needed.
