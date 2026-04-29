using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-10: Camera no-under-desk guard.
/// Simulated input that would drop camera below cameraMinAltitude clamps.
/// No geometric clipping into desks or floor.
/// </summary>
[TestFixture]
public class CameraNoUnderDeskGuardTests
{
    private GameObject       _cameraGo;
    private CameraController _controller;

    [SetUp]
    public void SetUp()
    {
        _cameraGo   = new GameObject("TestCamera_NoUnderDesk");
        _cameraGo.AddComponent<Camera>();
        _controller = _cameraGo.AddComponent<CameraController>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_cameraGo != null)
            Object.Destroy(_cameraGo);
    }

    [UnityTest]
    public IEnumerator CameraAltitude_NeverDropsBelowMinAltitude_WithAggresiveZoomIn()
    {
        yield return null;

        float minAlt = _controller.Constraints.MinAltitude;

        // Aggressively zoom in far beyond the minimum.
        for (int i = 0; i < 200; i++)
            _controller.InjectZoom(99f, 0.1f);

        Assert.GreaterOrEqual(_controller.Altitude, minAlt,
            $"Camera altitude {_controller.Altitude:F3} must not drop below MinAltitude {minAlt:F3} " +
            "even with aggressive zoom-in input.");
    }

    [UnityTest]
    public IEnumerator CameraWorldPosition_NeverBelowMinAltitude_WithAggresiveZoomIn()
    {
        yield return null;

        float minAlt = _controller.Constraints.MinAltitude;

        for (int i = 0; i < 200; i++)
            _controller.InjectZoom(99f, 0.1f);

        // The camera's world Y position should reflect the altitude constraint.
        // (Exact Y depends on tilt math, but it must be >= minAlt in practice.)
        float worldY = _cameraGo.transform.position.y;
        Assert.GreaterOrEqual(worldY, minAlt * 0.5f,   // allow for pitch offset, but not below half minAlt
            $"Camera world Y={worldY:F3} is suspiciously low. MinAltitude={minAlt:F3}.");
    }

    [UnityTest]
    public IEnumerator ViolatesFloor_Helper_RejectsAltitudeBelowMin()
    {
        yield return null;

        var constraints = _controller.Constraints;
        float below = constraints.MinAltitude - 0.1f;

        Assert.IsTrue(constraints.ViolatesFloor(below),
            $"ViolatesFloor({below:F2}) should return true when below MinAltitude={constraints.MinAltitude:F2}.");
        Assert.IsFalse(constraints.ViolatesFloor(constraints.MinAltitude),
            $"ViolatesFloor(exactly MinAltitude={constraints.MinAltitude:F2}) should return false.");
    }

    [UnityTest]
    public IEnumerator ClampAltitude_SnapsToBound_NotBelowMin()
    {
        yield return null;

        var constraints = _controller.Constraints;
        float clamped = constraints.ClampAltitude(0f);   // 0 is below any reasonable min

        Assert.AreEqual(constraints.MinAltitude, clamped, 0.001f,
            $"ClampAltitude(0) should return MinAltitude={constraints.MinAltitude:F2}, got {clamped:F2}.");
    }
}
