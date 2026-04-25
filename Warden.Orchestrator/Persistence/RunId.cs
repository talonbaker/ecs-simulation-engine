namespace Warden.Orchestrator.Persistence;

/// <summary>
/// Generates deterministic run identifiers in the form
/// <c>{yyyyMMdd-HHmmss}-{slug}</c>.
/// </summary>
public static class RunId
{
    /// <summary>
    /// Generates a new run ID stamped to the current UTC second.
    /// </summary>
    /// <param name="slug">
    /// Short human-readable suffix (e.g. "smoke", "mission01").
    /// Defaults to "run" when omitted.
    /// </param>
    public static string Generate(string? slug = null)
    {
        var ts = DateTimeOffset.UtcNow;
        var s  = string.IsNullOrWhiteSpace(slug) ? "run" : slug;
        return $"{ts:yyyyMMdd-HHmmss}-{s}";
    }
}
