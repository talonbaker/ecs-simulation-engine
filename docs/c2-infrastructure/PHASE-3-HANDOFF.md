# Phase 3 Handoff Summary

> Authored by the Phase 3 Opus at closure. The comprehensive reference for the next Opus picking up Phase 4. Read after `docs/PHASE-4-KICKOFF-BRIEF.md` (whose stale preamble this document supersedes — see §11).
>
> Mirrors the shape of `PHASE-2-HANDOFF.md` and `PHASE-1-HANDOFF.md`; this is the audit trail.

---

## 1. Phase 3 in one paragraph

Phase 3 took the gameplay-integrated engine Phase 2 produced and gave it a body, a pulse, and a mortality. Twenty-three Sonnet packets shipped across four sub-tracks: **3.0.x** built the death substrate (life-state component, cause-of-death events, choking / slipping / lockout / fainting scenarios, bereavement cascade, live-mutation hardening with `IWorldMutationApi` + `StructuralChangeBus`, and the `ComponentStore<T>` typed-array refactor that eliminated `Dictionary<Type, object>` boxing and unlocked 30-NPCs-at-60-FPS). **3.1.A–H** shipped the Unity scaffolding bundle (loads `APIFramework.dll` in-process; `WorldStateProjectorAdapter`; silhouette renderer; lighting visualization; build mode; player UI; JSONL stream; CDDA-style event log; dev console) — but did so as an eight-packet bundle that compiled and tested clean while rendering nothing on screen, requiring six follow-up fix commits to become visible. **The 3.1.x bundle incident** triggered the most consequential Phase 3 process decision: a Track 1 / Track 2 replan, a sandbox-first Unity packet protocol (`docs/UNITY-PACKET-PROTOCOL.md`), and the `WP-3.1.S.NN` sub-phase where the four foundational visual primitives — camera rig, selection outline, draggable prop, inspector popup — each shipped in their own atomic sandbox packet plus a paired `-INT` integration packet wiring them into MainScene. The do-over delivered. **3.2.x** then layered gameplay deepening on top: save/load round-trip hardening (schema bumped to v0.5 carrying the life-state surface), the `SoundTriggerBus`, rudimentary physics (`MassComponent`, `BreakableComponent`, deterministic decay, no continuous solver), the chore rotation system (the canonical NPC-driven challenge — Donna grumbles when it's her week to clean the microwave), the rescue mechanic (Heimlich, CPR, door-unlock — the world's compassion surface), per-archetype tuning JSONs (the balance-loop substrate), and silhouette animation state expansion. A parallel **Warden-side track** added the `AsciiMapProjector` (Unicode box-drawing floor plan, dev-time shared substrate for human + Sonnet + Haiku spatial reasoning) and wired it into Haiku validate prompts via a Stable/Volatile slab split. The worktree-per-packet rule was formalised mid-phase; the spec-completion protocol with self-cleanup-on-merge was established; the UX/UI bible v0.1 was authored at the Phase 3 boundary with three open questions deferred to v0.2 (Q4 notification carrier + challenge surface, Q5 player embodiment, Q6 intervention scope). **Phase 4 starts with UX bible v0.2** — Talon authors, Opus critiques, then 4.0.1 multi-floor and 4.0.2 the pixel-art-from-3D shader pipeline open. Build mode v2 (the home for BUG-001's eventual fix per `docs/known-bugs.md`) slots into Phase 4.0.x.

---

## 2. What shipped — by axis

### 2.1 Engine systems added (in `APIFramework`)

In tick-phase order:

- **Spawn / init phase:** `LifeStateInitializerSystem`, `MassInitializerSystem`, `BreakableInitializerSystem`, `ChoreRotationInitializerSystem`. Spawn-time attachment of Phase 3 components from per-archetype JSON catalogs.
- **Condition phase:** `ChokingDetectionSystem`, `SlipAndFallSystem`, `LockoutDetectionSystem`. Detection of imminent death conditions; produce candidate `NarrativeEventKind` values.
- **Cognition phase:** `LifeStateGuard` adopted by ~40 systems as a `if (!entity.IsAlive()) continue;` early-return. The guard pattern is now load-bearing across the engine — every action / drive / physiology system bypasses dead and incapacitated NPCs cleanly.
- **Cleanup phase:** `LifeStateTransitionSystem` (Alive ⇄ Incapacitated, Incapacitated → Deceased; routes to `CauseOfDeathComponent`); `CorpseSpawnerSystem`; `BereavementSystem` + `BereavementByProximitySystem` (witness grief intensity, stress gain scaled by affection); `ChoreRotationSystem` (per-archetype acceptance bias, over-rotation stress, refusal cascade); `RescueDetectionSystem` + `RescueCompletionSystem` (proximity-gated Heimlich / CPR / door-unlock); `PhysicsBreakageSystem` (hit-energy threshold, deterministic decay).
- **Topology / mutation:** `StructuralTaggingSystem` (boots `StructuralTag` onto walls/doors/obstacles); `PathfindingCacheInvalidationSystem` (subscribes to `StructuralChangeBus`, drops cache on emit). Producers: `RoomMembershipSystem`, `MovementSystem`, the `IWorldMutationApi` surface itself.
- **Storage substrate:** `ComponentStore<T>`, `ComponentStoreRegistry` — eliminated `Dictionary<Type, object>` boxing on `Entity`. Public API (`Get<T>`, `Has<T>`, `Set<T>`) unchanged at call sites; ~88% allocation reduction (~12 KB/tick in the 30-NPC scene); 10–20× faster `Get<T>()`.

Total: ~25 new systems registered in `SimulationBootstrapper`. The engine fact sheet (`docs/engine-fact-sheet.md`, regenerable via `ECSCli ai describe`, CI-checked by `FactSheetStalenessTests`) now lists ~73 systems and ~190 component types.

### 2.2 Engine components added

- **Life state:** `LifeStateComponent` (`Alive | Incapacitated | Deceased`); `CauseOfDeathComponent` (cause + tick + witness ids); `BereavementHistoryComponent` (per-NPC ledger of mourned ids).
- **Death scenarios:** `ChokingComponent` (bolus size, distraction inputs, timeout); `FallRiskComponent` (per-stain risk lookup); `LockedInComponent` (no-exit detection); `CorpseComponent`.
- **Topology mutation:** `StructuralTag` (only-tagged-entities trigger cache invalidation); `MutableTopologyTag` (whitelist for `IWorldMutationApi.MoveEntity`); `StructuralChangeEvent` (8 kinds: `EntityAdded`, `EntityRemoved`, `EntityMoved`, `WallAttached`, `WallDetached`, `DoorAttached`, `DoorDetached`, `RoomBoundsChanged`).
- **Audio:** `SoundTriggerKind` (10 values: `Cough`, `ChairSqueak`, `BulbBuzz`, `Footstep`, `SpeechFragment`, `Crash`, `Glass`, `Thud`, `Heimlich`, `DoorUnlock`); engine emits, host synthesises.
- **Physics (rudimentary):** `MassComponent`; `BreakableComponent` (`HitEnergyThreshold`); `ThrownVelocityComponent` (deterministic decay, no continuous solver).
- **Chores & rescue:** `ChoreAssignmentComponent` (current chore, week-counter, refusal-bias); `ChoreCompletionComponent`; `RescueIntentComponent` (Heimlich / CPR / door-unlock kind, target, urgency).

### 2.3 Narrative event kinds added

- `Choked`, `SlippedAndFell`, `StarvedAlone`, `Died` (general) — 4 values from WP-3.0.0; persistent globally.
- `BereavementWitnessed` — per-pair, persistent.
- `ChoreRefused`, `ChoreCompleted`, `MicrowaveCleaned` — chore-rotation feedback. Mostly per-pair persistent; `MicrowaveCleaned` is global non-persistent (background flavour).
- `ItemBroken`, `GlassShattered` — physics-driven, global persistent.
- `RescueAttempted`, `RescueSucceeded`, `RescueFailed` — per-pair, persistent.

### 2.4 Schema bumps

- **v0.5.0** (Phase 3.0.0 → 3.0.2): `entities[].lifeState` (enum); `entities[].causeOfDeath?` (object: kind, tick, witnessIds); `memoryEvents[]` gains `Choked | SlippedAndFell | StarvedAlone | Died | BereavementWitnessed` kinds.
- **v0.5.0 finalised** (Phase 3.2.0): every component family round-trips through `WorldStateDto` JSON. Mid-choke / mid-faint / mid-build / mid-chore saves load correctly. Save/load is now a stable contract — Phase 4 packets can rely on full round-trip semantics.

The `SCHEMA-ROADMAP.md` versioning rules held throughout. No major bumps required. Additive minors only.

### 2.5 SimConfig sections added

`lifeState`, `choking`, `bereavement`, `slipAndFall`, `lockout`, `livemutation`, `pathfindingCache`, `physics`, `chores`, `rescue`, `soundTriggers`. Plus per-archetype tuning JSONs:

- `archetypes/archetype-choke-rates.json`
- `archetypes/archetype-fall-rates.json`
- `archetypes/archetype-bereavement-bias.json`
- `archetypes/archetype-chore-acceptance.json`
- `archetypes/archetype-rescue-bias.json`
- `archetypes/archetype-mass.json`
- `archetypes/archetype-memory-persistence.json`

### 2.6 SRD additions

No new architectural axioms. Phase 3 confirmed two existing ones rather than adding more:

- **Axiom 8.6** (visual target = 2.5D top-down management sim) — realised in 3.1.A–H and the 3.1.S.0–3 sandbox sub-phase.
- **Axiom 8.7** (engine host-agnostic; telemetry build-conditional) — Unity host integration validated; `AsciiMapProjector` demonstrates the pattern from a different angle (pure C# projector, `#if WARDEN`-gated, strips at ship). The two-world boundary held under load.

### 2.7 Process changes (load-bearing)

Phase 3 produced more durable process artifacts than any prior phase. The next Opus must read these:

- **`docs/UNITY-PACKET-PROTOCOL.md`** — sandbox-first, atomic, prefab-driven Unity workflow. **Mandatory** for every Track 2 packet from now on. Established in response to the 3.1.x bundle incident; validated by seven clean shipments on the new protocol.
- **`docs/PHASE-3-REALITY-CHECK.md`** — Track 1 / Track 2 split rationale; corrects the stale Phase 4 brief preamble; the audit trail of what actually shipped vs what the brief asserted.
- **`docs/c2-infrastructure/work-packets/_PACKET-COMPLETION-PROTOCOL.md`** — canonical footer text for Track 1 and Track 2 packets; **Dispatch protocol** section (worktree-per-packet rule, branch naming, retirement steps); self-cleanup-on-merge convention. Inlined verbatim into every spec going forward.
- **`docs/wiki/06-cli-reference.md`** — comprehensive ECSCli reference. Talon's day-to-day operational doc.
- **`docs/known-bugs.md`** — deferred-bug ledger. **BUG-001** (prop-on-prop displacement) is the only entry; rooted in surface-height stacking; deferred to "build mode v2" alongside multi-prop stacking + footprint tracking + undo/redo. The doc shape is the right one for tracking deferred work going forward.

### 2.8 Content artifacts added

- **`docs/c2-content/ux-ui-bible.md` v0.1** — six axioms (ghost-camera, diegesis, mom-can-play, layered disclosure, simulation-rhythm-stress, iconography). Three open questions deferred to v0.2 — see §7.
- **`docs/c2-content/aesthetic-bible.md`** — extended with selection-cue candidates (halo+outline default vs CRT-blinking-box playtest alternative); chibi-anime emotion-cue family (anger lines, embarrassment red-face, nausea green-face, sweat drops, sleep-Zs, hearts, sparkles); environmental signal family (stink lines, green fog, dust motes, light beams, buzz lines).
- **`docs/c2-content/cast-bible.md`** — silhouette catalog locked in; per-archetype distinguishing marks documented.
- **`docs/c2-content/dialog-bible.md`** — gibberish phoneme model framed for future per-archetype voice profile work (Phase 4.1.0).

### 2.9 Warden-side tooling added

The `Warden.*` ecosystem grew significantly:

- **`Warden.Telemetry/AsciiMap/AsciiMapProjector.cs`** — pure C# Unicode-box-drawing floor-plan projector. Public API: `Render(WorldStateDto, AsciiMapOptions)`. Strips at ship via `#if WARDEN`.
- **`ECSCli/World/WorldMapCommand.cs`** — `ECSCli world map [options]` verb. Documented in `docs/wiki/06-cli-reference.md`.
- **`Warden.Orchestrator/Prompts/MapSlabFactory.cs`** — Stable/Volatile slab split for Haiku/Sonnet validation prompts. Token-economy win: ~28 800 input tokens saved per 25-scenario Haiku batch via stable-slab caching.
- **`OpusSpecPacket.SpatialContext`** flag — opt-in for ASCII-map injection. Schema additive minor (`opus-to-sonnet.schema.json` v0.5.0 → v0.5.1).

---

## 3. Architectural decisions — Phase 3's discoveries

### 3.1 The 3.1.x bundle incident — and what it taught

Phase 3.1.A–H shipped as an eight-packet Sonnet bundle. Every xUnit test passed. `dotnet build` was clean. The 30-NPC FPS gate held. The Unity scene rendered nothing where Talon expected something. Six follow-up "Phase 3.1.x fix" commits were required to make the scene visually correct: missing renderers in the committed scene, axis confusion (`Position.Y` vs `Position.Z`), sub-pixel dot sizing, camera altitude wrong for the 60-tile world, missing transitive STJ dependencies in `Plugins/`, missing scripting-define references.

**Why xUnit didn't catch it:** xUnit verifies ECS contracts (entity counts, component values, transition logic). It cannot see Inspector wiring, scene file contents, axis conventions, world-unit scale, camera framing, or whether *anything is on screen*. The Sonnet shipped against tests that couldn't catch the divergence.

**The structural fix:** the sandbox-first Unity workflow (`docs/UNITY-PACKET-PROTOCOL.md`). One feature, one prefab, one isolated `_Sandbox/*.unity` scene, a 5-minute Inspector-pass test recipe by Talon, integration is its own follow-up packet. Validated by WP-3.1.S.0 through S.3 plus their `-INT` counterparts: all seven shipped clean on the new protocol with at most one minor question per packet, no fix commits required after merge.

**The lesson generalises:** when the verification harness can't observe the failure mode, the harness is wrong, not the test discipline. The sandbox protocol is the verification harness for visual work. Future phases will face analogous "tests pass but reality is broken" classes of bug; the right response is to upgrade the harness, not to add more tests of the wrong kind.

### 3.2 The Track 1 / Track 2 parallel split

Phase 3 was the first phase that ran two tracks in parallel without losing the dependency thread. Track 1 (engine, headless, no visual gate) and Track 2 (Unity, sandbox-first) shared zero file surface beyond the predictable `SimulationBootstrapper.cs` / `SimConfig.json` / additive-enum keep-both tax. At peak, six Sonnets dispatched concurrently across both tracks (Wave 4: 3.2.0, 3.2.1, 3.2.3, 3.2.4 on Track 1 + S.2-INT, S.3 on Track 2). Talon's "parallelism by default" preference held throughout.

**The discipline that made it work:** explicit per-packet dependency declarations in the spec header (`Depends on:`, `Parallel-safe with:`) plus the **DO NOT DISPATCH UNTIL X IS MERGED** prefix line on packets with hard wave dependencies. The line gets read by the Sonnet at the start of every dispatch; it cannot be missed.

### 3.3 Worktree-per-packet — formalised mid-phase

The worktree-per-packet convention existed informally before Phase 3 but wasn't documented. After several Sonnets at peak parallelism, Talon needed visual clarity on what was in flight. The rule was formalised in `_PACKET-COMPLETION-PROTOCOL.md`'s **Dispatch protocol** section: one packet, one worktree at `.claude/worktrees/sonnet-wp-<id>/`, one branch named `sonnet-wp-<id>`. Sonnet executors run a step-0 worktree pre-flight check before doing any implementation work. Retirement on merge is two commands.

The rule pays dividends in three ways: (1) `git worktree list` shows exactly what's in flight at a glance; (2) testing is isolated per worktree (Unity Editor on one, xUnit on another, no contention); (3) blow-away is clean.

### 3.4 ASCII map as shared spatial substrate

Pre-3.0.W, "where is X" conversations between Talon, Sonnets, and Haikus involved JSON `position: {x, y, z}` floats and mental geometry. The `AsciiMapProjector` replaced that with a single Unicode-box-drawing floor plan that all three reasoners read directly. The Stable/Volatile slab split (Phase 3.0.W.1) made it cache-friendly: stable walls/rooms/furniture pay tokens once per Haiku batch, NPC positions and hazards re-render uncached per scenario. The token economy works out to ~$0.50 saved per balance-tuning Haiku batch.

This is a pattern worth keeping. Whenever a reasoning-heavy verification task involves spatial state, generate a text artifact that humans and models can both consume natively. JSON dumps are for machines; the dev-time observability surface should be readable.

### 3.5 The challenge surface is NPC-driven (confirmed)

The chore rotation packet (3.2.3) shipped as the canonical NPC-driven challenge example. Donna's per-archetype acceptance bias against microwave duty produces observable refusal-cascade behaviour: she grumbles, mood drops, chores accumulate, the office knows. The pattern is the template every future emergent-gameplay packet (plague week, PIP arc, fire) will follow: per-archetype acceptance/competence biasing, persistent memory of refusals, cascade through stress / mood / relationship. Talon's project-shaping decision that "challenge comes from NPCs, not task overload" is mechanised, not just documented.

### 3.6 `IWorldMutationApi` as the structural-mutation contract

Build mode (3.1.D / S.2-INT), runtime hazard spawn (3.0.3 stains), the dev console (3.1.H), and the future plague-week / fire packets all flow structural changes through `IWorldMutationApi`. The `StructuralChangeBus` invalidates the path cache on emit; consumers re-path naturally on next tick. No system pokes at topology internals; no system rebuilds path obstacles per-call. The 30-NPCs-at-60-FPS gate held throughout 3.1.x and 3.2.x because of this.

### 3.7 `LifeStateComponent` three-state machine (and the recovery path)

`Alive ⇄ Incapacitated → Deceased` is the canonical state machine. Recovery (Incapacitated → Alive) is real and exercised by:

- **Fainting (3.0.6)** — alone-recovery: faint, lie there, consciousness returns over time.
- **Rescue (3.2.4)** — bystander-recovery: Heimlich for choking, CPR for cardiac, door-unlock for lockout. Per-archetype rescue bias determines who steps in first.

Future Incapacitated states (concussion, seizure, hypoglycemia, panic-attack, heat-stroke) extend the machine without revisiting the contract. The bibles' "honest depiction of mature themes" commitment lives here — the rescue mechanic is the world's compassion surface, the counterweight to the death scenarios.

---

## 4. Phase 3 packet inventory

For audit. Twenty-three packets total across four sub-tracks.

### 4.1 Track 1 — Engine substrate (3.0.x + 3.0.W)

| ID | Title | Status |
|---|---|---|
| WP-3.0.0 | LifeStateComponent + cause-of-death events | Merged |
| WP-3.0.1 | Choking-on-food scenario | Merged |
| WP-3.0.2 | Deceased-entity handling + bereavement | Merged |
| WP-3.0.3 | Slip-and-fall + locked-in-and-starved | Merged |
| WP-3.0.4 | Live-mutation hardening (`IWorldMutationApi`) | Merged |
| WP-3.0.5 | `ComponentStore<T>` typed-array refactor | Merged |
| WP-3.0.6 | Fainting (Incapacitated → Alive recovery) | Merged |
| WP-3.0.W | ASCII Floor-Plan Projector | Merged |
| WP-3.0.W.1 | Haiku Prompt ASCII-Map Integration | Merged |

### 4.2 Track 2 — Unity scaffolding bundle (deprecated approach)

| ID | Title | Status |
|---|---|---|
| WP-3.1.A–H | Unity scaffold bundle (8 packets) | Merged with 6 follow-up fix commits — see §3.1 |

### 4.3 Track 2 — Sandbox protocol re-do

| ID | Title | Status |
|---|---|---|
| WP-3.1.S.0 | Camera rig sandbox | Merged |
| WP-3.1.S.0-INT | CameraRig.prefab into MainScene | Merged |
| WP-3.1.S.1 | Selection outline sandbox | Merged |
| WP-3.1.S.1-INT | Selection into NpcDotRenderer | Merged |
| WP-3.1.S.2 | Draggable prop sandbox | Merged |
| WP-3.1.S.2-INT | DraggableProp into BuildMode | Merged with **BUG-001** deferred — see `docs/known-bugs.md` |
| WP-3.1.S.3 | Inspector popup sandbox | Merged |
| WP-3.1.S.3-INT | Popup into selection (first end-to-end DTO→UI binding) | Merged |

### 4.4 Track 1 — Gameplay deepening (3.2.x)

| ID | Title | Status |
|---|---|---|
| WP-3.2.0 | Save/load round-trip hardening (schema v0.5) | Merged |
| WP-3.2.1 | Sound trigger bus | Merged |
| WP-3.2.2 | Rudimentary physics | Merged |
| WP-3.2.3 | Chore rotation system | Merged |
| WP-3.2.4 | Rescue mechanic | Merged |
| WP-3.2.5 | Per-archetype tuning JSONs | Merged |
| WP-3.2.6 | Silhouette animation state expansion | Merged |

---

## 5. Performance gate

The 30-NPCs-at-60-FPS gate is the single-most-load-bearing performance commitment of Phase 3. It held throughout, confirmed at multiple checkpoints:

- **After 3.0.5 (`ComponentStore<T>` refactor):** 88% allocation reduction (~12 KB/tick), 10–20× faster `Get<T>()`, no entity-component test regressions.
- **After 3.1.A–H (Unity scaffold):** 30 NPC dots animating at 60 FPS in the Unity profiler.
- **After 3.1.S.* sandbox protocol packets:** each sandbox scene profiled; main scene profiled after each `-INT` merge. No frame-time regression at any point.
- **After 3.2.x (sound bus + physics + chores + rescue):** still holding. Sound triggers dispatch in O(subscribers) per emit; physics is event-driven, no per-tick solver; chore rotation is once-per-game-day; rescue is proximity-gated.

**Phase 4 packets must continue to honor the gate.** If a Phase 4 visualization upgrade (e.g., the pixel-art-from-3D shader pipeline) breaks the gate, the upgrade is wrong, not the gate.

---

## 6. Schema state — v0.5

Current schema files (under `Warden.Contracts/SchemaValidation/`):

- `world-state.schema.json` — v0.5.0, life-state surface added.
- `opus-to-sonnet.schema.json` — v0.5.1, `SpatialContext` opt-in flag added.
- `sonnet-result.schema.json` — v0.5.0.
- `sonnet-to-haiku.schema.json` — v0.5.0.
- `haiku-result.schema.json` — v0.5.0.
- `ai-command-batch.schema.json` — v0.5.0.

Phase 4 will likely bump:

- **v0.6.0** for multi-floor topology (4.0.1) — `entities[].position.floor` becomes required; `rooms[].floor` enum gains validation.
- **v0.7.0** for player-embodiment surface (4.3.0) — depends on UX bible v0.2 Q5 resolution.

Per the `SCHEMA-ROADMAP.md` rules: additive minors only unless absolutely necessary. The orchestrator validates `schemaVersion` on every cross-tier message; major mismatches are refused, minor mismatches log a warning.

---

## 7. Phase 4 backlog — the punch list

The Phase 4 brief's sub-phase plan (4.0 → 4.4) remains the long-horizon roadmap. The brief's preamble was wrong about Phase 3 closure state at the time of writing; with this handoff in place, the brief becomes actionable. **Phase 4 dispatches in this order:**

### 7.1 Phase 4.0.x — UX bible v0.2, multi-floor, shader pipeline

1. **WP-4.0.0 — UX/UI bible v0.2.** Resolves Q4, Q5, Q6 from v0.1. Talon authors; Opus critiques. **First Phase 4 packet.** Bible-only, no code.
2. **WP-4.0.1 — Multi-floor topology.** Engine extends `RoomComponent` and `PathfindingService` for multi-floor. Camera adds floor-switch verb (D-pad up / Q / E + smooth float-out-and-back per the world bible). Stairwells become topology-traversable.
3. **WP-4.0.2 — Pixel-art-from-3D shader pipeline.** Aesthetic bible's render commitment: low-poly 3D underlying geometry, pixel-art shader at output, real shadows from real light. Couples to URP migration (or stays on built-in if URP regresses the 30-FPS gate).

### 7.2 Phase 4.0.x — Build mode v2 (BUG-001 home)

The deferred prop-on-prop displacement bug from S.2-INT belongs here. Scope: proper footprint tracking, multi-prop stacking rules, undo/redo, surface-height resolution that handles small-on-big and big-on-small correctly. Likely shipped as 2–3 sub-packets paralleling 4.0.1 / 4.0.2.

### 7.3 Phase 4.1.x — Audio + voice profiles + content tuning

4. **WP-4.1.0 — Per-archetype voice profile generation.** Sims-gibberish phoneme profile per archetype.
5. **WP-4.1.1 — Ambient soundscape + music.**
6. **WP-4.1.2 — Final art pipeline integration.** External (Talon's collaborator), outside the Sonnet flow.

### 7.4 Phase 4.2.x — Emergent gameplay deepening

7. **WP-4.2.0 — Plague week.** Communicable-illness scenario; transmission per proximity; productivity collapses by day 5.
8. **WP-4.2.1 — PIP arc.** Performance Improvement Plan; HR-threshold flagging; long-arc gameplay.
9. **WP-4.2.2 — Fire / disaster.** Structural emergency; evacuation; office reorganises around damage. Test of `IWorldMutationApi` + `StructuralChangeBus` at scale.
10. **WP-4.2.3 — Affair detection mechanic.** Carefully-scoped; mature theme.

### 7.5 Phase 4.3.x — Player verb expansion

After UX bible v0.2 lands.

11. **WP-4.3.0 — Player embodiment surface.** Per Q5 resolution (manager's office overlay leaning).
12. **WP-4.3.1 — Soft-suggestions verb.** Per Q6 resolution.
13. **WP-4.3.2 — Schedule editing.**

### 7.6 Phase 4.4.x — Hardcore mode + ship prep

14. **WP-4.4.0 — Hardcore mode.** Time-limited off-hours build window per UX bible §5.3.
15. **WP-4.4.1 — Final balance + tuning passes.** Haiku batch validations across archetype-bias matrices. Cast-validate-pattern from Phase 2 is the model.
16. **WP-4.4.2 — Tutorial / first-launch experience.** Per UX bible v0.2 position.

---

## 8. Open questions for Talon (carried forward + new)

From v0.1 of the UX bible, plus new questions raised during Phase 3:

### Carried from prior phases

- **World bible:** what does "the company" actually do? Industry, weekly cadence, calendar-mattering events.
- **Cast bible:** archetype layering cap (2 vs 3); vocabulary register completeness.
- **Action-gating bible:** willpower per-domain vs single-global; inhibition install during play; awareness transitions; conflict between inhibitions; player visibility.
- **Aesthetic bible:** building orientation fixed or parameterised; NPC noticeability separate from raw proximity.
- **Dialog bible:** corpus scope at v0.1; off-corpus action when no fragment matches; mask-slip dialog calcify behaviour.

### UX/UI bible v0.1 deferrals (resolve in v0.2 = Phase 4.0.0)

- **Q4 — Notification carrier model + the gameplay-loop / challenge-surface decision.** Talon's framing: NPC-driven challenge, sparse diegetic notifications. v0.2 commits to specific carrier surfaces (phone ringing, fax tray filling, email indicator on a CRT) and notification volume curve.
- **Q5 — Player embodiment.** Pure ghost-camera vs manager's-office overlay vs manager NPC. Manager's-office overlay is the leaning per the existing world-bible commitment that the player has a desk.
- **Q6 — Direct NPC intervention scope.** None vs soft suggestions vs direct commands. Soft suggestions is the leaning per the "mom can play" axiom — a player nudge, not a strafing command.

### New (Phase 3)

- **Selection cue final form.** v0.1 ships halo+outline default with CRT-blinking-box as a playtest alternative. Decision is post-playtest.
- **Sound triggers — synthesis routing in Unity.** Engine emits `SoundTriggerKind`; Unity host synthesises. v0.1 has a stub synth; production routing belongs in 4.1.0.
- **Animation state extension policy.** Where do `Sleeping`, `Vomiting`, `Mourning`, `Conspiring` slot in? Currently 6 states from 3.2.6; the bibles imply ~12 will be needed.
- **Per-archetype tuning vs per-NPC-instance tuning.** 3.2.5 lands per-archetype JSONs. Future need for per-NPC overrides (Donna specifically chokes more often than other Climbers)? If yes, where does that live — JSON, save state, or content-bible?

The next Opus surfaces these when the work depends on them. Don't volunteer answers; ask Talon at the right moment.

---

## 9. Operational lessons learned (process, not code)

From running Phase 3's 23-packet flow:

- **Bundles are forbidden.** The 3.1.A–H bundle's six follow-up fix commits is the cautionary tale. One feature, one packet, one worktree. The cost of one extra packet round is a fraction of the cost of one missed Inspector wiring.
- **Sandbox-first works.** Seven Track 2 packets shipped clean on the new protocol with at most one minor question per packet. The 5-minute Editor pass by Talon is the verification harness xUnit cannot be.
- **Worktree-per-packet pays for itself fast.** Visibility on what's in flight, isolated testing, clean retirement. The rule was informal pre-Phase 3; now formal in `_PACKET-COMPLETION-PROTOCOL.md`. Any Phase 4 packet that violates it is a process bug.
- **Spec drift is cheap to catch and cheap to fix.** S.1's coord drift (`(0, 0.5, 0)` vs `(28, 0.5, 18)`), missing SandboxSceneGuard requirement in S.2/S.3 — caught in Sonnet questions, fixed in a single doc PR before downstream dispatch. The pattern (Sonnet asks four questions, Opus answers + amends specs in flight) is healthy.
- **Self-cleanup-on-merge worked partially.** Several merged packets did not delete their spec from `docs/c2-infrastructure/work-packets/` per the protocol. The cleanup is a one-pass Haiku task — see `docs/c2-infrastructure/HAIKU-CLEANUP-WORK-ITEM.md`.
- **Real-API smoke after major merges.** Cheap ($0.13 per cache-cold smoke). Phase 3 did this less consistently than Phase 2; Phase 4 should restore the discipline post-each-wave.
- **Talon's "parallelism by default" is the right default.** Six concurrent Sonnets at peak Wave 4 produced clean shipments. The merge tax (`SimulationBootstrapper.cs`, `SimConfig.json`, additive enums — keep both) is real but cheap. The throughput gain dominates.
- **The do-over is a real strategy, not a failure mode.** When the 3.1.x bundle was shipping Inspector wiring bugs and `WorldStateProjector` axis confusion, the right move was not "add more tests" but "replan the protocol." The replan delivered.

---

## 10. Recommended first move for the next Opus

Read this handoff, the SRD (especially §8 — including 8.6 and 8.7), the bibles, `docs/PHASE-4-KICKOFF-BRIEF.md`, the v0.1 UX/UI bible, and `docs/UNITY-PACKET-PROTOCOL.md`. Then await Talon's draft of UX/UI bible v0.2.

When the v0.2 draft lands, **author `WP-4.0.0 — UX/UI bible v0.2 critique packet`** as the first Phase 4 packet. Suggested shape:

- **Goal:** structured critique of v0.2 against the six v0.1 axioms; verify Q4/Q5/Q6 resolutions are coherent; surface load-bearing implications for 4.0.1 (multi-floor) and 4.0.2 (shader pipeline) and 4.3.x (player verbs).
- **Non-goals:** rewriting the bible; arguing against axiom changes Talon committed; pre-empting the player-verb expansion design.
- **Acceptance:** Talon merges v0.2 with explicit responses to the critique points; the next packet (4.0.1 multi-floor) can author its spec against a stable v0.2 surface.

After v0.2 lands and 4.0.0 closes, dispatch 4.0.1 (multi-floor) and 4.0.2 (shader pipeline) in parallel — disjoint surfaces, classic two-track shape.

Build mode v2 (the BUG-001 home) slots into 4.0.x in parallel with 4.0.1 / 4.0.2. Author its spec when those two have stable acceptance criteria.

---

## 11. What you don't have to worry about

Things Phase 3 settled that the next Opus should treat as load-bearing:

- **The Unity host is real and stable.** `APIFramework.dll` loads in-process; engine ticks in the editor; `WorldStateDto` projection consumed per frame; the 30-NPCs-at-60-FPS gate holds. Don't propose alternative hosts. Don't propose abandoning Unity.
- **`ComponentStore<T>` is the storage substrate.** `Dictionary<Type, object>` boxing is gone. Public API unchanged at call sites. Don't re-litigate the storage layer.
- **`IWorldMutationApi` is the structural-mutation contract.** Build mode, dev console, future plague-week / fire packets — all flow through it.
- **`LifeStateComponent` three-state machine.** Alive ⇄ Incapacitated → Deceased. Recovery path is real and exercised by fainting + rescue. Future Incapacitated states extend the machine without revisiting the contract.
- **`SoundTriggerBus` is the audio seam.** Engine emits triggers; host synthesises. Don't propose engine-side audio.
- **Save/load round-trips at v0.5.** Every component family. Mid-state saves load correctly. Phase 4 packets can rely on this.
- **The chore rotation packet is the canonical NPC-driven-challenge example.** Plague week, PIP arc, fire, affair detection — all follow its pattern.
- **The 30-NPCs-at-60-FPS gate is non-negotiable.** Every Phase 3 packet preserved it. Phase 4 packets must too.
- **The sandbox protocol is non-negotiable for Track 2 work.** No bundles. No exceptions.
- **The worktree-per-packet rule is non-negotiable.** No reuse. No sharing.
- **The Phase 4 brief preamble is stale on Phase 3 closure state.** This handoff supersedes it. The brief's sub-phase plan (4.0 → 4.4) remains the long-horizon roadmap.

---

## 12. Goodbye from Phase 3 Opus

Twenty-three packets shipped. The do-over track from the 3.1.x bundle incident validated the sandbox protocol with seven clean shipments in a row. The engine substrate is honest about death now — choking, slipping, locked-in-and-starved, fainting, rescue — and the world is honest about the consequences with bereavement cascades that propagate through relationship memory. The chore rotation system is the proof of concept that the bibles' "challenge from NPCs, not task overload" commitment is mechanisable. The ASCII map projector is the unexpected MVP — a 90-minute Warden-side packet that pays dividends every time anyone (human or model) needs to reason about where something is.

Three pieces of meta-advice for the next Opus:

- **Talon authors the UX/UI bible v0.2 — don't pre-empt.** Phase 1 Opus learned this with the cast bible; Phase 3 Opus learned this with the v0.1 ship. When the v0.2 draft lands, critique it as an architect would critique a peer's design doc; the gameplay decisions are Talon's, the architectural implications are yours.
- **The sandbox protocol earned the right to keep working.** Don't relax it for "small" Unity changes. Small Unity changes that look small from a code perspective are the ones that miss Inspector wiring, and Inspector wiring is the failure surface xUnit cannot see.
- **Trust the worktree-per-packet rule.** It looks like overhead. It is the only reason six concurrent Sonnets could ship clean in Wave 4 without merge chaos. The first time you're tempted to "just reuse this worktree," that's the test of the rule. It earns its discipline back the moment a parallel dispatch hits.

Phase 4 begins now. Good luck.
