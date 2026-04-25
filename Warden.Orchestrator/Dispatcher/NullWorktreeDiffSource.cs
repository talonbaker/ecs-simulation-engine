namespace Warden.Orchestrator.Dispatcher;

/// <summary>
/// Test-time stub for <see cref="IWorktreeDiffSource"/>.
/// Returns a canned diff string verbatim (or null, skipping the banned-pattern check).
/// </summary>
public sealed class NullWorktreeDiffSource : IWorktreeDiffSource
{
    private readonly string? _cannedDiff;

    public NullWorktreeDiffSource(string? cannedDiff = null) => _cannedDiff = cannedDiff;

    public Task<string?> GetDiffAsync(string? worktreePath, CancellationToken ct)
        => Task.FromResult(_cannedDiff);
}
