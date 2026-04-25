using System.Collections.Generic;

namespace Warden.Contracts.SchemaValidation;

/// <summary>
/// Outcome of a <see cref="SchemaValidator.Validate"/> call.
/// </summary>
/// <param name="IsValid">True when the JSON conforms to the schema.</param>
/// <param name="Errors">
/// One human-readable message per violation.
/// Empty when <see cref="IsValid"/> is true.
/// </param>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    /// <summary>Singleton success result (no allocation on the happy path).</summary>
    public static readonly ValidationResult Ok = new(true, System.Array.Empty<string>());
}
