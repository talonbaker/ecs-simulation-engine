using Warden.Contracts.Handshake;
using Warden.Orchestrator.Dispatcher;
using Xunit;

namespace Warden.Orchestrator.Tests.Dispatcher;

public sealed class FailClosedEscalatorTests
{
    // -- AT-01: every enumerated outcome/reason combination has a handled branch -

    [Theory]
    [InlineData(OutcomeCode.Ok,      null,                              true,  OutcomeCode.Ok)]
    [InlineData(OutcomeCode.Failed,  null,                              false, OutcomeCode.Failed)]
    [InlineData(OutcomeCode.Failed,  BlockReason.ToolError,             false, OutcomeCode.Failed)]
    [InlineData(OutcomeCode.Blocked, BlockReason.AmbiguousSpec,         false, OutcomeCode.Blocked)]
    [InlineData(OutcomeCode.Blocked, BlockReason.BuildFailed,           false, OutcomeCode.Blocked)]
    [InlineData(OutcomeCode.Blocked, BlockReason.ToolError,             false, OutcomeCode.Blocked)]
    [InlineData(OutcomeCode.Blocked, BlockReason.Exception,             false, OutcomeCode.Blocked)]
    [InlineData(OutcomeCode.Blocked, BlockReason.SchemaMismatchOnOwnOutput, false, OutcomeCode.Blocked)]
    [InlineData(OutcomeCode.Blocked, BlockReason.BudgetExceeded,        false, OutcomeCode.Blocked)]
    [InlineData(OutcomeCode.Blocked, BlockReason.TimeboxExceeded,       false, OutcomeCode.Blocked)]
    public void AT01_AllEnumeratedCombinations_HaveHandledBranch(
        OutcomeCode  outcome,
        BlockReason? reason,
        bool         expectProceed,
        OutcomeCode  expectTerminal)
    {
        var result  = MakeSonnetResult(outcome, reason);
        var verdict = FailClosedEscalator.Evaluate(result);

        Assert.Equal(expectProceed,  verdict.ProceedDownstream);
        Assert.Equal(expectTerminal, verdict.TerminalOutcome);
    }

    // -- AT-02: property test — ProceedDownstream=true only for ok/null ---------

    [Fact]
    public void AT02_PropertyTest_NoBranch_ReturnsProceedDownstream_WhenOutcomeIsNotOk()
    {
        var rng     = new Random(42);
        var outcomes = Enum.GetValues<OutcomeCode>();
        var reasons  = Enum.GetValues<BlockReason>().Cast<BlockReason?>().Append(null).ToArray();
        int tested   = 0;

        while (tested < 500)
        {
            var outcome = outcomes[rng.Next(outcomes.Length)];
            var reason  = reasons[rng.Next(reasons.Length)];

            // Skip the one combination that is supposed to return true.
            if (outcome == OutcomeCode.Ok && reason is null)
                continue;

            var result = MakeSonnetResult(outcome, reason);
            tested++;

            EscalationVerdict verdict;
            try
            {
                verdict = FailClosedEscalator.Evaluate(result);
            }
            catch (InvalidOperationException)
            {
                // Throwing on an unrecognised combination is also fail-closed.
                continue;
            }

            Assert.False(verdict.ProceedDownstream,
                $"iteration {tested}: outcome={outcome}, reason={reason} " +
                $"unexpectedly returned ProceedDownstream=true");
        }
    }

    // -- AT-03: blocked propagates even if a sibling succeeded -----------------

    [Fact]
    public void AT03_MostSevere_Blocked_WinsOver_Ok_And_Failed()
    {
        var codes = new[] { OutcomeCode.Ok, OutcomeCode.Failed, OutcomeCode.Blocked };
        Assert.Equal(OutcomeCode.Blocked, FailClosedEscalator.MostSevere(codes));
    }

    [Fact]
    public void AT03_MostSevere_Failed_WinsOver_Ok()
    {
        var codes = new[] { OutcomeCode.Ok, OutcomeCode.Failed };
        Assert.Equal(OutcomeCode.Failed, FailClosedEscalator.MostSevere(codes));
    }

    [Fact]
    public void AT03_MostSevere_AllOk_ReturnsOk()
    {
        var codes = new[] { OutcomeCode.Ok, OutcomeCode.Ok };
        Assert.Equal(OutcomeCode.Ok, FailClosedEscalator.MostSevere(codes));
    }

    [Fact]
    public void AT03_MostSevere_SingleBlocked_Propagates()
    {
        var codes = new[] { OutcomeCode.Ok, OutcomeCode.Ok, OutcomeCode.Blocked, OutcomeCode.Ok };
        Assert.Equal(OutcomeCode.Blocked, FailClosedEscalator.MostSevere(codes));
    }

    // -- AT-05: ok result overridden to blocked/tool-error when diff has banned pattern -

    [Fact]
    public void AT05_OkResult_OverriddenToBlocked_WhenDiffHasBannedPattern()
    {
        var result = MakeSonnetResult(OutcomeCode.Ok, null);
        // Diff with a banned pattern: Process.Start without ECSCli
        var bannedDiff = BuildDiff("ECSCli/SomeFile.cs",
            "Process.Start(\"cmd.exe\", \"/c dir\");");

        var verdict = FailClosedEscalator.Evaluate(result, worktreeDiff: bannedDiff);

        Assert.False(verdict.ProceedDownstream);
        Assert.Equal(OutcomeCode.Blocked, verdict.TerminalOutcome);
        Assert.False(string.IsNullOrWhiteSpace(verdict.HumanMessage));
    }

    [Fact]
    public void AT05_OkResult_NotOverridden_WhenDiffIsClean()
    {
        var result    = MakeSonnetResult(OutcomeCode.Ok, null);
        var cleanDiff = BuildDiff("Warden.Contracts/SomeFile.cs",
            "public int Foo => 42;");

        var verdict = FailClosedEscalator.Evaluate(result, worktreeDiff: cleanDiff);

        Assert.True(verdict.ProceedDownstream);
        Assert.Equal(OutcomeCode.Ok, verdict.TerminalOutcome);
    }

    // -- AT-06: HumanMessage is present and non-empty on every non-ok path ------

    [Theory]
    [InlineData(OutcomeCode.Failed,  null)]
    [InlineData(OutcomeCode.Blocked, BlockReason.AmbiguousSpec)]
    [InlineData(OutcomeCode.Blocked, BlockReason.BuildFailed)]
    [InlineData(OutcomeCode.Blocked, BlockReason.ToolError)]
    [InlineData(OutcomeCode.Blocked, BlockReason.Exception)]
    [InlineData(OutcomeCode.Blocked, BlockReason.SchemaMismatchOnOwnOutput)]
    [InlineData(OutcomeCode.Blocked, BlockReason.BudgetExceeded)]
    [InlineData(OutcomeCode.Blocked, BlockReason.TimeboxExceeded)]
    public void AT06_HumanMessage_IsPresent_OnEveryNonOkPath(
        OutcomeCode outcome, BlockReason? reason)
    {
        var result  = MakeSonnetResult(outcome, reason);
        var verdict = FailClosedEscalator.Evaluate(result);

        Assert.False(string.IsNullOrWhiteSpace(verdict.HumanMessage),
            $"outcome={outcome}, reason={reason}: expected non-empty HumanMessage");
    }

    [Fact]
    public void AT06_HumanMessage_ContainsWorkerId_OnFailedPath()
    {
        var result  = MakeSonnetResult(OutcomeCode.Failed, null, workerId: "sonnet-03");
        var verdict = FailClosedEscalator.Evaluate(result);

        Assert.Contains("sonnet-03", verdict.HumanMessage);
    }

    [Fact]
    public void AT06_HaikuResult_FailedPath_HasNonEmptyHumanMessage()
    {
        var result  = MakeHaikuResult(OutcomeCode.Failed, null);
        var verdict = FailClosedEscalator.Evaluate(result);

        Assert.False(string.IsNullOrWhiteSpace(verdict.HumanMessage));
        Assert.False(verdict.ProceedDownstream);
    }

    // -- Extra: unhandled combination throws -----------------------------------

    [Fact]
    public void UnhandledCombination_Throws_InvalidOperationException()
    {
        // Ok with a reason is not a defined state — must throw.
        var result = MakeSonnetResult(OutcomeCode.Ok, BlockReason.BuildFailed);
        Assert.Throws<InvalidOperationException>(() => FailClosedEscalator.Evaluate(result));
    }

    // -- Helpers ---------------------------------------------------------------

    private static SonnetResult MakeSonnetResult(
        OutcomeCode  outcome,
        BlockReason? reason,
        string       workerId     = "sonnet-01",
        string?      worktreePath = "/runs/r1/sonnet-01/worktree")
        => new()
        {
            SchemaVersion         = "0.1.0",
            SpecId                = "spec-01",
            WorkerId              = workerId,
            Outcome               = outcome,
            BlockReason           = reason,
            WorktreePath          = worktreePath,
            AcceptanceTestResults = [],
            TokensUsed            = new TokenUsage()
        };

    private static HaikuResult MakeHaikuResult(OutcomeCode outcome, BlockReason? reason)
        => new()
        {
            SchemaVersion    = "0.1.0",
            ScenarioId       = "sc-01",
            ParentBatchId    = "batch-01",
            WorkerId         = "haiku-01",
            Outcome          = outcome,
            BlockReason      = reason,
            AssertionResults = [],
            TokensUsed       = new TokenUsage()
        };

    /// <summary>
    /// Builds a minimal unified diff string with one added line in the given file.
    /// </summary>
    private static string BuildDiff(string filePath, string addedLine)
        => $"""
            diff --git a/{filePath} b/{filePath}
            --- a/{filePath}
            +++ b/{filePath}
            @@ -1,1 +1,2 @@
             existing line
            +{addedLine}
            """;
}
