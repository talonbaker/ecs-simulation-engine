# WP-4.0.L — Authoring Loop Documentation + Ledger Entries

> **Wave 4 of the Phase 4.0.x foundational polish wave — authoring loop.** Docs + ledger packet that completes wave 4. Captures the new authoring surface in the future-features ledger (FF-016, with full architectural sketch + relationship to undo/redo per Talon's 2026-05-03 framing), confirms MAC-015 / MAC-016 entries, and ships an **interim quickstart README** for level designers and mod-author scene contributions. The comprehensive end-user guide is authored separately by Opus *after* the WP-4.0.I/J/K wave passes visual verification.

> **DO NOT DISPATCH UNTIL WP-4.0.I, J, AND K ARE MERGED** — docs reference the actual shape of the shipped code; drifting earlier produces stale guides.

**Tier:** Sonnet (small)
**Depends on:** WP-4.0.I, WP-4.0.J, WP-4.0.K (all merged + visual-verified).
**Parallel-safe with:** Anything not touching the same docs files.
**Timebox:** 75 minutes
**Budget:** $0.30
**Feel-verified-by-playtest:** NO (docs packet)

---

## Goal

The authoring loop (I + J + K) ships the *capability*. This packet ships the *legibility*:

1. **FF-016 — In-game scene authoring loop**: a permanent ledger entry capturing what was built, the architectural sketch, the relationship to undo/redo, and the explicit "shipped: WP-4.0.I/J/K" lineage. This is the historical record per `docs/future-features.md` maintenance discipline.
2. **MAC-015 + MAC-016 ledger confirmation**: ensure both entries are present in `MOD-API-CANDIDATES.md` with the post-implementation stability bumps and source-packet citations correct.
3. **PHASE-4-KICKOFF-BRIEF update**: add a "Wave 4 — Authoring Loop (Phase 4.0.I/J/K/L)" subsection to the existing Phase 4.0.x narrative so future-Opus reading the brief understands what landed.
4. **Interim level-designer quickstart README**: a short file at `docs/c2-content/world-definitions/README.md` covering the absolute minimum: (a) what a world-definition file is, (b) how to start author mode, (c) how to save, (d) how to reload, (e) where to put your file, (f) where to ask questions / submit a community scene. ~600-900 words. Functional, not exhaustive.

The comprehensive end-user guide (covering full author workflow, all tools, edge cases, troubleshooting, modder API quickstart with code samples) is **explicitly out of scope for this Sonnet packet**. That guide is authored by Opus directly, post-wave, when the tools have been used in anger and the rough edges are visible. This split exists because: (a) Sonnet docs from spec are accurate but lack lived-in clarity; (b) Opus authoring after first-use captures the friction points worth documenting; (c) the comprehensive guide is a load-bearing artifact for community contributions and deserves Opus-tier attention.

After this packet:
- `docs/future-features.md` has FF-016 entry with `Shipped: WP-4.0.I/J/K — 2026-05-NN` annotation.
- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` has MAC-015 + MAC-016 entries verified consistent (J + I added them; L audits).
- `docs/c2-infrastructure/PHASE-4-KICKOFF-BRIEF.md` has the Wave 4 subsection added under Phase 4.0.x.
- `docs/c2-content/world-definitions/README.md` exists with interim quickstart.
- A short note in `docs/c2-content/world-definitions/README.md` references "the comprehensive guide is forthcoming — see `docs/AUTHORING-GUIDE.md` (authored post-wave by Opus)" so the placeholder is clear.

---

## Reference files

- `docs/future-features.md` — read in full. FF-016 follows the established entry shape; FF-001 through FF-015 are the format reference.
- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` — read in full. Audit MAC-015 (added by J) + MAC-016 (added by I) are present with correct cross-references.
- `docs/c2-infrastructure/PHASE-4-KICKOFF-BRIEF.md` — read the existing Phase 4.0.x subsection. New Wave 4 narrative slots in after the existing Wave 1/2/3 paragraphs.
- `docs/c2-infrastructure/work-packets/WP-4.0.I-world-definition-writer.md` (this wave) — what shipped on the writer side.
- `docs/c2-infrastructure/work-packets/WP-4.0.J-author-mode-extended-palette.md` (this wave) — what shipped on the author-mode UI side.
- `docs/c2-infrastructure/work-packets/WP-4.0.K-npc-authoring.md` (this wave) — what shipped on the NPC-authoring side.
- `docs/c2-content/world-definitions/playtest-office.json` and `office-starter.json` — the canonical example scenes the README references.
- `docs/c2-content/aesthetic-bible.md`, `cast-bible.md`, `world-bible.md`, `ux-ui-bible.md` — referenced by the README's "context" sidebar so designers know where the content authority lives.

---

## Non-goals

- Do **not** author the comprehensive end-user guide (`docs/AUTHORING-GUIDE.md`). Opus authors that post-wave.
- Do **not** revise existing FF-NNN entries. FF-016 is additive; existing entries are immutable per future-features.md maintenance discipline.
- Do **not** revise existing MAC entries beyond confirmation audit. If MAC-015 / MAC-016 are missing or inconsistent (because J or I shipped with errors), file a `WP-4.0.L-blocker.md` rather than silently fixing — the wave-4 packets must own their own ledger entries.
- Do **not** add API-reference docs for `IWorldMutationApi` extensions. The interface XML doc comments are the source of truth; if they are insufficient, that is a J/K bug to fix, not an L doc-write.
- Do **not** add screenshots. The README is text-only for v0.1; screenshots come with the comprehensive guide post-wave.
- Do **not** modify any code. Pure docs packet.
- Do **not** create a separate "modder API" doc. Modder use cases are folded into the README's last section; full Mod API docs live in the future Mod API sub-phase per SRD §8.8.
- Do **not** translate / localize the README. English-only for v0.1.

---

## Design notes

### FF-016 entry shape (paste into `docs/future-features.md`)

Append to the appropriate section (likely a new `## Authoring tools` section near the bottom):

```markdown
## Authoring tools

### FF-016: In-game scene authoring loop

- **What:** A live in-game level-design surface — author rooms, lights, windows, props, and NPCs without recompiling, save the result as a `world-definition.json` file, hot-reload to iterate. Built on the existing `WorldDefinitionLoader` + `IWorldMutationApi` substrate; round-trip via the new `WorldDefinitionWriter`. WARDEN-only (dev / mod-author surface; not exposed to retail players).
- **Architectural sketch:**
  - `WorldDefinitionWriter` (WP-4.0.I) serializes the live ECS world to the existing JSON format consumed by `WorldDefinitionLoader`. Schema bumped 0.1.0 → 0.2.0 (additive: `placedProps` block).
  - Author mode (WP-4.0.J) extends `BuildPaletteCatalog` with three new categories — Room (rectangle drag), LightSource (point-place + tuner), LightAperture (point-place + tuner). Toggled via `Ctrl+Shift+A` (WARDEN). Lifts gameplay-time gating in `PlacementValidator`. Save/Load/Reload toolbar invokes the WP-4.0.I dev-console handlers.
  - NPC authoring (WP-4.0.K) adds an `NpcArchetype` palette category that auto-discovers archetypes from `TuningCatalog`. Spawned NPCs receive default tuning, default drives, auto-named from cast pool; archetype-correct behavior emerges from existing engine systems.
- **Relationship to undo/redo:** The undo stack from WP-4.0.G (build mode v2) is the only undo surface. Each new mutation in I / J / K ships an `IUndoableMutation` adapter so author-mode actions sit in the same stack as gameplay-time prop placements. Single Ctrl+Z affordance across all of build mode and author mode.
- **Pilot approach:** Three packets shipped sequentially in wave 4 (I → J → K) so each layer's seam is real before the next builds on it. WP-4.0.L (this entry's source packet) ships interim README + ledger; comprehensive end-user guide authored by Opus post-wave from lived-in friction.
- **Gate:** None — shipped wave 4 of Phase 4.0.x.
- **Home wave:** Phase 4.0.x wave 4 (this wave).
- **Source:** Talon's 2026-05-03 framing ("I don't have a good way to get items into the world without a predefined scene… I need controls and means of level design and architecture"). Aligns with SRD §8.8 (incremental Mod API surfacing) — author mode is the canonical first-class modder surface.
- **Shipped:** WP-4.0.I (writer) — 2026-05-NN; WP-4.0.J (author mode + extended palette) — 2026-05-NN; WP-4.0.K (NPC authoring) — 2026-05-NN.
```

(Sonnet substitutes actual ship dates from git log when authoring.)

### MAC-015 + MAC-016 audit

Confirm both entries present in `docs/c2-infrastructure/MOD-API-CANDIDATES.md` with:
- Correct **What / Where / Why a candidate / Stability / Source packet** sections per the ledger format.
- Cross-references between MAC-015 (extended palette) and MAC-007 (`IWorldMutationApi`) — the palette tools all flow through the mutation contract.
- Cross-references between MAC-016 (world-definition file format) and MAC-001 (per-archetype tuning) — `npcSlots#archetypeHint` keys into the archetype catalog.
- MAC-001 stability bump from *stabilizing* → *stable* (per WP-4.0.K's recommendation; MAC-001 now has 8+ consumers across engine systems + author palette).

If any inconsistency: file blocker, do not silently fix.

### PHASE-4-KICKOFF-BRIEF Wave 4 subsection

Insert under the existing Phase 4.0.x narrative, after the bullet list ending with WP-4.0.H. Example structure:

```markdown
**Wave 4 (Phase 4.0.I/J/K/L) — Authoring Loop (added 2026-05-03):**

The 4.0.A–H wave shipped a legible single-floor 5-NPC scene. But Talon's 2026-05-03 review surfaced a foundational gap: there's no way to *create* a scene without recompiling. Hand-editing `world-definition.json` and restarting is the current workflow; that's the in-engine equivalent of the recompile friction the wave-1 restructure was designed to eliminate. Wave 4 closes the loop:

- **WP-4.0.I — World definition writer.** Engine. Writes the current world to a `world-definition.json` matching the existing loader format. Schema bump 0.1.0 → 0.2.0 (additive `placedProps`). Dev-console commands: `save-world`, `reload-world`, `list-worlds`.
- **WP-4.0.J — Author mode + extended palette.** Unity. WARDEN-only "author mode" toggled via Ctrl+Shift+A. Extends BuildPaletteCatalog with Room (rectangle-drag) / LightSource / LightAperture categories. Save/Load/Reload toolbar wired to I's writer.
- **WP-4.0.K — NPC authoring.** Unity. NpcArchetype palette category auto-discovered from TuningCatalog. Spawn-an-archetype tool with auto-naming. Round-trips through I's npcSlots serialization.
- **WP-4.0.L — Authoring docs + ledger.** Docs. FF-016 ledger entry, MAC-015 / MAC-016 audit, interim README, Wave 4 narrative in this brief. Comprehensive end-user guide is authored by Opus post-wave (not Sonnet).

Wave 4 is the authoring substrate; subsequent waves can build content (more starter scenes, scene packs) without spec-and-dispatch friction.
```

### Interim README content sketch

`docs/c2-content/world-definitions/README.md` (~600-900 words):

```markdown
# World Definitions

This directory holds **scene files** for the ECS Simulation Engine. A scene is a JSON document describing rooms, lights, windows, props, and NPC spawn points. The simulation boots from one of these files; you can also save and reload them at runtime.

## Quickstart for level designers

1. **Boot the game** in WARDEN mode. (Use the dev launcher; standard player builds don't expose author mode.)
2. **Press `Ctrl+Shift+A`** to enter author mode. A banner appears at the top of the screen.
3. **Open the build palette** (existing build-mode hotkey) — you'll see new tabs: Room, LightSource, LightAperture, NpcArchetype.
4. **Draw a room**: pick the Room tab, choose a kind (e.g. Cubicle Area), click-drag a rectangle on the floor.
5. **Place lights**: pick LightSource, choose a kind (Overhead Fluorescent), click a tile inside the room. Tune state / intensity / temperature in the inline panel.
6. **Place a window**: pick LightAperture, click a wall tile on the room boundary.
7. **Drop NPCs**: pick NpcArchetype, choose an archetype (the-vent / the-newbie / etc.), click tiles inside rooms.
8. **Save**: Save toolbar button → name the scene → it lands at `docs/c2-content/world-definitions/<name>.json`.
9. **Reload**: Reload button or `> reload-world <name>` in the dev console.

## What's saved, what's not

A scene captures **structure** — rooms, lights, windows, props, NPC spawn slots (room + tile + archetype + name). It does **not** capture in-flight simulation state — drives, memories, in-progress actions, schedule cursors. Reloaded NPCs spawn at authored positions with default drives and zero memory. (That's a future feature; see FF-016 in `docs/future-features.md`.)

## Existing scenes

- `playtest-office.json` — the canonical 5-NPC playtest office.
- `office-starter.json` — minimal starter scene; useful as a template.

## File format

The schema is documented inline via XML doc comments on `APIFramework/Bootstrap/WorldDefinitionDto.cs`. Schema version 0.2.0 as of 2026-05-NN. Backwards-compatible (older 0.1.0 files still load).

The format follows the project's data-driven extension pattern — see `docs/c2-infrastructure/MOD-API-CANDIDATES.md#MAC-016`.

## Contributing a community scene

The "community scene" workflow is forthcoming and will be documented in the comprehensive AUTHORING-GUIDE.md (authored by Opus post-wave). For now: open a PR with your `<name>.json` file in this directory; reviewers will run it locally to validate.

## Where to look for context

- **Cast** (who lives in the office): `docs/c2-content/cast-bible.md`
- **World** (what offices look like in this world): `docs/c2-content/world-bible.md`
- **Aesthetic** (visual style, era, materials): `docs/c2-content/aesthetic-bible.md`
- **UX/UI** (player experience): `docs/c2-content/ux-ui-bible.md`

## Comprehensive guide

The full author / mod-author guide is at `docs/AUTHORING-GUIDE.md` (authored post-wave). This README is the v0.1 quickstart while that guide is in flight.
```

Sonnet adapts wording where the actual implementation differs from the sketch (e.g., if a hotkey was changed during implementation, README reflects what shipped). Cross-reference against the J/K sandbox recipes (`Assets/_Sandbox/author-mode.md` and `Assets/_Sandbox/npc-authoring.md`) for the source-of-truth on tool UX.

### Stability of MAC bumps

Wave 4 is a strong moment to re-evaluate the MOD-API-CANDIDATES ledger:

- **MAC-001** (per-archetype tuning JSONs) — recommend *stabilizing* → *stable*. 8+ consumers; data-driven authoring exposed it as a Mod API surface in user-visible form.
- **MAC-005** (SimConfig section pattern) — already *stable*. No change.
- **MAC-006** (WorldStateDto schema) — already *stable*. No change.
- **MAC-007** (`IWorldMutationApi`) — *stabilizing*. Multiple new consumers in this wave; potential bump to *stable* in a future wave once the new operations (CreateRoom, CreateLightSource, CreateNpc, etc.) prove out.
- **MAC-013** (NPC visual state catalog) — *stabilizing* per WP-4.0.E ship. No change.
- **MAC-014** (room visual identity catalog) — *fresh* per WP-4.0.D ship. No change.
- **MAC-015** (extended build palette) — newly added by J, *fresh* → *stabilizing* by K's second consumer.
- **MAC-016** (world-definition file format) — newly added by I, *stabilizing* immediately (loader was always there; writer + round-trip discipline lands in this wave).

Encode these in the ledger update.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| doc | `docs/future-features.md` (modification) | Add FF-016 entry per design notes; new `## Authoring tools` section if not present. |
| doc | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` (modification) | Audit MAC-015 + MAC-016 entries for correctness; bump MAC-001 to *stable* with rationale; minor stability re-evaluation per design notes. |
| doc | `docs/c2-infrastructure/PHASE-4-KICKOFF-BRIEF.md` (modification) | Insert Wave 4 subsection under Phase 4.0.x narrative. |
| doc | `docs/c2-content/world-definitions/README.md` (new) | Interim quickstart per design notes. |
| test | `APIFramework.Tests/Documentation/FutureFeaturesContainsFF016Tests.cs` (new) | Trivial doc-presence test: `docs/future-features.md` contains "FF-016" header and "Shipped: WP-4.0.I" line. Catches regressions in maintenance discipline. |
| test | `APIFramework.Tests/Documentation/ModApiCandidatesContainsMac015And016Tests.cs` (new) | Same pattern: ledger contains MAC-015 + MAC-016 with `Source packet:` lines pointing at WP-4.0.J + WP-4.0.I respectively. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `docs/future-features.md` contains FF-016 entry with all sections (What / Architectural sketch / Relationship to undo/redo / Pilot approach / Gate / Home wave / Source / Shipped). | unit + manual |
| AT-02 | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` MAC-015 entry present, references WP-4.0.J as source, lists at least one consumer (room tool) plus a second consumer note (NPC tool from K). | unit + manual |
| AT-03 | MAC-016 entry present, references WP-4.0.I, notes schema bump 0.1.0 → 0.2.0. | unit + manual |
| AT-04 | MAC-001 stability marker now reads *stable* with rationale citing 8+ consumers. | manual |
| AT-05 | `docs/c2-infrastructure/PHASE-4-KICKOFF-BRIEF.md` contains Wave 4 subsection under Phase 4.0.x. | manual |
| AT-06 | `docs/c2-content/world-definitions/README.md` exists; covers all 9 quickstart steps; references the forthcoming `docs/AUTHORING-GUIDE.md`. | manual |
| AT-07 | README's tool descriptions match what actually shipped in J + K (verify against `Assets/_Sandbox/author-mode.md` + `npc-authoring.md` recipes). | manual |
| AT-08 | Doc-presence tests pass. | unit |
| AT-09 | All Phase 0–3 + Phase 4.0.A–K tests stay green. | regression |
| AT-10 | `dotnet build` warning count = 0; all tests green. | build + test |

---

## Mod API surface

This packet introduces no new MAC entries. It audits + ships the ledger work for MAC-015 + MAC-016 introduced by J + I, and bumps MAC-001 stability based on the new consumer added in K.

---

## Followups (not in scope)

- **`docs/AUTHORING-GUIDE.md`** — the comprehensive end-user / mod-author guide. **Authored by Opus directly post-wave**, not dispatched as a Sonnet packet. Opus opens the tools, uses them in anger, and writes the guide that captures the friction points and idioms only visible from real use. Target length 3000-5000 words; covers all tools, edge cases, troubleshooting, modder API quickstart with code samples, conventions for community scene contributions.
- **Screenshots / video for the comprehensive guide.** Talon hands; out of Sonnet scope.
- **Localization / translation.** Far future.
- **In-app help layer** (tooltip "?", inline help panel) referencing the comprehensive guide. Polish, post-comprehensive-guide.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT required

Docs packet. `dotnet test` green + Talon's manual review of the README + ledger entries is the gate.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm worktree at `.claude/worktrees/sonnet-wp-4.0.l/` on branch `sonnet-wp-4.0.l` based on recent `origin/staging` (which now includes WP-4.0.I/J/K all merged).
1. Implement the spec.
2. Run `dotnet test`. All must stay green.
3. Stage all changes including self-cleanup.
4. Commit on the worktree's feature branch.
5. Push the branch.
6. Stop. Notify Talon: `READY FOR REVIEW — docs packet, no visual verification needed; please skim the README + FF-016 entry before merge.`.

### Feel-verified-by-playtest acceptance flag

**Feel-verified-by-playtest:** NO

Docs packet.

### Cost envelope

Target: **$0.30**. README + ledger + brief update + 2 trivial doc-presence tests. If cost approaches $0.50, escalate via `WP-4.0.L-blocker.md`.

Cost-discipline:
- Don't write the comprehensive guide. That's Opus's post-wave deliverable.
- Don't add screenshots. Text-only README v0.1.
- Don't restructure existing future-features.md / MOD-API-CANDIDATES.md / PHASE-4-KICKOFF-BRIEF.md beyond the additions specified — those files have established maintenance discipline.

### Self-cleanup on merge

Standard. No expected dependents (this packet closes wave 4).
