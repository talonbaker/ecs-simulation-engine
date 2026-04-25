using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Warden.Orchestrator.Dispatcher;

/// <summary>
/// Production <see cref="IWorktreeDiffSource"/> that shells out to
/// <c>git diff main...HEAD</c> inside the Sonnet's worktree.
/// Returns null on any error; never throws.
/// </summary>
public sealed class GitWorktreeDiffSource : IWorktreeDiffSource
{
    private readonly ILogger<GitWorktreeDiffSource> _log;
    private readonly TimeSpan                       _timeout;

    /// <param name="log">Logger for Information/Warning entries.</param>
    /// <param name="timeout">
    /// Hard limit for the git subprocess. Defaults to 10 seconds.
    /// Pass a smaller value in tests to trigger the timeout path.
    /// </param>
    public GitWorktreeDiffSource(
        ILogger<GitWorktreeDiffSource> log,
        TimeSpan? timeout = null)
    {
        _log     = log;
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    public async Task<string?> GetDiffAsync(string? worktreePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(worktreePath) || !Directory.Exists(worktreePath))
        {
            _log.LogInformation(
                "Worktree path '{Path}' is absent or not a directory; skipping diff check.",
                worktreePath);
            return null;
        }

        Process process;
        try
        {
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "git",
                    Arguments              = "diff main...HEAD",
                    WorkingDirectory       = worktreePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                }
            };
            process.Start();
        }
        catch (Exception ex)
        {
            _log.LogInformation(ex,
                "Could not start git process for worktree '{Path}'; skipping diff check.",
                worktreePath);
            return null;
        }

        using (process)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(_timeout);

            try
            {
                var output = await process.StandardOutput
                    .ReadToEndAsync(linkedCts.Token)
                    .ConfigureAwait(false);

                await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    _log.LogInformation(
                        "git diff exited {ExitCode} for worktree '{Path}'; skipping diff check.",
                        process.ExitCode, worktreePath);
                    return null;
                }

                return output;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Internal timeout fired; external cancellation propagates.
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                _log.LogWarning(
                    "git diff timed out after {Timeout} for worktree '{Path}'.",
                    _timeout, worktreePath);
                return null;
            }
        }
    }
}
