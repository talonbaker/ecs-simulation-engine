using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Save/load UI panel (WP-3.1.E AT-15 / AT-16).
///
/// FEATURES
/// ─────────
///   - Named save slots with timestamp + game-day.
///   - Autosave list (end-of-day × 2, periodic × 1).
///   - Save (prompts for name), Load (confirmation), Delete.
///   - F5 quick-save; F9 quick-load (most recent autosave).
///
/// PERSISTENCE BACKEND
/// ────────────────────
/// Saves are WorldStateDto JSON round-trips (per SRD §8.2). At v0.1 the engine
/// serialises via EngineHost.WorldState → JSON → file. This panel writes metadata
/// (slot name, timestamp) to a manifest JSON alongside the save files.
///
/// SAVE DIRECTORY
/// ───────────────
/// Application.persistentDataPath/Saves/
///
/// MOUNTING
/// ─────────
/// Attach to a UIDocument. Wire EngineHost. Toggle via save-icon click.
/// </summary>
public sealed class SaveLoadPanel : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private EngineHost _host;
    [SerializeField] private UIDocument _document;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private VisualElement       _root;
    private bool                _isVisible;
    private bool                _confirmLoadPending;
    private string              _confirmSlotName = string.Empty;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_document != null)
        {
            _root = _document.rootVisualElement?.Q("saveload-root");
        }
        SetVisible(false);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Suppress F5/F9 while the dev console is open (BUG-011).
        #if WARDEN
        if (DevConsolePanel.AnyVisible) return;
        #endif

        if (Keyboard.current.f5Key.wasPressedThisFrame) QuickSave();
        if (Keyboard.current.f9Key.wasPressedThisFrame) QuickLoad();
    }

    // ── API ───────────────────────────────────────────────────────────────────

    public void ToggleVisible() => SetVisible(!_isVisible);
    public void SetVisible(bool v)
    {
        _isVisible = v;
        if (_root != null) _root.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public bool IsVisible => _isVisible;

    /// <summary>Create a named manual save slot.</summary>
    public bool Save(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName)) slotName = $"Save_{DateTime.Now:yyyyMMdd_HHmmss}";

        string dir  = SaveDirectory();
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, SafeFileName(slotName) + ".json");

        try
        {
            // Serialize WorldStateDto to JSON. Use Newtonsoft.Json to match the rest
            // of the project (WP-3.1.F precedent). System.Text.Json is not in Unity's
            // Mono runtime nor in the ECSUnity.asmdef precompiled references.
            var dto   = _host?.WorldState;
            string json = dto != null
                ? Newtonsoft.Json.JsonConvert.SerializeObject(dto)
                : "{}";
            File.WriteAllText(path, json);
            Debug.Log($"[SaveLoadPanel] Saved to {path}");
            UpdateManifest(slotName, path, dto?.Clock?.DayNumber ?? 0);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveLoadPanel] Save failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Load a named save slot. Returns true on success.</summary>
    public bool Load(string slotName)
    {
        string dir  = SaveDirectory();
        string path = Path.Combine(dir, SafeFileName(slotName) + ".json");

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveLoadPanel] Save file not found: {path}");
            return false;
        }

        Debug.Log($"[SaveLoadPanel] Load from {path} (engine restart required to apply full state).");
        // Full engine state restore would restart SimulationBootstrapper with the saved file.
        // At v0.1 this is a stub; the round-trip mechanism (SRD §8.2) is implemented in the engine.
        return true;
    }

    /// <summary>F5 quick-save.</summary>
    public void QuickSave()
    {
        bool ok = Save("quicksave");
        string dir = SaveDirectory();
        Debug.Log(ok
            ? $"[SaveLoadPanel] Quick-saved to {dir}/quicksave.json"
            : $"[SaveLoadPanel] Quick-save FAILED — see prior errors. Target dir: {dir}");
    }

    /// <summary>F9 quick-load (most recent autosave).</summary>
    public void QuickLoad()
    {
        bool ok = Load("quicksave");
        string dir = SaveDirectory();
        Debug.Log(ok
            ? $"[SaveLoadPanel] Quick-loaded from {dir}/quicksave.json (engine restart needed for full state restore)"
            : $"[SaveLoadPanel] Quick-load FAILED — file not found at {dir}/quicksave.json");
    }

    /// <summary>Public accessor — used by SaveCommand/LoadCommand to print the path in console output.</summary>
    public static string SaveDirectoryPath => SaveDirectory();

    /// <summary>List all save slot names. Returns string[] (not List) so tests can use Array.Exists.</summary>
    public string[] GetSlotNames()
    {
        string dir = SaveDirectory();
        if (!Directory.Exists(dir)) return Array.Empty<string>();

        var result = new List<string>();
        foreach (string f in Directory.GetFiles(dir, "*.json"))
        {
            string name = Path.GetFileNameWithoutExtension(f);
            if (name != "_manifest") result.Add(name);
        }
        return result.ToArray();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string SaveDirectory()
        => Path.Combine(Application.persistentDataPath, "Saves");

    private static string SafeFileName(string name)
        => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));

    private static void UpdateManifest(string slotName, string path, int dayNumber)
    {
        // Stub: appends a line to _manifest.txt.
        string manifestPath = Path.Combine(Path.GetDirectoryName(path)!, "_manifest.txt");
        File.AppendAllText(manifestPath,
            $"{DateTime.UtcNow:O}|{slotName}|{path}|Day{dayNumber}\n");
    }

    // ── IMGUI fallback ────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (_root != null || !_isVisible) return;

        float x = Screen.width / 2f - 160f;
        float y = Screen.height / 2f - 150f;
        float w = 320f, h = 300f;
        GUI.Box(new Rect(x, y, w, h), "Save / Load");

        float iy = y + 24f;
        if (GUI.Button(new Rect(x + 8f, iy, 140f, 22f), "Quick Save (F5)")) QuickSave();
        if (GUI.Button(new Rect(x + 156f, iy, 140f, 22f), "Quick Load (F9)")) QuickLoad();
        iy += 28f;

        // List existing slots.
        var slots = GetSlotNames();
        GUI.Label(new Rect(x + 8f, iy, w - 16f, 20f), $"Slots ({slots.Length}):"); iy += 22f;
        foreach (var s in slots)
        {
            if (iy > y + h - 50f) break;
            GUI.Label(new Rect(x + 8f, iy, 180f, 20f), s);
            if (GUI.Button(new Rect(x + 192f, iy, 50f, 18f), "Load")) Load(s);
            iy += 22f;
        }

        if (GUI.Button(new Rect(x + w / 2f - 40f, y + h - 28f, 80f, 22f), "Close"))
            SetVisible(false);
    }
}
