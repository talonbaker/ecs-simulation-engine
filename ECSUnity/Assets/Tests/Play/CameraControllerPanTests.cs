using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-07: Camera pan. Simulated WASD/arrow input moves camera linearly.
/// Left arrow moves camera left; up arrow moves forward (relative to camera angle).
/// </summary>
[TestFixture]
public class CameraControllerPanTests
{
    private GameObject       _cameraGo;
    private CameraController _controller;

    [SetUp]
    public void SetUp()
    {
        _cameraGo   = new GameObject("TestCamera");
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
    public IEnumerator Pan_Left_MovesFocusPointLeftInWorldSpace()
    {
        yield return null;   // Awake/Start

        Vector3 focusBefore = _controller.FocusPoint;
        float dt = 0.1f;

        // Inject pan left (x = -1, z = 0)
        _controller.InjectPan(-1f, 0f, dt);

        Vector3 focusAfter = _controller.FocusPoint;
        Assert.Less(focusAfter.x, focusBefore.x,
            "Panning left should decrease the focus point X when yaw == 0.");
    }

    [UnityTest]
    public IEnumerator Pan_Forward_MovesFocusPointForwardInWorldSpace()
    {
        yield return null;

        Vector3 focusBefore = _controller.FocusPoint;
        _controller.InjectPan(0f, 1f, 0.1f);

        Vector3 focusAfter = _controller.FocusPoint;
        // At yaw == 0, forward pan moves along +X (sin(0)=0, cos(0)=1, forward = (1,0,0)).
        Assert.Greater(focusAfter.x, focusBefore.x,
            "Panning forward at yaw=0 should increase focus point X.");
    }

    [UnityTest]
    public IEnumerator Pan_Right_MovesFocusPointRight()
    {
        yield return null;

        Vector3 focusBefore = _controller.FocusPoint;
        _controller.InjectPan(1f, 0f, 0.1f);

        Assert.Greater(_controller.FocusPoint.x, focusBefore.x,
            "Panning right should increase focus point X at yaw=0.");
    }

    [UnityTest]
    public IEnumerator Pan_IsLinear_ProportionalToInput()
    {
        yield return null;

        // Two inputs of magnitude 1.0 and 2.0 should produce displacement in ratio 1:2.
        Vector3 start = _controller.FocusPoint;
        _controller.InjectPan(1f, 0f, 0.1f);
        float delta1 = Mathf.Abs(_controller.FocusPoint.x - start.x);

        _controller.InjectPan(-1f, 0f, 0.1f);   // undo
        _controller.InjectPan(1f, 0f, 0.2f);    // double the time step
        float delta2 = Mathf.Abs(_controller.FocusPoint.x - start.x);

        Assert.AreEqual(delta1 * 2f, delta2, 0.001f,
            "Pan displacement should be proportional to dt (linear relationship).");
    }
}
