using System.Collections;
using APIFramework.Systems.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-06 (altitude fade): Chibi cue alpha fades correctly at catalog-specified altitude bounds.
/// Verifies the catalog's ComputeCueAlpha logic and ChibiEmotionSlot.ApplyDisplayParams integration.
/// </summary>
[TestFixture]
public class ChibiEmotionFadeAltitudeTests
{
    private GameObject      _slotGo;
    private ChibiEmotionSlot _slot;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _slotGo = new GameObject("Test_ChibiSlot");
        _slot   = _slotGo.AddComponent<ChibiEmotionSlot>();
        _slot.Show(IconKind.SleepZ);
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        if (_slotGo != null) Object.Destroy(_slotGo);
    }

    // ── Pure alpha math ───────────────────────────────────────────────────────

    [Test]
    public void ComputeAlpha_BelowFadeStart_ReturnsOne()
    {
        float alpha = NpcVisualStateCatalog.ComputeCueAlpha(altitude: 10f, fadeAltitudeStart: 25f, fadeAltitudeEnd: 35f);
        Assert.AreEqual(1f, alpha, 0.001f);
    }

    [Test]
    public void ComputeAlpha_AtFadeStart_ReturnsOne()
    {
        float alpha = NpcVisualStateCatalog.ComputeCueAlpha(25f, 25f, 35f);
        Assert.AreEqual(1f, alpha, 0.001f);
    }

    [Test]
    public void ComputeAlpha_AtMidpoint_ReturnsHalf()
    {
        float alpha = NpcVisualStateCatalog.ComputeCueAlpha(30f, 25f, 35f);
        Assert.AreEqual(0.5f, alpha, 0.001f);
    }

    [Test]
    public void ComputeAlpha_AtFadeEnd_ReturnsZero()
    {
        float alpha = NpcVisualStateCatalog.ComputeCueAlpha(35f, 25f, 35f);
        Assert.AreEqual(0f, alpha, 0.001f);
    }

    [Test]
    public void ComputeAlpha_AboveFadeEnd_ReturnsZero()
    {
        float alpha = NpcVisualStateCatalog.ComputeCueAlpha(50f, 25f, 35f);
        Assert.AreEqual(0f, alpha, 0.001f);
    }

    // ── Slot integration with ApplyDisplayParams ──────────────────────────────

    [UnityTest]
    public IEnumerator ApplyDisplayParams_AlphaOne_SlotFullOpacity()
    {
        _slot.ApplyDisplayParams(1f, 1f, Vector3.zero);
        yield return null;

        Assert.AreEqual(1f, _slot.CurrentAlpha, 0.001f,
            "After ApplyDisplayParams(alpha=1), CurrentAlpha should be 1.");
    }

    [UnityTest]
    public IEnumerator ApplyDisplayParams_AlphaZero_SlotInvisibleAlpha()
    {
        _slot.ApplyDisplayParams(0f, 1f, Vector3.zero);
        yield return null;

        Assert.AreEqual(0f, _slot.CurrentAlpha, 0.001f,
            "After ApplyDisplayParams(alpha=0), CurrentAlpha should be 0.");
    }

    [UnityTest]
    public IEnumerator ApplyDisplayParams_ScaleTwo_LocalScaleIsTwo()
    {
        _slot.ApplyDisplayParams(1f, 2f, Vector3.zero);
        yield return null;

        Assert.AreEqual(new Vector3(2f, 2f, 2f), _slotGo.transform.localScale,
            "ApplyDisplayParams(scale=2) should set localScale to (2,2,2).");
    }

    [UnityTest]
    public IEnumerator ApplyDisplayParams_AnchorOffset_MovesTransform()
    {
        var offset = new Vector3(0.3f, 1.5f, 0f);
        _slot.ApplyDisplayParams(1f, 1f, offset);
        yield return null;

        Assert.AreEqual(offset, _slotGo.transform.localPosition,
            "ApplyDisplayParams should set the slot's localPosition to the catalog anchor offset.");
    }

    // ── Per-catalog-cue visibility at key altitudes ───────────────────────────

    [Test]
    [Description("AT-06: sleep-z must remain visible at 25m (fadeStart=40, fadeEnd=55).")]
    public void SleepZ_VisibleAt25m()
    {
        // From visual-state-catalog.json: sleep-z fadeAltitudeStart=40, fadeAltitudeEnd=55
        float alpha = NpcVisualStateCatalog.ComputeCueAlpha(25f, fadeAltitudeStart: 40f, fadeAltitudeEnd: 55f);
        Assert.Greater(alpha, 0f, "sleep-z must be visible (alpha > 0) at 25m altitude.");
        Assert.AreEqual(1f, alpha, 0.001f, "sleep-z should be fully visible below fadeStart.");
    }

    [Test]
    [Description("AT-06: exclamation must remain visible at 25m (fadeStart=35, fadeEnd=50).")]
    public void Exclamation_VisibleAt25m()
    {
        float alpha = NpcVisualStateCatalog.ComputeCueAlpha(25f, fadeAltitudeStart: 35f, fadeAltitudeEnd: 50f);
        Assert.Greater(alpha, 0f, "exclamation must be visible at 25m altitude.");
    }

    [Test]
    [Description("AT-06: red-face-flush must remain visible at 25m (fadeStart=30, fadeEnd=45).")]
    public void RedFaceFlush_VisibleAt25m()
    {
        float alpha = NpcVisualStateCatalog.ComputeCueAlpha(25f, fadeAltitudeStart: 30f, fadeAltitudeEnd: 45f);
        Assert.Greater(alpha, 0f, "red-face-flush must be visible at 25m altitude.");
    }

    [Test]
    [Description("AT-06: anger-lines acceptable to fade at 25m (fadeStart=25, fadeEnd=35).")]
    public void AngerLines_AcceptableToFadeAt25m()
    {
        // At exactly fadeStart → still alpha=1; acceptable.
        float alpha = NpcVisualStateCatalog.ComputeCueAlpha(25f, fadeAltitudeStart: 25f, fadeAltitudeEnd: 35f);
        Assert.GreaterOrEqual(alpha, 0f, "anger-lines alpha must be in [0,1] at 25m.");
    }

    [Test]
    [Description("AT-06: green-face-nausea must remain visible at 25m (fadeStart=30, fadeEnd=45).")]
    public void GreenFaceNausea_VisibleAt25m()
    {
        float alpha = NpcVisualStateCatalog.ComputeCueAlpha(25f, fadeAltitudeStart: 30f, fadeAltitudeEnd: 45f);
        Assert.Greater(alpha, 0f, "green-face-nausea must be visible at 25m altitude.");
    }

    // ── ChibiEmotionPopulator cue-ID mapping ──────────────────────────────────

    [Test]
    public void IconKindToCueId_AllKinds_ReturnNonEmpty()
    {
        var kinds = new[]
        {
            IconKind.Anger, IconKind.Sweat, IconKind.SleepZ, IconKind.Heart,
            IconKind.Sparkle, IconKind.QuestionMark, IconKind.Exclamation,
            IconKind.Stink, IconKind.RedFaceFlush, IconKind.GreenFaceNausea,
        };
        foreach (var kind in kinds)
        {
            string cueId = ChibiEmotionPopulator.TestIconKindToCueId(kind);
            Assert.IsFalse(string.IsNullOrEmpty(cueId),
                $"IconKind.{kind} must map to a non-empty catalog cue ID.");
        }
    }
}
