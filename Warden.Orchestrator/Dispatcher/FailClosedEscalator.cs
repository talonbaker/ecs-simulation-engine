using Warden.Contracts.Handshake;

namespace Warden.Orchestrator.Dispatcher;

/// <summary>
/// Translates a worker result into an <see cref="EscalationVerdict"/> by applying the
/// fail-closed policy defined in SRD §4.1.  No branch ever returns
/// <c>ProceedDownstream = true</c> for a non-<c>ok</c> outcome.
///
/// If a worktree diff is supplied, the <see cref="BannedPatternDetector"/> is consulted
/// first; a match overrides any self-reported <c>ok</c> and emits <c>blocked/tool-error</c>.
/// </summary>
public static class FailClosedEscalator
{
    /// <summary>
    /// Evaluates a Sonnet result.  Pass <paramref name="worktreeDiff"/> when the worktree
    /// diff is available so banned patterns are enforced (AT-05).
    /// </summary>
    public static EscalationVerdict Evaluate(SonnetResult result, string? worktreeDiff = null)
    {
        if (worktreeDiff is not null && BannedPatternDetector.HasBannedPattern(worktreeDiff))
        {
            return new EscalationVerdict(
                ProceedDownstream: false,
                HumanMessage: $"{result.WorkerId}: banned pattern detected in worktree diff. " +
                              "Inspect the diff; do not retry automatically.",
                TerminalOutcome: OutcomeCode.Blocked);
        }

        return ApplyStateMachine(result.Outcome, result.BlockReason,
            result.WorkerId, result.WorktreePath, "result.json");
    }

    /// <summary>Evaluates a Haiku result.</summary>
    public static EscalationVerdict Evaluate(HaikuResult result)
        => ApplyStateMachine(result.Outcome, result.BlockReason,
            result.WorkerId, worktreePath: null, "result.json");

    /// <summary>
    /// Returns the most severe outcome across a set of codes.
    /// Severity order: Blocked &gt; Failed &gt; Ok.
    /// Used by the aggregator to compute the mission's terminal outcome (AT-03).
    /// </summary>
    public static OutcomeCode MostSevere(IEnumerable<OutcomeCode> outcomes)
    {
        var seen = OutcomeCode.Ok;
        foreach (var o in outcomes)
        {
            if (o == OutcomeCode.Blocked) return OutcomeCode.Blocked;
            if (o == OutcomeCode.Failed)  seen = OutcomeCode.Failed;
        }
        return seen;
    }

    // -- State machine (authoritative, from SRD §4.1) --------------------------

    private static EscalationVerdict ApplyStateMachine(
        OutcomeCode  outcome,
        BlockReason? reason,
        string       workerId,
        string?      worktreePath,
        string       resultPath)
    {
        return (outcome, reason) switch
        {
            (OutcomeCode.Ok, null) =>
                new EscalationVerdict(
                    ProceedDownstream: true,
                    HumanMessage:      string.Empty,
                    TerminalOutcome:   OutcomeCode.Ok),

            (OutcomeCode.Failed, _) =>
                new EscalationVerdict(
                    ProceedDownstream: false,
                    HumanMessage:      $"{workerId}: one or more acceptance tests failed. " +
                                       $"Review {resultPath}.",
                    TerminalOutcome:   OutcomeCode.Failed),

            (OutcomeCode.Blocked, BlockReason.AmbiguousSpec) =>
                new EscalationVerdict(
                    ProceedDownstream: false,
                    HumanMessage:      $"{workerId}: spec was ambiguous. " +
                                       "Rewrite SpecPacket; do not redispatch as-is.",
                    TerminalOutcome:   OutcomeCode.Blocked),

            (OutcomeCode.Blocked, BlockReason.BuildFailed) =>
                new EscalationVerdict(
                    ProceedDownstream: false,
                    HumanMessage:      $"{workerId}: code did not build. " +
                                       $"See logs in {worktreePath ?? "(unknown)"}.",
                    TerminalOutcome:   OutcomeCode.Blocked),

            (OutcomeCode.Blocked,
             BlockReason.ToolError or BlockReason.Exception or BlockReason.SchemaMismatchOnOwnOutput) =>
                new EscalationVerdict(
                    ProceedDownstream: false,
                    HumanMessage:      $"{workerId}: halted on infrastructure issue. " +
                                       "Inspect, do not retry automatically.",
                    TerminalOutcome:   OutcomeCode.Blocked),

            (OutcomeCode.Blocked, BlockReason.BudgetExceeded or BlockReason.TimeboxExceeded) =>
                new EscalationVerdict(
                    ProceedDownstream: false,
                    HumanMessage:      $"{workerId}: exceeded a hard limit. " +
                                       "Reconsider scope before any follow-up.",
                    TerminalOutcome:   OutcomeCode.Blocked),

            (OutcomeCode.Blocked, BlockReason.MissingReferenceFile) =>
                new EscalationVerdict(
                    ProceedDownstream: false,
                    HumanMessage:      $"{workerId}: a reference file the spec named was not " +
                                       "available in the prompt context. Sonnets dispatched via " +
                                       "the orchestrator's API path cannot read filesystem files; " +
                                       "either inline the file content into the spec or run this " +
                                       "packet via Claude Code instead.",
                    TerminalOutcome:   OutcomeCode.Blocked),

            // Catch-all for any other Blocked combination — keep behaviour fail-closed but
            // do not crash. Surfaces the unknown reason verbatim so the operator can act on it.
            (OutcomeCode.Blocked, _) =>
                new EscalationVerdict(
                    ProceedDownstream: false,
                    HumanMessage:      $"{workerId}: blocked with reason={reason}. " +
                                       "No state-machine branch defined; treating as terminal blocked.",
                    TerminalOutcome:   OutcomeCode.Blocked),

            _ => throw new InvalidOperationException(
                $"Unhandled outcome/reason combination: outcome={outcome}, reason={reason}")
        };
    }
}
