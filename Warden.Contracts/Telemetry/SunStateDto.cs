namespace Warden.Contracts.Telemetry;

// ── Sun position on the clock (spatial pillar v0.3) ──────────────────────────

public sealed record SunStateDto
{
    public double   AzimuthDeg   { get; init; }
    public double   ElevationDeg { get; init; }
    public DayPhase DayPhase     { get; init; }
}

// ── TilePoint (shared by LightSourceDto and LightApertureDto) ─────────────────

public sealed record TilePointDto
{
    public int X { get; init; }
    public int Y { get; init; }
}

// ── Enum ──────────────────────────────────────────────────────────────────────

/// <summary>Six-phase day cycle. Serialises as camelCase string.</summary>
public enum DayPhase
{
    Night,
    EarlyMorning,
    MidMorning,
    Afternoon,
    Evening,
    Dusk
}
