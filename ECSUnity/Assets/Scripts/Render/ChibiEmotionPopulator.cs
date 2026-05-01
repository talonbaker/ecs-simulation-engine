using System.Collections.Generic;
using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Drives the <see cref="ChibiEmotionSlot"/> for each NPC based on their current
/// mood and social drive state (WP-3.1.E AT-17).
///
/// LOGIC (per UX bible §3.8)
/// ──────────────────────────
/// Priority order (highest wins; at most 2 icons shown per NPC):
///   1. PanicLevel >= 0.5                       → Sweat (+ Exclamation if >= 0.8)
///   2. EnergyComponent.Energy < 25             → SleepZ
///   3. SocialDrives.Irritation >= 70           → Anger
///   4. SocialDrives.Affection >= 80 (in range) → Heart
///   5. GriefLevel >= 0.4                       → (no anger icon; sad-droopy — IconKind.None for now)
///   6. MoodComponent.Joy >= 0.9                → Sparkle
///
/// DATA SOURCE
/// ────────────
/// Reads from WorldStateDto (primary path) for drive urgencies and physiology.
/// For social drives (Irritation, Affection), reads EngineHost.Engine components
/// directly since they are not yet projected into WorldStateDto (schema v0.4).
///
/// MOUNTING
/// ─────────
/// Attach to any persistent GameObject. Requires NpcSilhouetteRenderer to have
/// already spawned <see cref="NpcSilhouetteInstance"/> objects (which own the slots).
/// </summary>
public sealed class ChibiEmotionPopulator : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private EngineHost _host;

    [Tooltip("How often (in seconds) to refresh emotion icons. 0.1 = 10 Hz.")]
    [SerializeField] private float _refreshInterval = 0.1f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private float _timer;
    private NpcSilhouetteRenderer _silhouetteRenderer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _silhouetteRenderer = Object.FindObjectOfType<NpcSilhouetteRenderer>();
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

        // Collect all ChibiEmotionSlots in the scene.
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
            if (primary != IconKind.None)
                slot.Show(primary);
            else
                slot.Hide();
        }
    }

    private IconKind ComputePrimaryIcon(EntityStateDto entity)
    {
        if (entity?.Physiology == null) return IconKind.None;

        float energy = entity.Physiology.Energy;

        // 1. Sleeping / exhausted → SleepZ.
        if (entity.Physiology.IsSleeping || energy < 15f)
            return IconKind.SleepZ;

        // 2. Very low energy → SleepZ.
        if (energy < 25f)
            return IconKind.SleepZ;

        // 3. Dominant drive in a high-urgency state.
        var drives = entity.Drives;
        if (drives != null)
        {
            // Urgency thresholds for panic/sweat (0–1 scale).
            if (drives.EatUrgency >= 0.8f || drives.DrinkUrgency >= 0.8f)
                return IconKind.Sweat;
        }

        // Remaining checks would come from engine components not in WorldStateDto v0.4.
        // They are stubs here; WP-3.1.E notes this as an architectural gap to close
        // when social drives are projected.

        return IconKind.None;
    }

    // ── Test accessors ────────────────────────────────────────────────────────

    /// <summary>Compute the icon for a given entity DTO without side effects (for tests).</summary>
    public IconKind TestComputeIcon(EntityStateDto entity) => ComputePrimaryIcon(entity);
}
