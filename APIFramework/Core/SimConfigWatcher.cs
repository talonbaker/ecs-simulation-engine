using APIFramework.Config;

namespace APIFramework.Core;

/// <summary>
/// Watches SimConfig.json for changes and fires a callback when the file is saved.
///
/// DEBOUNCE
/// ────────
/// Most editors (VS Code, Rider, Notepad++) write files in several rapid bursts.
/// We wait 250 ms after the last write event before reading — this ensures we get
/// the complete file, not a half-written one.
///
/// THREAD SAFETY
/// ─────────────
/// The FileSystemWatcher fires on a background thread. The callback runs on that
/// same thread. Callers that need to apply config on a specific thread (e.g. the
/// Avalonia UI thread) must marshal the call themselves.
///
/// Usage
/// ─────
///   var watcher = new SimConfigWatcher(path, newCfg => bootstrapper.ApplyConfig(newCfg));
///   // ... later ...
///   watcher.Dispose();
/// </summary>
public sealed class SimConfigWatcher : IDisposable
{
    private readonly FileSystemWatcher _fsw;
    private readonly string            _configPath;
    private readonly Action<SimConfig> _onChanged;
    private readonly object            _debounceLock = new();
    private Timer?                     _debounceTimer;
    private bool                       _disposed;

    public SimConfigWatcher(string configPath, Action<SimConfig> onChanged)
    {
        _configPath = Path.GetFullPath(configPath);
        _onChanged  = onChanged;

        var dir  = Path.GetDirectoryName(_configPath)
                   ?? throw new ArgumentException("Config path has no directory.");
        var file = Path.GetFileName(_configPath);

        _fsw = new FileSystemWatcher(dir, file)
        {
            NotifyFilter        = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _fsw.Changed += OnFileChanged;
        _fsw.Created += OnFileChanged; // some editors replace the file entirely on save
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Restart the debounce timer on every event
        lock (_debounceLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(FireReload, null,
                dueTime: 250,       // wait 250 ms after last write
                period:  Timeout.Infinite);
        }
    }

    private void FireReload(object? state)
    {
        if (_disposed) return;

        // SimConfig.Load is safe to call from any thread
        var newCfg = SimConfig.Load(_configPath);
        _onChanged(newCfg);
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fsw.Dispose();
        lock (_debounceLock) _debounceTimer?.Dispose();
    }
}
