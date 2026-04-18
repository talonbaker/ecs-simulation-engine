using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using APIFramework.Core;

/// <summary>
/// Renders the GI tract as a horizontal strip of coloured cubes parented to
/// an entity's cube.  The strip sits to the entity's right (+X in local space)
/// at a comfortable reading distance.
///
/// ORGAN LAYOUT (local coordinates relative to entity cube centre)
/// ───────────────────────────────────────────────────────────────
///  Esophagus   (1.5,  0.5,  0)  — tall thin grey tube
///  Stomach     (2.4,  0.0,  0)  — wide yellow blob
///  SmallIntest (3.4,  0.0,  0)  — medium tan cube
///  LargeIntest (4.3,  0.0,  0)  — medium brown cube
///  Colon       (5.1,  0.2,  0)  — small dark-brown cube  (sits slightly higher)
///  Bladder     (5.1, -0.5,  0)  — small blue cube        (sits below colon)
///
/// FILL VISUALISATION
/// ──────────────────
///  Each organ's Y scale interpolates from 20 % to 140 % of its base scale
///  as fill goes 0 → 1.  Colour blends from neutral grey (empty) toward the
///  organ's natural colour (full).  Critical state triggers a red ping-pong pulse.
///
/// BOLUS TRANSIT
/// ─────────────
///  A small orange cube appears at the top of the esophagus and slides down to
///  the bottom as TransitItemSnapshot.Progress goes 0 → 1.
///  Multiple simultaneous transits are each tracked by their snapshot ID.
/// </summary>
public class OrganCluster : MonoBehaviour
{
    // ── Organ descriptors ─────────────────────────────────────────────────────

    private struct OrganDef
    {
        public string   Name;
        public Vector3  LocalPos;   // relative to entity cube centre
        public Vector3  BaseScale;
        public Color    NormalColor;
    }

    private static readonly OrganDef[] OrganDefs =
    {
        new() { Name = "Esophagus",     LocalPos = new(1.5f,  0.5f,  0f), BaseScale = new(0.18f, 1.00f, 0.18f), NormalColor = EcsColors.Esophagus      },
        new() { Name = "Stomach",       LocalPos = new(2.4f,  0.0f,  0f), BaseScale = new(0.80f, 0.60f, 0.50f), NormalColor = EcsColors.Stomach        },
        new() { Name = "SmallIntest",   LocalPos = new(3.4f,  0.0f,  0f), BaseScale = new(0.60f, 0.50f, 0.40f), NormalColor = EcsColors.SmallIntestine },
        new() { Name = "LargeIntest",   LocalPos = new(4.3f,  0.0f,  0f), BaseScale = new(0.65f, 0.50f, 0.45f), NormalColor = EcsColors.LargeIntestine },
        new() { Name = "Colon",         LocalPos = new(5.1f,  0.2f,  0f), BaseScale = new(0.45f, 0.45f, 0.35f), NormalColor = EcsColors.Colon          },
        new() { Name = "Bladder",       LocalPos = new(5.1f, -0.5f,  0f), BaseScale = new(0.45f, 0.40f, 0.35f), NormalColor = EcsColors.Bladder        },
    };

    // Organ index constants for clarity
    private const int IdxEso     = 0;
    private const int IdxStomach = 1;
    private const int IdxSI      = 2;
    private const int IdxLI      = 3;
    private const int IdxColon   = 4;
    private const int IdxBladder = 5;

    // Esophagus bolus transit geometry
    private static readonly float EsoTopY    = 0.5f + 0.5f;   // localPos.Y + halfHeight
    private static readonly float EsoBottomY = 0.5f - 0.5f;   // localPos.Y - halfHeight
    private const            float EsoX      = 1.5f;

    // ── Runtime state ─────────────────────────────────────────────────────────

    // One Transform per organ (index matches OrganDefs)
    private Transform[] _organs;
    private Material[]  _mats;

    // Bolus cubes keyed by transit snapshot ID
    private readonly Dictionary<Guid, Transform> _bolusCubes = new();

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        _organs = new Transform[OrganDefs.Length];
        _mats   = new Material[OrganDefs.Length];

        for (int i = 0; i < OrganDefs.Length; i++)
        {
            var def = OrganDefs[i];
            var go  = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = def.Name;

            // Parent = the Organs GameObject (which is a child of the entity cube)
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = def.LocalPos;
            go.transform.localScale    = def.BaseScale;

            var mat = new Material(Shader.Find("Standard"));
            mat.color = def.NormalColor;
            go.GetComponent<Renderer>().material = mat;

            _organs[i] = go.transform;
            _mats[i]   = mat;
        }
    }

    // ── Called each frame by EntityCubeView ───────────────────────────────────

    public void UpdateFromSnapshot(
        EntitySnapshot                  entity,
        IReadOnlyList<TransitItemSnapshot> myTransit)
    {
        // ── Organ fills ───────────────────────────────────────────────────────
        // Esophagus: always at base scale — bolus cube carries the visual signal
        SetOrganFill(IdxEso,     0f,              false);

        // Stomach: proxy Satiation as stomach fill (0 = empty, 1 = full)
        SetOrganFill(IdxStomach, entity.Satiation, false);

        SetOrganFill(IdxSI,      entity.SiFill,          false);
        SetOrganFill(IdxLI,      entity.LiFill,          false);
        SetOrganFill(IdxColon,   entity.ColonFill,        entity.ColonIsCritical);
        SetOrganFill(IdxBladder, entity.BladderFill,      entity.BladderIsCritical);

        // ── Bolus transit cubes ───────────────────────────────────────────────
        SyncBolusCubes(myTransit);
    }

    // ── Fill / colour helper ──────────────────────────────────────────────────

    private void SetOrganFill(int idx, float fill, bool critical)
    {
        var def   = OrganDefs[idx];
        var organ = _organs[idx];
        var mat   = _mats[idx];

        float clamped = Mathf.Clamp01(fill);

        // Y scale: 20 % of base (empty) → 140 % of base (over-full)
        float scaleY = Mathf.Lerp(def.BaseScale.y * 0.20f, def.BaseScale.y * 1.40f, clamped);
        organ.localScale = new Vector3(def.BaseScale.x, scaleY, def.BaseScale.z);

        // Colour
        if (critical)
        {
            // Red–white pulse (2 Hz)
            float t  = Mathf.PingPong(Time.time * 2f, 1f);
            mat.color = Color.Lerp(EcsColors.Critical, Color.white, t * 0.5f);
        }
        else if (idx == IdxEso)
        {
            // Esophagus stays its fixed grey regardless of fill
            mat.color = def.NormalColor;
        }
        else
        {
            // Blend from dark grey (empty) → organ colour (full)
            mat.color = Color.Lerp(new Color(0.25f, 0.25f, 0.25f), def.NormalColor, clamped);
        }
    }

    // ── Bolus cubes ───────────────────────────────────────────────────────────

    private void SyncBolusCubes(IReadOnlyList<TransitItemSnapshot> transit)
    {
        // Determine which bolus IDs are active this frame
        var activeIds = new HashSet<Guid>();
        foreach (var t in transit) activeIds.Add(t.Id);

        // Spawn new bolus cubes
        foreach (var t in transit)
        {
            if (!_bolusCubes.ContainsKey(t.Id))
                _bolusCubes[t.Id] = SpawnBolusCube(t);
        }

        // Update positions
        foreach (var t in transit)
        {
            if (!_bolusCubes.TryGetValue(t.Id, out var cube)) continue;

            // Progress 0 = top of esophagus, 1 = bottom (entering stomach)
            float localY = Mathf.Lerp(EsoTopY, EsoBottomY, t.Progress);
            cube.localPosition = new Vector3(EsoX, localY, 0f);
        }

        // Destroy finished bolus cubes
        var toRemove = new List<Guid>();
        foreach (var id in _bolusCubes.Keys)
            if (!activeIds.Contains(id)) toRemove.Add(id);

        foreach (var id in toRemove)
        {
            Destroy(_bolusCubes[id].gameObject);
            _bolusCubes.Remove(id);
        }
    }

    private Transform SpawnBolusCube(TransitItemSnapshot t)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"Bolus_{t.ContentLabel}";
        go.transform.SetParent(transform, worldPositionStays: false);
        go.transform.localScale    = Vector3.one * 0.15f;
        go.transform.localPosition = new Vector3(EsoX, EsoTopY, 0f);

        // Colour by content type
        bool isWater = t.ContentLabel.Equals("Water", StringComparison.OrdinalIgnoreCase);
        var mat = new Material(Shader.Find("Standard"));
        mat.color = isWater ? EcsColors.Water : EcsColors.Bolus;
        go.GetComponent<Renderer>().material = mat;

        return go.transform;
    }
}
