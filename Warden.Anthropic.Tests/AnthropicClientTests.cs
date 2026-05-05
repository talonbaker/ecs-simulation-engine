using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Warden.Anthropic;
using Warden.Contracts;
using Xunit;

namespace Warden.Anthropic.Tests;

// -- Stub HTTP handler ---------------------------------------------------------

/// <summary>
/// In-process HTTP message handler that intercepts requests and returns
/// canned responses. Lets all tests run without a network connection.
/// </summary>
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

// -- Helpers -------------------------------------------------------------------

internal static class Helpers
{
    /// <summary>Loads an embedded JSON sample by filename.</summary>
    internal static string LoadSample(string filename)
    {
        var asm  = typeof(AnthropicClientTests).Assembly;
        var name = asm.GetManifestResourceNames()
            .First(n => n.EndsWith(filename, StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal static HttpResponseMessage JsonOk(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    internal static string CannedMessageResponse(string model = "claude-sonnet-4-6") => $$"""
        {
          "id": "msg_01test",
          "type": "message",
          "role": "assistant",
          "content": [{"type": "text", "text": "Hi!"}],
          "model": "{{model}}",
          "stop_reason": "end_turn",
          "stop_sequence": null,
          "usage": {
            "input_tokens": 10,
            "cache_creation_input_tokens": 0,
            "cache_read_input_tokens": 0,
            "output_tokens": 5
          }
        }
        """;

    internal static string CannedBatchSubmission() => """
        {
          "id": "msgbatch_01test",
          "type": "message_batch",
          "processing_status": "in_progress",
          "created_at": "2026-04-24T00:00:00Z",
          "results_url": null
        }
        """;

    internal static string CannedBatchStatus(string resultsUrl) => $$"""
        {
          "id": "msgbatch_01test",
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
          "results_url": "{{resultsUrl}}"
        }
        """;
}

// -- Test class ----------------------------------------------------------------

public sealed class AnthropicClientTests
{
    // -- AT-01: CreateMessageAsync produces the documented /v1/messages wire shape --

    [Fact]
    public async Task AT01_CreateMessageAsync_ProducesCorrectWireShape()
    {
        // Arrange: capture the outgoing request body
        string? capturedBody = null;
        var handler = new StubHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Helpers.JsonOk(Helpers.CannedMessageResponse());
        });

        using var client = new AnthropicClient("test-key", handler);

        var request = new MessageRequest(
            ModelId.SonnetV46,
            1024,
            new List<MessageTurn>
            {
                new("user", new List<ContentBlock>
                {
                    new TextBlock("Hello, Claude!")
                })
            });

        // Act
        await client.CreateMessageAsync(request);

        // Assert: the body round-trips to the canonical sample shape
        Assert.NotNull(capturedBody);

        // Deserialise our captured body and the canonical sample, then compare key fields.
        var sampleJson = Helpers.LoadSample("messages-request.json");
        var fromSample = JsonSerializer.Deserialize<MessageRequest>(sampleJson, JsonOptions.Wire)!;
        var fromWire   = JsonSerializer.Deserialize<MessageRequest>(capturedBody!, JsonOptions.Wire)!;

        Assert.Equal(fromSample.Model.Name,   fromWire.Model.Name);
        Assert.Equal(fromSample.MaxTokens,     fromWire.MaxTokens);
        Assert.Single(fromWire.Messages);
        Assert.Equal("user",                   fromWire.Messages[0].Role);

        // Also verify "max_tokens" is the literal key in the JSON (snake_case)
        using var doc = JsonDocument.Parse(capturedBody!);
        Assert.True(doc.RootElement.TryGetProperty("max_tokens", out _),
            "Wire JSON must use 'max_tokens' (snake_case), not 'maxTokens'.");
        Assert.True(doc.RootElement.TryGetProperty("model", out var modelProp),
            "Wire JSON must have 'model' key.");
        Assert.Equal("claude-sonnet-4-6", modelProp.GetString());
    }

    // -- AT-02: CacheControl serialises correctly on the blocks it's attached to --

    [Fact]
    public void AT02_CacheControl_Ephemeral_SerializesWithoutTtl()
    {
        var block = new TextBlock("Cached text.", new CacheControl());
        var json  = JsonSerializer.Serialize<ContentBlock>(block, JsonOptions.Wire);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("cache_control", out var cc));
        Assert.Equal("ephemeral", cc.GetProperty("type").GetString());
        Assert.False(cc.TryGetProperty("ttl", out _),
            "Null Ttl must be omitted (WhenWritingNull).");
    }

    [Fact]
    public void AT02_CacheControl_WithTtl_SerializesTtl()
    {
        var block = new TextBlock("Long-lived text.", new CacheControl("ephemeral", "1h"));
        var json  = JsonSerializer.Serialize<ContentBlock>(block, JsonOptions.Wire);

        using var doc = JsonDocument.Parse(json);
        var cc = doc.RootElement.GetProperty("cache_control");
        Assert.Equal("ephemeral", cc.GetProperty("type").GetString());
        Assert.Equal("1h",        cc.GetProperty("ttl").GetString());
    }

    [Fact]
    public void AT02_CacheControl_OnlyOnMarkedBlocks()
    {
        // Create a request with system blocks: one cached, one not.
        var request = new MessageRequest(
            ModelId.SonnetV46, 1024,
            new List<MessageTurn> { new("user", new List<ContentBlock> { new TextBlock("Hi") }) })
        {
            System = new List<ContentBlock>
            {
                new TextBlock("Cached slab.", new CacheControl()),   // marked
                new TextBlock("Uncached slab.")                      // not marked
            }
        };

        var json = JsonSerializer.Serialize(request, JsonOptions.Wire);
        using var doc   = JsonDocument.Parse(json);
        var system      = doc.RootElement.GetProperty("system");
        var slab0       = system[0];
        var slab1       = system[1];

        Assert.True(slab0.TryGetProperty("cache_control", out _),
            "First slab must have cache_control.");
        Assert.False(slab1.TryGetProperty("cache_control", out _),
            "Second slab must NOT have cache_control (it was not marked).");
    }

    // -- AT-03: CreateBatchAsync produces correct /v1/messages/batches body --------

    [Fact]
    public async Task AT03_CreateBatchAsync_ProducesCorrectWireShape()
    {
        string? capturedBody = null;
        var handler = new StubHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Helpers.JsonOk(Helpers.CannedBatchSubmission());
        });

        using var client = new AnthropicClient("test-key", handler);

        var batchRequest = new BatchRequest(new List<BatchRequestEntry>
        {
            new("req-01", new MessageRequest(
                ModelId.SonnetV46, 512,
                new List<MessageTurn>
                {
                    new("user", new List<ContentBlock> { new TextBlock("Batch hello!") })
                }))
        });

        await client.CreateBatchAsync(batchRequest);

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);

        // Must have top-level "requests" array
        Assert.True(doc.RootElement.TryGetProperty("requests", out var requests));
        Assert.Equal(1, requests.GetArrayLength());

        var entry = requests[0];
        Assert.Equal("req-01", entry.GetProperty("custom_id").GetString());

        // params must contain the message request shape
        var parms = entry.GetProperty("params");
        Assert.Equal("claude-sonnet-4-6", parms.GetProperty("model").GetString());
        Assert.Equal(512,                 parms.GetProperty("max_tokens").GetInt32());
    }

    // -- AT-04: StreamBatchResultsAsync parses JSONL via results_url --------------

    [Fact]
    public async Task AT04_StreamBatchResultsAsync_ParsesJsonl()
    {
        const string batchId     = "msgbatch_01test";
        const string resultsUrl  = "https://api.anthropic.com/v1/messages/batches/msgbatch_01test/results";
        const string jsonlPayload = """
            {"custom_id":"req-01","result":{"type":"succeeded","message":{"id":"msg_01","type":"message","role":"assistant","content":[{"type":"text","text":"Result 1"}],"model":"claude-sonnet-4-6","stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":5,"cache_creation_input_tokens":0,"cache_read_input_tokens":0,"output_tokens":3}}}}
            {"custom_id":"req-02","result":{"type":"errored","error":{"type":"server_error","message":"Internal error"}}}
            """;

        var handler = new StubHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith($"/{batchId}", StringComparison.Ordinal))
                return Helpers.JsonOk(Helpers.CannedBatchStatus(resultsUrl));
            if (path.Contains("results", StringComparison.Ordinal))
                return Helpers.JsonOk(jsonlPayload);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = new AnthropicClient("test-key", handler,
            baseUrl: "https://api.anthropic.com/");

        var entries = new List<BatchResultEntry>();
        await foreach (var entry in client.StreamBatchResultsAsync(batchId))
            entries.Add(entry);

        Assert.Equal(2, entries.Count);

        // First entry: succeeded
        Assert.Equal("req-01", entries[0].CustomId);
        var succeeded = Assert.IsType<SucceededResult>(entries[0].Result);
        Assert.Equal("msg_01", succeeded.Message.Id);

        // Second entry: errored
        Assert.Equal("req-02", entries[1].CustomId);
        var errored = Assert.IsType<ErroredResult>(entries[1].Result);
        Assert.Equal("server_error", errored.Error.Type);
    }

    // -- AT-05: 429 → IsRetryable=true; 400 → IsRetryable=false ------------------

    [Fact]
    public async Task AT05_Http429_ThrowsAnthropicApiException_IsRetryable()
    {
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("""{"type":"error","error":{"type":"rate_limit_error","message":"Rate limited"}}""")
            });

        using var client = new AnthropicClient("test-key", handler);
        var request = new MessageRequest(ModelId.SonnetV46, 10,
            new List<MessageTurn> { new("user", new List<ContentBlock> { new TextBlock("Hi") }) });

        var ex = await Assert.ThrowsAsync<AnthropicApiException>(
            () => client.CreateMessageAsync(request));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.True(ex.IsRetryable, "429 must be retryable.");
    }

    [Fact]
    public async Task AT05_Http400_ThrowsAnthropicApiException_NotRetryable()
    {
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"type":"error","error":{"type":"invalid_request_error","message":"Bad request"}}""")
            });

        using var client = new AnthropicClient("test-key", handler);
        var request = new MessageRequest(ModelId.SonnetV46, 10,
            new List<MessageTurn> { new("user", new List<ContentBlock> { new TextBlock("Hi") }) });

        var ex = await Assert.ThrowsAsync<AnthropicApiException>(
            () => client.CreateMessageAsync(request));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.False(ex.IsRetryable, "400 must NOT be retryable.");
    }

    [Fact]
    public async Task AT05_Http500_ThrowsAnthropicApiException_IsRetryable()
    {
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"type":"error","error":{"type":"api_error","message":"Internal server error"}}""")
            });

        using var client = new AnthropicClient("test-key", handler);
        var request = new MessageRequest(ModelId.SonnetV46, 10,
            new List<MessageTurn> { new("user", new List<ContentBlock> { new TextBlock("Hi") }) });

        var ex = await Assert.ThrowsAsync<AnthropicApiException>(
            () => client.CreateMessageAsync(request));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.True(ex.IsRetryable, "500 must be retryable.");
    }

    // -- AT-06: AnthropicClient allocates exactly one HttpClient per instance ------

    [Fact]
    public void AT06_AnthropicClient_HasExactlyOneHttpClientField()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new AnthropicClient("test-key", handler);

        // Verify via reflection that there is exactly one HttpClient field
        var httpClientFields = typeof(AnthropicClient)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.FieldType == typeof(HttpClient))
            .ToList();

        Assert.Single(httpClientFields);

        var httpClient = (HttpClient?)httpClientFields[0].GetValue(client);
        Assert.NotNull(httpClient);
    }

    [Fact]
    public async Task AT06_MultipleMethodCalls_UseSameUnderlyingHandler()
    {
        // If only one HttpClient exists, all requests flow through the same handler.
        var callCount = 0;
        var handler = new StubHandler(req =>
        {
            callCount++;
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("batches/batch01/results"))
                return Helpers.JsonOk("{\"custom_id\":\"r1\",\"result\":{\"type\":\"canceled\"}}");
            if (path.Contains("batches/batch01"))
                return Helpers.JsonOk(Helpers.CannedBatchStatus(
                    "https://api.anthropic.com/v1/messages/batches/batch01/results"));
            return Helpers.JsonOk(Helpers.CannedMessageResponse());
        });

        using var client = new AnthropicClient("test-key", handler,
            baseUrl: "https://api.anthropic.com/");

        var msgRequest = new MessageRequest(ModelId.SonnetV46, 10,
            new List<MessageTurn> { new("user", new List<ContentBlock> { new TextBlock("Hi") }) });

        await client.CreateMessageAsync(msgRequest);      // call 1
        await client.GetBatchAsync("batch01");            // call 2
        // Both went through the same handler → callCount == 2
        Assert.Equal(2, callCount);
    }

    // -- AT-07: ModelId cannot be constructed with an arbitrary string externally --

    [Fact]
    public void AT07_ModelId_HasNoPublicParameterizedConstructors()
    {
        // A public parameterized constructor would let callers write new ModelId("hack").
        var publicParameterizedCtors = typeof(ModelId)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(c => c.GetParameters().Length > 0)
            .ToList();

        Assert.Empty(publicParameterizedCtors);
    }

    [Fact]
    public void AT07_ModelId_StaticValuesHaveCorrectNames()
    {
        Assert.Equal("claude-opus-4-6",           ModelId.OpusV46.Name);
        Assert.Equal("claude-sonnet-4-6",          ModelId.SonnetV46.Name);
        Assert.Equal("claude-haiku-4-5-20251001",  ModelId.HaikuV45.Name);
    }

    [Fact]
    public void AT07_ModelId_SerializesAsPlainString()
    {
        // Serialise a model ID and verify it produces a JSON string, not an object.
        var json = JsonSerializer.Serialize(ModelId.SonnetV46, JsonOptions.Wire);
        Assert.Equal("\"claude-sonnet-4-6\"", json);
    }

    [Fact]
    public void AT07_ModelId_DeserializesFromWireName()
    {
        var model = JsonSerializer.Deserialize<ModelId>("\"claude-opus-4-6\"", JsonOptions.Wire);
        Assert.Equal("claude-opus-4-6", model.Name);
    }

    // -- AT-08: CostRates.PricedAsOf ≥ 2026-04-01 ---------------------------------

    [Fact]
    public void AT08_CostRates_PricedAsOf_IsRecentEnough()
    {
        var cutoff = new DateOnly(2026, 4, 1);
        Assert.True(
            CostRates.PricedAsOf >= cutoff,
            $"CostRates.PricedAsOf ({CostRates.PricedAsOf:O}) must be ≥ {cutoff:O}. " +
            "Update CostRates.cs when Anthropic pricing changes.");
    }

    [Fact]
    public void AT08_CostRates_AllModelsHaveRates()
    {
        // ForModel must succeed for every well-known model; throws for unknown.
        var opus   = CostRates.ForModel(ModelId.OpusV46);
        var sonnet = CostRates.ForModel(ModelId.SonnetV46);
        var haiku  = CostRates.ForModel(ModelId.HaikuV45);

        Assert.True(opus.InputPerMtok   > 0m);
        Assert.True(sonnet.InputPerMtok > 0m);
        Assert.True(haiku.InputPerMtok  > 0m);

        // Opus is the most expensive input model
        Assert.True(opus.InputPerMtok > sonnet.InputPerMtok);
        Assert.True(sonnet.InputPerMtok > haiku.InputPerMtok);
    }
}
