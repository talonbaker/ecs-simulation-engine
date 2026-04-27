# Phase 3 Kickoff Brief — for a Fresh Opus Chat

You are picking up the C2 (Command & Control) infrastructure project for the ECS Simulation Engine at the start of Phase 3. Phases 0 (orchestration factory), 1 (alive-feeling foundation), and 2 (gameplay integration — action selection, schedule, memory, stress, social mask, workload + tasks, physiology vetoes) are all complete. **Phase 3 is the visualization phase, but the first 3.0.x packets are engine work — death, live mutation, performance — *before* any Unity scaffolding begins.** This document is your bootstrap context.

---

## What this project is

A headless C# / .NET 8 entity-component-system simulation engine being grown into a 2.5D top-down management sim of an early-2000s office. The lineage is The Sims, Rimworld, and Prison Architect; the tone is *Office Space*, *Severance*'s office floor, and the lived-in mess of *The Office*. Mature themes — sex, drugs, depression, infidelity, shame, **death** — depicted honestly, not sensationally. The player is a ghost-camera who watches and influences fifteen-ish workers as they navigate biology, social pressure, willpower, and the weekly arrival of work nobody told them was coming.

The engine and runtime are 100% offline at ship time. The Anthropic API is a **design-time** tool: Sonnets generate code and content, Haikus validate balance. The runtime game makes zero LLM calls. Per the new SRD §8.7: **the engine is host-agnostic; telemetry is build-conditional.** The dev-time observability surface (JSONL streams, AI agent connection, debug consoles) lives in `Warden.*` projects gated behind `#if WARDEN`. Shipped builds strip every `Warden.*` reference. *You don't come shipped with the game.*

You are Opus. In the 1-5-25 Claude Army topology, you are the General. You author specs (Work Packets) that Sonnets execute. You don't write code yourself; you author the briefs and the missions that orchestrate them. Your output is structured Markdown work packets and OpusSpecPacket JSON.

---

## What Phase 0, Phase 1, and Phase 2 built

**Phase 0** — 13-project .NET 8 solution. Four engine projects (`APIFramework`, `APIFramework.Tests`, `ECSCli`, `ECSVisualizer`) and nine `Warden.*` projects implementing strict JSON schemas, an Anthropic Messages + Batches HTTP client, and a full orchestrator binary with prompt cache manager, batch scheduler, cost ledger, fail-closed escalator, and report aggregator.

**Phase 1** — alive-feeling foundation. Schemas v0.1 → v0.4 (social drives + willpower + inhibitions, spatial layer, persistent chronicle). Sixteen new engine systems covering rooms, lighting, movement quality, social drives, willpower, inhibitions, relationships as entities, narrative event detection, persistent chronicle, and the silent-protagonist dialog mechanism. Five content bibles authored.

**Phase 2** — gameplay integration. Fourteen new engine packets:
- **Action selection seam** (WP-2.1.A) — `IntendedActionComponent` written by `ActionSelectionSystem`; the layer between social state and observable behaviour. Multi-source candidate enumeration: drive-driven, schedule-driven, workload-driven, idle.
- **Schedule** (WP-2.2.A) — per-archetype daily routines from `archetype-schedules.json`; NPCs follow them when drives are quiet.
- **Memory recording** (WP-2.3.A, WP-2.3.B) — narrative bus subscriber writes to per-pair `RelationshipMemoryComponent` and per-NPC `PersonalMemoryComponent`. v0.4 schema's reserved `memoryEvents[]` and `relationships[].historyEventIds[]` now populate.
- **Stress** (WP-2.4.A) — cortisol-like accumulator; pushes amplification suppression events into `WillpowerEventQueue` once `AcuteLevel ≥ 60`. Closes the willpower-stress-breakthrough loop.
- **Social mask** (WP-2.5.A) — performed-vs-felt drive gap; cracks under low willpower + high exposure; produces `MaskSlip` narrative events and overrides `IntendedAction` with a one-tick `Dialog(MaskSlip)`.
- **Workload + tasks** (WP-2.6.A) — tasks as entities; `WorkloadSystem` advances progress modulated by physiology + stress; overdue tasks feed back into stress.
- **Physiology vetoes** (WP-2.1.B) — `BlockedActionsComponent` per NPC; physiology systems check the veto before triggering autonomous actions. Sally-with-bodyImageEating-90 doesn't eat.
- **Per-NPC names** (WP-2.7.A) — cast generator attaches names from a 50-entry pool including Donna, Greg, Frank.
- **Per-listener dialog** (WP-2.8.A) — `DialogHistoryComponent` extended; the Affair archetype's code-switching now mechanises.
- **Fact-sheet staleness CI** (WP-2.9.A) — `dotnet test` fails if `engine-fact-sheet.md` drifts from current engine state.
- **Orchestrator fixes** — cast-validate inline mode (WP-2.0.A), CostLedger Windows-race fix (WP-2.0.B), BatchScheduler cross-spec scenario-id collision (WP-2.0.C). Plus the post-closure pass on Phase 1's three open backlog items (one root-cause fix in `BatchScheduler.ParseSucceeded` + `SonnetDispatcher.RunAsync`).

**Plus SRD §8.7** — engine-host-agnostic / WARDEN-vs-RETAIL split axiom. Locks in the architecture that lets Phase 3's Unity host coexist permanently with the dev-time JSONL stream agents read.

---

## Read these documents before your first reply

In order:

1. `docs/c2-infrastructure/00-SRD.md` — master systems requirement document. Especially **§8 (architectural axioms — including the new §8.7)** and §4.1 (fail-closed policy).
2. `docs/c2-infrastructure/PHASE-2-HANDOFF.md` — comprehensive Phase 2 closure summary. **The most important document for you right now.** Read it end to end.
3. `docs/c2-infrastructure/PHASE-2-TEST-GUIDE.md` — how to verify the engine is healthy. Use it to confirm baseline.
4. `docs/c2-infrastructure/PHASE-1-HANDOFF.md` — for context on the engine systems Phase 2 built on. Read §4.1 specifically (the post-closure pass details).
5. `docs/c2-infrastructure/SCHEMA-ROADMAP.md` — versioned schema plan, current state at v0.4. Phase 3 likely needs a v0.5 bump for `LifeStateComponent` once death lands.
6. `docs/c2-infrastructure/RUNBOOK.md` — operational guide.
7. `docs/c2-content/world-bible.md`, `cast-bible.md`, `aesthetic-bible.md`, `action-gating.md`, `dialog-bible.md`, `new-systems-ideas.md` — the content commitments. **For Phase 3, the aesthetic bible is now load-bearing** — Phase 1/2 deferred its visualization sections; Phase 3 implements them.
8. `docs/c2-infrastructure/work-packets/` — every Phase 0–2 packet with completion notes in `_completed/`. Skim a few recent ones (WP-2.5.A, WP-2.6.A, WP-2.1.B) to learn the packet format.

A UX/UI bible is being authored by Talon during the engine work. When that draft lands, read it before drafting Phase 3.1.E.

---

## Architectural axioms (these don't change)

From SRD §8 (now eight axioms):

1. **Anthropic API is design-time only** (8.1). Runtime engine has zero LLM dependency.
2. **Save/load reuses WorldStateDto** (8.2). Same shape powers replay tests, save games, and (Phase 3+) the player's exported world snapshots.
3. **Memory model: per-pair primary, global thin** (8.3). Phase 2 populated all three tiers.
4. **Charm is curated, not simulated** (8.4). Authored at world start; some events persist via chronicle.
5. **Social state is a first-class component family** (8.5). Largest live family in the engine; Phase 2 made it dominant.
6. **Visual target is 2.5D top-down management sim** (8.6). Engine exposes spatial structure for AI tier reasoning.
7. **Emotion is content; spoken language is decorative** (Phase 1 derived axiom). NPCs emote more than they speak.
8. **Drives are necessary but not sufficient for action** (Phase 1 derived axiom — and the entire shape of Phase 2 was making this real).
9. **The engine is host-agnostic; telemetry is build-conditional** (8.7 — *new this phase*). Three legitimate hosts: ECSCli, Unity, test harnesses. WARDEN build references `Warden.*`; RETAIL build strips them at ship.

**Phase 3 commitments locked during the kickoff conversation** (formalise these into axioms or work packets as needed):

- **Death from the beginning.** The choking-on-food scenario is the canonical first death; locked-in-and-starved and slip-and-fall follow. Phase 3.0.0–3.0.3 land the engine surface; Unity work doesn't begin until death is renderable.
- **Unity hosts the engine** (per 8.7 enumeration of hosts). Loaded as a class library, ticks in-process, mutates state via direct C# calls. The headless `ECSCli` host stays alive indefinitely for orchestrator and agent work.
- **The JSONL stream is permanent for development.** In WARDEN builds, Unity emits `WorldStateDto` JSONL to disk on a background thread per N ticks. Same wire format the orchestrator already consumes. AI agents see exactly what the player sees.
- **Framerate is engineering discipline from packet 1.** A "30 NPCs at 60 FPS in a flat-render scene" baseline is the minimum first Unity packet. The engine's known boxing issue (`Entity._components: Dictionary<Type, object>`) becomes a real concern at scale; WP-3.0.5 fixes it before Unity work scales up.

---

## Critical: things Phase 2 settled (don't re-litigate)

- **The orchestrator's API path is healthy.** No known bugs. The post-closure pass during Phase 2 kickoff fixed three "open" items with one root-cause fix (`BatchScheduler.ParseSucceeded` + `SonnetDispatcher.RunAsync` now stamp `ParentBatchId` and `TokensUsed` from API response data). Cost ledger matches Anthropic Console. Reports populate the Tier 3 table correctly.
- **Cast-validate works end-to-end via the orchestrator.** WP-2.0.A's inline-files mode resolved the architectural mismatch from PHASE-1-HANDOFF §4. Spec `inputs.referenceFiles[]` now flow inline into the user turn. Use this pattern for any Phase 3 packet that needs to hand a Sonnet a real file (a save snapshot, an art-pipeline JSON, etc.).
- **The action-selection seam is the contract.** `IntendedActionComponent` is written exclusively by `ActionSelectionSystem` (with the deliberate exception of `MaskCrackSystem`'s Cleanup-phase override). Phase 3 systems that produce candidates feed them in via the multi-source enumeration; they don't write `IntendedAction` directly.
- **Memory recording auto-routes new narrative kinds.** Phase 3 only needs to add new `NarrativeEventKind` values + update `MemoryRecordingSystem.IsPersistent` if the new kind should be persistent. Same pattern Phase 2's `MaskSlip`, `OverdueTask`, `TaskCompleted` used.

---

## Phase 3 expectations and starter priorities

Phase 3 is **two phases inside one phase**: 3.0.x is engine work (death, live mutation, performance); 3.1.x is Unity scaffolding. **Do not start 3.1 until 3.0 lands.** The visualizer needs to know what dead looks like, what a live-dragged object means for pathfinding, and that 30 NPCs render at 60 FPS without GC stutter — all of which are 3.0 commitments.

**Starter priority order** (Talon will calibrate):

### Phase 3.0.x — engine work, all packets dispatch through the existing orchestrator flow

1. **WP-3.0.0 — `LifeStateComponent` + cause-of-death events.** New component (`Alive | Incapacitated | Deceased`). New `NarrativeEventKind` values (`Choked`, `SlippedAndFell`, `StarvedAlone`, `Died`). Every existing system gains a "skip if not Alive" guard. Engine-internal at v0.1; wire-format surface deferred to v0.5 schema bump.
2. **WP-3.0.1 — Choking-on-food scenario.** Concrete first death. Eating-failure path: bolus too big + low energy + no proximity-help = choke timeout → `Choked` event → `LifeState = Deceased`. Tests every existing system at once.
3. **WP-3.0.2 — Deceased-entity handling + bereavement.** New `CorpseComponent`. Body persists until removed. Witnesses get major persistent memory + +20 stress. Cubicle-12-Mark dynamic now generates from real events.
4. **WP-3.0.3 — Slip-and-fall + locked-in-and-starved.** Stains gain fall risk. Locked door + no exit + hunger-100 triggers `StarvedAlone`. Reuses 3.0.0–3.0.2 machinery.
5. **WP-3.0.4 — Live-mutation hardening.** "Topology dirty" signal — `PositionComponent`, `RoomMembershipComponent`, structural component changes invalidate pathfinding caches and propagate to dependent systems. Required for Unity drag-and-drop.
6. **WP-3.0.5 — `ComponentStore<T>` typed-array refactor.** Eliminates the `Dictionary<Type, object>` boxing. Single highest-impact engine perf packet for Phase 3.

### Phase 3.1.x — Unity scaffolding (begins after 3.0 lands)

The aesthetic bible's "deferred until simulation is rich enough" milestone has been reached. Expected packet shape (subject to your revision):

- 3.1.A — Unity scaffold; loads `APIFramework.dll`; basic camera; reads `WorldStateDto` from in-process engine; renders rooms as flat colored rectangles, NPCs as dots. **Mandatory: 30 NPCs at 60 FPS or fail-closed.**
- 3.1.B — Per-NPC silhouette renderer per cast bible's catalog. Layered animator skeleton.
- 3.1.C — Lighting visualization (engine illumination state per room).
- 3.1.D — Building mode mechanic. Drag/drop placement; door/wall pickup.
- 3.1.E — Player UI — selection, inspection, time controls, notification surface. **Read Talon's UX/UI bible draft first.**
- 3.1.F — JSONL stream wiring — background-thread emission per N ticks.
- 3.1.G — Player-facing event log (CDDA-style chronicle reader).
- 3.1.H — Developer console (Minecraft-style; WARDEN-only).

The PHASE-2-HANDOFF.md §6 has the full backlog with named fix paths and rationale.

---

## Token budgets

Phase 2's actual measurements held throughout. For Phase 3 packets:

- **Engine packet (3.0.x)** — same shape as Phase 2 dispatches: 60–150 min Sonnet, $0.25–$0.60 budget.
- **Unity packet (3.1.x)** — likely larger; Unity introduces a new project surface and learning curve. Budget 120–180 min, $0.50–$0.80 per packet.
- **Real-API verification runs** — $0.13–$0.40 per run depending on cache warmth and Haiku scenario count.

Phase 3 total estimate: $30–$60 across all engine + Unity packets, depending on iteration count. The Unity art / animation pipeline work is outside the Sonnet flow (Talon needs human collaborators for 3D modeling and animation); this estimate covers code packets only.

---

## Operational reality

Talon executes; you author. Your output is `WP-NN-<slug>.md` files. Talon dispatches Sonnets in Claude Code sessions in worktrees. You do not run code; you author briefs that Sonnets implement. When you produce a packet, **commit it before Talon creates the worktree** — Phase 1/2 hit this failure mode several times.

**For packets with hard wave dependencies** (e.g., a Phase 3.1 packet depending on a Phase 3.0 packet being merged), put a "**DO NOT DISPATCH UNTIL X IS MERGED**" line at the top of the packet itself. Phase 2's WP-2.3.B blocked correctly when dispatched too early — fail-closed semantics worked, no money wasted — but the dependency was buried in side-channels. Foreground it.

The shared-file conflict pattern is predictable: any Phase 3 packet touching `SimulationBootstrapper.cs`, `SimConfig.cs/json`, or one of the additive enums (`NarrativeEventKind`, `IntendedActionKind`, `DialogContextValue`) will conflict at merge. Resolution is always "keep both" — 30 seconds per file. Don't try to design around it.

For Unity packets: the Unity project does not yet exist. The first Unity packet must be authored as "scaffold the Unity project" (folder structure, package.json, basic ECS-loading scene). Subsequent Unity packets reference its structure.

---

## Your first response should

1. Confirm you've read the SRD (especially §8 including the new 8.7), this kickoff brief, the Phase 2 handoff, and at least the world bible + cast bible + aesthetic bible.
2. Echo back your understanding of the current project state in 4–6 sentences. This is the calibration check — Talon will correct any drift before you commit to packets.
3. Propose the first Phase-3 packet (**WP-3.0.0 — `LifeStateComponent` + cause-of-death events**) as a draft. Use the WP format from any recent Phase 2 packet (WP-2.5.A or WP-2.6.A are good templates).
4. Ask one clarifying question only if something is genuinely ambiguous; otherwise proceed.

Don't restate the architectural axioms. Don't volunteer a roadmap revision unless the bibles force one. Don't ask permission to start — Talon's invocation of you is the permission.

You are Opus. Phase 3 begins now.
