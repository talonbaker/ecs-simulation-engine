using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Replaces <see cref="NpcDotRenderer"/> from WP-3.1.A.
/// Reads the engine state each frame and renders each live NPC as a layered
/// pixel-art silhouette composed of four sprites: body, hair, headwear, item.
///
/// ARCHITECTURE: DUAL-SOURCE READER
/// ──────────────────────────────────
/// <see cref="WorldStateDto"/> (via <see cref="EngineHost.WorldState"/>) exposes
/// entity positions, physiology (IsSleeping), and drive urgencies — enough for
/// position tracking. It does NOT yet expose silhouette descriptors, intended-action
/// kind, mood, or life state. Those fields live in ECS components that are not
/// projected at schema version 0.4.0.
///
/// NpcSilhouetteRenderer therefore reads from two sources:
///   1. EngineHost.WorldState  — entity list + positions (read-only snapshot).
///   2. EngineHost.Engine      — EntityManager; SilhouetteComponent,
///                               IntendedActionComponent, LifeStateComponent,
///                               MoodComponent, ChokingComponent, FacingComponent.
///
/// Both reads are read-only. Rendering never mutates the engine.
/// This is documented in WP-3.1.B completion note as a known architectural gap
/// to be closed when WorldStateDto v0.5.x adds the silhouette and animation fields.
///
/// ENTITY MAPPING
/// ───────────────
/// WorldStateDto entity IDs are GUID strings (e.g. "00000001-0000-...").
/// EntityManager entities carry Guid Entity.Id. The renderer builds a
/// Dictionary<Guid, Entity> once at boot (lazy on first tick when the engine is
/// ready) and updates it when the entity count changes between frames.
///
/// SPAWN / DESPAWN
/// ─────────────────
/// • New entity → <see cref="NpcSilhouetteInstance.CreateFor"/> called once.
/// • Entity gone from WorldState → GameObject destroyed. Deceased NPCs stay
///   rendered until <see cref="LifeState.Deceased"/> is set (they switch to Dead
///   pose, not immediately destroyed).
///
/// PERFORMANCE
/// ────────────
/// • No per-frame allocations: <c>_entityMap</c> and <c>_seenIds</c> are reused;
///   the seen-IDs set is cleared, not re-created.
/// • Sprite swaps are conditional on state change (NpcSilhouetteInstance guards).
/// • All four layer SpriteRenderers per NPC share per-layer materials from the
///   catalog, enabling Unity dynamic batching across all NPCs at ~4 draw calls.
/// • Animator parameter writes are conditional (NpcAnimatorController guards them).
///
/// FALLBACK PATH
/// ──────────────
/// If <see cref="_catalog"/> is null or if the entity has no SilhouetteComponent,
/// the NPC renders as an invisible instance (no sprites) rather than crashing.
/// NpcDotRenderer is preserved as a separate component — if you want the dot
/// fallback, add NpcDotRenderer alongside this component in the scene.
///
/// WARDEN / RETAIL
/// ───────────────
/// No conditional compilation needed here — the renderer is build-agnostic.
/// The dual-source read is safe in both paths because EngineHost.Engine returns
/// the same EntityManager regardless of projector variant.
/// </summary>
public sealed class NpcSilhouetteRenderer : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("EngineHost that provides both WorldState (DTO) and Engine (EntityManager) each frame.")]
    private EngineHost _engineHost;

    [SerializeField]
    [Tooltip("ScriptableObject mapping silhouette descriptors to sprite assets and shared materials.")]
    private SilhouetteAssetCatalog _catalog;

    [SerializeField]
    [Tooltip("RuntimeAnimatorController assigned to each NPC Animator at spawn. " +
             "Drag Assets/Animations/NpcAnimator.controller here.")]
    private RuntimeAnimatorController _animatorController;

    // ── Runtime state ──────────────────────────────────────────────────────────

    // Per-NPC instances keyed by entity GUID string (matches WorldStateDto entity IDs).
    private readonly Dictionary<string, NpcSilhouetteInstance> _instances = new();

    // Lazily-built GUID → Entity lookup for fast component reads from EntityManager.
    private Dictionary<Guid, Entity> _entityMap;

    // The entity count the last time _entityMap was built; used to detect stale cache.
    private int _entityMapBuiltCount = -1;

    // Parent transform to keep hierarchy tidy.
    private Transform _npcRoot;

    // Reusable set for tracking which entity IDs appeared in the latest WorldState frame.
    private readonly HashSet<string> _seenIds = new();

    // Reusable list for entities to remove (avoids allocating during dictionary iteration).
    private readonly List<string> _toRemove = new();

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _npcRoot = new GameObject("NpcSilhouettes").transform;
        _npcRoot.SetParent(transform, worldPositionStays: false);
    }

    private void LateUpdate()
    {
        if (_engineHost == null) return;

        var worldState = _engineHost.WorldState;
        if (worldState == null) return;

        var em = _engineHost.Engine;   // EntityManager; may be null before boot finishes
        if (em == null) return;

        // Ensure the GUID → Entity map is fresh.
        RefreshEntityMapIfStale(em);

        // Sync instances against the current WorldState entity list.
        _seenIds.Clear();

        foreach (var entity in worldState.Entities)
        {
            // Only render entities with position data.
            if (!entity.Position.HasPosition) continue;

            _seenIds.Add(entity.Id);

            // Look up the ECS Entity for component reads.
            if (!System.Guid.TryParse(entity.Id, out var guid)) continue;
            if (!_entityMap.TryGetValue(guid, out var ecsEntity)) continue;

            // Skip entities that have no silhouette component (non-NPC entities).
            if (!ecsEntity.Has<SilhouetteComponent>()) continue;

            // Create instance on first encounter.
            if (!_instances.TryGetValue(entity.Id, out var inst))
            {
                inst = SpawnInstance(entity.Id, entity.Name, ecsEntity);
                _instances[entity.Id] = inst;
            }

            // Update position and facing.
            float facingDeg = ecsEntity.Has<FacingComponent>()
                ? ecsEntity.Get<FacingComponent>().DirectionDeg
                : 0f;
            inst.UpdatePosition(entity.Position.X, entity.Position.Z, facingDeg);

            // Update animation state from component data.
            var animState = DetermineAnimationState(ecsEntity, entity);
            inst.SetAnimationState(animState);
        }

        // Destroy instances whose entity is no longer in the WorldState.
        _toRemove.Clear();
        foreach (var id in _instances.Keys)
            if (!_seenIds.Contains(id)) _toRemove.Add(id);

        foreach (var id in _toRemove)
        {
            if (_instances.TryGetValue(id, out var inst) && inst != null)
                Destroy(inst.gameObject);
            _instances.Remove(id);
        }
    }

    // ── Spawn ──────────────────────────────────────────────────────────────────

    private NpcSilhouetteInstance SpawnInstance(string entityId, string npcName, Entity ecsEntity)
    {
        var inst = NpcSilhouetteInstance.CreateFor(entityId, npcName, _npcRoot, _catalog);

        // Wire Animator Controller.
        if (_animatorController != null && inst.Animator != null)
            inst.Animator.runtimeAnimatorController = _animatorController;

        // Add the animator controller component that bridges engine state → Animator.
        var animCtrl = inst.gameObject.AddComponent<NpcAnimatorController>();
        animCtrl.Initialise(inst);

        // Apply initial silhouette immediately so first frame looks correct.
        if (ecsEntity.Has<SilhouetteComponent>())
        {
            var sil = ecsEntity.Get<SilhouetteComponent>();
            inst.UpdateSilhouette(
                sil.Height,
                sil.Build,
                sil.Hair,
                sil.Headwear,
                sil.DistinctiveItem,
                sil.DominantColor,
                _catalog);
        }

        return inst;
    }

    // ── Animation state resolution ─────────────────────────────────────────────

    /// <summary>
    /// Maps ECS component data to an <see cref="NpcAnimationState"/> enum value.
    ///
    /// Priority order (highest wins):
    ///   Dead > Panic (choke or panic >= 0.5) > Sleep (incapacitated or IsSleeping)
    ///   > Talk (dialog intent) > Sit (work/linger at desk) > Walk (approach + moving)
    ///   > Idle
    /// </summary>
    private static NpcAnimationState DetermineAnimationState(
        Entity          ecsEntity,
        EntityStateDto  dto)
    {
        // ── 1. Dead ───────────────────────────────────────────────────────────
        if (ecsEntity.Has<LifeStateComponent>())
        {
            var ls = ecsEntity.Get<LifeStateComponent>();
            if (ls.State == LifeState.Deceased)
                return NpcAnimationState.Dead;
        }

        // ── 2. Panic: active choking OR high MoodComponent.PanicLevel ─────────
        if (ecsEntity.Has<ChokingComponent>())
            return NpcAnimationState.Panic;

        if (ecsEntity.Has<MoodComponent>())
        {
            var mood = ecsEntity.Get<MoodComponent>();
            if (mood.PanicLevel >= 0.5f)
                return NpcAnimationState.Panic;
        }

        // ── 3. Sleep: incapacitated (fainting) OR physiology IsSleeping ───────
        if (ecsEntity.Has<LifeStateComponent>())
        {
            var ls = ecsEntity.Get<LifeStateComponent>();
            if (ls.State == LifeState.Incapacitated)
                return NpcAnimationState.Sleep;
        }

        // Also drive Sleep from WorldStateDto physiology so it works in both projector paths.
        if (dto.Physiology.IsSleeping)
            return NpcAnimationState.Sleep;

        // ── 4. Talk: Dialog intent ─────────────────────────────────────────────
        if (ecsEntity.Has<IntendedActionComponent>())
        {
            var intent = ecsEntity.Get<IntendedActionComponent>();
            if (intent.Kind == IntendedActionKind.Dialog)
                return NpcAnimationState.Talk;

            // ── 5. Walk: Approach + IsMoving ──────────────────────────────────
            if (intent.Kind == IntendedActionKind.Approach &&
                dto.Position.IsMoving == true)
                return NpcAnimationState.Walk;

            // ── 6. Sit: Work intent (assumed at-desk) ─────────────────────────
            if (intent.Kind == IntendedActionKind.Work)
                return NpcAnimationState.Sit;
        }

        // ── 7. Idle: default ──────────────────────────────────────────────────
        return NpcAnimationState.Idle;
    }

    // ── Entity map management ──────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the GUID → Entity lookup dictionary if the entity count has
    /// changed since the last build. Rebuilding is O(n) in entity count but
    /// happens infrequently (only on spawn / despawn events).
    ///
    /// Entity count is checked rather than a dirty flag because there is no
    /// EntityManager.OnEntitySpawned event at v0.1.
    /// </summary>
    private void RefreshEntityMapIfStale(EntityManager em)
    {
        int currentCount = em.Entities.Count;
        if (_entityMap != null && _entityMapBuiltCount == currentCount) return;

        _entityMap ??= new Dictionary<Guid, Entity>(currentCount);
        _entityMap.Clear();

        foreach (var e in em.Entities)
            _entityMap[e.Id] = e;

        _entityMapBuiltCount = currentCount;
    }

    // ── Test accessors ─────────────────────────────────────────────────────────

    /// <summary>Count of active NpcSilhouetteInstances. Exposed for play-mode tests.</summary>
    public int ActiveNpcCount => _instances.Count;

    /// <summary>
    /// Returns the <see cref="NpcSilhouetteInstance"/> for a given entity ID, or null.
    /// </summary>
    public NpcSilhouetteInstance GetInstance(string entityId)
        => _instances.TryGetValue(entityId, out var inst) ? inst : null;

    /// <summary>
    /// Returns the GameObject for an entity ID (convenience for legacy test patterns).
    /// </summary>
    public GameObject GetNpcGameObject(string entityId)
    {
        var inst = GetInstance(entityId);
        return inst != null ? inst.gameObject : null;
    }
}
