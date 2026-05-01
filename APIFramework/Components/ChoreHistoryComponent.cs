using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>
/// Per-NPC chore performance history. Tracks lifetime counts, refusals, quality,
/// and a 7-day rolling window for overrotation detection.
/// </summary>
public struct ChoreHistoryComponent
{
    public Dictionary<ChoreKind, int>   TimesPerformed;      // lifetime completions per chore
    public Dictionary<ChoreKind, int>   TimesRefused;        // lifetime refusals per chore
    public Dictionary<ChoreKind, float> AverageQuality;      // running average quality per chore
    public long                         LastRefusalTick;

    // Overrotation window: count completions within the last N game-days.
    public Dictionary<ChoreKind, int>   WindowTimesPerformed; // completions in current window
    public Dictionary<ChoreKind, int>   WindowStartDay;       // DayNumber when window started
}
