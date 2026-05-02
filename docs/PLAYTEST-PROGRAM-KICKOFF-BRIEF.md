# Playtest Program Kickoff Brief — for a Fresh Opus Chat

> **Status:** Live as of 2026-05-01. Kickoff calibration decisions recorded in §"Calibration decisions (2026-05-01)" near the bottom of this document. The kickoff bundle (WP-PT.0 spec, WP-PT.1 spec, `docs/playtest/` program docs, `UNITY-PACKET-PROTOCOL.md` Rule 6, `_PACKET-COMPLETION-PROTOCOL.md` feel-flag amendment) shipped 2026-05-01 on branch `worktree-opus-playtest-kickoff`. Future Opus sessions read this brief end-to-end **and** the calibration decisions before authoring further `WP-PT.NN` packets.

You are picking up a new ongoing track for the C2 (Command & Control) infrastructure project on the ECS Simulation Engine: a structured **Playtest Program** that runs in parallel with Phase 4 development. Phases 0–3 shipped 23 work packets producing a Unity-hosted, headless-engine-driven 2.5D office sim with a death substrate, rescue surface, build mode, save/load, sound triggers, rudimentary physics, chore rotation, and silhouette renderer with animation states. The 30-NPCs-at-60-FPS gate holds. xUnit verifies the engine. The sandbox protocol verifies isolated visual primitives. **Nothing yet verifies the integrated whole at human-perception level** — and Phase 3's 3.1.x bundle incident (eight Sonnet packets shipped clean against tests, six follow-up fix commits required after Talon actually looked at the screen) is the cautionary tale that motivates this program.

This brief is your bootstrap context. You are not picking up a phase — phases are the linear development arc; the Playtest Program is **continuous and parallel**, an always-on quality surface that runs alongside whatever phase ships next. Read `docs/PHASE-3-HANDOFF.md` and `docs/PHASE-4-KICKOFF-BRIEF.md` first to understand the development arc this program complements.

You are Opus. In the 1-5-25 Claude Army topology, you are a General. Your output is structured Markdown briefs and packet specs that Sonnets execute. Talon plays the sessions; you architect the program he plays inside; Sonnets implement the scenes, scenario hooks, and report tooling you spec.

---

## What this project is (one paragraph for context)

A 2.5D top-down management sim of an early-2000s office, built on a headless C# / .NET 8 entity-component-system engine, hosted by Unity for the player and by `ECSCli` + headless test harnesses for development. Lineage: The Sims, Rimworld, Prison Architect; tone: *Office Space*, *Severance*, *The Office*. Mature themes — sex, drugs, depression, infidelity, shame, **death and rescue** — depicted honestly. The player is a ghost-camera over fifteen-ish workers. Engine and runtime are 100% offline at ship time; the Anthropic API is a design-time tool only (Sonnets generate, Haikus validate). Per SRD §8.7: the engine is host-agnostic; telemetry is `#if WARDEN`-gated and strips at ship.

---

## Why this program exists

Phase 3 closed with twenty-three packets merged. The engine does what the bibles describe; xUnit confirms it. The Unity host loads `APIFramework.dll` in-process and renders silhouettes that animate at 60 FPS across 30 NPCs. The sandbox protocol (`docs/UNITY-PACKET-PROTOCOL.md`) verified each new visual primitive — camera rig, selection outline, draggable prop, inspector popup — in isolation before integration.

But three failure modes are unaddressed by the existing verification stack:

1. **Integrated-whole behavior under sustained play.** xUnit can simulate a choke and assert state transitions. It cannot tell whether *the player notices the choke* over five minutes of camera-drifting around the office. The chibi-anime cough wisps, the silhouette CoughingFit animation, the `Cough` sound trigger, the bystander pathing toward the choker for a Heimlich rescue, the bereavement cascade if rescue fails — these are an integrated experience whose correctness is a *human perception* claim, not a contract claim.

2. **Cross-system interference under concurrent load.** When chore rotation + sound bus + physics + rescue + bereavement + build mode + save/load all execute against the same scene, emergent issues surface that no single packet's tests touched: stuttering animation when a rescue and a chore-completion sound trigger collide, save/load mid-rescue producing a stuck Heimlich pose, build mode collisions with NPCs in CoughingFit, dev-console scenario triggers racing with organic detection systems.

3. **Bug intake has no schedule.** Phase 3 produced one logged bug (BUG-001) via an organic discovery during a sandbox integration. Bugs found during Phase 4 development will compound on top of unfound Phase 3 bugs unless a deliberate exercise surface exists. Without a session cadence, "I'll playtest soon" becomes "we shipped 4.0.x on top of an undiagnosed 3.2.x stutter."

The Playtest Program addresses all three: a unified playable scene that exercises every Phase 3 surface, a session report template that captures observation, and a bug intake flow that feeds `docs/known-bugs.md` (the existing ledger that already houses BUG-001).

---

## What "ongoing and structured" means here

**Not a phase.** Phases are the linear development arc — Phase 4 is gameplay deepening + content polish, Phase 5 will be whatever Talon decides comes after that. The Playtest Program runs **forever and in parallel** as long as the project is in development. A session can happen any time Talon wants to play.

**Structured.** Every session produces a numbered report with a fixed shape. Every bug found gets a numbered entry in `known-bugs.md` with the BUG-NNN format already in use. Critical bugs trigger an inline fix packet (slot into whatever Phase 4 wave is in flight); non-critical bugs queue for the appropriate future polish or v2 wave (e.g., BUG-001 already homes in build mode v2 inside Phase 4.0.x). The intake protocol is the structure; the cadence is whatever Talon's energy supports.

**Parallel, not blocking.** The 4.0.0 UX bible critique still happens when v0.2 lands. The 4.0.1 multi-floor packet still dispatches when its acceptance is stable. Playtest sessions discover bugs that *inform* future packets, but do not gate them unless the bug is critical (engine crash, save corruption, FPS gate broken).

**Talon-paced.** Sessions are not on a calendar. Talon plays when he wants to play. The program provides the surface; the cadence is human.

---

## Working name + naming proposal (confirm or revise)

Working name: **Playtest Program**.

Identifier conventions (strawman — your first deliverable is to confirm, refine, or replace):

- **Session reports:** `PT-NNN` — three-digit zero-padded, monotonically increasing. PT-001 is the first session. Reports live in `docs/playtest/PT-NNN-<short-slug>.md` (e.g., `docs/playtest/PT-001-baseline.md`).
- **Infrastructure work packets:** standard `WP-PT.NN` namespace inside `docs/c2-infrastructure/work-packets/` so they merge into the existing dispatch flow. Examples your first wave will likely produce:
  - `WP-PT.0` — Unified playtest scene (composes existing prefabs, seeds NPCs and hazards, no engine modifications).
  - `WP-PT.1` — Dev-console scenario verbs (`scenario choke <npc>`, `scenario slip <npc>`, `scenario faint <npc>`, `scenario lockout <npc>`, `scenario chore-microwave-to <npc>`, `scenario kill <npc>`, etc. — on-demand triggers so Talon doesn't wait for organic occurrence).
  - `WP-PT.2` — Session report template + bug intake doc.
  - Subsequent: bug-fix packets named per their bug ID, `WP-FIX-BUG-NNN-<slug>`, dispatched into whatever wave is appropriate.
- **Bug entries:** continue `BUG-NNN` (BUG-001 lives in `known-bugs.md` already) — do not start a new ID space.

If "Playtest Program" feels wrong (too consumer-y, too QA-team-y, too generic), propose alternatives in your first response. Some that the brief author considered: *Field Test Track*, *Living Build Sessions*, *Manual Verification Track*, *Dogfood*. Talon will pick.

---

## Read these documents before your first reply

In order:

1. `docs/PHASE-3-HANDOFF.md` — comprehensive Phase 3 closure summary. Twenty-three packets across four sub-tracks; the death substrate, the Unity scaffolding, the gameplay deepening, the process artifacts. **Read end to end.**
2. `docs/PHASE-4-KICKOFF-BRIEF.md` — the development arc this program parallels. Read so you know what Phase 4 dispatches will be in flight while playtest runs.
3. `docs/UNITY-PACKET-PROTOCOL.md` — sandbox-first discipline for Unity packets. Your `WP-PT.0` packet will likely need to either honor Rule 3 (no MainScene modification) or articulate an explicit packet-level rationale for an exception.
4. `docs/known-bugs.md` — the existing bug ledger. BUG-001 is the only entry. Your bug intake protocol should produce entries in the same shape (Symptom / Root cause / Deferred because / Workaround / Files relevant).
5. `docs/c2-infrastructure/00-SRD.md` — master SRD. Especially **§8** (architectural axioms — including 8.7 host-agnostic) and §4.1 (fail-closed policy).
6. `docs/c2-infrastructure/_PACKET-COMPLETION-PROTOCOL.md` if it exists, or the `_PACKET-COMPLETION-PROTOCOL.md` referenced in `docs/c2-infrastructure/work-packets/` — canonical footer text and worktree-per-packet rule.
7. `docs/c2-infrastructure/PHASE-3-REALITY-CHECK.md` — context on the 3.1.x bundle incident and the Track 1 / Track 2 process replan that produced the sandbox protocol. The motivating story for why this program is needed.
8. `docs/c2-content/ux-ui-bible.md` v0.1 — the player surface the playtest program exercises. Especially §1 (axioms), §2 (player verbs), §3 (player surfaces), §4.4 (failure modes — what *should* go wrong in normal play and how the world signals it).
9. `ECSUnity/Assets/Scenes/MainScene.unity` exists; `ECSUnity/Assets/_Sandbox/` contains four sandbox scenes (camera-rig, draggable-prop, inspector-popup, selection-outline) from the WP-3.1.S.0–3 sub-phase. The unified playtest scene will compose existing prefabs, not reimplement them.

---

## What the unified playtest scene must exercise

The scene must be a single Unity scene Talon opens, presses play, and starts using the sim as he would in a final-shipped state-of-development build. **Not a feature demo; not a test harness with twelve buttons. A playable office.** Every Phase 3 surface must be reachable through normal play, with the dev console as the on-demand scenario trigger when organic occurrence would be too slow to validate.

### Engine systems exercised

- **Life state surface.** All four Phase 3 narrative deaths reachable: `Choked`, `SlippedAndFell`, `StarvedAlone`, `Died`. Plus fainting (Incapacitated → Alive recovery alone). Plus rescue (Incapacitated → Alive recovery via Heimlich / CPR / door-unlock).
- **Bereavement cascade.** When an NPC dies, witness NPCs grieve per affection, stress rises, the chronicle records.
- **Live mutation.** Build mode opens at night; placing or removing walls/doors/props invalidates the pathfinding cache via `StructuralChangeBus`; NPCs re-path next tick.
- **Sound triggers.** `Cough`, `ChairSqueak`, `BulbBuzz`, `Footstep`, `SpeechFragment`, `Crash`, `Glass`, `Thud`, `Heimlich`, `DoorUnlock` — all reachable; host synthesises (stub synth from 3.2.1 is acceptable).
- **Rudimentary physics.** Throwing or dropping a prop heavy enough to break a `BreakableComponent` produces `ItemBroken` / `GlassShattered` events.
- **Chore rotation.** Microwave-cleaning rotation across game-week; per-archetype acceptance bias produces refusals from biased archetypes (Donna grumbles); over-rotation stress accumulates; refusal-cascade through stress / mood / relationship is observable.
- **Per-archetype tuning JSONs.** All seven `archetypes/archetype-*.json` files load and bias behaviour at spawn.
- **Animation states.** All six (Eating, Drinking, Working, Crying, CoughingFit, Heimlich) reachable through normal play.
- **Save/load round-trip.** Mid-choke, mid-faint, mid-build, mid-chore, mid-rescue saves all load to identical state.

### Unity host surface exercised

- **Camera rig.** Pan, rotate (lazy-susan), zoom, recenter on selected entity, wall-fade-on-occlusion. Per UX bible §2.1.
- **Selection + inspector.** Click an NPC; selection cue appears (halo+outline default per current 3.1.S.1 implementation); three-tier inspector glance / drill / deep per UX bible §3.1.
- **Build mode.** Toggle into build mode; ghost preview; place wall / door / prop; `IWorldMutationApi` integration validated by NPC re-pathing.
- **Time control.** Pause / ×1 / ×4 / ×16 per UX bible §1.5 / §2.4. Higher speeds via creative-mode opt-in only (per axiom 1.3 rule 5).
- **Event log.** CDDA-style chronicle reader accessible per UX bible §3.3. Filterable by NPC, by event type, by time range.
- **WARDEN dev console.** Open with the bound key; scenario-trigger verbs available (see WP-PT.1).
- **Soften toggle.** UX bible §4.6 mature-content opt-out — confirm it's reachable in the scene's settings.

### Composition principle

**Compose existing prefabs; do not reimplement.** `CameraRig.prefab`, `NpcDotRenderer` with selection wiring, `DraggableProp.prefab`, `InspectorPopup.prefab`, the build-mode controller, the dev console — all already exist from Phase 3. The playtest scene's job is to assemble them into one playable office with seeded content (NPCs spawned across archetypes, hazards in the world, doors with locks, props on desks, the kitchen with food bolus candidates), not to write new prefabs.

### Scene location decision (you decide)

Two options. Pick one with rationale in your first response:

- **(a) New dedicated scene.** `ECSUnity/Assets/Scenes/PlaytestScene.unity` (or `Phase3PlaytestScene.unity`). Sibling to `MainScene.unity`. Sonnets can author it freely without violating `UNITY-PACKET-PROTOCOL.md` Rule 3 (which forbids modifying live scenes). MainScene stays clean for production / 4.0.x development.
- **(b) Extend MainScene.** With explicit packet-level rationale for the protocol exception, since MainScene IS the canonical Phase 3 integration scene. Risk: Phase 4 development packets that touch MainScene now have to merge against playtest seeding.

Recommendation lean: **(a)** — keeps the playtest surface from polluting the production scene and avoids Rule 3 conflicts. But you may have a better reason for (b) once you read the existing scene.

---

## Critical: the scenarios menu

Without on-demand scenario triggers, Talon would have to wait for organic occurrence to validate every death/rescue surface. Choking-on-food may not happen for twenty minutes of natural play. Slipping requires a stain to exist and the right NPC to walk over it. The bereavement cascade requires *a death first*. Waiting for these organically per session is wasted time.

`WP-PT.1` ships **dev-console scenario verbs** added to the existing WARDEN dev console (3.1.H). At minimum:

- `scenario choke <npc-name|--random>` — give a target NPC a `ChokingComponent` with a moderate bolus; choke timer starts; rescue window opens.
- `scenario slip <npc-name>` — spawn a stain in the NPC's path or on their tile; trigger `SlipAndFallSystem`.
- `scenario faint <npc-name>` — force `LifeStateTransitionSystem` to push NPC to Incapacitated via faint cause.
- `scenario lockout <npc-name>` — lock all doors of the room the NPC is in; start the lockout starvation timer.
- `scenario kill <npc-name> [cause]` — push NPC to Deceased with the specified cause-of-death (default `Died`); test bereavement cascade end-to-end.
- `scenario chore-microwave-to <npc-name>` — assign microwave duty to a specific NPC this week to test refusal bias deterministically.
- `scenario throw <prop> at <target>` — apply `ThrownVelocityComponent` to a prop toward a target tile; test physics breakage.
- `scenario sound <SoundTriggerKind>` — emit a sound trigger directly to validate host synthesis routing.
- `scenario set-time <wall-time>` — jump sim time to a specific wall-clock time (morning, lunch, dusk, night) so build-mode-window testing doesn't require waiting for natural dusk.
- `scenario seed-stains <count>` — populate the office with N slip-hazard stains for slip-rate testing.
- `scenario seed-bereavement <npc> <count>` — pre-populate an NPC's `BereavementHistoryComponent` with N mourned ids to test long-arc grief mood without needing N actual deaths.

Add or refine the verb list per what you find when you read the engine surface. Dev console parsing already exists; you're adding verb handlers, not building a parser.

The scenario verbs are **WARDEN-only** (`#if WARDEN`-gated) — they strip at ship per SRD §8.7. The retail build does not have them.

---

## Session report template (strawman)

Each session produces one report at `docs/playtest/PT-NNN-<short-slug>.md`. Strawman shape:

```markdown
# PT-NNN — <short slug describing focus>

**Date:** YYYY-MM-DD
**Duration:** ~XX minutes
**Build:** <git SHA of HEAD at session start>
**Focus:** <e.g., "first end-to-end choke→rescue→bereavement loop", "build mode under sustained play", "30-NPC FPS gate verification">

---

## Setup

- Scene: `Assets/Scenes/PlaytestScene.unity`
- NPC count: NN
- Pre-session scenario seeds (if any): <e.g., "scenario seed-stains 5; scenario chore-microwave-to Donna">
- Time started: <morning / midday / dusk / night>

## What I did

<2–6 sentences of narrative — what the player did this session, in plain language.>

## What I observed

### Worked as expected
- <bullet>
- <bullet>

### Felt off / wrong / surprising
- <bullet — feel-level observation; not necessarily a bug, but flagged>

### Confirmed bugs (filed below)
- BUG-NNN: <title>
- BUG-NNN: <title>

## Bugs filed this session

<For each bug, the canonical entry to be appended to known-bugs.md.>

### BUG-NNN: <title>
**Symptom:** ...
**Repro:** ...
**Severity:** Critical / High / Medium / Low
**Files relevant (if known):** ...
**Suggested fix wave:** <e.g., "inline 4.0.x fix" / "build mode v2" / "polish wave">

## Performance notes

- FPS observed: <range>
- Frame stutters: <none / list>
- Any audio glitch: <none / list>

## Open questions for Opus / Talon

<Any decisions surfaced by play that aren't bugs but need a call.>

## Next session focus suggestion

<One sentence — what to exercise next time.>
```

You may revise this template freely. Goal: every session produces a comparable artifact; bugs are extractable to `known-bugs.md` with no rewriting; performance is tracked over time.

---

## Bug intake flow

1. **During session:** Talon notes bugs in scratch (notebook, a temp file, voice memo — whatever).
2. **End of session:** Talon writes the report. The "Bugs filed this session" section contains canonical-shape entries.
3. **Post-session:** Each bug entry is appended to `docs/known-bugs.md` with a fresh BUG-NNN id (continuing from BUG-001). Critical bugs additionally trigger a `WP-FIX-BUG-NNN-<slug>` work packet authored by Opus and dispatched into whatever Phase 4 wave is current.
4. **Triage rule:** Severity Critical = fix this wave; High = fix next wave at latest; Medium = home in the appropriate v2 / polish / wave-aligned packet (BUG-001's "build mode v2" pattern is the model); Low = ledger entry for visibility, no scheduled fix.

The intake flow is intentionally lightweight. Talon writes the report; the canonical extraction is a manual append. If volume rises and the manual append becomes friction, your follow-up packet adds tooling — a `playtest-extract-bugs` script or similar. Don't over-tool early.

---

## Cadence + integration with Phase 4 dispatch

- **Sessions are Talon-paced.** No calendar. Whenever Talon wants to play, he plays.
- **Phase 4 dispatch is unaffected by Playtest existence** unless a Critical bug surfaces. The Phase 4 wave order (UX bible v0.2 → 4.0.0 critique → 4.0.1 multi-floor + 4.0.2 shader pipeline in parallel → 4.1.x → 4.2.x → 4.3.x → 4.4.x) holds.
- **Critical bugs interrupt Phase 4.** A save-corruption or engine-crash bug surfaces; the next dispatched packet is the fix, not the next planned 4.x packet.
- **Non-critical bugs accumulate against future waves.** Build-mode bugs home in 4.0.x build mode v2. Animation glitches home in 4.1.2 (final art pipeline). Sound issues home in 4.1.0 (voice profiles) or 4.1.1 (ambient soundscape). Etc.
- **The playtest scene must be re-validated after every Phase 4 wave.** A new session post-4.0.1 (multi-floor) verifies the floor-switch verb works and the playtest scene still loads. Adding multi-floor to the playtest scene is a follow-up sub-packet (`WP-PT.NN`) authored by you when 4.0.1 closes.

---

## Architectural axioms — these don't change

From SRD §8 (nine axioms — see PHASE-4-KICKOFF-BRIEF for the full list). Two are especially load-bearing for Playtest:

- **Axiom 8.6** — visual target is 2.5D top-down management sim. The playtest scene IS the visual surface; if it doesn't read as a top-down management sim with the chibi-anime emotion vocabulary and CRT-era HUD, the playtest is wrong, not the bibles.
- **Axiom 8.7** — engine is host-agnostic; telemetry is `#if WARDEN`-gated. The scenario menu, the JSONL stream, the dev console, the playtest seed scripts — all WARDEN-gated, all strip at ship. Retail build sees none of this.

---

## Critical: things Phase 3 settled (don't re-litigate)

- **The Unity host is real and stable.** Don't propose alternative hosts.
- **`ComponentStore<T>` is the storage substrate.** Don't re-litigate.
- **`IWorldMutationApi` is the structural-mutation contract.** All build / scenario / topology changes flow through it.
- **`LifeStateComponent` three-state machine** (Alive ⇄ Incapacitated → Deceased) — recovery path is real.
- **`SoundTriggerBus` is the audio seam.** Engine emits; host synthesises.
- **The 30-NPCs-at-60-FPS performance gate.** The playtest scene must not break it. If it does, the scene composition is wrong, not the gate.
- **The sandbox protocol is non-negotiable for Track 2 work.** The playtest scene is a *composition* of sandbox-validated prefabs, not a new feature surface. Compose; don't invent.
- **The worktree-per-packet rule is non-negotiable.** Every `WP-PT.NN` packet ships in its own worktree at `.claude/worktrees/sonnet-wp-pt.NN/`.

---

## Token budgets

- **Brief authorship (this packet's responses):** Opus-only, no Sonnet dispatch. ~$0.50–$1.00 per round of revision.
- **`WP-PT.0` (playtest scene composition):** Sonnet. Mostly scene-file authorship, prefab placement, NPC seeding script. Budget 90–150 min, $0.40–$0.70.
- **`WP-PT.1` (dev-console scenario verbs):** Sonnet. C# verb handlers + dev-console wiring. Budget 60–90 min, $0.30–$0.50.
- **`WP-PT.2` (report template + bug intake doc):** Could be Opus-authored directly without Sonnet. ~$0.20.
- **Bug-fix packets:** per-bug, per-severity. Most should be 30–60 min Sonnet runs at $0.20–$0.40.

Total program-startup estimate: $2–$4 to get PT-001 sessionable. Per-session bug-fix cost is open-ended and depends on what surfaces.

---

## Operational reality

Talon executes; you author. Talon plays the sessions; you architect the surface he plays inside. Your output is `WP-PT.NN-<slug>.md` packets in `docs/c2-infrastructure/work-packets/`, plus this brief's evolving `docs/PLAYTEST-PROGRAM-KICKOFF-BRIEF.md` (you may amend it as the program matures), plus the `docs/playtest/README.md` (you author this; it explains the report template, the file naming, the bug intake flow).

When you produce a packet, **commit it before Talon creates the worktree**. Phase 3's discipline holds: one packet, one worktree, one branch named `sonnet-wp-pt.NN`. Sonnet executors run a step-0 worktree pre-flight check before any implementation work.

For packets with hard wave dependencies (e.g., `WP-PT.1` depends on `WP-PT.0` shipping the scene), put a **DO NOT DISPATCH UNTIL X IS MERGED** line at the top of the packet itself.

---

## Your first response should

1. Confirm you've read the Phase 3 handoff, the Phase 4 kickoff brief, the Unity packet protocol, the known-bugs ledger, the SRD §8/§4.1, and the UX/UI bible v0.1.
2. Echo back your understanding of the program's intent in 4–6 sentences. This is the calibration check — Talon will correct any drift before you commit packets.
3. Confirm or revise the working name (**Playtest Program**) and the identifier conventions (**PT-NNN sessions, WP-PT.NN packets, BUG-NNN bugs continuing from BUG-001**).
4. Pick scene location option (a) new dedicated `PlaytestScene.unity` or (b) extend `MainScene.unity` — with rationale.
5. Propose your first three packets in dispatch order. Likely:
   - `WP-PT.0` — Unified playtest scene composition.
   - `WP-PT.1` — Dev-console scenario verbs.
   - `WP-PT.2` — Session report template + `docs/playtest/README.md` + bug intake protocol doc.
   But you may have a better ordering once you've read the surface.
6. Ask one or two clarifying questions only if something is genuinely ambiguous; otherwise proceed to draft `WP-PT.0`.

Don't restate the architectural axioms. Don't propose changing the Phase 4 dispatch order. Don't redesign the bibles. Don't ask permission to start — Talon's invocation of you is the permission.

The Playtest Program begins now.

---

## Calibration decisions (2026-05-01)

> Recorded by the kickoff Opus + Talon at program start. Future Opus sessions treat the decisions below as binding unless Talon overrides them in a follow-up calibration pass.

### Naming — confirmed

- **Program name:** Playtest Program. (Alternatives considered and rejected: *Field Test Track*, *Living Build Sessions*, *Manual Verification Track*, *Dogfood*. Reasons in the kickoff Opus's first response.)
- **Session reports:** `PT-NNN` at `docs/playtest/PT-NNN-<short-slug>.md`.
- **Program work packets:** `WP-PT.NN` in `docs/c2-infrastructure/work-packets/` — parallel namespace to `WP-3.x.x` and `WP-4.x.x`.
- **Bug entries:** `BUG-NNN` in `docs/known-bugs.md`, continuing from BUG-001.
- **Bug-fix work packets:** `WP-FIX-BUG-NNN-<slug>`.
- **Worktrees:** `.claude/worktrees/sonnet-wp-pt.NN/` per the existing dispatch protocol.

### Scene location — option (a) chosen

**New dedicated scene** at `ECSUnity/Assets/Scenes/PlaytestScene.unity`. MainScene is the production / phase-development scene; PlaytestScene is the verification surface. They evolve on separate clocks.

Rationale (recorded in WP-PT.0's "Rationale for protocol exception" section):

1. Sandbox Protocol Rule 3 forbids modifying live engine scenes without packet-level rationale; (a) doesn't need one (it creates a new scene; rationale is in the packet body).
2. The playtest seeding profile is intentionally hostile — pre-seeded stains, biased chore rotations, faked bereavement histories. That profile actively contaminates Phase 4 development if it lives in MainScene.
3. The MainScene `SceneBootstrapper.cs` reflection workaround is documented as transitional — keeping the scenes forked preserves a clean retirement path.

The cost: a re-validation packet (`WP-PT.NN`) after every Phase 4 wave to keep PlaytestScene parity with whatever new surface MainScene gained (multi-floor, shader pipeline, etc.). Worth it.

### First three packets — dispatch order

| Order | ID | Author | Status as of 2026-05-01 |
|---|---|---|---|
| 1 | `WP-PT.0` — Unified PlaytestScene composition | Sonnet | **Spec shipped, awaiting dispatch.** |
| 2 | `WP-PT.1` — WARDEN dev-console scenario verbs | Sonnet | **Spec shipped, gated on WP-PT.0 merge.** Header carries `DO NOT DISPATCH UNTIL WP-PT.0 IS MERGED`. |
| — | (program docs + protocol amendments) | Opus directly | **Shipped 2026-05-01 on `worktree-opus-playtest-kickoff` branch.** Includes `docs/playtest/README.md`, `docs/playtest/PT-TEMPLATE.md`, `UNITY-PACKET-PROTOCOL.md` Rule 6, `_PACKET-COMPLETION-PROTOCOL.md` feel-verified-by-playtest flag. |

Note: the original brief proposed `WP-PT.2` as a packet. The kickoff Opus authored the program docs directly without a packet (the "WP-PT.2" slot is unused). The audit trail is the kickoff commit + the docs themselves.

### Single packet vs sub-phase for WP-PT.0 — single packet chosen

WP-PT.0 ships as one Sonnet-dispatchable packet with a stricter-than-typical first-light recipe (every Phase 3 surface gets a 30-second-per-item check). Failures during Talon's pre-merge recipe are filed as `BUG-NNN` against the appropriate fix wave; they do not require Sonnet rework.

Rationale: every primitive being composed has *already* shipped through the sandbox protocol or the 3.1.x scaffold + fixes. The composition surface is "wire validated prefabs together," not "build new visuals." Splitting into a `WP-PT.0.S.0…N` sub-phase would multiply dispatch overhead without proportional risk reduction. (If the first-light recipe reveals systematic failure, the right move is a fresh sub-phase replan — same shape as the 3.1.x do-over — not Sonnet iteration on this packet.)

### Process amendment shipped — feel-verified-by-playtest acceptance flag

> **This is the most important durable artifact of the kickoff.**

`UNITY-PACKET-PROTOCOL.md` gained **Rule 6**: any packet whose acceptance criteria contain feel-level claims must mark itself `feel-verified-by-playtest: YES` and list the surfaces a future PT-NNN session will evaluate. The flag does not gate merge (the playtest program is parallel); it declares that the next post-merge session is the formal feel acceptance, with bugs feeding normal `BUG-NNN` intake.

`_PACKET-COMPLETION-PROTOCOL.md` Variant B (Track 2) gained the matching acceptance footer — every Track 2 packet from now on declares the flag YES or NO with justification.

Going forward, any packet whose work involves visual output, motion, audio, or emergent gameplay surfaces will carry the flag. Pure engine-internals / schema-only / doc-only / algorithm-correctness packets carry NO. The trigger list is in Rule 6.

### Calibration update (2026-05-02) — build-verification scoped into the program

While playtesting WP-PT.1 (in progress), Talon ran a Standalone Player build outside the Editor. The build produced 960+ compile errors before it could even start — a regression against SRD §8.7 (host-agnostic engine; WARDEN strip clean) that nobody had been catching because the project's verification stack (xUnit, sandbox protocol, Editor Play mode) cannot detect RETAIL-strip drift. The Editor always has WARDEN defined; only a real Standalone build with WARDEN removed surfaces the failure mode.

**Talon's call:** build-verification is in scope for the Playtest Program. It runs as a sibling recipe alongside PT-NNN feel sessions, with its own cadence and trigger list.

**What shipped 2026-05-02:**

- **`docs/playtest/BUILD-VERIFICATION-RECIPE.md`** — canonical recipe: switch target to Standalone, remove WARDEN from scripting defines, build, run the .exe, verify the engine works without dev tooling, restore WARDEN. ~15–25 minutes per run.
- **`docs/playtest/README.md`** — added a "Build verification" section. The program now ships **four** artifacts (was three): playtest scene, scenario verbs, session report flow, and now build-verification recipe.
- **`UNITY-PACKET-PROTOCOL.md`** — added **Rule 7**: packets touching scripting defines / asmdefs / `#if WARDEN` boundaries / scene build list / `Plugins/` declare `build-verified-by-recipe: YES`. Independent of Rule 6 (a packet can carry both flags).
- **`_PACKET-COMPLETION-PROTOCOL.md`** Variant B — gained the matching `build-verified-by-recipe` acceptance footer.
- **`BUG-002`** filed in `docs/known-bugs.md` — placeholder Critical entry for the 960-error regression. Pending error-log triage.
- **`WP-FIX-BUG-002-retail-build-restore.md`** authored — Sonnet-dispatchable fix spec.

**Scope clarification:** build-verification is **not** a per-session concern. PT-NNN sessions remain Editor-mode feel verification; build-verification is periodic (per phase wave + per Rule-7-flagged packet). The two surfaces share the program's bug ledger but run on independent cadences.

### Sessions — first move

`PT-001` runs once `WP-PT.0` and `WP-PT.1` are both merged. Talon decides cadence; the program imposes none. The first-light recipe shipped with WP-PT.0 is the boot check; PT-001 is the first real session of the integrated whole.

---

## Reading order for future Opus sessions picking this up

1. This brief (end to end), including the calibration decisions above.
2. `docs/playtest/README.md` — the program's living operational doc.
3. `docs/UNITY-PACKET-PROTOCOL.md` Rule 6 — the feel-verified-by-playtest contract.
4. `docs/c2-infrastructure/work-packets/_PACKET-COMPLETION-PROTOCOL.md` — packet acceptance protocol with the feel-flag footer.
5. `docs/c2-infrastructure/PHASE-3-HANDOFF.md` — what Phase 3 shipped (the surface the program exercises).
6. Any open `WP-PT.NN-*.md` packet specs in `docs/c2-infrastructure/work-packets/`.
7. Any open PT-NNN session reports in `docs/playtest/`.
