# WP-2.9.A — fact-sheet-staleness-check — Completion Note

**Executed by:** claude-sonnet-4-6
**Branch:** feat/wp-2.9.A
**Started:** 2026-04-26T23:20:00Z
**Ended:** 2026-04-26T23:55:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Added `ECSCli.Tests/AiDescribe/FactSheetStalenessTests.cs` with a single `[Fact]` that:
1. Reads `docs/engine-fact-sheet.md` from the repo root (located by walking up from `AppContext.BaseDirectory` looking for a `.git` file or folder — worktree-safe).
2. Regenerates the fact sheet into a temp file by calling `AiCommand.Root.InvokeAsync(["describe", "--out", tempPath])` in-process (same approach as existing `AiVerbTests`).
3. Strips the `**Generated:**` timestamp line (and normalizes `\r\n` → `\n` to handle Windows line endings vs the `.gitattributes`-enforced `eol=lf` on disk) before comparing.
4. Asserts equality with a failure message that gives the exact command to run.

Two judgement calls:
- **Line-ending normalization added to `StripGeneratedLine`.** `AiDescribeCommand.Run()` uses `StringBuilder.AppendLine()` which emits `\r\n` on Windows, but git's `*.md text eol=lf` rule means the checked-in file has `\n` on disk. Normalization is added to both sides before comparison so the test is OS-independent.
- **xUnit `[Collection("AiCommandSingleton")]` added to both `FactSheetStalenessTests` and `AiVerbTests`.** `System.CommandLine` beta 4.22272.1's `ValidTokens` is not safe for concurrent re-entry on a shared `RootCommand` singleton. Adding both classes to the same xUnit collection serializes their execution without requiring any API surface change.

The checked-in `docs/engine-fact-sheet.md` was regenerated as part of this packet: it grew from 21 systems (Phase 0) to 48 systems (Phase 1 + Phase 2 Wave systems through Wave 4), and from 71 to 147 component types.

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | ✓ | `FactSheet_IsCurrent_ForCurrentEngineState` passes after fact-sheet regeneration. |
| AT-02 | ✓ | Manually verified with stale fact sheet: failure message reads "Engine fact sheet is out of date. Run `dotnet run --project ECSCli -- ai describe --out docs/engine-fact-sheet.md` and commit the result." |
| AT-03 | ✓ | `StripGeneratedLine` removes the `**Generated:**` line (and normalizes line endings); two runs of the test pass. |
| AT-04 | ✓ | Test runs in 24 ms (well under 5 s). |
| AT-05 | ✓ | Regenerated fact sheet lists 48 systems including all Wave 1–4 systems; visually verified. |
| AT-06 | ✓ | All 18 pre-existing `ECSCli.Tests` pass; all 632 `APIFramework.Tests` pass; 17 Anthropic, 66 Contracts, 48 Telemetry, 135 Orchestrator — 917 total, 0 failures. |
| AT-07 | ✓ | `dotnet build ECSSimulation.sln -c Release` → 0 warnings, 0 errors. |
| AT-08 | ✓ | `dotnet test ECSSimulation.sln` (excluding pre-existing `AT01_MockRun_ExitsZeroAndWritesLedger` flake) → 917 passed, 0 failed. |

## Files added

`ECSCli.Tests/AiDescribe/FactSheetStalenessTests.cs`

## Files modified

`ECSCli.Tests/AiVerbTests.cs` — added `[Collection("AiCommandSingleton")]` attribute to serialize execution with the new test class (prevents concurrent `InvokeAsync` on the shared `AiCommand.Root` singleton).

`docs/engine-fact-sheet.md` — regenerated via `ECSCli ai describe`; grew from 21 to 48 systems and 71 to 147 component types to reflect Phase 1 + Wave systems.

## Diff stats

3 files changed, 285 insertions(+), 25 deletions(-)

## Followups

- The `[Collection("AiCommandSingleton")]` workaround would be unnecessary if `System.CommandLine` were upgraded to a stable release; file a note when upgrading.
- `AiDescribeCommand.Run()` is `internal`; if a future test needs to call it without going through `AiCommand.Root`, add `[assembly: InternalsVisibleTo("ECSCli.Tests")]` to `ECSCli/AssemblyInfo.cs`.
