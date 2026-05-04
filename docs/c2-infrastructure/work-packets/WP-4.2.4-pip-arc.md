# WP-4.2.4 — PIP Arc

> **Phase 4.2.x emergent gameplay (post-reorg).** A "Performance Improvement Plan" arc: an NPC's chronic underperformance triggers a multi-day arc culminating in either redemption (PIP completion) or termination. Tests the stress-mood-relationship cascade under sustained pressure; gives the player meaningful binary outcomes that affect office-wide morale.

> **DO NOT DISPATCH UNTIL WP-4.2.0 IS MERGED** — uses `ZoneIdComponent` for zone-scoped morale impact.

**Tier:** Sonnet
**Depends on:** WP-4.2.0 (zone substrate), WP-3.2.3 (chore rotation — refused chores feed PIP threshold), WP-3.2.5 (per-archetype tuning — per-archetype PIP thresholds), WP-2.x.x (workload + tasks system — productivity score is the trigger metric).
**Parallel-safe with:** Other 4.2.x scenarios.
**Timebox:** 150 minutes
**Budget:** $0.65
**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** Does PIP onset feel earned (the NPC's struggles were visible) rather than arbitrary? Does the arc give the player a real choice (intervene vs let it play out)? Are the social cascades proportionate (a termination shakes the office; a redemption boosts morale)?

---

## Goal

Add the PIP arc as a multi-day emergent narrative event:

1. **Trigger:** an NPC's productivity drops below threshold (configurable per archetype) for ~3 consecutive in-game days, OR a manager NPC's stress + the underperformer's accumulated chore-refusals cross a combined threshold.
2. **Onset:** `PipComponent` attached; `PipStartedNarrativeEvent` emitted; chibi cue (e.g., a clipboard icon) appears over the NPC.
3. **Arc (5–7 in-game days):** the NPC has a daily "PIP review block" in their schedule (a meeting in the manager's office). They feel elevated stress, lower belonging, lower status. Coworkers notice (per-pair social drives shift). Drives can spiral or recover.
4. **Resolution:**
   - **Redemption:** if productivity recovers above threshold for 3 consecutive days OR a player-triggered intervention fires (place a coworker mentor in adjacent cubicle), `PipResolvedNarrativeEvent { Outcome: Redeemed }`.
   - **Termination:** if the arc completes without recovery, `PipResolvedNarrativeEvent { Outcome: Terminated }` → NPC departs (despawn cleanly via `IWorldMutationApi.DespawnNpc` from WP-4.0.K). Office morale dips; relationship cascade across remaining NPCs.

The scenario produces real story moments: "Greg made it through PIP and Donna threw him a desk-cake party" / "Bob got fired and Karen has been quiet ever since."

---

## Reference files

- `docs/c2-content/cast-bible.md` — reference for which archetypes are PIP-prone (the-cynic, the-newbie when overwhelmed) vs PIP-resistant (the-old-hand).
- `APIFramework/Systems/Stress/StressAccumulationSystem.cs` (or equivalent — locate at impl time).
- `APIFramework/Components/SocialDrivesComponent.cs` — Belonging / Status drive references.
- `APIFramework/Mutation/IWorldMutationApi.cs` — uses `DespawnNpc` (WP-4.0.K) for termination cleanup.
- `docs/c2-content/tuning/archetype-*.json` — extends with `pipThreshold` block.

---

## Non-goals

- Do **not** ship hiring (replacing terminated NPCs). v0.1 ends with one fewer NPC in the office. Hiring is FF-008 territory (couples to economy).
- Do **not** model HR documentation / paperwork detail — this is narrative shorthand, not a workplace simulator.
- Do **not** model legal / wrongful termination consequences. The office sim is character-driven, not procedural-justice driven.
- Do **not** tie PIP to player skill score / "did you manage well." v0.1 PIP is character-driven (drives and stress); player intervention shifts probabilities, doesn't determine outcome deterministically.

---

## Design notes

### `PipComponent`

```csharp
public struct PipComponent
{
    public long       OnsetTick               { get; init; }
    public long       NextReviewTick          { get; init; }
    public int        ReviewsCompleted        { get; init; }
    public float      AccumulatedRecovery     { get; init; }   // -1 to 1; ≥0.6 redeems, ≤-0.6 terminates
    public string?    InitiatingManagerName   { get; init; }   // for chronicle
}
```

### Per-archetype tuning

Extends `archetype-*.json`:

```jsonc
"pip": {
  "productivityThreshold":   0.45,
  "consecutiveDaysToTrigger": 3,
  "stressMultiplier":         1.5,
  "redemptionResistance":     0.5
}
```

### Onset, progression, resolution systems

- `PipOnsetSystem` — daily check; flags eligible NPCs.
- `PipReviewSystem` — schedules and runs the daily review meeting (uses existing schedule infrastructure).
- `PipProgressionSystem` — accumulates Recovery / Decline scores based on intervening productivity + stress.
- `PipResolutionSystem` — resolves at threshold; emits narrative event; cascade morale via social drives.

All run as Bucket A LoD (CoarsenInInactive) — slow social arcs are coarsenable safely. Termination itself is a synchronous event (entity despawn) so it must be in active-zone scope or the player should be notified — for v0.1, **PIP termination is suppressed in inactive zones** (rolls over to next active-zone tick) so the player doesn't return to find someone gone without seeing the moment.

### Player intervention

- **Place a mentor:** building author-mode-place a desk near the PIP'd NPC; if it's adjacent to a high-Trust archetype (the-old-hand, the-mentor-if-shipped), the PIP'd NPC's recovery accumulates faster.
- **Reduce workload:** if the player removes some chore-eligibility (manual placement of equipment / removal of broken items causing chore demand), PIP recovery accelerates.
- **Direct intervention** (future, requires player-verb expansion FF-016 / Q6 resolution).

### Performance

PIP systems are inexpensive (per-eligible-NPC checks; rare actual transitions). Cost is negligible.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/PipComponent.cs` (new) | State component. |
| code | `APIFramework/Systems/Pip/PipOnsetSystem.cs` (new) | Eligibility + trigger. |
| code | `APIFramework/Systems/Pip/PipReviewSystem.cs` (new) | Daily review block. |
| code | `APIFramework/Systems/Pip/PipProgressionSystem.cs` (new) | Recovery/decline accumulation. |
| code | `APIFramework/Systems/Pip/PipResolutionSystem.cs` (new) | Outcome emission. |
| code | `APIFramework/Components/NarrativeEventKind.cs` (modification — additive) | `PipStarted`, `PipReviewHeld`, `PipResolved`. |
| code | `APIFramework/Config/SimConfig.cs` (modification) | `PipConfig` section. |
| data | `docs/c2-content/tuning/archetype-*.json` (modification) | Add `pip` block to all 10 archetypes. |
| test | 5+ test files covering onset/progression/redemption/termination/cascade. | unit + integration |

---

## Acceptance tests

| ID | Assertion |
|:---|:---|
| AT-01 | NPC with productivity < threshold for N days triggers PIP. |
| AT-02 | PIP'd NPC has chibi cue indicator (couples to MAC-013). |
| AT-03 | Daily review block scheduled in manager's office. |
| AT-04 | Recovery / decline accumulates based on intervening behavior. |
| AT-05 | Redemption: outcome emitted; PIP component removed; morale lift. |
| AT-06 | Termination: outcome emitted; NPC despawned via IWorldMutationApi.DespawnNpc; remaining NPCs' Belonging/Trust drives shift. |
| AT-07 | Player intervention (mentor placement) shifts redemption probability. |
| AT-08 | PIP termination does NOT fire in inactive zones (deferred to next active-zone tick) to avoid unwitnessed exits. |
| AT-09 | All Phase 0–3 + 4.0.A–L + 4.2.0 tests stay green. |
| AT-10 | `dotnet build` warning count = 0; all tests green. |

---

## Mod API surface

No new MAC entry. Extends MAC-001 (`pip` block), MAC-002 (additive narrative kinds).

---

## Followups (not in scope)

- Hiring (replace terminated NPCs).
- HR-paperwork detail.
- Skill-tree progression for non-PIP'd NPCs.
- Player-verb expansion (direct intervention) per FF-016.

---

## Completion protocol

Standard. **Cost target $0.65.**
