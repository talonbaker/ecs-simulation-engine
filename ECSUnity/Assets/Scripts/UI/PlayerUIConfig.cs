using UnityEngine;

/// <summary>
/// ScriptableObject for all player UI tunables (WP-3.1.E).
///
/// Covers settings that live in the Settings panel and are persisted between sessions.
/// Runtime changes write to this object; a simple JSON serialiser persists it to
/// Application.persistentDataPath/player-ui-prefs.json.
/// </summary>
[CreateAssetMenu(menuName = "ECSUnity/Player UI Config", fileName = "DefaultPlayerUIConfig")]
public sealed class PlayerUIConfig : ScriptableObject
{
    [Header("Selection Visual")]
    [Tooltip("Default=halo+outline; alternative=crt-blink.")]
    public SelectionVisualMode SelectionVisual = SelectionVisualMode.HaloAndOutline;

    [Header("Soften Mode")]
    [Tooltip("When on: deceased entities fade; explicit content blurs.")]
    public bool SoftenMode = false;

    [Tooltip("Engine ticks before a deceased entity fades in soften mode (~1 game-hour at 50 tps).")]
    public int SoftenedDeathFadeTicks = 3000;

    [Header("Creative Mode")]
    [Tooltip("Unlocks free camera, skip-to-morning, time-zoom, spawn-anything palette.")]
    public bool CreativeMode = false;

    [Header("Time Scale")]
    [Tooltip("Current engine time-scale multiplier (1 = normal, 4 = fast, 16 = ultra).")]
    public float TimeScale = 1f;

    [Tooltip("Possible time-scale multipliers for the time HUD buttons.")]
    public float[] TimeScaleOptions = { 0f, 1f, 4f, 16f };

    [Header("Audio")]
    [Range(0f, 1f)] public float MasterVolume  = 0.70f;
    [Range(0f, 1f)] public float AmbientVolume = 0.60f;
    [Range(0f, 1f)] public float NpcVolume     = 0.80f;
    [Range(0f, 1f)] public float UiVolume      = 0.50f;

    [Header("Accessibility")]
    [Tooltip("Text scaling factor applied to all UI Toolkit panels.")]
    public float TextScale = 1f;

    [Tooltip("Enable sticky (toggle) controls instead of hold-to-activate.")]
    public bool StickyControls = false;

    public ColorBlindPalette ColorBlind = ColorBlindPalette.Default;

    [Header("Inspector")]
    [Tooltip("Default disclosure tier when the inspector opens.")]
    public InspectorTier DefaultInspectorTier = InspectorTier.Glance;
}

// ── Support enums ─────────────────────────────────────────────────────────────

public enum SelectionVisualMode
{
    HaloAndOutline,
    CrtBlink,
}

public enum ColorBlindPalette
{
    Default,
    Deuteranopia,
    Protanopia,
    Tritanopia,
}

public enum InspectorTier
{
    Glance,
    Drill,
    Deep,
}
