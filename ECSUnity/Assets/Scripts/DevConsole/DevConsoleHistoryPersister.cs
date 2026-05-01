#if WARDEN
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Saves and loads the command-navigation history to/from disk — WP-3.1.H AT-16.
///
/// Format: one command per line, UTF-8, newest at end.
/// On save, only the last <see cref="DevConsoleConfig.MaxCommandHistory"/> lines
/// are retained so the file never grows unbounded.
///
/// Thread safety: call from the Unity main thread only.
/// </summary>
public sealed class DevConsoleHistoryPersister
{
    // ── Save ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes <paramref name="commands"/> to <paramref name="filePath"/>,
    /// capped at <paramref name="maxEntries"/> lines (oldest dropped first).
    /// </summary>
    public void Save(IEnumerable<string> commands, string filePath, int maxEntries = 100)
    {
        if (commands == null) return;

        try
        {
            EnsureDirectory(filePath);

            // Collect into a list so we can cap at maxEntries.
            var list = new List<string>(commands);
            int start = list.Count > maxEntries ? list.Count - maxEntries : 0;

            using var writer = new StreamWriter(filePath, append: false,
                encoding: System.Text.Encoding.UTF8);
            for (int i = start; i < list.Count; i++)
                writer.WriteLine(list[i]);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DevConsoleHistoryPersister] Save failed: {ex.Message}");
        }
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads command history from <paramref name="filePath"/>.
    /// Returns an empty list if the file does not exist or cannot be read.
    /// </summary>
    public List<string> Load(string filePath)
    {
        var result = new List<string>();
        if (!File.Exists(filePath)) return result;

        try
        {
            var lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                    result.Add(trimmed);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DevConsoleHistoryPersister] Load failed: {ex.Message}");
        }

        return result;
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static void EnsureDirectory(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }
}

#endif
