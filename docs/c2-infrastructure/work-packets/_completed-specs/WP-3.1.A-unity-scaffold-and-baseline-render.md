# WP-3.1.A — Unity Scaffold + Baseline Camera + Dot Render

> **DO NOT DISPATCH UNTIL ALL OF PHASE 3.0.x IS MERGED** (WP-3.0.0, WP-3.0.1, WP-3.0.2, WP-3.0.3, WP-3.0.4, WP-3.0.5, plus WP-3.0.6 if a fainting packet was authored).
>
> This packet stands up the Unity host. It loads `APIFramework.dll` as a class library, ticks the engine in-process, and reads `WorldStateDto` for rendering. The performance gate (30 NPCs at 60 FPS) is the gate that proves WP-3.0.5's `ComponentStore<T>` perf refactor actually delivered. If 3.0.5 didn't ship its allocation-drop guarantee, this packet will fail-closed at the framerate test — that's the right behavior.

**Tier:** Sonnet
**Depends on:** Phase 3.0.x (all packets merged); particularly WP-3.0.5 (perf refactor that makes the FPS gate achievable)
**Parallel-safe with:** **NOTHING.** Solo dispatch only — Unity project does not yet exist; subsequent packets reference its scaffold.
**Timebox:** 180 minutes (longest packet in the phase; new project surface, new tooling, new platform)
**Budget:** $0.80

---

## Goal

The engine has been hostable by Unity since the SRD §8.7 axiom landed in Phase 2. This packet is when "hostable" becomes "hosted." After this packet, Unity loads `APIFramework.dll`, instantiates an `EntityManager`, runs the boot sequence, ticks the engine on its main thread (or a controlled secondary thread — see Design notes), reads `WorldStateDto` per tick, and renders the world as **flat colored rectangles for rooms** and **single-pixel-or-small dots for NPCs**. Cubicle 12 sits empty. Donna walks from her cubicle to the Women's Bathroom. Greg flickers between the IT Closet and his desk. The simulation is alive on screen for the first time.

The render is intentionally crude. This packet is about *the substrate*: project scaffold, engine bridge, camera control, render pipeline, performance discipline. Subsequent packets (3.1.B silhouettes, 3.1.C lighting, 3.1.D build mode, 3.1.E player UI, etc.) layer on top. **Resist temptation to ship pretty graphics.** Every minute spent on visuals here is a minute not spent on the substrate that needs to be rock-solid.

This packet ships:

1. A Unity project at `ECSUnity/` (mirroring the existing project naming — `APIFramework`, `ECSCli`, `ECSVisualizer` are the C# projects; `ECSUnity` is the Unity host).
2. An assembly bridge that loads `APIFramework.dll` (and, in WARDEN builds, `Warden.Telemetry.dll`).
3. An `EngineHost` MonoBehaviour that owns the engine lifetime: boot, tick, query for state, dispose.
4. A `WorldStateProjector` that produces `WorldStateDto` from the engine each render frame (or per N ticks; see Design notes).
5. A `RoomRectangleRenderer` and `NpcDotRenderer` that draw the world as flat rectangles + colored dots.
6. A `CameraController` with single-stick-equivalent control: pan, lazy-susan rotate, bounded zoom, fixed altitude (per UX bible §2.1).
7. A performance harness that asserts **30 NPCs at sustained ≥58 FPS** for at least 60 real-time-seconds. Below that, packet fails closed.
8. WARDEN scripting define + RETAIL scripting define configured. WARDEN references `Warden.Telemetry.dll`; RETAIL strips it.
9. Unity-appropriate `.gitignore` covering `Library/`, `Temp/`, `obj/`, `Build/`, `Logs/`, etc.
10. A baseline scene `MainScene.unity` that bootstraps the office-starter world and runs.

After this packet, every future Phase 3.1.x packet has a Unity project to extend. The visualization phase has its first concrete artifact.

---

## Reference files

- `docs/PHASE-3-KICKOFF-BRIEF.md` — Phase 3 commitments. Read for context on the WARDEN/RETAIL split, the 30-NPCs-at-60-FPS mandate, and the "engine hosts permanently" axiom (8.7).
- `docs/c2-infrastructure/00-SRD.md` — especially §8.7 (host-agnostic engine), §4.2 (determinism), §4.4 (observability — `WorldStateDto` is the universal observation surface).
- `docs/c2-infrastructure/PHASE-2-HANDOFF.md` §6.1 — Phase 3.1 scaffolding plan.
- `docs/c2-content/ux-ui-bible.md` — **read all of §1 and §2.1.** Camera commitments are load-bearing. Single-stick-equivalent, fixed-altitude under ceiling, walls-fade-on-occlusion, lazy-susan rotate, bounded zoom, no diving under desks.
- `docs/c2-content/world-bible.md` — three floors named, but **single-floor v0.1 per project memory.** This packet renders the ground floor only.
- `docs/c2-content/aesthetic-bible.md` — §1 priority axioms (lighting, proximity, movement). Lighting visualization is deferred to 3.1.C; this packet does flat colors only.
- `APIFramework/Core/EntityManager.cs` — read the public API. The Unity host instantiates one of these.
- `APIFramework/Core/SimulationBootstrapper.cs` — read for boot sequence. The Unity host calls equivalent boot logic.
- `APIFramework/Core/SimulationClock.cs` — the tick driver. Unity's host ticks this.
- `Warden.Telemetry/Projectors/*` — read for `WorldStateDto` projection. The Unity host reuses this projection logic in WARDEN builds; RETAIL builds need an equivalent inline projection or strip the projector entirely.
- `ECSCli/` source — read for the existing host-headless pattern. Unity's `EngineHost` mirrors `ECSCli.RunCommand` boot/tick/dispose semantics.
- `ECSVisualizer/` source — the existing Avalonia visualizer. Read for *patterns*, not as a copy target. Avalonia is an alternative renderer; Unity replaces it for Phase 3+ but Avalonia stays as the dev-debug viewer.
- `examples/office-starter.json` — the world definition this packet's `MainScene` boots.
- `SimConfig.json` — engine configuration. Unity reads from a scene-attached `SimConfigAsset` (ScriptableObject wrapping the JSON) or the JSON directly (Sonnet picks; recommend ScriptableObject for editor-time inspection).

---

## Non-goals

- Do **not** ship per-NPC silhouette rendering. NPCs render as colored dots at v0.1; silhouettes are WP-3.1.B.
- Do **not** implement lighting visualization. Rooms are flat colored rectangles; no sun beams, no flickering fluorescent. WP-3.1.C.
- Do **not** implement build mode (drag-drop placement, IWorldMutationApi calls). WP-3.1.D.
- Do **not** implement player UI (inspector, time controls, selection cues, notifications). WP-3.1.E.
- Do **not** implement JSONL stream emission. WP-3.1.F.
- Do **not** implement event log. WP-3.1.G.
- Do **not** implement dev console. WP-3.1.H.
- Do **not** modify any file under `APIFramework/`, `APIFramework.Tests/`, `Warden.*/` projects. The Unity host *consumes* the engine; it does not change it. If you find an engine bug while integrating, document it in the completion note as a follow-up packet candidate; do not patch in this scope.
- Do **not** introduce a NuGet dependency on the engine side. Unity loads the compiled `APIFramework.dll` directly from the build output.
- Do **not** ship pretty graphics. Resist visual scope creep. Flat colors, dots, that's it.
- Do **not** add multi-floor rendering. Single-floor (ground) only at v0.1 per project memory.
- Do **not** add a tutorial / first-launch experience. UX bible explicitly defers this.
- Do **not** retry, recurse, or "self-heal" on failure. Fail closed per SRD §4.1. If the FPS gate misses, escalate as blocked with measurements.
- Do **not** add a runtime LLM call. (SRD §8.1.)

---

## Design notes

### Project layout

```
ECSUnity/
├── Assets/
│   ├── Scripts/
│   │   ├── Engine/
│   │   │   ├── EngineHost.cs                      ← MonoBehaviour, owns engine lifetime
│   │   │   ├── WorldStateProjectorAdapter.cs      ← bridges Warden.Telemetry to Unity in WARDEN builds
│   │   │   └── SimConfigAsset.cs                  ← ScriptableObject wrapping SimConfig
│   │   ├── Camera/
│   │   │   ├── CameraController.cs                ← single-stick pan/rotate/zoom
│   │   │   ├── CameraInputBindings.cs             ← keyboard + mouse + gamepad bindings
│   │   │   └── CameraConstraints.cs               ← altitude clamp, zoom clamp, no-under-desk guard
│   │   ├── Render/
│   │   │   ├── RoomRectangleRenderer.cs           ← MeshFilter + MeshRenderer per room
│   │   │   ├── NpcDotRenderer.cs                  ← billboarded sprite or quad per NPC
│   │   │   └── RenderColorPalette.cs              ← era-appropriate palette per aesthetic-bible
│   │   ├── Performance/
│   │   │   ├── FrameRateMonitor.cs                ← rolling-average FPS, exposed for tests
│   │   │   └── PerformanceGate.cs                 ← asserts 30-NPCs-at-60-FPS in test mode
│   │   └── ECSUnity.asmdef                        ← assembly definition; references APIFramework
│   ├── Scenes/
│   │   └── MainScene.unity                        ← single scene; boots office-starter
│   ├── Plugins/
│   │   ├── APIFramework.dll                       ← copied from build output via post-build step
│   │   └── Warden.Telemetry.dll                   ← WARDEN builds only
│   └── Settings/
│       └── DefaultSimConfig.asset                 ← ScriptableObject with default SimConfig
├── Packages/
│   └── manifest.json                              ← Unity package manifest; pin Unity 6 LTS
├── ProjectSettings/                               ← Unity project settings; scripting defines
└── .gitignore                                     ← Library/, Temp/, obj/, Build/, Logs/
```

Unity version pin: **Unity 6 LTS (6000.0.x)** as of authoring. .NET Standard 2.1 / .NET 8 compat — verify in `manifest.json` and `csc.rsp`.

### `EngineHost` lifecycle

```csharp
public sealed class EngineHost : MonoBehaviour
{
    [SerializeField] SimConfigAsset _configAsset;
    [SerializeField] string _worldDefinitionPath = "Assets/StreamingAssets/office-starter.json";

    EntityManager _entityManager;
    SimulationClock _clock;
    SimulationBootstrapper _bootstrapper;
    bool _alive;

    void Start()
    {
        _bootstrapper = new SimulationBootstrapper(_configAsset.Config, LoadWorldDefinition());
        _entityManager = _bootstrapper.EntityManager;
        _clock = _bootstrapper.Clock;
        _bootstrapper.BootOnce();   // PreUpdate boot-time systems run once
        _alive = true;
    }

    void Update()
    {
        if (!_alive) return;

        // tick the engine deterministically; one engine tick per Unity frame at default
        // configurable: ticks-per-frame, frames-per-tick (for sub-engine-tick interpolation later)
        _bootstrapper.Tick(Time.deltaTime);
    }

    void OnDestroy()
    {
        _alive = false;
        _bootstrapper?.Dispose();
    }

    public WorldStateDto Snapshot() => _bootstrapper.Project();
    public EntityManager Engine => _entityManager;
}
```

The engine ticks **synchronously on Unity's main thread** at v0.1. Multi-threaded tick is a future packet (when and if profiling justifies it). The single-threaded model preserves the SRD §4.2 determinism contract trivially.

### `WorldStateDto` consumption

Unity reads `WorldStateDto` once per render frame — *not* per engine tick. Reasoning: rendering at 60 FPS while ticking at maybe 10–30 ticks/sec means most renders are interpolating between two engine ticks, not reading new state. At v0.1, no interpolation: render reads the latest projected snapshot every frame; if engine hasn't ticked, render is identical. Future packet adds visual interpolation between ticks for smooth motion.

For 30 NPCs, the projection is cheap (~0.1ms expected). The projector runs on Unity's main thread immediately after tick.

### `CameraController`

Single-stick-equivalent. Bindings (default — UX bible §2.1):

| Verb | Keyboard | Mouse | Gamepad |
|:---|:---|:---|:---|
| Pan | Arrow keys / WASD | Middle-click drag | Left stick |
| Rotate (lazy-susan) | Q / E | Right-click drag | Right stick X |
| Zoom | + / − | Scroll wheel | Triggers (LT / RT) |
| Recenter on selected | F | Double-click | A button |

Constraints:
- **Altitude:** clamped between `cameraMinAltitude` (just-above-cube-top, ~3 world-units) and `cameraMaxAltitude` (just-under-ceiling, ~5 world-units). No transition outside this range in default mode.
- **Look angle:** fixed at 45–60° pitch (top-down-ish). No angle change in default mode.
- **No collision with structural geometry.** Camera passes through walls; walls fade visually (deferred to 3.1.C lighting packet, but stub for fade hook is here).
- **No under-desk dive.** Implementation: collider-based clip-prevention or distance-from-floor check.

Creative-mode camera (full 3D, free altitude) is **not** in this packet. UX bible §5.2 notes it as a creative-mode unlock. Future packet adds the toggle.

### `RoomRectangleRenderer`

Reads `WorldStateDto.rooms[]`. For each room:
- Generate or update a flat quad mesh sized to the room's `bounds`.
- Apply a color from the era-appropriate palette (per aesthetic-bible §color-palette: muted beige, warm fluorescent yellow indoors, cool daylight cool grey outside).
- Color is room-kind-driven: cubicle area = beige; bathroom = pale tile blue; basement = concrete grey; office = warm cream.

Mesh-pooling: pre-allocate quads; reuse on update. No per-frame allocations.

### `NpcDotRenderer`

Reads `WorldStateDto.npcs[]`. For each NPC:
- Position a small (~0.3 world-unit) billboarded quad at the NPC's `position`.
- Color by archetype (or by silhouette dominant color from cast-bible). Donna = dark plum; Greg = pale yellow-green; Frank = brown.
- Z-position above floor, below ceiling.

This is the literal "30 dots that move" baseline. Performance gate hangs on this — 30 NPCs each one quad, single material, single draw call (instanced if Unity's render pipeline supports it).

### Performance gate

The packet's hardest acceptance test: **30 NPCs at sustained ≥58 FPS for 60 real-time-seconds.**

Implementation:
- `FrameRateMonitor` exposes a rolling 60-frame FPS average.
- `PerformanceGate` is a Unity Test Framework (UTF) Play-mode test.
- Boot scene with the office-starter world; assert NPC count == 30.
- Run for 60 seconds (real-time, in Play mode).
- Sample FPS every second.
- Assertion: minimum sample ≥ 55, mean ≥ 58, p99 ≥ 50.

If the test fails, the packet fails-closed. **Do not weaken the assertion to ship.** Profile, identify the bottleneck (suspect: WP-3.0.5 didn't deliver the boxing-free perf claim, OR the projection is too expensive, OR Unity-side allocations leak), document, and escalate as blocked.

The 30-NPCs-at-60-FPS gate is the proof that the engine + Unity host is viable. The whole rest of Phase 3.1 hangs on this.

### WARDEN vs RETAIL scripting defines

Unity supports custom scripting defines per platform / per-build-target. Configure two:

- `WARDEN`: includes `Warden.Telemetry.dll` reference; `WorldStateDto` projection uses the `Warden.Telemetry.Projectors` types.
- `RETAIL`: strips `Warden.*` references; projection uses an inline `WorldStateProjector` that mirrors `WorldStateDto`'s schema without depending on the Warden assembly.

Default editor build = WARDEN. Standalone builds for player distribution would set RETAIL.

The `WorldStateProjectorAdapter` class is a thin shim:

```csharp
#if WARDEN
using Warden.Telemetry.Projectors;
public class WorldStateProjectorAdapter
{
    public WorldStateDto Project(EntityManager em, SimulationClock clock)
        => WorldStateProjector.Project(em, clock);   // existing Warden code
}
#else
public class WorldStateProjectorAdapter
{
    public WorldStateDto Project(EntityManager em, SimulationClock clock)
        => InlineProjector.Project(em, clock);       // re-implementation that doesn't depend on Warden
}
#endif
```

`InlineProjector` is a new class in this packet, RETAIL-build-only. It mirrors the projection logic without the Warden dependency. Tests verify byte-identical output between WARDEN and RETAIL paths on a representative state.

### `.gitignore` for Unity

Append to repo `.gitignore` (or create `ECSUnity/.gitignore`):

```
Library/
Temp/
obj/
Build/
Builds/
Logs/
UserSettings/
*.csproj
*.sln
*.vcxproj
*.suo
*.user
.vs/
.idea/
.vscode/
```

`*.csproj` and `*.sln` are Unity-generated; the canonical files live in `ProjectSettings/` and `Packages/`.

### Determinism

Unity's `Time.deltaTime` is wall-clock-tied; passing it directly to `_bootstrapper.Tick(deltaTime)` makes the engine non-deterministic across machines. **At v0.1**, the engine ticks at a fixed rate driven by Unity's `FixedUpdate` instead of `Update`:

```csharp
void FixedUpdate()
{
    if (!_alive) return;
    _bootstrapper.Tick(Time.fixedDeltaTime);   // Unity's fixed timestep, configurable in project settings; default 0.02s = 50 ticks/sec
}
```

Render in `Update`; tick in `FixedUpdate`. The engine sees a constant `deltaTime` per tick. Determinism contract holds.

Future packet may add a custom tick scheduler that decouples engine tick rate from Unity's physics loop, but `FixedUpdate` is the v0.1 simplification.

### Tests

Unity Test Framework (UTF) Play-mode tests. Edit-mode tests where applicable.

- `EngineHostBootTests.cs` (Play-mode) — Start scene; assert engine boots without exception; entity count ≥ 30 after office-starter loads; clock starts at tick 0.
- `EngineHostTickTests.cs` (Play-mode) — boot scene; advance 100 frames; assert clock advanced; NPC positions changed for at least one NPC.
- `WorldStateProjectorAdapterTests.cs` (Edit-mode) — round-trip: project state, assert non-null DTO, assert NPC count matches engine entity count.
- `RoomRectangleRendererTests.cs` (Play-mode) — load scene; assert one mesh per room in `WorldStateDto.rooms[]`; mesh bounds match room bounds.
- `NpcDotRendererTests.cs` (Play-mode) — load scene; assert one renderer per NPC; renderer position tracks NPC position over 100 frames.
- `CameraControllerPanTests.cs` (Play-mode) — simulated WASD input; camera position changes accordingly.
- `CameraControllerRotateTests.cs` (Play-mode) — simulated Q/E input; camera angle changes; lazy-susan around focus point.
- `CameraControllerZoomTests.cs` (Play-mode) — simulated scroll input; altitude changes within clamp range; cannot exceed `cameraMaxAltitude` or drop below `cameraMinAltitude`.
- `CameraNoUnderDeskGuardTests.cs` (Play-mode) — simulated input that would drop camera below cube-top; camera clamps; no clip into desk geometry.
- **`PerformanceGate30NpcAt60FpsTests.cs`** (Play-mode, the hardest) — boot office-starter; run 60 seconds; sample FPS at 1Hz; assert min ≥ 55, mean ≥ 58, p99 ≥ 50. **No weakening.**
- `BuildConfigurationTests.cs` (Edit-mode) — assert `WARDEN` define is set in the editor build; assert `WorldStateProjectorAdapter` resolves to the Warden projection in WARDEN; assert it resolves to `InlineProjector` when WARDEN define is removed (test by toggling defines and reloading).
- `InlineProjectorParityTests.cs` (Edit-mode) — for a representative engine state, `InlineProjector.Project` produces byte-identical JSON output to `Warden.Telemetry.Projectors.WorldStateProjector.Project`.

### SimConfig additions

```jsonc
{
  "unityHost": {
    "ticksPerSecond":            50,        // Unity FixedUpdate rate; engine deterministic at this rate
    "renderFrameRateTarget":     60,        // not enforced, but communicates intent
    "performanceGateMinFps":     55,        // assertion floor for the perf test
    "performanceGateMeanFps":    58,
    "performanceGateP99Fps":     50,
    "cameraMinAltitude":         3.0,
    "cameraMaxAltitude":         5.0,
    "cameraPitchAngle":          50.0,      // degrees
    "cameraPanSpeed":            5.0,       // world-units/sec
    "cameraRotateSpeed":         90.0,      // degrees/sec
    "cameraZoomSpeed":           2.0,
    "logTickRateEverySeconds":   10.0       // dev console diagnostic
  }
}
```

These extend the existing `SimConfig.json`. The engine doesn't read them; the Unity host does. Document in the completion note as Unity-host-specific.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| project | `ECSUnity/` | Unity project root. |
| code | `ECSUnity/Assets/Scripts/Engine/EngineHost.cs` | Engine lifetime owner. |
| code | `ECSUnity/Assets/Scripts/Engine/WorldStateProjectorAdapter.cs` | WARDEN/RETAIL projector shim. |
| code | `ECSUnity/Assets/Scripts/Engine/InlineProjector.cs` | RETAIL-build projector. |
| code | `ECSUnity/Assets/Scripts/Engine/SimConfigAsset.cs` | ScriptableObject wrapping SimConfig. |
| code | `ECSUnity/Assets/Scripts/Camera/CameraController.cs` | Single-stick pan/rotate/zoom. |
| code | `ECSUnity/Assets/Scripts/Camera/CameraInputBindings.cs` | Bindings table. |
| code | `ECSUnity/Assets/Scripts/Camera/CameraConstraints.cs` | Altitude / pitch clamps + no-under-desk guard. |
| code | `ECSUnity/Assets/Scripts/Render/RoomRectangleRenderer.cs` | Flat-colored room rendering. |
| code | `ECSUnity/Assets/Scripts/Render/NpcDotRenderer.cs` | Per-NPC dot rendering. |
| code | `ECSUnity/Assets/Scripts/Render/RenderColorPalette.cs` | Era-appropriate palette source. |
| code | `ECSUnity/Assets/Scripts/Performance/FrameRateMonitor.cs` | Rolling FPS sampler. |
| code | `ECSUnity/Assets/Scripts/Performance/PerformanceGate.cs` | Test harness for FPS assertion. |
| asset | `ECSUnity/Assets/ECSUnity.asmdef` | Assembly definition. |
| scene | `ECSUnity/Assets/Scenes/MainScene.unity` | Single scene; boots office-starter. |
| asset | `ECSUnity/Assets/Settings/DefaultSimConfig.asset` | Default SimConfig ScriptableObject. |
| asset | `ECSUnity/Assets/StreamingAssets/office-starter.json` | World definition (copied or symlinked from `examples/`). |
| binary | `ECSUnity/Assets/Plugins/APIFramework.dll` | Built engine; post-build copy step. |
| binary | `ECSUnity/Assets/Plugins/Warden.Telemetry.dll` | WARDEN-only. |
| config | `ECSUnity/Packages/manifest.json` | Unity 6 LTS package manifest. |
| config | `ECSUnity/ProjectSettings/ProjectSettings.asset` | Scripting defines (WARDEN). |
| config | `ECSUnity/.gitignore` | Library/, Temp/, etc. |
| code | `SimConfig.cs` (existing, modified) | Add `UnityHostConfig` class + property. |
| config | `SimConfig.json` (modified) | Add `unityHost` section. |
| test | `ECSUnity/Assets/Tests/Play/EngineHostBootTests.cs` | Boot test. |
| test | `ECSUnity/Assets/Tests/Play/EngineHostTickTests.cs` | Tick test. |
| test | `ECSUnity/Assets/Tests/Edit/WorldStateProjectorAdapterTests.cs` | Projection round-trip. |
| test | `ECSUnity/Assets/Tests/Play/RoomRectangleRendererTests.cs` | Room rendering. |
| test | `ECSUnity/Assets/Tests/Play/NpcDotRendererTests.cs` | NPC dot rendering. |
| test | `ECSUnity/Assets/Tests/Play/CameraControllerPanTests.cs` | Camera pan. |
| test | `ECSUnity/Assets/Tests/Play/CameraControllerRotateTests.cs` | Camera rotate. |
| test | `ECSUnity/Assets/Tests/Play/CameraControllerZoomTests.cs` | Camera zoom. |
| test | `ECSUnity/Assets/Tests/Play/CameraNoUnderDeskGuardTests.cs` | Altitude clamp. |
| test | `ECSUnity/Assets/Tests/Play/PerformanceGate30NpcAt60FpsTests.cs` | **30 NPCs at 60 FPS, hardest gate.** |
| test | `ECSUnity/Assets/Tests/Edit/BuildConfigurationTests.cs` | WARDEN/RETAIL define check. |
| test | `ECSUnity/Assets/Tests/Edit/InlineProjectorParityTests.cs` | RETAIL projector parity. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.1.A.md` | Completion note. SimConfig defaults. Unity version pin. Build-step recipe (how `APIFramework.dll` gets into `Plugins/`). FPS measurements (min, mean, p99 — all three exact numbers). Whether `InlineProjector` was implemented or deferred. Any deviations from the spec, especially around Unity version compatibility. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `ECSUnity/` project opens cleanly in Unity 6 LTS without errors. `APIFramework.dll` referenced; namespace resolution works. | manual + edit-mode test |
| AT-02 | `EngineHost.Start` boots the engine without exceptions; entity count after boot equals office-starter spec (≥30 entities including 15 NPCs). | play-mode test |
| AT-03 | Engine ticks deterministically. Over 100 `FixedUpdate` calls, the clock advances exactly 100 ticks. NPC `PositionComponent` values change for at least one NPC. | play-mode test |
| AT-04 | `WorldStateProjectorAdapter.Project` returns a non-null DTO with `npcs.Count == engine.NpcEntities.Count` and `rooms.Count == engine.RoomEntities.Count`. | edit-mode test |
| AT-05 | One `RoomRectangleRenderer` mesh per room in `WorldStateDto.rooms[]`; mesh bounds match room bounds (tolerance ≤ 0.01 world-units). | play-mode test |
| AT-06 | One `NpcDotRenderer` per NPC; renderer position tracks NPC `PositionComponent` over 100 frames; max delta ≤ 0.5 world-units (lerp tolerance acceptable). | play-mode test |
| AT-07 | Camera pan: WASD or arrow input moves camera linearly; left arrow moves camera left; up arrow moves forward (relative to camera angle). | play-mode test |
| AT-08 | Camera rotate: Q/E or right-mouse-drag rotates camera around focus point (lazy-susan); 360° accessible; no axis flip. | play-mode test |
| AT-09 | Camera zoom: scroll-wheel or +/- changes altitude within `[cameraMinAltitude, cameraMaxAltitude]`; cannot exceed bounds. | play-mode test |
| AT-10 | Camera no-under-desk: simulated input that would drop camera below `cameraMinAltitude` clamps; no geometric clipping into desks or floor. | play-mode test |
| AT-11 | **Performance gate.** 30 NPCs office-starter scene; 60 seconds real-time; FPS sampled at 1Hz: min ≥ 55, mean ≥ 58, p99 ≥ 50. **No weakening.** | play-mode test |
| AT-12 | WARDEN scripting define: editor build has `WARDEN` set; `WorldStateProjectorAdapter` resolves to Warden projection. | edit-mode test |
| AT-13 | RETAIL scripting define: removing `WARDEN` and rebuilding resolves projector to `InlineProjector`. | edit-mode test |
| AT-14 | `InlineProjector.Project` produces byte-identical JSON to `Warden.Telemetry.Projectors.WorldStateProjector.Project` on a representative state. | edit-mode test |
| AT-15 | All Phase 0/1/2/3.0.x tests stay green. The Unity scaffold does not modify any engine surface. | regression |
| AT-16 | `dotnet build ECSSimulation.sln` (the existing solution) warning count = 0; existing solution unaffected. | build |
| AT-17 | `dotnet test ECSSimulation.sln` — all green, no exclusions. | build + unit-test |
| AT-18 | Unity Test Runner: all play-mode and edit-mode tests in `ECSUnity/Assets/Tests/` pass. | unity test runner |
| AT-19 | `.gitignore` excludes `Library/`, `Temp/`, `obj/`, `Build/`, `Logs/` from version control. | manual / git status |
| AT-20 | Build produces a Standalone Windows player executable that boots without runtime errors. (Smoke test only — full distribution polish is later.) | manual / build report |

---

## Followups (not in scope)

- **Visual interpolation between engine ticks.** Unity renders 60 FPS; engine ticks ~50/sec. Without interpolation, dot positions visibly stair-step. Cheap interpolation between last-tick and current-tick positions makes motion smooth. Future packet.
- **Custom tick scheduler.** Decouple engine tick rate from Unity's `FixedUpdate`. Allows runtime tick-rate adjustment from the dev console. Future.
- **Multi-threaded engine tick.** When and if profiling justifies it. Single-threaded is correct at v0.1.
- **Multi-floor rendering.** Single-floor at v0.1 per project memory. When multi-floor lands, room renderer extends to handle floor transitions; camera adds floor-switch verb.
- **Standalone build polish.** Windows / Mac / Linux builds with proper installer / icon / metadata. Outside this packet's scope.
- **Anti-aliasing, post-processing, render pipeline choice (URP vs HDRP vs built-in).** v0.1 uses built-in render pipeline for simplicity. Switching to URP for the pixel-art-from-3D rendering style (per aesthetic-bible) is a future packet — likely paired with WP-3.1.B silhouettes.
- **Asset bundle pipeline.** Loading world definitions from disk works for office-starter; future content packs may need bundle infrastructure.
- **Save/load wire-up.** UX bible §3.4 commits to JSON save format; this packet doesn't ship the save/load UI. WP-3.1.E-adjacent or its own packet.
- **Pretty graphics.** Silhouettes (3.1.B), lighting (3.1.C), wall fade-on-occlusion (3.1.C), pixel-art-from-3D shader (post-3.1.C), animation states (3.1.B). All future.
- **Unity Editor tooling.** Custom inspectors for `EntityManager` browse, scenario authoring, replay scrubber. Dev productivity work; future.
- **Audio.** UX bible §3.7 commits to trigger model. Engine emits triggers; Unity host synthesises. First sound packet probably comes after 3.1.E player UI.
