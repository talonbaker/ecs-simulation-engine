# WP-2.9.A — Engine Fact Sheet Staleness Check

**Tier:** Sonnet
**Depends on:** WP-04 (`ECSCli ai describe`)
**Parallel-safe with:** WP-2.7.A, WP-2.8.A, WP-2.3.B (different file footprints)
**Timebox:** 45 minutes
**Budget:** $0.20

---

## Goal

The engine fact sheet (`docs/engine-fact-sheet.md`) is what every cached prompt slab includes for the Sonnet/Haiku tiers. When the engine grows new systems or `SimConfig` keys, the fact sheet drifts unless someone manually runs `ECSCli ai describe`. Drift is silent: prompts continue to work but they don't reflect current engine state, and Sonnets reason against an out-of-date model. Phase 1 closure flagged this; the post-closure pass identified it as Phase-2 backlog item.

This packet adds a **build-time check** (an xUnit test in `ECSCli.Tests`) that regenerates the fact sheet against the current engine and fails when the regenerated content differs from the checked-in file. Developers either commit the regenerated fact sheet or revert their engine change. The check is fast (regeneration is sub-second) and runs every CI build.

After this packet, the fact sheet stays current automatically — drift is impossible without explicit acknowledgement.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` §2 Pillar A — `ECSCli ai describe` is the regenerator.
- `docs/c2-infrastructure/work-packets/_completed/WP-04.md` — the original `ai describe` packet.
- `docs/c2-infrastructure/PHASE-1-TEST-GUIDE.md` §3 — current operational instructions for regenerating manually.
- `docs/c2-infrastructure/PHASE-1-HANDOFF.md` §6 backlog item 12 (per the original list — confirm number) names this exact follow-up.
- `ECSCli/Ai/AiDescribeCommand.cs` — the regenerator. Read its public interface; the test calls into it (or invokes the CLI verb).
- `ECSCli.Tests/` — existing test project. Add the new test here.
- `docs/engine-fact-sheet.md` — the artifact under check.

## Non-goals

- Do **not** modify `AiDescribeCommand.cs` or change the fact sheet's format. The check compares current output to the checked-in file; format changes are out of scope.
- Do **not** add a CI/build target outside the test framework (no MSBuild target, no GitHub Actions workflow change). The xUnit test runs as part of `dotnet test` and is the entire surface.
- Do **not** auto-regenerate the fact sheet on test failure. The test reports a clear diff and instructions to run `ECSCli ai describe`; the developer commits the result. (Auto-write-on-failure makes the test non-idempotent and hides the underlying engine change.)
- Do **not** test the fact sheet's *content* (system count ≥ N, all expected systems present). The check is purely "current output matches checked-in file." Engine evolution flows through the file; the test guards against unintentional drift.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (SRD §8.1.)
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### The test

```csharp
[Fact]
public async Task FactSheet_IsCurrent_ForCurrentEngineState()
{
    // Arrange: locate the checked-in fact sheet.
    var repoRoot = FindRepoRoot();
    var checkedInPath = Path.Combine(repoRoot, "docs", "engine-fact-sheet.md");
    var checkedIn = await File.ReadAllTextAsync(checkedInPath);

    // Act: regenerate against current engine into a temp file.
    var tempPath = Path.GetTempFileName();
    var exitCode = await AiDescribeCommand.RunAsync(
        outputPath: tempPath,
        ct: CancellationToken.None);
    Assert.Equal(0, exitCode);
    var regenerated = await File.ReadAllTextAsync(tempPath);
    File.Delete(tempPath);

    // Strip any "Generated:" timestamp line so the comparison ignores wall-clock.
    var checkedInNormalized = StripGeneratedLine(checkedIn);
    var regeneratedNormalized = StripGeneratedLine(regenerated);

    // Assert: byte-identical (modulo timestamp).
    Assert.Equal(checkedInNormalized, regeneratedNormalized,
        $"Engine fact sheet is out of date. Run `dotnet run --project ECSCli -- " +
        $"ai describe --out docs/engine-fact-sheet.md` and commit the result.");
}

private static string StripGeneratedLine(string content) =>
    Regex.Replace(content, @"^\*\*Generated:\*\*.*$", string.Empty,
        RegexOptions.Multiline);
```

Two implementation notes the Sonnet must verify against the actual code:

1. **Public surface of `AiDescribeCommand`.** If `RunAsync(outputPath, ct)` doesn't exist, the test invokes the CLI verb via `Process.Start` instead. Either is fine; the test asserts on the produced file.
2. **Generated-timestamp handling.** Confirm the actual format of the timestamp line in the checked-in file (the test guide says `**Generated:**`); adapt the regex if different.

### Failure message quality

The Assert message must tell the developer exactly what to run. Vague "files differ" is bad; "run X command" is good. Include the exact command in the message.

### Test placement

`ECSCli.Tests/AiDescribe/FactSheetStalenessTests.cs` (new file).

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSCli.Tests/AiDescribe/FactSheetStalenessTests.cs` (new) | The single staleness test per Design notes. |
| code | `ECSCli/Ai/AiDescribeCommand.cs` (possibly modified) | Only if the test needs a public `RunAsync` entry point that doesn't currently exist. Minimal addition; do not refactor. |
| doc | `docs/engine-fact-sheet.md` (regenerated) | If the current checked-in file is already stale (likely — Wave 2 systems were added), the test will fail on first run; regenerate and commit as part of this packet. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-2.9.A.md` | Completion note. Confirm whether the fact sheet was regenerated as part of this packet (and what changed); confirm the test's failure-message quality. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `FactSheet_IsCurrent_ForCurrentEngineState` passes when the checked-in fact sheet matches current engine state. | unit-test |
| AT-02 | The test fails with a clear message ("run `dotnet run --project ECSCli -- ai describe --out ...`") when an engine change is introduced without regenerating the fact sheet. | manual-verify (the Sonnet introduces a temporary stub system, runs the test, confirms the failure message names the correct command, then removes the stub) |
| AT-03 | The test ignores the `**Generated:**` timestamp line — running the test twice in a row passes both times even though the regenerated timestamps differ. | unit-test |
| AT-04 | The test runs in under 5 seconds (regeneration is sub-second). | unit-test |
| AT-05 | The checked-in `docs/engine-fact-sheet.md` is current as of this packet's merge — every system from Wave 2 (and Wave 3 if merged before this) is listed. | regenerate + visual verify |
| AT-06 | All other tests stay green. | regression |
| AT-07 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-08 | `dotnet test ECSSimulation.sln` — all green. | build + unit-test |

---

## Followups (not in scope)

- **Multiple checked-in derived files.** If the cached corpus expands beyond the fact sheet (e.g., per-archetype reference files derived from `archetypes.json`), the same staleness pattern applies; generalise the test into a parametrised theory.
- **Pre-commit hook.** A git pre-commit hook that runs the regeneration and stages the result automatically. Out of scope for v0.1 — the test is the authoritative check.
- **CI badge.** When the project moves to a CI environment, surface fact-sheet-current as a status badge.
