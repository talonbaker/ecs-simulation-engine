# WP-3.0.W — ASCII Floor-Plan Projector (Warden-side)

> **Track:** Track 1 (engine, headless) per `docs/PHASE-3-REALITY-CHECK.md`. **Warden-gated** (`#if WARDEN`) per SRD §8.7.
> **Position in sub-phase:** Section B parallel-engine candidate, promoted from `docs/PHASE-3-PARALLEL-ENGINE-CANDIDATES.md`. Dispatchable in parallel with any in-flight engine or Unity packet — disjoint file surface.

**Tier:** Sonnet
**Depends on:** WP-3.0.4 (live-mutation hardening — for the `IWorldMutationApi` and `StructuralChangeBus` types referenced in test fixtures), and any `WorldStateDto` schema commits already on staging. No runtime dependency on Phase 3.1.x or 3.2.x.
**Parallel-safe with:** All Track 1 engine packets. All Track 2 sandbox packets. All `-INT` integration packets.
**Timebox:** 90 minutes
**Budget:** $0.50

---

## Goal

Establish a **canonical text projection** of the world's spatial state — walls, doors, room shading, furniture, NPCs, hazards — rendered using Unicode box-drawing and shading characters, that both human collaborators (Talon) and LLM collaborators (Haiku validators, Opus general, future agents) can read directly.

Today, when Haiku validates a chore-rotation prompt that asks "did Donna route to the microwave?", Haiku reads `WorldStateDto` JSON: hundreds of entities with `position: {x, y, z}` floats and tries to build a mental spatial model. When Talon says "the wall here is wrong," Haiku has no shared visual to anchor that statement to. This packet fixes both: a single ASCII artifact is the shared spatial truth across all reasoners.

Architecturally clean — `Warden.*`-gated, strips at ship time, host-agnostic. SRD §8.7 fits perfectly. No conflict with the iconography axiom (that governs *player-facing* visuals; this is dev surface).

This packet ships:

1. A new `AsciiMapProjector` static class in `Warden.Telemetry`, behind `#if WARDEN`. Public API: `string Render(WorldStateDto state, AsciiMapOptions opts = default)`.
2. An `AsciiMapOptions` record carrying `FloorIndex`, `IncludeLegend`, `ShowHazards`, `ShowFurniture`, `ShowNpcs` toggles.
3. A canonical glyph table documented in code (XML doc comments) and in this spec.
4. An `ECSCli world map` verb that renders the current world snapshot to stdout. Optional `--watch` mode tails tick-by-tick during dev.
5. xUnit snapshot tests under `Warden.Telemetry.Tests` (or the closest matching test project) covering: empty-world, single-room, multi-room, doors-open-vs-closed, NPC collision-on-tile (`*` glyph), hazard rendering, legend formatting, and a 30-NPC perf microbench (must render in <50ms per call).

This is a Track 1 (engine, headless) packet. xUnit-verifiable. No Unity work. No file conflicts with any in-flight packet.

---

## Reference files (read in this order)

1. `docs/c2-infrastructure/00-SRD.md` §4.2 (determinism) and §8.7 (engine host-agnostic; WARDEN gating).
2. `docs/c2-infrastructure/PHASE-3-PARALLEL-ENGINE-CANDIDATES.md` — context on where this packet sits in the engine track.
3. `docs/c2-infrastructure/SCHEMA-ROADMAP.md` — `WorldStateDto` schema (rooms, lightSources, lightApertures, entities[].position, entities[].social, hazards). The projector reads what's already on the wire; it does not require a schema bump.
4. `APIFramework/Contracts/WorldStateDto.cs` (or wherever the DTO lives) — the input shape.
5. `Warden.Telemetry/` — the existing project that owns dev-time observability surfaces. Mirror its structure (file layout, namespace, `#if WARDEN` patterns).
6. `ECSCli/` — existing verbs follow a verb-handler pattern. The new `world map` verb mirrors the existing `ai describe` shape.

---

## Non-goals

- Do **not** modify `WorldStateDto` or any schema file. The projector consumes existing fields only.
- Do **not** propose a player-facing version of this. **This is dev surface only.** It strips at ship time via the `#if WARDEN` guard.
- Do **not** add ANSI colour codes at v0.1. Single-colour text. Future packet may add colour for terminals that support it.
- Do **not** add multi-floor rendering at v0.1 — render the requested floor only (`AsciiMapOptions.FloorIndex` defaults to 0). Multi-floor lands in Phase 4.0.1; the API already accepts the floor index, so future extension is additive.
- Do **not** add a "diff mode" (changes between two states). Useful follow-up; not in this packet.
- Do **not** replace the JSONL stream or the engine-fact-sheet generator. This is an additional surface, not a replacement.
- Do **not** integrate with Unity. The projector is pure C#; Unity hosts can call it for in-Editor debug overlays in a future packet, but no Unity code lands here.
- Do **not** wire this into any Haiku prompt template in this packet. Wire-up is its own follow-up packet (`WP-3.0.W.1` — Haiku prompt integration).

---

## Glyph contract (canonical)

This table is the spec for what gets rendered. The Sonnet reproduces it verbatim in the XML doc comment on `AsciiMapProjector.Render`.

### Walls

Interior walls use single-line box-drawing:

| Glyph | Codepoint | Meaning |
|---|---|---|
| `┌` | U+250C | Wall upper-left corner |
| `┐` | U+2510 | Wall upper-right corner |
| `└` | U+2514 | Wall lower-left corner |
| `┘` | U+2518 | Wall lower-right corner |
| `─` | U+2500 | Horizontal wall |
| `│` | U+2502 | Vertical wall |
| `┬` | U+252C | T-junction (wall extends down) |
| `┴` | U+2534 | T-junction (wall extends up) |
| `├` | U+251C | T-junction (wall extends right) |
| `┤` | U+2524 | T-junction (wall extends left) |
| `┼` | U+253C | Wall cross / four-way junction |

Exterior boundary uses double-line — distinguishes the office shell from interior walls at a glance:

| Glyph | Codepoint | Meaning |
|---|---|---|
| `╔` | U+2554 | Outer upper-left |
| `╗` | U+2557 | Outer upper-right |
| `╚` | U+255A | Outer lower-left |
| `╝` | U+255D | Outer lower-right |
| `═` | U+2550 | Outer horizontal |
| `║` | U+2551 | Outer vertical |
| `╦` | U+2566 | Outer T-down |
| `╩` | U+2569 | Outer T-up |
| `╠` | U+2560 | Outer T-right |
| `╣` | U+2563 | Outer T-left |

### Doors / apertures

| Glyph | Codepoint | Meaning |
|---|---|---|
| `·` | U+00B7 | Open door (mid-dot in wall position) |
| `+` | U+002B | Closed door |

### Floor shading by room category

| Glyph | Codepoint | Meaning |
|---|---|---|
| ` ` | U+0020 | Open office / generic walkable |
| `░` | U+2591 | Corridor / hallway |
| `▒` | U+2592 | Kitchen / break room |
| `▓` | U+2593 | Bathroom (privacy-shaded) |

`RoomComponent.Category` values map to these glyphs via a lookup table. Categories not in the table default to space.

### Furniture (uppercase letters, tile-aligned)

| Glyph | Meaning |
|---|---|
| `D` | Desk / workstation |
| `C` | Chair |
| `M` | Microwave |
| `F` | Fridge / fountain |
| `T` | Toilet |
| `S` | Sink |
| `B` | Bed / couch |
| `O` | Other / generic obstacle |

### NPCs (lowercase first letter of name)

`d` Donna, `f` Frank, `g` Greg, etc. **Collisions** (two NPCs sharing a tile, or two NPCs whose names start with the same letter on different tiles) are reported via:

- The tile glyph becomes `*` (asterisk) instead of a letter.
- The legend disambiguates with full names + coordinates + current `IntendedActionKind`.

### Hazards

| Glyph | Meaning |
|---|---|
| `~` | Water / spill |
| `*` | Stain (slip-and-fall risk) |
| `!` | Fire |
| `x` | Corpse |
| `?` | Unknown / unclassified hazard |

### Z-order rule

When a tile has multiple things on it, render order is: NPCs > hazards > furniture > floor shading > wall. (NPCs always visible; walls never overdrawn by NPCs.)

---

## Output format

```
WORLD MAP — Tick {N} | Floor {I} ({Name}) | Time {HH:MM}

{rendered grid in box-drawing characters}

LEGEND
  d — Donna (32, 18) — Eating
  f — Frank (28, 18) — Working
  g — Greg (47, 18) — Walking
  *  (one entry per disambiguated collision tile)
  M — Microwave (33, 24)
  F — Fridge (35, 24)
  ! — Fire (kitchen, 34, 25)
```

The header line is always 2 lines (title + blank). The grid is the `WorldStateDto`-derived map. The legend is optional (controlled by `AsciiMapOptions.IncludeLegend`).

---

## Public API contract

```csharp
namespace Warden.Telemetry.AsciiMap;

#if WARDEN
public static class AsciiMapProjector
{
    /// <summary>
    /// Renders the world state as a Unicode box-drawing floor plan.
    /// See WP-3.0.W spec for the glyph contract.
    /// </summary>
    public static string Render(WorldStateDto state, AsciiMapOptions options = default);
}

public readonly record struct AsciiMapOptions(
    int FloorIndex = 0,
    bool IncludeLegend = true,
    bool ShowHazards = true,
    bool ShowFurniture = true,
    bool ShowNpcs = true);
#endif
```

The method is **pure** — no I/O, no globals, deterministic given the input. Same `WorldStateDto` always renders the same string.

---

## ECSCli verb

```
ECSCli world map [--floor N] [--no-legend] [--no-hazards] [--no-furniture] [--no-npcs] [--watch]
```

`--watch` tails the live world (re-renders every N ticks; `--watch=10` for every 10 ticks). Without `--watch`, dumps a one-shot snapshot to stdout.

Verb registration mirrors the existing `ECSCli ai describe` pattern.

---

## Acceptance criteria

| # | Criterion | Type |
|---|---|---|
| AT-01 | `AsciiMapProjector.Render` exists with the public API above; compiles with `#if WARDEN` guard. | unit-test |
| AT-02 | Empty `WorldStateDto` (no rooms, no entities) → renders header + minimal `╔══╗` / `╚══╝` outline + empty legend. Snapshot test. | unit-test |
| AT-03 | Single-room world (1 room, 4 corners, 4 walls, 0 doors) → outer double-line boundary + inner single-line wall outline. Snapshot test. | unit-test |
| AT-04 | Two-room world with one door between them → single-line wall has a `·` glyph at the door tile. Snapshot test. | unit-test |
| AT-05 | Closed-door variant → `+` glyph at the door tile. Snapshot test. | unit-test |
| AT-06 | Room with `RoomCategory.Kitchen` → tiles inside the room use `▒` shading. `RoomCategory.Corridor` → `░`. `RoomCategory.Bathroom` → `▓`. Snapshot test. | unit-test |
| AT-07 | Furniture rendering: a `Microwave` entity placed on tile `(33, 24)` renders as `M` at that tile. All eight furniture glyphs (`D C M F T S B O`) covered by one snapshot test using a fixture world. | unit-test |
| AT-08 | NPC rendering: an entity with `NameComponent.Name = "Donna"` at `(32, 18)` renders as `d` at that tile. | unit-test |
| AT-09 | NPC collision: two NPCs on the same tile → `*` at that tile, both names listed in the legend with coords. | unit-test |
| AT-10 | NPC name-letter collision (Donna and Daniel on different tiles): both render as `d` on their respective tiles, both appear in the legend. (No `*` because tiles are different.) | unit-test |
| AT-11 | Hazard rendering: stain at `(22, 21)` renders as `*` at that tile (with `ShowHazards = true`); fire at `(34, 25)` as `!`; corpse at `(20, 19)` as `x`. Z-order: NPC at corpse tile shows the NPC letter, not `x` (legend lists both). | unit-test |
| AT-12 | Z-order: where NPC and furniture coincide (e.g., NPC sitting at desk), tile shows the NPC letter; legend shows both. | unit-test |
| AT-13 | `AsciiMapOptions.IncludeLegend = false` → output ends at the grid; no LEGEND section. | unit-test |
| AT-14 | `AsciiMapOptions.ShowHazards = false` → hazards are excluded from the grid AND from the legend. | unit-test |
| AT-15 | Determinism: 100 calls with the same `WorldStateDto` produce byte-identical output. | unit-test |
| AT-16 | Perf microbench: 30-NPC, 8-room, 2-corridor world renders in <50ms (mean of 100 runs) on the CI runner. | perf-test |
| AT-17 | `ECSCli world map` verb compiles, runs against a baseline world, prints to stdout, returns exit code 0. | integration-test |
| AT-18 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-19 | `dotnet test ECSSimulation.sln` — all green, no exclusions. | build + unit-test |
| AT-20 | Phase 0/1/2/3 tests stay green. No regressions in `WorldStateProjectorTests`, `WorldStateDtoTests`, etc. | regression |

---

## Followups (not in scope)

- **Multi-floor rendering** — when Phase 4.0.1 lands multi-floor, extend `Render` to optionally produce one map per floor (already keyed by `FloorIndex`).
- **ANSI colour codes** — high-stress NPCs in red, calm in cyan, etc. Terminals that don't support ANSI strip the codes naturally.
- **Diff mode** — render only the *changes* between two `WorldStateDto` snapshots. Excellent for tick-by-tick `--watch` output.
- **WP-3.0.W.1 — Haiku prompt integration.** Insert ASCII map output into Haiku validate prompts that need spatial reasoning (chore rotation, proximity, fire evacuation). Replaces the current "JSON dump and good luck" approach.
- **Snapshot test harness for emergent scenarios.** Wave 4 packets (3.2.x) can use ASCII snapshot tests as a regression net for spatial behaviour without authoring custom assertions per scenario.
- **In-Editor Unity overlay.** A Unity-side `AsciiMapDebugOverlay` MonoBehaviour that calls `AsciiMapProjector.Render` and displays the result in a screen-space text panel. Out of scope here; future Track 2 packet.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT NEEDED

This is a Track 1 (engine, headless) packet. All verification is handled by the xUnit test suite. Once `dotnet test` returns green for `Warden.Telemetry.Tests` (and any other affected test project), the packet is ready to push and PR. **No Unity Editor steps required.**

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

Target: **$0.50** (90-minute timebox). If costs approach $1.00 without the acceptance criteria being achievable, **escalate to Talon** by stopping work and committing a `WP-3.0.W-blocker.md` note to the worktree explaining what burned the budget. Do not silently exceed the envelope.

Cost-discipline rules of thumb:
- The glyph table is fixed and small. Don't over-engineer the lookup.
- Snapshot tests use canned `WorldStateDto` JSON fixtures committed to the test project; don't generate them dynamically per test.
- Resist the urge to add ANSI colour, multi-floor, or diff mode in this packet — they're explicit non-goals and have their own followup slots.

### Self-cleanup on merge

Before the PR is merged, the executing Sonnet (or whoever lands the merge) must:

1. **Check downstream dependents:**
   ```bash
   git grep -l "WP-3.0.W" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. **Expected result at merge time:** No downstream dependents (the followup `WP-3.0.W.1` may be drafted by then, but it's an extension, not a hard dependent). **Delete the spec file** as part of the merge commit:
   ```bash
   git rm docs/c2-infrastructure/work-packets/WP-3.0.W-ascii-floor-plan-projector.md
   ```
   Add `Self-cleanup: spec deleted, no dependents.` to the commit message.

3. **Do not touch** files under `_completed/` or `_completed-specs/`.

4. The git history (commit message + PR body) is the historical record. The spec file itself is ephemeral once shipped without dependents.

---

*WP-3.0.W gives Talon, Haiku, and Opus a single shared spatial artifact. After this packet ships, every triage conversation that involves "where is X" or "why did Y route there" gets a 10-line ASCII paste and the answer is obvious to all three reasoners. Cheap to author, immediate value, no risk to the visual layer.*
