namespace Warden.Contracts.Handshake;

/// <summary>
/// Terminal state of any tier-2 (Sonnet) or tier-3 (Haiku) worker.
/// Serialises as camelCase lowercase strings via <see cref="JsonSmartEnumConverterFactory"/>:
/// <c>"ok"</c>, <c>"failed"</c>, <c>"blocked"</c>.
/// No <c>[JsonConverter]</c> attribute needed — the factory fallback handles it.
/// </summary>
public enum OutcomeCode
{
    /// <summary>All acceptance tests passed. Downstream dispatch proceeds.</summary>
    Ok,

    /// <summary>
    /// Worker ran to completion but one or more assertions evaluated to false.
    /// Downstream dispatch for this branch is halted.
    /// </summary>
    Failed,

    /// <summary>
    /// Worker could not evaluate the task (ambiguous spec, build failure, tool
    /// error, etc.). See <see cref="BlockReason"/> for the specific cause.
    /// Downstream dispatch for this branch is halted.
    /// </summary>
    Blocked
}
