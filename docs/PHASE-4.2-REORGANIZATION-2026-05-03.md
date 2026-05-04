# Phase 4.2 Reorganization — Zones over Multi-Floor

**Date:** 2026-05-03
**Author:** Opus (post-Wave 4 conversation with Talon)
**Status:** Drafted; awaits Talon sign-off before dispatch.

---

## TL;DR

The original Phase 4.2.x scope opened with **WP-4.2.0 — Multi-floor topology**, then four scenario packets (plague / PIP / fire / affair) layered on top. Talon's 2026-05-03 review surfaced two problems with that plan:

1. **The "multi-floor unblocks the scenarios" framing was wrong.** Only fire (4.2.3) materially needs multi-floor (for evacuation-at-scale stress testing). Affair, PIP, and plague work fine on a single floor.
2. **Adding multi-floor on top of an unverified Wave 4 foundation repeats the wave-1 mistake.** The kickoff brief itself articulated the lesson: *"don't build on top of a foundation that isn't working."* The authoring loop substrate just shipped; the Editor-side palette UI hasn't been wired yet. Layering multi-floor underneath that would compound the trap.

In its place, Talon proposed a **zone system**: separate physical areas (parking lot, warehouse, breakroom, upstairs office) modeled as peer scenes the camera switches between, with NPCs in inactive zones running at reduced simulation fidelity. That's a stronger architectural fit for an office sim, and it directly serves the project's #1 priority — **performance** — by enabling the 30-NPC FPS gate to extend to 100+ NPCs across many zones with only ~30 visible at full fidelity.

This memo lays out the new Phase 4.2.x structure and the rationale.

---

## What changed

| Old WP | Old Scope | New WP | New Scope |
|:---|:---|:---|:---|
| 4.2.0 | Multi-floor topology | **4.2.0** | **Zone substrate** (engine: ZoneIdComponent, multi-world-def loader, transition trigger) |
| — | — | **4.2.1** | **Active zone + camera hook** (engine + Unity glue for switching the visible zone) |
| — | — | **4.2.2** | **Simulation level-of-detail** (perf-critical: per-zone tick fidelity) |
| 4.2.1 | Plague week | 4.2.3 | Plague week (renumbered) |
| 4.2.2 | PIP arc | 4.2.4 | PIP arc (renumbered) |
| 4.2.4 | Affair detection | 4.2.5 | Affair detection (renumbered) |
| 4.2.3 | Fire / disaster | 4.2.6 | Fire / disaster (renumbered; now exercises cross-zone evacuation) |

Vertical multi-floor (literal stacked floors) is **not killed** — it becomes a future feature whose gate is "the zone system is shipped, used in real play, and there is a specific scenario whose readability requires vertical visualization." See FF-017 in `docs/future-features.md`.

---

## Why zones beat multi-floor

### Architectural fit

A zone is just a self-contained physical area with its own room layout, NPCs, props. The world has N zones; the player is *in* one at a time; the camera shows that zone. Transitions happen at special tiles ("walk through the breakroom door" → camera + active-zone state both switch).

This maps cleanly to the existing world-definition format:

- **Each zone = one `world-definition.json` file.** That format already exists (Phase 1 substrate; round-trip-validated by WP-4.0.I).
- **The multi-zone loader = N x existing single-zone loader.** Additive, not a rewrite.
- **Authoring is per-zone.** The Wave 4 author-mode tools (Ctrl+Shift+A → place rooms / lights / NPCs → save) work per-zone with no changes. Modders ship "scene packs" by dropping in `*.json` files.

Multi-floor would have required:
- Extending `RoomComponent` and `PathfindingService` to be floor-aware
- A new stairwell-traversal pathfinding layer
- Schema bump 0.5 → 0.6
- Coordinated authoring tools updates

…all of which is more invasive than zones for *less* architectural value.

### Performance fit (the #1 priority)

The existing 30-NPC-at-60-FPS gate caps the simulation at ~30 visible NPCs. With zones + LoD:

- **Active zone:** ~30 NPCs at full fidelity (existing systems unchanged).
- **Inactive zones:** N more NPCs at coarse fidelity — drives decay, memory accumulates, but expensive systems (pathfinding, physics, conversation, animation state) skip ticks or run at 1/4 rate.

Concrete target: **100 NPCs across 4 zones, 30 visible at any time, holds the existing 60 FPS gate.** That's a 3.3× capacity gain over today's 30-NPC ceiling without sacrificing the visible-zone feel.

The LoD design is genuinely interesting work (which systems can safely skip ticks; how to fairly handle "Donna choked while you were in the breakroom" — see WP-4.2.2 for the full design conversation), but it's the right place to invest because every additional zone the project ships compounds its value.

### Gameplay fit

Office spaces ARE zone-shaped more than they are floor-shaped. In an office sim the legible mental model is:

> "The breakroom is a different *place*. The parking lot is a different *place*. The smoking area outside is a different *place*."

…not:

> "The first floor and the second floor are different floors."

(Most small offices are single-floor anyway, with named areas. Multi-floor is a building-management sim concern more than an office sim concern.)

Zones give scenarios natural staging:

- **Plague week** spreads through an active-zone with infected-status-on-NPC; LoD-mode NPCs get probabilistic infection on zone-switch.
- **Affair detection** happens between two NPCs in the same zone (no cross-zone awkwardness about "what does the other floor know").
- **Fire / disaster** stresses cross-zone evacuation at scale — directly tests the zone-transition + LoD systems under load.
- **PIP arc** is a personal-relationship arc; happens wherever the involved NPCs are.

---

## What stays the same

Everything in Wave 4 still works as designed. The zone system is *additive* on top of the existing single-zone simulation:

- Today, the engine loads ONE `world-definition.json` and runs it. That continues to work.
- Tomorrow, the engine optionally loads N world-definitions, designates one active, and the zone substrate manages cross-zone state. Single-zone scenes continue to work unchanged.
- The Wave 4 authoring tools (Ctrl+Shift+A author mode + palette + save) become the per-zone editor with no code changes — same `WorldDefinitionWriter`, same `IWorldMutationApi`, same JSON schema.

The schema does NOT need to bump. The existing `world-definition.json` format already has everything a zone needs (floors / rooms / lights / npcSlots / anchor objects). Multi-zone is a *runtime* concept — a registry of loaded zones plus an active-zone pointer — not a schema concept.

---

## What's still hard

Two pieces are genuinely hard and deserve careful design:

### 1. Simulation LoD (WP-4.2.2)

Which systems can safely run at coarser intervals for inactive-zone NPCs without breaking gameplay invariants?

- **Safe to coarsen:** drives decay, social drives accumulation, schedule cursor advancement, idle movement.
- **Unsafe to coarsen:** life-state transitions (choking → death has a fairness contract — the player should be able to intervene), narrative-event recording (chronicle integrity).
- **Open design question:** what happens when an NPC chokes in an inactive zone? Three options:
  1. **Skip the choking system entirely in inactive zones** — coarsening implicitly avoids choke initiation; players never have an "unwitnessed death" problem.
  2. **Run choking at 1/4 rate, deliver a "Donna is in trouble in the breakroom!" notification** — fair to the player, but introduces a notification carrier (FF-005 territory).
  3. **Run choking at full fidelity for ALL NPCs regardless of zone** — gives up some perf but preserves narrative integrity.

The packet authors option 3 as the default (safest) and discusses the others as followups.

### 2. Cross-zone narrative continuity

If an NPC walks from zone A to zone B (transition trigger), does their chronicle remember the trip? Their relationship state? Their drives at the moment of transition?

Default: yes, the NPC's components are the source of truth and they travel with the entity across zones. Zone-switching is a teleport at the entity level — same entity, different ZoneIdComponent value. No state loss.

Edge cases (out of scope for v0.1):
- An NPC in zone A interacts with an NPC in zone B (e.g., a phone call). Today: not modeled. Future: a cross-zone interaction system that doesn't require both NPCs to be in the same physical space.

---

## New Phase 4.2.x dispatch order

```
WP-4.2.0 — Zone substrate                    (engine, parallel-safe with anything not touching Bootstrap or Mutation)
       ↓
WP-4.2.1 — Active zone + camera hook         (engine + Unity glue; depends on 4.2.0)
       ↓
WP-4.2.2 — Simulation LoD                    (engine, perf-critical; depends on 4.2.0; can land in parallel with 4.2.1)
       ↓
WP-4.2.3 — Plague week                       (depends on 4.2.0; 4.2.2 nice-to-have)
WP-4.2.4 — PIP arc                           (depends on 4.2.0)
WP-4.2.5 — Affair detection                  (depends on 4.2.0)
WP-4.2.6 — Fire / disaster                   (depends on 4.2.0 + 4.2.2; cross-zone evac stress test)
```

Wave 4 (M, I, J, K, L) is **not** a hard dependency for the zone substrate — these tracks are disjoint. But the project benefits from Wave 4 being verified end-to-end before zone work lands, so the user's authoring tools cover both single-zone (today) and multi-zone (tomorrow) workflows from day one.

---

## Independent extension packets (orthogonal track)

Two follow-ups to Wave 4 that don't touch the zone system but are worth landing when there's bandwidth:

- **WP-4.0.M.1 — Cast badge generation port.** Completes the deferred portion of WP-4.0.M (badge generation from `data.js#generateBadge`). Small, pure-engine, sets up the future hire-screen UI.
- **WP-4.0.N — Hire reroll economy substrate.** Extends WP-4.0.M's `Generate(Random)` seam with a token-spending wrapper. Substrate for the loot-box hire mechanic. Independent of zones and of UI.

These can land in any order at any time. They are not part of the Phase 4.2 critical path.

---

## Sign-off needed

This reorganization is drafted but not committed to dispatch. Before any 4.2.x packet ships:

1. **Talon reviews this memo** and the new packet specs (WP-4.2.0, 4.2.1, 4.2.2, 4.2.3, 4.2.4, 4.2.5, 4.2.6).
2. **Confirms the zone direction** vs. multi-floor (or proposes a third path).
3. **Confirms the LoD default** (option 3 — full-fidelity life-state systems regardless of zone) or chooses option 1 / 2.
4. **Confirms scenario priorities** — does plague go before PIP? Should fire wait for the LoD system to land, or run on full-fidelity zones initially?

Once signed off, the packet sequence proceeds via the standard merge-on-green-tests protocol.
