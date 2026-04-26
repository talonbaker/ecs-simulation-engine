# WP-2.8.A — Dialog Per-Listener Fragment Tracking — Completion Note

**Executed by:** sonnet-1
**Branch:** feat/wp-2.8.A
**Started:** 2026-04-26T00:00:00Z
**Ended:** 2026-04-26T00:30:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Extended `DialogHistoryComponent` with a second dictionary (`UsesByListenerAndFragmentId: Dictionary<int, Dictionary<string, int>>`) that counts per-listener fragment uses. The listener's int id (extracted with the existing `EntityIntId` byte-extraction pattern) serves as the key, keeping the structure flat and dictionary-lookup cheap.

Added per-listener recording in `DialogFragmentRetrievalSystem.Update()` — immediately after the existing global-use recording — and added a `PerListenerBiasScore` term to `SelectFragment` scoring. `SimConfig` default = 2, small enough to break ties without overriding register, valence, or calcify bias.

**Judgment call on deliverable assignment:** The packet listed `DialogCalcifySystem.cs` as the site for per-listener recording. After reading the code, all use-recording (and the `listenerIntId` stub variable) already lives in `DialogFragmentRetrievalSystem.Update()`. `DialogCalcifySystem` handles only `Calcified` state transitions. The packet's design notes said "or wherever the use-recording happens — confirm during reading" — confirmation led to `DialogFragmentRetrievalSystem`. `DialogCalcifySystem` was not modified.

`PerListenerBiasScore = 2` is below calcify bias (3) and well below valence-match (5), so speaker voice remains the dominant axis and per-listener differentiation only surfaces in established pairs, matching the dialog bible's stated intent.

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | ✓ | `DialogHistoryComponentPerListenerTests.cs` — 5 tests: construction, isolation, reference semantics, independence of both dicts |
| AT-02 | ✓ | `DialogFragmentRetrievalPerListenerBiasTests.cs` — 4 tests: bias applied, no bias without history, no bias with different listener, count recorded |
| AT-03 | ✓ | `DialogCodeSwitchingScenarioTests.cs` — chi-square on 50 moments (25 per listener) yields chi-square=50, p<<0.01 (threshold 6.635) |
| AT-04 | ✓ | All existing WP-1.10.A dialog tests pass — calcify, retrieval, corpus service, base determinism unchanged |
| AT-05 | ✓ | `DialogPerListenerDeterminismTests.cs` — two 5000-tick runs with same setup produce identical global + per-listener snapshots |
| AT-06 | ✓ | 929 total tests pass across all projects (Wave 1–4 regressions = 0) |
| AT-07 | ✓ | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors |
| AT-08 | ✓ | `dotnet test ECSSimulation.sln` — 929 passed, 0 failed |

---

## Files added

```
APIFramework.Tests/Components/DialogHistoryComponentPerListenerTests.cs
APIFramework.Tests/Systems/Dialog/DialogFragmentRetrievalPerListenerBiasTests.cs
APIFramework.Tests/Systems/Dialog/DialogCodeSwitchingScenarioTests.cs
APIFramework.Tests/Systems/Dialog/DialogPerListenerDeterminismTests.cs
docs/c2-infrastructure/work-packets/_completed/WP-2.8.A.md
```

## Files modified

```
APIFramework/Components/DialogHistoryComponent.cs          — added UsesByListenerAndFragmentId field + constructor init
APIFramework/Systems/Dialog/DialogFragmentRetrievalSystem.cs — moved listenerIntId computation up; added per-listener recording and SelectFragment bias term
APIFramework/Config/SimConfig.cs                           — added PerListenerBiasScore to DialogConfig (default 2)
SimConfig.json                                             — added perListenerBiasScore: 2 under dialog section
```

## Diff stats

9 files changed, ~460 insertions(+), 7 deletions(−)
*(source changes from HEAD~1: 5 files, 35 insertions, 7 deletions; 4 new test files ~425 lines)*

## Followups

- Per-listener calcify status (bible flags as deferred): fragment used 8+ times with same listener → per-pair tic, stronger retrieval bias only for that listener.
- Per-pair tic recognition: listener marks "X's thing with me" distinct from "X's general thing."
- Memory bounds: profile under playtest; add per-listener LRU bound at ~42K entries/NPC if needed.
- Wire-format surface: `UsesByListenerAndFragmentId` not yet in telemetry DTO; add at v0.5+ schema bump.
