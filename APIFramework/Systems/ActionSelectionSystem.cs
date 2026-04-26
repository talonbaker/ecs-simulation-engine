using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems;

/// <summary>
/// Phase: Cognition (after DriveDynamicsSystem, before WillpowerSystem).
/// Per tick, for each NPC: enumerate drive-based + idle candidates, score them
/// (including approach-avoidance inversion), pick the winner via weighted
/// selection, write IntendedActionComponent, update MovementTargetComponent,
/// and push a SuppressionTick into WillpowerEventQueue when a candidate is
/// actively suppressed.
/// </summary>
public sealed class ActionSelectionSystem : ISystem
{
    // ── Drive ordinals (deterministic sort key) ───────────────────────────────
    private enum DriveOrdinal { Belonging = 0, Status = 1, Affection = 2, Irritation = 3, Attraction = 4, Trust = 5, Suspicion = 6, Loneliness = 7 }

    // ── Static drive-to-candidate mapping (code change needed to alter) ────────
    private readonly record struct DriveEntry(
        DriveOrdinal   Ordinal,
        InhibitionClass? InhibClass,
        IntendedActionKind Kind,
        DialogContextValue Context,
        bool           NeedsTarget);  // true = multiplied by proximity targets

    private static readonly DriveEntry[] DriveTable =
    {
        new(DriveOrdinal.Irritation,  InhibitionClass.Confrontation,         IntendedActionKind.Dialog,  DialogContextValue.LashOut,    true),
        new(DriveOrdinal.Irritation,  InhibitionClass.InterpersonalConflict, IntendedActionKind.Dialog,  DialogContextValue.Complain,   true),
        new(DriveOrdinal.Affection,   InhibitionClass.Vulnerability,         IntendedActionKind.Dialog,  DialogContextValue.Share,      true),
        new(DriveOrdinal.Affection,   InhibitionClass.Vulnerability,         IntendedActionKind.Approach,DialogContextValue.None,       true),
        new(DriveOrdinal.Attraction,  InhibitionClass.Vulnerability,         IntendedActionKind.Approach,DialogContextValue.None,       true),
        new(DriveOrdinal.Loneliness,  InhibitionClass.Vulnerability,         IntendedActionKind.Approach,DialogContextValue.None,       true),
        new(DriveOrdinal.Loneliness,  InhibitionClass.PublicEmotion,         IntendedActionKind.Dialog,  DialogContextValue.Greet,      true),
        new(DriveOrdinal.Belonging,   null,                                  IntendedActionKind.Linger,  DialogContextValue.None,       false),
        new(DriveOrdinal.Suspicion,   null,                                  IntendedActionKind.Linger,  DialogContextValue.None,       false),
        new(DriveOrdinal.Trust,       InhibitionClass.Vulnerability,         IntendedActionKind.Dialog,  DialogContextValue.Share,      true),
        new(DriveOrdinal.Status,      InhibitionClass.PublicEmotion,         IntendedActionKind.Dialog,  DialogContextValue.Encourage,  true),
        new(DriveOrdinal.Status,      InhibitionClass.PublicEmotion,         IntendedActionKind.Dialog,  DialogContextValue.Acknowledge,true),
    };

    // ── Internal candidate record ─────────────────────────────────────────────
    private struct Candidate
    {
        public IntendedActionKind Kind;
        public DialogContextValue Context;
        public int     TargetEntityId;   // WillpowerSystem.EntityIntId
        public Guid    TargetEntityGuid;
        public int     DriveOrdinal;
        public int     SourceDriveCurrent;
        public double  Inhibition;       // max matching inhibition strength / 100
        public double  Weight;
    }

    // ── Services ──────────────────────────────────────────────────────────────
    private readonly ISpatialIndex        _spatial;
    private readonly EntityRoomMembership _rooms;
    private readonly WillpowerEventQueue  _willpowerQueue;
    private readonly SeededRandom         _rng;
    private readonly ActionSelectionConfig _cfg;
    private readonly EntityManager        _em;

    // Flee-target entities: NPC → ephemeral entity positioned at flee point.
    private readonly Dictionary<Entity, Entity> _fleeTargets = new();

    public ActionSelectionSystem(
        ISpatialIndex        spatial,
        EntityRoomMembership rooms,
        WillpowerEventQueue  willpowerQueue,
        SeededRandom         rng,
        ActionSelectionConfig cfg,
        EntityManager        em)
    {
        _spatial        = spatial;
        _rooms          = rooms;
        _willpowerQueue = willpowerQueue;
        _rng            = rng;
        _cfg            = cfg;
        _em             = em;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        // Process NPCs in deterministic order (ascending EntityIntId).
        var npcs = em.Query<NpcTag>()
            .Where(e => e.Has<SocialDrivesComponent>())
            .OrderBy(WillpowerSystem.EntityIntId)
            .ToList();

        // Track which NPCs get Avoid this tick; clean up stale flee targets after.
        var avoidNpcs = new HashSet<Entity>();

        foreach (var npc in npcs)
        {
            var drives   = npc.Get<SocialDrivesComponent>();
            var wp       = npc.Has<WillpowerComponent>()    ? npc.Get<WillpowerComponent>()    : new WillpowerComponent(50, 50);
            var inhibs   = npc.Has<InhibitionsComponent>()  ? npc.Get<InhibitionsComponent>()  : default;
            var pers     = npc.Has<PersonalityComponent>()  ? npc.Get<PersonalityComponent>()  : default;
            var pos      = npc.Has<PositionComponent>()     ? npc.Get<PositionComponent>()     : default;

            int tileX = (int)MathF.Round(pos.X);
            int tileY = (int)MathF.Round(pos.Z);
            int range = npc.Has<ProximityComponent>()
                ? npc.Get<ProximityComponent>().AwarenessRangeTiles
                : 8;

            // Nearby NPCs (excludes self), deterministic order.
            var nearby = _spatial.QueryRadius(tileX, tileY, range)
                .Where(e => e != npc && e.Has<NpcTag>())
                .OrderBy(WillpowerSystem.EntityIntId)
                .ToList();

            // Observability: room illumination + witness count.
            var room = _rooms.GetRoom(npc);
            int ambientLevel = room != null && room.Has<RoomComponent>()
                ? room.Get<RoomComponent>().Illumination.AmbientLevel
                : 50;
            double observabilityFactor = Math.Min(1.0, ambientLevel / 100.0 + nearby.Count * 0.1);

            // Enumerate and score candidates.
            var candidates = new List<Candidate>(_cfg.MaxCandidatesPerTick + 1);
            EnumerateCandidates(drives, wp, inhibs, nearby, observabilityFactor, candidates);

            // Idle is always present.
            candidates.Add(new Candidate
            {
                Kind              = IntendedActionKind.Idle,
                Context           = DialogContextValue.None,
                DriveOrdinal      = 99,
                TargetEntityId    = 0,
                TargetEntityGuid  = Guid.Empty,
                SourceDriveCurrent = 0,
                Inhibition        = 0.0,
                Weight            = _cfg.IdleScoreFloor
            });

            // Cap total candidates (stable sort: lowest ordinal + target id first).
            if (candidates.Count > _cfg.MaxCandidatesPerTick)
            {
                candidates = candidates
                    .OrderBy(c => c.DriveOrdinal)
                    .ThenBy(c => c.TargetEntityId)
                    .Take(_cfg.MaxCandidatesPerTick)
                    .ToList();
            }

            // Apply personality nudge.
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (pers.Conscientiousness > 0 &&
                    (c.Kind == IntendedActionKind.Linger || c.Kind == IntendedActionKind.Idle))
                {
                    c.Weight += pers.Conscientiousness * _cfg.PersonalityTieBreakWeight;
                    candidates[i] = c;
                }
                if (pers.Openness > 0 &&
                    (c.Kind == IntendedActionKind.Approach ||
                     (c.Kind == IntendedActionKind.Dialog && c.Context == DialogContextValue.Share)))
                {
                    c.Weight += pers.Openness * _cfg.PersonalityTieBreakWeight;
                    candidates[i] = c;
                }
            }

            // Add tiny seeded jitter for tie-breaking; pick max weight.
            Candidate winner = PickWinner(candidates);

            // Write intent.
            npc.Add(new IntendedActionComponent(
                winner.Kind,
                winner.TargetEntityId,
                winner.Context,
                (int)Math.Clamp(winner.Weight * 100, 0, 100)));

            // Update movement target.
            if (winner.Kind == IntendedActionKind.Approach && winner.TargetEntityGuid != Guid.Empty)
            {
                npc.Add(new MovementTargetComponent
                {
                    TargetEntityId = winner.TargetEntityGuid,
                    Label          = "approach"
                });
                CleanFleeTarget(npc);
            }
            else if (winner.Kind == IntendedActionKind.Avoid && winner.TargetEntityGuid != Guid.Empty)
            {
                avoidNpcs.Add(npc);
                Entity fleeEntity = SetupFleeEntity(npc, winner.TargetEntityGuid, pos, tileX, tileY);
                npc.Add(new MovementTargetComponent
                {
                    TargetEntityId = fleeEntity.Id,
                    Label          = "avoid"
                });
            }
            else
            {
                if (npc.Has<MovementTargetComponent>())
                    npc.Remove<MovementTargetComponent>();
                CleanFleeTarget(npc);
            }

            // Emit suppression event for the highest-suppressed loser.
            EmitSuppressionEvent(npc, winner, candidates, wp);
        }

        // Clean up flee targets for NPCs that no longer have Avoid intent.
        var staleKeys = _fleeTargets.Keys.Where(n => !avoidNpcs.Contains(n)).ToList();
        foreach (var n in staleKeys)
            CleanFleeTarget(n);
    }

    // ── Candidate enumeration ─────────────────────────────────────────────────

    private void EnumerateCandidates(
        SocialDrivesComponent drives,
        WillpowerComponent    wp,
        InhibitionsComponent  inhibs,
        List<Entity>          nearby,
        double                observabilityFactor,
        List<Candidate>       out_candidates)
    {
        double willpower = wp.Current / 100.0;

        foreach (var entry in DriveTable)
        {
            int driveValue = ReadDrive(drives, entry.Ordinal);
            if (driveValue < _cfg.DriveCandidateThreshold) continue;

            double push      = driveValue / 100.0;
            double inhibition = MaxInhibition(inhibs, entry.InhibClass);

            if (entry.NeedsTarget && nearby.Count > 0)
            {
                foreach (var target in nearby)
                {
                    AddCandidate(out_candidates, entry, push, inhibition, willpower,
                        observabilityFactor, driveValue, target);
                }
            }
            else if (!entry.NeedsTarget)
            {
                AddCandidate(out_candidates, entry, push, inhibition, willpower,
                    observabilityFactor, driveValue, null);
            }
        }
    }

    private void AddCandidate(
        List<Candidate>     candidates,
        DriveEntry          entry,
        double              push,
        double              inhibition,
        double              willpower,
        double              observabilityFactor,
        int                 sourceDriveCurrent,
        Entity?             target)
    {
        // stake: binary proximity (1 if target in range) × observability
        double proximityFactor = (target != null) ? 1.0 : 0.5;
        double stake           = proximityFactor * observabilityFactor;

        var kind = entry.Kind;

        // Approach-avoidance inversion.
        if (kind == IntendedActionKind.Approach && target != null &&
            stake      > _cfg.InversionStakeThreshold &&
            inhibition > _cfg.InversionInhibitionThreshold)
        {
            // Inversion holds unless willpower leakage overcomes the barrier.
            double giveUpStrength    = (1.0 - willpower) * push * _cfg.SuppressionGiveUpFactor;
            double inversionBarrier  = (inhibition - _cfg.InversionInhibitionThreshold) * stake;
            if (giveUpStrength < inversionBarrier)
                kind = IntendedActionKind.Avoid;
        }

        double weight = push * (1.0 - inhibition)
                      + (1.0 - willpower) * push * _cfg.SuppressionGiveUpFactor;

        candidates.Add(new Candidate
        {
            Kind               = kind,
            Context            = entry.Context,
            TargetEntityId     = target != null ? WillpowerSystem.EntityIntId(target) : 0,
            TargetEntityGuid   = target?.Id ?? Guid.Empty,
            DriveOrdinal       = (int)entry.Ordinal,
            SourceDriveCurrent = sourceDriveCurrent,
            Inhibition         = inhibition,
            Weight             = weight
        });
    }

    // ── Winner selection ──────────────────────────────────────────────────────

    private Candidate PickWinner(List<Candidate> candidates)
    {
        if (candidates.Count == 0)
            return new Candidate { Kind = IntendedActionKind.Idle, Weight = _cfg.IdleScoreFloor };

        // Add tiny seeded jitter so equal weights break deterministically.
        const double jitterScale = 1e-4;
        double bestWeight = double.MinValue;
        int    bestIdx    = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            double w = candidates[i].Weight + _rng.NextDouble() * jitterScale;
            if (w > bestWeight)
            {
                bestWeight = w;
                bestIdx    = i;
            }
        }
        return candidates[bestIdx];
    }

    // ── Suppression event emission ────────────────────────────────────────────

    private void EmitSuppressionEvent(
        Entity          npc,
        Candidate       winner,
        List<Candidate> candidates,
        WillpowerComponent wp)
    {
        // Find the highest-weight loser that was "actively suppressed":
        // its raw drive push would have nearly beaten the winner, but inhibition held it back.
        Candidate? bestSuppressed = null;
        double     bestRawPush    = 0.0;

        foreach (var c in candidates)
        {
            if (c.TargetEntityId == winner.TargetEntityId &&
                c.Kind            == winner.Kind &&
                c.Context         == winner.Context)
                continue; // skip winner itself

            if (c.Inhibition <= 0.0) continue;
            if (c.SourceDriveCurrent <= 0) continue;

            double rawPush = c.SourceDriveCurrent / 100.0;
            if (rawPush <= winner.Weight) continue;           // wasn't close to winning
            if (rawPush - winner.Weight >= _cfg.SuppressionEpsilon) continue; // too far away

            if (rawPush > bestRawPush)
            {
                bestRawPush    = rawPush;
                bestSuppressed = c;
            }
        }

        if (bestSuppressed.HasValue)
        {
            int magnitude = (int)Math.Max(1, bestSuppressed.Value.SourceDriveCurrent / 100.0
                                            * _cfg.SuppressionEventMagnitudeScale);
            _willpowerQueue.Enqueue(new WillpowerEventSignal(
                WillpowerSystem.EntityIntId(npc),
                WillpowerEventKind.SuppressionTick,
                magnitude));
        }
    }

    // ── Movement / flee target helpers ────────────────────────────────────────

    private Entity SetupFleeEntity(Entity npc, Guid targetGuid, PositionComponent npcPos,
                                   int npcTileX, int npcTileY)
    {
        // Destroy previous flee entity to force PathfindingTriggerSystem to recompute.
        CleanFleeTarget(npc);

        // Find target position.
        Entity? targetEntity = FindEntityByGuid(targetGuid);
        float targetX = npcPos.X + (float)_cfg.AvoidStandoffDistance; // fallback direction
        float targetZ = npcPos.Z;

        if (targetEntity != null && targetEntity.Has<PositionComponent>())
        {
            var tp = targetEntity.Get<PositionComponent>();
            targetX = tp.X;
            targetZ = tp.Z;
        }

        float dx = npcPos.X - targetX;
        float dz = npcPos.Z - targetZ;
        float dist = MathF.Sqrt(dx * dx + dz * dz);
        if (dist < 0.001f) { dx = 1.0f; dz = 0.0f; dist = 1.0f; }

        float fleeX = npcPos.X + (dx / dist) * _cfg.AvoidStandoffDistance;
        float fleeZ = npcPos.Z + (dz / dist) * _cfg.AvoidStandoffDistance;

        var fleeEntity = _em.CreateEntity();
        fleeEntity.Add(new PositionComponent { X = fleeX, Y = 0f, Z = fleeZ });
        _spatial.Register(fleeEntity,
            (int)MathF.Round(fleeX),
            (int)MathF.Round(fleeZ));

        _fleeTargets[npc] = fleeEntity;
        return fleeEntity;
    }

    private void CleanFleeTarget(Entity npc)
    {
        if (!_fleeTargets.TryGetValue(npc, out var flee)) return;
        _spatial.Unregister(flee);
        _em.DestroyEntity(flee);
        _fleeTargets.Remove(npc);
    }

    private Entity? FindEntityByGuid(Guid guid)
    {
        foreach (var e in _em.GetAllEntities())
            if (e.Id == guid) return e;
        return null;
    }

    // ── Drive readers ─────────────────────────────────────────────────────────

    private static int ReadDrive(SocialDrivesComponent d, DriveOrdinal key) => key switch
    {
        DriveOrdinal.Belonging  => d.Belonging.Current,
        DriveOrdinal.Status     => d.Status.Current,
        DriveOrdinal.Affection  => d.Affection.Current,
        DriveOrdinal.Irritation => d.Irritation.Current,
        DriveOrdinal.Attraction => d.Attraction.Current,
        DriveOrdinal.Trust      => d.Trust.Current,
        DriveOrdinal.Suspicion  => d.Suspicion.Current,
        DriveOrdinal.Loneliness => d.Loneliness.Current,
        _                       => 0
    };

    private static double MaxInhibition(InhibitionsComponent inhibs, InhibitionClass? inhibClass)
    {
        if (inhibClass == null) return 0.0;
        double max = 0.0;
        foreach (var inh in inhibs.Inhibitions)
            if (inh.Class == inhibClass.Value && inh.Strength / 100.0 > max)
                max = inh.Strength / 100.0;
        return max;
    }
}
