# WP-2.4.A — Stress System

**Tier:** Sonnet
**Depends on:** WP-1.4.A (willpower component + `WillpowerEventQueue`), WP-2.1.A (action-selection scaffold — the system that produces ambient suppression events stress will amplify)
**Parallel-safe with:** WP-2.0.C (different project), WP-2.2.A (different component surface), WP-2.3.A (different system surface)
**Timebox:** 90 minutes
**Budget:** $0.40

---

## Goal

Add the slow accumulator that depletes willpower over days. Phase 1 shipped willpower as a per-NPC depleting-and-regenerating reservoir; Phase 2's action-selection ships the consumer that drains it on suppressed actions. Without stress, willpower mostly tracks short-horizon "this hour" pressure. With stress, willpower tracks a longer arc — the breakdown timeline the bibles promise.

Stress is cortisol-like: it accumulates from sustained drive elevation, social conflict, work pressure (when the workload system ships), and chronic suppression (action-selection's deferred high-drive actions). It doesn't drain like a drive; it builds and then takes days to dissipate. High stress amplifies everything in the engine — drive drift accelerates, willpower regen slows, mood volatility rises. The arc the player sees: an NPC under stress for several game-days slowly becomes a different version of themselves until either the stressor lifts or they break.

This is the third of three Wave 2 systems building on WP-2.1.A. Schedule (WP-2.2.A) and memory recording (WP-2.3.A) are the others. Together with WP-2.1.A's action-selection seam, stress closes the breakdown loop:

> ActionSelection chooses to suppress a drive → willpower depletes (existing) → if suppression is sustained, stress accumulates (this packet) → high stress amplifies drive volatility (this packet) → drives push harder against the gate → ActionSelection chooses to suppress more (or breaks through) → ...

The amplification at the end is where the bibles' "stress diarrhea before the big meeting" and "the all-nighter" come from. v0.1 wires the loop's structure; the dramatic outputs come once stress integrates with workload (a later packet).

---

## Reference files

- `docs/c2-content/new-systems-ideas.md` — **read first.** Section 1 (StressSystem + StressComponent) is the design source for this packet. The biological knock-on effects listed there (hunger drain accelerates, sleep harder, emotion decay slows) are the v0.1 amplification surface.
- `docs/c2-content/action-gating.md` — willpower / suppression / inhibitions. Stress depletes willpower indirectly (via amplifying suppression event magnitude); it never bypasses the existing willpower path.
- `docs/c2-content/cast-bible.md` — archetypes vary in stress baseline. The Cynic has low baseline stress (gives no fucks); the Newbie is high baseline (everything is overwhelming); the Recovering is volatile.
- `docs/c2-content/world-bible.md` — gameplay loop. Stress is the *reason* "orders come in, nobody told you" matters at the human level.
- `docs/c2-infrastructure/00-SRD.md` §8.5 (social state is first-class).
- `docs/c2-infrastructure/SCHEMA-ROADMAP.md` — confirm whether stress gets a wire-format surface at v0.5 or later. **At v0.4 there is no `stress` field on the entity DTO.** This packet leaves the wire surface untouched; engine-side state only. Schema bump deferred to a follow-up packet once the design is playtest-validated.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.4.A.md` — the willpower model + event queue this packet integrates with. Section "Willpower depletion and regeneration" is essential.
- `docs/c2-infrastructure/work-packets/_completed/WP-2.1.A.md` — the action-selection scaffold producing `SuppressionTick` events stress reads.
- `APIFramework/Components/SocialDrivesComponent.cs` — the eight drives that contribute to stress (volatility on Irritation, Suspicion, Loneliness, etc.).
- `APIFramework/Components/WillpowerComponent.cs` — depleting reservoir; stress amplifies depletion magnitude, not the regen path directly.
- `APIFramework/Components/PersonalityComponent.cs` — Neuroticism feeds into stress sensitivity (high-neuroticism NPCs gain stress faster, retain it longer). Read this to know the trait surface to consume.
- `APIFramework/Systems/WillpowerEventQueue.cs` + `WillpowerEventSignal.cs` — the queue. Stress is a *consumer* of `SuppressionTick` events (reads them as a signal of sustained suppression) AND a *producer* of additional `SuppressionTick` events when chronic stress amplifies pressure.
- `APIFramework/Systems/WillpowerSystem.cs` — currently drains the queue and applies deltas. Read the existing flow; do not modify (this packet adds a new system, doesn't extend WillpowerSystem).
- `APIFramework/Systems/DriveDynamicsSystem.cs` — the drive volatility computation. Stress amplifies the volatility scale; the amplification is read by DriveDynamicsSystem from a new field on `SimConfig` plus the per-NPC stress level. Modification is one line in the volatility formula.
- `APIFramework/Components/Tags.cs` — add `StressedTag`, `OverwhelmedTag`, `BurningOutTag` per the new-systems-ideas doc.
- `APIFramework/Core/SystemPhase.cs` — `Cognition = 30` exists. Stress runs at... see Design notes (probably `Condition = 20` to write stress before ActionSelection reads it, or `Cleanup` after WillpowerSystem if it consumes events from the queue).
- `APIFramework/Core/SimulationBootstrapper.cs` — register the new system.
- `APIFramework/Core/SimulationClock.cs` — `TotalTime` is the in-game absolute clock; `DayNumber` is the day index. Stress decay is per-game-day, not per-tick; use `DayNumber` to gate the slow-decay pass.
- `APIFramework/Core/SeededRandom.cs` — for any tie-breaking. No `System.Random`.
- `SimConfig.json` — runtime tuning lives here.

## Non-goals

- Do **not** modify `WillpowerComponent`, `WillpowerSystem`, or the willpower event queue's shape. Stress is a peer producer/consumer; it doesn't restructure the willpower model.
- Do **not** modify `WorldStateDto`, `EntityDto`, or any DTO. Wire-format addition for stress is a separate packet timed with the v0.5 schema bump (TBD on the roadmap). Engine-side state only.
- Do **not** modify the `TelemetryProjector`. It will continue to ignore stress; that's correct until the schema bump.
- Do **not** add `WorkloadComponent` or any task/deadline scaffolding. WP-2.6.A will wire workload as a stress *source*; this packet ships only the accumulator and the amplification, with the existing internal sources (sustained drive elevation, suppression frequency).
- Do **not** add `BurningOutTag` *behaviour* (the bible's "stop performing the mask, start making mistakes"). The tag fires when the threshold is crossed, but no system reads it yet — that's a follow-up. v0.1 makes the tag observable; tag-driven behaviour is later.
- Do **not** modify `EatingSystem`, `SleepSystem`, `BladderSystem`, or any physiology system to "react to stress." The bible flags those couplings as desirable, but they're separate packets — keeping this one tight.
- Do **not** add player-facing stress UI, debug overlays, or per-NPC stress inspection. Engine-internal at v0.1; observability comes with the eventual wire-format bump.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (SRD §8.1.)
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### The new component

```csharp
/// <summary>
/// Cortisol-like stress accumulator. Slow to build, slow to dissipate.
/// Three numbers: today's level, a 7-day rolling baseline, and a tally of recent sources.
/// </summary>
public struct StressComponent
{
    public int    AcuteLevel;           // 0..100; today's accumulated stress
    public double ChronicLevel;         // 0..100; rolling 7-day average of AcuteLevel
    public int    LastDayUpdated;       // SimulationClock.DayNumber at last chronic update
    // Sources tally — counts since last decay pass; resets daily.
    public int    SuppressionEventsToday;
    public int    DriveSpikeEventsToday;
    public int    SocialConflictEventsToday;
}
```

`AcuteLevel` rises with sources during the day; decays a small amount per tick; gets averaged into `ChronicLevel` once per game-day (when `DayNumber` advances).

`ChronicLevel` is what `BurningOutTag` keys off (high chronic = burnout; the tag is the long-arc signal). `AcuteLevel` is what `StressedTag` and `OverwhelmedTag` key off (acute spikes today).

### The new system

`StressSystem` runs at `Cleanup` phase (after WillpowerSystem has drained and processed its queue, after action-selection has emitted, after drive-dynamics has run). The order matters: stress reads what willpower processed *this tick* and applies to next-tick effects.

Per tick, for each NPC with `StressComponent` (every NPC has one — added at spawn alongside other social components):

1. **Read sources from this tick.**
   - **Suppression events processed:** scan the current tick's drained `WillpowerEventQueue` events; count `SuppressionTick` entries for this NPC. Each contributes `+suppressionStressGain` to `AcuteLevel`.
   - **Drive spikes:** any drive whose `Current - Baseline > driveSpikeStressDelta` (default 25) contributes `+driveSpikeStressGain` per drive. Counts toward `DriveSpikeEventsToday`.
   - **Social conflict signals:** if the narrative bus emitted an `Argument` / `AbruptDeparture` / `ConflictSpike` candidate involving this NPC this tick, contribute `+socialConflictStressGain`. (Subscribe to the bus the same way `MemoryRecordingSystem` will.)

2. **Apply per-tick decay.**
   - `AcuteLevel -= acuteDecayPerTick` (default 0.05; small).

3. **Per-day chronic update.** When `clock.DayNumber > LastDayUpdated`:
   - `ChronicLevel = (ChronicLevel × 6 + AcuteLevel) / 7` (rolling 7-day mean, single-update form).
   - Reset `SuppressionEventsToday`, `DriveSpikeEventsToday`, `SocialConflictEventsToday` to 0.
   - Update `LastDayUpdated = clock.DayNumber`.

4. **Tag updates.**
   - `StressedTag` present iff `AcuteLevel ≥ stressedTagThreshold` (default 60).
   - `OverwhelmedTag` present iff `AcuteLevel ≥ overwhelmedTagThreshold` (default 85).
   - `BurningOutTag` present iff `ChronicLevel ≥ burningOutTagThreshold` (default 70). Once present, `BurningOutTag` is sticky for `burningOutCooldownDays` (default 3) — burnout doesn't end the moment a stressful day passes.

5. **Push amplification events.** When `AcuteLevel ≥ stressedTagThreshold`, push one extra `SuppressionTick` event per tick into `WillpowerEventQueue` with magnitude `stressAmplificationMagnitude` (default 1, scaled by `(AcuteLevel - stressedTagThreshold) / (100 - stressedTagThreshold)`). This is the loop closure: stress → extra willpower drain → eventually willpower runs low → existing system handles the breakthrough.

6. **Amplify drive volatility.** This requires a tiny modification to `DriveDynamicsSystem`'s volatility computation:

```csharp
// in DriveDynamicsSystem (modified):
var stressMultiplier = npc.Has<StressComponent>()
    ? 1.0 + npc.Get<StressComponent>().AcuteLevel / 100.0 * config.StressVolatilityScale
    : 1.0;
volatility *= stressMultiplier;   // existing volatility * (1.0 .. 1.0 + scale)
```

Default `stressVolatilityScale = 0.5`: max stress doubles drive volatility. The amplification is bounded; it doesn't make the engine explode.

### Personality coupling

`PersonalityComponent.Neuroticism` feeds into stress sensitivity:

```csharp
// Per-NPC stress gain multiplier:
var neuroFactor = 1.0 + neuroticism * neuroticismStressFactor;   // -2 .. +2 → 0.6 .. 1.4
```

The Cynic (low neuroticism) accumulates stress slowly. The Recovering (high neuroticism) accumulates fast and retains. Default `neuroticismStressFactor = 0.2`.

### Stress component spawn-time defaults

The cast generator (WP-1.8.A) doesn't yet add `StressComponent` because it doesn't exist. WP-2.4.A adds spawn-time injection: a small `StressInitializerSystem` runs once at boot for every NPC without `StressComponent`, attaching one with:

- `AcuteLevel = 0` (starts the day fresh)
- `ChronicLevel` = a per-archetype baseline (Cynic 20, Vent 50, Recovering 60, Hermit 30, Climber 50, Newbie 40, Old Hand 30, Affair 60, Founder's Nephew 25, Crush 35) — the Sonnet authors these from the cast bible's archetype descriptions
- `LastDayUpdated = 0`

The per-archetype baselines live in `archetype-stress-baselines.json` (small file, 10 entries) under `docs/c2-content/`. Same parallel as `archetype-schedules.json` from WP-2.2.A — keeps the existing `archetypes.json` untouched.

### SimConfig additions

```jsonc
{
  "stress": {
    "suppressionStressGain":          1.5,
    "driveSpikeStressDelta":          25,
    "driveSpikeStressGain":           2.0,
    "socialConflictStressGain":       3.0,
    "acuteDecayPerTick":              0.05,
    "stressedTagThreshold":           60,
    "overwhelmedTagThreshold":        85,
    "burningOutTagThreshold":         70,
    "burningOutCooldownDays":         3,
    "stressAmplificationMagnitude":   1.0,
    "stressVolatilityScale":          0.5,
    "neuroticismStressFactor":        0.2
  }
}
```

### Determinism

Stress is deterministic: same seeds + same world + same tick-count → byte-identical `StressComponent` state. The 5000-tick test from WP-2.1.A's family is the determinism anchor.

### Tests

- `StressComponentTests.cs` — construction, clamping (`AcuteLevel` 0..100, `ChronicLevel` 0..100), equality.
- `StressInitializerSystemTests.cs` — every NPC with `NpcArchetypeComponent` and no `StressComponent` gets one with the matching archetype baseline.
- `StressSystemTests.cs` — sources increase `AcuteLevel`; per-tick decay reduces it; cap at 0..100; per-day chronic update averages correctly; tag transitions fire at the configured thresholds.
- `StressBurningOutStickyTests.cs` — `BurningOutTag` persists for `burningOutCooldownDays` after `ChronicLevel` drops below threshold.
- `StressDriveVolatilityTests.cs` — modified `DriveDynamicsSystem` produces higher per-tick variance for an NPC with `AcuteLevel = 80` than the same NPC at `AcuteLevel = 0`. Significant difference at p<0.01 over 5000 ticks.
- `StressWillpowerLoopTests.cs` — sustained suppression events build stress; once `AcuteLevel ≥ stressedTagThreshold`, the system pushes additional suppression events that drain willpower further. Loop closes correctly.
- `StressNeuroticismCouplingTests.cs` — neuroticism +2 NPC gains stress 1.4× faster than neuroticism -2 NPC under identical input streams.
- `StressDeterminismTests.cs` — 5000-tick run, two seeds with the same world: byte-identical stress state.
- `ArchetypeStressBaselinesJsonTests.cs` — file loads; all 10 archetypes present; baselines in 0..100.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/StressComponent.cs` | The component per Design notes. |
| code | `APIFramework/Components/Tags.cs` (modified) | Add `StressedTag`, `OverwhelmedTag`, `BurningOutTag`. |
| code | `APIFramework/Systems/StressSystem.cs` | The per-tick + per-day update logic. |
| code | `APIFramework/Systems/StressInitializerSystem.cs` | Spawn-time injection per archetype baseline. |
| code | `APIFramework/Systems/DriveDynamicsSystem.cs` (modified) | Add the stress-volatility multiplier (one-line conditional). |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `StressSystem` (Cleanup phase) and `StressInitializerSystem` (boot/first-tick). |
| code | `APIFramework/Config/SimConfig.cs` (modified) | `StressConfig` class + property. |
| code | `SimConfig.json` (modified) | Add `stress` section. |
| data | `docs/c2-content/archetypes/archetype-stress-baselines.json` | 10 archetypes with chronic baseline values. |
| code | `APIFramework.Tests/Components/StressComponentTests.cs` | Construction, clamping. |
| code | `APIFramework.Tests/Systems/StressInitializerSystemTests.cs` | Spawn-time baseline injection. |
| code | `APIFramework.Tests/Systems/StressSystemTests.cs` | Source accumulation, per-tick decay, per-day chronic update, tag transitions. |
| code | `APIFramework.Tests/Systems/StressBurningOutStickyTests.cs` | Sticky burnout cooldown. |
| code | `APIFramework.Tests/Systems/StressDriveVolatilityTests.cs` | Stress amplifies drive volatility (statistical test). |
| code | `APIFramework.Tests/Systems/StressWillpowerLoopTests.cs` | Stress pushes suppression events; loop closes. |
| code | `APIFramework.Tests/Systems/StressNeuroticismCouplingTests.cs` | Neuroticism scales stress accumulation. |
| code | `APIFramework.Tests/Systems/StressDeterminismTests.cs` | 5000-tick byte-identical state. |
| code | `APIFramework.Tests/Data/ArchetypeStressBaselinesJsonTests.cs` | JSON validation. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-2.4.A.md` | Completion note. Standard template. List the 10 archetype baselines the Sonnet committed to; SimConfig defaults that survived; whether the chronic-update formula matched the design (single-pass rolling mean) or required adjustment for stability. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `StressComponent` compiles, instantiates, clamps `AcuteLevel` and `ChronicLevel` to 0..100, round-trips equality. | unit-test |
| AT-02 | `StressInitializerSystem` attaches `StressComponent` to every NPC with `NpcArchetypeComponent` and no existing component; the `ChronicLevel` matches the archetype baseline from the JSON. | unit-test |
| AT-03 | A `SuppressionTick` event for NPC X this tick increments `X.StressComponent.AcuteLevel` by `suppressionStressGain × (1 + Neuroticism × neuroticismStressFactor)`. | unit-test |
| AT-04 | Per-tick decay reduces `AcuteLevel` by `acuteDecayPerTick` (clamped at 0). | unit-test |
| AT-05 | Per-day chronic update on `DayNumber` advance correctly averages: `ChronicLevel = (ChronicLevel × 6 + AcuteLevel) / 7`; daily counters reset. | unit-test |
| AT-06 | `StressedTag` present iff `AcuteLevel ≥ stressedTagThreshold`; `OverwhelmedTag` iff `≥ overwhelmedTagThreshold`. Tags transition cleanly across the threshold each tick. | unit-test |
| AT-07 | `BurningOutTag` present iff `ChronicLevel ≥ burningOutTagThreshold`; sticky for `burningOutCooldownDays` after `ChronicLevel` drops below threshold. | unit-test |
| AT-08 | When `AcuteLevel ≥ stressedTagThreshold`, exactly one extra `SuppressionTick` event lands in `WillpowerEventQueue` per tick with magnitude scaling per Design notes. | unit-test |
| AT-09 | `DriveDynamicsSystem` produces higher per-tick drive variance for an NPC with `AcuteLevel = 80` than the same NPC at `AcuteLevel = 0` — significant difference at p<0.01 over 5000 ticks. | unit-test |
| AT-10 | Neuroticism coupling: neuroticism +2 NPC accumulates stress 1.4× faster than neuroticism -2 NPC under identical input streams. | unit-test |
| AT-11 | Determinism: 5000-tick run, two seeds with the same world: byte-identical stress state across runs. | unit-test |
| AT-12 | `archetype-stress-baselines.json` loads cleanly; all 10 archetypes present; baselines in 0..100. | unit-test |
| AT-13 | All WP-1.x and WP-2.1.A acceptance tests stay green. | regression |
| AT-14 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-15 | `dotnet test ECSSimulation.sln --filter "FullyQualifiedName!~RunCommandEndToEndTests.AT01"` — every existing test stays green; new tests pass. | build |

---

## Followups (not in scope)

- **Stress wire-format surface (v0.5 schema bump).** Add `entities[].social.stress: { acute, chronic, sources }` and the three stress tags to the entity DTO. Done as part of the next major schema version (TBD on roadmap).
- **Stress as a workload modulator.** When workload (WP-2.6.A) ships, overdue tasks become a stress source; high stress reduces work-progress rate. Loop closes the gameplay-relevant arc.
- **Burnout behaviour.** `BurningOutTag` should change behaviour: stop performing the social mask (when WP-2.5.A ships), reduced status-seeking, increased likelihood of abrupt departures. Tag-driven behaviour packet, deferred.
- **Stress-driven physiology.** Per `new-systems-ideas.md`: high stress → bladder/colon urgency thresholds drop ("stress diarrhea before the big meeting"); sleep wake-threshold raises (4am wake-ups under chronic stress). Touches multiple physiology systems; separate packet.
- **Stress recovery events.** A weekend, a vacation, a confidant — events that rapidly reduce both Acute and Chronic. Currently only daily decay; explicit "rest events" (which the schedule layer eventually hints) reduce stress beyond the daily floor.
- **Per-domain stress.** Currently a single number. The bibles flag the open question: should stress split into work-stress, social-stress, family-stress? Single-number is simpler at v0.1; multi-domain is a tuning packet if behaviours feel undifferentiated.
- **Memory + stress integration.** A high-stress NPC's memories should bias toward negative valence (stress narrows attentional focus to threats). Cross-system polish; defer to playtest.
