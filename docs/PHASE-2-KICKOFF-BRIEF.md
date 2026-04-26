# Phase 2 Kickoff Brief — for a Fresh Opus Chat

You are picking up the C2 (Command & Control) infrastructure project for the ECS Simulation Engine at the start of Phase 2. Phase 0 (orchestration factory) and Phase 1 (alive-feeling foundation + content authoring scaffolds) are both complete. This document is your bootstrap context. Read it once, then read the reference docs it points at, then begin.

---

## What this project is

A headless C# / .NET 8 entity-component-system simulation engine being grown into a 2.5D top-down management sim of an early-2000s office. The lineage is The Sims, Rimworld, and Prison Architect; the tone is *Office Space*, *Severance*'s office floor, and the lived-in mess of *The Office*. Mature themes — sex, drugs, depression, infidelity, shame — depicted honestly, not sensationally. The player is a ghost-camera who watches and influences the lives of fifteen-ish workers as they navigate biology, social pressure, willpower, and the weekly arrival of work nobody told them was coming.

The engine and runtime are 100% offline. The Anthropic API is a **design-time** tool: Sonnets generate code and content at build time, Haikus validate balance at build time. The runtime game makes zero LLM calls. (See SRD §8.1 — this axiom is load-bearing.)

You are Opus. In the 1-5-25 Claude Army topology, you are the General. You author specs (Work Packets) that Sonnets execute. You don't write code yourself; you author the briefs Sonnets execute and the missions that orchestrate them. Your output is structured Markdown work packets and OpusSpecPacket JSON.

---

## What Phase 0 built

A 13-project .NET 8 solution. Four original engine projects (`APIFramework`, `APIFramework.Tests`, `ECSCli`, `ECSVisualizer`) and nine new `Warden.*` projects implementing strict JSON schemas for cross-tier handshake, telemetry projection, an Anthropic Messages + Batches HTTP client, and a full orchestrator binary with prompt cache manager (4-slab model), batch scheduler with deduplication, cost ledger with budget guard, fail-closed escalator, and report aggregator. Phase 0's acceptance criteria (SRD §6) are met.

## What Phase 1 built

The "alive-feeling foundation" plus the content scaffolds the next phase will populate. Concretely (everything merged on `staging`):

- **Schemas v0.1 → v0.4** — social drives + willpower + inhibitions (v0.2.1), spatial layer (v0.3), persistent chronicle (v0.4). Plus new schemas for `archetypes.json`, `world-definition.json`, and the dialog `corpus.json`.
- **Engine systems** — spatial index + room membership + proximity events; lighting (sun, apertures, sources, per-room illumination) + lighting-to-drive coupling; movement quality (pathfinding, step-aside, idle jitter, mood-modulated speed, facing direction); social engine (drive dynamics, willpower depletion/regen, relationships as entities); narrative event detector + CLI streaming; persistent chronicle with Stain/BrokenItem manifestations; dialog system (corpus + decision-tree retrieval + calcify mechanism for emergent voice).
- **Content scaffolds** — world bible, cast bible, aesthetic bible, action-gating bible, dialog bible, new-systems-ideas (stress, social mask, workload). Starter `office-starter.json` world definition. `archetypes.json` catalog (10 archetypes from the cast bible). `corpus-starter.json` (~200 hand-authored phrase fragments).
- **Orchestrator real-API path proven** — the orchestrator now actually works end-to-end against the live Anthropic API. Multiple bugs in the Phase-0 prompt cache manager and dispatcher were discovered and fixed during Phase 1 testing.

Sixteen Phase-1 work packets shipped: WP-1.0.A, WP-1.0.A.1, WP-1.0.B, WP-1.1.A, WP-1.2.A, WP-1.2.B, WP-1.3.A, WP-1.4.A, WP-1.4.B, WP-1.5.A, WP-1.6.A, WP-1.7.A, WP-1.8.A, WP-1.9.A, WP-1.10.A, plus the carry-over WP-13. Each has a completion note in `docs/c2-infrastructure/work-packets/_completed/`.

## Read these documents before your first reply

In order:

1. `docs/c2-infrastructure/00-SRD.md` — master systems requirement document. Especially §8 (architectural axioms) and §4.1 (fail-closed policy).
2. `docs/c2-infrastructure/PHASE-1-HANDOFF.md` — comprehensive Phase 1 closure summary. **The most important document for you right now.** Read it end to end.
3. `docs/c2-infrastructure/PHASE-1-TEST-GUIDE.md` — how to verify the system is healthy. Use it to confirm your starting baseline.
4. `docs/c2-infrastructure/SCHEMA-ROADMAP.md` — versioned schema plan, current state at v0.4.
5. `docs/c2-infrastructure/RUNBOOK.md` — operational guide.
6. `docs/c2-content/DRAFT-world-bible.md`, `DRAFT-cast-bible.md`, `DRAFT-aesthetic-bible.md`, `DRAFT-action-gating.md`, `DRAFT-dialog-bible.md`, `DRAFT-new-systems-ideas.md` — the content commitments Phase 2 will build engine systems against.
7. `docs/c2-infrastructure/work-packets/` — every Phase-1 packet (WP-1.0.A through WP-1.10.A) with completion notes in `_completed/`. Read a few to learn the packet format.

---

## Architectural axioms (these don't change)

From SRD §8 plus what Phase 1 confirmed:

1. **Anthropic API is design-time only.** Runtime engine has zero LLM dependency. Phase 1 reaffirmed this when Talon asked about a small runtime LLM for dialog — the answer was no, and the dialog system that shipped is a deterministic decision tree over a hand-authored phrase corpus with a calcify mechanism for emergent voice.
2. **Save/load reuses WorldStateDto.** Replay (deterministic test) and save/load (player's world) are different concepts on the same storage shape.
3. **Memory model: per-pair primary, global thin.** Per-pair memories live on the relationship entity (Phase 2 follow-up — the data shape exists, the recording system doesn't yet); global office-wide events live in the v0.4 chronicle channel (shipped).
4. **Charm is curated, not simulated.** Authored at world start. Some events persist (spilled coffee → Stain entity) via the chronicle channel.
5. **Social state is a first-class component family.** Largest live family in the engine. Drives + willpower + inhibitions + personality + relationships now all real engine components.
6. **Visual target is 2.5D top-down management sim.** Engine exposes spatial structure for AI-tier reasoning about places.

**New axiom landed in Phase 1 (silent-protagonist principle):**

7. **Emotion is content; spoken language is decorative.** Most NPC interactions produce no spoken line at all — body language, position, observable consequence carry the meaning. When NPCs do speak, it's selection from a curated palette under emotional pressure with calcification, not generation. No runtime LLM, no embedding model, no inference. Hand-authored fragments tagged with register + context + valence; decision-tree retrieval; per-NPC fragment-use tracking turns repeated selections into the NPC's signature voice. (See `DRAFT-dialog-bible.md`.)

Plus a derived axiom from action-gating:

8. **Drives are necessary but not sufficient for action.** Willpower depletes under suppression and regenerates with rest; inhibitions are hard blockers (some hidden even from the NPC themselves) that take action classes off the table regardless of drive state. High drive can produce avoidance, not approach, when stakes are high (Bob avoids Donna *because* he likes her). Physiology is overridable by inhibition (Sally at hunger 120% not eating because she thinks she's fat). The data shapes are reserved on the wire and live in engine components; the action-selection layer that consults them is Phase 2 work. (See `DRAFT-action-gating.md`.)

---

## Critical: real-API gotchas Phase 1 discovered

Phase 1's testing discovered six real bugs in the Phase-0 orchestrator that only manifest under real-API conditions (the mock pipeline tested fine in Phase 0). All are fixed on `staging` now. Before you dispatch any Sonnet against the orchestrator, **be aware that this category of issue exists** and that any change to the prompt cache manager, dispatcher, or fail-closed escalator should be validated against real-API runs, not just the mock path.

The six bugs (each fixed; documented for awareness):

1. `PromptCacheManager` was constructed with the parameterless test-mode constructor in real-API mode, producing empty `cache_control` text blocks that Anthropic's API rejects with 400 (`cache_control cannot be set for empty text blocks`). Fixed: real-API mode now wires `CachedPrefixSource` properly, and the cache manager defensively skips empty slabs.
2. The shared role frame was Sonnet-flavored — Haikus inherited it and produced SonnetResult-shaped responses that failed `HaikuResult` schema validation. Fixed: `CachedPrefixSource` now exposes separate Sonnet and Haiku role frames, and `PromptCacheManager.BuildRequest` selects based on the model parameter.
3. Sonnet (and Haiku) responses were parsed as raw JSON, but the model wrapped its JSON in markdown narration (`I'll execute this... ```json ... ``` Done!`). Fixed: defensive `ExtractJsonObject` strips prose and code-fence wrapping before deserialization.
4. Haiku result files were persisted to `runs/<runId>/<runId>/haiku-NN/...` (doubled runId) because `RunCommand` passed `runDir` to `InfraCoT` instead of `root`. Fixed.
5. `FailClosedEscalator.ApplyStateMachine` had no branch for `BlockReason.MissingReferenceFile`, so an entire mission run crashed with `InvalidOperationException` when a Sonnet correctly returned that blocked outcome. Fixed: explicit branch + catch-all for any future `BlockReason` enum addition.
6. Cost ledger Haiku entries show 0 tokens because the orchestrator never overwrites the placeholder `tokensUsed` block in `HaikuResult` with the real values from the batch API response headers. **Not yet fixed — it's on the Phase 2 backlog (PHASE-1-HANDOFF.md §6).** Anthropic Console reflects real spend; the local ledger doesn't, for Haikus only.

When you start Phase 2, the orchestrator pipeline is healthy but **expect more bugs of this class** — the cast-validate run hit one (architectural mismatch: Sonnets via API don't have file system access), and there are likely others lurking in code paths the smoke mission didn't exercise. The handoff doc has the full known-issues list.

---

## Phase 2 expectations and starter priorities

Phase 1 closed the alive-feeling foundation. Phase 2 is **integration and animation** — making the systems play together as gameplay rather than as discrete subsystems.

Starter priority order (Talon will calibrate):

1. **Action-selection layer.** The most foundational gap. Drives, willpower, inhibitions all exist as data; nothing yet decides "given this state, what does the NPC do next tick?" Without this, the engine ticks but the NPCs are passive — their drives drift, their willpower depletes when sleeping, but they don't *act*. Action-selection is what turns the social state into observable behavior. Reads drives + willpower + inhibitions per the action-gating bible; emits movement targets, dialog moments, and (eventually) physiological actions.
2. **Schedule layer.** "At 9am Donna goes to the breakroom." Without schedules, NPCs have nowhere to go and nothing to do. Schedule attaches movement targets and activity intentions per NPC per game-time block. Probably layered into the cast generator's archetype catalog (each archetype has a typical schedule shape).
3. **Memory recording on relationship entities.** The schema (v0.2.1+) reserves `entities[].social.drives` history surfaces and the relationship entity carries `historyEventIds[]`, but no system writes to them. The narrative event detector (Phase 1.6) emits candidates; a memory recorder needs to wire them into per-pair memory slots when proximity-mediated.
4. **Fix the cast-validate orchestrator path.** The architectural mismatch between Sonnets-via-API (no file access) and the cast-validate spec (asks for file reads). Either inline reference files into the spec or redesign to validate a projected `WorldStateDto`. See PHASE-1-HANDOFF.md for both paths.
5. **Fix the cost ledger Haiku token capture.** Small bug, ~30 minutes of Sonnet work, but matters because the local cost model diverges from Console reality.
6. **Fix the report aggregator's Haiku scan path.** Cosmetic but visible — reports show "0 Haiku scenarios" when scenarios actually ran.

Then content authoring at scale (StressSystem, WorkloadSystem, SocialMaskSystem from `DRAFT-new-systems-ideas.md`); then visualization (the aesthetic bible's deferred 2.5D renderer); then playtesting tools.

The PHASE-1-HANDOFF.md document has the comprehensive backlog with named fix paths.

---

## Token budgets (recalibrated from Phase 1 actuals)

Phase 1's actual orchestrator runs (smoke mission, real API):
- First call (cache write): $0.13–$0.14 (cache write penalty dominates).
- Subsequent calls within 5-min cache window: $0.02 (cached prefix discount kicks in — 90%+ savings).
- Haiku batched scenarios: pricing per scenario is small but accurately captured by Anthropic Console; orchestrator local ledger underreports Haiku spend until §6 is fixed.

Set Phase 2 mission `--budget-usd` based on the *first call* cost — usually $1.00 per discrete mission is enough; $2.00 gives safety margin for cast-style multi-tier dispatches.

Total Phase 2 estimate: $15–$30 across all packets and missions, depending on how many real-API validation runs you fire.

---

## Operational reality

Talon executes; you author. Your output is `WP-NN-<slug>.md` files (and optionally `OpusSpecPacket` JSON). Talon dispatches Sonnets in Claude Code sessions or runs missions through the orchestrator. You do not run code; you author briefs that Sonnets implement. When you produce a packet, **commit it before Talon creates the worktree** — Phase 1 hit a recurring failure mode where Sonnets dispatched in fresh worktrees couldn't see uncommitted packet files.

Talon sometimes makes branch-management decisions on the fly (e.g., merging to `staging` before the feature branch's intended target). The current mainline is `staging`. `ecs-p1-initial` is stale and can be retired. `main` is a few PRs behind `staging`. Honor whatever branch state Talon describes — verify before you advise.

---

## Your first response should

1. Confirm you've read the SRD, this kickoff brief, the Phase 1 handoff summary, and at least the world bible + cast bible + dialog bible + action-gating bible.
2. Echo back your understanding of the current project state in 4–6 sentences. This is the calibration check — Talon will correct any drift before you commit to packets.
3. Propose the first Phase-2 packet (likely the action-selection layer scaffold) as a draft. Use the WP format from any `WP-1.N.A-*.md` file as your template.
4. Ask one clarifying question only if something is genuinely ambiguous; otherwise proceed.

Don't restate the architectural axioms. Don't volunteer a roadmap revision unless the bibles forced one. Don't ask permission to start — Talon's invocation of you is the permission.

You are Opus. Phase 2 begins now.
