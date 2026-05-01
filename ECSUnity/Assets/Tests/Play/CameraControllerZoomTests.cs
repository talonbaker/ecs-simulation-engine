using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-09: Camera zoom. Scroll/+- changes altitude within [minAltitude, maxAltitude].
/// Cannot exceed bounds.
/// </summary>
[TestFixture]
public class CameraControllerZoomTests
{
    private GameObject       _cameraGo;
    private CameraController _controller;

    [SetUp]
    public void SetUp()
    {
        _cameraGo   = new GameObject("TestCamera_Zoom");
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
    public IEnumerator ZoomIn_PositiveInput_DecreasesAltitude()
    {
        yield return null;

        float altBefore = _controller.Altitude;
        _controller.InjectZoom(1f, 0.1f);   // positive = zoom in = lower altitude

        Assert.Less(_controller.Altitude, altBefore,
            "Positive zoom input should decrease altitude (zoom in = descend).");
    }

    [UnityTest]
    public IEnumerator ZoomOut_NegativeInput_IncreasesAltitude()
    {
        yield return null;

        float altBefore = _controller.Altitude;
        _controller.InjectZoom(-1f, 0.1f);

        Assert.Greater(_controller.Altitude, altBefore,
            "Negative zoom input should increase altitude (zoom out = ascend).");
    }

    [UnityTest]
    public IEnumerator Zoom_CannotExceedMaxAltitude()
    {
        yield return null;

        // Drive altitude far beyond max with a large zoom-out input.
        _controller.InjectZoom(-100f, 10f);   // massively outward

        Assert.LessOrEqual(_controller.Altitude, _controller.Constraints.MaxAltitude,
            $"Altitude {_controller.Altitude:F3} should not exceed MaxAltitude {_controller.Constraints.MaxAltitude:F3}.");
    }

    [UnityTest]
    public IEnumerator Zoom_CannotDropBelowMinAltitude()
    {
        yield return null;

        // Drive altitude far below min with a large zoom-in input.
        _controller.InjectZoom(100f, 10f);

        Assert.GreaterOrEqual(_controller.Altitude, _controller.Constraints.MinAltitude,
            $"Altitude {_controller.Altitude:F3} should not drop below MinAltitude {_controller.Constraints.MinAltitude:F3}.");
    }

    [UnityTest]
    public IEnumerator Zoom_BoundsAreRespected_AtDefaultValues()
    {
        yield return null;

        float min = _controller.Constraints.MinAltitude;
        float max = _controller.Constraints.MaxAltitude;

        Assert.Less(min, max,
            "MinAltitude must be less than MaxAltitude for zoom to work correctly.");
        Assert.GreaterOrEqual(_controller.Altitude, min,
            "Default altitude should be >= MinAltitude.");
        Assert.LessOrEqual(_controller.Altitude, max,
            "Default altitude should be <= MaxAltitude.");
    }
}
