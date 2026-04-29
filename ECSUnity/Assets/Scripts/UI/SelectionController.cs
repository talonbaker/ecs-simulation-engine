using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the current selection state in the player UI (WP-3.1.E AT-01 / AT-02).
///
/// SELECTION RULES
/// ────────────────
///   - Single left-click on a <see cref="SelectableTag"/> collider → select; inspector slides in.
///   - Double-click (within <see cref="DoubleClickInterval"/> seconds) → select AND request camera glide.
///   - Click empty space → clear selection.
///   - Build mode intercepts clicks before this controller; <see cref="BlockInput"/> disables processing.
///
/// EVENTS
/// ───────
///   <see cref="SelectionChanged"/> — fires whenever selection changes (including clear).
///   <see cref="GlideRequested"/>   — fires on double-click to request camera smooth-pan.
///
/// ARCHITECTURE
/// ─────────────
/// Reads entity data via <see cref="EngineHost.WorldState"/> (positions, drives) and
/// <see cref="EngineHost.Engine"/> (direct component read for fields not in WorldStateDto).
/// Pure input / selection state management — does not mutate anything.
/// </summary>
public sealed class SelectionController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private EngineHost    _host;
    [SerializeField] private PlayerUIConfig _uiConfig;

    [Tooltip("Camera to raycast from.")]
    [SerializeField] private Camera _camera;

    [Tooltip("Layer mask for selectable colliders.")]
    [SerializeField] private LayerMask _selectableLayer = ~0;

    [Tooltip("Seconds between two clicks that still count as a double-click.")]
    [SerializeField] private float DoubleClickInterval = 0.35f;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fires when the selected entity changes (including deselect). Arg: new selection, or null.</summary>
    public event Action<SelectableTag> SelectionChanged;

    /// <summary>Fires on double-click. Arg: the selected entity's world position.</summary>
    public event Action<Vector3> GlideRequested;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private SelectableTag _current;
    private float         _lastClickTime = -1f;
    private bool          _blockInput;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_blockInput) return;
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        Camera cam = _camera != null ? _camera : Camera.main;
        if (cam == null) return;

        Vector2 screen = Mouse.current.position.ReadValue();
        Ray ray        = cam.ScreenPointToRay(screen);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, _selectableLayer))
        {
            var tag = hit.collider.GetComponentInParent<SelectableTag>();
            if (tag != null)
            {
                // Double-click detection.
                bool isDouble = (Time.unscaledTime - _lastClickTime) < DoubleClickInterval;
                _lastClickTime = Time.unscaledTime;

                SetSelection(tag);

                if (isDouble)
                {
                    GlideRequested?.Invoke(hit.collider.transform.position);
                }
                return;
            }
        }

        // Missed all selectables → deselect.
        ClearSelection();
    }

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>Currently selected entity, or null.</summary>
    public SelectableTag Current => _current;

    /// <summary>Whether anything is selected.</summary>
    public bool HasSelection => _current != null;

    /// <summary>
    /// Programmatically set the selection (used by EventLog click-through, dev console, etc.).
    /// </summary>
    public void SetSelection(SelectableTag tag)
    {
        _current = tag;
        SelectionChanged?.Invoke(_current);
    }

    /// <summary>Clear the selection.</summary>
    public void ClearSelection()
    {
        if (_current == null) return;
        _current = null;
        SelectionChanged?.Invoke(null);
    }

    /// <summary>
    /// When true, input is not processed (e.g. build mode is active or a modal is open).
    /// </summary>
    public void SetBlockInput(bool blocked) => _blockInput = blocked;
}
