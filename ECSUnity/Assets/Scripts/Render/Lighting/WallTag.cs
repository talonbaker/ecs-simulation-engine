using UnityEngine;

/// <summary>
/// Marker component attached to every wall quad created by <see cref="RoomRectangleRenderer"/>.
///
/// PURPOSE
/// ────────
/// Unity's Physics raycast returns a <see cref="Collider"/> reference. From there we need
/// to reach the wall's <see cref="Material"/> so <see cref="WallFadeController"/> can set
/// <c>_Alpha</c> without a slow <c>GetComponent&lt;Renderer&gt;()</c> call per hit.
/// WallTag caches the renderer reference at creation time and exposes it directly.
///
/// DESIGN
/// ───────
/// Using a MonoBehaviour marker (rather than a Unity Tag string or Layer int) avoids
/// the requirement to pre-configure Tags/Layers in the Unity project — those settings
/// live in ProjectSettings and cannot be set purely from code at runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class WallTag : MonoBehaviour
{
    // ── Set by RoomRectangleRenderer at creation time ─────────────────────────

    /// <summary>
    /// The <see cref="Material"/> instance on this wall face.
    /// Pre-fetched at creation so WallFadeController skips GetComponent calls.
    /// </summary>
    public Material FaceMaterial { get; set; }

    /// <summary>
    /// The room Id this wall belongs to. Used for per-room occlusion diagnostics.
    /// </summary>
    public string RoomId { get; set; }

    /// <summary>
    /// Current lerp target alpha for this wall.
    /// Written by WallFadeController; read during lerp interpolation.
    /// </summary>
    [System.NonSerialized]
    public float TargetAlpha = 1f;

    /// <summary>
    /// Current live alpha applied to the material each frame.
    /// Lerps toward <see cref="TargetAlpha"/> at <see cref="LightingConfig.wallFadeSeconds"/> rate.
    /// </summary>
    [System.NonSerialized]
    public float CurrentAlpha = 1f;
}
