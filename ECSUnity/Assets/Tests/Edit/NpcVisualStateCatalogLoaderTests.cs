using APIFramework.Systems.Animation;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AT-03 (Unity-side): NpcVisualStateCatalogLoader ScriptableObject loads the catalog
/// correctly and handles missing/null TextAsset gracefully.
/// </summary>
[TestFixture]
public class NpcVisualStateCatalogLoaderTests
{
    private NpcVisualStateCatalogLoader _loaderAsset;

    [SetUp]
    public void SetUp()
    {
        _loaderAsset = ScriptableObject.CreateInstance<NpcVisualStateCatalogLoader>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_loaderAsset != null)
            Object.DestroyImmediate(_loaderAsset);
    }

    // ── Graceful fallback when no TextAsset is assigned ───────────────────────

    [Test]
    public void Catalog_WithoutTextAsset_ReturnsNonNullCatalog()
    {
        // No TextAsset assigned; loader attempts path search or returns empty catalog.
        var catalog = _loaderAsset.Catalog;
        Assert.IsNotNull(catalog, "Catalog must never be null even without a TextAsset.");
    }

    [Test]
    public void Catalog_WithoutTextAsset_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => { var _ = _loaderAsset.Catalog; },
            "Accessing Catalog without a TextAsset must not throw.");
    }

    // ── With inline JSON ──────────────────────────────────────────────────────

    [Test]
    public void ParseJson_ValidCatalog_AllStatesLoaded()
    {
        const string json = @"{
            ""schemaVersion"": ""0.1.0"",
            ""states"": [
                { ""stateId"": ""Idle"",   ""frameDurationMs"": 200, ""accentColor"": ""#aaa"" },
                { ""stateId"": ""Walk"",   ""frameDurationMs"": 120, ""accentColor"": ""#bbb"" },
                { ""stateId"": ""Eating"", ""frameDurationMs"": 250, ""accentColor"": ""#ccc"" }
            ],
            ""cues"": [], ""transitions"": []
        }";
        var catalog = NpcVisualStateCatalogLoader.ParseJson(json);
        Assert.AreEqual(3, catalog.States.Count);
        Assert.IsNotNull(catalog.GetState("Idle"));
        Assert.IsNotNull(catalog.GetState("Walk"));
        Assert.IsNotNull(catalog.GetState("Eating"));
    }

    [Test]
    public void ParseJson_ValidCatalog_AllCuesLoaded()
    {
        const string json = @"{
            ""schemaVersion"": ""0.1.0"",
            ""states"": [],
            ""cues"": [
                { ""cueId"": ""sweat"", ""spriteAsset"": ""cue_sweat_drop.png"",
                  ""fadeAltitudeStart"": 25, ""fadeAltitudeEnd"": 35, ""minScaleMult"": 1.25 }
            ],
            ""transitions"": []
        }";
        var catalog = NpcVisualStateCatalogLoader.ParseJson(json);
        Assert.AreEqual(1, catalog.Cues.Count);
        var cue = catalog.GetCue("sweat");
        Assert.IsNotNull(cue);
        Assert.AreEqual(25f, cue.FadeAltitudeStart, 0.01f);
        Assert.AreEqual(35f, cue.FadeAltitudeEnd,   0.01f);
        Assert.AreEqual(1.25f, cue.MinScaleMult,    0.01f);
    }

    [Test]
    public void ParseJson_MissingFrameDuration_UsesDefault200()
    {
        const string json = @"{
            ""schemaVersion"": ""0.1.0"",
            ""states"": [{ ""stateId"": ""Idle"", ""accentColor"": ""#aaa"" }],
            ""cues"": [], ""transitions"": []
        }";
        var catalog = NpcVisualStateCatalogLoader.ParseJson(json);
        var state   = catalog.GetState("Idle");
        Assert.IsNotNull(state);
        Assert.AreEqual(200, state.FrameDurationMs,
            "Missing frameDurationMs should default to 200.");
    }

    [Test]
    public void ParseJson_MissingCueAffinity_UsesEmptyArray()
    {
        const string json = @"{
            ""schemaVersion"": ""0.1.0"",
            ""states"": [{ ""stateId"": ""Walk"", ""frameDurationMs"": 120, ""accentColor"": ""#ccc"" }],
            ""cues"": [], ""transitions"": []
        }";
        var catalog = NpcVisualStateCatalogLoader.ParseJson(json);
        var state   = catalog.GetState("Walk");
        Assert.IsNotNull(state);
        Assert.IsNotNull(state.CueAffinity);
        Assert.AreEqual(0, state.CueAffinity.Length,
            "Missing cueAffinity should fall back to empty array.");
    }

    // ── Transition lookup ─────────────────────────────────────────────────────

    [Test]
    public void GetTransition_CataloguedPair_ReturnsEntry()
    {
        const string json = @"{
            ""schemaVersion"": ""0.1.0"",
            ""states"": [], ""cues"": [],
            ""transitions"": [
                { ""from"": ""Walk"", ""to"": ""CoughingFit"",
                  ""intermediateFrames"": [12, 13, 14], ""totalDurationMs"": 360 }
            ]
        }";
        var catalog    = NpcVisualStateCatalogLoader.ParseJson(json);
        var transition = catalog.GetTransition(
            APIFramework.Components.NpcAnimationState.Walk,
            APIFramework.Components.NpcAnimationState.CoughingFit);
        Assert.IsNotNull(transition,
            "Catalogued Walk→CoughingFit transition must be returned.");
        Assert.AreEqual(3, transition.IntermediateFrames.Length);
        Assert.AreEqual(360, transition.TotalDurationMs);
    }

    [Test]
    public void GetTransition_UncataloguedPair_ReturnsNull()
    {
        var catalog = NpcVisualStateCatalogLoader.ParseJson(@"{ ""states"": [], ""cues"": [], ""transitions"": [] }");
        var result  = catalog.GetTransition(
            APIFramework.Components.NpcAnimationState.Idle,
            APIFramework.Components.NpcAnimationState.Talk);
        Assert.IsNull(result, "Uncatalogued transition pair should return null (snap is default).");
    }

    // ── Empty catalog safety ──────────────────────────────────────────────────

    [Test]
    public void EmptyCatalog_GetFrameDurationMs_Returns200()
    {
        var catalog = NpcVisualStateCatalogLoader.Empty;
        int dur = catalog.GetFrameDurationMs(APIFramework.Components.NpcAnimationState.Idle);
        Assert.AreEqual(200, dur,
            "Empty catalog GetFrameDurationMs should return fallback 200ms.");
    }

    [Test]
    public void EmptyCatalog_GetAnimatorSpeed_Returns1()
    {
        var catalog = NpcVisualStateCatalogLoader.Empty;
        float speed = catalog.GetAnimatorSpeed(APIFramework.Components.NpcAnimationState.Walk);
        Assert.AreEqual(1f, speed, 0.001f,
            "Empty catalog GetAnimatorSpeed should return 1.0 (neutral speed).");
    }
}
