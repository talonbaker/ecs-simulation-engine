# WP-1.4.A — Social Engine: Components + Drive Dynamics + Willpower + Relationships

**Tier:** Sonnet
**Depends on:** WP-1.0.A.1 (v0.2.1 social schema must be merged — already true on `staging` via PR #35)
**Parallel-safe with:** WP-1.0.B (different file footprint — see Design notes)
**Timebox:** 120 minutes
**Budget:** $0.50

---

## Goal

Land the engine-side social component family that the v0.2.1 wire format describes. This packet brings four data shapes into `APIFramework` as live ECS components — drives, willpower, personality, inhibitions — and three systems that mutate them per tick: drive dynamics (the "some Wednesdays you're chatty, some Wednesdays you're not" mechanic), willpower depletion-and-regeneration, and relationship lifecycle. Per the action-gating bible, drives + willpower + inhibitions are the input surface for the action-selection logic that will arbitrate between feeling and doing — but action-selection itself is deferred (it needs spatial proximity, which Phase 1.1 lands).

This is the first packet that populates the Phase-1 social architecture as runtime state. After this lands, the engine has NPCs whose moods drift through the day, whose willpower depletes when stressed and regenerates with rest, whose relationship histories evolve through pattern transitions — even though no NPC is *acting* on any of it yet, because action-selection needs proximity events.

The packet is deliberately scoped to be **parallel-safe with WP-1.0.B**: it does not touch `Warden.Telemetry`, `Warden.Contracts`, or `world-state.schema.json`. The DTO surface those packets create is already adequate for what this engine packet produces; populating the projector to expose runtime social state on the wire is a small follow-up packet (WP-1.4.B) that runs after both 1.0.B and 1.4.A merge.

---

## Reference files

- `docs/c2-content/DRAFT-action-gating.md` — the design source for willpower, inhibitions, approach-avoidance, and physiology-overridable-by-inhibition. **Read this first.** Section "How the systems compose" describes the action-selection sketch this packet reserves the components for.
- `docs/c2-content/DRAFT-cast-bible.md` — the eight-drive catalog, vocabulary register enum, personality dimension space, and archetype list. The Sonnet does not implement archetypes here (cast generator is Phase 1.8); it just makes sure component shapes are populatable from those archetype values.
- `docs/c2-content/DRAFT-new-systems-ideas.md` — StressSystem and SocialMaskSystem are kindred-spirit ideas. Not implemented in this packet, but the willpower model must leave room for stress to act as the long-term depleter (a follow-up packet wires them together).
- `docs/c2-infrastructure/00-SRD.md` §8.3 (per-pair primary), §8.5 (social state is first-class).
- `docs/c2-infrastructure/work-packets/_completed/WP-1.0.A.1.md` — confirms the v0.2.1 DTO surface this packet's components mirror in shape.
- `Warden.Contracts/Telemetry/SocialStateDto.cs` — the DTO that the engine's `SocialComponent` family must be projectable to (in a future packet). Component fields should align with DTO fields by name and unit.
- `APIFramework/Components/DriveComponent.cs` — the **physiological** drive component (hunger, thirst, energy). Read for naming convention and to ensure the new social components don't collide. The new file is `SocialDrivesComponent.cs`, deliberately named to disambiguate from the existing physiological `DriveComponent`.
- `APIFramework/Components/MoodComponent.cs` — existing mood state. The new `PersonalityComponent.CurrentMood` is *not* the same field; mood is short-lived emotion (existing), `currentMood` is a persistent self-perceived label (new). Don't collapse them.
- `APIFramework/Core/SimulationBootstrapper.cs` — system registration site. The three new systems get registered here in the right phase.
- `APIFramework/Core/SystemPhase.cs` — the phase enum. Likely no new phase needed; new systems land in an existing phase.
- `APIFramework/Components/EntityTemplates.cs` — for an additive `WithSocial(...)` builder helper.
- `APIFramework/Components/Tags.cs` — for `NpcTag` (add if absent).
- `SimConfig.json` — runtime tuning knobs go here, not in code.
- `APIFramework/Core/SeededRandom.cs` — RNG source. Use this, never `System.Random` directly. Determinism contract per SRD §2 Pillar A.
- `APIFramework.Tests/Systems/*.cs` — pattern reference for system test layout.

## Non-goals

- Do **not** modify `Warden.Telemetry/TelemetryProjector.cs`. Projector population of social state is the follow-up packet WP-1.4.B (deferred). The projector continues to emit `SchemaVersion = "0.1.0"` with social fields absent. **This is the parallel-safety contract with WP-1.0.B** — both packets must avoid the projector.
- Do **not** modify any file under `Warden.Contracts/`. The DTOs landed in WP-1.0.A.1 are the contract; this packet builds the engine to populate them later.
- Do **not** modify any file under `docs/c2-infrastructure/schemas/`. No schema bump in this packet.
- Do **not** implement action selection. Drives, willpower, and inhibitions are *read* by an action-selection layer that does not yet exist. The action-gating bible describes the design intent; the implementation comes later, after spatial/proximity lands. If the Sonnet feels itself starting to write `if (drive > threshold) doThing()`, stop — that's out of scope.
- Do **not** implement memory recording. Memory events are recorded when proximity-detected interactions produce notable drive deltas. Proximity is Phase 1.1; this packet predates it. No `MemorySystem`, no `RelationshipHistorySystem` writes.
- Do **not** populate inhibitions via archetype templates. Inhibition installation at NPC spawn is the cast-generator's job (Phase 1.8). This packet only adds the *component* and *the system that decays inhibition strength over time* (which is the simplest piece of inhibition lifecycle; full lifecycle including event-driven install is later).
- Do **not** add a Stress component or StressSystem. New-systems-ideas describes them as a separate concern; willpower depletion in this packet is driven by suppression-event signals, not by a stress accumulator. Stress integration is a follow-up packet.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)
- Do **not** modify the existing physiological `DriveComponent`, `MoodComponent`, `EnergyComponent`, or any `Bladder/Stomach/Intestine/Colon` components. The social engine sits *alongside* the physiological one; their integration (mood-modulated by social, willpower-overrides-physiology) is later.
- Do **not** modify the eventual *naming* of any field once it lands. Component field names are part of the contract that the projector will mirror — `current` and `baseline` exactly, no abbreviations.
- Do **not** include any test that depends on wall-clock time, `DateTime.Now`, or `System.Random`. All tests pass deterministic seeds and tick-counts. Determinism is enforced.

---

## Design notes

### Component layout

Four new components, all on the NPC entity. None of them are required for an entity to exist — an entity without `SocialDrivesComponent` is a non-NPC (the mailman, a thrown stapler, the office cat). NPCs carry all four; other entities carry none. Use `NpcTag` to mark which entities the social systems iterate over.

**`SocialDrivesComponent`** — eight drives, each `DriveValue { int Current, int Baseline }`, both `0–100`. The eight in canonical cast-bible order: `Belonging`, `Status`, `Affection`, `Irritation`, `Attraction`, `Trust`, `Suspicion`, `Loneliness`. Use `int` for parity with the wire format; we're not modeling sub-integer precision. Each drive's `Current` value is what changes during play; `Baseline` is the resting value the drive-dynamics system pulls toward.

**`WillpowerComponent`** — single `Current` and `Baseline`, both `0–100`. Same shape as a drive sub-field. One component per NPC. Domain specificity is in `InhibitionsComponent`, not in willpower.

**`PersonalityComponent`** — Big Five traits as five integer fields each `-2..+2`: `Openness`, `Conscientiousness`, `Extraversion`, `Agreeableness`, `Neuroticism`. Plus `VocabularyRegister` enum, `CurrentMood` short string (max 32 chars). Personality is **stable for the save** — set at spawn, never mutated by systems in this packet. (Slow drift over many in-game weeks could come later; not now.)

**`InhibitionsComponent`** — `IReadOnlyList<Inhibition>` where `Inhibition { InhibitionClass Class, int Strength, InhibitionAwareness Awareness }`. `Class` enum mirrors the v0.2.1 schema's eight values (`Infidelity`, `Confrontation`, `BodyImageEating`, `PublicEmotion`, `PhysicalIntimacy`, `InterpersonalConflict`, `RiskTaking`, `Vulnerability`). `Strength` is `0–100`. `Awareness` is `Known | Hidden`. Component carries up to eight inhibitions per NPC (mirrors schema `maxItems: 8`).

### The relationship as a first-class entity

Per SRD §8.3, a relationship is an entity, not a property. The `EntityManager` already supports arbitrary entities with components — relationships use that.

**`RelationshipComponent`** — on the relationship entity, not on either participant. Fields:
- `ParticipantA: int` (entity id, lower id by convention)
- `ParticipantB: int` (entity id, higher id by convention)
- `Patterns: IReadOnlyList<RelationshipPattern>` (max 2; enum mirrors schema)
- `Intensity: int` (0–100; how loud the relationship is in either NPC's life)

A `RelationshipTag` tag component marks these entities so systems can iterate them efficiently. Pair canonicalization (lower id first) is enforced at construction time and asserted in tests.

**No history fields on the engine-side component.** The schema's `historyEventIds[]` is a wire-format optimization for memory references; engine-side, memory recording (deferred) will manage history through a different mechanism (likely an append-only ring buffer per relationship, kept in `MemoryComponent` on the relationship entity — which a follow-up packet adds). For this packet, the relationship is a stable identity with patterns and intensity, no history yet.

### Drive dynamics — three forces

`DriveDynamicsSystem` runs each tick (or every Nth tick — see SimConfig section). For each NPC and each of its eight drives:

1. **Decay toward baseline.** `Current` drifts toward `Baseline` at a rate set by `SimConfig.SocialDriveDecayPerTick`. A drive that's been pushed up by an event slowly returns to typical; a drive that's been suppressed slowly recovers. Linear approach is fine; exponential is overkill at v0.2.1.

2. **Circadian shape.** A small modulation of `Current` based on the existing `SimulationClock.CircadianFactor` (already a 0..1 value). Each drive has a "circadian phase preference" — `Loneliness` peaks late evening, `Status` peaks in mid-morning when work performance is observed, `Affection` peaks in late afternoon, etc. The amplitudes are small (a few points up or down per cycle); the shape is what makes the world's social mood breathe. The exact mapping per drive lives in `SimConfig.SocialDriveCircadianAmplitudes` so it's tunable.

3. **Volatility (per-NPC noise).** A per-tick random nudge, in the range `[-volatility, +volatility]`, where `volatility` is derived from `PersonalityComponent.Neuroticism` (high neuroticism → larger nudges) and a global `SimConfig.SocialDriveVolatilityScale`. Use `SeededRandom`, not `System.Random`.

All three forces sum into `Current`, which is then clamped to `0..100`.

`Baseline` is **not** modified by this system. Baseline is a per-archetype value set at spawn; it's stable for the save (slow drift later).

### Willpower depletion and regeneration

`WillpowerSystem` runs each tick. The engine doesn't yet know what counts as "suppression" (action-selection produces those signals when it deferes a high-drive action). For this packet, the system listens for **suppression event signals** of shape:

```csharp
public readonly record struct WillpowerEventSignal(
    int EntityId,
    WillpowerEventKind Kind,    // SuppressionTick, RestTick
    int Magnitude               // 0–10 cost or recovery per tick
);
```

A `WillpowerEventQueue` (a simple `ConcurrentQueue<WillpowerEventSignal>` in DI) collects signals; the system drains it each tick and applies the deltas. Other future systems (action-selection, sleep, social mask, stress) push events into the queue; the system itself doesn't know who pushes.

For testing, the test harness pushes signals directly into the queue. The packet does NOT add any signal *producer* — that's the job of later packets. This is the cleanest seam for parallel future work.

Regeneration during sleep ticks (when an NPC is in a `SleepingTag` state, which the existing `SleepSystem` already manages) is the one producer this packet *does* wire: while `SleepingTag` is present, push one `RestTick` per tick at magnitude `SimConfig.WillpowerSleepRegenPerTick` (default 1). This gives the system something to validate end-to-end against existing engine state without inventing fake producers.

Willpower clamps to `0..100`. There's no maximum-overflow ("super willpower") and no negative ("negative willpower"). At zero, future action-selection will treat the NPC as "out of resistance" — but action-selection is later.

### Relationship lifecycle

`RelationshipLifecycleSystem` runs each tick (cheap; few relationships in the office). For this packet it does two things:

1. **Intensity decay.** Relationships not "fed" by recent interaction lose intensity at `SimConfig.RelationshipIntensityDecayPerTick`. Without proximity events to signal interaction (deferred), this is open-loop — every relationship slowly loses intensity. That's correct: a relationship the participants haven't been near in months IS losing intensity. When proximity ships, the system gains an "interaction → intensity boost" signal.

2. **Pattern transition skeleton.** A pattern can transition under specific conditions (Rival → OldFlame is impossible without history; Rival → Friend is possible; OldFlame → ActiveAffair is possible). For this packet, transitions are configuration-driven: a `RelationshipTransitionTable.json` lists which patterns can move to which, and a system pass each tick checks each relationship for transitions but **does not actually trigger any** (because the trigger conditions need memory and proximity). The infrastructure is in place; trigger wiring is later. Tests verify that the table loads, the system iterates without error, and no transition fires without the trigger conditions being met.

### Why no projector update

`Warden.Telemetry/TelemetryProjector.cs` currently emits `SchemaVersion = "0.1.0"` and ignores social state. Updating it to populate the new social fields is a small, isolated change but it is **the one file** that has the highest risk of conflict with WP-1.0.B's small projector touch (1.0.B's non-goals say no projector population, but 1.0.B does need to update the WorldStateDto constructor call to include `Rooms = null, LightSources = null, LightApertures = null` — that's a tiny edit but in the same file).

To keep this packet truly parallel-safe with 1.0.B, the projector update is its own follow-up packet (WP-1.4.B). That follow-up will:
- Run after both 1.0.B and 1.4.A merge.
- Update the projector to read SocialDrives/Willpower/Personality/Inhibitions components and populate the v0.2.1 wire format.
- Bump emitted `SchemaVersion` to `"0.2.1"` (or `"0.3.0"` if 1.0.B's spatial work is also being projected — TBD when WP-1.4.B is drafted).
- Be ~30 minutes of Sonnet work, $0.15.

### SimConfig additions

All tuning lives in `SimConfig.json`, not in code. New keys with sensible defaults:

```jsonc
{
  "social": {
    "driveDecayPerTick": 0.15,                 // points per tick toward baseline
    "driveCircadianAmplitudes": {              // peak deviation from current per drive
      "belonging":  3.0, "status":     2.5, "affection":  3.0, "irritation": 4.0,
      "attraction": 2.0, "trust":      1.5, "suspicion":  2.0, "loneliness": 5.0
    },
    "driveCircadianPhases": {                  // 0..1 of day at which each drive peaks
      "belonging":  0.40, "status":     0.30, "affection":  0.65, "irritation": 0.55,
      "attraction": 0.70, "trust":      0.45, "suspicion":  0.80, "loneliness": 0.85
    },
    "driveVolatilityScale": 1.0,               // global multiplier on neuroticism-driven noise
    "willpowerSleepRegenPerTick": 1,           // regen per tick while SleepingTag present
    "relationshipIntensityDecayPerTick": 0.05  // open-loop decay until proximity ships
  }
}
```

Defaults are starting points. Tuning happens later via Haiku-batch balance scenarios.

### Determinism

Every random nudge goes through `SeededRandom`. No `System.Random`. No wall-clock reads. Tests run with fixed seeds and fixed tick counts. Two runs with the same seed produce byte-identical drive trajectories. This is the engine determinism contract; this packet upholds it.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/SocialDrivesComponent.cs` | Eight `DriveValue { int Current, int Baseline }` fields in canonical order. Plus a small helper `Clamp01_100(int v)` if it doesn't already exist somewhere. |
| code | `APIFramework/Components/WillpowerComponent.cs` | `int Current`, `int Baseline`, both 0–100. Constructor enforces clamping. |
| code | `APIFramework/Components/PersonalityComponent.cs` | Five Big Five integer fields (-2..+2), `VocabularyRegister` enum (mirrors schema), `string CurrentMood` (max 32 chars; constructor truncates). |
| code | `APIFramework/Components/InhibitionsComponent.cs` | `IReadOnlyList<Inhibition>` (max 8). `Inhibition` record. `InhibitionClass` enum (mirrors schema's eight values). `InhibitionAwareness` enum (`Known`, `Hidden`). |
| code | `APIFramework/Components/RelationshipComponent.cs` | `int ParticipantA`, `int ParticipantB`, `IReadOnlyList<RelationshipPattern> Patterns` (max 2), `int Intensity` (0–100). Canonical pair ordering enforced at construction. `RelationshipPattern` enum mirrors schema's 13 values. |
| code | `APIFramework/Components/Tags.cs` (modified) | Add `NpcTag` and `RelationshipTag` if not present. |
| code | `APIFramework/Systems/DriveDynamicsSystem.cs` | Per-tick decay, circadian, volatility per NPC. Reads SimConfig values. Uses SeededRandom. |
| code | `APIFramework/Systems/WillpowerSystem.cs` | Drains the willpower-event queue and applies deltas. Pushes `RestTick` events for `SleepingTag` NPCs. |
| code | `APIFramework/Systems/RelationshipLifecycleSystem.cs` | Per-tick intensity decay; pattern-transition table iteration (no triggers fire yet). |
| code | `APIFramework/Systems/WillpowerEventQueue.cs` | Thin wrapper around `ConcurrentQueue<WillpowerEventSignal>`. Registered in DI as a singleton. |
| code | `APIFramework/Systems/WillpowerEventSignal.cs` | The signal record + `WillpowerEventKind` enum. |
| code | `APIFramework/Components/EntityTemplates.cs` (modified) | Add a static `WithSocial(EntityBuilder, ...)` extension or builder method that adds the four new components to an entity along with `NpcTag`. Used by tests; cast generator uses it later too. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register the three new systems in the appropriate phase (probably alongside existing `MoodSystem` / `BrainSystem`). Register `WillpowerEventQueue` as a singleton. |
| data | `APIFramework/Data/RelationshipTransitionTable.json` | The pattern-transition map — which pattern can transition to which. Loaded at boot. Use a small starter set: `Rival → AlliesOfConvenience → Friend`; `Friend → ActiveAffair`; `OldFlame → ActiveAffair`; `ActiveAffair → SleptWithSpouse` (when one participant has another relationship); `Rival → OldFlame` is **not** allowed (no path); etc. The Sonnet picks a sensible starter set; we'll tune later. |
| code | `SimConfig.json` (modified) | Add the `social` section per Design notes. |
| code | `APIFramework.Tests/Components/SocialDrivesComponentTests.cs` | Construction, clamping, equality. |
| code | `APIFramework.Tests/Components/WillpowerComponentTests.cs` | Construction, clamping. |
| code | `APIFramework.Tests/Components/PersonalityComponentTests.cs` | Big Five bounds, mood truncation, register parsing. |
| code | `APIFramework.Tests/Components/InhibitionsComponentTests.cs` | Up-to-8 enforcement, enum parsing, awareness invariants. |
| code | `APIFramework.Tests/Components/RelationshipComponentTests.cs` | Canonical pair ordering, max-2-patterns, equality on canonical id. |
| code | `APIFramework.Tests/Systems/DriveDynamicsSystemTests.cs` | Decay-toward-baseline (run 1000 ticks, verify drift); circadian shape (verify peak at expected day fraction); volatility bounds (verify deltas stay within scale). All use seeded RNG. |
| code | `APIFramework.Tests/Systems/WillpowerSystemTests.cs` | Suppression event reduces `Current`; rest event raises it; clamp at 0 and 100; regen-during-sleep produces expected delta over N sleep ticks. |
| code | `APIFramework.Tests/Systems/RelationshipLifecycleSystemTests.cs` | Intensity decays without interaction; transition table loads; no transition fires without trigger; canonical pair preserved across ticks. |
| code | `APIFramework.Tests/Systems/SocialDeterminismTests.cs` | Two runs with same seed produce byte-identical drive trajectories over 5000 ticks. (One test, one assertion, the determinism contract in active form.) |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.4.A.md` | Completion note. Standard template. Explicitly enumerate (a) which schema fields the engine now produces values for (drives, willpower, personality, inhibitions, relationships), (b) which systems iterate which entities, (c) what's deferred (action-selection, memory recording, projector update, stress integration). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | All new components compile, instantiate with sensible defaults, and pass invariant checks (clamping, max-counts, canonical ordering). | unit-test |
| AT-02 | `DriveDynamicsSystem` over 1000 ticks moves a drive whose `Current = 90` and `Baseline = 50` to within 5 points of `Baseline` (decay term dominates over volatility on this horizon). | unit-test |
| AT-03 | `DriveDynamicsSystem` produces a circadian peak for `Loneliness` at the configured phase (0.85 of day) — measured by averaging `Current - Baseline` across many seeds at each day fraction. | unit-test |
| AT-04 | `DriveDynamicsSystem` volatility scales with `Personality.Neuroticism`: an NPC with neuroticism +2 produces larger per-tick deltas than neuroticism -2 (significant difference at p<0.01 over 5000 ticks). | unit-test |
| AT-05 | `WillpowerSystem` consumes one `SuppressionTick` event with magnitude 5 and reduces `Current` by 5 (clamped at 0). | unit-test |
| AT-06 | `WillpowerSystem` with `SleepingTag` present pushes one `RestTick` per tick at the configured magnitude; over N ticks, `Current` rises by `N * SimConfig.WillpowerSleepRegenPerTick` (clamped at 100). | unit-test |
| AT-07 | `RelationshipLifecycleSystem` decays `Intensity` open-loop in absence of interaction signals — verified over 1000 ticks. | unit-test |
| AT-08 | `RelationshipLifecycleSystem` loads the transition table at boot; the table contains at least 5 entries; no transition fires during a 1000-tick test (because trigger conditions are not yet implemented). | unit-test |
| AT-09 | `RelationshipComponent` constructed with `(participantA: 7, participantB: 3)` canonicalizes to `(participantA: 3, participantB: 7)`. Two relationships with the same canonical pair compare equal. | unit-test |
| AT-10 | Determinism: `SocialDeterminismTests` produces byte-identical drive trajectories across two runs with the same seed over 5000 ticks. | unit-test |
| AT-11 | `Warden.Telemetry.Tests` all pass — projector still emits `SchemaVersion = "0.1.0"` and the social fields are absent (this packet does not modify the projector). | build + unit-test |
| AT-12 | `Warden.Contracts.Tests` all pass — DTOs unchanged. | build + unit-test |
| AT-13 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-14 | `dotnet test ECSSimulation.sln` — every existing test stays green; new tests pass. | build |

---

## Followups (not in scope)

- WP-1.4.B (small, ~$0.15): update `Warden.Telemetry/TelemetryProjector.cs` to populate `entities[].social` from the new components, populate `relationships[]` from relationship entities, and bump emitted `SchemaVersion` to `"0.2.1"` (or higher if 1.0.B's spatial fields are also being projected by then).
- Action-selection layer that consults drives + willpower + inhibitions per the action-gating bible. Needs spatial proximity (Phase 1.1).
- Memory recording. Needs proximity events.
- Cast-generator (Phase 1.8) populates inhibitions and willpower baseline ranges per archetype.
- Stress integration: `SuppressionTick` events from a `StressSystem` instead of just from sleep regen — wires up the long-term breakdown timeline.
- Pattern-transition trigger conditions — requires memory + proximity.
- Tunable per-archetype circadian shapes (some NPCs peak earlier; "morning people" vs "night owls").
- Slow personality drift over many in-game weeks.
