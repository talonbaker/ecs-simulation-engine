namespace APIFramework.Components;

/// <summary>
/// Engine-side sun position record. Same shape as Warden.Contracts.Telemetry.SunStateDto
/// but lives in APIFramework so lighting systems need no contract dependency.
/// </summary>
public readonly record struct SunStateRecord(
    double   AzimuthDeg,
    double   ElevationDeg,
    DayPhase DayPhase);
