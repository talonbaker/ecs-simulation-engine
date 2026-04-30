# WP-3.1.F — JSONL Stream Wiring (Unity-side)

> **DO NOT DISPATCH UNTIL WP-3.1.A IS MERGED.**
> The Unity scaffold provides `EngineHost` and `WorldStateProjectorAdapter`. This packet wires the host's JSONL stream emission for WARDEN builds.
>
> **WARDEN-build only.** All deliverables in this packet are gated behind `#if WARDEN`. RETAIL builds strip the entire emission pipeline.

**Tier:** Sonnet
**Depends on:** WP-3.1.A (Unity scaffold), Phase 0 telemetry (`Warden.Telemetry`, `WorldStateDto`)
**Parallel-safe with:** WP-3.1.B (silhouettes), WP-3.1.C (lighting), WP-3.1.D (build mode), WP-3.1.E (player UI)
**Timebox:** 90 minutes
**Budget:** $0.40

---

## Goal

The kickoff brief commits to: *"the JSONL stream is permanent for development. In WARDEN builds, Unity emits `WorldStateDto` JSONL to disk on a background thread per N ticks. Same wire format the orchestrator already consumes. AI agents see exactly what the player sees."*

This packet ships that. After this packet:

- A WARDEN-only `JsonlStreamEmitter` MonoBehaviour runs a background thread emitting `WorldStateDto` snapshots to `worldstate.jsonl`.
- Cadence is configurable: default emit every N engine ticks (N=30, ~once per game-second at 50 ticks/sec). Runtime-tunable from the dev console (when 3.1.H lands).
- The thread does **not block Unity's main loop.** Snapshot is captured on main thread, queued; serialisation + disk write happen on the worker thread.
- Wire format is byte-identical to `Warden.Telemetry.Projectors.WorldStateProjector.Project` output — agents and the orchestrator parse the file with no new code.
- File rotates on size threshold (default 100MB) or on session start.

This is the substrate that lets dev-time AI agents (in Claude Code, in the orchestrator, anywhere) see the live game state without modifying the game.

---

## Reference files

- `docs/c2-infrastructure/work-packets/WP-3.1.A-unity-scaffold-and-baseline-render.md` — `EngineHost.Snapshot()` returns `WorldStateDto`. WARDEN scripting define configured.
- `docs/c2-infrastructure/00-SRD.md` §2 (Pillar A — telemetry), §8.7 (engine host-agnostic; telemetry build-conditional).
- `docs/c2-content/ux-ui-bible.md` §4.7 — telemetry cadence policy: never per-frame; runtime-tunable.
- `docs/PHASE-3-KICKOFF-BRIEF.md` — JSONL stream commitments.
- `Warden.Telemetry/Projectors/*` — projection logic. Same DTO shape Unity emits.
- `Warden.Anthropic/*` and `Warden.Orchestrator/*` — JSONL consumers; wire format must match.
- `ECSCli` source — existing `ECSCli ai stream` command for reference. Unity emits the same format.
- `ECSUnity/Assets/Scripts/Engine/EngineHost.cs` (from 3.1.A) — `Snapshot()` API.

---

## Non-goals

- Do **not** ship in RETAIL builds. The entire packet is `#if WARDEN`. RETAIL stripping is automatic.
- Do **not** introduce a new wire format. JSONL line is `WorldStateDto` JSON-serialised; identical to existing `ai stream` output.
- Do **not** introduce server / network connectivity. File-on-disk only. Network telemetry is a future packet.
- Do **not** introduce streaming compression (gzip, etc.). Plain JSONL at v0.1; rotation handles size.
- Do **not** modify the engine, projectors, or orchestrator surface.
- Do **not** retry, recurse, or "self-heal."

---

## Design notes

### `JsonlStreamEmitter` MonoBehaviour

```csharp
#if WARDEN
public sealed class JsonlStreamEmitter : MonoBehaviour
{
    [SerializeField] EngineHost _host;
    [SerializeField] JsonlStreamConfig _config;

    Thread _worker;
    BlockingCollection<string> _queue;
    CancellationTokenSource _cts;
    long _lastEmitTick = -1;

    void Start()
    {
        _queue = new BlockingCollection<string>(boundedCapacity: 256);
        _cts = new CancellationTokenSource();
        _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "JsonlStreamEmitter" };
        _worker.Start();
    }

    void Update()
    {
        long currentTick = _host.Engine.Clock.CurrentTick;
        if (currentTick - _lastEmitTick < _config.EmitEveryNTicks) return;
        _lastEmitTick = currentTick;

        // capture on main thread; serialise + write on worker
        var dto = _host.Snapshot();
        var json = JsonSerializer.Serialize(dto, _config.SerializerOptions);
        if (!_queue.TryAdd(json))
        {
            // queue full — drop and log
            Debug.LogWarning($"JsonlStreamEmitter: queue full at tick {currentTick}, dropped frame");
        }
    }

    void WorkerLoop()
    {
        var path = _config.OutputPath;
        EnsureDirectory(path);
        long bytesWritten = 0;

        while (!_cts.IsCancellationRequested)
        {
            string json;
            try { json = _queue.Take(_cts.Token); }
            catch (OperationCanceledException) { break; }

            File.AppendAllText(path, json + "\n");
            bytesWritten += json.Length + 1;

            if (bytesWritten > _config.RotationSizeBytes)
            {
                RotateFile(path);
                bytesWritten = 0;
            }
        }
    }

    void OnDestroy()
    {
        _cts?.Cancel();
        _worker?.Join(timeout: TimeSpan.FromSeconds(2));
        _queue?.Dispose();
    }
}
#endif
```

Determinism note: file write order is deterministic given engine tick order; the queue is FIFO. Dropping frames on queue-full is the only non-deterministic case — and it's logged for diagnosis.

### `JsonlStreamConfig`

```csharp
[CreateAssetMenu]
public sealed class JsonlStreamConfig : ScriptableObject
{
    public int EmitEveryNTicks = 30;          // ~once per game-second at 50 ticks/sec
    public string OutputPath = "Logs/worldstate.jsonl";
    public long RotationSizeBytes = 100 * 1024 * 1024;   // 100MB
    public bool PrettyPrint = false;          // never on by default; debug only
    public JsonSerializerOptions SerializerOptions { get; set; }
}
```

### Runtime cadence tuning

The dev console (3.1.H, future) will surface a command `set tickrate <n>` to change `EmitEveryNTicks` at runtime. This packet exposes a public property setter; 3.1.H consumes it.

A small UI gear in the WARDEN-only debug overlay (corner of screen, only visible in WARDEN builds) shows current cadence and queue depth.

### File rotation

When emitted bytes exceed `RotationSizeBytes`:
- Close current file.
- Rename to `worldstate.<timestamp>.jsonl`.
- Open new `worldstate.jsonl`.

Per-session: on Start, check if `worldstate.jsonl` exists; if so, rename to `worldstate.<previous-session-timestamp>.jsonl` before starting fresh.

### Tests

- `JsonlStreamEmitterStartTests.cs` — boot scene; assert worker thread starts; output file created.
- `JsonlStreamEmitterCadenceTests.cs` — set `EmitEveryNTicks = 30`; advance 100 ticks; assert ~3 lines written.
- `JsonlStreamEmitterFormatTests.cs` — emitted line parses as valid `WorldStateDto`; round-trip through `JsonSerializer.Deserialize` produces equivalent DTO.
- `JsonlStreamEmitterByteIdenticalTests.cs` — Unity-emitted line for a representative state matches `Warden.Telemetry.Projectors.WorldStateProjector.Project` output for the same state, byte-identical.
- `JsonlStreamEmitterBackgroundThreadTests.cs` — simulate slow disk; main thread frame time stays ≤ 16ms (no main-thread block).
- `JsonlStreamEmitterRotationTests.cs` — set `RotationSizeBytes = 1024`; emit until rotation; assert old file renamed with timestamp.
- `JsonlStreamEmitterQueueOverflowTests.cs` — fill queue; assert overflow logged; main thread does not block.
- `JsonlStreamEmitterRetailStripTests.cs` — build with `WARDEN` define removed; assert `JsonlStreamEmitter` type is not present in compilation.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSUnity/Assets/Scripts/Telemetry/JsonlStreamEmitter.cs` | Background-thread emitter (WARDEN-only). |
| code | `ECSUnity/Assets/Scripts/Telemetry/JsonlStreamConfig.cs` | ScriptableObject. |
| code | `ECSUnity/Assets/Scripts/Telemetry/CadenceDebugOverlay.cs` | WARDEN-only on-screen indicator. |
| asset | `ECSUnity/Assets/Settings/DefaultJsonlStreamConfig.asset` | Defaults. |
| test | `ECSUnity/Assets/Tests/Play/JsonlStreamEmitterStartTests.cs` | Start. |
| test | `ECSUnity/Assets/Tests/Play/JsonlStreamEmitterCadenceTests.cs` | Cadence. |
| test | `ECSUnity/Assets/Tests/Edit/JsonlStreamEmitterFormatTests.cs` | Format. |
| test | `ECSUnity/Assets/Tests/Edit/JsonlStreamEmitterByteIdenticalTests.cs` | Byte-identical to Warden. |
| test | `ECSUnity/Assets/Tests/Play/JsonlStreamEmitterBackgroundThreadTests.cs` | Main-thread non-blocking. |
| test | `ECSUnity/Assets/Tests/Play/JsonlStreamEmitterRotationTests.cs` | Rotation. |
| test | `ECSUnity/Assets/Tests/Play/JsonlStreamEmitterQueueOverflowTests.cs` | Overflow. |
| test | `ECSUnity/Assets/Tests/Edit/JsonlStreamEmitterRetailStripTests.cs` | RETAIL strip. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.1.F.md` | Completion note. SimConfig defaults. Cadence measurements. Disk impact estimate (bytes/minute at default cadence). Whether file rotation triggered during testing. Verified WARDEN strip: confirm `JsonlStreamEmitter` absent from RETAIL build. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | Boot scene; `JsonlStreamEmitter.Start` creates output file at `Logs/worldstate.jsonl`. | play-mode test |
| AT-02 | After 100 ticks at `EmitEveryNTicks = 30`: ~3 lines written to file. | play-mode test |
| AT-03 | Each line parses as valid `WorldStateDto` JSON; round-trip preserves all fields. | edit-mode test |
| AT-04 | Unity-emitted JSON line for a fixed state == `Warden.Telemetry.Projectors.WorldStateProjector.Project(state)` output, byte-identical. | edit-mode test |
| AT-05 | Simulated slow disk (100ms write delay): main-thread frame time stays ≤ 16ms; FPS not affected. | play-mode test |
| AT-06 | At `RotationSizeBytes = 1024`: file rotates after threshold; old file renamed with timestamp. | play-mode test |
| AT-07 | Queue overflow (capacity 256, slow worker): overflow logged via `Debug.LogWarning`; main thread does not block. | play-mode test |
| AT-08 | RETAIL build (no `WARDEN` define): `JsonlStreamEmitter` type not present in compilation; reference fails to resolve in `RetailStripTests`. | edit-mode test |
| AT-09 | Performance gate from 3.1.A still passes: 30 NPCs at 60 FPS with JsonlStreamEmitter active. | play-mode test |
| AT-10 | All Phase 0/1/2/3.0.x and 3.1.A tests stay green. | regression |
| AT-11 | `dotnet build` warning count = 0; `dotnet test` all green. | build + test |
| AT-12 | Unity Test Runner: all tests pass. | unity test runner |

---

## Followups (not in scope)

- **Network streaming.** Emit to a TCP socket / IPC pipe instead of (or alongside) disk. Allows live agent connection. Future.
- **Compression.** Gzip-on-write for long-running sessions. Future.
- **Per-domain streams.** Currently one JSONL per session containing all state. Could split into `npcs.jsonl`, `events.jsonl`, etc. Future.
- **Replay-from-JSONL.** A consumer that reads the file and reconstructs state for visual replay. Future.
- **Telemetry policy in dev console.** 3.1.H exposes the cadence tuner.
- **Diff-mode emission.** Emit delta between snapshots instead of full DTO. Bandwidth optimisation; deferred until needed.
- **Session metadata header.** First line of file = session metadata (seed, config, version) for replay reproducibility. Future polish.
