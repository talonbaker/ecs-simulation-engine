using System.Collections.Generic;
using APIFramework.Systems.Animation;
using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Drives the <see cref="ChibiEmotionSlot"/> for each NPC based on their current
/// mood and social drive state. Now catalog-driven per WP-4.0.E (MAC-013):
/// fade altitude, scale multiplier, and anchor offset are read from
/// <see cref="NpcVisualStateCatalog"/> rather than hardcoded.
///
/// PRIORITY ORDER (highest wins; at most 1 icon shown per NPC per UX bible §3.8)
/// ──────────────────────────────────────────────────────────────────────────────
///   1. Sweat    — PanicLevel >= 0.5 or EatUrgency/DrinkUrgency >= 0.8
///   2. SleepZ   — Energy < 25 or IsSleeping
///   (future: Anger, RedFaceFlush, GreenFaceNausea, Heart, Sparkle, Exclamation
///    when social drives are projected into WorldStateDto v0.5+)
///
/// ALTITUDE FADE
/// ─────────────
/// Each cue fades between catalog fadeAltitudeStart and fadeAltitudeEnd.
/// Camera.main.transform.position.y is used as the altitude source.
///
/// DATA SOURCE
/// ────────────
/// Reads from WorldStateDto (primary path). Social drives remain stubs until
/// projected (WorldStateDto schema v0.5+).
/// </summary>
public sealed class ChibiEmotionPopulator : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private EngineHost _host;

    [Tooltip("How often (in seconds) to refresh emotion icons. 0.1 = 10 Hz.")]
    [SerializeField] private float _refreshInterval = 0.1f;

    [Tooltip("Catalog loader providing per-cue fade altitude, scale, and anchor offset.")]
    [SerializeField] private NpcVisualStateCatalogLoader _catalogLoader;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private float                 _timer;
    private NpcSilhouetteRenderer _silhouetteRenderer;
    private NpcVisualStateCatalog _catalog;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _silhouetteRenderer = Object.FindObjectOfType<NpcSilhouetteRenderer>();
        _catalog = _catalogLoader != null
            ? _catalogLoader.Catalog
            : NpcVisualStateCatalogLoader.Empty;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < _refreshInterval) return;
        _timer = 0f;
        Refresh();
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private void Refresh()
    {
        var worldState = _host?.WorldState;
        if (worldState?.Entities == null) return;

        float altitude = Camera.main != null ? Camera.main.transform.position.y : 15f;

        var slots = Object.FindObjectsOfType<ChibiEmotionSlot>();
        var slotMap = new Dictionary<string, ChibiEmotionSlot>();
        foreach (var slot in slots)
        {
            var tag = slot.GetComponentInParent<SelectableTag>();
            if (tag != null && !string.IsNullOrEmpty(tag.EntityId))
                slotMap[tag.EntityId] = slot;
        }

        foreach (var entity in worldState.Entities)
        {
            if (!slotMap.TryGetValue(entity.Id, out ChibiEmotionSlot slot)) continue;

            IconKind primary = ComputePrimaryIcon(entity);
            if (primary == IconKind.None)
            {
                slot.Hide();
                continue;
            }

            slot.Show(primary);
            ApplyCatalogParams(slot, primary, altitude);
        }
    }

    private void ApplyCatalogParams(ChibiEmotionSlot slot, IconKind kind, float altitude)
    {
        string cueId    = IconKindToCueId(kind);
        var    cueEntry = _catalog?.GetCue(cueId);
        if (cueEntry == null)
        {
            // No catalog entry: show at full alpha, default scale, no offset.
            slot.ApplyDisplayParams(1f, 1f, Vector3.zero);
            return;
        }

        float alpha = NpcVisualStateCatalog.ComputeCueAlpha(
            altitude, cueEntry.FadeAltitudeStart, cueEntry.FadeAltitudeEnd);
        float scale = Mathf.Max(cueEntry.MinScaleMult, 1f);
        var   anchor = cueEntry.AnchorOffset != null && cueEntry.AnchorOffset.Length >= 3
            ? new Vector3(cueEntry.AnchorOffset[0], cueEntry.AnchorOffset[1], cueEntry.AnchorOffset[2])
            : Vector3.zero;

        slot.ApplyDisplayParams(alpha, scale, anchor);
    }

    private IconKind ComputePrimaryIcon(EntityStateDto entity)
    {
        if (entity?.Physiology == null) return IconKind.None;

        float energy = entity.Physiology.Energy;

        // Panic / high urgency → Sweat.
        var drives = entity.Drives;
        if (drives != null && (drives.EatUrgency >= 0.8f || drives.DrinkUrgency >= 0.8f))
            return IconKind.Sweat;

        // Sleeping / exhausted → SleepZ.
        if (entity.Physiology.IsSleeping || energy < 15f)
            return IconKind.SleepZ;

        if (energy < 25f)
            return IconKind.SleepZ;

        // Remaining cues (Anger, RedFaceFlush, etc.) require social drives projected
        // into WorldStateDto v0.5+; stubs here per WP-3.1.E architectural note.
        return IconKind.None;
    }

    // ── Cue ID mapping ────────────────────────────────────────────────────────

    private static string IconKindToCueId(IconKind kind) => kind switch
    {
        IconKind.Anger           => "anger-lines",
        IconKind.Sweat           => "sweat",
        IconKind.SleepZ          => "sleep-z",
        IconKind.RedFaceFlush    => "red-face-flush",
        IconKind.GreenFaceNausea => "green-face-nausea",
        IconKind.Heart           => "heart",
        IconKind.Sparkle         => "sparkles",
        IconKind.Exclamation     => "exclamation",
        IconKind.QuestionMark    => "question-mark",
        IconKind.Stink           => "stink",
        _                        => string.Empty,
    };

    // ── Test accessors ────────────────────────────────────────────────────────

    /// <summary>Compute the icon for a given entity DTO without side effects (for tests).</summary>
    public IconKind TestComputeIcon(EntityStateDto entity) => ComputePrimaryIcon(entity);

    /// <summary>Maps an IconKind to its catalog cue ID string (for tests).</summary>
    public static string TestIconKindToCueId(IconKind kind) => IconKindToCueId(kind);

    /// <summary>Injects a catalog directly for tests, bypassing the ScriptableObject.</summary>
    public void InjectCatalog(NpcVisualStateCatalog catalog)
    {
        _catalog = catalog ?? NpcVisualStateCatalogLoader.Empty;
    }
}
