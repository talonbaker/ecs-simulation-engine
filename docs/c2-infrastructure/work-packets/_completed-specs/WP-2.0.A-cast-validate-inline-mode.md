# WP-2.0.A — Cast-Validate Inline-Files Mode

**Tier:** Sonnet
**Depends on:** Phase 1 closure (no Phase-2 dependencies)
**Parallel-safe with:** WP-2.0.B (different file footprint), WP-2.1.A (different project)
**Timebox:** 60 minutes
**Budget:** $0.30

---

## Goal

Resolve the architectural mismatch documented in PHASE-1-HANDOFF §4: Sonnets dispatched through the orchestrator's API path don't have file-system access, so any spec whose `inputs.referenceFiles[]` lists a real path produces a `blocked: missing-reference-file` outcome regardless of whether the files exist on disk. The cast-validate mission hit this exact pattern.

This packet implements **inline mode**: before dispatching each Sonnet, the orchestrator pre-reads every path listed in `spec.Inputs.ReferenceFiles`, concatenates the contents into a single delimited text block, and prepends that block to the Sonnet's user turn. The Sonnet sees the file contents inline; no file-system access required.

After this packet, the cast-validate mission produces a real validation result (ok / failed) instead of always blocking. The pattern generalises to every future spec that carries reference files — content-tuning packets, balance-validation runs, and the Phase-2 packets that read the bibles will all benefit.

The companion approach — **snapshot mode**, where the orchestrator boots the engine and projects a `WorldStateDto` for the Sonnet to validate — is a deliberate non-goal here. It's the long-term goal but a much larger packet; inline mode unblocks the cast-validate workflow today.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` — §4.1 (fail-closed policy), §8.1 (no runtime LLM).
- `docs/c2-infrastructure/PHASE-1-HANDOFF.md` — §4 (architectural finding), §4.1 (post-closure context). The "Inline mode" bullet at the bottom of §4 is the design source for this packet.
- `Warden.Orchestrator/RunCommand.cs` — the dispatch site. Read to confirm where `dispatcher.RunAsync(...)` is called per spec; inline-files preprocessing happens before that call.
- `Warden.Orchestrator/Dispatcher/SonnetDispatcher.cs` — currently calls `_cache.BuildRequest(ModelId.SonnetV46, specJson)`. The `specJson` string is the user-turn text. Inline-mode prepends a reference-files block to that text.
- `Warden.Orchestrator/Cache/PromptCacheManager.cs` — `BuildRequest(model, userText)` is the prompt-assembly entry point. Confirm the signature; do not modify it. Inline files go into `userText`, not into the cached prefix slabs (per-mission content varies, so it would invalidate the cache).
- `Warden.Contracts/Handshake/OpusSpecPacket.cs` — the spec shape. `Inputs.ReferenceFiles` is `List<string>` (one path per entry). Confirm the property name and type before consuming.
- `Warden.Contracts/Handshake/BlockReason.cs` — already includes `MissingReferenceFile`. Use it for any pre-dispatch fail-closed exit.
- `Warden.Orchestrator/Dispatcher/FailClosedEscalator.cs` — does NOT need to change; pre-dispatch blocks return a synthesized `SonnetResult` with `outcome=blocked` straight from the orchestrator, never reaching the escalator.
- `examples/smoke-mission-cast-validate.md`, `examples/smoke-specs/cast-validate.json` — the failing case. The spec lists two reference files (`docs/c2-content/world-definitions/office-starter.json`, `docs/c2-content/archetypes/archetypes.json`); together they're ~23KB. **Don't modify these files** — they're the test case, not the fix surface.
- `Warden.Orchestrator.Tests/RunCommandEndToEndTests.cs` — the existing end-to-end test pattern; new tests follow the same shape.
- `docs/c2-infrastructure/work-packets/_completed/WP-09.md`, `WP-13.md` — Phase-0 and Phase-1 completion notes that show the orchestrator-touch pattern. Read for style.

## Non-goals

- Do **not** implement snapshot mode. That's a separate, larger packet (TBD as WP-2.0.C). Inline mode and snapshot mode coexist long-term — inline for small bundled context (under 200KB), snapshot for the full projected `WorldStateDto`.
- Do **not** modify the cast-validate spec or mission file (`examples/smoke-specs/cast-validate.json`, `examples/smoke-mission-cast-validate.md`). Those are the *test case*. The fix is in the orchestrator.
- Do **not** modify `OpusSpecPacket` or `SpecInputs`. The schema already supports `referenceFiles[]`; this packet just consumes it correctly.
- Do **not** modify any schema file under `docs/c2-infrastructure/schemas/`.
- Do **not** modify `PromptCacheManager` or `CachedPrefixSource`. Inline files live in the user turn (per-mission), not the cached prefix (per-corpus, shared across missions).
- Do **not** add the inline-files block to Haiku dispatches. Haikus receive scenarios, not specs; their context already comes from the cached corpus + scenario JSON. Inline mode is Sonnet-only at v0.1.
- Do **not** modify the persisted `prompt.txt` format beyond the natural inclusion of the new prefix. Existing tests that read `prompt.txt` should keep passing because the file simply gains a section.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (SRD §8.1.)
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### The inlined block format

Each reference file becomes one delimited section in a single prepended user-turn block. The format is plain Markdown; the Sonnet has been seeing Markdown its whole life:

```
## Inlined reference files

The following files are provided inline because Sonnets dispatched through the
Anthropic API do not have file-system access. Each file appears between
`--- BEGIN <path> ---` and `--- END <path> ---` markers. Treat the contents
as authoritative; do not request file access.

--- BEGIN docs/c2-content/world-definitions/office-starter.json ---
<file contents verbatim>
--- END docs/c2-content/world-definitions/office-starter.json ---

--- BEGIN docs/c2-content/archetypes/archetypes.json ---
<file contents verbatim>
--- END docs/c2-content/archetypes/archetypes.json ---

## Spec packet

<existing specJson string>
```

The block precedes the spec JSON. The Sonnet's existing role-frame instructions to "return raw JSON" still apply — the inlined block is context, not output.

### Where the preprocessing lives

A new helper class `Warden.Orchestrator/Dispatcher/InlineReferenceFiles.cs` with a single method:

```csharp
public static class InlineReferenceFiles
{
    public sealed record Outcome(string? InlinedBlock, BlockReason? Reason, string? Details);

    public static Outcome Build(IReadOnlyList<string> paths, string repoRoot,
                                int maxSingleFileBytes  = 100_000,
                                int maxAggregateBytes   = 200_000);
}
```

`Build` returns either `(InlinedBlock: "## Inlined reference files...", null, null)` on success, or `(null, BlockReason.MissingReferenceFile or BlockReason.ToolError, "human-readable details")` on any failure. The caller decides what to do with the failure.

`SonnetDispatcher.RunAsync` calls `InlineReferenceFiles.Build` after the budget check and before the API call. If the helper returns a non-null `Reason`, the dispatcher writes a synthesized `SonnetResult` with `outcome=blocked, blockReason=<reason>` and returns immediately — no API call, no spend. If it returns an `InlinedBlock`, the dispatcher prepends it to the user turn before calling `_cache.BuildRequest`.

### Path resolution

`spec.Inputs.ReferenceFiles[]` paths are repo-relative (e.g., `docs/c2-content/archetypes/archetypes.json`). `RunCommand` already knows the repo root (it's the working directory the orchestrator is invoked from). Pass that into `InlineReferenceFiles.Build` as `repoRoot`. Resolve each path with `Path.Combine(repoRoot, relPath)` and validate the resolved path is *under* `repoRoot` (no `..` traversal). Reject anything that escapes.

### Size caps

- `maxSingleFileBytes = 100_000` (100KB). Anything bigger and the operator should be using snapshot mode or splitting the file.
- `maxAggregateBytes = 200_000` (200KB). The cached prefix is ~34KB; an extra 200KB user turn is still well under the model's context window but past which prompt costs balloon.
- Both caps are **byte counts of file contents**, not of the rendered block (which adds a few hundred bytes of markers). The deltas are noise; bytes-of-content keeps the budget-meaning predictable for the operator.
- Exceeding either cap → `BlockReason.ToolError` with details `"reference file <path> exceeds 100KB"` or `"aggregate reference files exceed 200KB"`. The operator can split, prune, or switch to snapshot mode.

### Pre-dispatch fail-closed semantics

When inlining fails, the dispatcher must:

1. Synthesize a `SonnetResult` with `outcome=blocked`, the appropriate `blockReason`, structured `blockDetails`, empty `acceptanceTestResults`, default `tokensUsed`. Use `MakeBlockedResult` (already exists) for consistency.
2. Persist a CoT entry so the run is auditable: `_cot.PersistResultAsync(runId, workerId, json)` — but **do not** persist a `prompt.txt` (no prompt was assembled) and **do not** persist a `response.raw.json` (no API call was made).
3. Append a structured event to `events.jsonl`: kind `inline-files-blocked`, with `workerId`, `reason`, and the spec id.
4. Return the synthesized result. The cost ledger is not touched (zero spend). The outer aggregator handles the blocked outcome via the existing path.

### Why not modify the cached prefix instead?

The cached prefix is shared across all Sonnets and Haikus in a 5-minute window. If two missions in the same window have different reference files, putting them in the cached prefix would invalidate the cache for one of them. Worse, the cache write penalty (1.25× input rate) on a 200KB prefix would dwarf the savings. Per-mission content belongs in the user turn, which is uncached by design.

### Tests

Five test cases at minimum:

1. Happy path with two files (both under cap, both exist) → returns the rendered block; the block contains both file contents between BEGIN/END markers in the order given.
2. Missing file (path resolves but file doesn't exist) → returns `(null, BlockReason.MissingReferenceFile, "...")`; details name the missing path.
3. Single file > 100KB → returns `(null, BlockReason.ToolError, "...")`.
4. Aggregate > 200KB (e.g., three 70KB files) → returns `(null, BlockReason.ToolError, "...")`.
5. Path with `..` traversal (e.g., `../etc/passwd`) → returns `(null, BlockReason.ToolError, "...")`.
6. Empty `referenceFiles[]` → returns `(InlinedBlock: null, null, null)`. Caller treats null as "no preprocessing needed" and dispatches normally. This is the smoke-mission case (`spec-smoke-01.json` has `referenceFiles: []`); WP-2.0.A must not regress smoke-mission behaviour.

Plus one integration test:

7. End-to-end `RunCommandEndToEndTests`: a synthetic spec with `referenceFiles: [tempFile]` produces a successful Sonnet dispatch in mock mode. The mock client receives the inlined block in its captured `userText`. Verifies the wiring without spending tokens.

The cast-validate end-to-end (real-API) verification is deliberately *not* part of the AT suite — it spends money and depends on a Sonnet behaving sensibly. The operator (Talon) runs that separately as the post-merge verification, similar to how Phase-1's real-API smoke verification was operator-run, not test-suite-run.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Orchestrator/Dispatcher/InlineReferenceFiles.cs` | Static helper class with the `Build` method per Design notes. Includes the rendering logic, path safety check, size caps, and the `Outcome` record. |
| code | `Warden.Orchestrator/Dispatcher/SonnetDispatcher.cs` (modified) | Wire `InlineReferenceFiles.Build` between the budget check and the API call. On `Outcome.Reason != null`, return a synthesized blocked result; else prepend `Outcome.InlinedBlock` to the user turn before `_cache.BuildRequest`. |
| code | `Warden.Orchestrator/RunCommand.cs` (modified) | Pass the repo root (use `Environment.CurrentDirectory` or the runs-root's parent) into `SonnetDispatcher` so it can resolve relative paths. Add a constructor parameter to `SonnetDispatcher` if needed. |
| code | `Warden.Orchestrator.Tests/Dispatcher/InlineReferenceFilesTests.cs` | Six unit tests per the Design notes (cases 1–6). Use `Path.GetTempFileName()` and `Path.GetTempPath()` for temp files; clean up in `Dispose`. |
| code | `Warden.Orchestrator.Tests/RunCommandEndToEndTests.cs` (modified) | Add the integration test (case 7) using the existing temp-dir scaffold. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-2.0.A.md` | Completion note. Standard template. Explicitly enumerate the SimConfig defaults that survived (none, this packet has no SimConfig surface), the test count delta, and what's deferred to snapshot mode (any reference-file scenario over the size cap). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `InlineReferenceFiles.Build` with empty `referenceFiles` returns `Outcome(null, null, null)`. | unit-test |
| AT-02 | `InlineReferenceFiles.Build` with two valid files returns `Outcome(InlinedBlock, null, null)` where `InlinedBlock` contains both file contents between `--- BEGIN <path> ---` / `--- END <path> ---` markers in input order. | unit-test |
| AT-03 | Missing file → `Outcome(null, BlockReason.MissingReferenceFile, details)` where `details` names the missing path. | unit-test |
| AT-04 | Single file exceeding 100KB → `Outcome(null, BlockReason.ToolError, details)` naming the offending file. | unit-test |
| AT-05 | Aggregate exceeding 200KB → `Outcome(null, BlockReason.ToolError, details)`. | unit-test |
| AT-06 | Path with `..` traversal → `Outcome(null, BlockReason.ToolError, details)` naming path-escape as the cause. | unit-test |
| AT-07 | Integration: a synthetic spec with one valid temp-file reference dispatches successfully in mock mode; the mock's captured user-turn text contains the inlined block. | integration-test |
| AT-08 | Integration: a synthetic spec with a missing reference file produces an exit-code-2 mock run with `outcome=blocked` and `blockReason=MissingReferenceFile`; no entry in `cost-ledger.jsonl` (zero spend). | integration-test |
| AT-09 | Existing tests stay green: smoke-mission with `referenceFiles: []` produces structurally identical output (no inlined section, no behaviour change). | regression |
| AT-10 | Existing tests stay green: `Warden.Orchestrator.Tests` (all 122 non-flaky), `Warden.Contracts.Tests`, `Warden.Anthropic.Tests`, `Warden.Telemetry.Tests`. | build + unit-test |
| AT-11 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-12 | `dotnet test ECSSimulation.sln --filter "FullyQualifiedName!~RunCommandEndToEndTests.AT01_MockRun_ExitsZeroAndWritesLedger"` — every existing test stays green; new tests pass. (The excluded test is the pre-existing flake from PHASE-1-HANDOFF §4.1; WP-2.0.B fixes it.) | build |

---

## Followups (not in scope)

- **WP-2.0.C — Snapshot mode.** Orchestrator boots the engine, runs cast generator, projects `WorldStateDto`, passes that as the spec input. Sonnet validates the projected world directly. Most ambitious; closest to what the bibles want long-term. Needed when a spec's reference set exceeds the inline-mode size cap.
- **Cast-validate real-API verification run** — operator (Talon) runs once after merge to confirm the cast-validate mission produces a real Sonnet `outcome=ok|failed` result instead of the previous `blocked: missing-reference-file`. Cost ~$0.40 (Sonnet + Haiku batch, cache-cold).
- **Reference-file content caching** — if a future spec uses the same large file across many missions, hashing the inlined block and caching it via `cache_control` could be a win. Premature at v0.1; revisit when playtest data shows repeat usage.
- **Tier-aware inline rendering** — if Haikus ever consume reference files, the rendering may want to differ (Haikus are smaller; the BEGIN/END style might want compression). Not relevant at v0.1; Haikus consume scenarios, not specs.
