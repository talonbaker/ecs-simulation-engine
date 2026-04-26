using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>
/// A listener's record of which phrase fragments they recognize as another NPC's
/// signature tic, and the raw hearing counts that feed the recognition threshold.
/// The Dictionary fields are reference types — copies of this struct share the same instances.
/// </summary>
public struct RecognizedTicComponent
{
    /// <summary>speakerId → set of fragment IDs the listener has recognized as that speaker's tic.</summary>
    public Dictionary<int, HashSet<string>> RecognizedTicsBySpeakerId;

    /// <summary>(speakerId, fragmentId) → number of times the listener has heard that fragment from that speaker.</summary>
    public Dictionary<(int SpeakerId, string FragmentId), int> HearingCounts;

    public RecognizedTicComponent()
    {
        RecognizedTicsBySpeakerId = new Dictionary<int, HashSet<string>>();
        HearingCounts             = new Dictionary<(int, string), int>();
    }
}
