using System.Collections.Generic;
using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Reads <see cref="WorldStateDto.Rooms"/> from <see cref="EngineHost"/> each frame
/// and renders each room as a flat colored rectangle (quad mesh at Y=0) plus four
/// vertical wall quads forming the room perimeter.
///
/// DESIGN
/// -------
/// Floor quads:
/// - One quad per room, created on demand, pooled across frames.
/// - Room geometry maps 1:1 to world-unit tile bounds (x, z). Y = 0 = floor.
/// - Color is room-kind-driven via RenderColorPalette.ForRoom.
/// - Materials use ECSUnity/RoomTint shader exposing _TintColor, _TintIntensity, _Alpha.
///   RoomAmbientTintApplier drives tint; WallFadeController drives wall _Alpha.
/// - Mesh regenerated only when room count changes (rooms are static in v0.1).
///
/// Wall quads (WP-3.1.C):
/// - Four thin vertical quads per room: North, South, East, West.
/// - Each wall has a BoxCollider (for WallFadeController raycasting) and a WallTag.
/// - Walls start fully opaque (_Alpha = 1.0); WallFadeController fades occluding walls.
///
/// EXISTING TEST COMPATIBILITY
/// ----------------------------
/// GetRoomGameObject(roomId) still returns the floor quad, so RoomRectangleRendererTests
/// (which checks localScale.x = width, localScale.y = height on the floor GO) passes unchanged.
///
/// MOUNTING
/// ---------
/// Attach to any GameObject in the scene. Assign _engineHost in the Inspector.
/// SceneBootstrapper will add this automatically.
/// </summary>
public sealed class RoomRectangleRenderer : MonoBehaviour
{
    // ---- Inspector -----------------------------------------------------------

    [SerializeField]
    [Tooltip("EngineHost that provides WorldState each frame.")]
    private EngineHost _engineHost;

    [SerializeField]
    [Tooltip("Y position of floor quads. Slightly above 0 prevents Z-fighting with floor plane.")]
    private float _floorY = 0.01f;

    [SerializeField]
    [Tooltip("Height of wall quads in world units.")]
    private float _wallHeight = 2.5f;

    [SerializeField]
    [Tooltip("Y base offset of wall quads bottom edge above the floor quad.")]
    private float _wallBaseY = 0.02f;

    // ---- Runtime state -------------------------------------------------------

    // One entry per room ID.
    private readonly Dictionary<string, RoomView> _roomViews = new();

    private Material  _floorMatTemplate;
    private Material  _wallMatTemplate;
    private Transform _roomRoot;

    // Cached shader property IDs (hash once, use every frame).
    private static readonly int ColorId         = Shader.PropertyToID("_Color");
    private static readonly int TintColorId     = Shader.PropertyToID("_TintColor");
    private static readonly int TintIntensityId = Shader.PropertyToID("_TintIntensity");
    private static readonly int AlphaId         = Shader.PropertyToID("_Alpha");

    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Prefer ECSUnity/RoomTint (exposes _TintColor + _Alpha).
        // Fall back to Unlit/Color so the renderer still works without the shader compiled.
        Shader roomShader    = Shader.Find("ECSUnity/RoomTint") ?? Shader.Find("Unlit/Color");
        _floorMatTemplate    = new Material(roomShader) { name = "RoomFloor" };
        _wallMatTemplate     = new Material(roomShader) { name = "RoomWall"  };
        _roomRoot            = new GameObject("Rooms").transform;
        _roomRoot.SetParent(transform, worldPositionStays: false);
    }

    private void Update()
    {
        if (_engineHost == null) return;
        var ws = _engineHost.WorldState;
        if (ws?.Rooms == null) return;
        SyncRooms(ws.Rooms);
    }

    // ---- Room sync -----------------------------------------------------------

    private void SyncRooms(IReadOnlyList<RoomDto> rooms)
    {
        var seenIds = new HashSet<string>();

        foreach (var room in rooms)
        {
            // Single floor v0.1: render only First and Basement floors.
            if (room.Floor != BuildingFloor.First && room.Floor != BuildingFloor.Basement)
            {
                if (_roomViews.TryGetValue(room.Id, out var stale))
                {
                    DestroyView(stale);
                    _roomViews.Remove(room.Id);
                }
                continue;
            }

            seenIds.Add(room.Id);

            if (!_roomViews.TryGetValue(room.Id, out var view))
            {
                view = CreateView(room);
                _roomViews[room.Id] = view;
            }

            UpdateView(view, room);
        }

        // Remove views for rooms no longer in the DTO.
        var toRemove = new List<string>();
        foreach (var id in _roomViews.Keys)
            if (!seenIds.Contains(id)) toRemove.Add(id);
        foreach (var id in toRemove)
        {
            DestroyView(_roomViews[id]);
            _roomViews.Remove(id);
        }
    }

    // ---- Creation -----------------------------------------------------------

    private RoomView CreateView(RoomDto room)
    {
        Color baseColor = RenderColorPalette.ForRoom(room.Category);

        // Floor quad -------------------------------------------------------
        var floorGo  = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floorGo.name = $"Room_{room.Name}";
        floorGo.transform.SetParent(_roomRoot, worldPositionStays: false);

        // Floor quad is purely visual -- no collider needed.
        var fc = floorGo.GetComponent<Collider>();
        if (fc != null) Destroy(fc);

        var floorMat = new Material(_floorMatTemplate);
        floorMat.SetColor(ColorId,         baseColor);
        floorMat.SetColor(TintColorId,     Color.white);
        floorMat.SetFloat(TintIntensityId, 0f);
        floorMat.SetFloat(AlphaId,         1f);
        floorGo.GetComponent<Renderer>().material = floorMat;

        // Four wall quads --------------------------------------------------
        var wallRoot = new GameObject($"Walls_{room.Name}").transform;
        wallRoot.SetParent(_roomRoot, worldPositionStays: false);

        var wallMats = new Material[4];
        var wallTags = new WallTag[4];
        var wallGos  = new GameObject[4];

        for (int i = 0; i < 4; i++)
        {
            var wallGo  = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wallGo.name = $"Wall_{room.Name}_{WallName(i)}";
            wallGo.transform.SetParent(wallRoot, worldPositionStays: false);

            var wm = new Material(_wallMatTemplate);
            wm.SetColor(ColorId,         baseColor * 0.85f);  // slightly darker than floor
            wm.SetColor(TintColorId,     Color.white);
            wm.SetFloat(TintIntensityId, 0f);
            wm.SetFloat(AlphaId,         1f);
            wallGo.GetComponent<Renderer>().material = wm;

            // Replace the default MeshCollider with a BoxCollider.
            // BoxColliders are more reliable for raycasting against thin vertical planes.
            var mc = wallGo.GetComponent<Collider>();
            if (mc != null) Destroy(mc);
            wallGo.AddComponent<BoxCollider>();   // size set in UpdateView

            // WallTag: links this wall to WallFadeController without requiring a Unity Layer.
            var tag          = wallGo.AddComponent<WallTag>();
            tag.FaceMaterial = wm;
            tag.RoomId       = room.Id;
            tag.TargetAlpha  = 1f;
            tag.CurrentAlpha = 1f;

            wallMats[i] = wm;
            wallTags[i] = tag;
            wallGos[i]  = wallGo;
        }

        return new RoomView
        {
            FloorGo  = floorGo,
            FloorMat = floorMat,
            WallRoot = wallRoot,
            WallGos  = wallGos,
            WallMats = wallMats,
            WallTags = wallTags,
        };
    }

    // ---- Per-frame update ---------------------------------------------------

    private void UpdateView(RoomView view, RoomDto room)
    {
        var b = room.BoundsRect;

        // Floor quad: rotate 90 around X to lie flat on XZ plane, then scale and position.
        view.FloorGo.transform.rotation   = Quaternion.Euler(90f, 0f, 0f);
        float cx = b.X + b.Width  * 0.5f;
        float cz = b.Y + b.Height * 0.5f;
        view.FloorGo.transform.position   = new Vector3(cx, _floorY, cz);
        view.FloorGo.transform.localScale = new Vector3(b.Width, b.Height, 1f);

        // Walls: [0]=North, [1]=South, [2]=East, [3]=West.
        // Unity Quad default faces +Z. Rotate Y to reorient each wall inward.
        // Vertical centre Y = wallBaseY + wallHeight/2.
        float wallH = _wallHeight;
        float wallY = _wallBaseY + wallH * 0.5f;

        // North wall: top edge Z = b.Y + b.Height, facing south (-Z inward).
        view.WallGos[0].transform.position   = new Vector3(cx, wallY, b.Y + b.Height);
        view.WallGos[0].transform.rotation   = Quaternion.Euler(0f, 180f, 0f);
        view.WallGos[0].transform.localScale = new Vector3(b.Width, wallH, 1f);
        SetBoxCollider(view.WallGos[0], new Vector3(b.Width, wallH, 0.15f));

        // South wall: bottom edge Z = b.Y, facing north (+Z inward).
        view.WallGos[1].transform.position   = new Vector3(cx, wallY, b.Y);
        view.WallGos[1].transform.rotation   = Quaternion.Euler(0f, 0f,   0f);
        view.WallGos[1].transform.localScale = new Vector3(b.Width, wallH, 1f);
        SetBoxCollider(view.WallGos[1], new Vector3(b.Width, wallH, 0.15f));

        // East wall: right edge X = b.X + b.Width, facing west (-X inward).
        view.WallGos[2].transform.position   = new Vector3(b.X + b.Width, wallY, cz);
        view.WallGos[2].transform.rotation   = Quaternion.Euler(0f, 270f, 0f);
        view.WallGos[2].transform.localScale = new Vector3(b.Height, wallH, 1f);
        SetBoxCollider(view.WallGos[2], new Vector3(0.15f, wallH, b.Height));

        // West wall: left edge X = b.X, facing east (+X inward).
        view.WallGos[3].transform.position   = new Vector3(b.X, wallY, cz);
        view.WallGos[3].transform.rotation   = Quaternion.Euler(0f, 90f,  0f);
        view.WallGos[3].transform.localScale = new Vector3(b.Height, wallH, 1f);
        SetBoxCollider(view.WallGos[3], new Vector3(0.15f, wallH, b.Height));
    }

    // ---- Helpers ------------------------------------------------------------

    /// <summary>
    /// Sets the BoxCollider size to match worldSize. Converts world-space to local-space
    /// by dividing by the GO's localScale (which is set to the room's tile dimensions).
    /// </summary>
    private static void SetBoxCollider(GameObject go, Vector3 worldSize)
    {
        var col = go.GetComponent<BoxCollider>();
        if (col == null) return;
        var ls = go.transform.localScale;
        col.size = new Vector3(
            ls.x > 0f ? worldSize.x / ls.x : worldSize.x,
            ls.y > 0f ? worldSize.y / ls.y : worldSize.y,
            ls.z > 0f ? worldSize.z / ls.z : worldSize.z);
    }

    private static void DestroyView(RoomView view)
    {
        Destroy(view.FloorGo);
        if (view.WallRoot != null) Destroy(view.WallRoot.gameObject);
    }

    private static string WallName(int i) =>
        i switch { 0 => "N", 1 => "S", 2 => "E", _ => "W" };

    // ---- Public accessors ---------------------------------------------------

    /// <summary>Count of active room views. Exposed for play-mode tests.</summary>
    public int ActiveRoomCount => _roomViews.Count;

    /// <summary>
    /// Returns the floor quad GameObject for a room ID, or null.
    /// localScale.x = width, localScale.y = height (compatible with RoomRectangleRendererTests).
    /// </summary>
    public GameObject GetRoomGameObject(string roomId)
        => _roomViews.TryGetValue(roomId, out var v) ? v.FloorGo : null;

    /// <summary>
    /// Returns the floor material for a room ID, or null.
    /// Used by RoomAmbientTintApplier to apply illumination tint each frame.
    /// </summary>
    public Material GetRoomMaterial(string roomId)
        => _roomViews.TryGetValue(roomId, out var v) ? v.FloorMat : null;

    /// <summary>
    /// Returns the four wall materials [N, S, E, W], or null if room not found.
    /// </summary>
    public Material[] GetWallMaterials(string roomId)
        => _roomViews.TryGetValue(roomId, out var v) ? v.WallMats : null;

    /// <summary>
    /// Returns the four WallTag components [N, S, E, W], or null if room not found.
    /// Used by tests and WallFadeController to inspect occlusion state.
    /// </summary>
    public WallTag[] GetWallTags(string roomId)
        => _roomViews.TryGetValue(roomId, out var v) ? v.WallTags : null;

    // ---- Inner types --------------------------------------------------------

    private sealed class RoomView
    {
        public GameObject   FloorGo;
        public Material     FloorMat;
        public Transform    WallRoot;
        public GameObject[] WallGos;   // index: [N=0, S=1, E=2, W=3]
        public Material[]   WallMats;
        public WallTag[]    WallTags;
    }
}
