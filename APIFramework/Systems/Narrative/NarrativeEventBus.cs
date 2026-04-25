using System;

namespace APIFramework.Systems.Narrative;

/// <summary>
/// Singleton in-process bus for narrative event candidates.
/// Registered by SimulationBootstrapper; subscribers receive candidates
/// in the order the detector emits them (entity-id ascending within a tick).
/// </summary>
public sealed class NarrativeEventBus
{
    public event Action<NarrativeEventCandidate>? OnCandidateEmitted;

    public void RaiseCandidate(NarrativeEventCandidate candidate) =>
        OnCandidateEmitted?.Invoke(candidate);
}
