using System.Collections.Generic;

namespace Warden.Telemetry;

/// <summary>
/// Outcome of a <see cref="CommandDispatcher.Apply"/> call.
/// </summary>
/// <param name="Applied">
/// Number of commands that were successfully applied to the simulation.
/// Always 0 when <paramref name="Rejected"/> is greater than 0 (atomic batch policy).
/// </param>
/// <param name="Rejected">
/// Number of commands that were rejected. Either 0 or equal to the full batch size —
/// partial application is never permitted.
/// </param>
/// <param name="Errors">
/// Human-readable error messages, one per rejected command (or per validation failure).
/// Empty when <paramref name="Rejected"/> is 0.
/// </param>
public sealed record DispatchResult(
    int                    Applied,
    int                    Rejected,
    IReadOnlyList<string>  Errors);
