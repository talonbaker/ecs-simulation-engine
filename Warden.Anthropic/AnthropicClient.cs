using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Warden.Contracts;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Warden.Anthropic.Tests")]

namespace Warden.Anthropic;

/// <summary>
/// Thin HTTP client for the Anthropic Messages and Message Batches APIs.
///
/// <para>
/// <b>Design constraints:</b>
/// <list type="bullet">
///   <item>Non-streaming only — no <c>stream: true</c> support.</item>
///   <item>One-shot — no internal retries. <see cref="AnthropicApiException.IsRetryable"/>
///     is a hint for the orchestrator's Polly policy.</item>
///   <item>One <see cref="System.Net.Http.HttpClient"/> per instance (AT-06).</item>
///   <item>Inject a custom <see cref="System.Net.Http.HttpMessageHandler"/> via the
///     constructor for offline unit tests.</item>
/// </list>
/// </para>
/// </summary>
public sealed class AnthropicClient : IAnthropicClient, IDisposable
{
    private readonly HttpClient _http;

    // -- Construction -------------------------------------------------------------

    /// <summary>
    /// Creates a new <see cref="AnthropicClient"/>.
    /// </summary>
    /// <param name="apiKey">Anthropic API key (value of the <c>ANTHROPIC_API_KEY</c> env var).</param>
    /// <param name="handler">
    /// Optional message handler. Inject a stub for offline tests; leave null for production.
    /// </param>
    /// <param name="baseUrl">
    /// Optional base URL override. Defaults to <c>https://api.anthropic.com/</c>.
    /// </param>
    public AnthropicClient(
        string apiKey,
        HttpMessageHandler? handler = null,
        string? baseUrl = null)
    {
        _http = Internal.HttpClientFactory.Create(apiKey, handler, baseUrl);
    }

    // -- Messages API -------------------------------------------------------------

    /// <summary>
    /// Calls <c>POST /v1/messages</c> and returns the model's response.
    /// Throws <see cref="AnthropicApiException"/> on any non-2xx status.
    /// </summary>
    public async Task<MessageResponse> CreateMessageAsync(
        MessageRequest request,
        CancellationToken ct = default)
    {
        using var content = Serialize(request);
        using var response = await _http
            .PostAsync("v1/messages", content, ct)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
        return await DeserializeAsync<MessageResponse>(response, ct).ConfigureAwait(false);
    }

    // -- Batch API ----------------------------------------------------------------

    /// <summary>
    /// Calls <c>POST /v1/messages/batches</c> and returns the created batch handle.
    /// Throws <see cref="AnthropicApiException"/> on any non-2xx status.
    /// </summary>
    public async Task<BatchSubmission> CreateBatchAsync(
        BatchRequest request,
        CancellationToken ct = default)
    {
        using var content = Serialize(request);
        using var response = await _http
            .PostAsync("v1/messages/batches", content, ct)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
        return await DeserializeAsync<BatchSubmission>(response, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Calls <c>GET /v1/messages/batches/{batchId}</c> and returns the current status.
    /// Poll this until <see cref="BatchStatus.ProcessingStatus"/> is
    /// <see cref="BatchProcessingStatus.Ended"/>; the orchestrator drives the polling timer.
    /// Throws <see cref="AnthropicApiException"/> on any non-2xx status.
    /// </summary>
    public async Task<BatchStatus> GetBatchAsync(
        string batchId,
        CancellationToken ct = default)
    {
        using var response = await _http
            .GetAsync($"v1/messages/batches/{batchId}", ct)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
        return await DeserializeAsync<BatchStatus>(response, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Streams JSONL results for a completed batch from its <c>results_url</c>.
    /// Calls <see cref="GetBatchAsync"/> once to obtain the URL, then fetches and
    /// parses the JSONL line-by-line.
    /// </summary>
    /// <param name="batchId">The batch identifier returned by <see cref="CreateBatchAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async sequence of <see cref="BatchResultEntry"/> records.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the batch has no <c>results_url</c> (still processing).
    /// </exception>
    /// <exception cref="AnthropicApiException">Thrown on non-2xx from either request.</exception>
    public async IAsyncEnumerable<BatchResultEntry> StreamBatchResultsAsync(
        string batchId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Resolve the results URL from the batch status.
        var status = await GetBatchAsync(batchId, ct).ConfigureAwait(false);

        if (status.ResultsUrl is null)
            throw new InvalidOperationException(
                $"Batch '{batchId}' has no results_url. " +
                $"Processing status is '{status.ProcessingStatus}'. " +
                "Wait until status is Ended before streaming results.");

        // Fetch results as a streaming response.
        using var response = await _http
            .GetAsync(status.ResultsUrl, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        using var stream = await response.Content
            .ReadAsStreamAsync(ct)
            .ConfigureAwait(false);

        // StreamReader(Stream, Encoding) — disposes stream when disposed.
        // The outer `using var stream` double-dispose is safe (no-op on disposed streams).
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var entry = JsonSerializer.Deserialize<BatchResultEntry>(line, JsonOptions.Wire)
                ?? throw new JsonException("JSONL results stream contained a null entry.");

            yield return entry;
        }
    }

    // -- IDisposable --------------------------------------------------------------

    /// <inheritdoc/>
    public void Dispose() => _http.Dispose();

    // -- Private helpers ----------------------------------------------------------

    private static StringContent Serialize<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions.Wire);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static async Task<T> DeserializeAsync<T>(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions.Wire, ct).ConfigureAwait(false)
            ?? throw new JsonException($"Anthropic API returned null when deserialising {typeof(T).Name}.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        throw new AnthropicApiException(response.StatusCode, body);
    }
}
