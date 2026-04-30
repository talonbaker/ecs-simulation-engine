using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-08: Camera rotate. Q/E input rotates camera around focus point (lazy-susan).
/// 360 degrees accessible; no axis flip.
/// </summary>
[TestFixture]
public class CameraControllerRotateTests
{
    private GameObject       _cameraGo;
    private CameraController _controller;

    [SetUp]
    public void SetUp()
    {
        _cameraGo   = new GameObject("TestCamera_Rotate");
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
    public IEnumerator Rotate_PositiveInput_IncreasesYaw()
    {
        yield return null;

        float yawBefore = _controller.Yaw;
        _controller.InjectRotate(1f, 0.1f);

        Assert.Greater(_controller.Yaw, yawBefore,
            "Positive rotate input should increase yaw (clockwise viewed from above).");
    }

    [UnityTest]
    public IEnumerator Rotate_NegativeInput_DecreasesYaw()
    {
        yield return null;

        // Start at 90° so we don't wrap to 360.
        _controller.InjectRotate(1f, 1f / 90f * 1f);   // get to ~1°
        float yawBefore = _controller.Yaw;
        _controller.InjectRotate(-1f, 0.1f);

        // Yaw wraps at 0/360 — check the absolute change is downward before wrapping.
        float change = yawBefore - _controller.Yaw;
        // Allow for wrap: change could be -359 if it wrapped. Check magnitude.
        bool decreased = change > 0f || change < -350f;
        Assert.IsTrue(decreased,
            $"Negative rotate input should decrease yaw. Before={yawBefore:F1} After={_controller.Yaw:F1}");
    }

    [UnityTest]
    public IEnumerator Rotate_FullCircle_ReturnsToStartAngle()
    {
        yield return null;

        float startYaw = _controller.Yaw;

        // Rotate 360 degrees — enough rotation at the default 90°/s rate over 4 seconds.
        // We inject 1 second worth of rotation 4 times.
        for (int i = 0; i < 4; i++)
            _controller.InjectRotate(1f, 1f);   // 90° per second × 1 second = 90° each

        // After 360° total, yaw should be back to (approximately) the start.
        float finalYaw = _controller.Yaw;
        Assert.AreEqual(startYaw, finalYaw, 1f,
            $"After 360° rotation, yaw should return to start. Expected ~{startYaw:F1}°, got {finalYaw:F1}°.");
    }

    [UnityTest]
    public IEnumerator Rotate_NoAxisFlip_PitchUnchanged()
    {
        yield return null;

        // Rotate 720° (two full circles) and verify the camera pitch hasn't flipped.
        Quaternion rotBefore = _cameraGo.transform.rotation;
        float pitchBefore = rotBefore.eulerAngles.x;

        for (int i = 0; i < 8; i++)
            _controller.InjectRotate(1f, 1f);

        float pitchAfter = _cameraGo.transform.rotation.eulerAngles.x;
        Assert.AreEqual(pitchBefore, pitchAfter, 1f,
            "Camera pitch (X euler angle) must not change during lazy-susan rotation.");
    }
}
