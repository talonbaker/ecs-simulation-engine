# WP-1.6.A — Narrative Telemetry Channel — Completion Note

**Executed by:** claude-sonnet-4-6
**Branch:** feat/wp-1.6.A
**Started:** 2026-04-25T00:00:00Z
**Ended:** 2026-04-25T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Landed the narrative telemetry channel. Three new types: `NarrativeEventKind` (five-value enum), `NarrativeEventCandidate` (sealed record — engine-internal, never touches wire format), `NarrativeEventBus` (singleton in-process event bus). One new system: `NarrativeEventDetector` (SystemPhase.Narrative = 70, runs last).

**Events now detected:** `DriveSpike` — per-NPC per-drive delta ≥ `narrativeDriveSpikeThreshold` (default 15); `WillpowerCollapse` — willpower drop ≥ `narrativeWillpowerDropThreshold` (default 10) in one tick; `WillpowerLow` — first crossing below `narrativeWillpowerLowThreshold` (default 20), re-fires after recovery above threshold; `ConversationStarted` — from `ProximityEventBus.OnEnteredConversationRange`; `LeftRoomAbruptly` — room departure within `abruptDepartureWindowTicks` (default 3) of a `DriveSpike` on the same NPC.

**CLI verb:** `ECSCli ai narrative-stream [--out path.jsonl] [--duration gs] [--seed n]` — runs simulation and emits one JSON line per candidate (camelCase, compact, immediate flush).

**Judgement calls:** `NarrativeEventKind` carries no `[JsonConverter]` attribute — `APIFramework` targets `netstandard2.1` and cannot reference `Warden.Contracts`. The `JsonSmartEnumConverterFactory` in `JsonOptions.Wire` covers camelCase fallback at the serialisation site (ECSCli). Candidates emitted in entity-id ascending order (LINQ stable sort) within a tick.

**Deferred:** chronicle persistence, story correlation, memory recording, candidate quality filtering.

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | OK | DriveSpike emitted with correct before/after/delta for delta ≥ 15. |
| AT-02 | OK | No candidate for delta < threshold. |
| AT-03 | OK | WillpowerCollapse emitted for drop ≥ 10. |
| AT-04 | OK | WillpowerLow fires on first crossing below 20; suppressed on subsequent low ticks. |
| AT-05 | OK | WillpowerLow re-fires after willpower recovers above threshold and drops again. |
| AT-06 | OK | ConversationStarted emitted on EnteredConversationRange event. |
| AT-07 | OK | LeftRoomAbruptly emitted within 3-tick window of DriveSpike; no emit outside window. |
| AT-08 | OK | Two simultaneous spikes: lower entity-id candidate fires first. |
| AT-09 | OK | 5000 ticks × 2 runs same seed=42: byte-identical JSON-serialised candidate streams. |
| AT-10 | OK | RunCore writes valid JSONL; each line has tick/kind/participantIds/detail fields. |
| AT-11 | OK | RunCore with low thresholds + suppression injection produces DriveSpike, WillpowerLow, ConversationStarted in 600 game-seconds. |
| AT-12 | OK | Warden.Telemetry.Tests: all pass — projector unchanged. |
| AT-13 | OK | All existing APIFramework.Tests stay green. |
| AT-14 | OK | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-15 | OK | `dotnet test ECSSimulation.sln` — 121 passed, 0 failed. |

---

## Files added

```
APIFramework/Systems/Narrative/NarrativeEventKind.cs
APIFramework/Systems/Narrative/NarrativeEventCandidate.cs
APIFramework/Systems/Narrative/NarrativeEventBus.cs
APIFramework/Systems/Narrative/NarrativeEventDetector.cs
APIFramework.Tests/Systems/Narrative/NarrativeEventDetectorTests.cs
APIFramework.Tests/Systems/Narrative/NarrativeEventBusTests.cs
ECSCli/Ai/AiNarrativeStreamCommand.cs
ECSCli/AssemblyInfo.cs                     — InternalsVisibleTo("ECSCli.Tests")
ECSCli.Tests/AiNarrativeStreamCommandTests.cs
docs/c2-infrastructure/work-packets/_completed/WP-1.6.A.md
```

## Files modified

```
APIFramework/Core/SystemPhase.cs            — added Narrative = 70 phase
APIFramework/Config/SimConfig.cs            — added NarrativeConfig class + Narrative property on SimConfig
APIFramework/Core/SimulationBootstrapper.cs — NarrativeBus property; NarrativeEventDetector in SystemPhase.Narrative; ApplyConfig merges NarrativeConfig
SimConfig.json                              — added "narrative" section with all five tuning knobs
ECSCli/Ai/AiCommand.cs                     — registered AiNarrativeStreamCommand
```

## Diff stats

15 files changed (10 added, 5 modified). Approximately 700 insertions.

## Followups

- WP-1.9.A: Persistent chronicle — candidate filtering against persistence threshold ("would they still be talking about this in a month"), persisted chronicle entries.
- Memory recording: notable conversations within proximity range produce memory event records on the relationship entity.
- Story correlation / arc detection: a Haiku reads the candidate stream over a window and recognises "start of an argument" or "romance forming."
- Refined detection: drive deltas weighted by archetype (Cynic's irritation spike is less notable than a Hermit's).
- More candidate kinds: physiology-related (bladder critical mid-meeting), workload-related (deadline missed), social-mask-related (mask cracked publicly).
- Candidate-filter Haikus: design-time batches that read the stream and rate candidates for interest; output drives chronicle filtering thresholds.
- Per-NPC notability calibration: a drift detector that learns an NPC's typical day and only emits candidates for departures from it.
