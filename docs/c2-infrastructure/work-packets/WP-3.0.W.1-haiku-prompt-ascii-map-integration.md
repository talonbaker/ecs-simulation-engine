# WP-3.0.W.1 — Haiku Prompt ASCII-Map Integration

> **Track:** Track 1 (engine, headless) per `docs/PHASE-3-REALITY-CHECK.md`. **Warden-gated** (`#if WARDEN`) per SRD §8.7.
> **Position in sub-phase:** Direct followup to WP-3.0.W. Wires the canonical projector into the Haiku/Sonnet validation prompt pipeline so spatial reasoning replaces JSON-position-floats with a literal floor plan.

**Tier:** Sonnet
**Depends on:** WP-3.0.W (`AsciiMapProjector`, `AsciiMapOptions`, `ECSCli world map`).
**Parallel-safe with:** All Track 1 engine packets (3.2.x). All Track 2 sandbox/integration packets. Disjoint file surface from chore-rotation / rescue / physics packets.
**Timebox:** 60 minutes
**Budget:** $0.30

---

## Goal

Replace the current "Sonnet/Haiku reads `WorldStateDto` JSON and constructs spatial intuition from `position: {x, y, z}` floats" approach with one where every prompt that needs spatial reasoning **gets the ASCII map appended automatically**, cached at the slab-3 boundary so the stable parts (walls, doors, room shading, fixed furniture) cost zero tokens after the first request.

The `cast-validate` smoke mission already exists and reads `WorldStateDto` directly. The 3.2.x emergent-gameplay packets (chore rotation, rescue) and the future evacuation/proximity validates all need spatial reasoning. This packet makes the map a first-class slab in `PromptCacheManager` so any Opus-authored mission can opt in with one flag.

This packet ships:

1. A new `MapSlab` content type alongside the existing `PromptSlab`. Carries the rendered map + legend split: walls/rooms/furniture in a `Stable` cached slab, NPCs/hazards in a `Volatile` per-request slab.
2. A `MapSlabFactory` that consumes a `WorldStateDto` and `AsciiMapOptions` and produces the two slabs.
3. An `OpusSpecPacket.SpatialContext` opt-in flag (additive schema field). When `true`, the orchestrator's dispatch code calls `MapSlabFactory.Build()` and inserts the slabs into the Sonnet/Haiku request. When omitted/`false`, behaviour is unchanged.
4. Updated `cast-validate.json` smoke spec — flips `SpatialContext = true` to dogfood the new path.
5. A new `chore-validate.json` example spec scaffolded for the WP-3.2.3 chore-rotation system, demonstrating spatial assertion patterns Haiku validates against the map.
6. xUnit tests under `Warden.Orchestrator.Tests` confirming: the map slabs render correctly, the cache disposition is right (`Stable` = `Ephemeral1h`, `Volatile` = uncached), token-budget warning fires when map exceeds 4 000 tokens, the legend never truncates, the smoke mission passes with `--mock-anthropic`.

This is a Track 1 (engine, headless) packet. xUnit-verifiable. No Unity work.

---

## Reference files (read in this order)

1. `docs/c2-infrastructure/work-packets/WP-3.0.W-ascii-floor-plan-projector.md` — the projector contract this packet wires into prompts. **Read first.**
2. `Warden.Orchestrator/Cache/PromptCacheManager.cs` — the four-slab prompt model. The integration point is the `missionSlabs` argument to `BuildRequest`.
3. `Warden.Contracts/Handshake/OpusSpecPacket.cs` (or wherever `OpusSpecPacket` lives) — the schema gets an additive `SpatialContext` boolean. Schema bump is an additive minor — read `docs/c2-infrastructure/SCHEMA-ROADMAP.md` versioning rules before authoring.
4. `Warden.Orchestrator/Dispatcher/SonnetDispatcher.cs` and `HaikuDispatcher.cs` — these are the dispatch sites that need to consult `SpecPacket.SpatialContext`.
5. `examples/smoke-mission-cast-validate.md` and `examples/smoke-specs/cast-validate.json` — the existing smoke mission this packet upgrades.
6. `Warden.Telemetry/AsciiMap/AsciiMapProjector.cs` — verify the public API matches this spec's expectations before wiring.

---

## Non-goals

- Do **not** modify `AsciiMapProjector.Render` or `AsciiMapOptions`. The API from WP-3.0.W is final.
- Do **not** add ANSI colour, multi-floor, or diff mode in this packet. Those are WP-3.0.W's followups.
- Do **not** change the four-slab prompt model. Add a slab; don't refactor the model.
- Do **not** force `SpatialContext = true` on existing missions that don't need it. Token-cost discipline.
- Do **not** wire this into the chore-rotation engine code. WP-3.2.3 already shipped; this packet only authors the validate-spec scaffolding under `examples/`.
- Do **not** build a UI for browsing maps (that's the in-Editor Unity overlay, deferred to a future Track 2 packet).

---

## Design notes

### The Stable / Volatile split

The map breaks naturally into two parts:

- **Stable** — walls, doors, room category shading, fixed furniture (microwave, fridge, sink, toilet). These change only when the player builds; for any given mission run they're effectively constant. Lives in slab 3 (cached, `Ephemeral1h` for Haiku batches, `Ephemeral5m` for Sonnet authoring).
- **Volatile** — NPC positions, transient hazards (stains, fires, corpses), the legend. These change every tick. Lives in slab 4 (user turn, uncached).

**Token economics:** for a 60×60 world with ~10 rooms, the stable layer is ~1 200 tokens. With 25-scenario Haiku batches, the cached version pays that cost once and reuses it 24 times — that's ~28 800 tokens of input savings per batch. Real money over the course of Phase 3.2.x balance tuning.

`MapSlabFactory.Build(state, options)` returns a `(MapSlab Stable, MapSlab Volatile)` tuple. The orchestrator places `Stable` in `missionSlabs` and prepends `Volatile.Text` to the user turn body.

### Glyph contract: as authored in WP-3.0.W

This packet does **not** redefine glyphs. It uses `AsciiMapProjector.Render(state, options)` and accepts whatever it produces. If the glyph table changes in a future WP-3.0.W revision, this packet's prompt slabs adopt the change automatically.

### Schema additive

`OpusSpecPacket` gains a `SpatialContext` boolean (default `false`). Schema bump is additive minor — `opus-to-sonnet.schema.json` bumps from `0.5.0` to `0.5.1` (or whatever the current minor is + 0.0.1). Per the schema-roadmap rules: optional new field, default false, v0.5.0 consumers ignore, v0.5.1 consumers honour.

### Prompt structure when SpatialContext = true

Slab 3 (cached) gains:

```
=== WORLD MAP — STABLE ===

╔══════════════════════════════════════════════════════════════╗
║                                                              ║
║  ┌──────────┐  ┌─────────────┐                              ║
║  │ D D D    │  │             │                              ║
║  │ C   C    │  │  M  F  S    │                              ║
║  │          │  │             │                              ║
║  └─────·────┘  └──────·──────┘                              ║
║░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░                       ║
║░░░░·░░░░░░░░░░░░░░░░░░░░░░░·░░░░░░░░                       ║
║                                                              ║
╚══════════════════════════════════════════════════════════════╝

LEGEND (FIXED FEATURES)
  M — Microwave (33, 24)
  F — Fridge (35, 24)
  S — Sink (37, 24)
  D — Desks (28-30, 18-20)
  C — Chairs (paired with desks)
  · — Doors (open at: 23,18  31,21)
```

Slab 4 (user turn body) gains, prepended to whatever the per-scenario prompt was:

```
=== WORLD MAP — TICK {N} ({HH:MM}) ===

  d (32, 18) — Eating
  f (28, 18) — Working
  g (47, 18) — Walking

(NPCs and hazards overlaid on the stable map above; ASCII layer omitted to save tokens.)

```

That last note is load-bearing: we don't re-render the whole map; we just enumerate what's *changed* relative to the cached stable layer. Models reason about "Donna at (32, 18) sitting in front of the cached `D` glyph" without burning per-request tokens on rewalls.

### Optional full-volatile-render mode

For prompts that explicitly request the full re-rendered map (e.g., a fire scenario where wall geometry has changed), `AsciiMapOptions.IncludeStableInVolatile = true` causes `Volatile` to carry the full map again. Default `false` (delta mode).

---

## Public API contract

```csharp
namespace Warden.Orchestrator.Prompts;

#if WARDEN
public sealed record MapSlab(string Text, CacheDisposition Cache);

public static class MapSlabFactory
{
    /// <summary>
    /// Renders the world state into a (Stable, Volatile) pair of prompt slabs
    /// per WP-3.0.W.1's two-tier cache strategy.
    /// </summary>
    public static (MapSlab Stable, MapSlab Volatile) Build(
        WorldStateDto state,
        AsciiMapOptions options = default);
}
#endif
```

The orchestrator's dispatch code does:

```csharp
if (specPacket.SpatialContext && state is not null)
{
    var (stable, volatileSlab) = MapSlabFactory.Build(state);
    missionSlabs = missionSlabs.Append(new PromptSlab("world-map-stable",
                                                      stable.Text,
                                                      stable.Cache));
    userTurnBody = volatileSlab.Text + "\n\n" + userTurnBody;
}
```

---

## Acceptance criteria

| # | Criterion | Type |
|---|---|---|
| AT-01 | `MapSlabFactory.Build` exists with the public API above; compiles with `#if WARDEN` guard. | unit-test |
| AT-02 | Empty `WorldStateDto` (no rooms, no entities) → both slabs render minimal placeholder text; no exception. | unit-test |
| AT-03 | Single-room world: `Stable.Text` includes the wall outline + room shading; `Volatile.Text` is the timestamp header + empty NPC list. | unit-test |
| AT-04 | Multi-NPC world: every NPC appears in `Volatile.Text` with coords + current `IntendedAction`; none appear in `Stable.Text`. | unit-test |
| AT-05 | Hazard rendering: stains/fire/corpses appear in `Volatile.Text`, **not** in `Stable.Text` (hazards are transient). | unit-test |
| AT-06 | Furniture (microwave, fridge, sink, toilet) renders in `Stable.Text` with legend entry. Movable items render in `Volatile.Text`. | unit-test |
| AT-07 | `IncludeStableInVolatile = true` → `Volatile.Text` carries the full re-rendered map (stable + volatile combined). | unit-test |
| AT-08 | `Stable.Cache` is `Ephemeral1h` for Haiku batches, `Ephemeral5m` for Sonnet authoring. `Volatile.Cache` is always `Uncached`. | unit-test |
| AT-09 | `OpusSpecPacket.SpatialContext` defaults to `false`. Schema bump is additive minor; old specs deserialize without error. | unit-test |
| AT-10 | `SonnetDispatcher.RunAsync` honours `SpatialContext = true` by injecting both slabs. With `false`, behaviour is byte-identical to before this packet. | integration-test |
| AT-11 | `HaikuDispatcher.RunAsync` (via `BatchScheduler`) ditto. | integration-test |
| AT-12 | Token-budget warning: if `Stable.Text` exceeds 4 000 tokens (heuristic: 16 000 chars), emit a stderr warning. | unit-test |
| AT-13 | `examples/smoke-specs/cast-validate.json` updated to set `SpatialContext = true`; existing smoke-mission test still passes with `--mock-anthropic`. | integration-test |
| AT-14 | New `examples/smoke-specs/chore-validate.json` spec exists, demonstrating spatial assertion patterns for chore rotation. Does not need to run a full mission; smoke-test that it parses cleanly via the existing schema validator. | unit-test |
| AT-15 | All Phase 0/1/2/3.0.x/3.1.x/3.2.0/3.2.1/3.2.3/3.2.4 tests stay green. | regression |
| AT-16 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-17 | `dotnet test ECSSimulation.sln` — all green, no exclusions. | build + unit-test |

---

## Followups (not in scope)

- **WP-3.0.W.2 — ANSI colour codes.** Stress-coloured NPC letters, room-category-tinted shading. Terminals that don't support ANSI strip the codes naturally.
- **WP-3.0.W.3 — Diff mode.** Render only the *delta* between two `WorldStateDto` snapshots. Excellent for tick-by-tick `--watch` output and for "what changed during this scenario" Haiku assertions.
- **Multi-floor support** — when Phase 4.0.1 lands multi-floor, `MapSlabFactory.Build` extends to optionally produce one (Stable, Volatile) pair per floor.
- **Generated `.unity` scenes from ASCII maps** — Talon's idea: take an ASCII floor plan as input, produce a Unity scene with walls, rooms, and furniture placed accordingly. This is a real packet candidate but not this one. Probably WP-3.0.W.4 or a Track 2 sandbox packet (`WP-3.1.S.X — ASCII-to-Scene Generator`). Couples to Phase 4.0.0's UX bible v0.2 because the floor-plan-as-source-of-truth changes the build-mode authoring model.
- **Per-archetype NPC glyph variants** — initials only get us so far; the cast bible's archetypes could each get a unique mark (different letter case, accent, or symbol). Future content packet.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT NEEDED

This is a Track 1 (engine, headless) packet. All verification is handled by the xUnit test suite. Once `dotnet test` returns green for `Warden.Orchestrator.Tests`, `Warden.Telemetry.Tests`, and the smoke-mission suite, the packet is ready to push and PR. **No Unity Editor steps required.**

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

Target: **$0.30** (60-minute timebox). This is a small surgical packet — schema bump, slab factory, dispatch wiring, two example specs. If costs approach $0.60 without the acceptance criteria being achievable, **escalate to Talon** by stopping work and committing a `WP-3.0.W.1-blocker.md` note.

Cost-discipline rules of thumb:
- Reuse `AsciiMapProjector.Render` directly. Don't reimplement glyph logic.
- Don't refactor the four-slab prompt model. The `MapSlab` is just a `PromptSlab` with a clearer name.
- The schema bump is additive — read the SCHEMA-ROADMAP rules once and follow them; don't open a debate about MAJOR vs MINOR.
- Resist scope creep into ANSI colour, diff mode, multi-floor, or scene generation. All are explicit non-goals.

### Self-cleanup on merge

Before the PR is merged:

1. **Check downstream dependents:**
   ```bash
   git grep -l "WP-3.0.W.1" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. **Expected result:** No downstream dependents. **Delete the spec file** as part of the merge commit:
   ```bash
   git rm docs/c2-infrastructure/work-packets/WP-3.0.W.1-haiku-prompt-ascii-map-integration.md
   ```
   Add `Self-cleanup: spec deleted, no dependents.` to the commit message.

3. **Do not touch** files under `_completed/` or `_completed-specs/`.

4. The git history (commit message + PR body) is the historical record. The spec file itself is ephemeral once shipped without dependents.

---

*WP-3.0.W.1 makes the ASCII map a first-class participant in every spatial-reasoning prompt. After this packet ships, every chore-rotation, rescue-range, fire-evacuation, or proximity validate that goes through the orchestrator includes a literal floor plan that Sonnet and Haiku read directly. Cost-disciplined via the Stable/Volatile cache split. Schema additive. No risk to the visual layer.*
