namespace Warden.Anthropic.Internal;

/// <summary>
/// Configures the single <see cref="System.Net.Http.HttpClient"/> used by
/// <see cref="AnthropicClient"/>. All Anthropic-specific headers are set here;
/// nothing else in <c>Warden.*</c> assembles HTTP headers directly.
/// </summary>
internal static class HttpClientFactory
{
    internal const string DefaultBaseUrl      = "https://api.anthropic.com/";
    internal const string AnthropicVersion    = "2023-06-01";

    /// <summary>
    /// Beta features require opt-in via the <c>anthropic-beta</c> header.
    /// Values verified against https://docs.claude.com at 2026-04-24:
    /// <list type="bullet">
    ///   <item><c>prompt-caching-2024-07-31</c> — enables <c>cache_control</c> on content blocks.</item>
    ///   <item><c>message-batches-2024-09-24</c> — enables <c>POST /v1/messages/batches</c> and related endpoints.</item>
    /// </list>
    /// If Anthropic graduates either feature to GA and drops the beta requirement,
    /// remove the relevant value here and update this comment.
    /// </summary>
    internal const string BetaHeaders = "prompt-caching-2024-07-31,message-batches-2024-09-24";

    /// <summary>
    /// Creates a configured <see cref="System.Net.Http.HttpClient"/>.
    /// </summary>
    /// <param name="apiKey">Value for the <c>x-api-key</c> header.</param>
    /// <param name="handler">
    /// Optional message handler override. When null a default handler is used.
    /// Pass a stub for offline testing.
    /// </param>
    /// <param name="baseUrl">
    /// Optional base URL override (e.g. for local proxy tests).
    /// Defaults to <see cref="DefaultBaseUrl"/>.
    /// </param>
    internal static System.Net.Http.HttpClient Create(
        string apiKey,
        System.Net.Http.HttpMessageHandler? handler = null,
        string? baseUrl = null)
    {
        var client = handler is not null
            ? new System.Net.Http.HttpClient(handler, disposeHandler: false)
            : new System.Net.Http.HttpClient();

        client.BaseAddress = new Uri(baseUrl ?? DefaultBaseUrl);

        var headers = client.DefaultRequestHeaders;
        headers.Add("x-api-key",        apiKey);
        headers.Add("anthropic-version", AnthropicVersion);
        headers.Add("anthropic-beta",    BetaHeaders);
        headers.Add("User-Agent",
            $"Warden.Anthropic/1.0 dotnet/{System.Environment.Version}");

        return client;
    }
}
