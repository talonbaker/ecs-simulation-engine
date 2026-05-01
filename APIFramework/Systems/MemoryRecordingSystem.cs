using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Tuning;

namespace APIFramework.Systems;

/// <summary>
/// Subscribes to NarrativeEventBus and routes each candidate to the
/// appropriate memory surface:
///   2 participants → RelationshipMemoryComponent on the canonical relationship entity.
///   1 participant  → PersonalMemoryComponent on that NPC.
///   3+ participants → PersonalMemoryComponent fan-out on every participant.
///
/// Auto-creates a relationship entity (Intensity=50, no patterns) when a pair
/// candidate arrives and no relationship entity yet exists for that pair.
///
/// Phase: Narrative (70) — after NarrativeEventDetector and PersistenceThresholdDetector;
/// before TelemetryProjector snapshots state.
/// </summary>
/// <remarks>
/// Reads: <see cref="NarrativeEventCandidate"/> instances pushed through
/// <see cref="NarrativeEventBus.OnCandidateEmitted"/>; existing
/// <see cref="RelationshipMemoryComponent"/> / <see cref="PersonalMemoryComponent"/> buffers;
/// <see cref="RelationshipTag"/>+<see cref="RelationshipComponent"/> when locating pair entities.
/// Writes: <see cref="RelationshipMemoryComponent"/> on relationship entities and
/// <see cref="PersonalMemoryComponent"/> on participant entities (single writer of both);
/// may auto-spawn a relationship entity carrying <see cref="RelationshipTag"/> + a default
/// <see cref="RelationshipComponent"/> when none exists for an emitted pair.
/// Ordering: subscribes at construction; effective work runs whenever upstream Narrative
/// systems (e.g. <c>NarrativeEventDetector</c>, <c>PersistenceThresholdDetector</c>)
/// publish candidates within the same tick. The system's own <see cref="Update"/> is a no-op.
/// </remarks>
/// <seealso cref="NarrativeEventBus"/>
/// <seealso cref="NarrativeEventCandidate"/>
/// <seealso cref="MemoryEntry"/>
public sealed class MemoryRecordingSystem : ISystem
{
    private readonly EntityManager _em;
    private readonly MemoryConfig  _cfg;
    private readonly TuningCatalog _tuning;

    /// <summary>
    /// Subscribes the routing handler to the supplied <paramref name="bus"/>.
    /// </summary>
    /// <param name="bus">Narrative event bus to subscribe to.</param>
    /// <param name="em">Entity manager — used to lookup participants and to create
    /// relationship entities on demand.</param>
    /// <param name="cfg">Memory tuning — supplies per-buffer capacity caps.</param>
    /// <param name="tuning">Per-archetype tuning catalog; supplies memory-persistence-bias multipliers.</param>
    public MemoryRecordingSystem(NarrativeEventBus bus, EntityManager em, MemoryConfig cfg, TuningCatalog? tuning = null)
    {
        _em     = em;
        _cfg    = cfg;
        _tuning = tuning ?? TuningCatalog.Empty();
        bus.OnCandidateEmitted += OnCandidateEmitted;
    }

    /// <summary>
    /// No-op. All work happens in the bus subscription <c>OnCandidateEmitted</c>.
    /// The system implements <see cref="ISystem"/> only so the engine can register it
    /// inside the Narrative phase for ordering.
    /// </summary>
    public void Update(EntityManager em, float deltaTime) { }

    // ── Candidate routing ─────────────────────────────────────────────────────

    private void OnCandidateEmitted(NarrativeEventCandidate candidate)
    {
        var count = candidate.ParticipantIds.Count;
        if (count == 0) return;

        if (count == 1)
        {
            AppendToPersonal(candidate.ParticipantIds[0], candidate);
            return;
        }

        if (count == 2)
        {
            int pA  = Math.Min(candidate.ParticipantIds[0], candidate.ParticipantIds[1]);
            int pB  = Math.Max(candidate.ParticipantIds[0], candidate.ParticipantIds[1]);
            var rel = FindOrCreateRelationship(pA, pB);
            AppendToRelationshipMemory(rel, candidate);
            return;
        }

        foreach (var pid in candidate.ParticipantIds)
            AppendToPersonal(pid, candidate);
    }

    // ── Append helpers ────────────────────────────────────────────────────────

    private void AppendToRelationshipMemory(Entity rel, NarrativeEventCandidate candidate)
    {
        var current = rel.Has<RelationshipMemoryComponent>()
            ? rel.Get<RelationshipMemoryComponent>().Recent
            : Array.Empty<MemoryEntry>();

        var entry = BuildEntry(candidate);
        var next  = AppendBounded(current, entry, _cfg.MaxRelationshipMemoryCount);
        rel.Add(new RelationshipMemoryComponent { Recent = next });
    }

    private void AppendToPersonal(int participantId, NarrativeEventCandidate candidate)
    {
        var entity = FindEntityByIntId(participantId);
        if (entity is null) return;

        var current = entity.Has<PersonalMemoryComponent>()
            ? entity.Get<PersonalMemoryComponent>().Recent
            : Array.Empty<MemoryEntry>();

        var archetypeId = entity.Has<NpcArchetypeComponent>()
            ? entity.Get<NpcArchetypeComponent>().ArchetypeId : null;
        var memBias = _tuning.GetMemoryPersistenceBias(archetypeId);

        var entry = BuildEntryWithBias(candidate, memBias);

        // Effective capacity: higher decayRateMult → smaller buffer → memories dropped sooner.
        int effectiveMax = Math.Max(1, (int)(_cfg.MaxPersonalMemoryCount / memBias.DecayRateMult));
        var next = AppendBounded(current, entry, effectiveMax);
        entity.Add(new PersonalMemoryComponent { Recent = next });
    }

    // ── Entity lookup ─────────────────────────────────────────────────────────

    private Entity FindOrCreateRelationship(int pA, int pB)
    {
        foreach (var e in _em.Query<RelationshipTag>())
        {
            if (!e.Has<RelationshipComponent>()) continue;
            var rc = e.Get<RelationshipComponent>();
            if (rc.ParticipantA == pA && rc.ParticipantB == pB)
                return e;
        }

        var rel = _em.CreateEntity();
        rel.Add(new RelationshipTag());
        rel.Add(new RelationshipComponent(pA, pB, intensity: 50));
        return rel;
    }

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

    // ── Entry construction ────────────────────────────────────────────────────

    private static MemoryEntry BuildEntry(NarrativeEventCandidate c)
        => BuildEntryWithBias(c, MemoryPersistenceBias.Default);

    private static MemoryEntry BuildEntryWithBias(NarrativeEventCandidate c, MemoryPersistenceBias bias)
    {
        int[] canonical;
        if (c.ParticipantIds.Count == 2)
        {
            canonical = new[]
            {
                Math.Min(c.ParticipantIds[0], c.ParticipantIds[1]),
                Math.Max(c.ParticipantIds[0], c.ParticipantIds[1]),
            };
        }
        else
        {
            canonical = c.ParticipantIds.ToArray();
        }

        // persistenceMult < 1.0 probabilistically demotes normally-persistent events.
        // Deterministic: hash the event ID so the same event always resolves the same way.
        bool persistent = IsPersistent(c.Kind);
        if (persistent && bias.PersistenceMult < 1f)
        {
            int hash = HashEventForPersistence(c.Tick, c.Kind, canonical);
            float roll = (hash & 0x7FFFFFFF) / (float)int.MaxValue;
            if (roll >= bias.PersistenceMult)
                persistent = false;
        }

        return new MemoryEntry(
            Id:             BuildId(c.Tick, c.Kind, canonical),
            Tick:           c.Tick,
            Kind:           c.Kind,
            ParticipantIds: canonical,
            RoomId:         c.RoomId,
            Detail:         c.Detail,
            Persistent:     persistent
        );
    }

    private static int HashEventForPersistence(long tick, NarrativeEventKind kind, int[] participants)
    {
        int hash = (int)(tick ^ (tick >> 17)) ^ (int)kind;
        foreach (var p in participants)
            hash = hash * 397 ^ p;
        return hash;
    }

    /// <summary>
    /// Deterministic ID: mem-{tick:D8}-{kind}-{firstParticipant:D8}-{count}.
    /// Stable across replay given the same inputs.
    /// </summary>
    public static string BuildId(long tick, NarrativeEventKind kind, int[] participantIds)
    {
        int first = participantIds.Length > 0 ? participantIds[0] : 0;
        return $"mem-{tick:D8}-{kind}-{first:D8}-{participantIds.Length}";
    }

    /// <summary>
    /// Per-pair persistence threshold: would these two people remember this next week?
    /// Lighter than the chronicle's office-wide threshold.
    /// </summary>
    public static bool IsPersistent(NarrativeEventKind kind) => kind switch
    {
        NarrativeEventKind.WillpowerCollapse => true,
        NarrativeEventKind.LeftRoomAbruptly  => true,
        NarrativeEventKind.MaskSlip          => true,
        NarrativeEventKind.OverdueTask       => true,
        NarrativeEventKind.TaskCompleted     => false,
        NarrativeEventKind.Choked            => true,
        NarrativeEventKind.SlippedAndFell    => true,
        NarrativeEventKind.StarvedAlone      => true,
        NarrativeEventKind.Died              => true,
        NarrativeEventKind.ChokeStarted      => true,
        NarrativeEventKind.RescuePerformed   => true,
        NarrativeEventKind.RescueAttempted   => false,
        NarrativeEventKind.RescueFailed      => true,
        NarrativeEventKind.ChoreRefused      => true,
        NarrativeEventKind.ChoreBadlyDone    => true,
        NarrativeEventKind.ChoreOverrotation => true,
        _                                    => false,
    };

    private static IReadOnlyList<MemoryEntry> AppendBounded(
        IReadOnlyList<MemoryEntry> current, MemoryEntry entry, int capacity)
    {
        var list = new List<MemoryEntry>(current) { entry };
        if (list.Count > capacity)
            list.RemoveRange(0, list.Count - capacity);
        return list;
    }
}
