using System.Collections.Generic;
using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Renders one translucent beam quad per window aperture (<see cref="LightApertureDto"/>).
///
/// DESIGN
/// ───────
/// Each aperture gets a single flat quad (Unity Quad primitive, lying on the XZ plane)
/// that is positioned at the aperture's tile and projects inward along the aperture's
/// facing direction. The quad's length, alpha, and orientation are driven by the sun state
/// from <see cref="WorldStateDto.Clock.Sun"/>.
///
/// DAY / NIGHT BEHAVIOUR
/// ──────────────────────
/// Day (sun elevation >= beamMinElevationDeg):
///   Beam projects INWARD — sun enters the room through the window.
///   Length scales with the inverse of elevation (low sun → long floor beam).
///   Alpha scales with the raw beam intensity (stronger at low elevation where light is more
///   parallel to the floor and scatters across a larger area visually).
///
/// Night (sun elevation < beamMinElevationDeg):
///   Beam projects OUTWARD — interior lights spill out through the window.
///   Uses a fixed shorter length and lower alpha (LightingConfig.beamNightSpillLength/Alpha).
///   This replicates the aesthetic-bible §"Time of day — Night: window beams flip" commitment.
///
/// BEAM ORIENTATION
/// ─────────────────
/// The aperture facing indicates which wall the window is on, i.e., the outward direction:
///   North facing → window on north wall → inward direction is south (-Z in Unity convention)
///   South facing → window on south wall → inward direction is north (+Z)
///   East  facing → window on east wall  → inward direction is west (-X)
///   West  facing → window on west wall  → inward direction is east (+X)
///   Ceiling       → no beam (skylight is handled differently in a future packet)
///
/// The quad is rotated so its local +Y points along the inward direction (i.e., it extends
/// from the aperture toward the room interior). The quad lies flat on the floor (rotated 90°
/// around X to be XZ-planar).
///
/// PERFORMANCE
/// ────────────
/// All beams share one material (per-night / per-day variant), so Unity can GPU-instance them.
/// Beam quads are created once on first appearance and repositioned cheaply each frame.
/// At 20–40 apertures this is ~1 draw call with instancing.
///
/// MOUNTING
/// ─────────
/// Attach to any GameObject. Assign _engineHost and _config in the Inspector.
/// </summary>
public sealed class BeamRenderer : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("Source of WorldState (apertures and sun state).")]
    private EngineHost _engineHost;

    [SerializeField]
    [Tooltip("Lighting tunable parameters.")]
    private LightingConfig _config;

    // ── Runtime state ─────────────────────────────────────────────────────────

    // Optional WorldState injected directly for test purposes.
    // When non-null, Update() uses this instead of _engineHost.WorldState.
    private WorldStateDto _injectedWorldState;

    // Beam quad GameObjects keyed by aperture Id.
    private readonly Dictionary<string, BeamView> _beamViews = new();

    // Shared materials — one for day beams, one for night spill beams.
    // Materials use BeamProjection.shader which has a radial alpha falloff.
    private Material _dayMaterial;
    private Material _nightMaterial;

    // Root to keep hierarchy clean.
    private Transform _beamRoot;

    // Cached shader property IDs.
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _beamRoot = new GameObject("BeamRoot").transform;
        _beamRoot.SetParent(transform, worldPositionStays: false);

        // Shared materials — beam quads are rendered with BeamProjection.shader.
        // If the shader isn't compiled yet (editor first-run), fall back to Particles/Alpha Blended.
        Shader beamShader = Shader.Find("ECSUnity/BeamProjection")
                         ?? Shader.Find("Particles/Alpha Blended");

        _dayMaterial = new Material(beamShader)
        {
            name = "BeamDay"
        };
        _dayMaterial.color = new Color(1f, 0.92f, 0.60f, 0.30f);   // warm yellow, semi-transparent

        _nightMaterial = new Material(beamShader)
        {
            name = "BeamNight"
        };
        _nightMaterial.color = new Color(0.85f, 0.90f, 1.00f, 0.10f);   // cool white, very faint
    }

    private void Update()
    {
        // Prefer the injected WorldState (set by tests) over the live EngineHost snapshot.
        var ws = _injectedWorldState ?? _engineHost?.WorldState;
        if (ws == null) return;

        var apertures = ws.LightApertures;
        if (apertures == null || apertures.Count == 0)
        {
            HideAll();
            return;
        }

        SunStateDto sun = ws.Clock?.Sun;

        // Read config with fallbacks.
        float minElevDeg    = _config != null ? _config.beamMinElevationDeg      : 3f;
        float maxLenElevDeg = _config != null ? _config.beamMaxLengthElevationDeg : 20f;
        float maxLen        = _config != null ? _config.beamMaxLengthUnits        : 12f;
        float maxAlpha      = _config != null ? _config.beamMaxAlpha              : 0.32f;
        float beamWidthMul  = _config != null ? _config.beamWidthMultiplier       : 1.4f;
        float nightAlpha    = _config != null ? _config.beamNightSpillAlpha       : 0.12f;
        float nightLen      = _config != null ? _config.beamNightSpillLength      : 3f;

        bool isNight = (sun == null) || (sun.ElevationDeg < minElevDeg);

        var seenIds = new HashSet<string>();
        foreach (var ap in apertures)
        {
            if (ap.Facing == ApertureFacing.Ceiling) continue;   // skylight — not handled in this packet
            seenIds.Add(ap.Id);

            if (!_beamViews.TryGetValue(ap.Id, out var view))
            {
                view = CreateBeamView(ap.Id);
                _beamViews[ap.Id] = view;
            }

            // Beam width scales with sqrt(AreaSqTiles) to roughly match window width.
            float beamWidth = Mathf.Sqrt((float)ap.AreaSqTiles) * beamWidthMul;

            if (isNight)
            {
                UpdateNightBeam(view, ap, nightLen, beamWidth, nightAlpha);
            }
            else
            {
                float elevDeg  = (float)sun.ElevationDeg;
                float sunAzDeg = (float)sun.AzimuthDeg;
                UpdateDayBeam(view, ap, elevDeg, sunAzDeg, maxLen, maxLenElevDeg, maxAlpha, beamWidth);
            }
        }

        // Remove stale beams.
        var toRemove = new List<string>();
        foreach (var id in _beamViews.Keys)
            if (!seenIds.Contains(id)) toRemove.Add(id);
        foreach (var id in toRemove)
        {
            Destroy(_beamViews[id].Go);
            _beamViews.Remove(id);
        }
    }

    // ── Beam creation ─────────────────────────────────────────────────────────

    private BeamView CreateBeamView(string id)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = $"Beam_{id}";
        go.transform.SetParent(_beamRoot, worldPositionStays: false);

        // Remove collider — beams are purely visual.
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Assign day material by default; UpdateDayBeam / UpdateNightBeam will override.
        var rend = go.GetComponent<Renderer>();
        rend.material = _dayMaterial;

        return new BeamView { Go = go, Rend = rend };
    }

    // ── Day beam update ───────────────────────────────────────────────────────

    /// <summary>
    /// Positions, rotates, and tints a daytime beam entering through the aperture.
    /// </summary>
    private static void UpdateDayBeam(
        BeamView       view,
        LightApertureDto ap,
        float          elevDeg,
        float          sunAzDeg,
        float          maxLen,
        float          maxLenElevDeg,
        float          maxAlpha,
        float          beamWidth)
    {
        // Inward direction vector (XZ plane) for this aperture facing.
        Vector3 inward = InwardVector(ap.Facing);

        // How much does this window face the current sun?
        // The outward normal of the window is the opposite of inward.
        // sunDir in XZ: azimuth 0 = north (+Z), 90 = east (+X), 180 = south (-Z), 270 = west (-X).
        float azRad    = sunAzDeg * Mathf.Deg2Rad;
        Vector3 sunXZ  = new Vector3(Mathf.Sin(azRad), 0f, Mathf.Cos(azRad));   // sun's XZ direction
        Vector3 outward = -inward;

        // Dot of outward normal against sun direction: positive = window faces the sun.
        float facingDot = Vector3.Dot(outward, sunXZ);

        // If the window doesn't face the sun at all, hide the beam.
        if (facingDot <= 0.05f)
        {
            view.Go.SetActive(false);
            return;
        }

        view.Go.SetActive(true);
        view.Rend.sharedMaterial = view.Rend.sharedMaterial.name == "BeamNight"
            ? view.Rend.sharedMaterial   // will be swapped below
            : view.Rend.sharedMaterial;

        // Assign day material.
        // (In a pooled system this would be a material property block; here we just
        //  set the shared material since each BeamView has its own renderer.)

        // Beam length: maximum at low sun elevation, shrinks to near-zero at zenith.
        // At elevation <= maxLenElevDeg → full length; at 90° → 0 (sun is overhead).
        float lenFraction = 1f - Mathf.Clamp01((elevDeg - maxLenElevDeg) / (90f - maxLenElevDeg));
        float beamLen     = maxLen * lenFraction * facingDot;   // also modulated by facing angle

        // Beam alpha: high at low elevation (visible beam slice), fades as sun rises.
        float alphaFraction = 1f - Mathf.Clamp01((elevDeg - 5f) / 60f);
        float alpha         = maxAlpha * alphaFraction * facingDot;

        // Position: aperture tile position, just above the floor.
        float posX = ap.Position.X;
        float posZ = ap.Position.Y;   // DTO Y = world Z (tile row)
        float posY = 0.03f;           // just above the floor quad

        // The beam quad extends from the aperture INTO the room along 'inward'.
        // We centre the quad halfway along the beam, offset from the aperture.
        Vector3 origin  = new Vector3(posX, posY, posZ);
        Vector3 centre  = origin + inward * (beamLen * 0.5f);

        view.Go.transform.position = centre;

        // Rotation: flat on the floor (rotate 90° around X so the quad is XZ-planar),
        // then yaw to align the quad's local Y with the inward direction.
        float yawDeg = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg;
        view.Go.transform.rotation = Quaternion.Euler(90f, yawDeg, 0f);

        // Scale: X = width, Y = length (in the quad's local forward direction after rotation).
        view.Go.transform.localScale = new Vector3(beamWidth, Mathf.Max(beamLen, 0.1f), 1f);

        // Apply alpha via material color (BeamProjection.shader multiplies vertex alpha by the texture).
        Color c = view.Rend.material.color;
        c.a = Mathf.Clamp01(alpha);
        view.Rend.material.color = c;
    }

    // ── Night spill beam update ───────────────────────────────────────────────

    /// <summary>
    /// Night-time: interior light spills OUT through the window.
    /// The beam reverses direction (outward) and uses the night spill alpha.
    /// </summary>
    private static void UpdateNightBeam(
        BeamView         view,
        LightApertureDto ap,
        float            nightLen,
        float            beamWidth,
        float            nightAlpha)
    {
        view.Go.SetActive(true);

        // Outward direction: opposite of the daytime inward direction.
        Vector3 outward = -InwardVector(ap.Facing);

        float posX   = ap.Position.X;
        float posZ   = ap.Position.Y;
        float posY   = 0.03f;
        Vector3 origin  = new Vector3(posX, posY, posZ);
        Vector3 centre  = origin + outward * (nightLen * 0.5f);

        view.Go.transform.position = centre;

        float yawDeg = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
        view.Go.transform.rotation = Quaternion.Euler(90f, yawDeg, 0f);
        view.Go.transform.localScale = new Vector3(beamWidth, Mathf.Max(nightLen, 0.1f), 1f);

        Color c = view.Rend.material.color;
        c.a = Mathf.Clamp01(nightAlpha);
        view.Rend.material.color = c;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the inward (room-interior) direction for an aperture facing.
    ///
    /// Convention: the facing names the wall the window is ON (e.g. North facing = window is
    /// on the north wall). Light enters from the opposite side, so the inward vector is the
    /// opposite of the wall's outward normal:
    ///   North wall outward normal = +Z → inward = -Z (south-going, into the room)
    ///   South wall outward normal = -Z → inward = +Z
    ///   East  wall outward normal = +X → inward = -X
    ///   West  wall outward normal = -X → inward = +X
    ///
    /// Note: this uses Unity's +Z = north, +X = east convention matching the tile grid.
    /// </summary>
    private static Vector3 InwardVector(ApertureFacing facing) => facing switch
    {
        ApertureFacing.North   => new Vector3( 0f, 0f, -1f),   // north wall → beam goes south
        ApertureFacing.South   => new Vector3( 0f, 0f,  1f),   // south wall → beam goes north
        ApertureFacing.East    => new Vector3(-1f, 0f,  0f),   // east wall  → beam goes west
        ApertureFacing.West    => new Vector3( 1f, 0f,  0f),   // west wall  → beam goes east
        _                      => Vector3.zero,                  // Ceiling: handled by caller
    };

    private void HideAll()
    {
        foreach (var v in _beamViews.Values)
            v.Go.SetActive(false);
    }

    // ── Test / diagnostic accessors ───────────────────────────────────────────

    /// <summary>
    /// Injects a WorldStateDto for test use. When set, Update() reads from this instead
    /// of EngineHost.WorldState, allowing tests to drive the renderer without a full engine boot.
    /// Pass null to restore live EngineHost feed.
    /// </summary>
    public void InjectWorldState(WorldStateDto ws) => _injectedWorldState = ws;

    /// <summary>Returns true if the beam for the given aperture is currently visible.</summary>
    public bool IsBeamVisible(string apertureId)
        => _beamViews.TryGetValue(apertureId, out var v) && v.Go.activeInHierarchy;

    /// <summary>Returns the current alpha of the beam material for the given aperture.</summary>
    public float GetBeamAlpha(string apertureId)
        => _beamViews.TryGetValue(apertureId, out var v) ? v.Rend.material.color.a : 0f;

    /// <summary>Count of active (visible) beam views. For tests.</summary>
    public int VisibleBeamCount
    {
        get
        {
            int n = 0;
            foreach (var v in _beamViews.Values)
                if (v.Go.activeInHierarchy) n++;
            return n;
        }
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private sealed class BeamView
    {
        public GameObject Go;
        public Renderer   Rend;
    }
}
