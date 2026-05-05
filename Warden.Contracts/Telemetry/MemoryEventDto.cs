using System;
using System.Collections.Generic;

namespace Warden.Contracts.Telemetry;

// -- Memory event (pair-scoped at v0.2) ----------------------------------------

public sealed record MemoryEventDto
{
    public string                Id             { get; init; } = string.Empty;
    public long                  Tick           { get; init; }
    public IReadOnlyList<string> Participants   { get; init; } = Array.Empty<string>();
    public string                Kind           { get; init; } = string.Empty;
    public MemoryScope           Scope          { get; init; }
    public string                Description    { get; init; } = string.Empty;
    public bool                  Persistent     { get; init; }
    public string?               RelationshipId { get; init; }
}

// -- Enum ----------------------------------------------------------------------

/// <summary>
/// Memory event scope. <c>Global</c> is reserved for v0.3.
/// Serialises as camelCase string.
/// </summary>
public enum MemoryScope { Pair, Global }
