# WP-2.7.A — per-npc-names — Completion Note

**Executed by:** sonnet-claude-sonnet-4-6
**Branch:** feat/wp-2.7.A
**Started:** 2026-04-26T00:00:00Z
**Ended:** 2026-04-26T00:30:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Added deterministic per-NPC name assignment to the cast generator using a new 50-name JSON pool. Created `docs/c2-content/cast/name-pool.json` with the exact name list from the packet's design notes (Donna, Greg, Frank included as required). Created `APIFramework/Bootstrap/NamePoolLoader.cs` mirroring the `ArchetypeCatalog.LoadDefault()` walk-up pattern — finds the file by ascending up to 8 directories. Added a `Lazy<NamePoolDto?>` cache so the file is read once per process.

Modified `CastGenerator.SpawnAll` with a new optional `namePool` parameter (defaults to `NamePoolLoader.LoadDefault()`). The selection algorithm uses `Except` against a per-call `HashSet<string>` so names are unique within each `SpawnAll` call but reusable across saves. Throws `InvalidOperationException` with a descriptive message when the pool is exhausted. `SpawnNpc` signature is unchanged — the `IdentityComponent` is attached after `SpawnNpc` returns.

Confirmed AT-07: `SimulationSnapshot.Capture()` already reads `IdentityComponent.Name` (line 98) and `TelemetryProjector` already maps `s.Name` to `EntityStateDto.Name`. No projector changes required.

Final name count: 50 names. Bible exemplars Donna, Greg, Frank confirmed present.

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | OK | `NamePoolLoaderTests` — loads cleanly, 50 entries ≥ 30, all unique, all non-empty |
| AT-02 | OK | `NamePoolBibleExemplarsTests` — Donna, Greg, Frank all present |
| AT-03 | OK | `CastGeneratorNameAssignmentTests` — 5 NPCs, all have `IdentityComponent`, names distinct, from pool |
| AT-04 | OK | `CastGeneratorNameAssignmentTests.AT04_SameSeed_ProducesSameNameAssignments` |
| AT-05 | OK | `CastGeneratorNameAssignmentTests.AT05_TenDifferentSeeds_ProduceAtLeastTwoDifferentNameAssignments` |
| AT-06 | OK | `CastGeneratorNameExhaustionTests` — `InvalidOperationException` thrown, "exhausted" in message, "name-pool.json" in message |
| AT-07 | OK | `IdentityProjectionTests` — `SpawnHuman(name: "Donna")` projects to `EntityStateDto.Name == "Donna"` |
| AT-08 | OK | `dotnet test ECSSimulation.sln` — 935 tests, 0 failures |
| AT-09 | OK | `dotnet build ECSSimulation.sln` — 0 warnings |
| AT-10 | OK | All tests green (see AT-08) |

## Files added

```
docs/c2-content/cast/name-pool.json
APIFramework/Bootstrap/NamePoolLoader.cs
APIFramework.Tests/Bootstrap/NamePoolLoaderTests.cs
APIFramework.Tests/Bootstrap/NamePoolBibleExemplarsTests.cs
APIFramework.Tests/Bootstrap/CastGeneratorNameAssignmentTests.cs
APIFramework.Tests/Bootstrap/CastGeneratorNameExhaustionTests.cs
Warden.Telemetry.Tests/IdentityProjectionTests.cs
docs/c2-infrastructure/work-packets/_completed/WP-2.7.A.md
```

## Files modified

```
APIFramework/Bootstrap/CastGenerator.cs — Added optional namePool parameter to SpawnAll; name selection and IdentityComponent assignment inside the spawn loop.
```

## Diff stats

8 files changed (7 new, 1 modified). Approximately 235 insertions, 4 deletions (in CastGenerator.cs).

(Run `git diff --stat HEAD~1...HEAD` after commit for exact counts.)

## Followups

- Per-archetype name biasing (e.g. Founder's Nephew → newer names like Tyler, Kayden).
- Last names for casts with more than 15 NPCs.
- Cultural diversity sweep on the 50-name pool.
- Cross-save name reuse prevention if world-to-world identity continuity is desired.
- `NamePoolLoader` cache is process-scoped via `Lazy<T>`; a test that modifies the pool file and calls `LoadDefault()` in the same process won't see the update. Acceptable for current test patterns.
