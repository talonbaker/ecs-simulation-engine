namespace Warden.Anthropic;

/// <summary>
/// A single turn in the conversation history sent to <c>POST /v1/messages</c>.
/// </summary>
/// <param name="Role">
/// Either <c>"user"</c> or <c>"assistant"</c>. The Anthropic API requires
/// alternating turns starting with <c>"user"</c>.
/// </param>
/// <param name="Content">One or more content blocks for this turn.</param>
public sealed record MessageTurn(
    string             Role,
    List<ContentBlock> Content);
