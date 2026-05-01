using UnityEngine;

/// <summary>
/// Maps prop prefab names to engine archetype identifier strings.
/// Add one entry per draggable prop type; future props extend the list.
/// </summary>
[CreateAssetMenu(fileName = "PropArchetypeMap", menuName = "Build Mode/Prop Archetype Map")]
public sealed class PropArchetypeMap : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        [Tooltip("Prefab name exactly as it appears in Unity (e.g. 'Table', 'Banana'). " +
                 "Runtime instances named 'Table(Clone)' are matched after stripping '(Clone)'.")]
        public string PrefabName;

        [Tooltip("Engine archetype identifier passed to IWorldMutationApi.")]
        public string ArchetypeId;
    }

    [SerializeField] private Entry[] _entries = System.Array.Empty<Entry>();

    /// <summary>
    /// Finds the archetype for the given prefab name (case-sensitive).
    /// Returns false when no entry matches.
    /// </summary>
    public bool TryGetArchetype(string prefabName, out string archetypeId)
    {
        foreach (var e in _entries)
        {
            if (e.PrefabName == prefabName)
            {
                archetypeId = e.ArchetypeId;
                return true;
            }
        }
        archetypeId = null;
        return false;
    }
}
