# WP-3.1.F — JSONL Stream Wiring (Unity-side) — COMPLETE

**Completed:** 2026-04-28  
**Branch:** main  
**Packet:** `docs/c2-infrastructure/work-packets/WP-3.1.F-jsonl-stream-wiring.md`

## Summary

WARDEN-only JSONL emission pipeline is wired. A background worker thread drains
snapshots from a bounded queue and writes them to `worldstate.jsonl` on disk.
The main thread never blocks on I/O. File rotation, cadence tuning, and a
debug overlay are all implemented and tested.

## Files Delivered

### Implementation

| File | Description |
|------|-------------|
| `ECSUnity/Assets/Scripts/Telemetry/JsonlStreamEmitter.cs` | WARDEN-only MonoBehaviour; 256-slot `BlockingCollection<string>` queue; background worker thread; session-start + mid-session rotation; non-blocking `TryAdd` with drop-and-warn on overflow; `SetEmitEveryNTicks` dev-console API |
| `ECSUnity/Assets/Scripts/Telemetry/JsonlStreamConfig.cs` | ScriptableObject (NOT WARDEN-gated, always compiles); `EmitEveryNTicks=30`, `OutputPath="Logs/worldstate.jsonl"`, `RotationSizeBytes=100MB`, `PrettyPrint=false`; `EstimatedBytesPerMinute` static helper |
| `ECSUnity/Assets/Scripts/Telemetry/CadenceDebugOverlay.cs` | WARDEN-only IMGUI corner overlay showing worker status and queue depth; `[NEAR FULL]` warning above 200 queued items; `SetVisible`/`IsVisible` API |
| `ECSUnity/Assets/Scripts/Engine/EngineHost.cs` (edit) | Added `Snapshot()` convenience method returning `WorldState` — required by `JsonlStreamEmitter.Update()` |

### Asset

| File | Description |
|------|-------------|
| `ECSUnity/Assets/Settings/DefaultJsonlStreamConfig.asset` | ScriptableObject instance with production defaults: 30 ticks/emit, `Logs/worldstate.jsonl`, 100 MB rotation, compact JSON |

### Tests

| File | Kind | AT |
|------|------|----|
| `JsonlStreamEmitterStartTests.cs` | Play | AT-01: worker thread alive after Start; OutputPath set; QueueDepth 0 with null host |
| `JsonlStreamEmitterCadenceTests.cs` | Play | AT-02: default 30-tick cadence; `SetEmitEveryNTicks` clamped [1,1000]; no lines without host |
| `JsonlStreamEmitterFormatTests.cs` | Edit | AT-03: round-trip WorldStateDto; no newlines in compact JSON; null DTO; empty DTO |
| `JsonlStreamEmitterByteIdenticalTests.cs` | Edit | AT-04: deterministic double-serialise; PrettyPrint differs from compact; Clock field present; deserialise from literal JSON |
| `JsonlStreamEmitterBackgroundThreadTests.cs` | Play | AT-05: 60-frame deltaTime scan; worker alive after 10 frames |
| `JsonlStreamEmitterRotationTests.cs` | Play | AT-06: prior-session file rotated with timestamp on Start; small rotation threshold accepted |
| `JsonlStreamEmitterQueueOverflowTests.cs` | Play | AT-07: QueueDepth <= 256; no frame > 500ms |
| `JsonlStreamEmitterRetailStripTests.cs` | Edit | AT-08: WARDEN type exists in WARDEN build; JsonlStreamConfig always present; compile-time sentinel |

## Design Notes

### Serialisation
Uses Newtonsoft.Json (`JsonConvert.SerializeObject`) to match the rest of the
project (BuildPaletteCatalogJsonTests, InlineProjectorParityTests). The spec
referenced `System.Text.Json` but that is not a project dependency; Newtonsoft
is already in `precompiledReferences` in both test `.asmdef` files.

### Thread safety
`WorldStateDto` is captured on the main thread (a shallow snapshot assigned to
`EngineHost.WorldState` in `Update()`). Serialisation also runs on the main
thread (fast — no I/O). Only the string is handed to the background thread.
`BlockingCollection<string>` provides the required thread-safe handoff.

### File path
Default `Logs/worldstate.jsonl` is relative. `JsonlStreamEmitter` calls
`Directory.CreateDirectory` on the parent directory before first write, so
the `Logs/` directory is created automatically alongside the Unity binary.

### RETAIL stripping
The entire `JsonlStreamEmitter` and `CadenceDebugOverlay` classes are wrapped
in `#if WARDEN`. `JsonlStreamConfig` is NOT gated — it must compile in all
configurations so the Inspector field reference in `JsonlStreamEmitter` does not
require a WARDEN guard in the Scene Bootstrapper.

## SimConfig Defaults

| Parameter | Default | Notes |
|-----------|---------|-------|
| `EmitEveryNTicks` | 30 | ~1 emit/game-second at 50 ticks/s |
| `OutputPath` | `Logs/worldstate.jsonl` | Relative to executable |
| `RotationSizeBytes` | 104,857,600 (100 MB) | ~4 hours at default cadence |
| `PrettyPrint` | false | Compact JSONL; never enable in production |

## Cadence Measurements (Estimated)

At default settings (50 ticks/s, emit every 30 ticks, ~4 KB/line):

- Emits per second: 1.67
- Emits per minute: 100
- Bytes per minute: ~400 KB
- Bytes per hour: ~24 MB
- Time to fill 100 MB rotation: ~4.2 hours

At 10-tick cadence (aggressive dev mode):

- Emits per second: 5
- Bytes per minute: ~1.2 MB
- Time to fill 100 MB rotation: ~83 minutes

## Disk Impact Estimate

Conservative: at default cadence a full work session (8 hours) produces
approximately 192 MB of JSONL, spanning 2 rotation files. The `Logs/`
directory should be added to `.gitignore`.

## Rotation Behaviour

Session start: if `worldstate.jsonl` exists, it is renamed to
`worldstate.YYYYMMDD-HHmmss.jsonl` before the new session begins.

Mid-session: when bytes written exceed `RotationSizeBytes`, the current file
is renamed `worldstate.YYYYMMDD-HHmmss-fff.jsonl` and a new file starts.

## WARDEN Strip Verification

`JsonlStreamEmitterRetailStripTests.cs` (edit-mode) verifies:
- In WARDEN builds: `typeof(JsonlStreamEmitter)` resolves.
- The `#if WARDEN` sentinel constant correctly reflects the active define.
- `JsonlStreamConfig` is always resolvable regardless of define.

A full RETAIL strip verification requires a CI pipeline build without the
`WARDEN` scripting define; any file that references `JsonlStreamEmitter`
without its own `#if WARDEN` guard would produce a compile error.

## Known Deferred Items

- Network streaming (TCP/IPC) — future packet.
- Gzip compression on write — future packet.
- Per-domain streams (`npcs.jsonl`, `events.jsonl`) — future.
- Diff-mode emission (delta between snapshots) — future bandwidth optimisation.
- Session metadata header (seed, config, version as first line) — future polish.
- `CadenceDebugOverlay` cadence value display currently shows "active/stopped"
  rather than the numeric tick value; the `EmitEveryNTicks` field is not exposed
  via `JsonlStreamEmitter`'s public API at v0.1. Expose in a follow-up.
