using System;
using System.Collections.Generic;

namespace APIFramework.Bootstrap;

/// <summary>
/// Thrown by <see cref="WorldDefinitionLoader"/> when the world-definition JSON fails
/// schema validation or cannot be parsed. Carries the full structured error list from
/// <c>SchemaValidator</c> so the operator can act without reading source code.
/// </summary>
public sealed class WorldDefinitionInvalidException : Exception
{
    public IReadOnlyList<string> ValidationErrors { get; }

    public WorldDefinitionInvalidException(IReadOnlyList<string> errors)
        : base(BuildMessage(errors))
    {
        ValidationErrors = errors;
    }

    private static string BuildMessage(IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
            return "World definition is invalid (no details provided).";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"World definition failed validation with {errors.Count} error(s):");
        foreach (var e in errors)
            sb.AppendLine($"  - {e}");
        return sb.ToString().TrimEnd();
    }
}
