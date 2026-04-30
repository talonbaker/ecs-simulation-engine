# WP-3.2.5 — Per-Archetype Tuning JSONs

> **DO NOT DISPATCH UNTIL WAVE 2 (3.2.2, 3.2.3, 3.2.4) IS MERGED.**
> This packet authors content tuning JSONs that the systems shipped in Wave 2 consume. Each system already runs with engine defaults; this packet adds per-archetype variation.

**Tier:** Sonnet
**Depends on:** WP-3.2.2 (physics — mass per archetype), WP-3.2.3 (chores — acceptance bias), WP-3.2.4 (rescue — bias). Earlier: WP-3.0.1 (choke), WP-3.0.3 (slip), WP-3.0.2 (bereavement).
**Parallel-safe with:** WP-3.2.6 (animation states)
**Timebox:** 90 minutes
**Budget:** $0.40

---

## Goal

Each Phase 3.0.x and 3.2.x system that varies behavior per-archetype currently uses uniform defaults. The Cynic and the Newbie panic the same way; the Vent and the Hermit grieve the same way; the Founder's Nephew is as likely to choke as the Old Hand. This packet authors the per-archetype tuning JSONs that make the cast feel like a cast.

After this packet, the cast bible's promise — "13 archetypes that read distinctly" — has data behind it across:

- Choke biasing (panic-eat tendency, choke risk)
- Slip biasing (movement carefulness, fall risk multiplier)
- Bereavement biasing (grief expression style, persistence of grief memory)
- Chore acceptance biasing (consolidates 3.2.3 stub)
- Rescue biasing (consolidates from 3.2.4)
- Physics mass (per-archetype body mass for slip momentum, throw force)
- Memory persistence (the Cynic genuinely doesn't remember most things)

Each JSON is consumed by its respective system at boot or per-event. Systems continue to ship defaults; the JSON overrides per archetype.

---

## Reference files

- `docs/c2-content/cast-bible.md` — full archetype catalog with behavioral commitments.
- `docs/c2-infrastructure/work-packets/_completed/WP-3.0.1.md` — choke detection thresholds.
- `docs/c2-infrastructure/work-packets/_completed/WP-3.0.2.md` — bereavement intensities.
- `docs/c2-infrastructure/work-packets/_completed/WP-3.0.3.md` — slip risk.
- `docs/c2-infrastructure/work-packets/WP-3.2.2-rudimentary-physics.md` — mass.
- `docs/c2-infrastructure/work-packets/WP-3.2.3-chore-rotation-system.md` — chore acceptance.
- `docs/c2-infrastructure/work-packets/WP-3.2.4-rescue-mechanic.md` — rescue bias.
- `APIFramework/Components/CastSpawnComponents.cs` — `NpcArchetypeComponent.ArchetypeId` is the lookup key.

---

## Non-goals

- Do **not** introduce new gameplay behaviors. Tuning content only.
- Do **not** modify systems consuming the JSONs beyond reading them at boot.
- Do **not** ship per-NPC tuning (overrides per individual). Per-archetype is v0.1 granularity.
- Do **not** retry, recurse, or self-heal.

---

## Design notes

### File layout

```
docs/c2-content/tuning/
├── archetype-choke-bias.json
├── archetype-slip-bias.json
├── archetype-bereavement-bias.json
├── archetype-chore-acceptance-bias.json    (consolidates 3.2.3)
├── archetype-rescue-bias.json              (consolidates 3.2.4)
├── archetype-physics-mass.json
└── archetype-memory-persistence-bias.json
```

### `archetype-choke-bias.json`

```jsonc
{
  "schemaVersion": "0.1.0",
  "archetypeChokeBias": [
    {"archetype": "the-newbie",      "bolusSizeThresholdMult": 0.85, "energyThresholdMult": 1.10, "stressThresholdMult": 0.90},
    {"archetype": "the-old-hand",    "bolusSizeThresholdMult": 1.20, "energyThresholdMult": 1.0,  "stressThresholdMult": 1.10},
    {"archetype": "the-cynic",       "bolusSizeThresholdMult": 1.0,  "energyThresholdMult": 1.0,  "stressThresholdMult": 1.05},
    {"archetype": "the-vent",        "bolusSizeThresholdMult": 0.95, "energyThresholdMult": 0.95, "stressThresholdMult": 0.85}
  ]
}
```

### `archetype-slip-bias.json`

```jsonc
{
  "schemaVersion": "0.1.0",
  "archetypeSlipBias": [
    {"archetype": "the-old-hand", "movementSpeedFactor": 0.90, "slipChanceMult": 0.70},
    {"archetype": "the-newbie",   "movementSpeedFactor": 1.10, "slipChanceMult": 1.30}
  ]
}
```

### `archetype-bereavement-bias.json`

```jsonc
{
  "schemaVersion": "0.1.0",
  "archetypeBereavementBias": [
    {"archetype": "the-vent",       "stressIntensityMult": 1.20, "moodIntensityMult": 1.30, "memoryPersistenceMult": 1.0},
    {"archetype": "the-cynic",      "stressIntensityMult": 0.70, "moodIntensityMult": 0.65, "memoryPersistenceMult": 0.85},
    {"archetype": "the-recovering", "stressIntensityMult": 1.10, "moodIntensityMult": 1.20, "memoryPersistenceMult": 1.10}
  ]
}
```

### `archetype-physics-mass.json`

```jsonc
{
  "schemaVersion": "0.1.0",
  "archetypeMass": [
    {"archetype": "the-old-hand",        "massKg": 75.0},
    {"archetype": "the-newbie",          "massKg": 65.0},
    {"archetype": "the-cynic",           "massKg": 70.0},
    {"archetype": "the-vent",            "massKg": 80.0},
    {"archetype": "the-climber",         "massKg": 70.0},
    {"archetype": "the-hermit",          "massKg": 65.0},
    {"archetype": "the-founders-nephew", "massKg": 65.0},
    {"archetype": "the-recovering",      "massKg": 75.0},
    {"archetype": "the-affair",          "massKg": 70.0},
    {"archetype": "the-crush",           "massKg": 60.0}
  ]
}
```

### `archetype-memory-persistence-bias.json`

```jsonc
{
  "schemaVersion": "0.1.0",
  "archetypeMemoryPersistenceBias": [
    {"archetype": "the-cynic",      "persistenceMult": 0.85, "decayRateMult": 1.20},
    {"archetype": "the-old-hand",   "persistenceMult": 1.10, "decayRateMult": 0.85},
    {"archetype": "the-recovering", "persistenceMult": 1.20, "decayRateMult": 1.0}
  ]
}
```

`MemoryRecordingSystem` consumes this when deciding persistence flags or decay rates.

### Tests

- `ChokeBiasJsonTests.cs`, `SlipBiasJsonTests.cs`, `BereavementBiasJsonTests.cs`, `ChoreAcceptanceBiasJsonTests.cs`, `RescueBiasJsonTests.cs`, `PhysicsMassJsonTests.cs`, `MemoryPersistenceBiasJsonTests.cs` — JSON validation per file.
- `BiasingIntegrationTests.cs` — verify behavior changes per archetype:
  - Newbie chokes statistically more than Old Hand over 1000 ticks.
  - Vent grieves harder than Cynic (witness stress accumulates more).
  - Old Hand walks slower than Newbie.
  - Cynic's persistent memory entries decay faster than Old Hand's.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| data | `docs/c2-content/tuning/archetype-choke-bias.json` | Choke. |
| data | `docs/c2-content/tuning/archetype-slip-bias.json` | Slip. |
| data | `docs/c2-content/tuning/archetype-bereavement-bias.json` | Bereavement. |
| data | `docs/c2-content/tuning/archetype-chore-acceptance-bias.json` | Chore (consolidates 3.2.3 stub). |
| data | `docs/c2-content/tuning/archetype-rescue-bias.json` | Rescue (consolidates 3.2.4). |
| data | `docs/c2-content/tuning/archetype-physics-mass.json` | Mass per archetype. |
| data | `docs/c2-content/tuning/archetype-memory-persistence-bias.json` | Memory persistence. |
| code | (modifications to consuming systems) | Each reads its respective JSON at boot via a `TuningCatalog` service; falls back to defaults if missing. |
| code | `APIFramework/Systems/Tuning/TuningCatalog.cs` | Centralized loader / lookup. |
| test | (~8 test files) | JSON validation + biasing integration. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.2.5.md` | Completion note. JSON inventory; bias values; statistical evidence of differential behavior. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | All seven JSONs load cleanly. | unit-test |
| AT-02 | All cast-bible archetypes present in each JSON; no archetype missing. | unit-test |
| AT-03 | All multipliers in valid range (0.5..2.0 for most; mass in 50..100kg). | unit-test |
| AT-04 | Newbie chokes statistically more than Old Hand under identical conditions over 1000 ticks. | integration-test |
| AT-05 | Vent's bereavement stress accumulates faster than Cynic's under identical witness conditions. | integration-test |
| AT-06 | Old Hand's slip rate is statistically lower than Newbie's. | integration-test |
| AT-07 | Cynic's persistent memory entries decay faster than Old Hand's. | integration-test |
| AT-08 | All Phase 0/1/2/3.0.x/3.1.x and prior 3.2.x tests stay green. | regression |
| AT-09 | `dotnet build` warning count = 0; `dotnet test` all green. | build + test |

---

## Followups (not in scope)

- Per-NPC tuning overrides. Future polish.
- Tuning UI / editor tool. Future tooling.
- AI-generated tuning. Sonnet authors per-archetype JSON from cast-bible at build time. Future automation.
- Statistical balance verification. 100 simulations; verify no archetype dominantly killed/stressed. Future balance pass.
- Cross-archetype-archetype interaction tuning ("Cynic and Vent get on each other's nerves more"). Per-pair JSON. Future depth.


---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT NEEDED

This is a Track 1 (engine) packet. All verification is handled by the xUnit test suite. Once `dotnet test` returns green for `APIFramework.Tests` (and any other affected test project), the packet is ready to push and PR. **No Unity Editor steps required.**

The Sonnet executor's pipeline:

1. Implement the spec.
2. Add or update xUnit tests to cover all acceptance criteria.
3. Run `dotnet test` from the repo root. Must be green.
4. Run `dotnet build` to confirm no warnings introduced.
5. Stage all changes including the self-cleanup deletion (see below).
6. Commit on the worktree's feature branch.
7. Push the branch and open a PR against `staging`.
8. Stop. Do **not** merge. Talon merges after review.

If a test fails or compile fails, fix the underlying cause. Do **not** skip tests, do **not** mark expected-failures, do **not** push a red branch.

### Cost envelope (1-5-25 Claude army)

Target: **$0.50–$1.20** per packet wall-time on the orchestrator. Timebox is stated above in the packet header. If the executing Sonnet observes its own cost approaching the upper bound without nearing acceptance criteria, **escalate to Talon** by stopping work and committing a `WP-X-blocker.md` note to the worktree explaining what burned the budget. Do not silently exceed the envelope.

Cost-discipline rules of thumb:
- Read reference files at most once per session — cache content in working memory rather than re-reading.
- Run `dotnet test` against the focused subset (`--filter`) during iteration; full suite only at the end.
- If a refactor is pulling far more files than the spec named, stop and re-read the spec; the spec may be wrong about scope.

### Self-cleanup on merge

The active `docs/c2-infrastructure/work-packets/` directory should contain only **pending** packets. Shipped packets are deleted, not archived to `_completed-specs/` (Talon's convention from 2026-04-30 forward).

Before opening the PR, the executing Sonnet must:

1. **Check downstream dependents** with this command from the repo root:
   ```bash
   git grep -l "<THIS-PACKET-ID>" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```
   Replace `<THIS-PACKET-ID>` with this packet's identifier (e.g., `WP-3.0.4`).

2. **If the grep returns no results** (no other pending packet references this one): include `git rm docs/c2-infrastructure/work-packets/<this-packet-filename>.md` in the staging set. The deletion ships in the same commit as the implementation. Add the line `Self-cleanup: spec file deleted, no pending dependents.` to the commit message.

3. **If the grep returns one or more pending packets**: leave the spec file in place. Add a one-line status header to the top of this spec file (immediately under the H1):
   ```markdown
   > **STATUS:** SHIPPED to staging YYYY-MM-DD. Retained because pending packets depend on this spec: <list>.
   ```
   Add the line `Self-cleanup: spec retained, dependents: <list>.` to the commit message.

4. **Do not touch** files under `_completed/` or `_completed-specs/` — those are historical artifacts from earlier phases.

5. The git history (commit message + PR body) is the historical record. The spec file itself is ephemeral once shipped without dependents.
