#if WARDEN
using UnityEngine;

/// <summary>
/// WARDEN-only F2 keybinding that cycles the NpcIntrospectionOverlay through
/// Off → Selected → All → Off.
///
/// Handles input via both Unity's legacy Input Manager (Update) and IMGUI
/// (OnGUI), mirroring the pattern in DevConsolePanel to work regardless of
/// which Input System package is active. A frame guard prevents double-fire
/// when both paths see the same keypress.
///
/// Does not steal F2 while the dev console is open.
/// </summary>
public sealed class NpcIntrospectionToggle : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The overlay this toggle drives. Auto-found if null.")]
    private NpcIntrospectionOverlay _overlay;

    private int _lastF2Frame = -1;

    private void Start()
    {
        if (_overlay == null)
            _overlay = FindObjectOfType<NpcIntrospectionOverlay>();
    }

    private void Update()
    {
        if (DevConsolePanel.AnyVisible) return;

        try
        {
            if (Input.GetKeyDown(KeyCode.F2))
                TryCycle("Update");
        }
        catch (System.Exception)
        {
            // Legacy Input disabled — OnGUI handles F2.
        }
    }

    private void OnGUI()
    {
        if (Event.current == null) return;
        if (Event.current.type != EventType.KeyDown) return;
        if (Event.current.keyCode != KeyCode.F2) return;
        if (DevConsolePanel.AnyVisible) return;

        TryCycle("OnGUI");
        Event.current.Use();
    }

    private void TryCycle(string source)
    {
        if (Time.frameCount == _lastF2Frame) return;
        _lastF2Frame = Time.frameCount;
        _overlay?.CycleMode();
    }

    // ── Test accessor ──────────────────────────────────────────────────────────

    /// <summary>Programmatically fire a toggle (used by play-mode tests).</summary>
    public void SimulateF2() => _overlay?.CycleMode();
}
#endif
