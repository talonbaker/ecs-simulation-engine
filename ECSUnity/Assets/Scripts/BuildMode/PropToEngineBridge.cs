using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Listens to every DraggableProp.OnDropped in the scene and forwards drops
/// to IWorldMutationApi via BuildModeController, so the engine tracks each
/// prop placement as live world state.
///
/// Inspector wiring required in MainScene:
///   _buildModeController → BuildModeController GameObject
///   _archetypeMap        → Assets/Resources/PropArchetypeMap.asset
///
/// This MonoBehaviour auto-subscribes to all DraggableProp instances present
/// at Start; drag-placed props spawned dynamically call Subscribe() explicitly.
/// </summary>
public sealed class PropToEngineBridge : MonoBehaviour
{
    [SerializeField]
    [Tooltip("BuildModeController — the bridge borrows its IWorldMutationApi.")]
    private BuildModeController _buildModeController;

    [SerializeField]
    [Tooltip("Prefab-name to engine archetype mapping. Drag PropArchetypeMap.asset here.")]
    private PropArchetypeMap _archetypeMap;

    // Prop → engine entity id (set on first drop via SpawnStructural).
    private readonly Dictionary<DraggableProp, Guid>    _entityIds       = new();
    // Prop → last stable world position (for snap-back on engine rejection).
    private readonly Dictionary<DraggableProp, Vector3> _stablePositions = new();

    private void Start()
    {
        foreach (var prop in FindObjectsOfType<DraggableProp>())
            Subscribe(prop);
    }

    /// <summary>Subscribe a prop that was added to the scene after Start.</summary>
    public void Subscribe(DraggableProp prop)
    {
        if (_stablePositions.ContainsKey(prop)) return;
        _stablePositions[prop] = prop.transform.position;
        prop.OnDropped         += OnPropDropped;
    }

    private void OnPropDropped(DraggableProp prop, Vector3 dropPosition)
    {
        var api = _buildModeController?.MutationApi;
        if (api == null)
        {
            Debug.LogWarning("[PropToEngineBridge] MutationApi not ready; drop not forwarded to engine.");
            return;
        }

        int tileX = Mathf.RoundToInt(dropPosition.x);
        int tileY = Mathf.RoundToInt(dropPosition.z);

        string prefabName = prop.gameObject.name.Replace("(Clone)", "").Trim();
        if (_archetypeMap != null && _archetypeMap.TryGetArchetype(prefabName, out string archetypeId))
            Debug.Log($"[PropToEngineBridge] Drop: '{prefabName}' (archetype='{archetypeId}') → tile ({tileX},{tileY})");
        else
            Debug.Log($"[PropToEngineBridge] Drop: '{prefabName}' (no archetype entry) → tile ({tileX},{tileY})");

        try
        {
            if (_entityIds.TryGetValue(prop, out Guid existingId))
            {
                api.MoveEntity(existingId, tileX, tileY);
            }
            else
            {
                Guid newId = api.SpawnStructural(tileX, tileY);
                _entityIds[prop] = newId;
            }
            _stablePositions[prop] = dropPosition;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PropToEngineBridge] Engine rejected drop for '{prop.name}': {ex.Message}. Snapping back.");
            if (_stablePositions.TryGetValue(prop, out Vector3 stablePos))
            {
                // If the prop is still parented to a socket, restore it in-place rather than
                // detaching it — detaching a socketed banana returns it to floor level and lets
                // physics push the table (causing the "table disappears" bug).
                if (prop.transform.parent?.GetComponent<PropSocket>() != null)
                {
                    prop.transform.localPosition = Vector3.zero;
                    prop.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    prop.transform.SetParent(null);
                    prop.transform.position = stablePos;
                }
            }
        }
    }
}
