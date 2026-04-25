using System.Net;

namespace Warden.Anthropic;

/// <summary>
/// Thrown by <see cref="AnthropicClient"/> when the Anthropic API returns a
/// non-2xx HTTP response.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IsRetryable"/> is a <em>hint</em> for the orchestrator's Polly policy —
/// it does not cause the client to retry internally. The client is one-shot.
/// </para>
/// <para>
/// Retryable: 429 (rate-limited) and 5xx (server errors). Non-retryable: all 4xx
/// except 429 (malformed requests, auth failures, etc.).
/// </para>
/// </remarks>
public sealed class AnthropicApiException : Exception
{
    /// <summary>HTTP status code returned by the API.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Response body, truncated at 4,096 characters to prevent unbounded log entries.
    /// </summary>
    public string ResponseBody { get; }

    /// <summary>
    /// True when the orchestrator's retry policy should attempt this request again.
    /// Specifically: <c>true</c> for 429 and any 5xx; <c>false</c> for all other 4xx.
    /// </summary>
    public bool IsRetryable { get; }

    /// <summary>Initialises a new instance from an API error response.</summary>
    public AnthropicApiException(HttpStatusCode statusCode, string responseBody)
        : base(BuildMessage(statusCode, responseBody))
    {
        StatusCode   = statusCode;
        ResponseBody = Truncate(responseBody, maxLength: 4096);
        IsRetryable  = DetermineIsRetryable(statusCode);
    }

    private static bool DetermineIsRetryable(HttpStatusCode code)
    {
        var numeric = (int)code;
        return code == HttpStatusCode.TooManyRequests   // 429
            || (numeric >= 500 && numeric <= 599);       // 5xx
    }

    private static string BuildMessage(HttpStatusCode code, string body)
    {
        var truncated = Truncate(body, 200);
        return $"Anthropic API error {(int)code} ({code}): {truncated}";
    }

    private static string Truncate(string s, int maxLength)
        => s.Length <= maxLength ? s : string.Concat(s.AsSpan(0, maxLength), "…");
}
