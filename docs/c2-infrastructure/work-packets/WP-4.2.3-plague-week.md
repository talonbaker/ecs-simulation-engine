# WP-4.2.3 — Plague Week

> **Phase 4.2.x emergent gameplay (post-reorg).** First scenario packet to ship on top of the zone substrate. Plague week: a contagion event sweeps the office over ~1 in-game week. Infected NPCs cough, miss work, infect adjacent NPCs. The player can't directly cure anyone — they can promote hand-washing chores, isolate sick NPCs, or just ride it out. Tests the social-systems cascade (drives → mood → relationships → chronicle) under stress.

> **DO NOT DISPATCH UNTIL WP-4.2.0 IS MERGED** — uses `ZoneIdComponent` for zone-aware infection spread. WP-4.2.2 (LoD) is nice-to-have (more NPCs make plague more interesting) but not required.

**Tier:** Sonnet
**Depends on:** WP-4.2.0 (zone substrate), WP-3.2.1 (sound trigger bus — coughs), WP-3.2.3 (chore rotation — hand-washing chore extension), WP-3.2.4 (rescue mechanic — mild self-care helps), WP-3.2.5 (per-archetype tuning — per-archetype infection susceptibility / recovery).
**Parallel-safe with:** Other 4.2.x scenarios (4.2.4 PIP, 4.2.5 affair, 4.2.6 fire — disjoint mechanics).
**Timebox:** 150 minutes
**Budget:** $0.65
**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** Does plague feel like a slow-burn social event, not a video-game disease mechanic? Are coughs frequent enough to read but not annoying? Do per-archetype susceptibility differences land (the-newbie catches it first; the-old-hand muscles through)? Does player intervention (hand-wash chores, isolation) actually shift outcomes?

---

## Goal

Add a plague-week scenario as an emergent narrative event: a contagion sweeps the office over ~5–7 in-game days, infecting a fraction of the cast, generating chore + drive cascades + chronicle moments, then resolving (recovery or isolation chain).

After this packet:
- A `PlagueOnsetSystem` triggers (configurable: random per game-month, or scenario-scripted).
- Infected NPCs gain `InfectionComponent` with stage (Incubating | Symptomatic | Recovering).
- Symptomatic NPCs cough (existing `Cough` SoundTriggerKind from MAC-003); reduce productivity (workload bias); skip occasional work blocks; get irritable (drive impact).
- Adjacent NPCs (within ~3 tiles, respecting zone boundaries from MAC-018) have a per-tick infection probability based on archetype susceptibility tuning.
- Hand-washing chore (new `HandWashing` `ChoreKind`) reduces infection probability for the chore-completer.
- Chronicle records: "Bert started coughing on Tuesday", "Donna caught it from Bert", "Greg recovered after 3 days", "the entire west cubicle row missed Friday's standup."
- Player intervention (build mode placing more sinks → enables more hand-wash chores; isolation by placing temporary walls — exercises the WP-4.0.J author-mode mutations) shifts outcomes.

The scenario is **single-zone or cross-zone** (infection respects zones — an infected NPC must enter a zone for that zone's NPCs to be at risk; via WP-4.2.0 transition triggers).

---

## Reference files

- `docs/c2-content/new-systems-ideas.md` — likely has plague-week design notes (audit first; this packet refines, doesn't restart).
- `docs/c2-content/cast-bible.md` — archetype reference for susceptibility design.
- `APIFramework/Systems/Chores/ChoreRotationSystem.cs` — existing pattern; HandWashing chore extends `ChoreKind`.
- `APIFramework/Systems/LifeState/ChokingDetectionSystem.cs` — pattern for medical-event detection.
- `APIFramework/Audio/SoundTriggerKind.cs` — adds nothing if `Cough` already exists (it does, per WP-3.2.1).
- `APIFramework/Bootstrap/ZoneRegistry.cs` (from WP-4.2.0) — for zone-aware spread.
- `docs/c2-content/tuning/archetype-*.json` — extends with `infectionSusceptibility` + `recoveryRate` fields.

---

## Non-goals

- Do **not** model death from plague. v0.1 is non-lethal (recovery 100% guaranteed in ~3–5 in-game days). Lethal variants are future content packets.
- Do **not** model immunity / vaccination. v0.1: catch it once per scenario, recover, no immunity carryover. Future polish.
- Do **not** add visual symptoms beyond the existing chibi cue layer (MAC-013 — `RedFaceFlush` already exists; reuse for fever).
- Do **not** model HR / leave policies. NPCs miss work blocks per their schedule but no "calling in sick" UI.
- Do **not** model patient-zero discovery mechanics. The scenario fires; the chronicle reveals onset; the player traces it themselves.
- Do **not** modify zone substrate. Zone-aware spread reads `ZoneIdComponent` without modifying it.
- Do **not** add per-illness types (cold vs flu vs covid analog). v0.1 is one generic illness.

---

## Design notes

### `InfectionComponent`

```csharp
public struct InfectionComponent
{
    public InfectionStage Stage          { get; init; }
    public long           OnsetTick      { get; init; }
    public long           NextStageTick  { get; init; }
    public string         OutbreakId     { get; init; }   // links related infections in chronicle
}

public enum InfectionStage { Incubating, Symptomatic, Recovering }
```

Spawned by `PlagueOnsetSystem` on patient zero; spread by `InfectionSpreadSystem`.

### Per-archetype tuning

Extend `archetype-*.json` with optional `plague` block:

```jsonc
"plague": {
  "susceptibility": 0.35,    // 0.0-1.0; base per-tick infection chance (when adjacent to infected)
  "recoveryRate":   1.0,     // multiplier on default recovery time
  "complaintLevel": "loud"   // "quiet" / "average" / "loud" — drives cough frequency + drive impact
}
```

Per-archetype defaults for the 10 cast-bible archetypes:
- the-vent: high complaint, average susceptibility (loud).
- the-newbie: high susceptibility (catches it first), average complaint.
- the-old-hand: low susceptibility (muscles through), quiet complaint.
- the-cynic: low complaint (suffers in silence), low susceptibility.
- the-affair: average across the board, but spreads to its affair partner with elevated probability (modeling close contact).
- (etc.)

### Hand-washing chore

Adds `HandWashing` to existing `ChoreKind` enum. Eligible NPCs: any (no archetype lock). Chore tile: any room with `RoomCategory.Bathroom` or `RoomCategory.Breakroom` (sink-equipped). Effect: chore completer gets a 30-tick `WashedRecently` mark, halving their infection probability during that window.

### Spread mechanics

`InfectionSpreadSystem` runs each tick (Bucket A — `CoarsenInInactive` — outbreaks evolve over hours, not seconds, so coarse ticks are fine):

1. For each Symptomatic NPC, find all NPCs in adjacent tiles (within radius 3) AND in the same zone.
2. Per neighbor: roll `tickProbability = baseRate × neighborSusceptibility × (washedRecently ? 0.5 : 1.0)`.
3. If roll succeeds: spawn `InfectionComponent { Stage = Incubating, OutbreakId = sharedId }` on the neighbor.

**Zone-aware:** infection does not jump zones unless an infected NPC physically transits via WP-4.2.0 transition trigger. This makes plague a per-zone phenomenon initially, slow-spreading across the office over days.

### Player intervention

The player has two levers:

1. **Build mode (existing):** Place additional sinks in rooms where the hand-wash chore can fire. More sinks = more hand-washing = lower spread.
2. **Author mode walls (WP-4.0.J):** Temporarily wall off a zone or split a room. Effective but disruptive (workflow / drive impact on isolated NPCs).

### Chronicle integration

New narrative kinds: `InfectionStarted`, `InfectionSpread` (with cause: "from Bert via adjacency"), `InfectionRecovered`. Records into the existing chronicle systems; surfaces in the dialog corpus when NPCs talk about who's sick.

### Performance

`InfectionSpreadSystem` opts into `[ZoneLod(CoarsenInInactive)]` (Bucket A). Even at full fidelity it's cheap (per-NPC adjacency scan + roll); the perf gate isn't at risk.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/InfectionComponent.cs` (new) | Stage + tick + outbreak id. |
| code | `APIFramework/Components/InfectionStage.cs` (new) | Enum. |
| code | `APIFramework/Systems/Plague/PlagueOnsetSystem.cs` (new) | Trigger logic; selects patient zero. |
| code | `APIFramework/Systems/Plague/InfectionSpreadSystem.cs` (new) | Per-tick adjacent spread. `[ZoneLod(CoarsenInInactive)]`. |
| code | `APIFramework/Systems/Plague/InfectionStageSystem.cs` (new) | Incubating → Symptomatic → Recovering progression. |
| code | `APIFramework/Systems/Plague/InfectionEffectSystem.cs` (new) | Symptomatic → emit Cough trigger; bias drives. |
| code | `APIFramework/Systems/Chores/HandWashingChore.cs` (new) | Chore handler. |
| code | `APIFramework/Components/ChoreKind.cs` (modification — additive enum) | Add `HandWashing`. |
| code | `APIFramework/Components/NarrativeEventKind.cs` (modification — additive enum) | Add `InfectionStarted`, `InfectionSpread`, `InfectionRecovered`. |
| code | `APIFramework/Config/SimConfig.cs` (modification) | Add `PlagueConfig` section. |
| data | `docs/c2-content/tuning/archetype-*.json` (modification) | Add `plague` block to all 10 archetype files. |
| test | `APIFramework.Tests/Systems/Plague/PlagueOnsetSystemTests.cs` (new) | Trigger semantics. |
| test | `APIFramework.Tests/Systems/Plague/InfectionSpreadSystemTests.cs` (new) | Spread is zone-bounded; susceptibility respected. |
| test | `APIFramework.Tests/Systems/Plague/InfectionStageProgressionTests.cs` (new) | Stage transitions on schedule. |
| test | `APIFramework.Tests/Systems/Plague/HandWashingReducesSpreadTests.cs` (new) | WashedRecently halves spread. |
| test | `APIFramework.Tests/Systems/Plague/PlagueChronicleIntegrationTests.cs` (new) | Narrative events emitted. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Note `InfectionComponent` + plague systems as latent surfaces (not new MAC entry — these consume MAC-001 archetype tuning + MAC-002 narrative events). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `PlagueOnsetSystem` triggers per scenario / per random chance per game-month. | unit-test |
| AT-02 | Patient zero gets `InfectionComponent { Stage = Incubating }`. | unit-test |
| AT-03 | After incubation period, stage → Symptomatic; coughs emitted. | unit-test |
| AT-04 | Symptomatic NPC infects adjacent NPCs based on per-archetype susceptibility. | unit-test |
| AT-05 | Spread does NOT jump zones (cross-zone infection requires NPC transit). | zone-test |
| AT-06 | `HandWashing` chore halves infection probability for the next 30 ticks. | unit-test |
| AT-07 | Stage progression: Incubating → Symptomatic → Recovering → Healthy (no Infection). | unit-test |
| AT-08 | Recovery time tunable per archetype. | unit-test |
| AT-09 | Chronicle records `InfectionStarted` with cause + tick + entities. | integration-test |
| AT-10 | Plague systems run under LoD (`CoarsenInInactive`); FPS gate holds. | perf-test |
| AT-11 | All Phase 0–3 + Phase 4.0.A–L + WP-4.2.0 tests stay green. | regression |
| AT-12 | `dotnet build` warning count = 0; all tests green. | build + test |

---

## Mod API surface

No new MAC entry. Reuses MAC-001 (per-archetype tuning extended with `plague` block), MAC-002 (`NarrativeEventKind` additive — new kinds), MAC-003 (`SoundTriggerKind.Cough` reused). Demonstrates the archetype-tuning pattern for scenarios.

---

## Followups (not in scope)

- Per-illness variants (cold / flu / stomach bug — different symptom profiles).
- Vaccination / immunity carryover.
- Death-from-plague (lethal variant; couples to LifeState).
- "Calling in sick" UI / notification.
- Patient-zero discovery puzzle (player tracks the index case via chronicle).

---

## Completion protocol (REQUIRED — read before merging)

Standard. Visual verification helpful (see coughs in play, see chronicle entries). Sandbox scene optional — plague behavior verifiable in test rig + perf test.

### Cost envelope

Target: **$0.65**. Components + 5 systems + chore + tuning data + tests. If cost approaches $1.10, escalate.
