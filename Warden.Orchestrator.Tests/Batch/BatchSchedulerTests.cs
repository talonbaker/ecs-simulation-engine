using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Warden.Anthropic;
using Warden.Contracts;
using Warden.Contracts.Handshake;
using Warden.Orchestrator.Batch;
using Warden.Orchestrator.Cache;
using Warden.Orchestrator.Infrastructure;
using Xunit;

namespace Warden.Orchestrator.Tests.Batch;

// -- In-process HTTP stub -------------------------------------------------------

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => _respond = respond;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken  cancellationToken)
        => Task.FromResult(_respond(request));
}

// -- JSON fixture helpers -------------------------------------------------------

internal static class Fixtures
{
    internal static HttpResponseMessage JsonOk(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    internal static string BatchSubmissionJson(string id = "batch-test-001") => $$"""
        {
          "id": "{{id}}",
          "type": "message_batch",
          "processing_status": "in_progress",
          "created_at": "2026-04-24T00:00:00Z",
          "results_url": null
        }
        """;

    internal static string InProgressStatusJson(
        string id         = "batch-test-001",
        int    processing = 1) => $$"""
        {
          "id": "{{id}}",
          "processing_status": "in_progress",
          "request_counts": {
            "processing": {{processing}},
            "succeeded": 0,
            "errored": 0,
            "canceled": 0,
            "expired": 0
          },
          "ended_at": null,
          "created_at": "2026-04-24T00:00:00Z",
          "expires_at": "2026-04-25T00:00:00Z",
          "results_url": null
        }
        """;

    internal static string EndedStatusJson(
        string  id         = "batch-test-001",
        string? resultsUrl = null) => $$"""
        {
          "id": "{{id}}",
          "processing_status": "ended",
          "request_counts": {
            "processing": 0,
            "succeeded": 1,
            "errored": 0,
            "canceled": 0,
            "expired": 0
          },
          "ended_at": "2026-04-24T01:00:00Z",
          "created_at": "2026-04-24T00:00:00Z",
          "expires_at": "2026-04-25T00:00:00Z",
          "results_url": "{{resultsUrl ?? $"https://api.anthropic.com/v1/messages/batches/{id}/results"}}"
        }
        """;

    /// <summary>
    /// Builds a JSONL results stream where every entry is a valid succeeded HaikuResult.
    /// <paramref name="scenarioBatchId"/> is used as the batch-id half of the composite
    /// <c>custom_id</c> ("<c>scenarioBatchId::scenarioId</c>") that <see cref="BatchScheduler"/>
    /// submits to Anthropic and expects back verbatim in the results stream.
    /// </summary>
    internal static string BuildResultsJsonl(
        IEnumerable<(string ScenarioId, string HaikuId)> entries,
        string batchId         = "batch-test-001",
        string scenarioBatchId = "fixture-batch")
    {
        var lines = entries.Select(e =>
        {
            var haikuResultJson = HaikuResultJson(e.ScenarioId, batchId, e.HaikuId);
            // Serialize the string to produce a properly JSON-escaped string literal
            var textValue = JsonSerializer.Serialize(haikuResultJson);
            // Avoid consecutive-brace CS9007 by building via string.Concat
            var msgJson =
                @"{""id"":""msg_test"",""type"":""message"",""role"":""assistant""," +
                @"""content"":[{""type"":""text"",""text"":" + textValue + @"}]," +
                @"""model"":""claude-haiku-4-5-20251001""," +
                @"""stop_reason"":""end_turn"",""stop_sequence"":null," +
                @"""usage"":{""input_tokens"":100,""cache_creation_input_tokens"":0,""cache_read_input_tokens"":0,""output_tokens"":50}}";
            // composite custom_id: "<scenarioBatchId>::<scenarioId>"
            var customId = scenarioBatchId + "::" + e.ScenarioId;
            return
                @"{""custom_id"":""" + customId +
                @""",""result"":{""type"":""succeeded"",""message"":" + msgJson + @"}}";
        });
        return string.Join("\n", lines);
    }

    /// <summary>Builds a valid HaikuResult JSON string that passes schema validation.</summary>
    internal static string HaikuResultJson(
        string scenarioId,
        string batchId  = "batch-test-001",
        string workerId = "haiku-01") => $$"""
        {
          "schemaVersion": "0.1.0",
          "scenarioId": "{{scenarioId}}",
          "parentBatchId": "{{batchId}}",
          "workerId": "{{workerId}}",
          "outcome": "ok",
          "assertionResults": [{"id": "A-01", "passed": true}],
          "tokensUsed": {"input": 100, "cachedRead": 0, "output": 50}
        }
        """;

    internal static ScenarioBatch MakeBatch(params ScenarioDto[] scenarios)
        => new()
        {
            BatchId      = "fixture-batch",
            ParentSpecId = "spec-01",
            Scenarios    = scenarios.ToList()
        };

    internal static ScenarioDto MakeScenario(string scenarioId, int seed = 0)
        => new()
        {
            ScenarioId          = scenarioId,
            Seed                = seed,
            DurationGameSeconds = 60.0,
            Assertions = new List<ScenarioAssertionDto>
            {
                new() { Id = "A-01", Kind = AssertionKind.AtEnd, Target = "entity-count" }
            }
        };
}

// -- Tests ----------------------------------------------------------------------

public sealed class BatchSchedulerTests : IDisposable
{
    private readonly string _tempDir;

    public BatchSchedulerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"wp07-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    /// <summary>
    /// Creates a fully wired <see cref="BatchScheduler"/> whose <see cref="AnthropicClient"/>
    /// routes through the given handler. Caller is responsible for disposing the returned client.
    /// </summary>
    private (BatchScheduler Scheduler, AnthropicClient Client) Wire(
        HttpMessageHandler handler,
        BatchPoller?       poller = null)
    {
        var client    = new AnthropicClient("test-key", handler, baseUrl: "https://api.anthropic.com/");
        var cache     = new PromptCacheManager();
        var cot       = new ChainOfThoughtStore(_tempDir);
        var ledger    = new CostLedger(Path.Combine(_tempDir, "cost-ledger.jsonl"));
        var log       = NullLogger<BatchScheduler>.Instance;
        var scheduler = poller is null
            ? new BatchScheduler(client, cache, cot, ledger, log)
            : new BatchScheduler(client, cache, cot, ledger, log, poller);

        return (scheduler, client);
    }

    private static BatchPoller FastPoller()
        => new(sleep: (_, _) => Task.CompletedTask);

    // -- AT-01 ---------------------------------------------------------------------

    [Fact]
    public async Task AT01_RunAsync_Submits_Exactly_One_Batch_For_25_Unique_Scenarios()
    {
        const string batchId = "batch-test-001";
        int batchCallCount = 0;
        int entriesSubmitted = 0;

        var scenarios = Enumerable.Range(1, 25)
            .Select(i => Fixtures.MakeScenario($"sc-{i:D2}", seed: i))
            .ToArray();

        var resultsJsonl = Fixtures.BuildResultsJsonl(
            scenarios.Select((s, i) => (s.ScenarioId, HaikuId: $"haiku-{i + 1:D2}")),
            batchId);

        var handler = new StubHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (req.Method == HttpMethod.Post && path.Contains("batches"))
            {
                batchCallCount++;
                var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(body);
                entriesSubmitted = doc.RootElement.GetProperty("requests").GetArrayLength();
                return Fixtures.JsonOk(Fixtures.BatchSubmissionJson(batchId));
            }

            if (req.Method == HttpMethod.Get && path.EndsWith("/results"))
                return Fixtures.JsonOk(resultsJsonl);

            if (req.Method == HttpMethod.Get && path.Contains("batches"))
                return Fixtures.JsonOk(Fixtures.EndedStatusJson(batchId));

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var (scheduler, client) = Wire(handler, FastPoller());
        using (client)
        {
            var results = await scheduler.RunAsync(
                "run-at01",
                new[] { Fixtures.MakeBatch(scenarios) },
                CancellationToken.None);

            Assert.Equal(1, batchCallCount);
            Assert.Equal(25, entriesSubmitted);
            Assert.Equal(25, results.Count);
        }
    }

    // -- AT-02 ---------------------------------------------------------------------

    [Fact]
    public async Task AT02_Identical_Scenarios_Produce_One_Batch_Entry_And_N_Results()
    {
        const string batchId = "batch-test-001";
        int entriesSubmitted = 0;

        // sc-01, sc-02, sc-03 unique; sc-04 and sc-05 are content-identical to sc-01
        var sc01 = Fixtures.MakeScenario("sc-01", seed: 1);
        var sc02 = Fixtures.MakeScenario("sc-02", seed: 2);
        var sc03 = Fixtures.MakeScenario("sc-03", seed: 3);
        var sc04 = sc01 with { ScenarioId = "sc-04" };
        var sc05 = sc01 with { ScenarioId = "sc-05" };

        // Batch returns results only for the 3 unique scenarios
        var resultsJsonl = Fixtures.BuildResultsJsonl(
            new[] { ("sc-01", "haiku-01"), ("sc-02", "haiku-02"), ("sc-03", "haiku-03") },
            batchId);

        var handler = new StubHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (req.Method == HttpMethod.Post && path.Contains("batches"))
            {
                var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(body);
                entriesSubmitted = doc.RootElement.GetProperty("requests").GetArrayLength();
                return Fixtures.JsonOk(Fixtures.BatchSubmissionJson(batchId));
            }

            if (req.Method == HttpMethod.Get && path.EndsWith("/results"))
                return Fixtures.JsonOk(resultsJsonl);

            if (req.Method == HttpMethod.Get && path.Contains("batches"))
                return Fixtures.JsonOk(Fixtures.EndedStatusJson(batchId));

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var (scheduler, client) = Wire(handler, FastPoller());
        using (client)
        {
            var results = await scheduler.RunAsync(
                "run-at02",
                new[] { Fixtures.MakeBatch(sc01, sc02, sc03, sc04, sc05) },
                CancellationToken.None);

            Assert.Equal(3, entriesSubmitted);  // only 3 unique scenarios sent to the API
            Assert.Equal(5, results.Count);      // all 5 inputs get a result

            // Duplicates share the same outcome and token counts as their original
            Assert.Equal(results[0].Outcome,           results[3].Outcome);
            Assert.Equal(results[0].Outcome,           results[4].Outcome);
            Assert.Equal(results[0].TokensUsed.Input,  results[3].TokensUsed.Input);
            Assert.Equal(results[0].TokensUsed.Output, results[4].TokensUsed.Output);
        }
    }

    // -- AT-03 ---------------------------------------------------------------------

    [Fact]
    public async Task AT03_More_Than_25_Scenarios_Throws_InvalidOperationException_With_Clear_Message()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var (scheduler, client) = Wire(handler, FastPoller());
        using (client)
        {
            var scenarios = Enumerable.Range(1, 26)
                .Select(i => Fixtures.MakeScenario($"sc-{i:D3}", seed: i))
                .ToArray();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => scheduler.RunAsync(
                    "run-at03",
                    new[] { Fixtures.MakeBatch(scenarios) },
                    CancellationToken.None));

            Assert.Contains("26",  ex.Message);  // actual count
            Assert.Contains("25",  ex.Message);  // limit
        }
    }

    // -- AT-04 ---------------------------------------------------------------------

    [Fact]
    public void AT04_BatchPoller_Default_Interval_Is_60_Seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), BatchPoller.ComputeInterval(0));
    }

    [Fact]
    public async Task AT04_BatchPoller_Backs_Off_To_5_Minutes_After_10_Consecutive_No_Change_Polls()
    {
        // Unit-level assertion: ComputeInterval(10) == 5min
        Assert.Equal(TimeSpan.FromMinutes(5), BatchPoller.ComputeInterval(10));

        // Integration: run a real poll loop and verify the recorded delay sequence
        var recordedDelays = new List<TimeSpan>();
        var poller = new BatchPoller(sleep: (delay, _) =>
        {
            recordedDelays.Add(delay);
            return Task.CompletedTask;
        });

        int pollCount = 0;
        const string batchId  = "batch-test-001";
        const string resultsUrl =
            "https://api.anthropic.com/v1/messages/batches/batch-test-001/results";

        var handler = new StubHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (req.Method != HttpMethod.Get || !path.Contains("batches"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            pollCount++;
            return pollCount > 12
                ? Fixtures.JsonOk(Fixtures.EndedStatusJson(batchId, resultsUrl))
                : Fixtures.JsonOk(Fixtures.InProgressStatusJson(batchId));
        });

        using var client = new AnthropicClient("test-key", handler,
            baseUrl: "https://api.anthropic.com/");

        await poller.PollUntilEndedAsync(client, batchId, CancellationToken.None);

        // sleep[0]  = ComputeInterval(0) = 60s  (before poll 1; lastCounts=null → noChange=0)
        // sleep[1]  = ComputeInterval(0) = 60s  (before poll 2; after poll 1 noChange stays 0)
        // sleep[2]  = ComputeInterval(1) = 120s (before poll 3; after poll 2 noChange=1)
        // ...
        // sleep[11] = ComputeInterval(10)= 5min (before poll 12; after poll 11 noChange=10)
        Assert.True(recordedDelays.Count >= 12,
            $"Expected ≥ 12 sleep calls, got {recordedDelays.Count}.");
        Assert.Equal(TimeSpan.FromSeconds(60), recordedDelays[0]);
        Assert.Equal(TimeSpan.FromMinutes(5),  recordedDelays[11]);
    }

    // -- AT-05 ---------------------------------------------------------------------

    [Fact]
    public async Task AT05_Cancellation_Stops_Poll_Loop_Within_2_Seconds()
    {
        const string batchId = "batch-test-001";

        // The batch submits instantly; status always returns in_progress
        var handler = new StubHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (req.Method == HttpMethod.Post && path.Contains("batches"))
                return Fixtures.JsonOk(Fixtures.BatchSubmissionJson(batchId));

            if (req.Method == HttpMethod.Get && path.Contains("batches"))
                return Fixtures.JsonOk(Fixtures.InProgressStatusJson(batchId));

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        // Use the default BatchPoller (real Task.Delay with a 60s first interval);
        // the CancellationToken fires at ~200ms, cutting the sleep short.
        var (scheduler, client) = Wire(handler, poller: null);
        using (client)
        {
            var cts  = new CancellationTokenSource();
            var sw   = Stopwatch.StartNew();
            var task = scheduler.RunAsync(
                "run-at05",
                new[] { Fixtures.MakeBatch(Fixtures.MakeScenario("sc-01", seed: 1)) },
                cts.Token);

            cts.CancelAfter(TimeSpan.FromMilliseconds(200));
            // TaskCanceledException derives from OperationCanceledException
            var ex = await Record.ExceptionAsync(() => task);
            Assert.True(ex is OperationCanceledException,
                $"Expected OperationCanceledException (or subclass), got: {ex?.GetType().Name ?? "null"}");
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 2000,
                $"Expected cancellation within 2 s; elapsed {sw.ElapsedMilliseconds} ms.");
        }
    }

    // -- AT-06 ---------------------------------------------------------------------

    [Fact]
    public async Task AT06_Malformed_HaikuResult_JSON_Becomes_Blocked_Result_Not_Exception()
    {
        const string batchId = "batch-test-001";
        // Text content is not valid JSON — ParseSucceeded should produce a blocked stub.
        // custom_id uses the composite format that BatchScheduler submits and expects back.
        const string jsonlLine =
            """{"custom_id":"fixture-batch::sc-01","result":{"type":"succeeded","message":{"id":"m","type":"message","role":"assistant","content":[{"type":"text","text":"not valid json at all"}],"model":"claude-haiku-4-5-20251001","stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":5,"cache_creation_input_tokens":0,"cache_read_input_tokens":0,"output_tokens":3}}}}""";

        var handler = new StubHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (req.Method == HttpMethod.Post && path.Contains("batches"))
                return Fixtures.JsonOk(Fixtures.BatchSubmissionJson(batchId));

            if (req.Method == HttpMethod.Get && path.EndsWith("/results"))
                return Fixtures.JsonOk(jsonlLine);

            if (req.Method == HttpMethod.Get && path.Contains("batches"))
                return Fixtures.JsonOk(Fixtures.EndedStatusJson(batchId));

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var (scheduler, client) = Wire(handler, FastPoller());
        using (client)
        {
            var results = await scheduler.RunAsync(
                "run-at06",
                new[] { Fixtures.MakeBatch(Fixtures.MakeScenario("sc-01", seed: 1)) },
                CancellationToken.None);

            Assert.Single(results);
            Assert.Equal(OutcomeCode.Blocked,    results[0].Outcome);
            Assert.Equal(BlockReason.ToolError,  results[0].BlockReason);
        }
    }

    // -- AT-07 ---------------------------------------------------------------------

    [Fact]
    public async Task AT07_Cost_Ledger_Receives_One_Line_Per_Unique_Haiku_Call_Not_Per_Duplicate()
    {
        const string batchId = "batch-test-001";

        var sc01 = Fixtures.MakeScenario("sc-01", seed: 10);
        var sc02 = Fixtures.MakeScenario("sc-02", seed: 20);
        var sc03 = sc01 with { ScenarioId = "sc-03" };  // dup of sc-01
        var sc04 = Fixtures.MakeScenario("sc-04", seed: 40);
        var sc05 = sc01 with { ScenarioId = "sc-05" };  // dup of sc-01

        var resultsJsonl = Fixtures.BuildResultsJsonl(
            new[] { ("sc-01", "haiku-01"), ("sc-02", "haiku-02"), ("sc-04", "haiku-04") },
            batchId);

        var handler = new StubHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (req.Method == HttpMethod.Post && path.Contains("batches"))
                return Fixtures.JsonOk(Fixtures.BatchSubmissionJson(batchId));

            if (req.Method == HttpMethod.Get && path.EndsWith("/results"))
                return Fixtures.JsonOk(resultsJsonl);

            if (req.Method == HttpMethod.Get && path.Contains("batches"))
                return Fixtures.JsonOk(Fixtures.EndedStatusJson(batchId));

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var (scheduler, client) = Wire(handler, FastPoller());
        using (client)
        {
            await scheduler.RunAsync(
                "run-at07",
                new[] { Fixtures.MakeBatch(sc01, sc02, sc03, sc04, sc05) },
                CancellationToken.None);
        }

        var ledgerPath = Path.Combine(_tempDir, "cost-ledger.jsonl");
        Assert.True(File.Exists(ledgerPath), "Ledger file must exist after RunAsync.");

        var ledger  = new CostLedger(ledgerPath);
        var entries = await ledger.ReadAllAsync();

        // 3 unique Haiku calls (sc-01, sc-02, sc-04); sc-03 and sc-05 are duplicates
        Assert.Equal(3, entries.Count);
        Assert.All(entries, e => Assert.Equal("haiku", e.Tier));
    }

    // -- AT-08 ---------------------------------------------------------------------

    [Fact]
    public async Task AT08_Five_Scenario_Run_Produces_Five_Haiku_Directories_With_Both_Files()
    {
        const string batchId = "batch-test-001";

        var scenarios = Enumerable.Range(1, 5)
            .Select(i => Fixtures.MakeScenario($"sc-0{i}", seed: i * 10))
            .ToArray();

        var resultsJsonl = Fixtures.BuildResultsJsonl(
            scenarios.Select((s, i) => (s.ScenarioId, HaikuId: $"haiku-{i + 1:D2}")),
            batchId);

        var handler = new StubHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (req.Method == HttpMethod.Post && path.Contains("batches"))
                return Fixtures.JsonOk(Fixtures.BatchSubmissionJson(batchId));

            if (req.Method == HttpMethod.Get && path.EndsWith("/results"))
                return Fixtures.JsonOk(resultsJsonl);

            if (req.Method == HttpMethod.Get && path.Contains("batches"))
                return Fixtures.JsonOk(Fixtures.EndedStatusJson(batchId));

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var (scheduler, client) = Wire(handler, FastPoller());
        using (client)
        {
            await scheduler.RunAsync(
                "run-at08",
                new[] { Fixtures.MakeBatch(scenarios) },
                CancellationToken.None);
        }

        var cot = new ChainOfThoughtStore(_tempDir);
        for (int i = 1; i <= 5; i++)
        {
            var haikuId = $"haiku-{i:D2}";
            var dir     = cot.HaikuDir("run-at08", haikuId);

            Assert.True(File.Exists(Path.Combine(dir, "scenario.json")),
                $"{haikuId}/scenario.json should exist.");
            Assert.True(File.Exists(Path.Combine(dir, "result.json")),
                $"{haikuId}/result.json should exist.");
        }
    }

    // -- AT_BS_X_TwoBatches --------------------------------------------------------

    /// <summary>
    /// Two ScenarioBatches with overlapping scenario IDs (sc-01..sc-03 in each) must
    /// both dispatch and return without throwing ArgumentException. The composite-key
    /// design ensures each (batchId, scenarioId) pair is unique in all lookup tables.
    /// </summary>
    [Fact]
    public async Task AT_BS_X_TwoBatches_SameScenarioIds_BothDispatchAndReturn()
    {
        const string anthropicBatchId = "batch-test-001";

        // Two batches with overlapping sc-01..sc-03, but different seeds → different
        // content hashes → neither batch's scenarios are deduped against the other's.
        var batchA = new ScenarioBatch
        {
            BatchId      = "batch-a",
            ParentSpecId = "spec-a",
            Scenarios    = new List<ScenarioDto>
            {
                Fixtures.MakeScenario("sc-01", seed: 1),
                Fixtures.MakeScenario("sc-02", seed: 2),
                Fixtures.MakeScenario("sc-03", seed: 3),
            }
        };
        var batchB = new ScenarioBatch
        {
            BatchId      = "batch-b",
            ParentSpecId = "spec-b",
            Scenarios    = new List<ScenarioDto>
            {
                Fixtures.MakeScenario("sc-01", seed: 10),
                Fixtures.MakeScenario("sc-02", seed: 20),
                Fixtures.MakeScenario("sc-03", seed: 30),
            }
        };

        int entriesSubmitted = 0;

        // Results for all 6 unique scenarios (3 per batch), with composite custom_ids.
        var resultsA = Fixtures.BuildResultsJsonl(
            new[] { ("sc-01", "haiku-01"), ("sc-02", "haiku-02"), ("sc-03", "haiku-03") },
            anthropicBatchId,
            scenarioBatchId: "batch-a");

        var resultsB = Fixtures.BuildResultsJsonl(
            new[] { ("sc-01", "haiku-04"), ("sc-02", "haiku-05"), ("sc-03", "haiku-06") },
            anthropicBatchId,
            scenarioBatchId: "batch-b");

        var resultsJsonl = resultsA + "\n" + resultsB;

        var handler = new StubHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (req.Method == HttpMethod.Post && path.Contains("batches"))
            {
                var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(body);
                entriesSubmitted = doc.RootElement.GetProperty("requests").GetArrayLength();
                return Fixtures.JsonOk(Fixtures.BatchSubmissionJson(anthropicBatchId));
            }

            if (req.Method == HttpMethod.Get && path.EndsWith("/results"))
                return Fixtures.JsonOk(resultsJsonl);

            if (req.Method == HttpMethod.Get && path.Contains("batches"))
                return Fixtures.JsonOk(Fixtures.EndedStatusJson(anthropicBatchId));

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var (scheduler, client) = Wire(handler, FastPoller());
        using (client)
        {
            var results = await scheduler.RunAsync(
                "run-two-batches",
                new[] { batchA, batchB },
                CancellationToken.None);

            // (a) No exception thrown — reaching here proves it.
            // (b) All 6 input scenarios produce results.
            Assert.Equal(6, results.Count);

            // (c) 6 unique scenarios → 6 entries submitted to Anthropic.
            Assert.Equal(6, entriesSubmitted);

            // (d) haikuId is uniquely assigned across both batches.
            var haikuIds = results.Select(r => r.WorkerId).ToHashSet();
            Assert.Equal(6, haikuIds.Count);

            // (e) parentBatchId correctly distinguishes the two batches.
            var batchAResults = results.Take(3).ToList();
            var batchBResults = results.Skip(3).Take(3).ToList();
            Assert.All(batchAResults, r => Assert.Equal("batch-a", r.ParentBatchId));
            Assert.All(batchBResults, r => Assert.Equal("batch-b", r.ParentBatchId));
        }
    }

    // -- AT_BS_X_CustomIdRoundTrip -------------------------------------------------

    /// <summary>
    /// Verifies the composite custom_id encode/decode round-trip: the scheduler submits
    /// "<batchId>::<scenarioId>", Anthropic echoes it back, the parser recovers both parts,
    /// and the final result carries the correct ParentBatchId. Also covers AT-09: a batchId
    /// that makes the composite exceed 64 characters throws a clean InvalidOperationException.
    /// </summary>
    [Fact]
    public async Task AT_BS_X_CustomIdRoundTrip_RecoversBatchAndScenarioIds()
    {
        const string anthropicBatchId = "batch-test-001";
        const string scenarioBatchId  = "batch-foo";
        const string scenarioId       = "sc-01";

        string? submittedCustomId = null;

        var resultsJsonl = Fixtures.BuildResultsJsonl(
            new[] { (scenarioId, "haiku-01") },
            anthropicBatchId,
            scenarioBatchId: scenarioBatchId);

        var handler = new StubHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (req.Method == HttpMethod.Post && path.Contains("batches"))
            {
                var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(body);
                submittedCustomId = doc.RootElement
                    .GetProperty("requests")[0]
                    .GetProperty("custom_id")
                    .GetString();
                return Fixtures.JsonOk(Fixtures.BatchSubmissionJson(anthropicBatchId));
            }

            if (req.Method == HttpMethod.Get && path.EndsWith("/results"))
                return Fixtures.JsonOk(resultsJsonl);

            if (req.Method == HttpMethod.Get && path.Contains("batches"))
                return Fixtures.JsonOk(Fixtures.EndedStatusJson(anthropicBatchId));

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var batch = new ScenarioBatch
        {
            BatchId      = scenarioBatchId,
            ParentSpecId = "spec-foo",
            Scenarios    = new List<ScenarioDto> { Fixtures.MakeScenario(scenarioId) }
        };

        var (scheduler, client) = Wire(handler, FastPoller());
        using (client)
        {
            var results = await scheduler.RunAsync(
                "run-roundtrip",
                new[] { batch },
                CancellationToken.None);

            // Composite custom_id submitted verbatim.
            Assert.Equal($"{scenarioBatchId}::{scenarioId}", submittedCustomId);

            // Round-trip: result carries the correct batch and scenario ids.
            Assert.Single(results);
            Assert.Equal(scenarioId,      results[0].ScenarioId);
            Assert.Equal(scenarioBatchId, results[0].ParentBatchId);
        }

        // AT-09: batchId of 60 chars + "::" + "sc-01" = 67 > 64 → InvalidOperationException.
        var longBatchId = new string('x', 60);
        var longBatch = new ScenarioBatch
        {
            BatchId      = longBatchId,
            ParentSpecId = "spec-long",
            Scenarios    = new List<ScenarioDto> { Fixtures.MakeScenario("sc-01") }
        };

        var handler2 = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var (scheduler2, client2) = Wire(handler2, FastPoller());
        using (client2)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => scheduler2.RunAsync("run-too-long", new[] { longBatch }, CancellationToken.None));

            Assert.Contains("64", ex.Message);
            Assert.Contains(longBatchId, ex.Message);
        }

        // Malformed custom_id without "::" causes InvalidOperationException during result streaming.
        const string malformedJsonl =
            """{"custom_id":"no-separator","result":{"type":"succeeded","message":{"id":"m","type":"message","role":"assistant","content":[{"type":"text","text":"{}"}],"model":"claude-haiku-4-5-20251001","stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":5,"cache_creation_input_tokens":0,"cache_read_input_tokens":0,"output_tokens":3}}}}""";

        var handler3 = new StubHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (req.Method == HttpMethod.Post && path.Contains("batches"))
                return Fixtures.JsonOk(Fixtures.BatchSubmissionJson(anthropicBatchId));
            if (req.Method == HttpMethod.Get && path.EndsWith("/results"))
                return Fixtures.JsonOk(malformedJsonl);
            if (req.Method == HttpMethod.Get && path.Contains("batches"))
                return Fixtures.JsonOk(Fixtures.EndedStatusJson(anthropicBatchId));
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var (scheduler3, client3) = Wire(handler3, FastPoller());
        using (client3)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => scheduler3.RunAsync("run-malformed", new[] { batch }, CancellationToken.None));
        }
    }
}
