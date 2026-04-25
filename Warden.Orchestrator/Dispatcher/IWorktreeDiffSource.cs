namespace Warden.Orchestrator.Dispatcher;

/// <summary>
/// Retrieves the git unified diff for a Sonnet worktree, used by
/// <see cref="SonnetDispatcher"/> to feed banned-pattern detection (SRD §4.1).
/// </summary>
public interface IWorktreeDiffSource
{
    /// <summary>
    /// Returns the output of <c>git diff main...HEAD</c> for the given worktree,
    /// or <c>null</c> if the path is absent, invalid, or git fails for any reason.
    /// Never throws.
    /// </summary>
    Task<string?> GetDiffAsync(string? worktreePath, CancellationToken ct);
}
