using System;
using System.Collections.Generic;

namespace APIFramework.Systems.Coupling;

/// <summary>
/// Context resolved for a single NPC at the current tick, used for condition matching.
/// All string fields are lowercase enum names for case-insensitive comparison.
/// </summary>
public readonly struct CouplingMatchContext
{
    public readonly string? RoomCategory;
    public readonly string? DominantSourceState;
    public readonly string? DominantSourceKind;
    public readonly int     AmbientLevel;
    public readonly string  DayPhase;
    public readonly bool    ApertureBeamPresent;

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
