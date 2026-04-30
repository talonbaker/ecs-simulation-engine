#if WARDEN
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Background-thread JSONL emitter — WP-3.1.F.
///
/// DESIGN
/// ──────
/// Unity's main thread captures a <see cref="WorldStateDto"/> snapshot each frame
/// (via <see cref="EngineHost.Snapshot()"/>) and enqueues the serialised JSON string
/// into a bounded <see cref="BlockingCollection{T}"/>.  A background worker thread
/// drains the queue and writes lines to <c>worldstate.jsonl</c> on disk.
///
/// This decoupling means disk latency (even hundreds of milliseconds) never touches
/// the main-thread frame time.
///
/// CADENCE
/// ───────
/// Emit every <c>EmitEveryNTicks</c> engine ticks (default 30 = ~once/game-second at
/// 50 ticks/sec).  Tune at runtime via <see cref="SetEmitEveryNTicks"/>.
///
/// FILE ROTATION
/// ─────────────
/// On Start: if <c>worldstate.jsonl</c> already exists, it is renamed with a
/// timestamp so each session starts fresh.
/// During a session: when bytes written exceed <see cref="JsonlStreamConfig.RotationSizeBytes"/>,
/// the current file is rotated with a mid-session timestamp.
///
/// QUEUE OVERFLOW
/// ──────────────
/// If the background thread is slower than emission (e.g. a frozen disk), the
/// bounded queue drops the incoming frame and logs a warning.  The main thread
/// never blocks.
///
/// WARDEN-ONLY
/// ───────────
/// The entire class is compiled only in WARDEN builds.  RETAIL builds have zero
/// overhead — the #if WARDEN guard removes all code from the compilation unit.
///
/// MOUNTING
/// ────────
/// Attach to the same persistent GameObject as EngineHost.  Assign _host and _config
/// in the Inspector (or via SceneBootstrapper reflection).
/// </summary>
public sealed class JsonlStreamEmitter : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField]
    [Tooltip("EngineHost providing world-state snapshots.")]
    private EngineHost _host;

    [SerializeField]
    [Tooltip("Configuration: cadence, output path, rotation threshold.")]
    private JsonlStreamConfig _config;

    // ── Threading ─────────────────────────────────────────────────────────────

    private Thread                      _worker;
    private BlockingCollection<string>  _queue;
    private CancellationTokenSource     _cts;

    // ── Cadence tracking ──────────────────────────────────────────────────────

    private long _lastEmitTick = -1;

    // ── Test / dev-console accessors ──────────────────────────────────────────

    /// <summary>Current number of items waiting in the emission queue.</summary>
    public int QueueDepth => _queue?.Count ?? 0;

    /// <summary>Whether the worker thread is alive.</summary>
    public bool IsWorkerAlive => _worker != null && _worker.IsAlive;

    /// <summary>Path the emitter is currently writing to.</summary>
    public string OutputPath => _config != null ? _config.OutputPath : string.Empty;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_config == null)
        {
            // Create a default config so the emitter is functional without an asset.
            _config = ScriptableObject.CreateInstance<JsonlStreamConfig>();
            Debug.LogWarning("[JsonlStreamEmitter] No JsonlStreamConfig assigned; using built-in defaults.");
        }

        // Rotate any pre-existing file from a prior session.
        RotateSessionFile(_config.OutputPath);

        // Bounded queue: 256 slots.  If the worker can't keep up we drop and log.
        _queue = new BlockingCollection<string>(boundedCapacity: 256);
        _cts   = new CancellationTokenSource();

        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name         = "JsonlStreamEmitter",
        };
        _worker.Start();

        Debug.Log($"[JsonlStreamEmitter] Started. Output: {_config.OutputPath} | " +
                  $"Cadence: every {_config.EmitEveryNTicks} ticks | " +
                  $"Rotation: {_config.RotationSizeBytes / (1024 * 1024)} MB");
    }

    private void Update()
    {
        // Guard: host must be present and alive.
        if (_host == null) return;

        long currentTick = _host.TickCount;

        // Check cadence: only emit every N engine ticks.
        if (currentTick - _lastEmitTick < _config.EmitEveryNTicks) return;
        _lastEmitTick = currentTick;

        // Capture snapshot on the main thread (WorldStateDto is a plain-data graph).
        WorldStateDto dto = _host.Snapshot();
        if (dto == null) return;

        // Serialise synchronously — JsonConvert is fast for small-to-medium DTOs.
        // The resulting string is passed to the background thread for disk I/O.
        string json;
        try
        {
            json = _config.PrettyPrint
                ? JsonConvert.SerializeObject(dto, Formatting.Indented)
                : JsonConvert.SerializeObject(dto, Formatting.None);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[JsonlStreamEmitter] Serialisation failed at tick {currentTick}: {ex.Message}");
            return;
        }

        // Non-blocking enqueue.  TryAdd returns false when the queue is full.
        if (!_queue.TryAdd(json))
        {
            Debug.LogWarning($"[JsonlStreamEmitter] Queue full at tick {currentTick} — frame dropped. " +
                             "Consider reducing EmitEveryNTicks or diagnosing disk performance.");
        }
    }

    private void OnDestroy()
    {
        // Signal worker to stop and wait briefly for a clean shutdown.
        _cts?.Cancel();
        _worker?.Join(TimeSpan.FromSeconds(2));
        _queue?.Dispose();
        _cts?.Dispose();

        Debug.Log("[JsonlStreamEmitter] Destroyed. Worker thread joined.");
    }

    // ── Worker thread ─────────────────────────────────────────────────────────

    private void WorkerLoop()
    {
        string path = _config.OutputPath;
        EnsureDirectory(path);

        long bytesWritten = 0;

        while (!_cts.IsCancellationRequested)
        {
            string json;
            try
            {
                // Block until a line is available or cancellation fires.
                json = _queue.Take(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                File.AppendAllText(path, json + "\n");
                bytesWritten += json.Length + 1;
            }
            catch (Exception ex)
            {
                // Disk errors are non-fatal; log and continue.
                UnityEngine.Debug.LogError($"[JsonlStreamEmitter] Write error: {ex.Message}");
            }

            // Mid-session rotation when the file exceeds the size threshold.
            if (bytesWritten > _config.RotationSizeBytes)
            {
                RotateMidSession(path);
                bytesWritten = 0;
            }
        }

        // Drain remaining items from the queue after cancellation before exit.
        while (_queue.TryTake(out var remaining))
        {
            try { File.AppendAllText(path, remaining + "\n"); }
            catch { /* best-effort flush */ }
        }
    }

    // ── File rotation helpers ─────────────────────────────────────────────────

    /// <summary>
    /// If <paramref name="path"/> already exists (prior session), rename it with
    /// a timestamp so this session starts with a clean file.
    /// </summary>
    private static void RotateSessionFile(string path)
    {
        if (!File.Exists(path)) return;

        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string dir       = Path.GetDirectoryName(path) ?? ".";
        string baseName  = Path.GetFileNameWithoutExtension(path);
        string ext       = Path.GetExtension(path);
        string rotated   = Path.Combine(dir, $"{baseName}.{timestamp}{ext}");

        try
        {
            File.Move(path, rotated);
            Debug.Log($"[JsonlStreamEmitter] Previous session log rotated to: {rotated}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[JsonlStreamEmitter] Could not rotate prior session file: {ex.Message}");
        }
    }

    /// <summary>
    /// Rotate mid-session when the file exceeds <see cref="JsonlStreamConfig.RotationSizeBytes"/>.
    /// Called from the worker thread — must not touch UnityEngine APIs.
    /// </summary>
    private static void RotateMidSession(string path)
    {
        if (!File.Exists(path)) return;

        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        string dir       = Path.GetDirectoryName(path) ?? ".";
        string baseName  = Path.GetFileNameWithoutExtension(path);
        string ext       = Path.GetExtension(path);
        string rotated   = Path.Combine(dir, $"{baseName}.{timestamp}{ext}");

        try { File.Move(path, rotated); }
        catch { /* best-effort; non-fatal */ }
    }

    private static void EnsureDirectory(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    // ── Dev-console API ───────────────────────────────────────────────────────

    /// <summary>
    /// Change the emission cadence at runtime.  Called by the dev console (3.1.H).
    /// Value is clamped to [1, 1000].
    /// </summary>
    public void SetEmitEveryNTicks(int n)
    {
        if (_config == null) return;
        _config.EmitEveryNTicks = Mathf.Clamp(n, 1, 1000);
        Debug.Log($"[JsonlStreamEmitter] Cadence changed to every {_config.EmitEveryNTicks} ticks.");
    }
}
#endif
