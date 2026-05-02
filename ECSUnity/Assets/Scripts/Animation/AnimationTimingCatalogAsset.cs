using System.IO;
using APIFramework.Systems.Animation;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Unity ScriptableObject that exposes per-archetype animation speed multipliers
/// loaded from <c>archetype-animation-timing.json</c>.
///
/// Wraps <see cref="APIFramework.Systems.Animation.AnimationTimingCatalog"/> so that
/// the pure-C# catalog can be constructed at runtime and used by Unity systems
/// (e.g. Animator speed scaling) while remaining testable by xUnit without Unity.
///
/// USAGE
/// ──────
/// 1. Create via Assets → Create → ECS → AnimationTimingCatalogAsset.
/// 2. Set the JsonAsset field to the archetype-animation-timing.json TextAsset.
/// 3. Reference this asset from EngineHost or NpcSilhouetteRenderer as needed.
/// </summary>
[CreateAssetMenu(menuName = "ECS/AnimationTimingCatalogAsset", fileName = "AnimationTimingCatalog")]
public sealed class AnimationTimingCatalogAsset : ScriptableObject
{
    [SerializeField]
    [Tooltip("TextAsset pointing to docs/c2-content/animation/archetype-animation-timing.json.")]
    private TextAsset _jsonAsset;

    private APIFramework.Systems.Animation.AnimationTimingCatalog _catalog;

    /// <summary>
    /// The loaded catalog. Populated on first access.
    /// Falls back to <see cref="APIFramework.Systems.Animation.AnimationTimingCatalog.Default"/>
    /// if the JSON asset is absent.
    /// </summary>
    public APIFramework.Systems.Animation.AnimationTimingCatalog Catalog
    {
        get
        {
            if (_catalog != null) return _catalog;
            _catalog = _jsonAsset != null
                ? LoadFromJson(_jsonAsset.text)
                : APIFramework.Systems.Animation.AnimationTimingCatalog.Default;
            return _catalog;
        }
    }

    private static APIFramework.Systems.Animation.AnimationTimingCatalog LoadFromJson(string json)
    {
        var dto = JsonConvert.DeserializeObject<TimingJson>(json);
        if (dto?.archetypeAnimationTiming == null)
            return APIFramework.Systems.Animation.AnimationTimingCatalog.Default;

        var entries = new ArchetypeAnimationTiming[dto.archetypeAnimationTiming.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            var r = dto.archetypeAnimationTiming[i];
            entries[i] = new ArchetypeAnimationTiming(r.archetype, r.walkSpeedMult, r.eatSpeedMult, r.talkGesturalRate);
        }
        return new APIFramework.Systems.Animation.AnimationTimingCatalog(entries);
    }

    // ── JSON DTO ──────────────────────────────────────────────────────────────

    private sealed class TimingJson
    {
        public string        schemaVersion           { get; set; }
        public TimingRow[]   archetypeAnimationTiming { get; set; }
    }

    private sealed class TimingRow
    {
        public string archetype        { get; set; }
        public float  walkSpeedMult    { get; set; }
        public float  eatSpeedMult     { get; set; }
        public float  talkGesturalRate { get; set; }
    }
}
