using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Spatial;


namespace APIFramework.Systems.LifeState;

/// <summary>
/// Per-tick system that fires the "NPC physically encounters a corpse in their room"
/// bereavement effect — the second tier of the WP-3.0.2 bereavement cascade.
///
/// For each Alive NPC in the same room as a corpse entity, if:
///   - The NPC had a relationship with the deceased (intensity ≥ ProximityBereavementMinIntensity)
///   - The NPC has NOT previously been hit by this corpse's proximity-bereavement
/// Then: apply ProximityBereavementStressGain to StressComponent.AcuteLevel (direct, one-shot).
///
/// The hit is tracked via BereavementHistoryComponent.EncounteredCorpseIds (HashSet<Guid>),
/// attached lazily on first encounter. Once recorded, the NPC is never hit again by the same
/// corpse, regardless of how long they remain in the room or how often they re-enter.
///
/// Deterministic: Alive NPCs are iterated in ascending EntityIntId order; corpses likewise.
///
/// Phase: Cleanup (80) — after BereavementSystem (Narrative bus subscriber) so the immediate
/// bereavement hit has already been applied; this tier fires on physical encounter.
///
/// WP-3.0.2: Deceased-Entity Handling + Bereavement.
/// </summary>
/// <remarks>
/// Reads <c>RelationshipComponent</c>, <c>CorpseTag</c>, the room-membership snapshot, and
/// (lazily) <c>BereavementHistoryComponent</c>. Writes <c>StressComponent.AcuteLevel</c>
/// and <c>BereavementHistoryComponent.EncounteredCorpseIds</c>.
/// </remarks>
/// <seealso cref="BereavementSystem"/>
/// <seealso cref="CorpseSpawnerSystem"/>
public sealed class BereavementByProximitySystem : ISystem
{
    private readonly EntityRoomMembership _roomMembership;
    private readonly BereavementConfig    _cfg;

    /// <summary>
    /// Stores room-membership and bereavement-tuning references used per tick.
    /// </summary>
    /// <param name="roomMembership">Membership lookup used to find NPCs and corpses sharing a room.</param>
    /// <param name="cfg">Bereavement config — supplies <c>ProximityBereavementMinIntensity</c> and <c>ProximityBereavementStressGain</c>.</param>
    public BereavementByProximitySystem(EntityRoomMembership roomMembership, BereavementConfig cfg)
    {
        _roomMembership = roomMembership;
        _cfg            = cfg;
    }

    /// <summary>
    /// Per-tick entry point. Walks every Alive NPC and applies a one-shot bereavement hit
    /// for each in-room corpse they had a meaningful relationship with.
    /// </summary>
    /// <param name="em">Entity manager — queried for corpses and Alive NPCs.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        // Gather corpse entities and their rooms (skip corpses with no room).
        var corpses = em.Query<CorpseTag>()
            .Select(c => (Entity: c, Room: _roomMembership.GetRoom(c)))
            .Where(c => c.Room != null)
            .ToList();

        if (corpses.Count == 0) return;

        // Iterate Alive NPCs in deterministic order.
        foreach (var npc in em.Query<NpcTag>()
                               .Where(e => LifeStateGuard.IsAlive(e))
                               .OrderBy(e => EntityIntId(e))
                               .ToList())
        {
            var npcRoom = _roomMembership.GetRoom(npc);
            if (npcRoom is null) continue;

            int npcIntId = EntityIntId(npc);

            foreach (var (corpse, corpseRoom) in corpses)
            {
                // Same room check.
                if (!Equals(npcRoom.Id, corpseRoom!.Id)) continue;

                // Avoid self-hit (NPC is the deceased — shouldn't happen as they're not Alive).
                if (corpse.Id == npc.Id) continue;

                // Check if already hit by this corpse.
                BereavementHistoryComponent history = npc.Has<BereavementHistoryComponent>()
                    ? npc.Get<BereavementHistoryComponent>()
                    : new BereavementHistoryComponent { EncounteredCorpseIds = new System.Collections.Generic.HashSet<Guid>() };

                if (history.EncounteredCorpseIds.Contains(corpse.Id)) continue;

                // Relationship intensity check.
                int deceasedIntId = EntityIntIdFromGuid(corpse.Id);
                int relationshipIntensity = FindRelationshipIntensity(em, npcIntId, deceasedIntId);
                if (relationshipIntensity < _cfg.ProximityBereavementMinIntensity) continue;

                // Apply one-shot proximity bereavement stress.
                if (npc.Has<StressComponent>())
                {
                    var stress = npc.Get<StressComponent>();
                    stress.AcuteLevel = Math.Clamp(
                        (int)(stress.AcuteLevel + _cfg.ProximityBereavementStressGain), 0, 100);
                    npc.Add(stress);
                }

                // Record the encounter to prevent re-triggering.
                history.EncounteredCorpseIds.Add(corpse.Id);
                npc.Add(history);
            }
        }
    }

    // ── Relationship lookup ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the Intensity of the relationship between npcIntId and deceasedIntId,
    /// or 0 if no relationship entity exists for this pair.
    /// </summary>
    private static int FindRelationshipIntensity(EntityManager em, int npcIntId, int deceasedIntId)
    {
        int pA = Math.Min(npcIntId, deceasedIntId);
        int pB = Math.Max(npcIntId, deceasedIntId);

        foreach (var rel in em.Query<RelationshipTag>())
        {
            if (!rel.Has<RelationshipComponent>()) continue;
            var rc = rel.Get<RelationshipComponent>();
            if (rc.ParticipantA == pA && rc.ParticipantB == pB)
                return rc.Intensity;
        }
        return 0;
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    private static int EntityIntIdFromGuid(Guid id)
    {
        var b = id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }
}
