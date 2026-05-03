using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Spawns a soft multiply-blend disc shadow below this object, driven by a
/// custom light direction rather than Unity's shadow system.
///
/// The disc renders in the Transparent queue before the PixelArtRendererFeature
/// post-process runs. The Bayer dithering automatically converts the smooth
/// gradient into a dithered pattern that matches the ball shading style.
///
/// Call flow: OnEnable creates disc → LateUpdate repositions it each frame →
/// OnDisable destroys it (so no shadows leak into other scenes).
/// </summary>
[AddComponentMenu("Sandbox/BlobShadowCaster")]
public class BlobShadowCaster : MonoBehaviour
{
    [SerializeField, Tooltip("BlobShadow.mat. Drag from Assets/_Sandbox/.")]
    Material _shadowMaterial;

    [SerializeField, Tooltip("Direction light rays travel (from light toward scene).")]
    Vector3 _lightDirection = new Vector3(-0.37f, -0.66f, 0.65f);

    [SerializeField, Range(0.1f, 5f), Tooltip("Base radius of the shadow disc (world units).")]
    float _radius = 1.5f;

    [SerializeField, Range(0f, 1f), Tooltip("Darkness at the shadow centre.")]
    float _opacity = 0.65f;

    static readonly int s_OpacityId = Shader.PropertyToID("_Opacity");

    GameObject            _disc;
    MeshRenderer          _mr;
    MaterialPropertyBlock _mpb;

    void OnEnable()
    {
        _disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _disc.name = "_BlobShadow_" + gameObject.name;

        if (_disc.TryGetComponent<Collider>(out var col)) Destroy(col);

        _mr = _disc.GetComponent<MeshRenderer>();
        _mr.sharedMaterial    = _shadowMaterial;
        _mr.shadowCastingMode = ShadowCastingMode.Off;
        _mr.receiveShadows    = false;
        _mr.lightProbeUsage   = LightProbeUsage.Off;

        _mpb = new MaterialPropertyBlock();
        UpdateDisc();
    }

    void LateUpdate() => UpdateDisc();

    void OnDisable()
    {
        if (_disc != null) Destroy(_disc);
        _disc = null;
    }

    void UpdateDisc()
    {
        if (_disc == null || _shadowMaterial == null) return;

        Vector3 pos = transform.position;
        Vector3 dir = _lightDirection.normalized;
        if (dir.y >= -0.0001f) return; // light must have a downward component

        // Project the object centre along the light direction to the ground plane (y = 0).
        float   t            = -pos.y / dir.y;
        Vector3 groundCenter = pos + dir * t;
        _disc.transform.position = groundCenter + Vector3.up * 0.005f;

        // Scale: grows slightly as the object gets higher; stretch in light direction.
        float heightScale = 1f + pos.y * 0.15f;
        float xStretch    = 1f + Mathf.Abs(dir.x / dir.y) * 0.6f;
        float zStretch    = 1f + Mathf.Abs(dir.z / dir.y) * 0.6f;
        _disc.transform.localScale = new Vector3(
            _radius * heightScale * xStretch,
            _radius * heightScale * zStretch, 1f);

        // Lie flat on the ground, yaw-aligned to the shadow cast direction.
        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        _disc.transform.rotation = Quaternion.Euler(90f, yaw, 0f);

        _mpb.SetFloat(s_OpacityId, _opacity);
        _mr.SetPropertyBlock(_mpb);
    }
}
