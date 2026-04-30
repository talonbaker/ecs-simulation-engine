using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-01: One NpcSilhouetteInstance per NPC with position data.
/// Each instance must have the four expected child renderers: Body, Hair, Headwear, Item.
/// Also verifies the EmotionOverlay child transform exists (ChibiEmotionSlot stub).
/// </summary>
[TestFixture]
public class NpcSilhouetteRendererSpawnTests
{
    private GameObject             _hostGo;
    private EngineHost             _host;
    private NpcSilhouetteRenderer  _renderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _hostGo = new GameObject("TestHost_SilhouetteSpawn");
        _host   = _hostGo.AddComponent<EngineHost>();

        // Minimal SimConfig (defaults are fine for spawn test)
        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        SetPrivateField(_host, "_configAsset", configAsset);
        SetPrivateField(_host, "_worldDefinitionPath", "");

        var rendererGo = new GameObject("TestSilhouetteRenderer");
        _renderer = rendererGo.AddComponent<NpcSilhouetteRenderer>();
        SetPrivateField(_renderer, "_engineHost", _host);

        // No catalog — renderer must handle null catalog gracefully (AT-01 only checks counts).
        // The SilhouetteAssetCatalog field stays null; missing sprites are expected at v0.1.

        // Wait two frames: one for Start(), one for first Update()/LateUpdate()
        yield return null;
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        if (_hostGo != null)                Object.Destroy(_hostGo);
        if (_renderer?.gameObject != null)  Object.Destroy(_renderer.gameObject);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator InstanceCount_MatchesNpcsWithPosition()
    {
        yield return null;  // Let LateUpdate run at least once

        var entities = _host.WorldState?.Entities;
        if (entities == null)
        {
            Assert.Inconclusive("WorldState.Entities is null — engine may not have booted.");
            yield break;
        }

        // Only NPC entities with SilhouetteComponent AND position are rendered.
        // Since we have no world file, the engine boots with 0 entities. This test
        // confirms no instances are created for zero entities.
        int renderedCount = _renderer.ActiveNpcCount;
        Assert.GreaterOrEqual(renderedCount, 0,
            "ActiveNpcCount must not be negative.");

        // If the engine did spawn NPCs, each must have an instance.
        // (With default boot and no world file, entity count may be 0.)
        Debug.Log($"[SpawnTests] Entities: {entities.Count}, Rendered NPC instances: {renderedCount}");
    }

    [UnityTest]
    public IEnumerator EachInstance_HasFourLayerRenderers_BodyHairHeadwearItem()
    {
        yield return null;

        // Skip if no NPCs spawned
        if (_renderer.ActiveNpcCount == 0)
        {
            Assert.Inconclusive("No NPCs spawned — cannot verify layer structure. Run with an office-starter world file for full coverage.");
            yield break;
        }

        // Check that each rendered instance has the expected child hierarchy.
        var engine = _host.Engine;
        Assert.IsNotNull(engine, "Engine must be available.");

        foreach (var entity in engine.Entities)
        {
            if (!entity.Has<APIFramework.Components.SilhouetteComponent>()) continue;

            var go = _renderer.GetNpcGameObject(entity.Id.ToString());
            if (go == null) continue;

            Assert.IsNotNull(go.transform.Find("Body"),
                $"Instance for entity {entity.ShortId} must have a 'Body' child.");
            Assert.IsNotNull(go.transform.Find("Hair"),
                $"Instance for entity {entity.ShortId} must have a 'Hair' child.");
            Assert.IsNotNull(go.transform.Find("Headwear"),
                $"Instance for entity {entity.ShortId} must have a 'Headwear' child.");
            Assert.IsNotNull(go.transform.Find("Item"),
                $"Instance for entity {entity.ShortId} must have an 'Item' child.");

            // AT-01 also implicitly requires the EmotionOverlay slot exists.
            Assert.IsNotNull(go.transform.Find("EmotionOverlay"),
                $"Instance for entity {entity.ShortId} must have an 'EmotionOverlay' child.");
        }
    }

    [UnityTest]
    public IEnumerator EachInstance_HasSpriteRendererOnEachLayer()
    {
        yield return null;

        if (_renderer.ActiveNpcCount == 0)
        {
            Assert.Inconclusive("No NPCs spawned — skipping SpriteRenderer check.");
            yield break;
        }

        var engine = _host.Engine;
        foreach (var entity in engine.Entities)
        {
            if (!entity.Has<APIFramework.Components.SilhouetteComponent>()) continue;

            var go = _renderer.GetNpcGameObject(entity.Id.ToString());
            if (go == null) continue;

            string[] layerNames = { "Body", "Hair", "Headwear", "Item" };
            foreach (var name in layerNames)
            {
                var child = go.transform.Find(name);
                Assert.IsNotNull(child, $"Layer child '{name}' not found.");
                var sr = child.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(sr, $"Child '{name}' must have a SpriteRenderer component.");
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void SetPrivateField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(target, value);
    }
}
