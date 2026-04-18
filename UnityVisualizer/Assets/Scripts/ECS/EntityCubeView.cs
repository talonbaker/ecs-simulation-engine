using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using APIFramework.Core;
using APIFramework.Components;

/// <summary>
/// Renders a living entity (Billy, Cat …) as a 1×1×1 cube that slides
/// smoothly to its ECS position each frame.
///
/// Children:
///   Label      — floating TextMesh (name / destination / drive)
///   Organs     — OrganCluster that renders the GI strip to the entity's right
///
/// Spawned and driven by WorldSceneBuilder.
/// </summary>
public class EntityCubeView : MonoBehaviour
{
    private Renderer     _renderer;
    private Material     _mat;
    private TextMesh     _label;
    private OrganCluster _organs;

    private Vector3 _targetPos;
    private bool    _initialized;

    // How fast the cube slides towards its ECS target position (lerp speed).
    private const float LerpSpeed = 15f;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        // ── Body cube material ────────────────────────────────────────────────
        _renderer          = GetComponent<Renderer>();
        _mat               = new Material(Shader.Find("Standard"));
        _renderer.material = _mat;

        // ── Floating label above the cube ─────────────────────────────────────
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(transform, worldPositionStays: false);
        labelGo.transform.localScale = Vector3.one * 0.14f;

        _label           = labelGo.AddComponent<TextMesh>();
        _label.alignment = TextAlignment.Center;
        _label.anchor    = TextAnchor.LowerCenter;
        _label.fontSize  = 28;
        _label.color     = Color.white;

        // ── Organ strip (child GameObject, positioned to the entity's right) ──
        var organGo = new GameObject("Organs");
        organGo.transform.SetParent(transform, worldPositionStays: false);
        _organs = organGo.AddComponent<OrganCluster>();
    }

    void Update()
    {
        if (!_initialized) return;

        // Smooth slide — lerp in world space; the entity "floats" 0.5 units above y=0
        transform.position = Vector3.Lerp(
            transform.position,
            _targetPos,
            Time.deltaTime * LerpSpeed);
    }

    // ── Called every frame by WorldSceneBuilder ───────────────────────────────

    public void UpdateFromSnapshot(
        EntitySnapshot                     entity,
        IReadOnlyList<TransitItemSnapshot> transitItems,
        float                              worldScale)
    {
        // Body colour driven by dominant desire / state
        _mat.color = EcsColors.ForEntity(entity);

        // Target world position (+ 0.5 so cube sits on the floor plane)
        _targetPos = new Vector3(
            entity.PosX * worldScale,
            entity.PosY * worldScale + 0.5f,
            entity.PosZ * worldScale);
        _initialized = true;

        // ── Label text ────────────────────────────────────────────────────────
        // Line 1 — entity name
        // Line 2 — movement destination (if walking somewhere)
        // Line 3 — dominant drive / state abbreviation
        string moveLine = entity.IsMoving && !string.IsNullOrEmpty(entity.MoveTarget)
            ? $"\n→ {entity.MoveTarget}"
            : string.Empty;

        string driveLine = entity.IsSleeping
            ? "ZZZ"
            : FormatDrive(entity.Dominant);

        _label.text = $"{entity.Name}{moveLine}\n{driveLine}";

        // Lift label just above the cube top (cube half-height = 0.5)
        _label.transform.localPosition = new Vector3(0f, 0.5f + 0.4f, 0f);

        // ── Organ strip ───────────────────────────────────────────────────────
        var myTransitItems = transitItems
            .Where(t => t.TargetEntityId == entity.Id)
            .ToList();

        _organs.UpdateFromSnapshot(entity, myTransitItems);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static string FormatDrive(DesireType drive) => drive switch
    {
        DesireType.Eat      => "EAT",
        DesireType.Drink    => "DRINK",
        DesireType.Sleep    => "SLEEP",
        DesireType.Defecate => "DEFECATE",
        DesireType.Pee      => "PEE",
        _                   => "IDLE",
    };
}
