using System;
using System.Collections.Generic;

namespace APIFramework.Systems.Coupling;

/// <summary>
/// Context resolved for a single NPC at the current tick, used for condition matching.
/// All string fields are lowercase enum names for case-insensitive comparison.
/// </summary>
public readonly struct CouplingMatchContext
{
    /// <summary>Lowercase RoomCategory enum name for the NPC's current room, or null if outside rooms.</summary>
    public readonly string? RoomCategory;
    /// <summary>Lowercase LightState enum name of the room's dominant light source, or null.</summary>
    public readonly string? DominantSourceState;
    /// <summary>Lowercase LightKind enum name of the room's dominant light source, or null.</summary>
    public readonly string? DominantSourceKind;
    /// <summary>The room's accumulated AmbientLevel (0–100).</summary>
    public readonly int     AmbientLevel;
    /// <summary>Lowercase DayPhase enum name resolved from <see cref="APIFramework.Systems.Lighting.SunStateService"/>.</summary>
    public readonly string  DayPhase;
    /// <summary>True if any aperture in the room is currently emitting a beam.</summary>
    public readonly bool    ApertureBeamPresent;

    /// <summary>
    /// Captures the per-NPC lighting context resolved by
    /// <see cref="LightingToDriveCouplingSystem"/> for this tick.
    /// </summary>
    /// <param name="roomCategory">Lowercase RoomCategory enum name, or null.</param>
    /// <param name="dominantSourceState">Lowercase LightState enum name, or null.</param>
    /// <param name="dominantSourceKind">Lowercase LightKind enum name, or null.</param>
    /// <param name="ambientLevel">Room AmbientLevel (0–100).</param>
    /// <param name="dayPhase">Lowercase DayPhase enum name.</param>
    /// <param name="apertureBeamPresent">Whether at least one aperture in the room emits a beam.</param>
    public CouplingMatchContext(
        string? roomCategory,
        string? dominantSourceState,
        string? dominantSourceKind,
        int     ambientLevel,
        string  dayPhase,
        bool    apertureBeamPresent)
    {
        RoomCategory        = roomCategory;
        DominantSourceState = dominantSourceState;
        DominantSourceKind  = dominantSourceKind;
        AmbientLevel        = ambientLevel;
        DayPhase            = dayPhase;
        ApertureBeamPresent = apertureBeamPresent;
    }
}

/// <summary>
/// AND-of-all-clauses condition for a lighting-to-drive coupling entry.
/// Null / empty-list clauses are "any" — they always match.
/// Loaded from SimConfig.lighting.driveCouplings[].condition via Newtonsoft.Json.
/// </summary>
public class CouplingCondition
{
    /// <summary>Room category must be one of these (case-insensitive). Null = any room.</summary>
    public List<string>? RoomCategoryAny { get; set; }

    /// <summary>Dominant source LightState name (e.g. "flickering"). Null = any state.</summary>
    public string? DominantSourceState { get; set; }

    /// <summary>Dominant source LightKind name (e.g. "deskLamp"). Null = any kind.</summary>
    public string? DominantSourceKind { get; set; }

    /// <summary>Minimum room ambient level (inclusive). Null = no minimum.</summary>
    public int? AmbientLevelMin { get; set; }

    /// <summary>Maximum room ambient level (inclusive). Null = no maximum.</summary>
    public int? AmbientLevelMax { get; set; }

    /// <summary>Day phase must be one of these (case-insensitive). Null = any phase.</summary>
    public List<string>? DayPhaseAny { get; set; }

    /// <summary>Whether at least one light aperture in the room is emitting a beam. Null = don't care.</summary>
    public bool? ApertureBeamPresent { get; set; }

    /// <summary>
    /// Returns true if the context satisfies every non-null clause in this condition.
    /// </summary>
    /// <param name="ctx">The per-NPC match context resolved this tick.</param>
    public bool Matches(in CouplingMatchContext ctx)
    {
        if (RoomCategoryAny is { Count: > 0 })
        {
            bool found = false;
            foreach (var cat in RoomCategoryAny)
            {
                if (string.Equals(cat, ctx.RoomCategory, StringComparison.OrdinalIgnoreCase))
                { found = true; break; }
            }
            if (!found) return false;
        }

        if (DominantSourceState != null &&
            !string.Equals(DominantSourceState, ctx.DominantSourceState, StringComparison.OrdinalIgnoreCase))
            return false;

        if (DominantSourceKind != null &&
            !string.Equals(DominantSourceKind, ctx.DominantSourceKind, StringComparison.OrdinalIgnoreCase))
            return false;

        if (AmbientLevelMin.HasValue && ctx.AmbientLevel < AmbientLevelMin.Value)
            return false;

        if (AmbientLevelMax.HasValue && ctx.AmbientLevel > AmbientLevelMax.Value)
            return false;

        if (DayPhaseAny is { Count: > 0 })
        {
            bool found = false;
            foreach (var phase in DayPhaseAny)
            {
                if (string.Equals(phase, ctx.DayPhase, StringComparison.OrdinalIgnoreCase))
                { found = true; break; }
            }
            if (!found) return false;
        }

        if (ApertureBeamPresent.HasValue && ctx.ApertureBeamPresent != ApertureBeamPresent.Value)
            return false;

        return true;
    }
}
