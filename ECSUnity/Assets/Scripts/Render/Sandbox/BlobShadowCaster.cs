using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Spawns a contact-aware blob shadow below this object.
///
/// The disc's front edge (UV.y = 0) is placed at the ball's base contact point
/// (directly under the sphere centre). The shadow then extends along the flat
/// component of _lightDirection, getting progressively lighter as it goes. The
/// BlobShadow shader's Falloff curve handles the dark-to-light gradient; the
/// Bayer dithering post-process converts the smooth gradient into dots.
/// </summary>
[AddComponentMenu("Sandbox/BlobShadowCaster")]
public class BlobShadowCaster : MonoBehaviour
{
    [SerializeField, Tooltip("BlobShadow.mat — drag from Assets/_Sandbox/.")]
    Material _shadowMaterial;

    [SerializeField, Tooltip("Direction light rays travel (from light source toward scene).")]
    Vector3 _lightDirection = new Vector3(-0.37f, -0.66f, 0.65f);

    [SerializeField, Range(0.1f, 3f),  Tooltip("Half-width of the shadow disc in world units.")]
    float _radius = 1.2f;

    [SerializeField, Range(0.5f, 6f),  Tooltip("Multiplier on computed shadow reach length.")]
    float _lengthScale = 2.5f;

    [SerializeField, Range(0f, 1f),    Tooltip("Maximum darkness at the contact point.")]
    float _opacity = 0.75f;

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
        if (dir.y >= -0.0001f) return;

        // Contact point: ball's base projected straight down to the ground plane.
        Vector3 contactPoint = new Vector3(pos.x, 0f, pos.z);

        // Flat (horizontal) component of light direction — the ground shadow direction.
        Vector3 flatDir  = new Vector3(dir.x, 0f, dir.z);
        float   flatMag  = flatDir.magnitude;
        Vector3 flatNorm = flatMag > 0.0001f ? flatDir / flatMag : Vector3.forward;

        // How far the shadow reaches along the ground from the contact point.
        float shadowLength = pos.y * (flatMag / Mathf.Abs(dir.y)) * _lengthScale + _radius;

        // Place disc so its UV.y=0 edge coincides with contactPoint.
        // Disc centre is half-way along the shadow tail.
        Vector3 discCenter = contactPoint + flatNorm * (shadowLength * 0.5f);
        _disc.transform.position  = discCenter + Vector3.up * 0.005f;
        _disc.transform.localScale = new Vector3(_radius * 2f, shadowLength, 1f);

        // Lie flat; local +Y (UV.y=1 side) points away in the shadow direction.
        float yaw = Mathf.Atan2(flatNorm.x, flatNorm.z) * Mathf.Rad2Deg;
        _disc.transform.rotation = Quaternion.Euler(90f, yaw, 0f);

        _mpb.SetFloat(s_OpacityId, _opacity);
        _mr.SetPropertyBlock(_mpb);
    }
}
