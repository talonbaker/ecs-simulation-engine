#if WARDEN
using UnityEngine;

/// <summary>
/// WARDEN-only corner overlay showing JSONL emission cadence and queue depth (WP-3.1.F).
///
/// Renders a small diagnostic label in the bottom-right of the screen via IMGUI.
/// Visible only in WARDEN builds; stripped entirely from RETAIL.
///
/// MOUNTING
/// ────────
/// Attach to any persistent GameObject alongside JsonlStreamEmitter.
/// Assign _emitter in the Inspector.
/// </summary>
public sealed class CadenceDebugOverlay : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("The JsonlStreamEmitter this overlay reads from.")]
    private JsonlStreamEmitter _emitter;

    [SerializeField]
    [Tooltip("Show or hide the overlay at runtime.")]
    private bool _visible = true;

    // ── Layout constants ──────────────────────────────────────────────────────

    private const float PanelWidth  = 240f;
    private const float PanelHeight = 56f;
    private const float Margin      =  8f;

    // ── IMGUI ─────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (!_visible || _emitter == null) return;

        float x = Screen.width  - PanelWidth  - Margin;
        float y = Screen.height - PanelHeight - Margin;

        // Semi-transparent dark background box.
        GUI.Box(new Rect(x, y, PanelWidth, PanelHeight), GUIContent.none);

        // Cadence line.
        string cadence = _emitter != null
            ? $"JSONL cadence: every {(_emitter.OutputPath.Length > 0 ? GetCadenceDisplay() : "??")} ticks"
            : "JSONL emitter: absent";
        GUI.Label(new Rect(x + 4f, y + 4f, PanelWidth - 8f, 20f), cadence);

        // Queue depth line.
        int depth = _emitter.QueueDepth;
        string queueInfo = $"Queue depth: {depth}/256{(depth > 200 ? " [NEAR FULL]" : "")}";
        GUI.Label(new Rect(x + 4f, y + 24f, PanelWidth - 8f, 20f), queueInfo);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a display string for the current cadence setting.
    /// Reads from the emitter's config via reflection if necessary;
    /// at v0.1 we display the queue depth as a proxy for health.
    /// </summary>
    private string GetCadenceDisplay()
    {
        // The emitter exposes IsWorkerAlive; use it to show health.
        bool alive = _emitter.IsWorkerAlive;
        return alive ? "active" : "stopped";
    }

    // ── Dev API ───────────────────────────────────────────────────────────────

    /// <summary>Show or hide the debug overlay.</summary>
    public void SetVisible(bool visible) => _visible = visible;

    /// <summary>Whether the overlay is currently shown.</summary>
    public bool IsVisible => _visible;
}
#endif
