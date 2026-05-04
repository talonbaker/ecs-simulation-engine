using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.VFX;
using APIFramework.Systems.Visual;

/// <summary>
/// ScriptableObject catalog mapping each <see cref="ParticleTriggerKind"/> to a
/// <see cref="VisualEffectAsset"/> and spawn parameters.
///
/// Populate via the Inspector or load from the JSON mirror at
/// <c>docs/c2-content/visual/particle-trigger-catalog.json</c>.
/// Modders extend by appending entries to the JSON and authoring the matching VFX asset.
/// </summary>
[CreateAssetMenu(menuName = "ECSUnity/ParticleTriggerCatalog", fileName = "DefaultParticleTriggerCatalog")]
public sealed class ParticleTriggerCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public ParticleTriggerKind Kind;
        [Tooltip("VFX Graph asset for this trigger.")]
        public VisualEffectAsset   VfxAsset;
        [Tooltip("Seconds before the spawned VFX instance is destroyed.")]
        public float               LifetimeSeconds = 2f;
        [Tooltip("Default intensity multiplier (0..1).")]
        [Range(0f, 1f)]
        public float               DefaultIntensity = 1f;
    }

    [SerializeField]
    private List<Entry> _entries = new();

    private Dictionary<ParticleTriggerKind, Entry> _lookup;

    private void OnEnable() => BuildLookup();

    /// <summary>Returns the catalog entry for <paramref name="kind"/>, or null if not registered.</summary>
    public Entry GetByKind(ParticleTriggerKind kind)
    {
        if (_lookup == null) BuildLookup();
        _lookup.TryGetValue(kind, out var entry);
        return entry;
    }

    /// <summary>All registered entries (read-only for tests).</summary>
    public IReadOnlyList<Entry> Entries => _entries;

    private void BuildLookup()
    {
        _lookup = new Dictionary<ParticleTriggerKind, Entry>();
        foreach (var e in _entries)
            _lookup[e.Kind] = e;
    }

    // ── JSON loading ──────────────────────────────────────────────────────��──

    [Serializable]
    private sealed class JsonRoot
    {
        public string schemaVersion;
        public List<JsonEntry> particleTriggers;
    }

    [Serializable]
    private sealed class JsonEntry
    {
        public string kind;
        public string vfxAsset;
        public float  lifetimeSec     = 2f;
        public float  defaultIntensity = 1f;
    }

    /// <summary>
    /// Validates that the JSON catalog contains entries for all 10 known kinds.
    /// Used by unit tests; does not modify the ScriptableObject.
    /// </summary>
    public static bool ValidateJson(string json, out List<string> errors)
    {
        errors = new List<string>();
        JsonRoot root;
        try { root = JsonUtility.FromJson<JsonRoot>(json); }
        catch (Exception ex) { errors.Add($"Parse error: {ex.Message}"); return false; }

        if (root?.particleTriggers == null) { errors.Add("particleTriggers array missing"); return false; }

        var allKinds = Enum.GetNames(typeof(ParticleTriggerKind));
        var found    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in root.particleTriggers)
        {
            if (string.IsNullOrEmpty(e.kind))   { errors.Add("Entry with empty kind"); continue; }
            if (string.IsNullOrEmpty(e.vfxAsset)){ errors.Add($"Entry '{e.kind}' has no vfxAsset"); }
            found.Add(e.kind);
        }

        foreach (var k in allKinds)
            if (!found.Contains(k))
                errors.Add($"Missing entry for {k}");

        return errors.Count == 0;
    }
}
