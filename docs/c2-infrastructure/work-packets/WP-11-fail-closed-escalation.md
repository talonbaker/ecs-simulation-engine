# WP-11 — Fail-Closed Escalation Policy

**Tier:** Sonnet
**Depends on:** WP-09
**Timebox:** 45 minutes
**Budget:** $0.20

---

## Goal

Translate the SRD's fail-closed rules into executable state-machine code and enforcement tests. The goal is not to add new behaviour; it is to make the existing dispatcher **physically incapable** of the recursive, helper-spawning, self-healing loops that make multi-agent systems bankrupt operators.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` §4.1 (the canonical policy)
- `docs/c2-infrastructure/02-cost-model.md` §6 ("the sleeper lever")
- `Warden.Contracts/Handshake/SonnetResult.cs` (from WP-02)
- `Warden.Contracts/Handshake/HaikuResult.cs` (from WP-02)

## Non-goals

- Do not add a "retry this worker" code path anywhere. Retry is for transient HTTP, nothing else.
- Do not mutate the `outcome` field after the worker returns. A worker's verdict is final.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Orchestrator/Dispatcher/FailClosedEscalator.cs` | Implements the state machine. |
| code | `Warden.Orchestrator/Dispatcher/EscalationVerdict.cs` | `record EscalationVerdict(bool ProceedDownstream, string HumanMessage, OutcomeCode TerminalOutcome)`. |
| code | `Warden.Orchestrator/Dispatcher/BannedPatternDetector.cs` | Static code analyser-lite: scans Sonnet worktree diffs for patterns that indicate a banned behaviour (new HTTP clients, new `AnthropicClient` instantiations outside the orchestrator, new `Task.Run(...Claude...)`, new subprocess spawns beyond `ECSCli`). A match flips the Sonnet result to `blocked` with reason `tool-error`. |
| code | `Warden.Orchestrator.Tests/Dispatcher/FailClosedEscalatorTests.cs` | Exhaustive — every transition in the state machine. |
| code | `Warden.Orchestrator.Tests/Dispatcher/BannedPatternDetectorTests.cs` | Each banned pattern is a named test case. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-11.md` | Completion note. |

---

## The state machine (authoritative, from SRD §4.1)

```
Input:  SonnetResult | HaikuResult
Output: EscalationVerdict

match (outcome, blockReason) {
    ("ok",     null)
        => Verdict(ProceedDownstream: true,  TerminalOutcome: Ok);

    ("failed", _)
        => Verdict(ProceedDownstream: false, TerminalOutcome: Failed,
                   HumanMessage: "{workerId}: one or more acceptance tests failed. Review {resultPath}.");

    ("blocked", "ambiguous-spec")
        => Verdict(ProceedDownstream: false, TerminalOutcome: Blocked,
                   HumanMessage: "{workerId}: spec was ambiguous. Rewrite SpecPacket; do not redispatch as-is.");

    ("blocked", "build-failed")
        => Verdict(ProceedDownstream: false, TerminalOutcome: Blocked,
                   HumanMessage: "{workerId}: code did not build. See logs in {worktreePath}.");

    ("blocked", "tool-error"       |
                "exception"        |
                "schema-mismatch-on-own-output")
        => Verdict(ProceedDownstream: false, TerminalOutcome: Blocked,
                   HumanMessage: "{workerId}: halted on infrastructure issue. Inspect, do not retry automatically.");

    ("blocked", "budget-exceeded"  |
                "timebox-exceeded")
        => Verdict(ProceedDownstream: false, TerminalOutcome: Blocked,
                   HumanMessage: "{workerId}: exceeded a hard limit. Reconsider scope before any follow-up.");

    _ => throw new InvalidOperationException("unhandled outcome/reason combination");
}
```

No branch in the match arm ever returns `ProceedDownstream = true` for a non-`ok` outcome. That is the property test in AT-04.

---

## Banned patterns the detector catches

1. Any new `using System.Net.Http;` in a file outside `Warden.Anthropic` or `Warden.Orchestrator`.
2. Any new construction of `AnthropicClient`, `HttpClient`, or `WebSocketClient` outside those two projects.
3. Any new `Process.Start(...)` that is not `ECSCli`.
4. Any new file under `Warden.*` that contains the string `ANTHROPIC_API_KEY` in code (the key should only be read in `Program.cs`).
5. Any new dependency added to `APIFramework.csproj`, `ECSCli.csproj`, or `ECSVisualizer.csproj` in a Sonnet diff. (The engine does not depend on Warden.* outside the `ai` verb subtree in `ECSCli`, and even that is `Warden.Contracts` + `Warden.Telemetry` only.)
6. Any new `Task.Run` that calls back into an orchestrator type — a clean signal of recursion.

The detector's false-positive rate matters less than its zero-false-negative property on the patterns listed. Flagged Sonnet outputs are escalated as `blocked, tool-error` even if the worker self-reported `ok`.

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | Every outcome/reason combination enumerated in the SRD has a handled branch. | unit-test |
| AT-02 | No branch returns `ProceedDownstream = true` when `outcome != ok`. (Property test: generate 500 random combinations, assert the invariant.) | unit-test |
| AT-03 | `blocked` propagates even if a sibling Sonnet succeeded — the mission's terminal outcome is the **most severe** (Blocked > Failed > Ok). | unit-test |
| AT-04 | `BannedPatternDetector` flags each of the six patterns with ≥1 positive test and ≥1 negative test (legitimate code that looks similar but should pass). | unit-test |
| AT-05 | A Sonnet that returns `outcome = ok` but whose worktree matches any banned pattern is escalated to `blocked, tool-error`. | unit-test |
| AT-06 | `EscalationVerdict.HumanMessage` is present and non-empty on every non-ok path. | unit-test |
