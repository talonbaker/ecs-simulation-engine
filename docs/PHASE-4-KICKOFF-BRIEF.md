# Phase 4 Kickoff Brief — for a Fresh Opus Chat

You are picking up the C2 (Command & Control) infrastructure project for the ECS Simulation Engine at the start of Phase 4. Phases 0 (orchestration), 1 (alive-feeling foundation), 2 (gameplay integration), and 3 (visualization — death substrate, live mutation, perf refactor, Unity scaffold, silhouettes, lighting, build mode, player UI, JSONL stream, event log, dev console, save/load hardening, sound bus, physics, chores, rescue, per-archetype tuning, animation states) are all complete. **Phase 4 is the gameplay-deepening + content-polish phase** — taking the now-running simulation and giving it the depth the bibles promised: multi-floor, the pixel-art-from-3D rendering pipeline, per-archetype voice profiles, emergent narrative scenarios (plague week, PIP arc, fire), player verb expansion (resolution of UX/UI bible Q5/Q6), hardcore mode, and ship-prep balance passes. This document is your bootstrap context.

---

## What this project is

A 2.5D top-down management sim of an early-2000s office, built on a headless C# / .NET 8 entity-component-system simulation engine, hosted by Unity for the player and by `ECSCli` + headless test harnesses for development. The lineage is The Sims, Rimworld, and Prison Architect; the tone is *Office Space*, *Severance*'s office floor, and the lived-in mess of *The Office*. Mature themes — sex, drugs, depression, infidelity, shame, **death and rescue from death** — depicted honestly, not sensationally. The player is a ghost-camera who watches and influences fifteen-ish workers across an early-2000s office. The engine and runtime are 100% offline at ship time. The Anthropic API is a **design-time** tool: Sonnets generate code and content, Haikus validate balance. Per SRD §8.7: **the engine is host-agnostic; telemetry is build-conditional.** The dev-time observability (JSONL streams, AI agent connection, debug consoles) lives in `Warden.*` projects gated behind `#if WARDEN`. Shipped builds strip every `Warden.*` reference.

You are Opus. In the 1-5-25 Claude Army topology, you are the General. You author specs (Work Packets) that Sonnets execute. You don't write code yourself; you author the briefs and the missions that orchestrate them. Your output is structured Markdown work packets and OpusSpecPacket JSON.

---

## What Phase 0, Phase 1, Phase 2, and Phase 3 built

**Phase 0** — 13-project .NET 8 solution. Engine projects (`APIFramework`, `APIFramework.Tests`, `ECSCli`, `ECSVisualizer`) and `Warden.*` projects implementing strict JSON schemas, an Anthropic Messages + Batches HTTP client, and a full orchestrator binary (prompt cache manager, batch scheduler, cost ledger, fail-closed escalator, report aggregator).

**Phase 1** — alive-feeling foundation. Schemas v0.1 → v0.4 (social drives + willpower + inhibitions, spatial layer, persistent chronicle). Sixteen engine systems covering rooms, lighting, movement quality, social drives, willpower, inhibitions, relationships as entities, narrative event detection, persistent chronicle, the silent-protagonist dialog mechanism. Five content bibles authored.

**Phase 2** — gameplay integration. Action selection seam, schedule layer, memory recording, stress, social mask cracking, workload + tasks, physiology vetoes, per-NPC names, dialog per-listener tracking, fact-sheet staleness CI, plus orchestrator hardening. Closed the willpower-stress-mask loop; populated all three memory tiers (per-pair, personal, chronicle). Added SRD §8.7 (engine host-agnostic axiom).

**Phase 3** — visualization, death, and gameplay deepening:

- **3.0.x** — engine substrate. `LifeStateComponent` (Alive | Incapacitated | Deceased), `CauseOfDeathComponent`, four narrative kinds (Choked, SlippedAndFell, StarvedAlone, Died), `LifeStateGuard.IsAlive` adopted by ~40 systems. Choking-on-food scenario. Corpse handling + bereavement cascade. Slip-and-fall + locked-in-and-starved scenarios. Live-mutation hardening (`StructuralChangeBus`, `IWorldMutationApi`, `PathfindingCache`). `ComponentStore<T>` typed-array refactor (eliminated `Dictionary<Type, object>` boxing; the perf substrate Unity needs). Plus the fainting scenario (WP-3.0.6) added during integration — validates the Incapacitated → Alive recovery path.
- **3.1.x** — Unity scaffolding. `ECSUnity/` project loads `APIFramework.dll`; `EngineHost` MonoBehaviour ticks the engine in-process; `WorldStateProjectorAdapter` produces `WorldStateDto` per frame; rooms render as flat colored rectangles upgraded to silhouettes per cast bible (3.1.B); lighting visualization per `RoomIllumination` (3.1.C); build mode toggle with ghost preview + `IWorldMutationApi` integration (3.1.D); player UI with three-tier inspector / time HUD / selection / soften toggle (3.1.E); WARDEN-only JSONL stream emission (3.1.F); CDDA-style event log (3.1.G); WARDEN-only dev console (3.1.H). **The 30-NPCs-at-60-FPS performance gate is live and passing.**
- **3.2.x** — gameplay deepening. Save/load round-trip hardening (3.2.0) — every component family round-trips through `WorldStateDto` JSON; mid-choke / mid-faint / mid-build saves load correctly. Sound trigger bus (3.2.1) — engine emits `Cough`, `ChairSqueak`, `BulbBuzz`, `Footstep`, `SpeechFragment`, etc.; Unity host synthesises. Rudimentary physics (3.2.2) — `MassComponent`, `BreakableComponent`, `ThrownVelocityComponent`, deterministic decay, no continuous solver. Chore rotation (3.2.3) — trash duty, fridge cleaning, microwave cleaning; per-archetype acceptance bias; over-rotation stress; the constant-Donna-cleans-the-microwave dynamic. Rescue mechanic (3.2.4) — Heimlich, CPR, door-unlock; the world's compassion surface. Per-archetype tuning JSONs (3.2.5) — choke / slip / bereavement / chore / rescue / mass / memory-persistence biasing. Silhouette animation state expansion (3.2.6) — Eating, Drinking, Working, Crying, CoughingFit, Heimlich states.

**UX/UI bible v0.1** authored at the Phase 3 boundary. Six axioms (ghost-camera, diegesis, mom-can-play, layered disclosure, simulation-rhythm-stress, iconography). Three open questions deliberately deferred to **v0.2** (resolved by Phase 4):
- **Q4** — notification carrier model + the bigger gameplay-loop / challenge-surface decision (Talon's framing: NPC-driven challenge over task overload).
- **Q5** — player embodiment (pure ghost vs manager's-office overlay vs manager NPC).
- **Q6** — direct NPC intervention scope (none / soft suggestions / direct commands).

---

## Read these documents before your first reply

In order:

1. `docs/c2-infrastructure/00-SRD.md` — master SRD. Especially **§8 (architectural axioms — including 8.7)** and §4.1 (fail-closed policy).
2. `docs/c2-infrastructure/PHASE-3-HANDOFF.md` — comprehensive Phase 3 closure summary. **The most important document for you right now.** Read end to end. (If it doesn't exist yet — Talon may not have authored it; the next packet you draft might be authoring it from `docs/c2-infrastructure/PHASE-3.0-COMPLETION-SUMMARY.md` and the `_completed-specs/` audit trail.)
3. `docs/c2-infrastructure/PHASE-3-TEST-GUIDE.md` — operational verification (if exists).
4. `docs/c2-infrastructure/PHASE-2-HANDOFF.md` — for context on Phase 2 systems Phase 3 built on.
5. `docs/c2-infrastructure/SCHEMA-ROADMAP.md` — versioned schema plan; current at v0.5 (life-state surface added during Phase 3).
6. `docs/c2-infrastructure/RUNBOOK.md` — operational guide.
7. `docs/c2-content/world-bible.md`, `cast-bible.md`, `aesthetic-bible.md`, `action-gating.md`, `dialog-bible.md`, `ux-ui-bible.md`, `new-systems-ideas.md`. **For Phase 4, the UX/UI bible (currently v0.1) is load-bearing** — packet 4.0.x lands its v0.2 revision.
8. `docs/c2-infrastructure/work-packets/_completed-specs/` and `_completed/` — every Phase 0–3 packet's spec + completion note. Skim WP-3.0.0, WP-3.1.A, WP-3.2.0 (the Phase 3 substrate packets) and WP-3.2.4 (rescue, the most-recently-shipped emergent-gameplay surface) to learn the packet format and Phase 3's design language.
9. The cleanup branch (`ecs-cleanup-and-phase4-brief`) reorganized completed specs into `_completed-specs/`. Active `work-packets/` directory contains only **pending** packets.

A v0.2 UX/UI bible draft will land early in Phase 4 (your first or second packet). Read it before drafting any Phase 4.3.x packet (player verb expansion / Q5 / Q6).

---

## Architectural axioms (these don't change)

From SRD §8 (ten axioms — §8.8 added 2026-05-02 codifying the Mod API pacing commitment; §8.7 codified during Phase 4 from Phase 3's de-facto adoption):

1. **Anthropic API is design-time only** (8.1). Runtime engine has zero LLM dependency.
2. **Save/load reuses WorldStateDto** (8.2). Hardened to v0.5 schema during Phase 3.2.0.
3. **Memory model: per-pair primary, global thin** (8.3). All three tiers populated.
4. **Charm is curated, not simulated** (8.4).
5. **Social state is a first-class component family** (8.5). Largest live family.
6. **Visual target is 2.5D top-down management sim** (8.6). Realised in Phase 3.1.
7. **Emotion is content; spoken language is decorative** (Phase 1 derived).
8. **Drives are necessary but not sufficient for action** (Phase 1 derived).
9. **The engine is host-agnostic; telemetry is build-conditional** (8.7). WARDEN/RETAIL split enforced via Unity scripting defines + `#if WARDEN` everywhere.
10. **API surfaces evolve incrementally; modder-extension points are recognized over time, not designed up front** (8.8). The engine ↔ content boundary is real but matured slowly; no premature repo split; no premature full Mod API; every cross-boundary packet leaves the boundary slightly more legible than it found it. A `MOD-API-CANDIDATES.md` ledger (to be authored when first surfaces stabilize) tracks candidate extension points organically. A formal Mod API sub-phase lands in Phase 5 or 6 territory after the gameplay loop matures.

**Phase 4 carries-forward design memories** that are project-shaping. Read them as load-bearing:

- **Stress and lull come from the simulation's rhythm, not from time-skips.** The fire arrives because the player wasn't watching. Default time controls = pause / ×1 / ×4 / ×16. Skip-to-morning, time-zoom, free-camera are creative-mode-only.
- **"Mom can play" is a primary design driver.** Single-stick-equivalent, discrete actions over continuous holds, defaults produce good play with no input.
- **Iconography over text.** Chibi-anime emotion cues, CRT-era UI icons, environmental signals (stink lines, green fog). No floating-emoji-popup-modern-game style.
- **Off-hours = player's build / restructure window.** Worker hours = morning to dusk. Night belongs to the player.
- **Single-floor v0.1.** Multi-floor deferred until floor 1 functional. Phase 4.0.x is when multi-floor lands.
- **Rudimentary physics, not a simulator.** Phase 3.2.2 shipped this. Future scope additions stay disciplined.
- **Absurdity is diegetic.** Donna-rips-ass, deaths, mask-slips: observed onscreen, not popped up. World commentates via consequence.
- **Sound is welcome on a trigger model.** Engine emits triggers (Phase 3.2.1); Unity host synthesises.
- **Challenge surface is NPC-driven, not task overload.** Constant-choker, food-thief, chore rotation. Notifications stay sparse and diegetic.

---

## Critical: things Phase 3 settled (don't re-litigate)

- **The Unity host is real and stable.** Loading `APIFramework.dll` works; engine ticks in-process; `WorldStateDto` projection is consumed per frame; the 30-NPCs-at-60-FPS gate holds. Don't propose alternative hosts. Don't propose abandoning Unity.
- **`ComponentStore<T>` is the storage substrate.** `Dictionary<Type, object>` boxing is gone. The API surface (`Get<T>`, `Has<T>`, `Set<T>`) is unchanged at call sites. Don't re-litigate the storage layer.
- **`IWorldMutationApi` is the structural-mutation contract.** Build mode, dev console, future packets that mutate topology — all flow through it. The `StructuralChangeBus` invalidates the path cache on emit; consumers re-path naturally on next tick.
- **`LifeStateComponent` three-state machine.** Alive / Incapacitated / Deceased. Recovery path Alive ← Incapacitated is real and exercised by fainting (3.0.6) and rescue (3.2.4). Future Incapacitated states (concussion, seizure, hypoglycemia) extend without revisiting the contract.
- **Sound triggers, not synthesis, in the engine.** Engine emits `SoundTriggerBus` events; host synthesises. Don't propose engine-side audio.
- **The chore rotation system is the canonical NPC-driven-challenge example.** Future emergent gameplay (plague week, PIP arc, fire) follows its pattern: per-archetype acceptance/competence biasing, persistent memory of refusals, cascade through stress / mood / relationship.
- **The 30-NPCs-at-60-FPS performance gate.** Every Phase 3.1.x packet preserved it. Phase 4 packets must continue to honor it. If a Phase 4 visualization upgrade breaks the gate, the upgrade is wrong, not the gate.

---

## Phase 4 expectations and starter priorities

> **2026-05-02 restructure.** The original 4.0.x scope (UX bible v0.2 + multi-floor + shader pipeline) was reordered after Talon's playtest-driven realization: the single-floor scene isn't legible yet, NPCs bunch and clip, floors don't read as floors. Adding multi-floor on top would compound the unreadability. Lesson is the same as the 3.1.x bundle incident, generalized: **don't build on top of a foundation that isn't working**. Multi-floor is postponed to 4.2.x where emergent scenarios actually need it. The new 4.0.x is the *foundational polish* wave that makes a 5-NPC single-floor scene legible, responsive, and visually right. The shader pipeline is promoted to lead because the look-and-feel must land before subsequent polish work authors against silhouettes-on-flat-rectangles. SRD §8.8 (added 2026-05-02) commits the project to incremental Mod API surfacing during this wave: every foundational-polish packet carries a "Mod API surface" section; candidate extension points accumulate in `docs/c2-infrastructure/MOD-API-CANDIDATES.md`.

Phase 4 is **gameplay deepening + content polish**, dispatched in this order:

### Phase 4.0.x — Foundational polish (NEW SCOPE)

The wave that makes the single-floor 5-NPC scene functional, legible, and visually settled. Multi-floor is intentionally OUT of this wave; emergent gameplay (plague, fire, PIP, affairs) is intentionally OUT of this wave.

- **WP-4.0.A — URP migration foundation** (Track 2 pipeline-migration). Adopt URP from the start per Talon's 2026-05-02 architectural call (zero-cost-now vs compounding-cost-later; CPU efficiency for the heavy ECS backend; multi-platform readiness; access to Shader Graph + VFX Graph). Migrates the four existing custom shaders; visual parity required. Foundation for all subsequent visual packets.
- **WP-4.0.A1 — Pixel-art Renderer Feature sandbox** (Track 2 sandbox + companion -INT). The aesthetic bible's render commitment as a URP `ScriptableRendererFeature`. Depends on WP-4.0.A merging. First consumer of MAC-009 (URP ScriptableRendererFeature pattern, standardized).
- **WP-4.0.B — NPC spatial behavior / anti-overlap** (Track 1 engine). Soft repulsion / personal-space component. NPCs may have nothing meaningful to do yet, but they shouldn't clip into each other while doing it. PersonalSpaceComponent becomes a Mod API extension surface.
- **WP-4.0.C — Build prop footprint foundation** (Track 1.5). The substrate BUG-001's eventual fix needs: per-prop occupancy footprint as a first-class engine concept. Doesn't fix BUG-001 yet; lays the foundation.
- **WP-4.0.D — Floor & room visual identity** (Track 2 sandbox + -INT). Floors that read as floors (carpet, linoleum, concrete); walls that read as walls; doors that read as doors. Couples to 4.0.A's pipeline.
- **WP-4.0.E — NPC visual state communication** (Track 2 sandbox + -INT). Animation states from 3.2.6 are defined but not reading. Polish pass: emotion cues, activity legibility, two-second-glance answer to "what is this person doing right now."
- **WP-4.0.F — NPC dev-mode introspection overlay** (Track 2 sandbox + -INT). WARDEN-only. What each NPC is doing, intends to do, drive vector, why-am-I-here. Talon's "refine in dev mode" call mechanized. Strips at ship.
- **WP-4.0.G — Build mode v2 / BUG-001 fix** (Track 1.5). Prop-on-prop displacement resolved using 4.0.C's footprint substrate; multi-prop stacking; undo/redo. Lands after 4.0.C.
- **WP-4.0.H — Particle vocabulary (steam / smoke / fire)** (Track 2 sandbox + -INT). Trailing steam from coffee; fire/smoke for the plague-week and fire scenarios that come later; modder-extensible particle registration. Lands after 4.0.A.

**Wave 4 — Authoring Loop (Phase 4.0.M / I / J / K / L; added 2026-05-03):**

The 4.0.A–H wave shipped a legible single-floor 5-NPC scene. But Talon's 2026-05-03 review surfaced a foundational gap: there's no way to *create* a scene without recompiling. Hand-editing `world-definition.json` and restarting is the current workflow; that's the in-engine equivalent of the recompile friction the wave-1 restructure was designed to eliminate. Wave 4 closes the loop:

- **WP-4.0.M — Cast name generator library.** Engine. Port of Talon's HTML/JS roster generator (`~/talonbaker.github.io/name-face-gen/data.js`) to a deterministic seedable C# library. Six-tier rarity model (Common / Uncommon / Rare / Epic / Legendary / Mythic) with per-tier name + title structure; modular title builder; fusion grammar with consonant-collapse cleanup. The deterministic `Generate(Random)` overload is the seam for the future "reroll for a better hire" loot-box mechanic. Catalog at `docs/c2-content/cast/name-data.json`. **Shipped 2026-05-03.**
- **WP-4.0.I — World definition writer.** Engine. Inverse of `WorldDefinitionLoader`: serializes the live ECS world to a `world-definition.json` matching the existing format. Round-trip-validated against `office-starter.json`. Substrate for the author-mode Save toolbar. **Shipped 2026-05-03.**
- **WP-4.0.J — Author mode + extended palette.** Engine + Unity. `IWorldMutationApi` extended with 6 author-mode operations (CreateRoom / DespawnRoom with `RoomDespawnPolicy` / CreateLightSource / TuneLightSource / CreateLightAperture / DespawnLight). Author-mode palette JSON at `docs/c2-content/build/author-mode-palette.json`. Unity-side `AuthorModeController` (WARDEN-only, Ctrl+Shift+A) wraps the engine API for Editor-wired UI. Full palette UI + tools + sandbox scene deferred to Editor follow-up. **Shipped 2026-05-03.**
- **WP-4.0.K — NPC authoring.** Engine + Unity. `IWorldMutationApi` extended with CreateNpc / DespawnNpc / RenameNpc; `WorldMutationApi` 5-arg constructor accepts the cast deps. `CastNamePool` wraps WP-4.0.M's generator with collision retry. Cross-cutting fix: `NpcSlotComponent.NameHint` plumbing (the JSON had `nameHint` but the loader was silently dropping it; now round-trips through save/reload). **Shipped 2026-05-03.**
- **WP-4.0.L — Authoring docs + ledger.** Docs. FF-016 entry, MAC-015 / MAC-016 / MAC-017 ledger, this Wave 4 narrative, interim README at `docs/c2-content/world-definitions/`. Comprehensive end-user guide (`docs/AUTHORING-GUIDE.md`) deferred to post-Editor-verification — authored by Opus directly from lived-in use rather than by Sonnet from spec. **Shipped 2026-05-03.**

Wave 4 ships the authoring substrate; Editor follow-up ships the user-facing palette UI / tools / sandbox scenes. With both in place, content (more starter scenes, scene packs, custom-archetype mods) can be authored without dispatch friction.

The UX bible v0.2 work is paused (the v0.2 commitments captured in conversation are the working draft; formal v0.2 file revision lands when it's ready). The original WP-4.0.0 critique packet is implicitly fulfilled by iterative Talon ↔ Opus drafting; no separate critique-packet artifact unless audit trail demands one.

### Phase 4.1.x — Audio + voice profiles + content tuning

- **WP-4.1.0 — Per-archetype voice profile generation.** Sims-gibberish phoneme profile per archetype.
- **WP-4.1.1 — Ambient soundscape.** Diegetic-only first pass. Couples to camera proximity.
- **WP-4.1.2 — Final art pipeline integration.** Replaces placeholder silhouettes + chibi sprites with hand-drawn final pixel art. Couples to 4.0.A.

### Phase 4.2.x — Multi-floor + emergent gameplay deepening

Multi-floor moved here from 4.0.x; the emergent scenarios that need it slot immediately after.

- **WP-4.2.0 — Multi-floor topology** (was 4.0.1). Engine extends `RoomComponent` and `PathfindingService`. Camera floor-switch verb. Stairwells topology-traversable. Schema v0.5 → v0.6 bump.
- **WP-4.2.1 — Plague week.**
- **WP-4.2.2 — PIP arc.**
- **WP-4.2.3 — Fire / disaster.** Test of multi-floor evacuation at scale.
- **WP-4.2.4 — Affair detection mechanic.**

### Phase 4.3.x — Player verb expansion (after UX bible v0.2 formally lands)

- **WP-4.3.0 — Player embodiment surface** (per Q5 resolution = pure ghost camera, captured in conversation; HUD elements determined per playtest evidence).
- **WP-4.3.1 — Pick-up-NPC-and-drop intervention verb** (per Q6 resolution = physical placement primary; click-context-menu interim with Force/Urgent verbs).
- **WP-4.3.2 — Schedule editing.**

### Phase 4.4.x — Hardcore mode + ship prep

- **WP-4.4.0 — Hardcore mode.**
- **WP-4.4.1 — Final balance + tuning passes.**
- **WP-4.4.2 — Tutorial / first-launch experience.**

**Starter priority order (2026-05-02 calibration, revised post-URP decision):**
1. Wave 1 batch (dispatch immediately, parallel-safe — disjoint file surfaces): **WP-4.0.A** (URP migration foundation), **WP-4.0.B** (NPC anti-overlap, engine-only), **WP-4.0.C** (build footprint foundation, engine + additive Unity read).
2. After 4.0.A merges (URP active): **WP-4.0.A1** (pixel-art Renderer Feature sandbox), then **WP-4.0.A1-INT** (integrate into MainScene/PlaytestScene), then **WP-4.0.D** (floor identity) + **WP-4.0.E** (NPC readability) + **WP-4.0.H** (particle vocabulary, likely VFX Graph) in parallel.
3. After 4.0.C merges: **WP-4.0.G** (build mode v2 / BUG-001) using the footprint substrate.
4. **WP-4.0.F** (dev-mode introspection) any time after 4.0.A; doesn't strictly depend on it but benefits from URP being settled.
5. Phase 4.1.x dispatches when 4.0.x stabilizes.

---

## Token budgets

Phase 3 measurements held throughout. For Phase 4 packets:

- **Engine packet (4.0.1, 4.2.x)** — 60–150 min Sonnet, $0.30–$0.60 budget.
- **Unity / shader packet (4.0.2, 4.1.x)** — likely larger; new shader surface. Budget 120–180 min, $0.50–$0.80.
- **Content / tuning packet (4.4.1)** — Haiku-heavy. Cast-validate-pattern spec; $0.30–$0.50 per run.
- **Real-API verification runs** — $0.13–$0.40 per run depending on cache warmth.

Phase 4 total estimate: $30–$60 across all engine + Unity packets, depending on iteration count. The art / animation pipeline work (4.1.2) is outside the Sonnet flow; this estimate covers code packets only.

---

## Operational reality

Talon executes; you author. Your output is `WP-NN-<slug>.md` files. Talon dispatches Sonnets in Claude Code sessions in worktrees. You do not run code; you author briefs that Sonnets implement. When you produce a packet, **commit it before Talon creates the worktree**. The cleanup branch reorganized completed specs into `_completed-specs/`; Phase 4 packets go in the active `docs/c2-infrastructure/work-packets/` directory.

**For packets with hard wave dependencies**, put a **DO NOT DISPATCH UNTIL X IS MERGED** line at the top of the packet itself. The integration history (Phase 3.0/3.1 had a bundling incident) showed this discipline pays.

The shared-file conflict pattern is predictable: `SimulationBootstrapper.cs`, `SimConfig.cs/json`, additive enums (`NarrativeEventKind`, `IntendedActionKind`, `DialogContextValue`, `SoundTriggerKind`, `RescueKind`, `ChoreKind`) will conflict at merge. Resolution is always "keep both" — 30 seconds per file. Don't try to design around it.

For Unity packets in 4.0.2 / 4.1.x: the `ECSUnity/` project is real. Subsequent packets reference its existing structure (Camera/, Render/, UI/, Animation/, BuildMode/, DevConsole/, Telemetry/).

---

## Your first response should

1. Confirm you've read the SRD (especially §8 and §4.1), this kickoff brief, the Phase 3 handoff (or note its absence), the UX/UI bible v0.1, and the relevant content bibles.
2. Echo back your understanding of the current project state in 4–6 sentences. This is the calibration check — Talon will correct any drift before you commit to packets.
3. Either propose **WP-4.0.0 — UX/UI bible v0.2 critique packet** as your first draft, or, if no PHASE-3-HANDOFF.md exists yet, propose authoring it as your first deliverable so the audit trail is complete before content work begins.
4. Ask one clarifying question only if something is genuinely ambiguous; otherwise proceed.

Don't restate the architectural axioms. Don't volunteer a roadmap revision unless the bibles force one. Don't ask permission to start — Talon's invocation of you is the permission.

You are Opus. Phase 4 begins now.
