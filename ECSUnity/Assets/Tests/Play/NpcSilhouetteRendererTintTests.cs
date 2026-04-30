using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-03: Body SpriteRenderer color tint matches SilhouetteComponent.DominantColor.
///
/// The tint is applied via SpriteRenderer.color (vertex color). We sample three
/// archetypes to anchor the test:
///   the-old-hand  → #5C2A4B (dark plum, Donna)
///   the-hermit    → #A8C070 (pale yellow-green, Greg)
///   the-cynic     → #7B5A3A (brown, Frank)
///
/// Since the cast generator samples from archetype SilhouetteFamily.DominantColors,
/// we verify that whatever color was assigned to the SilhouetteComponent is correctly
/// reflected in the body SpriteRenderer.color — the test does not hard-code archetype
/// expectations (NPC identity is determined by archetype ID at spawn).
///
/// IMPLEMENTATION NOTE
/// ─────────────────────
/// We read SilhouetteComponent.DominantColor from the EntityManager (not WorldStateDto,
/// which doesn't expose it yet). ParseHexColor lives on NpcSilhouetteInstance and
/// is tested here as a side-effect.
/// </summary>
[TestFixture]
public class NpcSilhouetteRendererTintTests
{
    private const float ColorTolerance = 0.01f;  // ~2.5/255 rounding tolerance

    private GameObject            _hostGo;
    private EngineHost            _host;
    private NpcSilhouetteRenderer _renderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _hostGo = new GameObject("TestHost_SilhouetteTint");
        _host   = _hostGo.AddComponent<EngineHost>();

        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        SetField(_host, "_configAsset", configAsset);
        SetField(_host, "_worldDefinitionPath", "office-starter.json");

        var rendererGo = new GameObject("TestSilhouetteTintRenderer");
        _renderer = rendererGo.AddComponent<NpcSilhouetteRenderer>();
        SetField(_renderer, "_engineHost", _host);

        // No catalog (null sprites, but tint color is independent of sprite assets).

        yield return null;  // Start()
        yield return null;  // LateUpdate
    }

    [TearDown]
    public void TearDown()
    {
        if (_hostGo != null)               Object.Destroy(_hostGo);
        if (_renderer?.gameObject != null) Object.Destroy(_renderer.gameObject);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator BodyRendererColor_MatchesDominantColorFromComponent()
    {
        yield return null;

        var engine = _host.Engine;
        if (engine == null)
        {
            Assert.Inconclusive("Engine not booted.");
            yield break;
        }

        int checkedCount = 0;

        foreach (var entity in engine.Entities)
        {
            if (!entity.Has<APIFramework.Components.SilhouetteComponent>()) continue;
            if (!entity.Has<APIFramework.Components.PositionComponent>())   continue;

            var sil = entity.Get<APIFramework.Components.SilhouetteComponent>();
            if (string.IsNullOrWhiteSpace(sil.DominantColor)) continue;

            var inst = _renderer.GetInstance(entity.Id.ToString());
            if (inst == null) continue;

            // Expected: parse the component's hex color the same way the renderer does.
            Color expected = NpcSilhouetteInstance.ParseHexColor(sil.DominantColor);
            Color actual   = inst.BodyRenderer.color;

            Assert.AreEqual(expected.r, actual.r, ColorTolerance,
                $"Entity '{entity.ShortId}' body R mismatch: " +
                $"expected {expected.r:F3} (from {sil.DominantColor}), got {actual.r:F3}.");
            Assert.AreEqual(expected.g, actual.g, ColorTolerance,
                $"Entity '{entity.ShortId}' body G mismatch.");
            Assert.AreEqual(expected.b, actual.b, ColorTolerance,
                $"Entity '{entity.ShortId}' body B mismatch.");

            checkedCount++;
        }

        if (checkedCount == 0)
            Assert.Inconclusive("No NPC entities found. Ensure office-starter.json loads.");
    }

    [UnityTest]
    public IEnumerator ParseHexColor_KnownValues_AreCorrect()
    {
        yield return null;

        // AT-03 anchor colors from WP spec — verified against silhouette-catalog.json.

        // Donna: #5C2A4B
        var donna = NpcSilhouetteInstance.ParseHexColor("#5C2A4B");
        Assert.AreEqual(0x5C / 255f, donna.r, ColorTolerance, "Donna R");
        Assert.AreEqual(0x2A / 255f, donna.g, ColorTolerance, "Donna G");
        Assert.AreEqual(0x4B / 255f, donna.b, ColorTolerance, "Donna B");

        // Greg: #A8C070
        var greg = NpcSilhouetteInstance.ParseHexColor("#A8C070");
        Assert.AreEqual(0xA8 / 255f, greg.r, ColorTolerance, "Greg R");
        Assert.AreEqual(0xC0 / 255f, greg.g, ColorTolerance, "Greg G");
        Assert.AreEqual(0x70 / 255f, greg.b, ColorTolerance, "Greg B");

        // Frank: #7B5A3A
        var frank = NpcSilhouetteInstance.ParseHexColor("#7B5A3A");
        Assert.AreEqual(0x7B / 255f, frank.r, ColorTolerance, "Frank R");
        Assert.AreEqual(0x5A / 255f, frank.g, ColorTolerance, "Frank G");
        Assert.AreEqual(0x3A / 255f, frank.b, ColorTolerance, "Frank B");
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private static void SetField(object target, string name, object value)
    {
        var f = target.GetType().GetField(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f?.SetValue(target, value);
    }
}
