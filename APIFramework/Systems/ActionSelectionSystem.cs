using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
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
/// <remarks>
/// Reads: <see cref="NpcTag"/>, <see cref="SocialDrivesComponent"/>, <see cref="WillpowerComponent"/>,
/// <see cref="InhibitionsComponent"/>, <see cref="PersonalityComponent"/>, <see cref="PositionComponent"/>,
/// <see cref="ProximityComponent"/>, <see cref="CurrentScheduleBlockComponent"/>,
/// <see cref="WorkloadComponent"/>, <see cref="TaskComponent"/>, <see cref="RoomComponent"/>;
/// queries the <see cref="ISpatialIndex"/> and <see cref="EntityRoomMembership"/>.
/// Writes:
///   <list type="bullet">
///     <item><description><see cref="IntendedActionComponent"/> — single writer of this component each tick.</description></item>
///     <item><description><see cref="MovementTargetComponent"/> — added/removed depending on the chosen action.</description></item>
///     <item><description>Enqueues <see cref="WillpowerEventKind.SuppressionTick"/> signals on the
///         <see cref="WillpowerEventQueue"/> when a near-winning candidate was held back by inhibition.</description></item>
///     <item><description>Spawns and despawns ephemeral "flee target" entities when an Avoid action is selected.</description></item>
///   </list>
/// Ordering: must run after <see cref="DriveDynamicsSystem"/> (so drive Currents are fresh) and
/// before <see cref="WillpowerSystem"/> (which drains suppression signals enqueued here).
/// </remarks>
/// <seealso cref="IntendedActionComponent"/>
/// <seealso cref="DriveDynamicsSystem"/>
/// <seealso cref="WillpowerSystem"/>
public sealed class ActionSelectionSystem : ISystem
{
    /// <summary>
    /// Diagnostic tag identifying which enumerator produced a candidate.
    /// Drive candidates come from <see cref="DriveTable"/>; Schedule from the active routine
    /// block; Idle is the always-present fallback; Workload from the NPC's active tasks.
    /// </summary>
    private enum CandidateSource { Drive = 0, Schedule = 1, Idle = 2, Workload = 3 }

    /// <summary>
    /// Canonical ordering of the eight social drives. Used as a stable sort key when the
    /// candidate list exceeds <c>MaxCandidatesPerTick</c> so the truncated set is deterministic.
    /// </summary>
    private enum DriveOrdinal { Belonging = 0, Status = 1, Affection = 2, Irritation = 3, Attraction = 4, Trust = 5, Suspicion = 6, Loneliness = 7 }

    /// <summary>
    /// One row of the static drive→action mapping table. Encodes which drive can produce
    /// which kind of action, the inhibition class that suppresses it, the dialog context
    /// (when applicable), and whether the action requires a nearby target NPC.
    /// </summary>
    /// <param name="Ordinal">Source drive (deterministic sort key).</param>
    /// <param name="InhibClass">Inhibition class that opposes this candidate; <c>null</c> means no inhibition.</param>
    /// <param name="Kind">The kind of <see cref="IntendedActionComponent"/> that would be produced.</param>
    /// <param name="Context">Dialog context written into <see cref="IntendedActionComponent"/> for dialog-kind actions.</param>
    /// <param name="NeedsTarget">When <c>true</c>, one candidate is emitted per nearby NPC. When <c>false</c>, exactly one is emitted.</param>
    private readonly record struct DriveEntry(
        DriveOrdinal   Ordinal,
        InhibitionClass? InhibClass,
        IntendedActionKind Kind,
        DialogContextValue Context,
        bool           NeedsTarget);  // true = multiplied by proximity targets

    /// <summary>
    /// Static drive→candidate mapping. Each <see cref="DriveEntry"/> describes one way a
    /// drive can manifest as an action. Editing this table is a code change — runtime tuning
    /// only affects weights via <see cref="ActionSelectionConfig"/>.
    /// </summary>
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

    /// <summary>
    /// Internal scoring record for one candidate action. Built during enumeration,
    /// optionally re-weighted by personality, and finally consumed by <see cref="PickWinner"/>.
    /// </summary>
    private struct Candidate
    {
        /// <summary>Action kind that would be written into <see cref="IntendedActionComponent"/>.</summary>
        public IntendedActionKind Kind;
        /// <summary>Dialog context (applicable when <see cref="Kind"/> is Dialog).</summary>
        public DialogContextValue Context;
        /// <summary>Target NPC's int id (low-32-bit Guid counter); 0 when no target.</summary>
        public int             TargetEntityId;   // WillpowerSystem.EntityIntId
        /// <summary>Target's full Guid; <see cref="Guid.Empty"/> when no target.</summary>
        public Guid            TargetEntityGuid;
        /// <summary>Sort key — 0–7 for drive entries, 97 for Workload, 98 for Schedule, 99 for Idle.</summary>
        public int             DriveOrdinal;
        /// <summary>Raw drive Current at enumeration time. Used by <see cref="EmitSuppressionEvent"/>.</summary>
        public int             SourceDriveCurrent;
        /// <summary>Maximum matching inhibition strength, normalized to 0–1.</summary>
        public double          Inhibition;       // max matching inhibition strength / 100
        /// <summary>Final scoring weight before tie-break jitter is applied.</summary>
        public double          Weight;
        /// <summary>Diagnostic source tag.</summary>
        public CandidateSource Source;
    }

    // ── Services ──────────────────────────────────────────────────────────────
    private readonly ISpatialIndex        _spatial;
    private readonly EntityRoomMembership _rooms;
    private readonly WillpowerEventQueue  _willpowerQueue;
    private readonly SeededRandom         _rng;
    private readonly ActionSelectionConfig _cfg;
    private readonly ScheduleConfig        _scheduleCfg;
    private readonly WorkloadConfig        _workloadCfg;
    private readonly EntityManager        _em;

    // Flee-target entities: NPC → ephemeral entity positioned at flee point.
    private readonly Dictionary<Entity, Entity> _fleeTargets = new();

    /// <summary>
    /// Constructs the system.
    /// </summary>
    /// <param name="spatial">Spatial index used to query nearby NPCs by tile radius.</param>
    /// <param name="rooms">Room-membership map; supplies the NPC's current room for ambient illumination.</param>
    /// <param name="willpowerQueue">Queue into which suppression signals are pushed; drained by <see cref="WillpowerSystem"/>.</param>
    /// <param name="rng">Seeded RNG for tie-break jitter; preserves replay determinism.</param>
    /// <param name="cfg">Action-selection tuning — thresholds, scaling factors, candidate cap.</param>
    /// <param name="scheduleCfg">Schedule tuning — anchor distance threshold and base weight.</param>
    /// <param name="em">Entity manager — used to spawn/destroy flee-target entities and resolve Guids.</param>
    /// <param name="workloadCfg">Optional workload tuning — supplies <c>WorkActionBaseWeight</c> for work candidates.
    /// Falls back to <see cref="WorkloadConfig"/> defaults when null.</param>
    public ActionSelectionSystem(
        ISpatialIndex        spatial,
        EntityRoomMembership rooms,
        WillpowerEventQueue  willpowerQueue,
        SeededRandom         rng,
        ActionSelectionConfig cfg,
        ScheduleConfig        scheduleCfg,
        EntityManager        em,
        WorkloadConfig?       workloadCfg = null)
    {
        _spatial        = spatial;
        _rooms          = rooms;
        _willpowerQueue = willpowerQueue;
        _rng            = rng;
        _cfg            = cfg;
        _scheduleCfg    = scheduleCfg;
        _workloadCfg    = workloadCfg ?? new WorkloadConfig();
        _em             = em;
    }

    /// <summary>
    /// Per-tick update. For every alive NPC with social drives:
    /// enumerates drive candidates, optionally adds a Schedule candidate (at the active
    /// routine block's anchor) and a Workload candidate (highest-priority active task when
    /// scheduled <c>AtDesk</c>), always adds the Idle fallback, applies personality nudges,
    /// caps to <c>MaxCandidatesPerTick</c>, picks a winner with seeded jitter, writes
    /// <see cref="IntendedActionComponent"/> and updates <see cref="MovementTargetComponent"/>.
    /// Stale flee-target entities for NPCs whose intent no longer is Avoid are cleaned up.
    /// </summary>
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
            if (!LifeStateGuard.IsAlive(npc)) continue;  // WP-3.0.0: skip non-Alive NPCs
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

            // Schedule hint — one extra candidate from ScheduleSystem output.
            if (npc.Has<CurrentScheduleBlockComponent>())
            {
                var sched = npc.Get<CurrentScheduleBlockComponent>();
                if (sched.AnchorEntityId != Guid.Empty)
                {
                    var anchorEntity = FindEntityByGuid(sched.AnchorEntityId);
                    if (anchorEntity != null)
                    {
                        float dist = anchorEntity.Has<PositionComponent>()
                            ? pos.DistanceTo(anchorEntity.Get<PositionComponent>())
                            : float.MaxValue;

                        bool atAnchor = dist < _scheduleCfg.ScheduleLingerThresholdCells;
                        IntendedActionKind schedKind;
                        if (sched.Activity == ScheduleActivityKind.Sleeping)
                            schedKind = IntendedActionKind.Linger;
                        else if (sched.Activity == ScheduleActivityKind.AtDesk && atAnchor)
                            schedKind = IntendedActionKind.Linger;
                        else
                            schedKind = IntendedActionKind.Approach;

                        candidates.Add(new Candidate
                        {
                            Kind               = schedKind,
                            Context            = DialogContextValue.None,
                            TargetEntityId     = WillpowerSystem.EntityIntId(anchorEntity),
                            TargetEntityGuid   = sched.AnchorEntityId,
                            DriveOrdinal       = 98,
                            SourceDriveCurrent = 0,
                            Inhibition         = 0.0,
                            Weight             = _scheduleCfg.ScheduleAnchorBaseWeight,
                            Source             = CandidateSource.Schedule
                        });
                    }
                }
            }

            // Work candidate — emitted when NPC has an active task AND is scheduled AtDesk.
            if (npc.Has<WorkloadComponent>() && npc.Has<CurrentScheduleBlockComponent>())
            {
                var workload = npc.Get<WorkloadComponent>();
                var sched    = npc.Get<CurrentScheduleBlockComponent>();
                var active   = workload.ActiveTasks;
                if (active != null && active.Count > 0 &&
                    sched.Activity == ScheduleActivityKind.AtDesk)
                {
                    // Pick highest-priority active task.
                    Entity? topTask   = null;
                    int     topPriority = -1;
                    foreach (var taskGuid in active)
                    {
                        var te = FindEntityByGuid(taskGuid);
                        if (te == null || !te.Has<TaskComponent>()) continue;
                        int p = te.Get<TaskComponent>().Priority;
                        if (p > topPriority)
                        {
                            topPriority = p;
                            topTask     = te;
                        }
                    }

                    if (topTask != null)
                    {
                        candidates.Add(new Candidate
                        {
                            Kind               = IntendedActionKind.Work,
                            Context            = DialogContextValue.None,
                            TargetEntityId     = WillpowerSystem.EntityIntId(topTask),
                            TargetEntityGuid   = topTask.Id,
                            DriveOrdinal       = 97,
                            SourceDriveCurrent = 0,
                            Inhibition         = 0.0,
                            Weight             = _workloadCfg.WorkActionBaseWeight,
                            Source             = CandidateSource.Workload
                        });
                    }
                }
            }

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

    /// <summary>
    /// Walks <see cref="DriveTable"/> and emits one or more candidates per drive whose
    /// Current exceeds <c>DriveCandidateThreshold</c>. Target-needing entries fan out across
    /// all <paramref name="nearby"/> NPCs; non-target entries emit a single candidate.
    /// </summary>
    /// <param name="drives">Source NPC's social drives.</param>
    /// <param name="wp">Source NPC's willpower component (Current normalized to 0–1).</param>
    /// <param name="inhibs">Source NPC's inhibitions, scanned by class for matching strengths.</param>
    /// <param name="nearby">Deterministically-ordered list of nearby NPCs (excludes self).</param>
    /// <param name="observabilityFactor">Combined ambient-light + witness-count factor in [0, 1].</param>
    /// <param name="out_candidates">Output list — candidates are appended.</param>
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

    /// <summary>
    /// Builds a single <see cref="Candidate"/> from a drive entry, applying approach→avoid
    /// inversion when the action is an Approach, the target stake exceeds
    /// <c>InversionStakeThreshold</c>, inhibition exceeds <c>InversionInhibitionThreshold</c>,
    /// and willpower leakage cannot overcome the inversion barrier.
    /// </summary>
    /// <param name="candidates">Output list to which the new candidate is appended.</param>
    /// <param name="entry">Drive table row driving this candidate.</param>
    /// <param name="push">Drive Current normalized to 0–1.</param>
    /// <param name="inhibition">Maximum opposing inhibition strength normalized to 0–1.</param>
    /// <param name="willpower">NPC willpower Current normalized to 0–1.</param>
    /// <param name="observabilityFactor">Ambient-light × witness-count factor in [0, 1].</param>
    /// <param name="sourceDriveCurrent">Raw drive Current (0–100), preserved for suppression analysis.</param>
    /// <param name="target">Target NPC, or <c>null</c> for non-target candidates.</param>
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

    /// <summary>
    /// Picks the highest-weight candidate, adding a tiny seeded jitter so equal weights
    /// break deterministically across two replays with the same seed but different ordering.
    /// Returns a default Idle candidate if the list is empty.
    /// </summary>
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

    /// <summary>
    /// Finds the highest-raw-push loser whose drive came within <c>SuppressionEpsilon</c> of
    /// beating the winner but was held back by inhibition, and pushes a
    /// <see cref="WillpowerEventKind.SuppressionTick"/> for it onto <see cref="_willpowerQueue"/>.
    /// </summary>
    /// <param name="npc">The NPC whose action was just selected.</param>
    /// <param name="winner">The chosen candidate (excluded from the loser scan).</param>
    /// <param name="candidates">All candidates considered this tick.</param>
    /// <param name="wp">NPC willpower component (currently unused but kept for symmetry / future extension).</param>
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

    /// <summary>
    /// Spawns (or replaces) the ephemeral entity that an Avoid-acting NPC will path toward.
    /// The entity is positioned <c>AvoidStandoffDistance</c> tiles away from the avoidance
    /// target, in the opposite direction. Always destroys the previous flee entity for the
    /// same NPC first so the pathfinder picks up a fresh goal.
    /// </summary>
    /// <param name="npc">The avoiding NPC.</param>
    /// <param name="targetGuid">Guid of the entity being avoided.</param>
    /// <param name="npcPos">The NPC's current world-space position.</param>
    /// <param name="npcTileX">NPC's tile X (currently unused, kept for symmetry).</param>
    /// <param name="npcTileY">NPC's tile Y (currently unused, kept for symmetry).</param>
    /// <returns>The newly created flee-target entity, registered with the spatial index.</returns>
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

    /// <summary>
    /// Unregisters and destroys the flee entity associated with <paramref name="npc"/>, if any,
    /// and removes the bookkeeping entry. No-op when the NPC has no active flee target.
    /// </summary>
    private void CleanFleeTarget(Entity npc)
    {
        if (!_fleeTargets.TryGetValue(npc, out var flee)) return;
        _spatial.Unregister(flee);
        _em.DestroyEntity(flee);
        _fleeTargets.Remove(npc);
    }

    /// <summary>
    /// Linear scan of <see cref="EntityManager.GetAllEntities"/> for an entity with the
    /// given Guid. Returns <c>null</c> when not found (e.g. entity has been destroyed).
    /// </summary>
    private Entity? FindEntityByGuid(Guid guid)
    {
        foreach (var e in _em.GetAllEntities())
            if (e.Id == guid) return e;
        return null;
    }

    // ── Drive readers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <c>Current</c> value of the drive identified by <paramref name="key"/>.
    /// Centralized so the enumeration loop can index into <see cref="SocialDrivesComponent"/>
    /// by ordinal without per-call switch fanout in the hot path.
    /// </summary>
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

    /// <summary>
    /// Returns the maximum strength (normalized to 0–1) of inhibitions in the given class
    /// carried by <paramref name="inhibs"/>. Returns 0 when <paramref name="inhibClass"/> is
    /// <c>null</c> or no matching inhibition exists.
    /// </summary>
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
