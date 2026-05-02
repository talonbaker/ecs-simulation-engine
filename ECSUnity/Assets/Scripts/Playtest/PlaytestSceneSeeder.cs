using System.Reflection;
using UnityEngine;

/// <summary>
/// Seeds PlaytestScene at boot: configures world path, NPC count, archetype balance,
/// initial stains and breakable props, and start time for the Playtest Program substrate.
///
/// Awake() overrides EngineHost._worldDefinitionPath before EngineHost.Start() fires,
/// so this component is the authoritative path source for PlaytestScene.
/// </summary>
[RequireComponent(typeof(EngineHost))]
public sealed class PlaytestSceneSeeder : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("How many NPCs to spawn at scene boot. 15 is the bibles' default office; up to 30 for FPS gate verification.")]
    [Range(1, 30)]
    private int _npcCount = 15;

    [SerializeField]
    [Tooltip("StreamingAssets-relative path to the world-definition JSON for this scene.")]
    private string _worldDefinitionPath = "playtest-office.json";

    [SerializeField]
    [Tooltip("EvenAcrossAll = 1+ of each archetype filling up to npcCount; CustomFromJson = read distribution from world-definition.")]
    private ArchetypeBalanceMode _archetypeBalanceMode = ArchetypeBalanceMode.EvenAcrossAll;

    [SerializeField]
    [Tooltip("Initial slip-hazard stains placed at boot. Scenario verbs (WP-PT.1) let Talon seed more during play.")]
    [Range(0, 20)]
    private int _seedStainsAtBoot = 3;

    [SerializeField]
    [Tooltip("Initial BreakableComponent props placed on desks at boot.")]
    [Range(0, 30)]
    private int _seedBreakablesAtBoot = 6;

    [SerializeField]
    [Tooltip("Sim wall-clock time at scene boot. Default = 8:30 AM (workers arriving).")]
    private string _startWallTimeHHMM = "08:30";

    // ── Enums ─────────────────────────────────────────────────────────────────

    public enum ArchetypeBalanceMode
    {
        EvenAcrossAll,
        CustomFromJson
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Override world path on EngineHost before EngineHost.Start() fires.
        // AfterSceneLoad (SceneBootstrapper) fires after all Awake() calls, so this
        // also runs before SceneBootstrapper has a chance to substitute defaults.
        var host = GetComponent<EngineHost>();
        if (host == null) return;

        var field = typeof(EngineHost).GetField("_worldDefinitionPath",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(host, _worldDefinitionPath);
    }

    private void Start()
    {
        Debug.Log(
            $"[PlaytestSceneSeeder] Boot — " +
            $"NPCs:{_npcCount} World:{_worldDefinitionPath} " +
            $"BalanceMode:{_archetypeBalanceMode} " +
            $"Stains:{_seedStainsAtBoot} Breakables:{_seedBreakablesAtBoot} " +
            $"StartTime:{_startWallTimeHHMM}");
    }

    // ── Editor helpers ────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!string.IsNullOrEmpty(_startWallTimeHHMM) && !_startWallTimeHHMM.Contains(":"))
            Debug.LogWarning("[PlaytestSceneSeeder] _startWallTimeHHMM should be HH:MM (e.g. \"08:30\").");
    }
#endif

    // ── Public accessors (for tests) ──────────────────────────────────────────

    public int NpcCount           => _npcCount;
    public string WorldDefinitionPath => _worldDefinitionPath;
    public int SeedStainsAtBoot   => _seedStainsAtBoot;
    public int SeedBreakablesAtBoot => _seedBreakablesAtBoot;
    public string StartWallTimeHHMM => _startWallTimeHHMM;
    public ArchetypeBalanceMode BalanceMode => _archetypeBalanceMode;
}
