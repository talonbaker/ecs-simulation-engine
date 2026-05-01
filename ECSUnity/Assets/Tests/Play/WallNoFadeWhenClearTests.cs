using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-08: No wall between camera and focus → all walls remain at _Alpha = 1.0.
///
/// Verifies that WallFadeController does NOT fade walls when the camera-to-focus
/// raycast is unobstructed (no WallTag colliders in the path).
/// </summary>
[TestFixture]
public class WallNoFadeWhenClearTests
{
    private GameObject         _cameraGo;
    private CameraController   _cameraCtrl;
    private WallFadeController _wallFadeCtrl;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _cameraGo = new GameObject("WallClear_Camera");
        _cameraGo.AddComponent<Camera>();
        _cameraCtrl = _cameraGo.AddComponent<CameraController>();

        var wfcGo = new GameObject("WallClear_WFC");
        _wallFadeCtrl = wfcGo.AddComponent<WallFadeController>();
        SetField(_wallFadeCtrl, "_cameraController", _cameraCtrl);

        yield return null;   // Start()
    }

    [TearDown]
    public void TearDown()
    {
        DestroyAll("WallClear_");
    }

    // ── AT-08a: No wall in path → all walls at full alpha ─────────────────────

    [UnityTest]
    public IEnumerator ClearPath_AllWalls_RemainAtFullAlpha()
    {
        // Place wall tags far from the raycast path.
        // Camera focus is at (30, 0, 20) by default.
        // Camera is at approximately (30, 3.5, 8).
        // Place walls far away at (0, 0, 0) — clearly not in the raycast path.

        for (int i = 0; i < 3; i++)
        {
            var go  = new GameObject($"WallClear_Wall_{i}");
            go.transform.position = new Vector3(-50f + i * 5f, 1.25f, -50f);

            var col  = go.AddComponent<BoxCollider>();
            col.size = new Vector3(5f, 2.5f, 0.15f);

            var mat        = new Material(Shader.Find("ECSUnity/RoomTint") ?? Shader.Find("Unlit/Color"));
            var tag        = go.AddComponent<WallTag>();
            tag.RoomId     = "clear-room";
            tag.FaceMaterial = mat;
            tag.TargetAlpha  = 1f;
            tag.CurrentAlpha = 1f;
        }

        _wallFadeCtrl.InvalidateWallCache();

        // Let WallFadeController run for 3 frames.
        yield return null;
        yield return null;
        yield return null;

        var walls = Object.FindObjectsOfType<WallTag>();
        if (walls.Length == 0)
        {
            Assert.Inconclusive("No WallTag objects found.");
            yield break;
        }

        foreach (var w in walls)
        {
            // Target should be full alpha (no occlusion).
            Assert.AreEqual(1f, w.TargetAlpha, 0.01f,
                $"Wall '{w.name}' TargetAlpha should be 1.0 when ray is clear.");
            // Current should converge toward 1.0.
            Assert.Greater(w.CurrentAlpha, 0.9f,
                $"Wall '{w.name}' CurrentAlpha {w.CurrentAlpha:F3} should be near 1.0.");
        }
    }

    // ── AT-08b: ForceAlphaAll → then clear path → walls lerp back to 1.0 ─────

    [UnityTest]
    public IEnumerator AfterForceAlpha_ClearPath_WallsLerpBackToFull()
    {
        // Create a wall not in the ray path.
        var go  = new GameObject("WallClear_LerpBack");
        go.transform.position = new Vector3(-100f, 1.25f, -100f);

        var col  = go.AddComponent<BoxCollider>();
        col.size = new Vector3(5f, 2.5f, 0.15f);

        var mat      = new Material(Shader.Find("ECSUnity/RoomTint") ?? Shader.Find("Unlit/Color"));
        var tag      = go.AddComponent<WallTag>();
        tag.RoomId   = "lerp-room";
        tag.FaceMaterial = mat;

        _wallFadeCtrl.InvalidateWallCache();
        yield return null;

        // Force fade.
        _wallFadeCtrl.ForceAlphaAll(0.2f);
        yield return null;

        float fadedAlpha = tag.CurrentAlpha;
        Assert.AreEqual(0.2f, fadedAlpha, 0.01f, "After ForceAlphaAll, wall should be at 0.2.");

        // Now let the controller run — target should become 1.0 (no occlusion).
        // wallFadeSeconds = 0.18 s → need ~0.25 s to converge.
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Assert.Greater(tag.CurrentAlpha, 0.9f,
            $"Wall CurrentAlpha {tag.CurrentAlpha:F3} should be near 1.0 after lerp-back.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetField(object target, string field, object value)
    {
        var f = target.GetType().GetField(field,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f?.SetValue(target, value);
    }

    private static void DestroyAll(string prefix)
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith(prefix))
                Object.Destroy(go);
    }
}
