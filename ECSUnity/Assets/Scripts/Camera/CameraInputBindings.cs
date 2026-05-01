using UnityEngine;

/// <summary>
/// All camera input bindings in one place.
///
/// Centralising bindings here means future remapping (via a settings screen or
/// Input System asset) only requires changes in this class. No hard-coded
/// KeyCode or axis strings live in CameraController.
///
/// BINDING TABLE (UX bible §2.1)
/// ──────────────────────────────
/// Pan       — WASD / Arrow keys / Middle-click drag / Left gamepad stick
/// Rotate    — Q / E / Right-click drag / Right gamepad stick X-axis
/// Zoom      — Scroll wheel / + − keys / Gamepad triggers (LT / RT)
/// Recenter  — F / Double-click / Gamepad A
///
/// v0.1: keyboard + mouse only. Gamepad bindings are stubs (always return 0)
/// until Input System integration is added in a future packet.
/// </summary>
public sealed class CameraInputBindings
{
    // Raw Mouse X/Y axis gives ~0.05 per pixel. This multiplier scales it up
    // so middle-click drag feels roughly as fast as keyboard pan at speed 1.
    private const float MouseDragSensitivity = 5f;

    // ── Pan ───────────────────────────────────────────────────────────────────

    /// <summary>Returns a [−1, +1] pan X axis from keyboard/mouse input this frame.</summary>
    public float PanX()
    {
        float value = 0f;

        // WASD
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  value -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) value += 1f;

        // Middle-mouse drag (horizontal). Negated: drag right → view moves right (grab-style).
        if (Input.GetMouseButton(2))
            value -= Input.GetAxis("Mouse X") * MouseDragSensitivity;

        return Mathf.Clamp(value, -1f, 1f);
    }

    /// <summary>Returns a [−1, +1] pan Z axis from keyboard/mouse input this frame.</summary>
    public float PanZ()
    {
        float value = 0f;

        // WASD — W/S map to forward/back in camera-local XZ space.
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))   value += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) value -= 1f;

        // Middle-mouse drag (vertical). Negated: drag down → view moves down (grab-style).
        if (Input.GetMouseButton(2))
            value -= Input.GetAxis("Mouse Y") * MouseDragSensitivity;

        return Mathf.Clamp(value, -1f, 1f);
    }

    // ── Rotate (lazy-susan, Y-axis only) ─────────────────────────────────────

    /// <summary>Returns a [−1, +1] rotate input (positive = clockwise when viewed from above).</summary>
    public float Rotate()
    {
        float value = 0f;

        if (Input.GetKey(KeyCode.E)) value += 1f;
        if (Input.GetKey(KeyCode.Q)) value -= 1f;

        // Right-click drag horizontal
        if (Input.GetMouseButton(1))
            value += Input.GetAxis("Mouse X") * 0.5f;

        return Mathf.Clamp(value, -1f, 1f);
    }

    // ── Zoom ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a zoom delta for this frame.
    /// Positive = zoom in (lower altitude), negative = zoom out (higher altitude).
    /// Already scaled so one scroll tick = a comfortable step.
    /// </summary>
    public float Zoom()
    {
        float value = 0f;

        // Scroll wheel — Unity's "Mouse ScrollWheel" axis gives ±0.1 per notch.
        // Multiply to get a usable step size.
        value += Input.GetAxis("Mouse ScrollWheel") * 10f;

        // Keyboard
        if (Input.GetKey(KeyCode.KeypadPlus)  || Input.GetKey(KeyCode.Equals)) value += 1f;
        if (Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.Minus))  value -= 1f;

        return value;
    }

    // ── Recenter ──────────────────────────────────────────────────────────────

    /// <summary>Returns true on the frame the user presses the recenter key.</summary>
    public bool RecenterPressed() => Input.GetKeyDown(KeyCode.F);
}
