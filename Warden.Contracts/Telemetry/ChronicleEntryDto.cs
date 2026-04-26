using System.Collections.Generic;

namespace Warden.Contracts.Telemetry;

/// <summary>
/// Wire-format record for a single persistent chronicle entry.
/// Part of <c>WorldStateDto.Chronicle</c> (v0.4.0+).
/// </summary>
public sealed record ChronicleEntryDto
{
    public string              Id           { get; init; } = string.Empty;
    public ChronicleEventKind  Kind         { get; init; }
    public long                Tick         { get; init; }
    public List<string>        Participants { get; init; } = new();
    public string?             Location     { get; init; }
    public string              Description  { get; init; } = string.Empty;
    public bool                Persistent   { get; init; }
    public string?             PhysicalManifestEntityId { get; init; }
}
