#if WARDEN
using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using UnityEngine;

/// <summary>Display mode for the NPC introspection overlay.</summary>
public enum NpcIntrospectionMode
{
    /// <summary>No overlays shown. Default.</summary>
    Off,
    /// <summary>Overlay only on the currently selected NPC.</summary>
    Selected,
    /// <summary>Overlay on every Alive / Incapacitated NPC.</summary>
    All,
}

/// <summary>
/// WARDEN-only dev overlay: floats per-NPC introspection text above each NPC's
/// world position. Reads existing ECS components — no engine mutations.
///
/// MODES
/// ──────
///   Off      — no overlays (default).
///   Selected — overlay only on the currently selected NPC (via SelectionController).
///   All      — overlay on every Alive/Incapacitated NPC.
///
/// TOGGLE
/// ───────
///   F2 cycles Off → Selected → All → Off  (via <see cref="NpcIntrospectionToggle"/>).
///   `introspect` console verb sets the mode explicitly.
///
/// RENDERING
/// ──────────
///   IMGUI OnGUI. Text content throttled to <see cref="_updateHz"/> Hz.
///   Screen positions re-projected every frame for smooth tracking.
///
/// OVERLAP AVOIDANCE
/// ──────────────────
///   In All mode, rows sorted by ascending screen-y and nudged apart so no two
///   are closer than <see cref="_minPixelSpacing"/> pixels.
///
/// WARDEN STRIPPING
/// ─────────────────
///   Entire class inside #if WARDEN — compiles out completely in RETAIL builds.
/// </summary>
public sealed class NpcIntrospectionOverlay : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("EngineHost providing WorldState and EntityManager. Auto-found if null.")]
    private EngineHost _host;

    [SerializeField]
    [Tooltip("SelectionController used to identify the selected NPC in Selected mode. Auto-found if null.")]
    private SelectionController _selection;

    [SerializeField]
    [Tooltip("World-unit Y offset above the NPC's floor position where the overlay anchors.")]
    [Range(0.5f, 4f)]
    private float _worldYOffset = 1.8f;

    [SerializeField]
    [Tooltip("Minimum pixel gap between stacked overlay panels in All mode.")]
    [Range(40f, 220f)]
    private float _minPixelSpacing = 90f;

    [SerializeField]
    [Tooltip("Text content refresh rate in Hz. 4 Hz is smooth enough for dev use.")]
    [Range(1f, 30f)]
    private float _updateHz = 4f;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private NpcIntrospectionMode _mode = NpcIntrospectionMode.Off;

    private readonly Dictionary<string, NpcIntrospectionTextRow> _rows    = new();
    private readonly List<NpcIntrospectionTextRow>               _visible = new();
    private readonly List<string>                                _toRemove = new();

    private float _nextUpdateTime;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Current display mode.</summary>
    public NpcIntrospectionMode Mode => _mode;

    /// <summary>Set the display mode explicitly.</summary>
    public void SetMode(NpcIntrospectionMode mode)
    {
        _mode = mode;
        if (mode == NpcIntrospectionMode.Off)
        {
            _rows.Clear();
            _visible.Clear();
        }
    }

    /// <summary>Cycle Off → Selected → All → Off.</summary>
    public void CycleMode()
    {
        SetMode(_mode switch
        {
            NpcIntrospectionMode.Off      => NpcIntrospectionMode.Selected,
            NpcIntrospectionMode.Selected => NpcIntrospectionMode.All,
            _                             => NpcIntrospectionMode.Off,
        });
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_host      == null) _host      = FindObjectOfType<EngineHost>();
        if (_selection == null) _selection = FindObjectOfType<SelectionController>();
    }

    private void LateUpdate()
    {
        if (_mode == NpcIntrospectionMode.Off) return;
        if (_host == null) return;

        var em = _host.Engine;
        if (em == null) return;

        Camera cam = Camera.main;

        bool doRefresh = Time.unscaledTime >= _nextUpdateTime;
        if (doRefresh)
            _nextUpdateTime = Time.unscaledTime + 1f / _updateHz;

        // Determine the selected entity ID (for Selected mode).
        string selectedId = null;
        if (_mode == NpcIntrospectionMode.Selected)
        {
            var sel = _selection?.Current;
            if (sel == null) { _visible.Clear(); return; }
            selectedId = sel.EntityId;
        }

        _visible.Clear();

        foreach (var entity in em.Entities)
        {
            if (!entity.Has<IdentityComponent>())    continue;
            if (!entity.Has<LifeStateComponent>())   continue;
            if (!entity.Has<PositionComponent>())    continue;

            // Skip Deceased.
            if (entity.Get<LifeStateComponent>().State == LifeState.Deceased) continue;

            // Filter by mode.
            if (_mode == NpcIntrospectionMode.Selected &&
                entity.Id.ToString() != selectedId)
                continue;

            string entityId = entity.Id.ToString();

            if (!_rows.TryGetValue(entityId, out var row))
            {
                row = new NpcIntrospectionTextRow { EntityId = entityId };
                _rows[entityId] = row;
                doRefresh = true;
            }

            if (doRefresh)
                row.Refresh(entity, _host);

            // Update screen position every frame.
            if (cam != null)
            {
                var pos     = entity.Get<PositionComponent>();
                var world   = new Vector3(pos.X, _worldYOffset, pos.Z);
                var screen  = cam.WorldToScreenPoint(world);

                row.BehindCamera = screen.z < 0f;
                // Convert Unity screen-space (y=0 at bottom) → GUI space (y=0 at top).
                row.ScreenPos    = new Vector2(screen.x, Screen.height - screen.y);
            }

            _visible.Add(row);
        }

        // Remove rows whose entities are no longer tracked.
        _toRemove.Clear();
        foreach (var id in _rows.Keys)
        {
            bool found = false;
            foreach (var r in _visible)
                if (r.EntityId == id) { found = true; break; }
            if (!found) _toRemove.Add(id);
        }
        foreach (var id in _toRemove)
            _rows.Remove(id);

        // Overlap avoidance in All mode.
        if (_mode == NpcIntrospectionMode.All && _visible.Count > 1)
            SeparateOverlaps();
    }

    private void SeparateOverlaps()
    {
        // Sort by screen-y ascending (smaller y = higher on screen in GUI space).
        _visible.Sort((a, b) => a.ScreenPos.y.CompareTo(b.ScreenPos.y));

        for (int i = 1; i < _visible.Count; i++)
        {
            float minY = _visible[i - 1].ScreenPos.y + _minPixelSpacing;
            if (_visible[i].ScreenPos.y < minY)
                _visible[i].ScreenPos = new Vector2(_visible[i].ScreenPos.x, minY);
        }
    }

    // ── IMGUI rendering ────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (_mode == NpcIntrospectionMode.Off) return;

        foreach (var row in _visible)
        {
            if (!row.BehindCamera)
                row.Draw();
        }
    }

    // ── Test accessors ─────────────────────────────────────────────────────────

    /// <summary>Number of overlay rows currently in the visible set. For play-mode tests.</summary>
    public int ActiveRowCount => _visible.Count;
}
#endif
