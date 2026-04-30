using UnityEngine;

/// <summary>
/// Free-fly camera. No focus point, no altitude constraint.
///
/// CONTROLS
/// ─────────
/// Right-click drag  — mouse look (yaw + pitch)
/// WASD / arrows     — fly forward/back/strafe
/// E / Q             — fly up / down
/// Scroll wheel      — fly forward (discrete jump per notch)
/// </summary>
[RequireComponent(typeof(Camera))]
public sealed class CameraController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed   = 15f;
    [SerializeField] private float _scrollSpeed = 25f;

    [Header("Look")]
    [SerializeField] private float _lookSensitivity = 3f;

    private float _yaw;
    private float _pitch;

    private void Awake()
    {
        var e = transform.eulerAngles;
        _yaw   = e.y;
        _pitch = e.x > 180f ? e.x - 360f : e.x;
    }

    private void Update()
    {
        HandleLook();
        HandleMove();
    }

    private void HandleLook()
    {
        if (!Input.GetMouseButton(1)) return;

        _yaw   += Input.GetAxis("Mouse X") * _lookSensitivity;
        _pitch -= Input.GetAxis("Mouse Y") * _lookSensitivity;
        _pitch  = Mathf.Clamp(_pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private void HandleMove()
    {
        float dt = Time.deltaTime;

        Vector3 dir = Vector3.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    dir += transform.forward;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  dir -= transform.forward;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dir += transform.right;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  dir -= transform.right;
        if (Input.GetKey(KeyCode.E))                                      dir += Vector3.up;
        if (Input.GetKey(KeyCode.Q))                                      dir -= Vector3.up;

        if (dir.sqrMagnitude > 0f)
            transform.position += dir.normalized * (_moveSpeed * dt);

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (!Mathf.Approximately(scroll, 0f))
            transform.position += transform.forward * (scroll * _scrollSpeed);
    }

    // ── Test / diagnostic accessors ───────────────────────────────────────────

    public Vector3 Position => transform.position;
    public float   Yaw      => _yaw;
    public float   Pitch    => _pitch;
}
