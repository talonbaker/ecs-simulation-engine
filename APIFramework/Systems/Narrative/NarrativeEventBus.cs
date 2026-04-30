using System;

namespace APIFramework.Systems.Narrative;

/// <summary>
/// Singleton in-process bus for narrative event candidates.
/// Registered by SimulationBootstrapper; subscribers receive candidates
/// in the order the detector emits them (entity-id ascending within a tick).
/// </summary>
/// <remarks>
/// Single-writer rule: only <see cref="NarrativeEventDetector"/> raises candidates from
/// per-tick detection logic. Scenario systems (choking, fainting, life-state transitions,
/// bereavement) may also raise candidates synchronously when they detect a discrete event,
/// but they must do so BEFORE applying any state flip that would invalidate participant
/// guards (per the WP-3.0.0 narrative-emit contract).
///
/// Subscribers run synchronously inside <see cref="RaiseCandidate"/>. Treat handlers as
/// time-critical and side-effect-only — do not perform heavy computation or block.
/// </remarks>
/// <example>
/// Subscribing to and emitting candidates:
/// <code>
/// // Subscribe (typically from a system constructor):
/// narrativeBus.OnCandidateEmitted += candidate =&gt;
/// {
///     if (candidate.Kind == NarrativeEventKind.WillpowerCollapse)
///         Console.WriteLine($"Tick {candidate.Tick}: willpower collapse for {candidate.ParticipantIds[0]}");
/// };
///
/// // Emit (from a detector or scenario system):
/// narrativeBus.RaiseCandidate(new NarrativeEventCandidate(
///     Tick:           clock.CurrentTick,
///     Kind:           NarrativeEventKind.ConversationStarted,
///     ParticipantIds: new[] { speakerId, listenerId },
///     RoomId:         "kitchen",
///     Detail:         "conversation started in kitchen"));
/// </code>
/// </example>
/// <seealso cref="NarrativeEventCandidate"/>
/// <seealso cref="NarrativeEventKind"/>
/// <seealso cref="NarrativeEventDetector"/>
public sealed class NarrativeEventBus
{
    /// <summary>
    /// Raised once for every <see cref="NarrativeEventCandidate"/> emitted by the detector
    /// or by a scenario system. Handlers run synchronously on the calling thread.
    /// </summary>
    public event Action<NarrativeEventCandidate>? OnCandidateEmitted;

    /// <summary>
    /// Publishes <paramref name="candidate"/> to every subscriber of
    /// <see cref="OnCandidateEmitted"/>. Safe to call when there are no subscribers.
    /// </summary>
    /// <param name="candidate">The candidate to publish.</param>
    public void RaiseCandidate(NarrativeEventCandidate candidate) =>
        OnCandidateEmitted?.Invoke(candidate);
}
