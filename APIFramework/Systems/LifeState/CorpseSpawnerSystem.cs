using System;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Narrative;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// NarrativeEventBus subscriber that attaches <see cref="CorpseTag"/> and
/// <see cref="CorpseComponent"/> to an NPC entity when a death narrative event is received.
///
/// Subscribes to the four death event kinds:
///   Choked, SlippedAndFell, StarvedAlone, Died.
///
/// The subscription fires synchronously from the bus at the moment
/// <see cref="LifeStateTransitionSystem"/> raises the death candidate — before
/// <see cref="LifeState.Deceased"/> is actually written (per the WP-3.0.0 narrative-emit contract).
/// By the time the next tick begins, both CorpseTag and LifeState.Deceased are set.
///
/// Idempotent: re-emitting the same death event will not duplicate the tag or component
/// (guarded by the early-return on existing CorpseTag).
///
/// This system has no per-tick Update logic; it is purely event-driven.
///
/// WP-3.0.2: Deceased-Entity Handling + Bereavement.
/// </summary>
/// <seealso cref="BereavementSystem"/>
/// <seealso cref="BereavementByProximitySystem"/>
public sealed class CorpseSpawnerSystem : ISystem
{
    private readonly EntityManager _em;

    /// <summary>
    /// Subscribes to <see cref="NarrativeEventBus.OnCandidateEmitted"/> to react to the four death event kinds.
    /// </summary>
    /// <param name="narrativeBus">Bus from which death events are received.</param>
    /// <param name="em">Entity manager — used to resolve the deceased entity by EntityIntId.</param>
    public CorpseSpawnerSystem(NarrativeEventBus narrativeBus, EntityManager em)
    {
        _em = em;
        narrativeBus.OnCandidateEmitted += OnDeathEvent;
    }

    private void OnDeathEvent(NarrativeEventCandidate ev)
    {
        // Only react to the four death narrative kinds (WP-3.0.0).
        if (ev.Kind is not (
            NarrativeEventKind.Choked      or
            NarrativeEventKind.SlippedAndFell or
            NarrativeEventKind.StarvedAlone   or
            NarrativeEventKind.Died))
            return;

        if (ev.ParticipantIds.Count == 0) return;

        // First participant is always the deceased (WP-3.0.0 contract).
        var deceased = FindEntityByIntId(ev.ParticipantIds[0]);
        if (deceased is null) return;

        // Idempotent guard — re-sending the event does not duplicate the tag.
        if (deceased.Has<CorpseTag>()) return;

        // Read cause-of-death metadata for the component (may not exist yet if event
        // fired before LifeStateTransitionSystem wrote it; LocationRoomId is best-effort).
        string? roomId    = null;
        long    deathTick = ev.Tick;
        if (deceased.Has<CauseOfDeathComponent>())
        {
            var cod  = deceased.Get<CauseOfDeathComponent>();
            roomId   = cod.LocationRoomId;
            deathTick = cod.DeathTick;
        }

        deceased.Add(new CorpseTag());
        deceased.Add(new CorpseComponent
        {
            DeathTick           = deathTick,
            OriginalNpcEntityId = deceased.Id,
            LocationRoomId      = roomId,
            HasBeenMoved        = false,
        });
    }

    /// <summary>
    /// No per-tick work — this system is purely event-driven via the narrative bus subscription.
    /// </summary>
    /// <param name="em">Entity manager (unused).</param>
    /// <param name="deltaTime">Tick delta in seconds (unused).</param>
    public void Update(EntityManager em, float deltaTime) { /* event-driven; no per-tick work */ }

    // -- Utility ---------------------------------------------------------------

    private Entity? FindEntityByIntId(int intId)
    {
        foreach (var e in _em.GetAllEntities())
        {
            if (EntityIntId(e) == intId) return e;
        }
        return null;
    }

    private static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }
}
