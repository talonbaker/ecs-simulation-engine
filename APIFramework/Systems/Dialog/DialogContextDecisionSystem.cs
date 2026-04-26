using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems.Dialog;

/// <summary>
/// Phase 75 — clears PendingDialogQueue then repopulates it based on NPC drive state.
/// Subscribes to ProximityEventBus to track which NPC pairs are in conversation range.
/// For each in-range pair, probabilistically attempts dialog and maps the speaker's
/// dominant elevated drive to a corpus context string.
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

        foreach (var (speaker, listener) in _inRange)
        {
            if (!speaker.Has<NpcTag>())               continue;
            if (!listener.Has<NpcTag>())              continue;
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
}
