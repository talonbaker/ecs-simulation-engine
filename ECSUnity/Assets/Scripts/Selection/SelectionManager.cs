using System;
using UnityEngine;

/// <summary>
/// Singleton-ish MonoBehaviour that owns the "currently selected" reference
/// and drives the click → raycast → <see cref="Selectable.Select"/> flow.
///
/// Uses the legacy <see cref="Input"/> API (no Input System dependency).
/// Esc clears the current selection.
/// </summary>
public sealed class SelectionManager : MonoBehaviour
{
    [Tooltip("Camera to cast selection rays from. Auto-grabs Camera.main if null.")]
    [SerializeField] private Camera _camera;

    [Tooltip("Layers Physics.Raycast considers for selection.")]
    [SerializeField] private LayerMask _selectableLayerMask = ~0;

    [Tooltip("Mouse button index: 0 = left, 1 = right, 2 = middle.")]
    [Range(0, 2)]
    [SerializeField] private int _clickButton = 0;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fires whenever the selection changes (including a clear). Arg: new selection or null.</summary>
    public event Action<Selectable> OnSelectionChanged;

    /// <summary>Currently selected object, or null if nothing is selected.</summary>
    public Selectable CurrentSelection { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Deselect();
            return;
        }
        SelectByRaycast();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires a raycast from the selection camera on each Update.
    /// Hits a <see cref="Selectable"/> → selects it.
    /// Hits nothing (or a non-selectable) → deselects current.
    /// </summary>
    public void SelectByRaycast()
    {
        if (!Input.GetMouseButtonDown(_clickButton)) return;

        Camera cam = _camera != null ? _camera : Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, _selectableLayerMask))
        {
            var sel = hit.collider.GetComponentInParent<Selectable>();
            if (sel != null)
            {
                SetSelection(sel);
                return;
            }
        }

        Deselect();
    }

    /// <summary>Clears the current selection (e.g. on Esc or external request).</summary>
    public void Deselect()
    {
        if (CurrentSelection == null) return;
        CurrentSelection.Deselect();
        CurrentSelection = null;
        OnSelectionChanged?.Invoke(null);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void SetSelection(Selectable sel)
    {
        if (CurrentSelection == sel) return;
        if (CurrentSelection != null)
            CurrentSelection.Deselect();
        CurrentSelection = sel;
        sel.Select();
        OnSelectionChanged?.Invoke(sel);
    }
}
