using System;
using UnityEngine;
using UnityEngine.InputSystem;
using APIFramework.Mutation;

/// <summary>
/// Owns the Build Mode lifecycle and input routing (WP-3.1.D).
///
/// RESPONSIBILITIES
/// ─────────────────
///   - Toggle build mode with the B key (or the on-screen button).
///   - Show / hide the palette panel and tint overlay.
///   - Route input to placement, pickup, lock/unlock, and cancel sub-systems.
///   - Inject IWorldMutationApi from EngineHost once the engine is alive.
///
/// BUILD MODE LIFECYCLE (per frame, when active)
/// ──────────────────────────────────────────────
///   1. Read mouse-world-position; snap to grid.
///   2. If intent is Placing/PickingUp:
///        a. Move ghost to cursor position.
///        b. Run PlacementValidator; update ghost tint.
///        c. If left-click AND valid → commit mutation.
///        d. If Esc / right-click → cancel.
///   3. If intent is None:
///        a. Left-click → TryPickup entity under cursor (must have MutableTopologyTag).
///        b. Right-click → TryShowContextMenu (lock/unlock door).
///
/// DISRUPTION
/// ───────────
/// Disruption costs (NPC stress/irritation when a desk is moved while occupied) are
/// handled by the engine systems — no explicit call needed here. The mutation API
/// emits the structural change event, which triggers the engine's disruption cascade
/// on the next tick.
///
/// WARDEN / RETAIL
/// ────────────────
/// Build Mode is available in both builds. No conditional compilation required here.
/// Creative mode (all-items palette unlock) is gated by PlayerUIConfig.CreativeMode
/// which this class does not set — it reads it when populating the palette.
/// </summary>
public sealed class BuildModeController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("The EngineHost — source of the engine API and entity manager.")]
    private EngineHost _host;

    [SerializeField]
    private BuildPaletteUI _palette;

    [SerializeField]
    private BuildOverlay _overlay;

    [SerializeField]
    private GhostPreview _ghost;

    [SerializeField]
    private PlacementValidator _validator;

    [SerializeField]
    private PickupController _pickup;

    [SerializeField]
    private DoorLockContextMenu _doorLock;

    [SerializeField]
    private BuildModeConfig _config;

    [SerializeField]
    [Tooltip("Optional reference to CameraController — used to temporarily block " +
             "camera pan/rotate while a context menu is open.")]
    private CameraController _camera;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private bool              _isBuildMode;
    private BuildIntent       _currentIntent = BuildIntent.None;
    private IWorldMutationApi _mutationApi;

    // Tracks whether mutation API has been injected (engine might not be alive at Start).
    private bool _mutationApiReady;

    // Mouse position last frame (to detect click vs drag).
    private Vector2 _mouseDownPos;
    private bool    _mouseWasDragged;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        // Wire palette callback.
        if (_palette != null)
            _palette.OnPlacementRequested += OnPalettePlacementRequested;

        // Initially hide build-mode UI.
        SetBuildMode(false);
    }

    private void Update()
    {
        // Inject mutation API once the engine is alive.
        if (!_mutationApiReady) TryInjectMutationApi();

        // Toggle build mode with B key.
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
            ToggleBuildMode();

        if (_isBuildMode)
        {
            HandleBuildModeInput();
        }
    }

    // ── Toggle ────────────────────────────────────────────────────────────────

    /// <summary>Toggle build mode on or off.</summary>
    public void ToggleBuildMode() => SetBuildMode(!_isBuildMode);

    /// <summary>Explicitly enable or disable build mode.</summary>
    public void SetBuildMode(bool active)
    {
        _isBuildMode = active;

        _palette?.SetVisible(active);
        _overlay?.SetTinted(active);

        // Cancel any active intent when leaving build mode.
        if (!active) ClearIntent();

        Debug.Log($"[BuildModeController] Build mode → {(_isBuildMode ? "ON" : "OFF")}");
    }

    /// <summary>True when build mode is currently active.</summary>
    public bool IsBuildMode => _isBuildMode;

    // ── Input handling ────────────────────────────────────────────────────────

    private void HandleBuildModeInput()
    {
        // Escape or right-click cancels current intent.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClearIntent();
            return;
        }

        // Get camera + cursor world position.
        if (Camera.main == null) return;
        Vector2 mouseScreen = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : Vector2.zero;

        float gridSize = _config != null ? _config.snapGridSize : 1f;

        if (!PlacementValidator.ScreenToWorld(mouseScreen, Camera.main, gridSize, out Vector3 worldPos))
            return;

        // If we have an active intent, update ghost and listen for commit/cancel.
        if (_currentIntent.IsActive)
        {
            _ghost?.MoveTo(worldPos);

            // Validate and update ghost tint.
            bool valid = _validator == null || _validator.IsValidPlacement(worldPos, out _);
            _ghost?.SetValid(valid);

            // Rotate with R key.
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                float step = _config != null ? _config.rotationStep : 90f;
                _ghost?.Rotate(step);
            }

            // Left-click to commit.
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (valid) CommitIntent(worldPos);
                // If invalid: flash ghost red (already red), don't commit.
            }

            // Right-click to cancel.
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                ClearIntent();
        }
        else
        {
            // No active intent — handle entity clicks and context menu.
            if (Mouse.current == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // Try pickup on whatever is under the cursor.
                TryPickupAtCursor(mouseScreen);
            }
            else if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                // Right-click → context menu (lock/unlock door).
                _doorLock?.TryShowContextMenu(mouseScreen);
            }
        }
    }

    // ── Palette callback ──────────────────────────────────────────────────────

    private void OnPalettePlacementRequested(PaletteEntry entry)
    {
        // Clear any existing intent and start a new placement.
        ClearIntent();
        _currentIntent = BuildIntent.ForPlacement(entry.TemplateId, entry.Label, entry.Category.ToString());

        // Start ghost at world center as placeholder; it will follow cursor next Update.
        if (Camera.main != null)
        {
            float gridSize = _config != null ? _config.snapGridSize : 1f;
            Vector3 startPos = new Vector3(0f, 0f, 0f);
            if (PlacementValidator.ScreenToWorld(
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                Camera.main, gridSize, out Vector3 ctrPos))
                startPos = ctrPos;

            _ghost?.Activate(startPos);
            _ghost?.SetValid(true);
        }
    }

    // ── Commit / cancel ───────────────────────────────────────────────────────

    private void CommitIntent(Vector3 worldPos)
    {
        if (_currentIntent.Kind == BuildIntentKind.Placing)
        {
            CommitPlacement(worldPos);
        }
        else if (_currentIntent.Kind == BuildIntentKind.PickingUp)
        {
            if (_currentIntent.EntityId.HasValue)
                _pickup?.CommitPickup(_currentIntent.EntityId.Value, worldPos);
        }

        ClearIntent();
    }

    private void CommitPlacement(Vector3 worldPos)
    {
        if (!_mutationApiReady || _mutationApi == null)
        {
            Debug.LogWarning("[BuildModeController] MutationApi not ready; cannot spawn.");
            return;
        }

        int tileX = Mathf.RoundToInt(worldPos.x);
        int tileY = Mathf.RoundToInt(worldPos.z);

        Guid spawned = _mutationApi.SpawnStructural(_currentIntent.TemplateId, tileX, tileY);
        if (spawned == Guid.Empty)
            Debug.LogWarning($"[BuildModeController] SpawnStructural returned empty — placement failed.");
        else
            Debug.Log($"[BuildModeController] Spawned {_currentIntent.Label} at ({tileX},{tileY}), id={spawned}");
    }

    private void ClearIntent()
    {
        _ghost?.Deactivate();
        _doorLock?.DismissMenu();
        _currentIntent = BuildIntent.None;
    }

    private void TryPickupAtCursor(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f)) return;

        var selectable = hit.collider.GetComponentInParent<SelectableTag>();
        if (selectable == null) return;

        if (!Guid.TryParse(selectable.EntityId, out Guid entityId)) return;

        BuildIntent intent = default;
        string reason = "PickupController not assigned.";
        if (_pickup == null || !_pickup.TryPickup(entityId, selectable.DisplayName,
                out intent, out reason))
        {
            Debug.Log($"[BuildModeController] Pickup rejected for {selectable.DisplayName}: {reason}");
            return;
        }

        _currentIntent = intent;

        float gridSize = _config != null ? _config.snapGridSize : 1f;
        if (PlacementValidator.ScreenToWorld(screenPos, cam, gridSize, out Vector3 startPos))
            _ghost?.Activate(startPos);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void TryInjectMutationApi()
    {
        if (_host == null || _host.Engine == null) return;

        // EntityManager has no service locator at v0.1. Construct a fresh
        // StructuralChangeBus here; this matches WP-3.1.D Assumption #2 fallback.
        // When the engine exposes a service locator, retrieve the singleton instead.
        _mutationApi = new WorldMutationApi(
            _host.Engine,
            new APIFramework.Systems.Spatial.StructuralChangeBus());

        _pickup?.SetMutationApi(_mutationApi);
        _doorLock?.SetDependencies(_host, _mutationApi);
        _mutationApiReady = true;
    }

    // ── Test accessors ────────────────────────────────────────────────────────

    /// <summary>Current active intent. BuildIntent.None when idle.</summary>
    public BuildIntent CurrentIntent => _currentIntent;

    /// <summary>Simulate a B-key press in tests.</summary>
    public void TestToggleBuildMode() => ToggleBuildMode();

    /// <summary>Expose mutation API for direct testing.</summary>
    public IWorldMutationApi MutationApi => _mutationApi;

    /// <summary>Inject mutation API directly (for tests, bypassing engine boot).</summary>
    public void InjectMutationApi(IWorldMutationApi api)
    {
        _mutationApi      = api;
        _mutationApiReady = true;
        _pickup?.SetMutationApi(api);
        _doorLock?.SetDependencies(_host, api);
    }

    /// <summary>Simulate a palette item selection (for tests).</summary>
    public void TestSelectPaletteEntry(PaletteEntry entry) => OnPalettePlacementRequested(entry);

    /// <summary>Simulate a placement commit at the given world position (for tests).</summary>
    public void TestCommitAt(Vector3 worldPos) => CommitIntent(worldPos);

    /// <summary>Simulate a cancel (for tests).</summary>
    public void TestCancel() => ClearIntent();
}
