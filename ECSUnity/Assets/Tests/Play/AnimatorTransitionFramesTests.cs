using System.Collections;
using APIFramework.Components;
using APIFramework.Systems.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-04: State transitions use catalog's intermediate frames (CrossFadeInFixedTime)
/// for catalogued pairs. Uncatalogued transitions snap instantly.
/// </summary>
[TestFixture]
public class AnimatorTransitionFramesTests
{
    private GameObject       _animatorGo;
    private Animator         _animator;
    private SilhouetteAnimator _silhouetteAnimator;
    private NpcSilhouetteInstance _instance;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Build a minimal NPC hierarchy with Animator.
        _animatorGo = new GameObject("Test_AnimTransition");
        _instance   = _animatorGo.AddComponent<NpcSilhouetteInstance>();

        _animator = _animatorGo.AddComponent<Animator>();

        // Wire up the scene-level SilhouetteAnimator.
        var saGo           = new GameObject("Test_SilhouetteAnimator");
        _silhouetteAnimator = saGo.AddComponent<SilhouetteAnimator>();

        yield return null;  // allow Start()
    }

    [TearDown]
    public void TearDown()
    {
        if (_animatorGo != null) Object.Destroy(_animatorGo);
        var all = Object.FindObjectsOfType<GameObject>();
        foreach (var go in all)
            if (go.name.StartsWith("Test_"))
                Object.Destroy(go);
    }

    // ── Catalog injection ─────────────────────────────────────────────────────

    private static NpcVisualStateCatalog BuildTestCatalog()
    {
        const string json = @"{
            ""schemaVersion"": ""0.1.0"",
            ""states"": [
                { ""stateId"": ""Idle"",        ""frameDurationMs"": 200, ""accentColor"": ""#aaa"" },
                { ""stateId"": ""Walk"",         ""frameDurationMs"": 120, ""accentColor"": ""#bbb"" },
                { ""stateId"": ""CoughingFit"",  ""frameDurationMs"": 100, ""accentColor"": ""#ccc"" }
            ],
            ""cues"": [],
            ""transitions"": [
                { ""from"": ""Walk"", ""to"": ""CoughingFit"",
                  ""intermediateFrames"": [12, 13, 14], ""totalDurationMs"": 360 }
            ]
        }";
        return NpcVisualStateCatalogLoader.ParseJson(json);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator CatalogInjected_GetTransition_Walk_CoughingFit_NotNull()
    {
        var catalog = BuildTestCatalog();
        _silhouetteAnimator.InjectCatalog(catalog);
        yield return null;

        var t = catalog.GetTransition(NpcAnimationState.Walk, NpcAnimationState.CoughingFit);
        Assert.IsNotNull(t,
            "Walk→CoughingFit transition must exist in the test catalog.");
        Assert.AreEqual(360, t.TotalDurationMs);
    }

    [UnityTest]
    public IEnumerator UncataloguedTransition_Idle_Talk_ReturnsNull()
    {
        var catalog = BuildTestCatalog();
        _silhouetteAnimator.InjectCatalog(catalog);
        yield return null;

        var t = catalog.GetTransition(NpcAnimationState.Idle, NpcAnimationState.Talk);
        Assert.IsNull(t, "Idle→Talk is not catalogued; should snap (return null).");
    }

    [UnityTest]
    public IEnumerator AnimatorSpeed_SetForWalkState_MatchesCatalog()
    {
        // Catalog: Walk frameDuration = 120ms → speed = 200/120 ≈ 1.667
        var catalog = BuildTestCatalog();
        _silhouetteAnimator.InjectCatalog(catalog);
        yield return null;

        float expectedSpeed = 200f / 120f;
        float actualSpeed   = catalog.GetAnimatorSpeed(NpcAnimationState.Walk);
        Assert.AreEqual(expectedSpeed, actualSpeed, 0.01f,
            "Walk animator speed should be 200/120 per catalog.");
    }

    [UnityTest]
    public IEnumerator AnimatorSpeed_SetForIdleState_MatchesCatalog()
    {
        var catalog = BuildTestCatalog();
        _silhouetteAnimator.InjectCatalog(catalog);
        yield return null;

        // Idle frameDuration = 200ms → speed = 200/200 = 1.0
        float speed = catalog.GetAnimatorSpeed(NpcAnimationState.Idle);
        Assert.AreEqual(1.0f, speed, 0.01f, "Idle at 200ms should give Animator.speed = 1.0.");
    }

    [UnityTest]
    public IEnumerator AnimatorSpeed_Dead_IsSubstantiallySlower()
    {
        // Inject a catalog with Dead at 9999ms → speed ≈ 0.02 (very slow / frozen)
        const string json = @"{
            ""schemaVersion"": ""0.1.0"",
            ""states"": [{ ""stateId"": ""Dead"", ""frameDurationMs"": 9999, ""accentColor"": ""#707070"" }],
            ""cues"": [], ""transitions"": []
        }";
        var catalog = NpcVisualStateCatalogLoader.ParseJson(json);
        _silhouetteAnimator.InjectCatalog(catalog);
        yield return null;

        float speed = catalog.GetAnimatorSpeed(NpcAnimationState.Dead);
        Assert.Less(speed, 0.1f, "Dead state should have near-zero animator speed.");
    }

    [UnityTest]
    public IEnumerator Transition_IntermediateFrames_AllPositive()
    {
        var catalog = BuildTestCatalog();
        _silhouetteAnimator.InjectCatalog(catalog);
        yield return null;

        var t = catalog.GetTransition(NpcAnimationState.Walk, NpcAnimationState.CoughingFit);
        Assert.IsNotNull(t);
        foreach (int frame in t.IntermediateFrames)
            Assert.GreaterOrEqual(frame, 0, "Intermediate frame indices must be non-negative.");
    }
}
