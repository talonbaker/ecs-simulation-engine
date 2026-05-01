# Phase 3 — Parallel Engine Candidates

> **Track:** Track 1 (engine, headless, no visual gate) per `docs/PHASE-3-REALITY-CHECK.md`.
> **Purpose:** Identify engine-side packets that can dispatch *in parallel* with the visual sandbox track — keeping forward motion on simulation depth while Track 2 settles the visual layer.
> **Authority:** Living document. Talon picks; Opus drafts the chosen packets when promoted.

---

## How to read this document

Three sections, ordered by dispatch priority:

1. **Section A — Already-specced packets ready to dispatch.** These are existing WP-3.0.x and WP-3.2.x specs in `docs/c2-infrastructure/work-packets/`. Their dispatch is gated only on dependencies, not on new design work.
2. **Section B — High-confidence new packet candidates.** Drawn from `new-systems-ideas.md`, `new-systems-ideas-vol2.md`, and `potential-systems-ideas/`. Pure engine, no visual gate, small-to-medium complexity. Could be specced as Phase 3 add-on packets without disrupting the existing 3.2.x roadmap.
3. **Section C — Larger candidates with partial visual dependency.** Logic is mostly headless but has a visual aspect (where the pest is, what a leak looks like). Defer until sandbox track is further along.

Section A dispatches *first*. Section B layers on once Section A is in motion. Section C waits.

---

## Section A — Already-specced, dispatch in dependency order

These specs already exist in `docs/c2-infrastructure/work-packets/`. They're the canonical Phase 3 backlog.

| # | Packet | What | Gate |
|---|---|---|---|
| A.1 | **WP-3.0.4** | Live-mutation hardening — `IWorldMutationApi`, `StructuralChangeBus`, pathfinding cache | **Open. Dispatch now.** |
| A.2 | **WP-3.0.3** | Slip-and-fall + locked-in-and-starved | After A.1 |
| A.3 | **WP-3.0.5** | `ComponentStore<T>` typed-array refactor | After A.1, A.2. **Solo dispatch.** |
| A.4 | **WP-3.2.0** | Save/load round-trip hardening | After 3.0.x closes |
| A.5 | **WP-3.2.1** | Sound trigger bus (engine-side emit only) | After 3.0.x closes |
| A.6 | **WP-3.2.2** | Rudimentary physics (`MassComponent`, `BreakableComponent`, throw decay) | After 3.0.x closes |
| A.7 | **WP-3.2.3** | Chore rotation system | After 3.0.x closes; the canonical NPC-driven challenge example |
| A.8 | **WP-3.2.4** | Rescue mechanic (Heimlich, CPR, door-unlock) | After A.7 (chore patterns inform rescue) |
| A.9 | **WP-3.2.5** | Per-archetype tuning JSONs | After A.7, A.8 |
| A.10 | **WP-3.2.6** | Silhouette animation state expansion (engine-side only) | After A.5 (sound triggers couple to states) |

**WP-3.2.5 is the load-bearing balance packet** per Talon's Q2(a). All headless number-tuning happens there. Haiku validation runs verify the curves; Talon iterates JSON values from console output.

---

## Section B — New candidates, headless, parallel-safe

Drawn from the proposed-additions docs. These don't yet have specs; promotion to a real WP packet requires Talon's nod. Each is engine-only, no Unity work, fully xUnit-testable.

Ordered top-to-bottom by best fit for *parallel* dispatch alongside the sandbox track.

### B.1 — TicComponent (motor habits)

- **What:** Pen-clicking, foot-tapping, knuckle-cracking, hair-twirling. Per-archetype tic profile.
- **Extends:** Existing noise/proximity event bus, archetype generator.
- **Complexity:** Small. One component, one system, integrates with existing buses.
- **Why now:** No new infrastructure. Adds character legibility immediately. High signal, low cost.
- **Source:** `new-systems-ideas-vol2.md` (motor habits cluster).

### B.2 — FlatulenceSystem (silent-1 / silent-2 variant)

- **What:** The engine already models digestion through colon transit. A flatulence event is one tag on an existing release. Silent variant produces an `OdorComponent` propagation only; audible variant emits a `SoundTrigger.Fart` (engine-side; consumed later by the sound bus packet A.5).
- **Extends:** `ColonComponent`, existing `OdorComponent` propagation (if present), `InhibitionsComponent`.
- **Complexity:** Small. Extension of existing biology, not a new subsystem.
- **Why now:** Doc-promised (`world-bible`'s Donna-rips-ass moment). Mature theme handled headlessly. Low risk.
- **Source:** `potential-systems-ideas/02-body-taboos`.

### B.3 — DeskStateComponent + Territory Boundary Events

- **What:** Per-NPC desk state (clutter level, food crumbs, personal-item drift). Crossing into another NPC's desk territory raises `BoundaryViolation` narrative events. Couples to passive-aggression accumulators.
- **Extends:** Spatial layer, narrative bus, existing relationship memory.
- **Complexity:** Small. New component + one detection system.
- **Why now:** Couples to chore rotation (A.7) — the territory violation is the kind of thing Donna-cleans-the-microwave-grumbling enriches.
- **Source:** `potential-systems-ideas/04-cleanliness/4.1` and `4.4`.
- **Duplicate flag:** Functionally identical to `new-systems-ideas-vol2.md`'s `PersonalTerritorySystem`. Implement once under whichever name Talon prefers.

### B.4 — InvoluntaryEmissionSystem (burp / hiccup / sneeze / yawn)

- **What:** Cluster of cheap involuntary outputs. Burps couple to recent food intake; sneezes to allergen / illness state; hiccups to a per-NPC tic-rate; yawns to fatigue / contagion (one yawn triggers nearby NPCs' yawn probability).
- **Extends:** `IllnessSystem`, `MoodComponent`, dialog corpus (sneeze → "bless you" auto-response).
- **Complexity:** Small per emission, medium as a cluster.
- **Why now:** Free character texture. The yawn-contagion mechanic is the kind of emergent thing playtesters notice.
- **Source:** `potential-systems-ideas/02-body-taboos`.

### B.5 — CravingSystem + Cyclic-Deficit Mechanics

- **What:** Generalises the existing coffee/caffeine logic to nicotine, gum, "I need to step outside." Hidden deficit accumulates → mask draws strain → satisfaction event resets the cycle. The smoker-bench observation surface.
- **Extends:** `StressComponent`, `WorkloadComponent`, `SocialMaskComponent`.
- **Complexity:** Small. One component, integrates with existing drives.
- **Why now:** Couples to meeting captivity (when satisfaction is denied) for the first emergent drama in the new system.
- **Source:** `potential-systems-ideas/01-vices`.

### B.6 — CompulsionComponent (OCD-lite)

- **What:** Per-NPC compulsion checks (alignment of pens on desk, double-checking the lock, even-step counting). Compulsions cost willpower when interrupted. Couples to context-switching cost.
- **Extends:** `WillpowerComponent`, dialog (when interrupted → mask draw).
- **Complexity:** Small. One component, logic-heavy but no new infrastructure.
- **Why now:** Adds another quiet-character-detail axis Talon can vary across the cast.
- **Source:** `potential-systems-ideas/01-vices` (extension).

### B.7 — PersonalOdorComponent (body-BO aura)

- **What:** Per-NPC odor signature based on hygiene-drift, sweat (stress), illness. Propagates via existing odor system. Disgust input to nearby NPCs' moods. Avoidance pathing for high-odor NPCs.
- **Extends:** `OdorComponent` (if present), `AppearanceComponent`.
- **Complexity:** Small. Wraps existing infrastructure.
- **Why now:** Plays into mask-failure patterns. High signal, no rendering needed at all.
- **Source:** `new-systems-ideas-vol2.md` + `potential-systems-ideas/02`.

### B.8 — SecretWeightSystem + Suppression-Cost Mechanics

- **What:** Each NPC has zero-or-more `Secret`s — facts they don't want others to know. Secrets accumulate suppression-cost per tick (suppression drains willpower). Near-slip events fire when willpower is low + a topic-relevant trigger occurs.
- **Extends:** `KnowledgeComponent`, `SocialMaskComponent`, `IrritabilityAccumulator`.
- **Complexity:** Medium. Multi-NPC tracking, burden calculation.
- **Why now:** Phase 4.2.3 (affair detection) will lean on this. Building it earlier gives the affair-arc richer substrate.
- **Source:** `new-systems-ideas-vol2.md`.

### B.9 — HyperfixationComponent + Conversational Dominance

- **What:** Some NPCs lock onto a topic and steer conversations back to it. Hyperfixated dialog draws status from listener boredom; couples to belonging drive when reinforced.
- **Extends:** Dialog corpus, `BelongingDrive`, `StatusDrive`.
- **Complexity:** Small. One component, conversation-duration gating.
- **Why now:** Adds character distinctiveness through dialog without new dialog content.
- **Source:** `new-systems-ideas-vol2.md`.

### B.10 — AversionLinkComponent (NPC-specific fear)

- **What:** Per-pair aversion edges in the relationship graph. NPC X always finds reason to leave when NPC Y arrives. Couples to mask-draw on forced proximity.
- **Extends:** Relationship graph, pathfinding (avoidance bias), `MaskComponent`.
- **Complexity:** Small. One edge type.
- **Why now:** Creates emergent floor-pattern observability — "Donna and Greg are never in the breakroom together." Pure logic, observable in JSONL stream.
- **Source:** `new-systems-ideas-vol2.md`.

### B.11 — NoiseSystem + AcousticEnvironmentComponent

- **What:** Per-room ambient noise level (HVAC, fluorescent buzz, traffic). Discrete noise events (shouted argument, sudden laugh, door slam). Disrupts flow state, raises stress for noise-sensitive archetypes.
- **Extends:** `WorkloadComponent`, `StressComponent`, future `FlowStateComponent`.
- **Complexity:** Medium. Room-based ambient + discrete event firing.
- **Why now:** Couples to A.5 (sound trigger bus). Authoring noise events headlessly now means A.5's sound triggers have content the moment they ship.
- **Source:** `new-systems-ideas.md` extension.

---

## Section C — Larger candidates with partial visual dependency

These are good systems but want a visual feedback loop to balance correctly. Defer until the sandbox track has shipped enough integration packets to support visual verification.

| Candidate | Why deferred |
|---|---|
| PhobiaComponent + PestEntitySystem | Pest *position* matters for handler-NPC pathing; cockroach-in-meeting is a signature scenario but needs visual anchor. |
| PanicAttackSystem | Logic is headless; "frozen NPC" state visually communicates. Useful but ships richer once silhouette animation states exist. |
| BathroomZoneComponent | Stall-door visual state matters. Crying-in-bathroom is a feature but hard to verify without seeing it. |
| MenstrualCycleComponent | Calendar logic is headless; rare leak event has a visual aspect (blood-on-clothes) that needs sandbox confirmation. |
| FlowStateComponent + InterruptionTracking | Flow-state needs a visual signal (NPC posture) for player to read. Logic OK headless. |
| AppearanceDecaySystem + PostureComponent | Same. Decay is logic; visual signal lands later. |
| LunchRitualSystem + GroupFormation | Group-formation logic is headless, but *where* lunch happens (breakroom / car / outside) wants visual distinction. |

---

## Recommendation for parallel dispatch (Talon picks)

**Highest-leverage parallel queue while sandbox track ships:**

1. Track 1 already-specced: A.1 (WP-3.0.4) → A.2 (WP-3.0.3) → A.3 (WP-3.0.5). Dispatch in order.
2. Then Track 1 already-specced: A.4 → A.5 → A.6 → A.7 → A.8 → A.9 → A.10 in dependency order.
3. New candidates layered in (Talon picks 2–3 to spec early — recommended starters): **B.1 (Tics)**, **B.3 (Desk territory)**, **B.5 (Cravings)**. These three are the cheapest, lowest-risk, highest-character-payoff additions.

The remaining B-section candidates are good but should not all dispatch at once — pick 2–3 per phase so each gets balanced and integrated thoughtfully against existing systems.

---

## What to do with this document

1. Talon reviews, picks 0–3 Section B candidates to promote.
2. Opus drafts WP packets for the picked candidates, committed to `docs/c2-infrastructure/work-packets/`.
3. Sonnet dispatches them as Track 1 packets in parallel with the sandbox track.
4. Update this doc as candidates promote into specs and specs into completed work.

---

*Survey conducted by an Explore subagent against `new-systems-ideas.md`, `new-systems-ideas-vol2.md`, `potential-systems-ideas/`, and `potential-systems-implementation-ideas/`. ~600-word agent report distilled into this prioritised plan. Original agent output captured in conversation history; re-run if categories shift.*
