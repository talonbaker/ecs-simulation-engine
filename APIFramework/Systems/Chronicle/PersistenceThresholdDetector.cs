using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Narrative;

namespace APIFramework.Systems.Chronicle;

/// <summary>
/// Runs after <see cref="NarrativeEventDetector"/> in the Narrative phase.
/// Applies the world-bible's persistence threshold to each candidate and promotes
/// qualifying events to <see cref="ChronicleEntry"/> records in <see cref="ChronicleService"/>.
///
/// Persistence rules (see §Persistence threshold in DRAFT-world-bible.md):
///   1. Relationship-changing — WillpowerCollapse/LeftRoomAbruptly/ConversationStarted
///      that pushed any involved relationship's intensity by ≥ threshold.
///   2. Physically hard to undo — irritation DriveSpike with post-spike value ≥ threshold
///      AND a non-NPC entity within 2 tiles → spawns Stain.
///   3. Talk-about — same event kind from ≥ N distinct NPCs in the same tick.
///
/// Doesn't stick: WillpowerLow alone; DriveSpike where drive already returned to baseline.
/// </summary>
/// <remarks>
/// Phase: Narrative (70). Subscribes to <see cref="NarrativeEventBus.OnCandidateEmitted"/>
/// and buffers candidates within a tick; <see cref="Update"/> evaluates the buffered list
/// and appends qualifying entries to <see cref="ChronicleService"/>. May spawn Stain
/// entities via <see cref="PhysicalManifestSpawner"/> when the irritation-near-item rule fires.
///
/// Must run AFTER <see cref="APIFramework.Systems.Narrative.NarrativeEventDetector"/>
/// (which lives in the same phase but is registered first) so candidates are present in
/// the buffer when this system processes them.
/// </remarks>
/// <seealso cref="ChronicleService"/>
/// <seealso cref="ChronicleEntry"/>
/// <seealso cref="PhysicalManifestSpawner"/>
public sealed class PersistenceThresholdDetector : ISystem
{
    private readonly ChronicleService        _chronicle;
    private readonly EntityManager           _em;
    private readonly SimulationClock         _clock;
    private readonly SeededRandom            _rng;
    private readonly ChronicleConfig         _config;
    private readonly PhysicalManifestSpawner _spawner;

    // Candidates buffered from the NarrativeBus this tick.
    private readonly List<NarrativeEventCandidate> _buffered = new();

    // Relationship intensity snapshot from the previous tick; key = (ParticipantA, ParticipantB).
    private readonly Dictionary<(int, int), int> _prevRelIntensity = new();

    /// <summary>
    /// Subscribes to <paramref name="narrativeBus"/> and constructs the spawner used to
    /// create physical manifestations when persistence rules fire.
    /// </summary>
    /// <param name="chronicle">Target chronicle service for promoted entries.</param>
    /// <param name="narrativeBus">Bus from which candidates are buffered.</param>
    /// <param name="em">Entity manager — used to scan entities for relationships and nearby items.</param>
    /// <param name="clock">Simulation clock — currently unused but stored for future expansion.</param>
    /// <param name="rng">Deterministic RNG used for entry-id generation and stain magnitudes.</param>
    /// <param name="config">Persistence thresholds and physical-manifest tuning.</param>
    public PersistenceThresholdDetector(
        ChronicleService  chronicle,
        NarrativeEventBus narrativeBus,
        EntityManager     em,
        SimulationClock   clock,
        SeededRandom      rng,
        ChronicleConfig   config)
    {
        _chronicle = chronicle;
        _em        = em;
        _clock     = clock;
        _rng       = rng;
        _config    = config;
        _spawner   = new PhysicalManifestSpawner(em, rng, config);
        narrativeBus.OnCandidateEmitted += c => _buffered.Add(c);
    }

    /// <summary>
    /// Evaluates every candidate buffered this tick against the persistence rules,
    /// promotes survivors to <see cref="ChronicleEntry"/> records, and refreshes
    /// the relationship-intensity snapshot for next-tick comparison.
    /// </summary>
    /// <param name="em">Entity manager — scanned for relationship and item entities.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        var currentRelIntensity = SnapshotRelIntensities(em);

        // Count unique participants per kind for talk-about detection.
        var kindParticipants = new Dictionary<NarrativeEventKind, HashSet<int>>();
        foreach (var c in _buffered)
        {
            if (!kindParticipants.TryGetValue(c.Kind, out var set))
                kindParticipants[c.Kind] = set = new HashSet<int>();
            foreach (var p in c.ParticipantIds)
                set.Add(p);
        }

        foreach (var candidate in _buffered)
        {
            var entry = EvaluateCandidate(candidate, currentRelIntensity, em, kindParticipants);
            if (entry is not null)
                _chronicle.Append(entry);
        }

        // Update previous-tick intensity snapshot.
        _prevRelIntensity.Clear();
        foreach (var (key, v) in currentRelIntensity)
            _prevRelIntensity[key] = v;

        _buffered.Clear();
    }

    // ── Evaluation ────────────────────────────────────────────────────────────

    private ChronicleEntry? EvaluateCandidate(
        NarrativeEventCandidate                         candidate,
        Dictionary<(int, int), int>                     currentRelIntensity,
        EntityManager                                   em,
        Dictionary<NarrativeEventKind, HashSet<int>>    kindParticipants)
    {
        // WillpowerLow never persists on its own.
        if (candidate.Kind == NarrativeEventKind.WillpowerLow)
            return null;

        // ── Relationship-changing rule ─────────────────────────────────────────
        if (candidate.Kind is NarrativeEventKind.WillpowerCollapse
                           or NarrativeEventKind.LeftRoomAbruptly
                           or NarrativeEventKind.ConversationStarted)
        {
            if (HasRelationshipImpact(candidate.ParticipantIds, currentRelIntensity))
                return MakeEntry(candidate, ChronicleEventKind.PublicArgument, null);
        }

        // ── DriveSpike rules ──────────────────────────────────────────────────
        if (candidate.Kind == NarrativeEventKind.DriveSpike)
        {
            var (driveName, currentValue, _) = ParseDriveSpike(candidate.Detail);

            // Physical-manifest rule: high irritation near an item.
            if (driveName == "irritation"
                && currentValue >= _config.ThresholdRules.IrritationSpikeMinForPhysicalManifest)
            {
                var nearby = FindNearbyItem(candidate.ParticipantIds, em);
                if (nearby is not null)
                {
                    string entryId    = NewEntryId();
                    int    sourceId   = candidate.ParticipantIds.Count > 0 ? candidate.ParticipantIds[0] : 0;
                    float  spawnX = 0f, spawnZ = 0f;
                    var    npcEntity  = FindEntityByIntId(em, sourceId);
                    if (npcEntity is not null && npcEntity.Has<PositionComponent>())
                    {
                        var pos = npcEntity.Get<PositionComponent>();
                        spawnX = pos.X; spawnZ = pos.Z;
                    }
                    int manifestId = _spawner.SpawnStain(spawnX, spawnZ,
                        $"participant:{sourceId}", entryId, candidate.Tick);
                    return MakeEntryWithId(entryId, candidate,
                        ChronicleEventKind.SpilledSomething,
                        PhysicalManifestSpawner.IntIdToGuidString(manifestId));
                }
            }

            // Return-to-baseline check: if the drive is already back at baseline, discard.
            if (candidate.ParticipantIds.Count > 0)
            {
                var entity = FindEntityByIntId(em, candidate.ParticipantIds[0]);
                if (entity is not null && IsBackAtBaseline(entity, driveName))
                    return null;
            }

            // Talk-about rule: same kind from ≥ N distinct NPCs.
            if (kindParticipants.TryGetValue(candidate.Kind, out var set)
                && set.Count >= _config.ThresholdRules.TalkAboutMinReferenceCount)
            {
                return MakeEntry(candidate, ChronicleEventKind.PublicArgument, null);
            }

            return null;
        }

        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool HasRelationshipImpact(
        IReadOnlyList<int>          participantIds,
        Dictionary<(int, int), int> currentIntensity)
    {
        int threshold = _config.ThresholdRules.IntensityChangeMinForRelationshipStick;
        foreach (var (key, curr) in currentIntensity)
        {
            bool involves = participantIds.Contains(key.Item1) || participantIds.Contains(key.Item2);
            if (!involves) continue;
            if (_prevRelIntensity.TryGetValue(key, out int prev)
                && Math.Abs(curr - prev) >= threshold)
                return true;
        }
        return false;
    }

    private static Entity? FindNearbyItem(IReadOnlyList<int> participantIds, EntityManager em)
    {
        if (participantIds.Count == 0) return null;

        float px = 0f, pz = 0f;
        bool found = false;
        foreach (var e in em.GetAllEntities())
        {
            if (EntityIntId(e) == participantIds[0] && e.Has<PositionComponent>())
            {
                var pos = e.Get<PositionComponent>();
                px = pos.X; pz = pos.Z;
                found = true;
                break;
            }
        }
        if (!found) return null;

        const float Radius = 2f;
        foreach (var e in em.GetAllEntities())
        {
            if (e.Has<NpcTag>() || e.Has<RoomTag>() || e.Has<LightSourceTag>()
                || e.Has<LightApertureTag>() || e.Has<RelationshipTag>()
                || e.Has<StainTag>() || e.Has<BrokenItemTag>())
                continue;
            if (!e.Has<PositionComponent>()) continue;
            var p = e.Get<PositionComponent>();
            float dx = p.X - px, dz = p.Z - pz;
            if (dx * dx + dz * dz <= Radius * Radius)
                return e;
        }
        return null;
    }

    private static Entity? FindEntityByIntId(EntityManager em, int intId)
    {
        foreach (var e in em.GetAllEntities())
        {
            if (EntityIntId(e) == intId)
                return e;
        }
        return null;
    }

    private static bool IsBackAtBaseline(Entity entity, string driveName)
    {
        if (!entity.Has<SocialDrivesComponent>()) return false;
        var d = entity.Get<SocialDrivesComponent>();
        var drive = driveName switch
        {
            "belonging"  => d.Belonging,
            "status"     => d.Status,
            "affection"  => d.Affection,
            "irritation" => d.Irritation,
            "attraction" => d.Attraction,
            "trust"      => d.Trust,
            "suspicion"  => d.Suspicion,
            "loneliness" => d.Loneliness,
            _            => default
        };
        return Math.Abs(drive.Current - drive.Baseline) <= 5;
    }

    private static (string driveName, int currentValue, int delta) ParseDriveSpike(string detail)
    {
        // Format: "driveName: prev → curr (+delta)"
        int colonIdx = detail.IndexOf(':');
        if (colonIdx < 0) return (string.Empty, 0, 0);
        string driveName = detail[..colonIdx].Trim();

        int arrowIdx = detail.IndexOf('→', colonIdx);
        if (arrowIdx < 0) return (driveName, 0, 0);

        int parenIdx = detail.IndexOf('(', arrowIdx);
        if (parenIdx < 0) return (driveName, 0, 0);

        if (!int.TryParse(detail[(arrowIdx + 1)..parenIdx].Trim(), out int curr))
            return (driveName, 0, 0);

        int closeIdx = detail.IndexOf(')', parenIdx);
        int delta    = 0;
        if (closeIdx > parenIdx)
            int.TryParse(detail[(parenIdx + 1)..closeIdx].Trim().TrimStart('+'), out delta);

        return (driveName, curr, delta);
    }

    private static Dictionary<(int, int), int> SnapshotRelIntensities(EntityManager em)
    {
        var result = new Dictionary<(int, int), int>();
        foreach (var e in em.GetAllEntities())
        {
            if (e.Has<RelationshipTag>() && e.Has<RelationshipComponent>())
            {
                var rc  = e.Get<RelationshipComponent>();
                result[(rc.ParticipantA, rc.ParticipantB)] = rc.Intensity;
            }
        }
        return result;
    }

    private static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    // ── Chronicle-entry construction ──────────────────────────────────────────

    private string NewEntryId()
    {
        var bytes = new byte[16];
        for (int i = 0; i < 16; i++)
            bytes[i] = (byte)_rng.NextInt(256);
        // Force variant/version bits to produce a valid RFC 4122 v4-ish UUID.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes).ToString();
    }

    private ChronicleEntry MakeEntry(
        NarrativeEventCandidate candidate,
        ChronicleEventKind      kind,
        string?                 physicalManifestGuidString)
        => MakeEntryWithId(NewEntryId(), candidate, kind, physicalManifestGuidString);

    private static ChronicleEntry MakeEntryWithId(
        string                  id,
        NarrativeEventCandidate candidate,
        ChronicleEventKind      kind,
        string?                 physicalManifestGuidString)
    {
        string desc = candidate.Detail.Length > 280
            ? candidate.Detail[..280]
            : candidate.Detail;

        return new ChronicleEntry(
            Id:                      id,
            Kind:                    kind,
            Tick:                    candidate.Tick,
            ParticipantIds:          candidate.ParticipantIds,
            Location:                candidate.RoomId ?? string.Empty,
            Description:             desc,
            Persistent:              true,
            PhysicalManifestEntityId: physicalManifestGuidString
        );
    }
}
