using System.Text.Json.Serialization;

namespace Warden.Contracts.Handshake;

/// <summary>
/// Union of all <c>blockReason</c> values from the Sonnet and Haiku schemas.
/// Serialises as kebab-case strings (e.g. <c>AmbiguousSpec</c> → <c>"ambiguous-spec"</c>).
///
/// Sonnet-only values: AmbiguousSpec, BuildFailed, SchemaMismatchOnOwnOutput,
///   MissingReferenceFile, BudgetExceeded, TimeboxExceeded.
/// Haiku-only values: CliNonzero, TelemetryUnreadable, Timeout, InvariantViolated.
/// Shared values: ToolError, Exception.
/// </summary>
[JsonConverter(typeof(JsonKebabCaseEnumConverter<BlockReason>))]
public enum BlockReason
{
    // -- Sonnet ----------------------------------------------------------------

    /// <summary>The SpecPacket contains contradictory or missing requirements.</summary>
    AmbiguousSpec,

    /// <summary><c>dotnet build</c> failed; the diff cannot be validated.</summary>
    BuildFailed,

    /// <summary>The worker's own output did not validate against its schema.</summary>
    SchemaMismatchOnOwnOutput,

    /// <summary>A required reference file was not present in the worktree.</summary>
    MissingReferenceFile,

    /// <summary>The worker's ledger slice exceeded <c>workerBudgetUsd</c>.</summary>
    BudgetExceeded,

    /// <summary>The worker exceeded its <c>timeboxMinutes</c> wall-clock limit.</summary>
    TimeboxExceeded,

    // -- Haiku -----------------------------------------------------------------

    /// <summary>ECSCli returned a non-zero exit code.</summary>
    CliNonzero,

    /// <summary>The telemetry stream could not be parsed.</summary>
    TelemetryUnreadable,

    /// <summary>The scenario exceeded its allowed wall-clock run time.</summary>
    Timeout,

    /// <summary>The simulation's InvariantSystem recorded a critical violation.</summary>
    InvariantViolated,

    // -- Shared ----------------------------------------------------------------

    /// <summary>A tool call returned an error that the worker cannot recover from.</summary>
    ToolError,

    /// <summary>An unhandled exception was caught at the worker boundary.</summary>
    Exception
}
