using System;
using System.Collections.Generic;
using UnityEngine;
using APIFramework.Core;

/// <summary>
/// Scene orchestrator — runs every frame, reads SimulationManager.Snapshot,
/// and creates / updates / destroys GameObjects to match the engine state.
///
/// ADD THIS to an empty GameObject named "WorldSceneBuilder" in the scene.
/// It owns all world-object cubes and entity cubes.
/// </summary>
public class WorldSceneBuilder : MonoBehaviour
{
    // ── Inspector fields ──────────────────────────────────────────────────────
    [Header("Scale")]
    [Tooltip("Converts ECS world-units → Unity metres.  1 = 1 unit per metre.")]
    public float worldScale = 1f;

    [Header("Optional Parents (assign in Inspector or leave null)")]
    public Transform worldObjectRoot;
    public Transform entityRoot;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private readonly Dictionary<Guid, WorldObjectCubeView> _worldObjViews = new();
    private readonly Dictionary<Guid, EntityCubeView>      _entityViews   = new();

    // ── World-object cube sizes (x, y, z Unity units) ────────────────────────
    private static readonly Vector3 FridgeScale  = new(1.0f, 2.0f, 0.8f);
    private static readonly Vector3 SinkScale    = new(0.8f, 0.5f, 0.6f);
    private static readonly Vector3 ToiletScale  = new(0.8f, 0.7f, 1.0f);
    private static readonly Vector3 BedScale     = new(2.0f, 0.4f, 1.2f);

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        BuildFloor();
    }

    void Update()
    {
        var snap = SimulationManager.Snapshot;
        if (snap == null) return;

        SyncWorldObjects(snap);
        SyncEntities(snap);
    }

    // ── Floor ─────────────────────────────────────────────────────────────────

    private static void BuildFloor()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        // Plane default = 10×10 units.  Scale 5 → 50×50 — big enough for any layout.
        floor.transform.localScale  = new Vector3(1.2f, 1f, 1.2f); // 12×12 units — fits 10×10 world
        floor.transform.position    = Vector3.zero;
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.20f, 0.20f, 0.22f);   // dark grey floor
        floor.GetComponent<Renderer>().material = mat;
    }

    // ── World objects ─────────────────────────────────────────────────────────

    private void SyncWorldObjects(SimulationSnapshot snap)
    {
        var seen = new HashSet<Guid>();

        foreach (var obj in snap.WorldObjects)
        {
            seen.Add(obj.Id);

            if (!_worldObjViews.TryGetValue(obj.Id, out var view))
                view = SpawnWorldObjectCube(obj);

            // Position (Y adjusted so cube sits on floor: half its height above y=0)
            float halfH = view.transform.localScale.y * 0.5f;
            view.transform.position = new Vector3(
                obj.X * worldScale,
                obj.Y * worldScale + halfH,
                obj.Z * worldScale);

            view.UpdateFromSnapshot(obj, worldScale);
        }

        // Remove views for objects that no longer exist
        foreach (var stale in StaleKeys(_worldObjViews.Keys, seen))
        {
            Destroy(_worldObjViews[stale].gameObject);
            _worldObjViews.Remove(stale);
        }
    }

    private WorldObjectCubeView SpawnWorldObjectCube(WorldObjectSnapshot obj)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = obj.Name;
        if (worldObjectRoot) go.transform.SetParent(worldObjectRoot);
        go.transform.localScale = ScaleFor(obj);

        var view = go.AddComponent<WorldObjectCubeView>();
        _worldObjViews[obj.Id] = view;
        return view;
    }

    private static Vector3 ScaleFor(WorldObjectSnapshot obj)
    {
        if (obj.IsFridge)  return FridgeScale;
        if (obj.IsSink)    return SinkScale;
        if (obj.IsToilet)  return ToiletScale;
        if (obj.IsBed)     return BedScale;
        return Vector3.one;
    }

    // ── Living entities ───────────────────────────────────────────────────────

    private void SyncEntities(SimulationSnapshot snap)
    {
        var seen = new HashSet<Guid>();

        foreach (var entity in snap.LivingEntities)
        {
            if (!entity.HasPosition) continue;
            seen.Add(entity.Id);

            if (!_entityViews.TryGetValue(entity.Id, out var view))
                view = SpawnEntityCube(entity);

            view.UpdateFromSnapshot(entity, snap.TransitItems, worldScale);
        }

        foreach (var stale in StaleKeys(_entityViews.Keys, seen))
        {
            Destroy(_entityViews[stale].gameObject);
            _entityViews.Remove(stale);
        }
    }

    private EntityCubeView SpawnEntityCube(EntitySnapshot entity)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = entity.Name;
        if (entityRoot) go.transform.SetParent(entityRoot);
        go.transform.localScale = Vector3.one;

        // Start at the entity's actual position so there is no spawn-slide
        go.transform.position = new Vector3(
            entity.PosX * worldScale,
            entity.PosY * worldScale + 0.5f,
            entity.PosZ * worldScale);

        var view = go.AddComponent<EntityCubeView>();
        _entityViews[entity.Id] = view;
        return view;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    /// Returns keys that are in <paramref name="existing"/> but not in <paramref name="current"/>.
    private static List<Guid> StaleKeys(IEnumerable<Guid> existing, HashSet<Guid> current)
    {
        var stale = new List<Guid>();
        foreach (var id in existing)
            if (!current.Contains(id)) stale.Add(id);
        return stale;
    }
}
