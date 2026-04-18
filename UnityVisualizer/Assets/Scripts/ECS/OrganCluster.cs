using System;
using System.Collections.Generic;
using UnityEngine;
using APIFramework.Core;

/// <summary>
/// Renders the full GI tract as a horizontal strip of coloured cubes parented
/// to an entity cube.  The strip extends to the entity's right (+X local).
///
/// Each organ has a shell (dark container) and a fill cube (content colour,
/// Y-scaled 0 to full).  A motion-bolus cube slides left/right when fill > 0.
/// Esophagus is special: transit bolus cubes slide top-to-bottom (orange=food,
/// cyan=water).  Colon and bladder spawn falling discharge cubes on emptying.
///
/// ORGAN LAYOUT  (local coords relative to entity cube centre)
///   Esophagus    (1.5,  0.5, 0)  grey tube
///   Stomach      (2.8,  0.0, 0)  orange     (food digesting)
///   SmallIntest  (4.0,  0.0, 0)  orange-tan (nutrient chyme)
///   LargeIntest  (5.2,  0.0, 0)  brown      (waste forming)
///   Colon        (6.2,  0.2, 0)  dark brown (ready for expulsion)
///   Bladder      (6.2, -0.6, 0)  YELLOW     (urine)
/// </summary>
public class OrganCluster : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Organ definition

    private struct OrganDef
    {
        public string  Name;
        public Vector3 LocalPos;
        public Vector3 ShellScale;
        public Color   ShellColor;
        public Color   ContentColor;
        public bool    HasMotion;
    }

    private static readonly OrganDef[] Defs =
    {
        new OrganDef { Name = "Esophagus",
            LocalPos = new Vector3(1.5f,  0.5f,  0f),
            ShellScale = new Vector3(0.20f, 1.10f, 0.20f),
            ShellColor = EcsColors.Esophagus, ContentColor = EcsColors.Esophagus,
            HasMotion = false },

        new OrganDef { Name = "Stomach",
            LocalPos = new Vector3(2.8f,  0.0f,  0f),
            ShellScale = new Vector3(0.85f, 0.65f, 0.55f),
            ShellColor = new Color(0.22f, 0.10f, 0.02f), ContentColor = EcsColors.Stomach,
            HasMotion = true },

        new OrganDef { Name = "SmallIntest",
            LocalPos = new Vector3(4.0f,  0.0f,  0f),
            ShellScale = new Vector3(0.65f, 0.55f, 0.45f),
            ShellColor = new Color(0.18f, 0.10f, 0.04f), ContentColor = EcsColors.SmallIntestine,
            HasMotion = true },

        new OrganDef { Name = "LargeIntest",
            LocalPos = new Vector3(5.2f,  0.0f,  0f),
            ShellScale = new Vector3(0.65f, 0.55f, 0.45f),
            ShellColor = new Color(0.12f, 0.07f, 0.02f), ContentColor = EcsColors.LargeIntestine,
            HasMotion = true },

        new OrganDef { Name = "Colon",
            LocalPos = new Vector3(6.2f,  0.2f,  0f),
            ShellScale = new Vector3(0.48f, 0.48f, 0.38f),
            ShellColor = new Color(0.08f, 0.04f, 0.01f), ContentColor = EcsColors.Colon,
            HasMotion = false },

        new OrganDef { Name = "Bladder",
            LocalPos = new Vector3(6.2f, -0.6f,  0f),
            ShellScale = new Vector3(0.48f, 0.44f, 0.38f),
            ShellColor = new Color(0.14f, 0.12f, 0.01f), ContentColor = EcsColors.Bladder,
            HasMotion = false },
    };

    private const int IdxEso     = 0;
    private const int IdxStomach = 1;
    private const int IdxSI      = 2;
    private const int IdxLI      = 3;
    private const int IdxColon   = 4;
    private const int IdxBladder = 5;

    // -------------------------------------------------------------------------
    // Runtime state

    private Transform[] _shells;
    private Material[]  _shellMats;
    private Transform[] _fills;
    private Material[]  _fillMats;
    private Transform[] _motionBolus;
    private Material[]  _motionMats;

    // Esophagus transit cubes keyed by snapshot ID
    private readonly Dictionary<Guid, Transform> _transitCubes = new Dictionary<Guid, Transform>();

    // Falling discharge cubes (waste/urine drops)
    private class FallCube
    {
        public Transform  Tr;
        public Material   Mat;
        public Color      StartColor;
        public float      BirthTime;
        public float      FallSpeed;
        public const float Lifetime = 1.2f;
    }
    private readonly List<FallCube> _falling = new List<FallCube>();

    // Previous fills to detect sudden drops (defecation / urination)
    private float _prevColonFill   = -1f;
    private float _prevBladderFill = -1f;

    // -------------------------------------------------------------------------
    // Awake

    void Awake()
    {
        int n    = Defs.Length;
        _shells      = new Transform[n];
        _shellMats   = new Material[n];
        _fills       = new Transform[n];
        _fillMats    = new Material[n];
        _motionBolus = new Transform[n];
        _motionMats  = new Material[n];

        for (int i = 0; i < n; i++)
        {
            var def = Defs[i];

            // Shell (outer container)
            var shell = MakeCube(def.Name + "_Shell", transform,
                                 def.LocalPos, def.ShellScale, def.ShellColor);
            _shells[i]    = shell;
            _shellMats[i] = shell.GetComponent<Renderer>().material;

            // Fill (content indicator, grows upward from organ bottom)
            var fillScale = new Vector3(def.ShellScale.x * 0.82f,
                                        def.ShellScale.y * 0.15f,
                                        def.ShellScale.z * 0.82f);
            var fill = MakeCube(def.Name + "_Fill", transform,
                                def.LocalPos, fillScale, EcsColors.OrganEmpty);
            _fills[i]    = fill;
            _fillMats[i] = fill.GetComponent<Renderer>().material;

            // Motion bolus (tiny sliding dot showing content movement)
            if (def.HasMotion)
            {
                float mSize = Mathf.Min(def.ShellScale.x, def.ShellScale.z) * 0.35f;
                var motion = MakeCube(def.Name + "_Motion", transform, def.LocalPos,
                                      new Vector3(mSize, mSize, mSize), def.ContentColor);
                motion.gameObject.SetActive(false);
                _motionBolus[i] = motion;
                _motionMats[i]  = motion.GetComponent<Renderer>().material;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Update

    void Update()
    {
        TickFallingCubes();
    }

    // -------------------------------------------------------------------------
    // Called each frame by EntityCubeView

    public void UpdateFromSnapshot(EntitySnapshot entity,
                                   IReadOnlyList<TransitItemSnapshot> myTransit)
    {
        // Fill levels per organ
        float[] fills = new float[]
        {
            0f,                 // Esophagus: transit bolus cubes handle visuals
            entity.Satiation,   // Stomach
            entity.SiFill,      // Small intestine
            entity.LiFill,      // Large intestine
            entity.ColonFill,   // Colon
            entity.BladderFill, // Bladder
        };

        bool[] criticals = new bool[]
        {
            false, false, false, false,
            entity.ColonIsCritical,
            entity.BladderIsCritical,
        };

        for (int i = 1; i < Defs.Length; i++) // skip esophagus (i=0)
        {
            ApplyFill(i, fills[i], criticals[i]);
        }

        // Motion boluses for organs that have content flowing through
        UpdateMotion(IdxStomach, entity.Satiation,  EcsColors.Stomach);
        UpdateMotion(IdxSI,      entity.SiFill,     EcsColors.SmallIntestine);
        UpdateMotion(IdxLI,      entity.LiFill,     EcsColors.LargeIntestine);

        // Esophagus: per-bolus transit cubes
        SyncTransitCubes(myTransit);

        // Discharge detection: big fill drop = emptying event
        if (_prevColonFill   > 0.25f && entity.ColonFill   < _prevColonFill   - 0.20f)
            SpawnDischarge(IdxColon,   EcsColors.Waste);
        if (_prevBladderFill > 0.20f && entity.BladderFill < _prevBladderFill - 0.15f)
            SpawnDischarge(IdxBladder, EcsColors.Urine);

        _prevColonFill   = entity.ColonFill;
        _prevBladderFill = entity.BladderFill;
    }

    // -------------------------------------------------------------------------
    // Fill / colour

    private void ApplyFill(int idx, float fill, bool critical)
    {
        float clamped = Mathf.Clamp01(fill);
        var   def     = Defs[idx];
        var   fillTr  = _fills[idx];
        var   fillMat = _fillMats[idx];

        // Grow from the bottom of the shell upward
        float targetY  = Mathf.Lerp(def.ShellScale.y * 0.15f,
                                     def.ShellScale.y * 0.88f, clamped);
        float shellBot = def.LocalPos.y - def.ShellScale.y * 0.5f;
        float fillCtrY = shellBot + targetY * 0.5f;

        fillTr.localScale    = new Vector3(def.ShellScale.x * 0.82f, targetY,
                                            def.ShellScale.z * 0.82f);
        fillTr.localPosition = new Vector3(def.LocalPos.x, fillCtrY, def.LocalPos.z);

        if (critical)
        {
            float t = Mathf.PingPong(Time.time * 2f, 1f);
            fillMat.color = Color.Lerp(EcsColors.Critical, Color.white, t * 0.5f);
        }
        else
        {
            fillMat.color = Color.Lerp(EcsColors.OrganEmpty, def.ContentColor, clamped);
        }
    }

    // -------------------------------------------------------------------------
    // Motion bolus

    private void UpdateMotion(int idx, float fill, Color col)
    {
        var bolus = _motionBolus[idx];
        if (bolus == null) return;

        if (fill < 0.08f)
        {
            bolus.gameObject.SetActive(false);
            return;
        }

        bolus.gameObject.SetActive(true);

        var   def    = Defs[idx];
        float halfW  = def.ShellScale.x * 0.40f;
        float speed  = Mathf.Lerp(0.25f, 1.4f, fill);
        float t      = Mathf.PingPong(Time.time * speed, 1f);

        float shellBot = def.LocalPos.y - def.ShellScale.y * 0.5f;
        float fillH    = Mathf.Lerp(def.ShellScale.y * 0.15f,
                                     def.ShellScale.y * 0.88f, Mathf.Clamp01(fill));
        float fillTop  = shellBot + fillH;

        bolus.localPosition = new Vector3(
            def.LocalPos.x + Mathf.Lerp(-halfW, halfW, t),
            fillTop - def.ShellScale.y * 0.12f,
            def.LocalPos.z);

        _motionMats[idx].color = Color.Lerp(col * 0.6f, col, Mathf.Clamp01(fill));
    }

    // -------------------------------------------------------------------------
    // Esophagus transit boluses

    private const float EsoTopY    =  1.0f;
    private const float EsoBottomY = -0.05f;

    private void SyncTransitCubes(IReadOnlyList<TransitItemSnapshot> transit)
    {
        var active = new HashSet<Guid>();
        foreach (var t in transit) active.Add(t.Id);

        foreach (var t in transit)
            if (!_transitCubes.ContainsKey(t.Id))
                _transitCubes[t.Id] = SpawnTransitCube(t);

        foreach (var t in transit)
        {
            if (!_transitCubes.TryGetValue(t.Id, out var cube)) continue;
            float localY = Mathf.Lerp(EsoTopY, EsoBottomY, t.Progress);
            cube.localPosition = new Vector3(Defs[IdxEso].LocalPos.x, localY, 0f);
        }

        var toRemove = new List<Guid>();
        foreach (var id in _transitCubes.Keys)
            if (!active.Contains(id)) toRemove.Add(id);
        foreach (var id in toRemove)
        {
            Destroy(_transitCubes[id].gameObject);
            _transitCubes.Remove(id);
        }
    }

    private Transform SpawnTransitCube(TransitItemSnapshot t)
    {
        bool isWater = t.ContentLabel.Equals("Water", StringComparison.OrdinalIgnoreCase);
        Color col    = isWater ? EcsColors.Water : EcsColors.Bolus;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Transit_" + t.ContentLabel;
        go.transform.SetParent(transform, false);
        go.transform.localScale    = Vector3.one * 0.18f;
        go.transform.localPosition = new Vector3(Defs[IdxEso].LocalPos.x, EsoTopY, 0f);

        var mat   = new Material(Shader.Find("Standard"));
        mat.color = col;
        go.GetComponent<Renderer>().material = mat;
        return go.transform;
    }

    // -------------------------------------------------------------------------
    // Discharge (falling drop)

    private void SpawnDischarge(int organIdx, Color col)
    {
        var pos = transform.TransformPoint(Defs[organIdx].LocalPos);
        var go  = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Discharge_" + Defs[organIdx].Name;
        go.transform.localScale = Vector3.one * 0.22f;
        go.transform.position   = pos;
        Destroy(go.GetComponent<Collider>());

        var mat = new Material(Shader.Find("Standard"));
        // Enable transparency mode so alpha fade works
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.color = col;
        go.GetComponent<Renderer>().material = mat;

        _falling.Add(new FallCube
        {
            Tr         = go.transform,
            Mat        = mat,
            StartColor = col,
            BirthTime  = Time.time,
            FallSpeed  = 2.2f,
        });
    }

    private void TickFallingCubes()
    {
        for (int i = _falling.Count - 1; i >= 0; i--)
        {
            var   fc  = _falling[i];
            float age = Time.time - fc.BirthTime;

            if (age >= FallCube.Lifetime)
            {
                Destroy(fc.Tr.gameObject);
                _falling.RemoveAt(i);
                continue;
            }

            float t = age / FallCube.Lifetime;
            fc.Tr.position += Vector3.down * fc.FallSpeed * Time.deltaTime;
            var c = fc.StartColor;
            fc.Mat.color = new Color(c.r, c.g, c.b, 1f - t);
        }
    }

    // -------------------------------------------------------------------------
    // Utility

    private static Transform MakeCube(string name, Transform parent,
                                       Vector3 localPos, Vector3 localScale, Color col)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = localScale;

        var mat   = new Material(Shader.Find("Standard"));
        mat.color = col;
        go.GetComponent<Renderer>().material = mat;
        return go.transform;
    }
}
