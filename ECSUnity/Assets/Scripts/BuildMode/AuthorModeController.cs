#if WARDEN
using System;
using System.Linq;
using APIFramework.Build;
using APIFramework.Components;
using APIFramework.Mutation;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WP-4.0.J — Author mode foundation. WARDEN-only MonoBehaviour that toggles
/// "author mode" (Ctrl+Shift+A by default) and exposes the WP-4.0.J author-mode
/// extensions to <see cref="IWorldMutationApi"/> as Unity-side methods.
///
/// SCOPE (this packet, this script):
///   - The toggle + state flag.
///   - The mutation surface: DrawRoom, PlaceLightSource, TuneLightSource, PlaceAperture, Erase.
///   - Save / Load / Reload via WorldDefinitionWriter (WP-4.0.I).
///   - Palette catalog access (loaded from docs/c2-content/build/author-mode-palette.json).
///
/// OUT OF SCOPE (deferred to Editor follow-up):
///   - Palette UI panel + per-category tabs (use BuildPaletteUI as the model).
///   - Click-and-drag room rectangle tool with ghost preview.
///   - Inline tuner panels for light state/intensity/temperature.
///   - Save / Load / Reload toolbar widget.
///   - Sandbox scene (Assets/_Sandbox/author-mode.unity).
///   - Author-mode banner UI.
///
/// To use this controller from Unity Editor work:
///   1. Add AuthorModeController to a GameObject in the WARDEN-build scene.
///   2. Wire EngineHost reference in the inspector.
///   3. Wire up custom UI buttons (in the Editor) that call DrawRoom / PlaceLightSource / etc.
///   4. The toggle (Ctrl+Shift+A) is automatic; check IsActive to gate UI visibility.
///
/// In a non-WARDEN build, the entire script is compiled out (#if WARDEN guard).
/// </summary>
public sealed class AuthorModeController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("EngineHost — source of the engine API and entity manager.")]
    private EngineHost _host;

    [SerializeField]
    [Tooltip("If true, author mode is active when the scene starts. " +
             "Useful for sandbox/dev scenes that should boot directly into authoring.")]
    private bool _activeOnStart = false;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private IWorldMutationApi    _api;
    private AuthorModePaletteData _palette;

    /// <summary>True while the player is in author mode.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Fires when author mode toggles on or off.</summary>
    public event Action<bool> OnAuthorModeChanged;

    /// <summary>The loaded author-mode palette (rooms / light sources / apertures).</summary>
    public AuthorModePaletteData Palette => _palette;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _palette = AuthorModePaletteLoader.LoadDefault();
        if (_palette is null)
            Debug.LogWarning("AuthorModeController: author-mode-palette.json not found; palette features disabled.");

        if (_activeOnStart) SetActive(true);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb is null) return;

        // Ctrl+Shift+A toggles author mode.
        bool ctrl  = kb.leftCtrlKey.isPressed  || kb.rightCtrlKey.isPressed;
        bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        if (ctrl && shift && kb.aKey.wasPressedThisFrame)
        {
            SetActive(!IsActive);
        }
    }

    private void SetActive(bool value)
    {
        if (IsActive == value) return;
        IsActive = value;
        OnAuthorModeChanged?.Invoke(IsActive);
        Debug.Log($"[AuthorMode] {(IsActive ? "ACTIVATED" : "deactivated")}");
    }

    // ── Mutation surface (callers: Editor-wired UI buttons / hotkeys) ─────────

    /// <summary>Draws a new room. Returns the new room entity's ID.</summary>
    public Guid DrawRoom(RoomCategory category, BuildingFloor floor, BoundsRect bounds, string name = null)
    {
        EnsureActive();
        return Api.CreateRoom(category, floor, bounds, name);
    }

    /// <summary>Places a light source inside a room. Returns the new light entity's ID.</summary>
    public Guid PlaceLightSource(string roomId, int tileX, int tileY,
                                 LightKind kind, LightState state, int intensity, int colorTempK)
    {
        EnsureActive();
        return Api.CreateLightSource(roomId, tileX, tileY, kind, state, intensity, colorTempK);
    }

    /// <summary>Mutates an existing light source's tunable properties.</summary>
    public void TuneLightSource(Guid lightId, LightState state, int intensity, int colorTempK)
    {
        EnsureActive();
        Api.TuneLightSource(lightId, state, intensity, colorTempK);
    }

    /// <summary>Places a window/skylight on a room boundary. Returns the new aperture entity's ID.</summary>
    public Guid PlaceAperture(string roomId, int tileX, int tileY, ApertureFacing facing, double areaSqTiles)
    {
        EnsureActive();
        return Api.CreateLightAperture(roomId, tileX, tileY, facing, areaSqTiles);
    }

    /// <summary>Erases a room (with the chosen content policy).</summary>
    public void EraseRoom(Guid roomId, RoomDespawnPolicy policy)
    {
        EnsureActive();
        Api.DespawnRoom(roomId, policy);
    }

    /// <summary>Erases a light source or aperture.</summary>
    public void EraseLight(Guid lightId)
    {
        EnsureActive();
        Api.DespawnLight(lightId);
    }

    // ── NPC authoring (WP-4.0.K) ──────────────────────────────────────────────

    /// <summary>
    /// Spawns an NPC of the given archetype at the tile in the named room.
    /// If <paramref name="name"/> is null, the auto-name pool generates one via WP-4.0.M.
    /// Returns the new NPC entity's ID.
    /// </summary>
    public Guid SpawnNpc(string roomId, int tileX, int tileY, string archetypeId, string name = null)
    {
        EnsureActive();
        if (string.IsNullOrWhiteSpace(name))
            name = ResolveAutoName();
        return Api.CreateNpc(roomId, tileX, tileY, archetypeId, name);
    }

    /// <summary>Despawns an authored NPC.</summary>
    public void EraseNpc(Guid npcId)
    {
        EnsureActive();
        Api.DespawnNpc(npcId);
    }

    /// <summary>Renames an NPC.</summary>
    public void RenameNpc(Guid npcId, string newName)
    {
        EnsureActive();
        Api.RenameNpc(npcId, newName);
    }

    /// <summary>
    /// Returns the next auto-generated name from the cast-name pool (WP-4.0.M).
    /// Lazy-loads the generator on first call.
    /// </summary>
    public string ResolveAutoName()
    {
        if (_namePool is null)
        {
            var data = APIFramework.Cast.CastNameDataLoader.LoadDefault();
            if (data is null)
                throw new InvalidOperationException(
                    "ResolveAutoName: cast name-data.json not discoverable; use SpawnNpc with an explicit name.");
            var gen = new APIFramework.Cast.CastNameGenerator(data);
            _namePool = new APIFramework.Bootstrap.CastNamePool(_host.Engine, gen);
        }
        var result = _namePool.GenerateUniqueName();
        return result.DisplayName;
    }
    private APIFramework.Bootstrap.CastNamePool _namePool;

    // ── Save / Load / Reload (uses WP-4.0.I writer + existing loader) ─────────

    /// <summary>
    /// Serializes the current world to JSON via <see cref="APIFramework.Bootstrap.WorldDefinitionWriter"/>
    /// and writes it to <c>docs/c2-content/world-definitions/&lt;name&gt;.json</c>.
    /// </summary>
    public string SaveWorld(string sceneName, string worldName, int seed = 0)
    {
        EnsureActive();
        if (string.IsNullOrWhiteSpace(sceneName))
            throw new ArgumentException("Scene name required.", nameof(sceneName));
        // Defense-in-depth: reject path traversal / absolute paths.
        if (sceneName.Contains("..") || System.IO.Path.IsPathRooted(sceneName))
            throw new ArgumentException("Scene name must be a simple file name.", nameof(sceneName));

        var path = LocateWorldDefinitionsDir();
        if (path is null)
            throw new InvalidOperationException("Could not locate docs/c2-content/world-definitions/.");

        var fullPath = System.IO.Path.Combine(path, $"{sceneName}.json");
        APIFramework.Bootstrap.WorldDefinitionWriter.WriteToFile(
            _host.Engine, fullPath, sceneName, worldName, seed);
        Debug.Log($"[AuthorMode] Saved scene to {fullPath}");
        return fullPath;
    }

    private static string LocateWorldDefinitionsDir()
    {
        // Walk up from the working directory looking for docs/c2-content/world-definitions/.
        var dir = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
        for (int i = 0; i < 8; i++)
        {
            if (dir is null) break;
            var candidate = System.IO.Path.Combine(
                dir.FullName, "docs", "c2-content", "world-definitions");
            if (System.IO.Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private IWorldMutationApi Api
    {
        get
        {
            if (_api is null)
            {
                if (_host is null)
                    throw new InvalidOperationException(
                        "AuthorModeController: EngineHost reference is missing in the inspector.");
                if (_host.Engine is null)
                    throw new InvalidOperationException(
                        "AuthorModeController: EngineHost.Engine is null — engine not initialised yet.");

                // Construct our own WorldMutationApi instance, matching the BuildModeController
                // pattern. We use the 5-arg constructor so CreateNpc (WP-4.0.K) is available;
                // the cast deps are loaded from disk on first access (independent copy of the
                // archetype catalog is cheap — same JSON data, no shared state with boot).
                var bus      = new APIFramework.Systems.Spatial.StructuralChangeBus();
                var catalog  = APIFramework.Bootstrap.ArchetypeCatalog.LoadDefault();
                var castCfg  = new APIFramework.Config.CastGeneratorConfig();
                var castRng  = new APIFramework.Core.SeededRandom(unchecked((int)DateTime.UtcNow.Ticks));

                _api = catalog is not null
                    ? new WorldMutationApi(_host.Engine, bus, catalog, castCfg, castRng)
                    : new WorldMutationApi(_host.Engine, bus);

                if (catalog is null)
                    Debug.LogWarning(
                        "AuthorModeController: archetype catalog not discoverable; SpawnNpc will throw.");
            }
            return _api;
        }
    }

    private void EnsureActive()
    {
        if (!IsActive)
            throw new InvalidOperationException(
                "Author mode is not active. Press Ctrl+Shift+A to toggle on.");
    }
}
#endif
