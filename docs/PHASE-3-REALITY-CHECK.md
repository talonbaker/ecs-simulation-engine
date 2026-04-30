# Phase 3 Reality-Check Addendum

> **Authority:** This document overrides `PHASE-4-KICKOFF-BRIEF.md` wherever they disagree about Phase 3 completion state.
> **Author:** Opus General, 2026-04-30.
> **Status:** Live.

---

## Why this document exists

`PHASE-4-KICKOFF-BRIEF.md` was authored aspirationally and asserts that Phase 3.0.x, 3.1.x, **and 3.2.x are all complete**. They are not. Future Opus sessions must read *this* document before treating the Phase 4 brief's roadmap as actionable.

The Phase 4 brief should not be deleted — its sub-phase plan (4.0 → 4.4) remains the long-horizon roadmap. But its preamble is wrong about what shipped.

---

## Actual completion state, as of 2026-04-30

### Done and merged

- **WP-3.0.0** — `LifeStateComponent` + cause-of-death events.
- **WP-3.0.1** — Choking-on-food scenario.
- **WP-3.0.2** — Deceased-entity handling + bereavement.
- **WP-3.0.6** — Fainting (Incapacitated → Alive recovery path; added during integration).
- **WP-3.1.A → WP-3.1.H** — Unity scaffolding bundle. **Compiles and tests pass, but visual output diverges from spec.** See "The 3.1.x visual mismatch" below.

### Pending — still in active `docs/c2-infrastructure/work-packets/`

- **WP-3.0.3** — Slip-and-fall + locked-in-and-starved. Blocked on 3.0.4.
- **WP-3.0.4** — Live-mutation hardening (`IWorldMutationApi`, `StructuralChangeBus`, pathfinding cache). **Top of dependency chain — dispatch first.**
- **WP-3.0.5** — `ComponentStore<T>` typed-array refactor. Blocked on all of 3.0.x landing.
- **WP-3.2.0 → WP-3.2.6** — All seven 3.2.x packets (save/load hardening, sound trigger bus, physics, chore rotation, rescue mechanic, per-archetype tuning, animation states). All gated behind "DO NOT DISPATCH UNTIL 3.1.x IS MERGED" — that gate is now satisfied for the *engine-side* portions of these packets.

### Deferred

- **All of Phase 4** is paused until (a) the engine backlog above closes and (b) the visual sandbox track has shipped enough integration packets to make Phase 4.0.0 (UX bible v0.2) a meaningful conversation. Re-evaluate once 3.2.x lands.

---

## The 3.1.x visual mismatch — the core lesson

The 3.1.x bundle compiled, all xUnit tests passed, the FPS gate held — and Unity still rendered nothing where Talon expected something. Six follow-up "Phase 3.1.x fix" commits patched it: missing renderers in the committed scene, axis confusion (`Position.Y` vs `Position.Z`), sub-pixel dot sizing, camera altitude wrong for the 60-tile world, missing transitive dependencies, missing scripting-define references.

**Why this happened:** xUnit tests verify ECS contracts (entity counts, component values, transition logic). They cannot see Inspector wiring, scene file contents, axis conventions, world-unit scale, camera framing, or whether *anything is on screen*. The Sonnet shipped against tests that couldn't catch the divergence.

**The structural fix:** `docs/UNITY-PACKET-PROTOCOL.md` codifies a sandbox-first, atomic, prefab-driven Unity workflow that makes future visual work verifiable in 5-minute Inspector passes. All future Unity packets follow it.

**The naming convention:** Visual sandbox sub-phase is **WP-3.1.S.NN** (S = sandbox). Parallel to 3.1.A–H but explicitly atomic and Inspector-driven. The first integration packet that wires a sandbox-validated prefab into the live engine scene gets a follow-up `-INT` suffix (e.g., `WP-3.1.S.0-INT`).

---

## Forward roadmap

Two tracks running in parallel:

### Track 1 — Engine deepening (no visual gate)

Dependency-ordered dispatch:

1. WP-3.0.4 (live-mutation hardening) — ready, dispatch first.
2. WP-3.0.3 (slip-and-fall) — after 3.0.4.
3. WP-3.0.5 (ComponentStore refactor) — solo dispatch after Wave 2 merges.
4. WP-3.2.0 → WP-3.2.6 in dependency order. Engine-side only; sound bus and animation states emit triggers, the Unity host's consumption of those triggers belongs to Track 2.
5. WP-3.2.5 (per-archetype tuning JSONs) is the **balance loop substrate**. Talon iterates JSON values; Haiku validation runs verify curves; verdict reports are the feedback channel. No visual loop required for headless balancing per Q2(a).

### Track 2 — Visual sandbox (WP-3.1.S.NN)

Per `UNITY-PACKET-PROTOCOL.md`. Ordering will be drafted in a follow-up planning packet once the protocol doc is reviewed. Initial queue:

- WP-3.1.S.0 — Camera rig prefab + sandbox scene (extracts and locks in the 3.1.x camera fixes).
- WP-3.1.S.1 — Selection outline shader on cube/sphere sandbox.
- WP-3.1.S.2 — Draggable prop with snap-to-grid (table + banana sandbox).
- WP-3.1.S.3 — Click-to-inspect popup on bare cube.
- WP-3.1.S.4+ — Integration packets that wire validated prefabs into the live engine scene one at a time.

### Visual playtesting / "feel" balance pass (Q2(b))

Explicitly later. Requires multiple human playtesters. Not on this roadmap.

---

## Spec-file housekeeping

The files below are **pending**, not done. Future sessions should not move them to `_completed-specs/` until they actually ship:

```
docs/c2-infrastructure/work-packets/WP-3.0.3-slip-and-fall-and-locked-in-and-starved.md
docs/c2-infrastructure/work-packets/WP-3.0.4-live-mutation-hardening.md
docs/c2-infrastructure/work-packets/WP-3.0.5-component-store-typed-array-refactor.md
docs/c2-infrastructure/work-packets/WP-3.2.0-save-load-round-trip-hardening.md
docs/c2-infrastructure/work-packets/WP-3.2.1-sound-trigger-bus.md
docs/c2-infrastructure/work-packets/WP-3.2.2-rudimentary-physics.md
docs/c2-infrastructure/work-packets/WP-3.2.3-chore-rotation-system.md
docs/c2-infrastructure/work-packets/WP-3.2.4-rescue-mechanic.md
docs/c2-infrastructure/work-packets/WP-3.2.5-per-archetype-tuning-jsons.md
docs/c2-infrastructure/work-packets/WP-3.2.6-silhouette-animation-state-expansion.md
```

---

*Update this document when 3.0.x closes, when 3.2.x closes, and when any WP-3.1.S.NN packet ships. Stale completion-state claims are how this whole mess started.*
