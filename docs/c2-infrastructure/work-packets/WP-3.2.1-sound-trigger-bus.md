# WP-3.2.1 — Sound Trigger Bus (engine-side)

> **DO NOT DISPATCH UNTIL ALL OF PHASE 3.1.x IS MERGED.**
> The Unity host (3.1.A through 3.1.H) consumes engine-emitted sound triggers; this packet supplies them. Engine ships triggers; host synthesises actual audio.

**Tier:** Sonnet
**Depends on:** Phase 0/1/2 (system surface for emission), WP-3.0.0 (LifeStateGuard), Phase 3.1.x (host that consumes)
**Parallel-safe with:** WP-3.2.0 (save/load — disjoint surface)
**Timebox:** 110 minutes
**Budget:** $0.50

---

## Goal

The UX/UI bible §3.7 commits to a trigger-based audio model: engine emits sound *triggers* (Cough, ChairSqueak, BulbBuzz, etc.); the host (Unity) synthesises actual audio with proximity attenuation. After this packet:

- A new `SoundTriggerBus` (mirroring `NarrativeEventBus`) is registered as a singleton service.
- A `SoundTriggerKind` enum covers the v0.1 vocabulary: `Cough`, `Gasp`, `Wheeze`, `Footstep`, `ChairSqueak`, `KeyboardClack`, `MicrowaveDing`, `FridgeHum`, `PrinterChug`, `ElevatorDing`, `PhoneRing`, `FaxChug`, `EmailBeep`, `BulbBuzz`, `OfficeAmbient`, `SpeechFragment`, `Sneeze`, `Yawn`, `Sigh`, `Crash`, `Glass`, `Slip`, `Thud`, `Slurp`, `Chew`, `Burp`.
- Engine systems emit triggers at appropriate moments (e.g., `ChokingDetectionSystem` emits Cough+Gasp at choke onset; `MovementSystem` emits Footstep per-tile-cross; `LightSourceStateSystem` emits BulbBuzz periodically while a fluorescent is flickering).
- Sound triggers carry: kind, source entity, source position, intensity (0..1), and a deterministic `Tick` stamp + monotonic `SequenceId`.
- WARDEN/RETAIL split-friendly: the bus exists in both builds (game logic emits regardless); only the host's audio pipeline differs.
- Determinism preserved: emissions happen in deterministic engine order.

---

## Reference files

- `docs/c2-content/ux-ui-bible.md` §3.7 — audio surface commitments.
- `docs/c2-infrastructure/00-SRD.md` §8.1, §8.7.
- `APIFramework/Systems/Narrative/NarrativeEventBus.cs` — pattern template.
- `APIFramework/Systems/Spatial/ProximityEventBus.cs` — alternative template.
- `APIFramework/Systems/Lighting/LightSourceStateSystem.cs` — produces `LightState.Flickering`.
- `APIFramework/Systems/MovementSystem.cs`, `Movement/StepAsideSystem.cs` — Footstep emission.
- `APIFramework/Systems/EsophagusSystem.cs` — Chew + Slurp.
- `APIFramework/Systems/LifeState/ChokingDetectionSystem.cs` — Cough + Gasp at choke.
- `APIFramework/Systems/LifeState/SlipAndFallSystem.cs` — Slip + Thud at slip.

---

## Non-goals

- Do **not** ship per-archetype voice profile generation. Future packet.
- Do **not** ship the host-side audio synthesis. Unity-side; already in 3.1.x scope.
- Do **not** ship music / score / non-diegetic audio. Trigger model is for diegetic only.
- Do **not** ship audio mixing / mastering / per-channel routing. UX §3.7 audio sliders are 3.1.E.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or self-heal.

---

## Design notes

### `SoundTriggerKind` enum

```csharp
public enum SoundTriggerKind
{
    Cough = 0, Gasp = 1, Wheeze = 2, Sneeze = 3, Yawn = 4, Sigh = 5,
    Slurp = 10, Chew = 11, Burp = 12,
    Footstep = 20, ChairSqueak = 21, KeyboardClack = 22, ElevatorDing = 23,
    Slip = 30, Thud = 31, Crash = 32, Glass = 33,
    MicrowaveDing = 40, FridgeHum = 41, PrinterChug = 42, PhoneRing = 43,
    FaxChug = 44, EmailBeep = 45, BulbBuzz = 46,
    OfficeAmbient = 50,
    SpeechFragment = 60,
}
```

Additive only.

### `SoundTriggerEvent`

```csharp
public readonly record struct SoundTriggerEvent(
    SoundTriggerKind Kind,
    Guid             SourceEntityId,
    float            SourceX,
    float            SourceZ,
    float            Intensity,    // 0..1
    long             Tick,
    long             SequenceId
);
```

### `SoundTriggerBus`

Singleton mirroring `ProximityEventBus`:

```csharp
public sealed class SoundTriggerBus
{
    private readonly List<Action<SoundTriggerEvent>> _subscribers = new();
    private long _sequenceId;

    public void Subscribe(Action<SoundTriggerEvent> handler) => _subscribers.Add(handler);

    public void Emit(SoundTriggerKind kind, Guid sourceEntity, float x, float z, float intensity, long tick)
    {
        var ev = new SoundTriggerEvent(kind, sourceEntity, x, z,
            Math.Clamp(intensity, 0f, 1f), tick, ++_sequenceId);
        foreach (var h in _subscribers) h(ev);
    }
}
```

### Emission integration

| System | Trigger | When |
|:---|:---|:---|
| `MovementSystem` | `Footstep` | Per-NPC, when `PositionComponent` advances ≥ 1 tile. |
| `Movement/StepAsideSystem` | `ChairSqueak` | Per step-past-chair event. |
| `EsophagusSystem` | `Chew`, `Slurp` | Per-bolus-advance. |
| `BiologicalConditionSystem` | `Sneeze`, `Yawn`, `Sigh` | Per emission. |
| `ChokingDetectionSystem` | `Cough`, `Gasp` | At choke onset (one-shot). |
| `LifeStateTransitionSystem` | `Wheeze` | Each tick during `IsChokingTag`. |
| `Lighting/LightSourceStateSystem` | `BulbBuzz` | Every `bulbBuzzEmitIntervalTicks` for `Flickering` sources. |
| `SlipAndFallSystem` | `Slip`, `Thud` | At slip event. |
| `Dialog/DialogFragmentRetrievalSystem` | `SpeechFragment` | Per fragment. Intensity by register. |
| `LifeStateTransitionSystem` (Deceased) | `Thud` | At Deceased transition. |
| `InteractionSystem` | per-object kinds | At interaction events. |

Each integration: receive `SoundTriggerBus` via constructor, emit at the right moment.

### Determinism

All emissions in deterministic engine tick order. `SequenceId` monotonic per-emit. Same-seed = byte-identical sequence.

### WARDEN/RETAIL parity

Bus exists in both. Engine emits unconditionally. WARDEN host consumes via JSONL stream emitter (3.1.F) AND in-process subscription. RETAIL host: in-process subscription only.

### Tests

- `SoundTriggerBusEmitTests.cs` — emit; subscriber receives.
- `SoundTriggerBusSequenceIdMonotonicTests.cs` — sequence increments.
- `SoundTriggerBusMultiSubscriberTests.cs` — N subscribers all receive.
- `MovementSystemFootstepEmitTests.cs` — NPC moves 1 tile → Footstep emitted.
- `EsophagusSystemChewEmitTests.cs` — bolus advances → Chew.
- `ChokingDetectionSystemCoughGaspEmitTests.cs` — choke onset → Cough + Gasp.
- `LifeStateIncapacitationWheezeEmitTests.cs` — each tick during Incap → Wheeze.
- `LightSourceFlickerBuzzEmitTests.cs` — Flickering source → BulbBuzz at cadence.
- `SlipAndFallSlipThudEmitTests.cs` — slip event → Slip + Thud.
- `DialogSpeechFragmentEmitTests.cs` — fragment → SpeechFragment with register-scaled intensity.
- `SoundTriggerDeterminismTests.cs` — 5000-tick run two seeds: byte-identical sequence.
- `SoundTriggerWardenRetailParityTests.cs` — both builds produce same engine-side sequence.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Systems/Audio/SoundTriggerKind.cs` | Enum. |
| code | `APIFramework/Systems/Audio/SoundTriggerEvent.cs` | Record. |
| code | `APIFramework/Systems/Audio/SoundTriggerBus.cs` | Bus singleton. |
| code | `APIFramework/Systems/Audio/SoundTriggerConfig.cs` | Tunables. |
| code | (modifications across ~10-15 systems per integration table) | Each gains a SoundTriggerBus reference and 1-2 emit calls. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `SoundTriggerBus`. |
| code | `SimConfig.cs` (modified) | `SoundTriggerConfig` class + property. |
| config | `SimConfig.json` (modified) | New `soundTriggers` section. |
| test | (~12 test files per Tests section) | Bus + integrations + determinism. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.2.1.md` | Completion note. Integration table; SimConfig defaults. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `SoundTriggerKind`, `SoundTriggerEvent`, `SoundTriggerBus` compile and instantiate. | unit-test |
| AT-02 | `SoundTriggerBus.Emit(Cough, ...)` → subscribers receive event with all fields populated. | unit-test |
| AT-03 | `SequenceId` monotonically increments by 1 per emit. | unit-test |
| AT-04 | NPC moves 1 tile → `MovementSystem` emits Footstep with correct entity ID and position. | integration-test |
| AT-05 | NPC begins choking → `ChokingDetectionSystem` emits Cough + Gasp (one-shot). | integration-test |
| AT-06 | NPC `IsChokingTag` for 100 ticks → Wheeze emitted each tick (100 emissions). | integration-test |
| AT-07 | Flickering bulb for 5 game-seconds → BulbBuzz at configured interval. | integration-test |
| AT-08 | Slip event → Slip + Thud emitted. | integration-test |
| AT-09 | Speech fragment → SpeechFragment with register-scaled intensity. | integration-test |
| AT-10 | Determinism: 5000 ticks two seeds → byte-identical event sequence. | integration-test |
| AT-11 | All Phase 0/1/2/3.0.x/3.1.x tests stay green. | regression |
| AT-12 | `dotnet build` warning count = 0; `dotnet test` all green. | build + test |

---

## Followups (not in scope)

- Per-archetype voice profile generation. Future packet (Phase 4.1.0).
- Music / score / ambient atmospheric. Non-diegetic; future content.
- Sound trigger volume per-archetype. Future tuning.
- Sound emission rate-limiting (engine-side). Future polish.
- Trigger composition (Cough+Wheeze as composite). v0.1 emits separate triggers.
