using System;
using UnityEngine;

public sealed class PopupClickHandler : MonoBehaviour
{
    public event Action<InspectorTarget> OnClick;

    private bool _active;

    public void Activate()   => _active = true;
    public void Deactivate() => _active = false;

    private void Update()
    {
        if (!_active) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            var target = hit.collider.GetComponentInParent<InspectorTarget>();
            if (target != null)
            {
                OnClick?.Invoke(target);
                return;
            }
        }

        OnClick?.Invoke(null);
    }
}
