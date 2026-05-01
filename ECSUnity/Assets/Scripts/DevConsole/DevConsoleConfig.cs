using UnityEngine;

/// <summary>
/// Configuration ScriptableObject for the developer console — WP-3.1.H.
///
/// NOT gated behind #if WARDEN so the Inspector reference compiles in RETAIL
/// builds even though the console itself is stripped.
///
/// Create via Assets > Create > Warden > DevConsoleConfig.
/// </summary>
[CreateAssetMenu(
    menuName = "Warden/DevConsoleConfig",
    fileName = "DefaultDevConsoleConfig")]
public sealed class DevConsoleConfig : ScriptableObject
{
    [Tooltip("Key that toggles the dev console open/closed. Default: BackQuote (~ / `).")]
    public KeyCode ToggleKey = KeyCode.BackQuote;

    [Tooltip("Maximum lines retained in the visible history buffer.")]
    [Range(50, 1000)]
    public int MaxHistoryLines = 200;

    [Tooltip("Maximum commands stored in the navigation history (up/down arrows).")]
    [Range(10, 500)]
    public int MaxCommandHistory = 100;

    [Tooltip("Path for persisted command history. Relative to Application.dataPath parent.")]
    public string HistoryFilePath = "Logs/console-history.txt";

    [Tooltip("Persist the last N commands to disk so history survives between sessions.")]
    public bool PersistHistory = true;

    [Tooltip("Panel height as a fraction of screen height (0 = bottom, 0.5 = half-screen).")]
    [Range(0.2f, 0.8f)]
    public float PanelHeightFraction = 0.45f;
}
