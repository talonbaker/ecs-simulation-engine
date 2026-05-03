# WP-4.0.H — Particle Vocabulary (Steam / Smoke / Sparks)

> **Wave 3 of the Phase 4.0.x foundational polish wave.** Per the 2026-05-02 brief restructure: "Trailing steam from coffee; fire/smoke for the plague-week and fire scenarios that come later; modder-extensible particle registration." Now dispatchable: A1 + A1-INT shipped the URP-native pipeline 2026-05-03; VFX Graph (URP-only) is now available. Track 1.5 packet (engine vocabulary + Unity host VFX) — sandbox + companion `-INT`.

**Tier:** Sonnet
**Depends on:** WP-4.0.A (URP, merged), WP-4.0.A1 (pixel-art Renderer Feature, merged), WP-4.0.A1-INT (pixel-art live, merged), WP-3.2.1 (SoundTriggerBus pattern — parallel structure for visual triggers), WP-3.2.2 (rudimentary physics — sparks emit on physics breakage).
**Parallel-safe with:** WP-4.0.D (floor identity — disjoint), WP-4.0.E (NPC readability — disjoint render scripts), WP-4.0.G1 (build mode undo/redo — disjoint), WP-PT.* (sandbox-first).
**Timebox:** 150 minutes
**Budget:** $0.70
**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN (post-`-INT`):** does steam from coffee read as steam? Do sparks read as sparks when a prop breaks? Is the visual addition diegetic + non-distracting?

---

## Goal

Add a **particle effect vocabulary** that NPCs and props can emit, parallel in design to the audio `SoundTriggerBus` (WP-3.2.1). Engine emits typed triggers (`ParticleTriggerKind.SteamFromCoffee`, `Sparks`, `SmokeFromFire`); Unity host listens via `ParticleTriggerBus`-equivalent and spawns/manages VFX Graph effects at the trigger location.

Initial vocabulary (10 entries):
- `SteamFromCoffee` — gentle wisps rising from a hot mug; loops while mug is hot, fades when cooled.
- `SteamFromFood` — similar to SteamFromCoffee, slightly broader plume; emitted by hot plates.
- `SmokeFromFire` — for fire scenarios (WP-4.2.x territory but vocabulary lands now); thicker, darker, rises faster.
- `Sparks` — emitted on physics breakage (couples to existing `BreakableComponent.HitEnergyThreshold`); brief burst.
- `DustKickedUp` — emitted by NPC walking on dusty floor (basement, supply closet); subtle, fades fast.
- `WaterSplash` — emitted on stain creation (couples to existing slip-and-fall stain mechanic); brief.
- `BulbFlicker` — visual companion to existing `BulbBuzz` audio trigger; subtle on/off light pulse rendered as a small particle near the bulb (also couples to lighting visualization from 3.1.C).
- `CleaningMist` — emitted during chore-completion-clean events (couples to chore rotation from 3.2.3).
- `BreathPuff` — cold-air breath in winter / cold rooms; future-proofing for future temperature variation.
- `SpeechBubblePuff` — small puff timed to speech-fragment events; visual companion to dialog corpus output.

Each VFX Graph asset is authored at the same low-resolution / pixel-art aesthetic as the rest of the visual surface (palette-restricted, simple particle shapes, minimal alpha gradient — must read clearly under the pixel-art shader's down-sample pass).

The `ParticleTriggerBus` is a Mod API extension surface (MAC-012, this packet introduces). Modders adding a sneeze mod append `Sneeze` to the enum, author a sneeze VFX Graph asset, register it in the catalog, and emit from their SneezeSystem — no engine code change beyond the enum addition.

After this packet:
- 10 particle effects authored as VFX Graph assets.
- Engine-side `ParticleTriggerKind` enum + `ParticleTriggerBus` (parallel to `SoundTriggerBus`).
- Unity host `ParticleTriggerSpawner` MonoBehaviour reads the bus, spawns/pools VFX Graph instances at trigger locations.
- A `ParticleTriggerCatalog.json` content file maps `ParticleTriggerKind` to VFX Graph asset path + spawn settings — modder-extensible.
- Sandbox scene fires each particle effect on demand (per-button); Talon validates each reads visually.
- 30-NPCs-at-60-FPS gate holds with up to 10 simultaneous particle effects active (the typical worst case in PlaytestScene).

---

## Reference files

- `docs/UNITY-PACKET-PROTOCOL.md` — sandbox-first per Rule 2.
- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` — MAC-003 (SoundTriggerKind, the parallel pattern), MAC-012 (ParticleTriggerKind — this packet introduces).
- `docs/c2-infrastructure/work-packets/_completed-specs/WP-3.2.1-sound-trigger-bus.md` — read in full. The audio trigger bus is the canonical pattern this packet mirrors for visual triggers.
- `docs/c2-content/aesthetic-bible.md` — pixel-art commitments; particles must read at the shader's down-sample pass.
- `docs/c2-content/ux-ui-bible.md` §3.8 (environmental iconography — stink lines, green fog, dust motes, etc.). Some of those are existing decoupled environmental effects, distinct from event-triggered particles; this packet is the *event-triggered* particle vocabulary.
- `APIFramework/Audio/SoundTriggerKind.cs` and `SoundTriggerBus.cs` — read in full. Pattern source.
- `APIFramework/Components/BreakableComponent.cs` — read for the hit-energy-threshold breakage event that emits `Sparks`.
- `APIFramework/Components/Hazards/StainComponent.cs` — read for the stain creation that emits `WaterSplash`.
- `ECSUnity/Assets/Scripts/Render/Lighting/LightSourceHaloRenderer.cs` — read for context on how flickering bulbs currently render (couples to BulbFlicker particle).
- URP VFX Graph documentation: <https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/index.html> — Sonnet should consult for current API.

---

## Non-goals

- Do **not** ship a complex particle physics system. VFX Graph handles the math; this packet authors triggers + a small set of effects.
- Do **not** add per-NPC particle customization. v0.2 is uniform; per-archetype variation is future depth.
- Do **not** modify existing visual effects (stink lines, green fog, dust motes from §3.8). Those are decoupled environmental effects, not in this packet's scope.
- Do **not** add audio side effects to particle triggers. Audio is the SoundTriggerBus's job; coupling is via parallel emit (engine emits both audio AND visual triggers for events that warrant both — done at the system level, not in the bus itself).
- Do **not** ship a particle pooling layer that's anything beyond Unity's standard VFX Graph instance reuse. Optimization comes when needed.
- Do **not** modify the pixel-art Renderer Feature. Particles render through standard URP and pass through the existing post-process chain.
- Do **not** introduce VFX Graph dependencies that pull in HDRP-only features. URP-compatible VFX Graph only.
- Do **not** modify `WorldStateDto`. Particle emissions are transient events, not persistent state. Save/load doesn't need to round-trip in-flight particles.

---

## Design notes

### Engine-side: `ParticleTriggerKind` + `ParticleTriggerBus`

Direct parallel to audio:

```csharp
public enum ParticleTriggerKind {
    SteamFromCoffee,
    SteamFromFood,
    SmokeFromFire,
    Sparks,
    DustKickedUp,
    WaterSplash,
    BulbFlicker,
    CleaningMist,
    BreathPuff,
    SpeechBubblePuff
}

public class ParticleTriggerBus {
    public event Action<ParticleTriggerEvent> OnTrigger;

    public void Emit(ParticleTriggerKind kind, Vector3 worldPosition, float intensityMult = 1.0f);
}

public record ParticleTriggerEvent(
    ParticleTriggerKind Kind,
    Vector3 WorldPosition,
    float IntensityMult,
    long TickEmitted);
```

Engine systems emit via `ParticleTriggerBus.Emit(...)` at the right tick. The bus dispatches to subscribers in O(subscribers). The Unity host has one subscriber.

### Producers (existing systems wired to emit)

| Trigger | Producer system | When |
|:---|:---|:---|
| `SteamFromCoffee` | (placeholder; couples to a future hot-coffee mechanic) | Stub for now; the trigger exists, emission lands when consumable items are added in a future content packet |
| `SteamFromFood` | (placeholder; same — future hot-food mechanic) | Stub |
| `SmokeFromFire` | (placeholder; emit from `WP-4.2.3` fire scenario) | Stub for v0.2 |
| `Sparks` | `PhysicsBreakageSystem` | When `BreakableComponent.HitEnergy` exceeds threshold and item breaks |
| `DustKickedUp` | `MovementSystem` | When NPC walks on a tile with `DustyTag` (future component; for v0.2, emit only if tag exists) |
| `WaterSplash` | (couples to stain creation in slip-and-fall) | When a new `StainComponent` is created |
| `BulbFlicker` | (couples to existing fluorescent flicker logic from 3.1.C) | When a bulb's flicker state transitions |
| `CleaningMist` | `ChoreCompletionSystem` | When a clean-chore completes (couples to MicrowaveCleaned, FridgeCleaned events) |
| `BreathPuff` | (placeholder; future temperature mechanic) | Stub |
| `SpeechBubblePuff` | (couples to existing speech-fragment emit) | When a speech-fragment fires for a visible NPC |

For v0.2: wire the **emission code** in producer systems where the producing event already exists (Sparks, WaterSplash, BulbFlicker, CleaningMist, SpeechBubblePuff = 5 immediate emitters). The other 5 (SteamFromCoffee, SteamFromFood, SmokeFromFire, DustKickedUp, BreathPuff) ship as enum entries + VFX Graph assets but are NOT yet emitted by any system — they're vocabulary in waiting for the future systems that need them.

The sandbox scene exercises all 10 via on-demand buttons regardless of whether a real producer exists yet.

### Unity-side: `ParticleTriggerSpawner` + catalog

```csharp
public class ParticleTriggerSpawner : MonoBehaviour {
    [SerializeField] ParticleTriggerCatalog _catalog;

    void Start() {
        var bus = EngineHost.Instance.GetService<ParticleTriggerBus>();
        bus.OnTrigger += HandleTrigger;
    }

    void HandleTrigger(ParticleTriggerEvent evt) {
        var entry = _catalog.GetByKind(evt.Kind);
        if (entry == null) return;
        var instance = Instantiate(entry.VfxGraphPrefab, evt.WorldPosition, Quaternion.identity);
        var vfx = instance.GetComponent<VisualEffect>();
        vfx.SetFloat("Intensity", evt.IntensityMult);
        vfx.Play();
        Destroy(instance, entry.LifetimeSeconds);
    }
}
```

The catalog is a `ScriptableObject` referencing `VisualEffectAsset` per `ParticleTriggerKind`. JSON-mirrored for modder modification:

`docs/c2-content/visual/particle-trigger-catalog.json`:

```jsonc
{
  "schemaVersion": "0.1.0",
  "particleTriggers": [
    {"kind": "SteamFromCoffee", "vfxAsset": "VFX/SteamFromCoffee.vfx", "lifetimeSec": 4.0, "defaultIntensity": 1.0},
    {"kind": "SteamFromFood",   "vfxAsset": "VFX/SteamFromFood.vfx",   "lifetimeSec": 5.0, "defaultIntensity": 1.0},
    {"kind": "SmokeFromFire",   "vfxAsset": "VFX/SmokeFromFire.vfx",   "lifetimeSec": 8.0, "defaultIntensity": 1.0},
    {"kind": "Sparks",          "vfxAsset": "VFX/Sparks.vfx",          "lifetimeSec": 1.5, "defaultIntensity": 1.0},
    {"kind": "DustKickedUp",    "vfxAsset": "VFX/DustKickedUp.vfx",    "lifetimeSec": 1.5, "defaultIntensity": 1.0},
    {"kind": "WaterSplash",     "vfxAsset": "VFX/WaterSplash.vfx",     "lifetimeSec": 1.0, "defaultIntensity": 1.0},
    {"kind": "BulbFlicker",     "vfxAsset": "VFX/BulbFlicker.vfx",     "lifetimeSec": 0.3, "defaultIntensity": 1.0},
    {"kind": "CleaningMist",    "vfxAsset": "VFX/CleaningMist.vfx",    "lifetimeSec": 2.0, "defaultIntensity": 1.0},
    {"kind": "BreathPuff",      "vfxAsset": "VFX/BreathPuff.vfx",      "lifetimeSec": 1.0, "defaultIntensity": 1.0},
    {"kind": "SpeechBubblePuff","vfxAsset": "VFX/SpeechBubblePuff.vfx","lifetimeSec": 0.6, "defaultIntensity": 1.0}
  ]
}
```

### VFX Graph asset authoring

Each VFX Graph asset is small. Standard pattern:
- Initialize particles at trigger position.
- Emit ~10-30 particles per burst (per-effect-tuned).
- Velocity: vertical for steam/smoke/dust, radial-burst for sparks/splash.
- Color: palette-restricted (4-8 colors per effect, biased toward effect's nature — white/gray for steam, orange/yellow for sparks, etc.).
- Lifetime: per-particle, seconds-scale (0.5-3s typically).
- Size: small at pixel-art resolution; the down-sample pass will further compress.

Sonnet authors functional placeholders. WP-4.1.2 (final art pipeline) takes them to final-quality if needed; many may stay at functional quality if they read well.

### Camera-proximity attenuation

Particles far from camera don't need to render at full quality. VFX Graph supports per-camera distance-based culling. Apply a default culling distance of ~30m (effects beyond fade out / don't render). Inspector-tunable per-effect in the VFX Graph asset.

### Sandbox scene

`Assets/_Sandbox/particle-vocabulary.unity`:
- A small office room (10×10 tiles, single floor type, simple walls).
- A row of 10 buttons in a sandbox UI panel, one per `ParticleTriggerKind`. Click each to fire the effect at the center of the room.
- A "Fire all" button that fires every effect simultaneously to stress-test rendering.
- A "Loop selected" toggle that re-fires the selected effect every 2 seconds for sustained-effect testing.
- A camera at default 15m altitude over the room center; pan/zoom to validate at different distances.
- A control panel:
  - "Toggle pixel-art" — A/B compare under-shader.
  - Per-altitude buttons.

Test recipe walks Talon through each effect individually + the all-fire stress test + a sustained-effect test.

### Performance

VFX Graph is GPU-accelerated; even 30 simultaneous particle systems with ~30 particles each (~900 active particles) is well within URP+VFX-Graph's batching budget. Verify with `PerformanceGate30NpcWithParticlesTests` (new variant).

If FPS regresses with all 10 effects firing at once: most likely cause is a single misbehaving VFX Graph asset (excessive particle count or expensive shader). Profile in URP Frame Debugger, identify the offender, tune.

### Sandbox vs integration

- **WP-4.0.H (this packet):** sandbox + engine bus + Unity spawner + 10 VFX assets + catalog + producer wiring for the 5 immediate emitters. Production scenes unchanged structurally; producer systems begin emitting (visible particles in PlaytestScene as a side effect of normal play).
- **WP-4.0.H-INT** (companion, drafted later): drop the `ParticleTriggerSpawner` MonoBehaviour into PlaytestScene + MainScene; Talon's hands wire the catalog reference. Small.

Note: producer-system wiring DOES modify systems that run in production scenes. This is acceptable because the production effect is purely additive (particles begin appearing where producer events fire); no breakage. But the host-side spawner needs to be in the scene before particles render visibly, hence the -INT packet.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Visual/ParticleTriggerKind.cs` (new) | Enum (10 entries). |
| code | `APIFramework/Visual/ParticleTriggerBus.cs` (new) | Bus + event record. |
| code | `APIFramework/Systems/SimulationBootstrapper.cs` (modification) | Register the bus as a service. |
| code | (5 producer modifications) | `PhysicsBreakageSystem` (Sparks), stain-creation site (WaterSplash), bulb-flicker site (BulbFlicker), `ChoreCompletionSystem` (CleaningMist), speech-fragment emit site (SpeechBubblePuff). |
| code | `ECSUnity/Assets/Scripts/Render/Visual/ParticleTriggerSpawner.cs` (new) | MonoBehaviour subscribing to the bus + spawning VFX. |
| code | `ECSUnity/Assets/Scripts/Render/Visual/ParticleTriggerCatalog.cs` (new) | ScriptableObject + JSON loader. |
| asset | `ECSUnity/Assets/Settings/DefaultParticleTriggerCatalog.asset` (new) | The catalog asset. |
| vfx | `ECSUnity/Assets/VFX/SteamFromCoffee.vfx` and 9 others (10 total) | VFX Graph assets per `ParticleTriggerKind`. |
| data | `docs/c2-content/visual/particle-trigger-catalog.json` (new) | JSON mirror. |
| scene | `ECSUnity/Assets/_Sandbox/particle-vocabulary.unity` | Sandbox per Rule 4. |
| doc | `ECSUnity/Assets/_Sandbox/particle-vocabulary.md` | 10-15 minute test recipe. |
| test | `APIFramework.Tests/Visual/ParticleTriggerBusTests.cs` | Bus emit + subscriber dispatch. |
| test | `APIFramework.Tests/Visual/ParticleTriggerCatalogJsonTests.cs` | JSON validation. |
| test | `ECSUnity/Assets/Tests/Edit/ParticleTriggerSpawnerSubscriptionTests.cs` | Spawner subscribes/unsubscribes correctly. |
| test | `ECSUnity/Assets/Tests/Play/ProducerEmissionTests.cs` | Each of the 5 immediate producers emits the right kind on the right event. |
| test | `ECSUnity/Assets/Tests/Play/PerformanceGate30NpcWithParticlesTests.cs` | FPS gate with all 10 effects firing. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Confirm MAC-012 entry post-implementation; bump MAC-003 (SoundTriggerKind) to *stable* now that the parallel pattern is well-established. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `ParticleTriggerBus.Emit` dispatches to subscribers; subscribers can be added/removed. | unit-test |
| AT-02 | All 10 enum values have catalog entries pointing at valid VFX Graph assets. | unit-test |
| AT-03 | Each of the 5 immediate producers emits the correct trigger on the correct event. | integration-test |
| AT-04 | Each of 10 VFX Graph assets renders correctly when fired in sandbox. | manual visual |
| AT-05 | Each effect reads as the intended phenomenon (steam reads as steam, sparks read as sparks, etc.) at default 15m altitude under pixel-art shader. | manual visual |
| AT-06 | "Fire all" simultaneous test does not blow FPS gate. | play-mode test |
| AT-07 | Sustained-effect test (looped emit) holds steady FPS for ~30 seconds. | play-mode test |
| AT-08 | Camera-distance fallout works (effects fade beyond ~30m culling distance). | manual visual |
| AT-09 | All Phase 0–3 + Phase 4.0.A/A1/A1-INT/B/C/F/G tests stay green. | regression |
| AT-10 | `dotnet build` warning count = 0; all tests green. | build + test |
| AT-11 | MAC-012 entry in `MOD-API-CANDIDATES.md` confirms shipped state; MAC-003 bumped to *stable*. | review |

---

## Mod API surface

This packet **realizes MAC-012** (Particle effect vocabulary, previously *fresh* / pending). Bumps to *fresh* with one consumer (this packet); will graduate to *stabilizing* when a second consumer (e.g., a future fire scenario emits its own custom `ParticleTriggerKind` value via the same bus pattern).

This packet also gives MAC-003 (SoundTriggerKind) a parallel-pattern peer, which strengthens the case for graduating MAC-003 to *stable* — two parallel buses with the same shape demonstrates the pattern is settled.

The catalog JSON is a Mod API extension surface: modders adding new particle effects extend the JSON + author a new VFX Graph asset; engine code only requires the enum addition. (When the formal Mod API ships, the enum opens to a registry to remove even that constraint.)

---

## Followups (not in scope)

- `WP-4.0.H-INT` — wire `ParticleTriggerSpawner` into PlaytestScene + MainScene. Talon's hands.
- Consumable items (hot coffee, hot food) that fire `SteamFromCoffee` / `SteamFromFood`. Future content packets.
- Fire/disaster scenario (WP-4.2.3) that fires `SmokeFromFire` extensively. Already on the roadmap.
- Temperature mechanic that fires `BreathPuff`. Future depth.
- Per-effect tuning UI / presets. Future tooling if needed.
- Particle effects coupling with audio for synchronized AV effects (sparks + crash sound). Already supported via parallel emission; documented usage pattern in followups.
- Final art pipeline pass on VFX Graph assets. WP-4.1.2.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

Track 2 sandbox packet (with engine-side bus integration). Visual verification by Talon required.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm worktree at `.claude/worktrees/sonnet-wp-4.0.h/` on branch `sonnet-wp-4.0.h` based on recent `origin/staging` (which now includes URP + pixel-art live).
1. Implement the spec.
2. Run all Unity tests + `dotnet test`. All must stay green.
3. Stage all changes including self-cleanup.
4. Commit on the worktree's feature branch.
5. Push the branch.
6. Stop. Notify Talon: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/particle-vocabulary.md (10-15 min recipe)`.

### Feel-verified-by-playtest acceptance flag

**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN (post-`-INT`):** does steam from coffee read as steam? Sparks as sparks? Are the effects diegetic and non-distracting in normal play?

### Cost envelope

Target: **$0.70**. Engine bus + Unity spawner + 10 VFX Graph assets + 5 producer wirings. If cost approaches $1.20, escalate via `WP-4.0.H-blocker.md`.

Cost-discipline:
- Don't author final-quality VFX Graph assets — functional placeholders that read clearly are the bar.
- Reuse the SoundTriggerBus pattern verbatim where applicable; don't reinvent.
- Don't try to make the 5 stub-only triggers actually emit from real producers; that's future content packet territory.

### Self-cleanup on merge

Standard. Check for `WP-4.0.H-INT` as likely dependent.
