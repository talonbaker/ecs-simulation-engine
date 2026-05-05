using System.Security.Cryptography;
using System.Text;
using Warden.Orchestrator.Persistence;
using Xunit;

namespace Warden.Orchestrator.Tests.Persistence;

/// <summary>
/// Unit tests for <see cref="ChainOfThoughtStore"/>.
/// </summary>
public sealed class ChainOfThoughtStoreTests : IDisposable
{
    private readonly string _tempDir;

    public ChainOfThoughtStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"wp10-cot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    private string Runs => Path.Combine(_tempDir, "runs");

    // -- AT-01 ---------------------------------------------------------------------

    /// <summary>
    /// AT-01: A successful mock run produces every file listed in the SRD layout.
    /// </summary>
    [Fact]
    public async Task AT01_SuccessfulRun_ProducesAllLayoutFiles()
    {
        const string runId     = "at01-run";
        const string sonnetId  = "sonnet-01";
        var store = new ChainOfThoughtStore(Runs);
        store.InitRun(runId);

        await store.PersistMissionAsync(runId, "# Mission\n\nTest mission.", default);
        await store.AppendEventAsync(runId,
            $"{{\"ts\":\"{DateTimeOffset.UtcNow:O}\",\"kind\":\"run-started\",\"runId\":\"{runId}\"}}",
            default);
        await store.PersistSpecAsync(runId, sonnetId,
            "{\"specId\":\"spec-smoke-01\",\"missionId\":\"m01\",\"title\":\"T\"}", default);
        await store.PersistPromptAsync(runId, sonnetId, "system prompt assembled", default);
        await store.PersistRawResponseAsync(runId, sonnetId, "{\"id\":\"resp-01\"}", default);
        await store.PersistResultAsync(runId, sonnetId, "{\"specId\":\"spec-01\"}", default);
        await store.AppendEventAsync(runId,
            $"{{\"ts\":\"{DateTimeOffset.UtcNow:O}\",\"kind\":\"run-completed\",\"exitCode\":0}}",
            default);

        Assert.True(File.Exists(RunLayout.MissionFile(Runs, runId)),
            "mission.md must exist");
        Assert.True(File.Exists(RunLayout.EventsFile(Runs, runId)),
            "events.jsonl must exist");
        Assert.True(File.Exists(RunLayout.SpecFile(Runs, runId, sonnetId)),
            "spec.json must exist");
        Assert.True(File.Exists(RunLayout.PromptFile(Runs, runId, sonnetId)),
            "prompt.txt must exist");
        Assert.True(File.Exists(RunLayout.RawResponseFile(Runs, runId, sonnetId)),
            "response.raw.json must exist");
        Assert.True(File.Exists(RunLayout.ResultFile(Runs, runId, sonnetId)),
            "result.json must exist");
    }

    // -- AT-05 ---------------------------------------------------------------------

    /// <summary>
    /// AT-05: Event lines are strictly append-only.
    /// Each append changes the SHA-256 hash of the file, and prior content is preserved.
    /// </summary>
    [Fact]
    public async Task AT05_EventsFile_IsStrictlyAppendOnly()
    {
        const string runId = "at05-run";
        var store = new ChainOfThoughtStore(Runs);
        store.InitRun(runId);

        var eventsPath = RunLayout.EventsFile(Runs, runId);

        var events = new[]
        {
            "{\"kind\":\"run-started\"}",
            "{\"kind\":\"sonnet-dispatched\",\"workerId\":\"sonnet-01\"}",
            "{\"kind\":\"sonnet-completed\",\"workerId\":\"sonnet-01\",\"outcome\":\"ok\"}",
        };

        byte[]? previousBytes = null;
        byte[]? previousHash  = null;

        foreach (var evt in events)
        {
            await store.AppendEventAsync(runId, evt, default);

            var currentBytes = await File.ReadAllBytesAsync(eventsPath);
            var currentHash  = SHA256.HashData(currentBytes);

            if (previousHash is not null)
            {
                Assert.NotEqual(previousHash, currentHash);
                Assert.True(currentBytes.Length > previousBytes!.Length,
                    "Events file must only grow in size.");
                Assert.Equal(previousBytes, currentBytes[..previousBytes.Length]);
            }

            previousBytes = currentBytes;
            previousHash  = currentHash;
        }

        var lines = await File.ReadAllLinesAsync(eventsPath);
        Assert.Equal(events.Length, lines.Length);
    }

    // -- AT-06 ---------------------------------------------------------------------

    /// <summary>
    /// AT-06: Run ID collision is rejected — InitRun throws when the run root already exists.
    /// </summary>
    [Fact]
    public void AT06_RunIdCollision_IsRejectedAtStartup()
    {
        const string runId = "collision-run";
        var store = new ChainOfThoughtStore(Runs);

        store.InitRun(runId);

        var ex = Assert.Throws<InvalidOperationException>(() => store.InitRun(runId));
        Assert.Contains("collision", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AT-06 (variant): A second concurrent <see cref="ChainOfThoughtStore"/> instance
    /// for the same runs root also rejects the same run ID once it is initialised.
    /// </summary>
    [Fact]
    public void AT06_RunIdCollision_DifferentStoreInstances_BothRejected()
    {
        const string runId = "concurrent-collision";
        var store1 = new ChainOfThoughtStore(Runs);
        var store2 = new ChainOfThoughtStore(Runs);

        store1.InitRun(runId);

        Assert.Throws<InvalidOperationException>(() => store2.InitRun(runId));
    }

    // -- Write-order invariant verification ----------------------------------------

    [Fact]
    public async Task WriteOrderInvariant_PromptBeforeResponse_ResponseBeforeResult()
    {
        const string runId    = "order-run";
        const string workerId = "sonnet-01";
        var store = new ChainOfThoughtStore(Runs);
        store.InitRun(runId);

        await store.PersistPromptAsync(runId, workerId, "prompt", default);
        var promptTime = File.GetLastWriteTimeUtc(RunLayout.PromptFile(Runs, runId, workerId));

        await Task.Delay(10);
        await store.PersistRawResponseAsync(runId, workerId, "{}", default);
        var responseTime = File.GetLastWriteTimeUtc(RunLayout.RawResponseFile(Runs, runId, workerId));

        await Task.Delay(10);
        await store.PersistResultAsync(runId, workerId, "{}", default);
        var resultTime = File.GetLastWriteTimeUtc(RunLayout.ResultFile(Runs, runId, workerId));

        Assert.True(promptTime <= responseTime, "prompt.txt must be written before response.raw.json");
        Assert.True(responseTime <= resultTime, "response.raw.json must be written before result.json");
    }
}
