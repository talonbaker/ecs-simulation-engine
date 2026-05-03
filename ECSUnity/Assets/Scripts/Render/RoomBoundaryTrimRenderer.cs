using System.Collections.Generic;
using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Emits thin trim quads along floor-type boundaries between adjacent rooms.
///
/// DESIGN
/// ───────
/// On first Update after room data is available, queries all rooms and detects shared
/// edges where neighbouring rooms have different floor materials (as determined by
/// <see cref="RoomVisualIdentityLoader"/>).  A 1-pixel-tall dark quad is placed at
/// each such seam to visually anchor "where the carpet ends and the hallway begins."
///
/// Trim quads are created once and not regenerated (rooms are static in v0.2).
/// Same-material boundaries generate no trim (CubicleArea next to CubicleArea: no seam).
///
/// MOUNTING
/// ─────────
/// Add to the same GameObject as RoomRectangleRenderer.
/// Assign _engineHost and _visualIdentityLoader in Inspector.
/// </summary>
public sealed class RoomBoundaryTrimRenderer : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────

    [SerializeField] private EngineHost               _engineHost;
    [SerializeField] private RoomVisualIdentityLoader _visualIdentityLoader;

    [SerializeField]
    [Tooltip("Height of trim quads above the floor plane.")]
    private float _trimY = 0.015f;

    [SerializeField]
    [Tooltip("Thickness of trim quads in world units.")]
    private float _trimThickness = 0.08f;

    [SerializeField]
    [Tooltip("How much to darken the trim color relative to the floor color.")]
    private float _trimDarken = 0.18f;

    // ── Runtime state ──────────────────────────────────────────────────────────

    private bool      _built;
    private Transform _trimRoot;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        _trimRoot = new GameObject("BoundaryTrim").transform;
        _trimRoot.SetParent(transform, worldPositionStays: false);
    }

    private void Update()
    {
        if (_built) return;
        if (_engineHost == null) return;

        var ws = _engineHost.WorldState;
        if (ws?.Rooms == null || ws.Rooms.Count == 0) return;

        BuildTrimQuads(ws.Rooms);
        _built = true;
    }

    // ── Build ──────────────────────────────────────────────────────────────────

    private void BuildTrimQuads(IReadOnlyList<RoomDto> rooms)
    {
        // For each pair (i, j) check if they share a horizontal or vertical edge
        // and have different floor material names.
        for (int i = 0; i < rooms.Count; i++)
        {
            for (int j = i + 1; j < rooms.Count; j++)
            {
                var a = rooms[i];
                var b = rooms[j];

                // Skip non-rendered floors.
                if (a.Floor != BuildingFloor.First && a.Floor != BuildingFloor.Basement) continue;
                if (b.Floor != BuildingFloor.First && b.Floor != BuildingFloor.Basement) continue;
                if (a.Floor != b.Floor) continue;

                TryEmitTrim(a, b);
            }
        }
    }

    private void TryEmitTrim(RoomDto a, RoomDto b)
    {
        // Determine the floor material name for each room.
        string matNameA = FloorMaterialName(a.Category);
        string matNameB = FloorMaterialName(b.Category);

        // Same floor type: no trim.
        if (string.Equals(matNameA, matNameB, System.StringComparison.Ordinal)) return;

        var ra = a.BoundsRect;
        var rb = b.BoundsRect;

        // Check for a shared vertical edge (same X boundary).
        // a.right == b.left or b.right == a.left, and Y ranges overlap.
        if (TryGetVerticalSeam(ra, rb, out float seamX, out float overlapMinZ, out float overlapMaxZ))
        {
            float length = overlapMaxZ - overlapMinZ;
            if (length <= 0f) return;
            EmitTrimQuad(
                pos:       new Vector3(seamX, _trimY, overlapMinZ + length * 0.5f),
                scaleX:    _trimThickness,
                scaleZ:    length,
                rotation:  Quaternion.Euler(90f, 0f, 0f),
                baseColor: TrimColor(a, b));
            return;
        }

        // Check for a shared horizontal edge (same Z boundary).
        if (TryGetHorizontalSeam(ra, rb, out float seamZ, out float overlapMinX, out float overlapMaxX))
        {
            float length = overlapMaxX - overlapMinX;
            if (length <= 0f) return;
            EmitTrimQuad(
                pos:       new Vector3(overlapMinX + length * 0.5f, _trimY, seamZ),
                scaleX:    length,
                scaleZ:    _trimThickness,
                rotation:  Quaternion.Euler(90f, 0f, 0f),
                baseColor: TrimColor(a, b));
        }
    }

    private static bool TryGetVerticalSeam(
        BoundsRectDto a, BoundsRectDto b,
        out float seam, out float minZ, out float maxZ)
    {
        seam = 0f; minZ = 0f; maxZ = 0f;

        float aRight  = a.X + a.Width;
        float bRight  = b.X + b.Width;

        bool shares = Mathf.Approximately(aRight, b.X) || Mathf.Approximately(bRight, a.X);
        if (!shares) return false;

        seam = Mathf.Approximately(aRight, b.X) ? aRight : bRight;
        minZ = Mathf.Max(a.Y, b.Y);
        maxZ = Mathf.Min(a.Y + a.Height, b.Y + b.Height);
        return maxZ > minZ;
    }

    private static bool TryGetHorizontalSeam(
        BoundsRectDto a, BoundsRectDto b,
        out float seam, out float minX, out float maxX)
    {
        seam = 0f; minX = 0f; maxX = 0f;

        float aTop = a.Y + a.Height;
        float bTop = b.Y + b.Height;

        bool shares = Mathf.Approximately(aTop, b.Y) || Mathf.Approximately(bTop, a.Y);
        if (!shares) return false;

        seam = Mathf.Approximately(aTop, b.Y) ? aTop : bTop;
        minX = Mathf.Max(a.X, b.X);
        maxX = Mathf.Min(a.X + a.Width, b.X + b.Width);
        return maxX > minX;
    }

    private void EmitTrimQuad(Vector3 pos, float scaleX, float scaleZ, Quaternion rotation, Color baseColor)
    {
        var go       = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name      = "Trim";
        go.transform.SetParent(_trimRoot, worldPositionStays: false);

        // Remove collider — trim is purely visual.
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        go.transform.position   = pos;
        go.transform.rotation   = rotation;
        go.transform.localScale = new Vector3(scaleX, scaleZ, 1f);

        Shader shader  = Shader.Find("ECSUnity/RoomTint") ?? Shader.Find("Unlit/Color");
        var    mat     = new Material(shader);
        Color  trimCol = new Color(
            Mathf.Max(0f, baseColor.r - _trimDarken),
            Mathf.Max(0f, baseColor.g - _trimDarken),
            Mathf.Max(0f, baseColor.b - _trimDarken),
            1f);
        mat.SetColor(ColorId, trimCol);
        mat.SetFloat(AlphaId, 1f);
        go.GetComponent<Renderer>().material = mat;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    // room.Category is Warden.Contracts.Telemetry.RoomCategory; loader and palette expect
    // APIFramework.Components.RoomCategory — values are identical by design so cast is safe.
    private string FloorMaterialName(RoomCategory wardenCat)
    {
        var cat = (APIFramework.Components.RoomCategory)(int)wardenCat;
        if (_visualIdentityLoader == null) return cat.ToString();
        var mat = _visualIdentityLoader.GetFloorMaterial(cat);
        return mat != null ? mat.name : cat.ToString();
    }

    private Color TrimColor(RoomDto a, RoomDto b)
    {
        Color ca = FloorColor(a.Category);
        Color cb = FloorColor(b.Category);
        float lumA = 0.299f * ca.r + 0.587f * ca.g + 0.114f * ca.b;
        float lumB = 0.299f * cb.r + 0.587f * cb.g + 0.114f * cb.b;
        return lumA < lumB ? ca : cb;
    }

    private Color FloorColor(RoomCategory wardenCat)
    {
        var apiCat = (APIFramework.Components.RoomCategory)(int)wardenCat;
        if (_visualIdentityLoader == null) return RenderColorPalette.ForRoom(wardenCat);
        var mat = _visualIdentityLoader.GetFloorMaterial(apiCat);
        if (mat == null) return RenderColorPalette.ForRoom(wardenCat);
        return mat.HasProperty(ColorId) ? mat.GetColor(ColorId) : RenderColorPalette.ForRoom(wardenCat);
    }
}
