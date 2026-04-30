using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems.Narrative;

/// <summary>
/// End-of-tick observer. Compares engine state between ticks and emits
/// <see cref="NarrativeEventCandidate"/> records when thresholds cross.
///
/// Three detection classes:
///   Drive-delta    — |drive.Current delta| >= NarrativeConfig.DriveSpikeThreshold
///   Willpower-delta — drop >= WillpowerDropThreshold; first crossing below WillpowerLowThreshold
///   Proximity      — OnEnteredConversationRange → ConversationStarted;
///                    OnRoomMembershipChanged within AbruptDepartureWindowTicks of a DriveSpike → LeftRoomAbruptly
///
/// Phase: Narrative (70) — runs after all other phases so state has settled.
/// </summary>
/// <remarks>
/// Reads <c>SocialDrivesComponent</c> and <c>WillpowerComponent</c> snapshots tick-over-tick;
/// reads <c>IdentityComponent</c> on rooms; reads <see cref="EntityRoomMembership"/>; consumes
/// proximity events buffered from the Spatial phase. Writes nothing to the entity world —
/// the only side effect is publishing on <see cref="NarrativeEventBus"/>.
///
/// Determinism: NPCs are iterated in entity-id ascending order, candidates are sorted by
/// primary participant id before emission, and LINQ <c>OrderBy</c> stable-sorts ties.
/// </remarks>
/// <seealso cref="NarrativeEventBus"/>
/// <seealso cref="NarrativeEventCandidate"/>
/// <seealso cref="NarrativeEventKind"/>
public sealed class NarrativeEventDetector : ISystem
{
    private readonly NarrativeEventBus    _narrativeBus;
    private readonly EntityRoomMembership _roomMembership;
    private readonly NarrativeConfig      _cfg;

    // Per-entity drive and willpower snapshots from the previous tick
    private readonly Dictionary<int, int[]> _prevDrives    = new();
    private readonly Dictionary<int, int>   _prevWillpower = new();

    // Tick number of the last DriveSpike emitted per entity (for LeftRoomAbruptly window)
    private readonly Dictionary<int, long>  _lastSpikeByEntity = new();

    // Events buffered from the Spatial phase of this tick
    private readonly List<ProximityEnteredConversationRange> _convEvents = new();
    private readonly List<RoomMembershipChanged>             _roomEvents = new();

    private long _tick;

    private static readonly string[] DriveNames =
        { "belonging", "status", "affection", "irritation",
          "attraction", "trust", "suspicion", "loneliness" };

    /// <summary>
    /// Subscribes to the proximity bus and stores the dependencies for per-tick detection.
    /// </summary>
    /// <param name="narrativeBus">Bus on which detected candidates are published.</param>
    /// <param name="proximityBus">Bus from which OnEnteredConversationRange and OnRoomMembershipChanged events are buffered.</param>
    /// <param name="roomMembership">Room-membership lookup used to resolve room names.</param>
    /// <param name="cfg">Tunable thresholds (drive-spike, willpower drop/low, abrupt-departure window, detail max length).</param>
    public NarrativeEventDetector(
        NarrativeEventBus    narrativeBus,
        ProximityEventBus    proximityBus,
        EntityRoomMembership roomMembership,
        NarrativeConfig      cfg)
    {
        _narrativeBus   = narrativeBus;
        _roomMembership = roomMembership;
        _cfg            = cfg;

        proximityBus.OnEnteredConversationRange += e => _convEvents.Add(e);
        proximityBus.OnRoomMembershipChanged    += e => _roomEvents.Add(e);
    }

    /// <summary>
    /// Per-tick entry point. Diffs current drive/willpower snapshots against the previous tick,
    /// processes buffered proximity events, and publishes ordered candidates on the bus.
    /// </summary>
    /// <param name="em">Entity manager — queried for NPCs.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused; this system is tick-counted).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        _tick++;
        var candidates = new List<(int primaryId, NarrativeEventCandidate c)>();

        // ── Drive-delta detection ─────────────────────────────────────────────
        // Iterate in entity-id order so accumulators are always visited consistently.
        foreach (var entity in em.Query<NpcTag>().OrderBy(e => e.Id).ToList())
        {
            if (!entity.Has<SocialDrivesComponent>()) continue;

            int id     = EntityIntId(entity);
            var drives = entity.Get<SocialDrivesComponent>();
            int[] curr = DriveSnapshot(drives);

            if (_prevDrives.TryGetValue(id, out int[]? prev))
            {
                for (int i = 0; i < 8; i++)
                {
                    int delta = curr[i] - prev[i];
                    if (Math.Abs(delta) < _cfg.DriveSpikeThreshold) continue;

                    string detail = Truncate(
                        $"{DriveNames[i]}: {prev[i]} → {curr[i]} ({(delta >= 0 ? "+" : "")}{delta})",
                        _cfg.CandidateDetailMaxLength);

                    candidates.Add((id, new NarrativeEventCandidate(
                        _tick, NarrativeEventKind.DriveSpike,
                        new[] { id }, null, detail)));

                    _lastSpikeByEntity[id] = _tick;
                }
            }

            _prevDrives[id] = curr;
        }

        // ── Willpower-delta detection ─────────────────────────────────────────
        foreach (var entity in em.Query<NpcTag>().OrderBy(e => e.Id).ToList())
        {
            if (!entity.Has<WillpowerComponent>()) continue;

            int id   = EntityIntId(entity);
            int curr = entity.Get<WillpowerComponent>().Current;

            if (_prevWillpower.TryGetValue(id, out int prev))
            {
                int delta = curr - prev;

                // WillpowerCollapse: a single-tick drop >= threshold
                if (delta <= -_cfg.WillpowerDropThreshold)
                {
                    string detail = Truncate(
                        $"willpower collapsed: {prev} → {curr} ({delta})",
                        _cfg.CandidateDetailMaxLength);
                    candidates.Add((id, new NarrativeEventCandidate(
                        _tick, NarrativeEventKind.WillpowerCollapse,
                        new[] { id }, null, detail)));
                }

                // WillpowerLow: first crossing below threshold after being at or above it
                if (curr < _cfg.WillpowerLowThreshold && prev >= _cfg.WillpowerLowThreshold)
                {
                    string detail = Truncate(
                        $"willpower low: {prev} → {curr}",
                        _cfg.CandidateDetailMaxLength);
                    candidates.Add((id, new NarrativeEventCandidate(
                        _tick, NarrativeEventKind.WillpowerLow,
                        new[] { id }, null, detail)));
                }
            }

            _prevWillpower[id] = curr;
        }

        // ── Proximity-mediated candidates ─────────────────────────────────────
        foreach (var ev in _convEvents)
        {
            int idA     = EntityIntId(ev.Observer);
            int idB     = EntityIntId(ev.Target);
            int primary = Math.Min(idA, idB);
            int[] participants = { Math.Min(idA, idB), Math.Max(idA, idB) };

            string? roomId = RoomNameOf(ev.Observer);
            string  detail = roomId is null
                ? "conversation started"
                : Truncate($"conversation started in {roomId}", _cfg.CandidateDetailMaxLength);

            candidates.Add((primary, new NarrativeEventCandidate(
                _tick, NarrativeEventKind.ConversationStarted,
                participants, roomId, detail)));
        }

        foreach (var ev in _roomEvents)
        {
            // Only "left a room" transitions qualify (OldRoom present means was in a room)
            if (ev.OldRoom is null) continue;

            int id = EntityIntId(ev.Subject);

            // Only abrupt if a DriveSpike fired within the configured window
            if (!_lastSpikeByEntity.TryGetValue(id, out long lastSpike)) continue;
            if (_tick - lastSpike > _cfg.AbruptDepartureWindowTicks) continue;

            string? roomId = ev.OldRoom.Has<IdentityComponent>()
                ? ev.OldRoom.Get<IdentityComponent>().Name
                : null;

            string detail = Truncate(
                roomId is null ? "left room abruptly" : $"left {roomId} abruptly",
                _cfg.CandidateDetailMaxLength);

            candidates.Add((id, new NarrativeEventCandidate(
                _tick, NarrativeEventKind.LeftRoomAbruptly,
                new[] { id }, roomId, detail)));
        }

        // ── Emit in entity-id ascending order (stable within same id) ─────────
        // LINQ OrderBy is a stable sort — ties preserve insertion order, which is
        // deterministic because we iterated entities in Id order above.
        foreach (var (_, c) in candidates.OrderBy(x => x.primaryId))
            _narrativeBus.RaiseCandidate(c);

        _convEvents.Clear();
        _roomEvents.Clear();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string? RoomNameOf(Entity entity)
    {
        var room = _roomMembership.GetRoom(entity);
        if (room is null) return null;
        return room.Has<IdentityComponent>() ? room.Get<IdentityComponent>().Name : null;
    }

    private static int[] DriveSnapshot(SocialDrivesComponent d) =>
        new[]
        {
            d.Belonging.Current,  d.Status.Current,    d.Affection.Current,  d.Irritation.Current,
            d.Attraction.Current, d.Trust.Current,     d.Suspicion.Current,  d.Loneliness.Current,
        };

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen];

    /// <summary>Extracts the lower 32 bits of the entity's deterministic counter-Guid.</summary>
    public static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }
}
