using System.Collections.Generic;
using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Reads <see cref="WorldStateDto.Entities"/> from <see cref="EngineHost"/> each frame
/// and renders each NPC as a small billboard quad (colored dot).
///
/// DESIGN
/// ───────
/// • One quad per NPC — created on demand, pooled across frames.
/// • Quads are billboarded: always face the camera so they appear as circles
///   regardless of camera angle.
/// • Size: 0.3 world-units square (just visible at default altitude, not a distraction).
/// • Color: archetype-driven via <see cref="RenderColorPalette.ForNpc"/>.
/// • Z-position: placed at Y = 0.5 (half a world-unit above floor) so dots float
///   at "waist height" above the room quads.
///
/// PERFORMANCE DESIGN (30-NPC-at-60-FPS gate — AT-11)
/// ────────────────────────────────────────────────────
/// • All NPC quads share a single Material — Unity batches them into one draw call
///   per unique color. With 30 NPCs and 3 unique archetype colors, that is 3 draw
///   calls in the worst case (all 3 archetypes live simultaneously).
/// • No per-frame mesh allocation — quads are pooled.
/// • Position update is a Transform.position assignment — no Rigidbody, no physics.
/// • No interpolation at v0.1 — position tracks the latest engine snapshot directly.
///   Visual stair-stepping between engine ticks is expected; smooth interpolation
///   is a future packet.
///
/// BILLBOARDING
/// ─────────────
/// Each LateUpdate() frame the quad's rotation is set to face the camera.
/// This ensures the dot looks like a circle regardless of the camera's pitch/yaw.
///
/// NON-GOALS
/// ──────────
/// • No silhouette rendering (WP-3.1.B).
/// • No animation states (WP-3.1.B).
/// • No selection highlight (WP-3.1.E).
/// </summary>
public sealed class NpcDotRenderer : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("EngineHost that provides WorldState each frame.")]
    private EngineHost _engineHost;

    [SerializeField]
    [Tooltip("World-unit size of each NPC dot quad.")]
    private float _dotSize = 1.0f;

    [SerializeField]
    [Tooltip("World-unit height above the floor where NPC dots float.")]
    private float _dotHeight = 0.5f;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private readonly Dictionary<string, NpcView> _npcViews = new();

    // Shared base material; per-NPC color is set on a cloned instance.
    private Material _baseMaterial;

    // Cache of color → material to minimize Material instances (colors repeat across NPCs).
    private readonly Dictionary<Color, Material> _materialCache = new();

    // Parent to keep hierarchy tidy.
    private Transform _npcRoot;

    // Camera reference for billboarding.
    private Camera _mainCamera;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _baseMaterial = new Material(Shader.Find("Unlit/Color"));

        _npcRoot = new GameObject("NpcDots").transform;
        _npcRoot.SetParent(transform, worldPositionStays: false);
    }

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (_engineHost == null) return;

        var worldState = _engineHost.WorldState;
        if (worldState == null) return;

        SyncNpcs(worldState.Entities);
    }

    private void LateUpdate()
    {
        // Billboard: face each dot toward the camera after all movement has settled.
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        foreach (var view in _npcViews.Values)
        {
            view.Go.transform.LookAt(
                _mainCamera.transform.position,
                _mainCamera.transform.up);
        }
    }

    // ── NPC sync ──────────────────────────────────────────────────────────────

    private void SyncNpcs(List<EntityStateDto> entities)
    {
        var seenIds = new HashSet<string>();

        foreach (var npc in entities)
        {
            // Skip entities with no position data — they cannot be rendered.
            if (!npc.Position.HasPosition) continue;

            seenIds.Add(npc.Id);

            if (!_npcViews.TryGetValue(npc.Id, out var view))
            {
                view = CreateNpcView(npc);
                _npcViews[npc.Id] = view;
            }

            UpdateNpcView(view, npc);
        }

        // Destroy views for NPCs that have left the DTO (e.g. died or were removed).
        var toRemove = new List<string>();
        foreach (var id in _npcViews.Keys)
            if (!seenIds.Contains(id)) toRemove.Add(id);

        foreach (var id in toRemove)
        {
            Destroy(_npcViews[id].Go);
            _npcViews.Remove(id);
        }
    }

    private NpcView CreateNpcView(EntityStateDto npc)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = $"NpcDot_{npc.Name}";
        go.transform.SetParent(_npcRoot, worldPositionStays: false);
        go.transform.localScale = Vector3.one * _dotSize;

        // Remove collider — dots are purely visual.
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Assign archetype color.
        Color dotColor = RenderColorPalette.ForNpc(npc.Name);
        if (!_materialCache.TryGetValue(dotColor, out var mat))
        {
            mat = new Material(_baseMaterial);
            mat.color = dotColor;
            _materialCache[dotColor] = mat;
        }
        go.GetComponent<Renderer>().material = mat;

        return new NpcView { Go = go, LastColor = dotColor };
    }

    private void UpdateNpcView(NpcView view, EntityStateDto npc)
    {
        // Assign world-space position. Engine X → Unity X, Engine Y → Unity Y, Engine Z → Unity Z.
        // NPCs are on the floor plane; we lift them by _dotHeight so they float above room quads.
        // Engine coordinate system: X = horizontal, Y = depth (floor plan).
        // Unity coordinate system: X = horizontal, Z = depth, Y = up.
        view.Go.transform.position = new Vector3(
            npc.Position.X,
            _dotHeight,
            npc.Position.Y);

        // Scale is constant — set once at create, not per frame.
    }

    // ── Test accessors ────────────────────────────────────────────────────────

    /// <summary>Count of active NPC dot views. Exposed for play-mode tests.</summary>
    public int ActiveNpcCount => _npcViews.Count;

    /// <summary>Returns the GameObject for an NPC entity ID string, or null.</summary>
    public GameObject GetNpcGameObject(string entityId)
        => _npcViews.TryGetValue(entityId, out var view) ? view.Go : null;

    // ── Inner types ───────────────────────────────────────────────────────────

    private sealed class NpcView
    {
        public GameObject Go;
        public Color      LastColor;
    }
}
