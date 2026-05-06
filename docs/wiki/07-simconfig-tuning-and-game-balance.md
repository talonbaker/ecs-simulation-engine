# 07 — SimConfig Tuning and Game Balance

## What SimConfig.json Is

`SimConfig.json` is the single file that controls every tunable value in the simulation. Nothing biologically or mechanically significant is hardcoded in system or component files — all thresholds, rates, and ceilings live here. This means you can change simulation behaviour without recompiling.

The file lives at the repository root. The simulation searches upward from the current working directory (up to 6 levels) to find it. If no file is found, compiled defaults are used and you will see the console message:

```
[SimConfig] 'SimConfig.json' not found — using compiled defaults.
```

All defaults documented in this file match what you would get without the JSON file, so the C# class definitions are the canonical source of truth.

---

## Hot-Reload

`SimConfigWatcher` monitors `SimConfig.json` for file-system changes using a `FileSystemWatcher`. When a save is detected, the change is applied on the next tick via `SimulationBootstrapper.ApplyConfig(SimConfig)`, which uses reflection to merge the changed config object into the live sim without a restart.

**Console output when a hot-reload fires:**

```
[Hot-reload] Change detected in SimConfig.json — applying next tick...
[Config] Reloaded — 2 value(s) changed:
         FaintingConfig.FearThreshold  85 → 70
         MoodSystemConfig.NegativeDecayRate  0.003 → 0.005
```

**What hot-reloads (no restart required):**

- All system thresholds (hunger, thirst, tired, stressed, fainting fear, etc.)
- All drain and restore rates
- Brain drive score ceilings and mood multipliers
- Narrative event detection thresholds
- FaintingConfig (FearThreshold, FaintDurationTicks)
- ChokingConfig thresholds
- Social drive circadian amplitudes and phases
- Movement speed modifier parameters
- Dialog scoring parameters
- Stress config, workload config, physiology gate config

**What does NOT hot-reload (requires sim restart):**

- Entity starting values (SatiationStart, EnergyStart, etc.) — only apply to newly spawned entities
- World structure (room layouts, spawn positions)
- `humanCount` — set at boot

---

## Full Config Reference

### WorldConfig

| Field | Type | Default | Effect |
|:------|:-----|:--------|:-------|
| `DefaultTimeScale` | float | 120 | Game-seconds per real-second. 120 = 2 game-minutes per real-second. Overridden by `--timescale` CLI flag. |

---

### EntitiesConfig

Contains `Human` and `Cat` entries. Each is an `EntityConfig` composed of the sub-configs below.

#### MetabolismEntityConfig (Human defaults)

| Field | Default | Effect |
|:------|:--------|:-------|
| `SatiationStart` | 90 | Starting satiation. Set to 100 for a freshly-fed NPC. |
| `HydrationStart` | 90 | Starting hydration. |
| `BodyTemp` | 37.0 | Resting body temperature in Celsius. |
| `SatiationDrainRate` | 0.002 | Satiation lost per game-second (scaled by TimeScale). At 0.002 hunger onset takes ~5.6 game-hours. |
| `HydrationDrainRate` | 0.004 | Hydration lost per game-second. Thirst onset in ~2.1 game-hours awake. |
| `SleepMetabolismMultiplier` | 0.10 | Fraction of drain rates applied while sleeping. 0.10 = 10% cost during sleep. |

#### EnergyEntityConfig (Human defaults)

| Field | Default | Effect |
|:------|:--------|:-------|
| `EnergyStart` | 90 | Starting energy (0–100). |
| `SleepinessStart` | 5 | Starting sleepiness (0–100). Near-zero for a just-woken NPC. |
| `EnergyDrainRate` | 0.001 | Energy lost per game-second while awake. |
| `SleepinessGainRate` | 0.0012 | Sleepiness built per game-second while awake. |
| `EnergyRestoreRate` | 0.003 | Energy restored per game-second while sleeping. |
| `SleepinessDrainRate` | 0.002 | Sleepiness lost per game-second while sleeping. |

#### GI Pipeline (Human defaults)

| Section | Key Field | Default | Notes |
|:--------|:----------|:--------|:------|
| StomachEntityConfig | `DigestionRate` | 0.017 ml/s | Food digested per game-second. |
| SmallIntestineEntityConfig | `AbsorptionRate` | 0.008 ml/s | Chyme absorbed per game-second. |
| SmallIntestineEntityConfig | `ResidueToLargeFraction` | 0.4 | 40% of SI volume becomes LI waste. |
| LargeIntestineEntityConfig | `WaterReabsorptionRate` | 0.001 ml/s | Slow secondary hydration source. |
| LargeIntestineEntityConfig | `MobilityRate` | 0.003 ml/s | Content advance toward colon. |
| LargeIntestineEntityConfig | `StoolFraction` | 0.6 | 60% of processed LI volume forms stool. |
| ColonEntityConfig | `UrgeThresholdMl` | 100 ml | `DefecationUrgeTag` applied above this. |
| ColonEntityConfig | `CapacityMl` | 200 ml | `BowelCriticalTag` at capacity — overrides all drives. |
| BladderEntityConfig | `FillRate` | 0.010 ml/s | At TimeScale 120: urge threshold reached in ~2 game-hours. |
| BladderEntityConfig | `UrgeThresholdMl` | 70 ml | `UrinationUrgeTag` applied above this. |
| BladderEntityConfig | `CapacityMl` | 100 ml | `BladderCriticalTag` at capacity. |

---

### SystemsConfig

#### BiologicalConditionSystemConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `ThirstTagThreshold` | 30 | Thirst (0–100) at which `ThirstTag` is applied. |
| `DehydratedTagThreshold` | 70 | Thirst at which `DehydratedTag` (severe) is applied. |
| `HungerTagThreshold` | 30 | Hunger at which `HungerTag` is applied. |
| `StarvingTagThreshold` | 80 | Hunger at which `StarvingTag` is applied. |
| `IrritableThreshold` | 60 | Hunger OR Thirst above which `IrritableTag` is applied. |

#### EnergySystemConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `TiredThreshold` | 60 | Energy below which `TiredTag` is applied. |
| `ExhaustedThreshold` | 25 | Energy below which `ExhaustedTag` is applied. |

#### SleepSystemConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `WakeThreshold` | 20 | Sleepiness must be below this before an NPC can wake up. |

#### BrainSystemConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `EatMaxScore` | 1.0 | Drive score ceiling for Eat. Score = (Hunger/100) × EatMaxScore. |
| `DrinkMaxScore` | 1.0 | Drive score ceiling for Drink. |
| `SleepMaxScore` | 0.9 | Drive score ceiling for Sleep (slightly below survival drives). |
| `DefecateMaxScore` | 0.85 | Drive score ceiling for Defecate (BowelCriticalTag forces to 1.0). |
| `PeeMaxScore` | 0.80 | Drive score ceiling for Pee (BladderCriticalTag forces to 1.0). |
| `BoredUrgencyBonus` | 0.04 | Flat bonus added to all drives when BoredTag is present. |
| `MinUrgencyThreshold` | 0.05 | Below this all drives zero — Dominant becomes None (idle). |
| `SadnessUrgencyMult` | 0.80 | All urgency scores × 0.80 when SadTag is present. |
| `GriefUrgencyMult` | 0.50 | All urgency scores × 0.50 when GriefTag is present. |

#### FeedingSystemConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `HungerThreshold` | 40 | Hunger level required before the brain considers eating. |
| `NutritionQueueCap` | 240 kcal | Max stomach queue before FeedingSystem stops adding food. |
| `FoodFreshnessSeconds` | 86400 | Game-seconds before a placed food entity starts decaying. |
| `FoodRotRate` | 0.001 | RotLevel gained per game-second after freshness expires. |

#### DrinkingSystemConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `HydrationQueueCap` | 15 ml | Max water queued before DrinkingSystem stops (normal thirst). |
| `HydrationQueueCapDehydrated` | 30 ml | Higher cap when `DehydratedTag` is present. |

#### DigestionSystemConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `SatiationPerCalorie` | 0.3 | Satiation points per absorbed kcal. Banana (~117 kcal) → ~35 satiation. |
| `HydrationPerMl` | 2.0 | Hydration points per absorbed ml of water. 15 ml gulp → 30 hydration. |
| `ResidueFraction` | 0.2 | Fraction of digested stomach volume transferred to SmallIntestine. |

#### MoodSystemConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `LowThreshold` | 10 | Emotion above this applies the low-intensity tag (e.g. SerenityTag). |
| `MidThreshold` | 34 | Emotion above this applies the primary tag (e.g. JoyTag). |
| `HighThreshold` | 67 | Emotion above this applies the high-intensity tag (e.g. EcstasyTag). |
| `PositiveDecayRate` | 0.005 | Joy/Trust/Anticipation decay per game-second. |
| `NegativeDecayRate` | 0.003 | Fear/Sadness/Disgust/Anger decay per game-second. |
| `SurpriseDecayRate` | 0.05 | Surprise decays fastest — brief by nature. |
| `JoyGainRate` | 0.01 | Joy gained per game-second while all resources above JoyComfortThreshold. |
| `JoyComfortThreshold` | 60 | All three resources (satiation, hydration, energy) must exceed this for Joy gain. |
| `AngerGainRate` | 0.015 | Anger gained per game-second while IrritableTag present. |
| `SadnessGainRate` | 0.008 | Sadness gained per game-second while HungerTag or ThirstTag present. |
| `BoredGainRate` | 0.005 | Disgust (boredom) gained per game-second while Dominant == None. |
| `RottenFoodDisgustSpike` | 40 | Instant Disgust added when ConsumedRottenFoodTag is applied. |
| `AnticipationGainRate` | 0.006 | Anticipation gained while hunger/thirst is building. |

#### RotSystemConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `RotTagThreshold` | 30 | RotLevel at which RotTag is applied. FeedingSystem checks this before eating. |

---

### StressConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `SuppressionStressGain` | 1.5 | AcuteLevel gain per suppression tick event. |
| `DriveSpikeStressDelta` | 25 | Drive must exceed its baseline by this to count as a spike. |
| `DriveSpikeStressGain` | 2.0 | AcuteLevel gain per drive spike per tick. |
| `SocialConflictStressGain` | 3.0 | AcuteLevel gain per social conflict event. |
| `AcuteDecayPerTick` | 0.05 | AcuteLevel natural decay per tick. |
| `StressedTagThreshold` | 60 | AcuteLevel above which `StressedTag` is applied. |
| `OverwhelmedTagThreshold` | 85 | AcuteLevel above which `OverwhelmedTag` is applied. |
| `BurningOutTagThreshold` | 70 | ChronicLevel above which `BurningOutTag` is applied. |
| `BurningOutCooldownDays` | 3 | Days BurningOutTag stays after ChronicLevel drops below threshold. |
| `StressAmplificationMagnitude` | 1.0 | Extra suppression magnitude pushed when StressedTag is present. |
| `StressVolatilityScale` | 0.5 | Fraction of AcuteLevel added to drive volatility multiplier. |
| `NeuroticismStressFactor` | 0.2 | Per Neuroticism point, stress gain ×(1 + N × 0.2). |

---

### ActionSelectionConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `DriveCandidateThreshold` | 60 | Drive.Current must exceed this to enumerate candidates. |
| `IdleScoreFloor` | 0.20 | Idle wins when nothing else clears this floor. |
| `InversionStakeThreshold` | 0.55 | Stake above which approach-avoidance flip check fires. |
| `InversionInhibitionThreshold` | 0.50 | Combined with stake to complete an inversion flip. |
| `SuppressionGiveUpFactor` | 0.30 | Fraction of suppressed drive that leaks through at low willpower. |
| `SuppressionEpsilon` | 0.10 | Closeness to winner that marks a candidate as actively suppressed. |
| `SuppressionEventMagnitudeScale` | 5 | Willpower cost per suppression event. |
| `PersonalityTieBreakWeight` | 0.05 | Per Conscientiousness/Openness point nudge. |
| `MaxCandidatesPerTick` | 32 | Hard cap on candidates evaluated per NPC per tick. |
| `AvoidStandoffDistance` | 4 | Tiles the flee target is pushed from the threat. |

---

### PhysiologyGateConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `VetoStrengthThreshold` | 0.50 | Effective inhibition at which an action class is blocked. |
| `LowWillpowerLeakageStart` | 30 | Willpower below which leakage weakens the social veto. |
| `StressMaxRelaxation` | 0.70 | Maximum fraction by which acute stress relaxes the veto. |

---

### NarrativeConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `DriveSpikeThreshold` | 15 | Minimum drive delta per tick to emit `DriveSpike`. |
| `WillpowerDropThreshold` | 10 | Minimum willpower drop in a single tick to emit `WillpowerCollapse`. |
| `WillpowerLowThreshold` | 20 | Willpower level below which `WillpowerLow` is emitted once. |
| `AbruptDepartureWindowTicks` | 3 | Ticks after DriveSpike in which a room-exit emits `LeftRoomAbruptly`. |
| `CandidateDetailMaxLength` | 280 | Character cap on the Detail field of any narrative candidate. |

---

### SocialSystemConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `DriveDecayPerTick` | 0.15 | Points a social drive moves toward its Baseline per tick. |
| `DriveCircadianAmplitudes` | see below | Peak oscillation (points) per drive per day. |
| `DriveCircadianPhases` | see below | Fraction of day (0–1) at which each drive peaks. |
| `DriveVolatilityScale` | 1.0 | Global multiplier on neuroticism-driven drive noise. |
| `WillpowerSleepRegenPerTick` | 1 | Willpower restored per tick while sleeping. |
| `RelationshipIntensityDecayPerTick` | 0.05 | Intensity points lost per tick without proximity. |

Default circadian amplitudes and phases:

| Drive | Amplitude | Phase |
|:------|:----------|:------|
| belonging | 3.0 | 0.40 (mid-afternoon) |
| status | 2.5 | 0.30 (late morning) |
| affection | 3.0 | 0.65 (early evening) |
| irritation | 4.0 | 0.55 (mid-afternoon) |
| attraction | 2.0 | 0.70 (evening) |
| trust | 1.5 | 0.45 (early afternoon) |
| suspicion | 2.0 | 0.80 (dusk) |
| loneliness | 5.0 | 0.85 (night) |

---

### DialogConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `CalcifyThreshold` | 8 | Uses before calcification eligibility. |
| `CalcifyContextDominanceMin` | 0.70 | Fraction of uses sharing dominant context before calcification. |
| `TicRecognitionThreshold` | 5 | Times a listener hears a fragment for tic recognition. |
| `RecencyWindowSeconds` | 300 | Game-seconds in which a prior use incurs RecencyPenalty. |
| `ValenceMatchScore` | 5 | Score per drive key matching current drive level. |
| `RecencyPenalty` | -10 | Score penalty when fragment was used recently. |
| `CalcifyBiasScore` | 3 | Bonus score for calcified fragments. |
| `PerListenerBiasScore` | 2 | Bonus when the speaker has used this fragment with this listener before. |
| `DialogAttemptProbability` | 0.05 | Per-tick probability an in-range NPC pair attempts dialog. |
| `DriveContextThreshold` | 60 | Drive level at which a drive is considered elevated for context selection. |

---

### LifeStateConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `DefaultIncapacitatedTicks` | 180 | Ticks before Incapacitated → Deceased (at TimeScale 120, ~3 game-minutes). |
| `EmitDeathInvariantOnTransition` | true | Whether InvariantSystem applies deceased invariants on death. |
| `IncapacitatedAllowsBladderVoid` | true | Whether BladderSystem still runs on Incapacitated entities. |
| `DeceasedFreezesPosition` | true | Whether MovementSystem skips Deceased entities. |

---

### ChokingConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `BolusSizeThreshold` | 0.65 | Toughness (0–1) at which a bolus is a choking hazard. |
| `EnergyThreshold` | 40 | Energy strictly below this triggers distracted-tired choke vulnerability. |
| `StressThreshold` | 70 | AcuteLevel at or above which distracted-stressed vulnerability applies. |
| `IrritationThreshold` | 65 | Irritation at or above which frustrated-eating vulnerability applies. |
| `IncapacitationTicks` | 90 | Ticks Incapacitated before Deceased(Choked). Overrides DefaultIncapacitatedTicks. |
| `PanicMoodIntensity` | 0.85 | Panic (0–1 scale) applied to MoodComponent at moment of choke. |
| `EmitChokeStartedNarrative` | true | Whether ChokeStarted narrative candidate is emitted. |

---

### FaintingConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `FearThreshold` | 85 | MoodComponent.Fear (0–100) at which an Alive NPC faints. |
| `FaintDurationTicks` | 20 | Ticks Incapacitated before recovery (at TimeScale 120, ~20 game-seconds). |
| `EmitFaintedNarrative` | true | Whether `Fainted` narrative candidate is emitted on faint onset. |
| `EmitRegainedConsciousnessNarrative` | true | Whether `RegainedConsciousness` candidate is emitted on recovery. |

---

### BereavementConfig (WP-3.0.2)

| Field | Default | Effect |
|:------|:--------|:-------|
| `WitnessedDeathStressGain` | 20.0 | One-shot AcuteLevel gain for a direct witness of a death. |
| `BereavementStressGain` | 5.0 | One-shot AcuteLevel gain for non-witness colleagues. |
| `WitnessGriefIntensity` | 80 | GriefLevel set on the witness immediately. |
| `ColleagueBereavementGriefIntensity` | 40 | GriefLevel ceiling for colleagues (scaled by relationship intensity). |
| `BereavementMinIntensity` | 20 | Minimum relationship intensity required for non-witness impact. |
| `ProximityBereavementMinIntensity` | 30 | Minimum relationship intensity for proximity-entry grief trigger. |
| `ProximityBereavementStressGain` | 8.0 | AcuteLevel gain when NPC enters the room containing their relation's corpse. |

---

### WorkloadConfig

| Field | Default | Effect |
|:------|:--------|:-------|
| `TaskGenerationHourOfDay` | 8.0 | Game-hour at which TaskGeneratorSystem fires each day. |
| `TaskGenerationCountPerDay` | 5 | Tasks created per generation event. |
| `TaskEffortHoursMin` / `Max` | 0.5 / 6.0 | Range of task effort in game-hours. |
| `TaskDeadlineHoursMin` / `Max` | 4.0 / 48.0 | Range of deadline offsets from creation time. |
| `BaseProgressRatePerSecond` | 0.0001 | Base work-progress per game-second. |
| `OverdueTaskStressGain` | 1.0 | Stress gain per overdue task per tick. |

---

## FaintingConfig — Deep Dive

Fainting is the most commonly tuned life-state scenario because it creates frequent narrative events without permanent consequences.

### How fear-based fainting works

1. Each tick, `FaintingDetectionSystem` queries every Alive NPC and reads `MoodComponent.Fear`.
2. If `Fear >= FearThreshold` (default 85) and the NPC does not already have `IsFaintingTag`, the system emits a `Fainted` narrative candidate and enqueues a `BeginIncapacitated` request against `LifeStateTransitions`.
3. `FaintingRecoverySystem` monitors Incapacitated NPCs with `IsFaintingTag`. Once `Clock.CurrentTick >= FaintingComponent.RecoveryTick`, it emits `RegainedConsciousness` and enqueues `RecoverToAlive`.
4. `FaintingCleanupSystem` removes `IsFaintingTag` and `FaintingComponent` once the NPC is back to Alive.

The `IncapacitatedTickBudget` written into `LifeStateComponent` is `FaintDurationTicks + 1`, so the generic death-by-incapacitation timer cannot fire before recovery completes.

### Key gameplay dials

**FearThreshold (default 85).** This is the primary lever. Lowering it to 60–70 produces frequent fainting from moderate fear. Raising it above 90 makes fainting extremely rare. The scale tracks Plutchik's wheel: 85 corresponds to near-terror, 34–66 covers the anxiety/fear range, below 34 is apprehension.

**FaintDurationTicks (default 20).** At TimeScale 120, each tick is roughly 1 real-second of game-time. 20 ticks = 20 game-seconds of unconsciousness. Increase to 60–120 for longer dramatic episodes; decrease to 5–10 for brief swoons.

**EmitFaintedNarrative / EmitRegainedConsciousnessNarrative (both default true).** These control whether the AI pipeline and chronicle receive narrative signals about the event. Set to false only during stress testing to reduce narrative bus load.

---

## Game Balance Workflow

The general loop for tuning any subsystem:

```
1. Edit SimConfig.json → save → observe hot-reload console output
2. Use ai narrative-stream or ai stream to observe the change in behaviour
3. Iterate until the narrative rate and quality matches design intent
4. Commit the tuned SimConfig.json alongside the code that depends on it
```

### Step 1 — Change

Edit the relevant config section in `SimConfig.json`. The file must be valid JSON. The `Newtonsoft.Json` deserialiser ignores unknown keys and preserves compiled defaults for missing keys, so partial files are safe.

### Step 2 — Observe with the AI CLI

**Streaming narrative events** is the fastest feedback loop for social and emotional tuning:

```bash
dotnet run --project ECSCli -- ai narrative-stream --duration 7200
```

This runs 2 game-hours and prints every narrative event candidate as a JSON line. Count `Fainted` / `RegainedConsciousness` events. If you see dozens per minute, `FearThreshold` is too low. If you see none after a full game-day, it is too high.

**Streaming telemetry frames** gives you the raw drive and physiological data:

```bash
dotnet run --project ECSCli -- ai stream --out telemetry.jsonl --interval 60 --duration 7200
```

Each frame contains every NPC's component state. Pipe through `jq` to isolate the fear channel:

```bash
jq '.entities[] | select(.components.mood != null) | {id: .id, fear: .components.mood.fear}' telemetry.jsonl
```

### Step 3 — Balance Red Flags

| Symptom | Likely cause | Fix |
|:--------|:-------------|:----|
| All NPCs fainting constantly | `FearThreshold` too low, or `NegativeDecayRate` for Fear too low (fear not decaying fast enough) | Raise `FearThreshold` to 80–90; raise `MoodSystemConfig.NegativeDecayRate` to 0.008–0.015 |
| No NPCs ever faint | `FearThreshold` too high, or no system is driving fear above baseline | Lower `FearThreshold` to 70–75; check that fear-inducing stimuli exist in the world |
| Drive urgency never spiking in narrative | `NarrativeConfig.DriveSpikeThreshold` too high, or drives are too well-fed | Lower `DriveSpikeThreshold` to 8–10 for denser narrative; check that drain rates produce genuine urgency |
| NPCs ignoring hunger/thirst entirely | `BrainSystemConfig.MinUrgencyThreshold` is too high | Lower `MinUrgencyThreshold` to 0.02–0.03 |
| NPCs never sleeping despite exhaustion | `EnergySystemConfig.ExhaustedThreshold` too low, or `SleepMaxScore` too low | Raise `ExhaustedThreshold` to 35–40; raise `SleepMaxScore` to 0.95 |
| Constant stress/burnout | `StressConfig.AcuteDecayPerTick` too low | Raise to 0.1–0.2; check `SuppressionStressGain` |
| NPCs eating before they are hungry | `FeedingSystemConfig.HungerThreshold` too low | Raise to 50–60 for visible hunger before eating |

---

## Recommended Fainting Tuning Starting Points

For a simulation where fainting is a rare but real possibility (once per 2–3 game-hours across a 10-NPC cast):

```json
"Fainting": {
  "FearThreshold": 78,
  "FaintDurationTicks": 30,
  "EmitFaintedNarrative": true,
  "EmitRegainedConsciousnessNarrative": true
}
```

For dramatic storytelling where fainting is an expected genre beat:

```json
"Fainting": {
  "FearThreshold": 65,
  "FaintDurationTicks": 60
}
```

Pair fainting tuning with `MoodSystemConfig.NegativeDecayRate`. If fear spikes fast and decays slowly, the threshold is effectively lower over time. If you lower `FearThreshold`, consider also raising `NegativeDecayRate` (for Fear) to prevent sustained unconsciousness from extended fear episodes.

---

## Using Haiku (25×) for Balance Sweeps

In the 1-5-25 architecture, Haiku instances handle high-volume automated balance sweeps. The pattern is:

1. **Opus defines the sweep** — parameter ranges, acceptance criteria (e.g. "Fainted events per game-hour: 0.3–0.8"), and output format.
2. **A Haiku instance runs each configuration variant** — uses `ai narrative-stream` and `ai stream`, extracts the target metric with `jq`, reports pass/fail.
3. **A Sonnet instance aggregates the sweep results** — identifies the winning config, writes the updated `SimConfig.json`, flags regressions.

Example Haiku sweep command:

```bash
# Haiku runs this for each FearThreshold value in [65, 70, 75, 80, 85]
dotnet run --project ECSCli -- ai narrative-stream \
  --duration 14400 \
  --seed 42 \
  | jq -s '[.[] | select(.kind == "Fainted")] | length'
```

The `--seed` flag ensures the same cast and random sequence across all variants, so differences in event counts reflect the config change and nothing else.

For full instructions on constructing Haiku balance sweep prompts, see [08-ai-prompting-guide-1-5-25.md](08-ai-prompting-guide-1-5-25.md).

---

*See also: [02-system-pipeline-reference.md](02-system-pipeline-reference.md) | [06-cli-reference.md](06-cli-reference.md) | [08-ai-prompting-guide-1-5-25.md](08-ai-prompting-guide-1-5-25.md)*
