# WP-4.0.B — NPC Spatial Behavior / Anti-Overlap

> **Wave 1 of the Phase 4.0.x foundational polish wave.** Per the 2026-05-02 brief restructure: NPCs currently bunch and clip into each other in the playtest scene, even though they have no purposeful tasks yet. This is a foundational legibility issue. This packet adds a soft-repulsion personal-space layer so NPCs visually maintain space without a continuous physics solver. Track 1 engine packet.

**Tier:** Sonnet
**Depends on:** Existing movement system, `PositionComponent`, `MovementSystem`. No Unity changes.
**Parallel-safe with:** WP-4.0.A (Unity render-only, disjoint surface), WP-4.0.C (build-mode + footprint, disjoint from movement), all WP-PT.* (playtest scene work; this packet does not modify scenes).
**Timebox:** 90 minutes
**Budget:** $0.40

---

## Goal

NPCs in the playtest scene currently overlap each other when idle, when on similar paths, or when their pathfinding routes them to nearby cells. The engine has no concept of *personal space*. Soft physics-style repulsion would be expensive (continuous solver per SRD §8.4 spirit — "rudimentary physics, not a simulator"). This packet adds a **per-tick, per-NPC, soft-positional-nudge** that maintains a configurable personal-space radius without a continuous solver.

After this packet:
- Each NPC has a `PersonalSpaceComponent` with a radius and repulsion-strength.
- A `SpatialBehaviorSystem` runs in the cleanup phase (after movement, before render projection) and applies a soft per-tick nudge: when two NPCs are within the sum of their personal-space radii, both get pushed apart by a small fraction of the overlap.
- Per-archetype tuning JSON authored so introverts (the Hermit, the Cynic) maintain larger personal space; extroverts (the Newbie, the Vent) tolerate closer proximity.
- xUnit tests verify: NPCs spawned on top of each other separate within N ticks; NPCs walking past each other maintain minimum distance; archetype bias produces measurable difference in steady-state spacing.

The behavior must be **soft** — NPCs should not snap apart, jitter, or oscillate. Players should perceive natural spacing, not collision response. The visual effect is "people respect each other's bubble," not "collision detection fired."

---

## Reference files

- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` — read MAC-001 (per-archetype tuning pattern) and MAC-010 (PersonalSpaceComponent — this packet introduces).
- `docs/PHASE-4-KICKOFF-BRIEF.md` — read the 2026-05-02 restructure section. This packet is part of Wave 1.
- `docs/c2-infrastructure/00-SRD.md` §8.4 — the "rudimentary physics, not a simulator" axiom. This packet must respect it.
- `APIFramework/Components/PositionComponent.cs` — read for the position type used.
- `APIFramework/Systems/Movement/MovementSystem.cs` — read for the tick phase ordering and how movement currently writes positions.
- `APIFramework/Systems/Tuning/TuningCatalog.cs` — read the existing per-archetype tuning loader. This packet adds one more JSON to the same loader pattern.
- `docs/c2-content/tuning/archetype-mass.json` — read for the canonical per-archetype tuning JSON shape (every archetype listed; multipliers in valid range).
- `docs/c2-content/cast-bible.md` — read for archetype list and personality dimensions (introvert/extrovert leaning per archetype).

---

## Non-goals

- Do **not** add a continuous physics solver. Per-tick nudges only.
- Do **not** add full collision detection (object-vs-object impact, swept volumes, etc.). NPCs should not be able to push *props*; they only respect each other's bubbles.
- Do **not** modify pathfinding. NPCs continue to path normally; the spatial-behavior system runs after pathing has produced a target position and adjusts only the residual position.
- Do **not** add NPC-vs-wall repulsion. Walls are handled by pathfinding; NPCs do not normally cross walls.
- Do **not** apply spatial behavior to NPCs in `Incapacitated` or `Deceased` states. Per `LifeStateGuard`, fainted/dead NPCs lie where they lie; bystanders going to perform Heimlich must be able to *get close*.
- Do **not** modify `WorldStateDto`. The `PersonalSpaceComponent` round-trips through the existing DTO mechanism (Phase 3.2.0 hardened this — every component family round-trips).
- Do **not** add per-NPC overrides. Per-archetype is the v0.1 granularity (consistent with WP-3.2.5).
- Do **not** apply repulsion during rescue scenarios (CPR, Heimlich) where bystanders intentionally must be close. Use `RescueIntentComponent` presence as the bypass signal.

---

## Design notes

### `PersonalSpaceComponent`

```csharp
public class PersonalSpaceComponent : IComponent {
    public float RadiusMeters { get; set; }       // typical: 0.4 – 0.9
    public float RepulsionStrength { get; set; }  // 0..1, fraction of overlap to apply per tick
}
```

Defaults at spawn (before per-archetype JSON applied): `RadiusMeters = 0.6`, `RepulsionStrength = 0.3`. These produce visible-but-soft spacing.

### `SpatialBehaviorSystem`

Tick phase: **cleanup**, after `MovementSystem`, before `WorldStateProjector`. (If those are different phases in the current pipeline, place it after movement writes but before any render-side projection reads.)

Algorithm per tick:

1. Build a spatial index of NPCs (or iterate naively if N ≤ 30 and the FPS gate holds — measure first; the engine targets 30 NPCs at 60 FPS, so an O(N²) inner loop on 30 entities is 900 comparisons per tick, well within budget).
2. For each pair `(a, b)` of alive NPCs (skip Incapacitated, Deceased, and any with active `RescueIntentComponent` toward each other):
   - Compute `distance = ||a.Position - b.Position||` in the XZ plane (ignore Y; floors are flat in v0.2).
   - Compute `minDistance = a.PersonalSpace.RadiusMeters + b.PersonalSpace.RadiusMeters`.
   - If `distance < minDistance`:
     - Compute overlap = `minDistance - distance`.
     - Compute push direction = `normalize(a.Position - b.Position)` in XZ.
     - Apply `a.Position += pushDir * overlap * a.RepulsionStrength * 0.5`
     - Apply `b.Position -= pushDir * overlap * b.RepulsionStrength * 0.5`
3. The 0.5 factor splits the push between the two NPCs. The `RepulsionStrength` factor (0..1) damps the per-tick effect so NPCs don't snap apart in one frame; over a few ticks they ease apart visibly but smoothly.
4. **Edge case — exact overlap (distance ≈ 0):** pick a deterministic pseudo-random direction (e.g., based on entity-id parity) so the two NPCs don't both push in the same direction and oscillate.

### Per-archetype tuning JSON

`docs/c2-content/tuning/archetype-personal-space.json`:

```jsonc
{
  "schemaVersion": "0.1.0",
  "archetypePersonalSpace": [
    {"archetype": "the-hermit",          "radiusMult": 1.40, "repulsionStrengthMult": 1.20},
    {"archetype": "the-cynic",           "radiusMult": 1.20, "repulsionStrengthMult": 1.10},
    {"archetype": "the-old-hand",        "radiusMult": 1.10, "repulsionStrengthMult": 1.0},
    {"archetype": "the-recovering",      "radiusMult": 1.10, "repulsionStrengthMult": 1.0},
    {"archetype": "the-affair",          "radiusMult": 0.95, "repulsionStrengthMult": 1.0},
    {"archetype": "the-climber",         "radiusMult": 1.0,  "repulsionStrengthMult": 1.0},
    {"archetype": "the-founders-nephew", "radiusMult": 1.0,  "repulsionStrengthMult": 1.0},
    {"archetype": "the-newbie",          "radiusMult": 0.85, "repulsionStrengthMult": 0.85},
    {"archetype": "the-vent",            "radiusMult": 0.80, "repulsionStrengthMult": 0.80},
    {"archetype": "the-crush",           "radiusMult": 0.75, "repulsionStrengthMult": 0.75}
  ]
}
```

`SpatialBehaviorInitializerSystem` (spawn phase, parallel to other initializers): reads the JSON, applies multipliers to the spawn-default `PersonalSpaceComponent` per archetype.

### Rescue-scenario bypass

When an NPC has `RescueIntentComponent` targeting another NPC, the spatial-behavior system skips the pair (in both directions). The rescuer must be able to reach the patient. After rescue completes (`RescueCompletionSystem` removes `RescueIntentComponent`), normal repulsion resumes.

### Soften-toggle interaction

None. Personal space is not a mature-content concern.

### FPS gate

Verify the 30-NPCs-at-60-FPS gate holds in a synthetic spawn-30-NPCs-overlapping test. If O(N²) is too slow at 30, consider a uniform-grid spatial index keyed by floor tile. Likely unnecessary at this scale; measure before optimizing.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/PersonalSpaceComponent.cs` | Component definition. |
| code | `APIFramework/Systems/Spatial/SpatialBehaviorSystem.cs` | The per-tick repulsion system. |
| code | `APIFramework/Systems/Spatial/SpatialBehaviorInitializerSystem.cs` | Spawn-phase initializer; reads tuning JSON. |
| code | `APIFramework/Systems/SimulationBootstrapper.cs` (modification) | Register the two new systems in the correct tick phases. |
| code | `APIFramework/Systems/Tuning/TuningCatalog.cs` (modification, if needed) | Load `archetype-personal-space.json`. |
| data | `docs/c2-content/tuning/archetype-personal-space.json` | Per-archetype tuning. |
| code | `APIFramework/Projection/WorldStateDto.cs` (modification) | Round-trip `PersonalSpaceComponent` (radius + strength). |
| schema | `Warden.Contracts/SchemaValidation/world-state.schema.json` (modification) | Additive minor (`entities[].personalSpace?` optional object). Bump v0.5.0 → v0.5.1. |
| test | `APIFramework.Tests/Systems/Spatial/PersonalSpaceComponentTests.cs` | Component round-trip. |
| test | `APIFramework.Tests/Systems/Spatial/SpatialBehaviorSystemTests.cs` | NPCs separate over N ticks; rescue bypass works; archetype bias produces differential spacing. |
| test | `APIFramework.Tests/Systems/Spatial/PersonalSpaceJsonTests.cs` | JSON validation. |
| test | `APIFramework.Tests/Systems/Spatial/Spatial30NpcFpsTests.cs` | Synthetic 30-NPC ticks-per-second check (target: ≥1000 ticks-per-second on the test runner; far above the 60 FPS = 60 ticks/sec real-time bound). |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Confirm MAC-010 entry post-implementation; revise if shape changed. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | Two NPCs spawned at the same position separate to at least `minDistance` within 30 ticks. | unit-test |
| AT-02 | Two NPCs walking toward each other on a collision course pass with at least `minDistance * 0.95` minimum separation. | unit-test |
| AT-03 | Two Hermit NPCs in steady state (idle) maintain larger spacing than two Vent NPCs in steady state. | integration-test |
| AT-04 | An NPC with `RescueIntentComponent` targeting an Incapacitated NPC can reach distance 0 (touch). | unit-test |
| AT-05 | Incapacitated NPCs do not push or get pushed by alive NPCs. | unit-test |
| AT-06 | All cast-bible archetypes present in `archetype-personal-space.json`; multipliers in valid range (0.5..2.0). | unit-test |
| AT-07 | `PersonalSpaceComponent` round-trips through `WorldStateDto` JSON. Mid-spatial-nudge save loads to identical state. | integration-test |
| AT-08 | 30 NPCs in a 5×5 cluster reach steady-state non-overlapping configuration in ≤ 60 ticks; FPS-gate-equivalent test passes. | integration-test |
| AT-09 | All Phase 0/1/2/3 tests stay green. | regression |
| AT-10 | `dotnet build` warning count = 0; `dotnet test` all green. | build + test |
| AT-11 | Schema bump v0.5.0 → v0.5.1 is additive-minor; existing v0.5.0 saves load against v0.5.1 schema (the new field is optional). | unit-test |

---

## Mod API surface

This packet introduces **MAC-010: PersonalSpaceComponent + spatial-behavior tuning**. See `docs/c2-infrastructure/MOD-API-CANDIDATES.md`.

The component shape is intentionally minimal (radius + strength). Modders adding social-distancing-during-illness, introvert-trait deepening, or new archetypes can extend the tuning JSON without code changes. Future Mod API formalization will document the registration pattern.

The spatial-behavior algorithm is private; modders modify behavior via tuning, not by replacing the system. If a future need arises for replaceable behavior (e.g., a "personal space scales with mood" mod), that becomes a new component family or a system-extension hook — not a today problem.

---

## Followups (not in scope)

- Spatial index optimization beyond O(N²) — only if 30-NPC FPS gate fails. Measure first.
- NPC-vs-prop soft repulsion (NPCs walk around chairs naturally rather than clipping). Future polish.
- Personal space scaling with mood (irritated NPCs want more space). Future deepening.
- Multi-floor adaptation (post WP-4.2.0). Trivial — Y-coordinate compares tag the floor.
- Animation tie-in: NPCs in an active personal-space adjustment could face away briefly. Future animation polish (couples to WP-4.0.E).

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT NEEDED

This is a Track 1 (engine) packet. All verification is handled by the xUnit test suite. Once `dotnet test` returns green for `APIFramework.Tests`, the packet is ready to push and PR. **No Unity Editor steps required.**

(However, the next post-merge PT session will surface whether the spacing reads naturally in the playtest scene. That's expected playtest follow-up, not a gate on this packet.)

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm you are in a dedicated worktree at `.claude/worktrees/sonnet-wp-4.0.b/` on branch `sonnet-wp-4.0.b` based on recent `origin/staging`. If anything is wrong, stop and notify Talon.
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

Target: **$0.50–$1.20** per packet. If costs approach the upper bound without acceptance criteria nearing completion, **escalate to Talon** by stopping work and committing a `WP-4.0.B-blocker.md` note.

Cost-discipline:
- Read reference files at most once per session.
- Run `dotnet test --filter` against the focused subset during iteration; full suite only at the end.
- If the spatial index needs upgrading from naive O(N²), don't reach for a complex grid — measure first; the test-runner numbers tell you what's actually slow.

### Self-cleanup on merge

Before opening the PR:

1. **Check downstream dependents:**
   ```bash
   git grep -l "WP-4.0.B" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. **If the grep returns no results**: include `git rm docs/c2-infrastructure/work-packets/WP-4.0.B-npc-spatial-anti-overlap.md` in the staging set. Add `Self-cleanup: spec file deleted, no pending dependents.` to the commit message.

3. **If the grep returns one or more pending packets**: leave the spec file in place. Add a one-line status header:
   ```markdown
   > **STATUS:** SHIPPED to staging YYYY-MM-DD. Retained because pending packets depend on this spec: <list>.
   ```
   Add `Self-cleanup: spec retained, dependents: <list>.` to the commit message.

4. **Do not touch** files under `_completed/` or `_completed-specs/`.

5. The git history (commit message + PR body) is the historical record. The spec file itself is ephemeral once shipped without dependents.
