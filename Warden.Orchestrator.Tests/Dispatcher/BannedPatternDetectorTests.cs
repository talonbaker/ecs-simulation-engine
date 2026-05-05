using Warden.Orchestrator.Dispatcher;
using Xunit;

namespace Warden.Orchestrator.Tests.Dispatcher;

/// <summary>
/// AT-04: Each of the six banned patterns has ≥1 positive test (should be flagged)
/// and ≥1 negative test (similar-but-legitimate code that must not be flagged).
/// </summary>
public sealed class BannedPatternDetectorTests
{
    // -- Pattern 1: using System.Net.Http outside Warden.Anthropic / Warden.Orchestrator --

    [Fact]
    public void P1_Positive_HttpNamespace_InAPIFramework_IsDetected()
    {
        var diff = BuildDiff("APIFramework/HttpHelpers.cs", "using System.Net.Http;");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.HttpNamespaceOutsideWarden);
    }

    [Fact]
    public void P1_Positive_HttpNamespace_InECSCli_IsDetected()
    {
        var diff = BuildDiff("ECSCli/Commands/SomeCommand.cs", "using System.Net.Http;");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.HttpNamespaceOutsideWarden);
    }

    [Fact]
    public void P1_Negative_HttpNamespace_InWardenAnthropic_IsAllowed()
    {
        var diff = BuildDiff("Warden.Anthropic/AnthropicClient.cs", "using System.Net.Http;");
        var detections = BannedPatternDetector.Scan(diff);
        AssertNotDetected(detections, BannedPatternKind.HttpNamespaceOutsideWarden);
    }

    [Fact]
    public void P1_Negative_HttpNamespace_InWardenOrchestrator_IsAllowed()
    {
        var diff = BuildDiff("Warden.Orchestrator/Infrastructure/SomeFile.cs",
            "using System.Net.Http;");
        var detections = BannedPatternDetector.Scan(diff);
        AssertNotDetected(detections, BannedPatternKind.HttpNamespaceOutsideWarden);
    }

    // -- Pattern 2: new AnthropicClient/HttpClient/WebSocketClient outside allowed projects --

    [Fact]
    public void P2_Positive_NewHttpClient_InWardenContracts_IsDetected()
    {
        var diff = BuildDiff("Warden.Contracts/SomeFile.cs",
            "var client = new HttpClient();");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.ClientConstructionOutsideWarden);
    }

    [Fact]
    public void P2_Positive_NewAnthropicClient_InECSCli_IsDetected()
    {
        var diff = BuildDiff("ECSCli/Commands/AiCommand.cs",
            "var ai = new AnthropicClient(apiKey);");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.ClientConstructionOutsideWarden);
    }

    [Fact]
    public void P2_Positive_NewWebSocketClient_InAPIFramework_IsDetected()
    {
        var diff = BuildDiff("APIFramework/WebSupport.cs",
            "var ws = new WebSocketClient(uri);");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.ClientConstructionOutsideWarden);
    }

    [Fact]
    public void P2_Negative_NewHttpClient_InWardenAnthropic_IsAllowed()
    {
        var diff = BuildDiff("Warden.Anthropic/HttpLayer.cs",
            "_http = new HttpClient();");
        var detections = BannedPatternDetector.Scan(diff);
        AssertNotDetected(detections, BannedPatternKind.ClientConstructionOutsideWarden);
    }

    [Fact]
    public void P2_Negative_UnrelatedNew_InAPIFramework_IsNotFlagged()
    {
        // "new MyEngine()" contains "new " but no banned client type.
        var diff = BuildDiff("APIFramework/EngineBootstrap.cs",
            "var engine = new SimulationEngine(config);");
        var detections = BannedPatternDetector.Scan(diff);
        AssertNotDetected(detections, BannedPatternKind.ClientConstructionOutsideWarden);
    }

    // -- Pattern 3: Process.Start that is not ECSCli ---------------------------

    [Fact]
    public void P3_Positive_ProcessStart_WithCmdExe_IsDetected()
    {
        var diff = BuildDiff("Warden.Contracts/Util.cs",
            "Process.Start(\"cmd.exe\", \"/c dir\");");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.UnauthorizedProcessStart);
    }

    [Fact]
    public void P3_Positive_ProcessStart_WithDotnet_IsDetected()
    {
        var diff = BuildDiff("APIFramework/Runner.cs",
            "Process.Start(\"dotnet\", \"run\");");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.UnauthorizedProcessStart);
    }

    [Fact]
    public void P3_Negative_ProcessStart_WithECSCli_IsAllowed()
    {
        var diff = BuildDiff("Warden.Orchestrator/Infrastructure/CliRunner.cs",
            "Process.Start(\"ECSCli\", args);");
        var detections = BannedPatternDetector.Scan(diff);
        AssertNotDetected(detections, BannedPatternKind.UnauthorizedProcessStart);
    }

    [Fact]
    public void P3_Negative_ProcessStart_WithECSCliPath_IsAllowed()
    {
        var diff = BuildDiff("Warden.Orchestrator/Infrastructure/CliRunner.cs",
            "Process.Start(ECSCli.ExecutablePath, cliArgs);");
        var detections = BannedPatternDetector.Scan(diff);
        AssertNotDetected(detections, BannedPatternKind.UnauthorizedProcessStart);
    }

    // -- Pattern 4: ANTHROPIC_API_KEY in Warden.* code outside Program.cs ------

    [Fact]
    public void P4_Positive_ApiKey_InWardenContracts_IsDetected()
    {
        var diff = BuildDiff("Warden.Contracts/Config.cs",
            "const string Key = \"ANTHROPIC_API_KEY\";");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.ApiKeyInWardenCode);
    }

    [Fact]
    public void P4_Positive_ApiKey_InWardenOrchestrator_NotProgram_IsDetected()
    {
        var diff = BuildDiff("Warden.Orchestrator/Infrastructure/CliRunner.cs",
            "Environment.GetEnvironmentVariable(\"ANTHROPIC_API_KEY\")");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.ApiKeyInWardenCode);
    }

    [Fact]
    public void P4_Negative_ApiKey_InProgramCs_IsAllowed()
    {
        var diff = BuildDiff("Warden.Orchestrator/Program.cs",
            "var key = Environment.GetEnvironmentVariable(\"ANTHROPIC_API_KEY\")");
        var detections = BannedPatternDetector.Scan(diff);
        AssertNotDetected(detections, BannedPatternKind.ApiKeyInWardenCode);
    }

    [Fact]
    public void P4_Negative_ApiKey_InNonWardenProject_IsNotFlagged()
    {
        // ECSCli is not Warden.* — reading the key there would be a different policy concern.
        var diff = BuildDiff("ECSCli/Program.cs",
            "var key = \"ANTHROPIC_API_KEY\";");
        var detections = BannedPatternDetector.Scan(diff);
        // Pattern 4 only covers Warden.* files.
        AssertNotDetected(detections, BannedPatternKind.ApiKeyInWardenCode);
    }

    // -- Pattern 5: new dependency in forbidden csproj files ------------------

    [Fact]
    public void P5_Positive_PackageRef_InAPIFrameworkCsproj_IsDetected()
    {
        var diff = BuildDiff("APIFramework/APIFramework.csproj",
            "<PackageReference Include=\"Warden.Orchestrator\" Version=\"1.0.0\" />");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.ForbiddenCsprojDependency);
    }

    [Fact]
    public void P5_Positive_ProjectRef_InECSCliCsproj_IsDetected()
    {
        var diff = BuildDiff("ECSCli/ECSCli.csproj",
            "<ProjectReference Include=\"..\\Warden.Contracts\\Warden.Contracts.csproj\" />");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.ForbiddenCsprojDependency);
    }

    [Fact]
    public void P5_Positive_PackageRef_InECSVisualizerCsproj_IsDetected()
    {
        var diff = BuildDiff("ECSVisualizer/ECSVisualizer.csproj",
            "<PackageReference Include=\"Anthropic.SDK\" Version=\"2.0.0\" />");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.ForbiddenCsprojDependency);
    }

    [Fact]
    public void P5_Negative_PackageRef_InWardenContractsCsproj_IsAllowed()
    {
        var diff = BuildDiff("Warden.Contracts/Warden.Contracts.csproj",
            "<PackageReference Include=\"System.Text.Json\" Version=\"8.0.0\" />");
        var detections = BannedPatternDetector.Scan(diff);
        AssertNotDetected(detections, BannedPatternKind.ForbiddenCsprojDependency);
    }

    [Fact]
    public void P5_Negative_PropertyGroup_InECSCliCsproj_IsNotFlagged()
    {
        // A non-reference change in the forbidden csproj is fine.
        var diff = BuildDiff("ECSCli/ECSCli.csproj",
            "<TargetFramework>net8.0</TargetFramework>");
        var detections = BannedPatternDetector.Scan(diff);
        AssertNotDetected(detections, BannedPatternKind.ForbiddenCsprojDependency);
    }

    // -- Pattern 6: Task.Run calling back into orchestrator types -------------

    [Fact]
    public void P6_Positive_TaskRun_WithDispatcher_IsDetected()
    {
        var diff = BuildDiff("Warden.Orchestrator/RunCommand.cs",
            "await Task.Run(() => _dispatcher.RunAsync(spec, ct));");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.TaskRunCallsOrchestrator);
    }

    [Fact]
    public void P6_Positive_TaskRun_WithOrchestrator_InEngineCode_IsDetected()
    {
        var diff = BuildDiff("APIFramework/EngineLoop.cs",
            "Task.Run(() => new SonnetOrchestrator().Start());");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.TaskRunCallsOrchestrator);
    }

    [Fact]
    public void P6_Positive_TaskRun_WithScheduler_IsDetected()
    {
        var diff = BuildDiff("ECSCli/Commands/AiCommand.cs",
            "var t = Task.Run(() => batchScheduler.Submit(batch));");
        var detections = BannedPatternDetector.Scan(diff);
        AssertDetected(detections, BannedPatternKind.TaskRunCallsOrchestrator);
    }

    [Fact]
    public void P6_Negative_TaskRun_WithPureIo_IsNotFlagged()
    {
        var diff = BuildDiff("Warden.Orchestrator/Infrastructure/ChainOfThoughtStore.cs",
            "await Task.Run(() => File.WriteAllText(path, json));");
        var detections = BannedPatternDetector.Scan(diff);
        AssertNotDetected(detections, BannedPatternKind.TaskRunCallsOrchestrator);
    }

    [Fact]
    public void P6_Negative_TaskRun_WithMath_IsNotFlagged()
    {
        var diff = BuildDiff("APIFramework/SimMath.cs",
            "var result = await Task.Run(() => ComputeExpensiveSum(data));");
        var detections = BannedPatternDetector.Scan(diff);
        AssertNotDetected(detections, BannedPatternKind.TaskRunCallsOrchestrator);
    }

    // -- HasBannedPattern surface ----------------------------------------------

    [Fact]
    public void HasBannedPattern_ReturnsFalse_ForCleanDiff()
    {
        var diff = BuildDiff("Warden.Contracts/SomeFile.cs",
            "public int Answer => 42;");
        Assert.False(BannedPatternDetector.HasBannedPattern(diff));
    }

    [Fact]
    public void HasBannedPattern_ReturnsTrue_WhenAnyPatternMatches()
    {
        var diff = BuildDiff("ECSCli/Hack.cs",
            "using System.Net.Http;");
        Assert.True(BannedPatternDetector.HasBannedPattern(diff));
    }

    [Fact]
    public void EmptyDiff_ProducesNoDetections()
    {
        var detections = BannedPatternDetector.Scan(string.Empty);
        Assert.Empty(detections);
    }

    [Fact]
    public void RemovedLines_AreIgnored_EvenIfTheyMatchBannedPattern()
    {
        // A line prefixed with "-" is a deletion — must not be flagged.
        var diff =
            "diff --git a/APIFramework/Old.cs b/APIFramework/Old.cs\n" +
            "--- a/APIFramework/Old.cs\n" +
            "+++ b/APIFramework/Old.cs\n" +
            "@@ -1,2 +1,1 @@\n" +
            "-using System.Net.Http;\n" +   // removed — should not fire
            " public class Foo {}\n";
        var detections = BannedPatternDetector.Scan(diff);
        Assert.Empty(detections);
    }

    // -- Helpers ---------------------------------------------------------------

    private static void AssertDetected(
        IReadOnlyList<BannedDetection> detections,
        BannedPatternKind              kind)
    {
        Assert.Contains(detections, d => d.Kind == kind);
    }

    private static void AssertNotDetected(
        IReadOnlyList<BannedDetection> detections,
        BannedPatternKind              kind)
    {
        Assert.DoesNotContain(detections, d => d.Kind == kind);
    }

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
