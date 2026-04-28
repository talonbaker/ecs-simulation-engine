using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems.Dialog;

/// <summary>
/// Phase 75 — clears PendingDialogQueue then repopulates it based on NPC intent / drive state.
///
/// Two paths per (speaker, listener) pair:
/// 1. If the speaker has IntendedActionComponent with Kind == Dialog, use that context directly
///    and skip the probability gate (ActionSelectionSystem has already decided).
/// 2. Otherwise, fall back to the existing heuristic: probabilistic gate + drive-to-context map.
///    This preserves all WP-1.10.A tests for scenarios that don't exercise action-selection.
/// </summary>
public sealed class DialogContextDecisionSystem : ISystem
{
    private readonly PendingDialogQueue _queue;
    private readonly DialogConfig       _cfg;
    private readonly SeededRandom       _rng;

    // Bidirectional in-range pairs; maintained via ProximityEventBus subscriptions.
    private readonly HashSet<(Entity Speaker, Entity Listener)> _inRange = new();

    public DialogContextDecisionSystem(
        PendingDialogQueue queue,
        ProximityEventBus  bus,
        DialogConfig       cfg,
        SeededRandom       rng)
    {
        _queue = queue;
        _cfg   = cfg;
        _rng   = rng;

        bus.OnEnteredConversationRange += e =>
        {
            _inRange.Add((e.Observer, e.Target));
            _inRange.Add((e.Target,   e.Observer));
        };
        bus.OnLeftConversationRange += e =>
        {
            _inRange.Remove((e.Observer, e.Target));
            _inRange.Remove((e.Target,   e.Observer));
        };
    }

    public void Update(EntityManager em, float deltaTime)
    {
        _queue.Clear();

        // Track which speakers have already been assigned dialog this tick so a
        // speaker with Dialog intent only emits once (to the first matching listener).
        var processedSpeakers = new HashSet<Entity>();

        foreach (var (speaker, listener) in _inRange)
        {
            if (!speaker.Has<NpcTag>())  continue;
            if (!listener.Has<NpcTag>()) continue;
            if (!LifeStateGuard.IsAlive(speaker))  continue;  // WP-3.0.0: skip non-Alive NPCs
            if (!LifeStateGuard.IsAlive(listener)) continue;

            // Path 1: IntendedActionComponent with Dialog intent.
            if (!processedSpeakers.Contains(speaker) &&
                speaker.Has<IntendedActionComponent>())
            {
                var intent = speaker.Get<IntendedActionComponent>();
                if (intent.Kind == IntendedActionKind.Dialog)
                {
                    // Use the intended target if specified; otherwise accept the first in-range pair.
                    if (intent.TargetEntityId != 0)
                    {
                        int listenerId = WillpowerSystem.EntityIntId(listener);
                        if (listenerId != intent.TargetEntityId) continue;
                    }
                    _queue.Enqueue(speaker, listener, MapContextValue(intent.Context));
                    processedSpeakers.Add(speaker);
                    continue;
                }
            }

            // Path 2: Heuristic fallback (existing WP-1.10.A logic).
            if (!speaker.Has<SocialDrivesComponent>()) continue;
            if (_rng.NextDouble() >= _cfg.DialogAttemptProbability) continue;

            var drives  = speaker.Get<SocialDrivesComponent>();
            var context = SelectContext(drives, _cfg.DriveContextThreshold);
            _queue.Enqueue(speaker, listener, context);
        }
    }

    /// <summary>Maps the speaker's most elevated drive to a corpus context string.</summary>
    private static string SelectContext(SocialDrivesComponent d, int threshold)
    {
        if (d.Irritation.Current >= threshold)                          return "lashOut";
        if (d.Attraction.Current >= threshold && d.Trust.Current > 0)  return "flirt";
        if (d.Loneliness.Current >= threshold)                          return "share";
        if (d.Status.Current     >= threshold)                          return "boast";
        if (d.Suspicion.Current  >= threshold)                          return "complaint";
        if (d.Affection.Current  >= threshold)                          return "acknowledge";
        if (d.Belonging.Current  >= threshold)                          return "greeting";
        return "greeting";
    }

    /// <summary>Maps DialogContextValue enum to corpus context string.</summary>
    private static string MapContextValue(DialogContextValue v) => v switch
    {
        DialogContextValue.LashOut    => "lashOut",
        DialogContextValue.Share      => "share",
        DialogContextValue.Flirt      => "flirt",
        DialogContextValue.Deflect    => "deflect",
        DialogContextValue.BrushOff   => "brushOff",
        DialogContextValue.Acknowledge => "acknowledge",
        DialogContextValue.Greet      => "greeting",
        DialogContextValue.Refuse     => "refuse",
        DialogContextValue.Agree      => "agree",
        DialogContextValue.Complain   => "complaint",
        DialogContextValue.Encourage  => "encourage",
        DialogContextValue.Thanks     => "thanks",
        DialogContextValue.Apologise  => "apology",
        DialogContextValue.MaskSlip  => "maskSlip",
        _                             => "greeting"
    };
}
