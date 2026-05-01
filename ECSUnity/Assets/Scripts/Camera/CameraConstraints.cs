using UnityEngine;

/// <summary>
/// Pure-value camera constraint parameters with enforcement helpers.
///
/// This class holds no Unity state — it is a plain data object that
/// <see cref="CameraController"/> reads on every frame. Constraints are enforced
/// by clamping the camera transform after any input is applied.
///
/// CONSTRAINT SUMMARY (UX bible §2.1)
/// ────────────────────────────────────
/// • Altitude: Y position clamped to [MinAltitude, MaxAltitude].
///   Default: 3–5 world-units above the floor (just-above-cube-top to just-under-ceiling).
/// • Pitch: fixed at CameraPitchAngle (default 50°). No free look in default mode.
/// • Zoom: implemented as altitude change, so the altitude clamp doubles as a zoom clamp.
/// • No-under-desk guard: hard floor at MinAltitude so the camera cannot clip into desks.
///   There is no "soft" approach to the limit — it snaps to the boundary.
///
/// CREATIVE-MODE (future packet)
/// ──────────────────────────────
/// When creative mode is enabled (UX bible §5.2), CameraController will bypass these
/// constraints to allow full 3D free-look. The toggle is not wired in this packet.
/// </summary>
public sealed class CameraConstraints
{
    // ── Altitude (Y-axis in world space) ──────────────────────────────────────

    /// <summary>
    /// Minimum camera Y. Camera cannot descend below this value.
    /// Typically set to just above the tallest furniture (cube-top).
    /// </summary>
    public float MinAltitude { get; set; } = 3f;

    /// <summary>
    /// Maximum camera Y. Camera cannot rise above this value.
    /// Typically set to just below the ceiling plane.
    /// </summary>
    public float MaxAltitude { get; set; } = 5f;

    // ── Pitch ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fixed pitch angle (degrees from horizontal). 0 = side-on, 90 = straight down.
    /// Default 50° gives a comfortable top-down-ish isometric feel.
    /// Applied as a constant by CameraController; the user cannot change it in default mode.
    /// </summary>
    public float PitchAngle { get; set; } = 50f;

    // ── Enforcement helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Clamps a proposed Y altitude to [MinAltitude, MaxAltitude].
    /// </summary>
    public float ClampAltitude(float y) => Mathf.Clamp(y, MinAltitude, MaxAltitude);

    /// <summary>
    /// Returns true when the proposed altitude would violate the no-under-desk guard.
    /// </summary>
    public bool ViolatesFloor(float y) => y < MinAltitude;

    /// <summary>
    /// Returns true when the proposed altitude would violate the ceiling guard.
    /// </summary>
    public bool ViolatesCeiling(float y) => y > MaxAltitude;

    /// <summary>
    /// Builds and returns a Quaternion representing the fixed pitch with the given
    /// yaw (lazy-susan rotation angle). Called by CameraController each frame to
    /// enforce a constant look angle.
    /// </summary>
    public Quaternion BuildRotation(float yawDegrees)
    {
        // Yaw around world Y, then tilt down by pitch.
        return Quaternion.Euler(PitchAngle, yawDegrees, 0f);
    }
}
