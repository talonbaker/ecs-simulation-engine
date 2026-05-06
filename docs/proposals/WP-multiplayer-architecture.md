# WP-Multiplayer: Architecture Bible

> **Status:** Constraints document. Not an implementation plan. Captures the multiplayer architecture, gameplay vision, performance targets, and discipline rules to be maintained while the simulation foundation continues to mature. No code lands from this document. Implementation is deferred until the gameplay loop has stabilized and the open questions below are answered.

## North Star

Two or more players share **the same simulated world at the same simulated time**. Every player sees identical NPC behavior, identical room state, identical narrative beats — not because the server is broadcasting state to thin clients, but because every machine is running the same simulation from the same seed and consuming the same player commands at the same ticks. State is *generated*, not *replicated*.

This is the lockstep model. It is the single architecturally cheapest path to "synced time worlds" and the single most performant choice over the wire — because the wire only carries player commands, not simulation state. A multi-thousand-entity office full of NPCs costs the same network bandwidth as a chess game.

The constraint is hard: **any non-determinism in the simulation is a multiplayer-fatal defect.** This is the price of admission, and it must be paid up front.

---

## Player Role and Game Modes

The player is a god watching the sim, never a possessed NPC. This decision is **load-bearing for everything below** and should not be revisited without re-deriving the rest of this document. Direct-control multiplayer would demand lag compensation, client-side prediction, and rollback netcode — none of which are required by god-mode.

Three game modes are in scope, in order of architectural complexity:

### 1. Poltergeist (asymmetric observer) — recommended MVP

One player owns the world. Another player joins as a spectral presence with a narrow, low-intensity command set: emit a sound glyph at a position, knock a cup off a counter, ripple cold air through a tile. They see the full simulation but can only *perturb* it.

This mode is the multiplayer MVP because:
- Conflict resolution is trivial (one authority, one disturbance source).
- It tests the snapshot bootstrap and command pipeline without testing PvP arbitration.
- It integrates *for free* with the [acoustic glyph proposal](WP-acoustic-glyph-system.md) — a poltergeist's "presence" is just another `SoundGlyphComponent` source. Knocking a cup is a `HazardBrokenGlass` glyph emission. Existing `CuriosityComponent` / `ParanoiaComponent` variations produce different NPC reactions to the same ghost without any new code.
- It is the most original of the three modes. Not present in any comparable game.

### 2. Co-op build — second milestone

Two players, each with full edit authority, share the same office. Conflict resolution becomes "last write wins" or a queued command stream with deterministic ordering. NPC pool is shared. Gameplay is collaborative — build a great office, weather a chaos event together.

### 3. Rival startup — third milestone

Two players, contested resources (shared breakroom / fridge / toilet), partitioned authority over half the floor each, sabotage mechanics. Win condition is a sim-native emotional-state aggregate: whose half has lower stress, fewer choking incidents, higher mood. **No HP bars, no score** — the simulation already knows who is winning, because the simulation already tracks `StressComponent`, `MoodSystem`, `BereavementSystem`, the life-state machine. The victory condition is read from the systems already in place.

This mode is the most marketable but the most architecturally complex. It depends on co-op build being mature, and it surfaces the hardest design decisions about zone authority and command arbitration. Defer.

---

## Foundation-First Sequencing

Before anything in this document touches code, the single-player simulation must be **bulletproof** — semi-truck-stable, performant, and feature-complete enough that "what is the player doing" has a clear answer. Multiplayer is a *force multiplier* on whatever the single-player experience already is. If single-player is shaky, multiplayer makes it twice as shaky and ten times harder to debug.

This translates to a hard sequencing rule for everything below: **don't build the secondary effect before the primary feature it depends on exists.** Don't build poltergeist mode before the acoustic glyph system lands. Don't build acoustic-glyph world-level effects (printer fluff, ambient-sound-starts-fire) before the core glyph propagation system exists. Don't build rival-mode resource arbitration before co-op build mode is shipped. Don't build any of this before the simulation foundation can run a thousand-tick session without a hiccup.

The corollary: every multiplayer-prep activity in the *Discipline Rules* and *What Should Happen Now* sections below is also a single-player improvement. Determinism audits, snapshot hashing, command-DTO formalization — these all make the single-player experience more stable, more debuggable, more performant. None of them are "doing multiplayer work early." They're foundation work that happens to be multiplayer-compatible.

---

## Resolved Design Decisions

### Spectator agency (poltergeist mode)

The spectating player must have enough agency to **never get bored**. The default impulse — "click everything just to see" — must be rewarded. Every interactable object in the office reacts to a ghost click in *some* small way, even if cosmetic:

- A potted plant shivers its leaves.
- A cup of coffee ripples.
- A monitor flickers briefly.
- A door creaks without opening.
- A piece of paper flutters to the floor.

NPCs in particular must react to being clicked. Not picked up, not flung, but **something** — a startle animation, a glance over the shoulder, a brief mood-state perturbation, a single line of dialog. The principle is *acknowledgment of presence*, not control.

Bandwidth-wise this is fine: clicks are tiny `EmitGhostInteractionCommand { TargetEntityId, X, Y }` events, the same shape as every other command. A budget cap (e.g. one significant interaction per second of sim time, with cosmetic clicks unmetered) prevents a misbehaving spectator from drowning out the world's actual events.

### Spectator visual identity (poltergeist mode)

The host player sees the spectator as **an ethereal ghost figure** — floating, soft-edged, *cute and curious* rather than threatening. They drift through the office, look at things, click on them. Their visual identity is "lore creature," not "UI cursor." When they aren't actively doing something, they're doing the same thing the host's NPCs are doing: existing in the space, reacting to it, being a small presence.

### Spectator origin lore

The ghost emerges from **Mark's old cube** — directly referencing the [acoustic glyph proposal's example NPC](WP-acoustic-glyph-system.md), where Marcus is the sleeper woken by an argument. In the lore, Marcus is gone (transferred? laid off? something worse?), but his cube has become a thin spot in the world. Spectator-players manifest from it. This single piece of worldbuilding accomplishes three things at once:

1. It explains *diegetically* why the host's NPCs are scared of the player's presence — the ghost isn't an abstract spectator, it's a thing that came out of the haunted cube. Stress reactions to ghost proximity have an in-world reason.
2. It ties multiplayer narratively to the acoustic-glyph proposal, making both proposals feel like parts of the same game rather than disconnected systems.
3. It gives every multiplayer session a built-in setting beat: "remember Mark? He's still… around."

This is the kind of cross-system lore coherence the project should aim for everywhere: design choices that solve a mechanical problem and tell a story at the same time.

---

## Long-Term Player Goals — The Vision

The player's goal is to **manage the controlled chaos of a startup full of young adults** — and to feel *wonder* doing it.

### The cast: young adults with underdeveloped prefrontal cortexes

The NPCs are framed not as employees in the abstract, but as *young adults whose prefrontal cortexes haven't fully formed yet*. They are slightly smarter than babies. They are smart enough to do their jobs, dumb enough to give in to short-term impulse, and not yet wise enough to know better. This single character framing is load-bearing for the whole project, because it tells every system how its NPCs should behave: they make locally-coherent decisions that are bad for them long-term. They eat the contaminated breakroom food because the breakroom is closer than the kitchen on the next floor. They start smoking because the lead does. They clump together because being alone is uncomfortable, and then they sneeze on each other.

This framing makes the chaos *funny and knowable* rather than tragic and pitiable. The player isn't shepherding helpless infants — they're managing recognizable young people who are doing the best they can with the cognitive tools they have, which are not many. The fantasy is closer to "Office of the Young" than to "The Sims as social engineering puzzle."

### Believability: the emotional gap this game is trying to close

In Stardew Valley, the townsfolk are *full of life* but they don't feel *real*. They do what they're told. They follow scripts. An NPC can be marked sad in the dialogue tree and the player nods at the label without believing it. **This game wants to close that gap.** When an NPC is sad, the player should *believe* they're sad — because the player can *see* it propagating through the simulation: the slumped posture, the lower productivity, the muted dialog, the cluster of co-workers who notice and react, the acoustic signature of subdued speech, the eventual narrative beat the system surfaces about it.

The reference for player relationship is **The Sims, voyeurism, and god-fantasy.** The player wants to see some of their cast succeed. The player wants to help some of their cast fail. Both are legitimate. Both are fun. Pretending otherwise is dishonest about why people play these games. The design embraces it: the player has god-fantasy power, and the cast has the emotional and behavioral range that makes that fantasy feel *meaningful*.

To make NPCs *believable*, three things must hold:

1. **Full emotional range.** The cast can feel anything they would plausibly feel — joy, dread, jealousy, infatuation, embarrassment, smugness, despair, mania. Not all of it has to ship in the first version. But the architecture must not cap the range — `MoodSystem`, `StressSystem`, `BiologicalConditionSystem`, and the personality components must be *expandable* in this direction, not closed-form.
2. **Felt autonomy.** NPCs should *feel like they're making their own choices* — not waypointed puppets. They walk somewhere because something in their internal state pulled them there, not because the player ordered it. This is mostly already true (the schedule, drive, action-selection systems are autonomous), and it must stay true. The player nudges the *world*; the NPCs nudge themselves.
3. **Behavioral freedom.** If any NPC in the simulation can do something, every NPC of the right type can do it. No scripted-only abilities. The library of behaviors grows over time, and every behavior is available to every NPC who plausibly fits. This is what produces the "wait, what just happened?" moments — players see emergent combinations of behaviors they didn't predict.

When all three hold, the player believes the NPCs. Without that, the chaos is just numbers.

### The player–cast contract: mutual dependency

The player needs the cast to keep the company alive. The cast needs the player to keep the chaos from killing them. This mutual dependency is the relationship the entire game is built around. The player is not a god optimizing a spreadsheet, and not a referee watching from above — they are the only adult in a building full of brilliant, stupid, hungry young people who would burn the place down without intervention. And probably will anyway.

### The macro goal: a startup of the player's own product

The player chooses what their startup *makes*. The product is a player choice, not a designer choice. The game's progression follows the company's growth: more cast members, more office space, more interpersonal collisions, more chaos to manage. There is *something to expand to get* — more headcount, more floors, more revenue — so that wonder is anchored to a progression loop rather than left as freeform vibes.

### The core experience: wonder

Players should look around their office and not believe what they're seeing. Pokes should be rewarded with a moment of "wait, what just happened?" The acoustic glyph system, the proximity-driven emergent effects, the world-level reactions in [acoustic Phase 2](WP-acoustic-glyph-system.md#phase-2--world-level-effects-deferred) — these are not decorative polish, they are the *core gameplay experience*. Without them, the wonder collapses to spreadsheets and the game is just management. Wonder is the thing that makes the player tell their friend about a session, and a player who tells a friend is a player who keeps playing.

Wonder alone, however, is not a game. It needs the macro goal above to give it structure. Wonder + progression = "I can't believe what I'm seeing, *and* there's a reason I'm building toward seeing more of it."

### Both chaos and extreme organization must be valid play styles

This is a hard design constraint. The game must reward players who run a clinically organized office *and* players who let the office descend into chaos and find that funny. Neither style should be the "right answer." The systems must be tuned so that:

- A clean, well-organized office produces steady, modest growth and rewards players who enjoy planning.
- A chaotic, messy, dramatic office produces wild swings — sometimes catastrophic, sometimes brilliantly creative — and rewards players who enjoy stories.
- Both reach the macro goal. Different paths, equally legitimate.

This rules out a tuning approach where "stress = bad, low stress = good." Stress is *interesting* in either direction. A high-stress office full of dysfunctional young people is a *valid* state of the simulation, not a failure mode. The challenge for the designer is making both ends of that spectrum produce satisfying gameplay.

### Proximity is a first-class design primitive

The mechanical commitment that ties the vision together: **spatial proximity creates emergent effects that are simultaneously rewarding and costly.** Every spatial decision the player makes has both upsides and downsides depending on what they value. Examples articulated:

- **Clumped workers** are more productive together but sneeze on each other and annoy everyone within earshot.
- **Workers around a smoking lead** become more productive (modeling habits, social cohesion) but pick up the smoking habit themselves; the player rolls dice on health vs. output.
- **Bathroom adjacent to breakroom** means smell propagates into the food space, NPCs lose appetite, and a starvation cascade can follow if the player doesn't intervene.

These all share a shape: no spatial layout is "just better." Every choice trades one good for another bad. This is the deepest mechanical commitment of the game and it's what makes the proximity systems (acoustic glyphs, eventual olfactory propagation, behavioral contagion, illness spread — see [acoustic Phase 2](WP-acoustic-glyph-system.md#phase-2--world-level-effects-deferred)) load-bearing for the player goal rather than decorative.

### Implications for multiplayer modes

The three modes inherit clear shapes from this vision:

- **Poltergeist:** a ghost in someone else's startup full of young adults. Pokes things. Watches the chaos unfold. Already articulated in *Resolved Design Decisions* above.
- **Co-op build:** two players co-managing the same startup. Mutual dependency on each other and on the cast.
- **Rival startup:** two startups in the same office building, competing for the same shared resources. Each player is the only adult in their half. Sabotage is the natural move — and the cast on each side is a *vulnerability*, not just a workforce, because the rival can target your young people to hurt your output.

### Deferred — the day/night cycle question

Whether the game continues at night (cast goes home; player follows them; "night stuff" the user's friend suggested but didn't specify), or whether the game cleanly bookends each day at office close, is **explicitly deferred**. More ambitious than multiplayer. Revisit when the core single-player loop is bulletproof and the macro goal proves out. Captured here so the question doesn't get lost; not promoted to current scope.

---

## Architecture: Deterministic Lockstep

### The model

Every connected client runs an identical, full copy of `SimulationEngine`, seeded from the same value, advancing the same number of ticks per second, consuming the same player commands at the same tick boundaries. No client is "the server" in the state-replication sense. One client is elected **tick coordinator** — responsible for collecting commands from all peers and broadcasting the per-tick command bundle — but it does not own state. If the coordinator drops, election re-runs.

### Tick scheduling

The simulation has two clocks:

- **Local tick rate** — how fast the engine *can* advance on this machine. Single-player can hit 120× time-scale because commands are local and immediate.
- **Network tick rate** — the rate at which command bundles are exchanged. Over typical internet, **30 Hz** is a reasonable ceiling. Over LAN, 60 Hz is comfortable.

In multiplayer, the local engine advances at most one network-tick at a time. If a peer is late submitting commands, all peers stall until that peer is heard from or times out (and is dropped or has empty commands inserted). This is the **lockstep tax**: you advance at the speed of your slowest peer.

To preserve the "120× time-scale" experience in multiplayer, decouple **simulation tick** from **wall-clock**. A network tick can carry a delta-T, and each engine advances `N` simulation steps within that delta-T. The network rate stays at 30 Hz; the simulation can still run fast.

### What goes on the wire

Per network tick, per peer:

```
PlayerCommandBundle {
    PeerId      : uint
    NetworkTick : ulong
    Commands    : Command[]    // 0..N
    StateHash   : uint64?      // every K ticks; CRC of SimulationSnapshot
}
```

A `Command` is a serializable DTO. Examples:

- `PlaceFurnitureCommand { Kind, X, Y }`
- `EmitGhostGlyphCommand { GlyphClass, Intensity, X, Y }` (poltergeist)
- `OpenDoorCommand { DoorEntityId }`
- `KnockOverCommand { TargetEntityId }`

**The wire never carries simulation state.** State is regenerated locally on every machine from `(seed, command stream)`.

Bandwidth cost: a typical session in steady state is well under 1 KB/sec per peer. State-replication architectures (Minecraft, MMOs) measure bandwidth in MB/sec. This is the order-of-magnitude advantage lockstep buys.

### Late join and spectator entry

The one *expensive* network operation: bringing a new client into an in-progress session.

1. Coordinator pauses the simulation at tick `T`.
2. Coordinator serializes `SimulationSnapshot` + the seed + the canonical command stream `[T-K..T]` into a bootstrap blob.
3. Bootstrap blob ships to the joining client (this is the only large transfer; once-per-join, not per-tick).
4. Joining client: deserialize snapshot, instantiate engine at tick `T-K`, replay commands `[T-K..T]` to validate the bootstrap matches, then resume.
5. Coordinator un-pauses; new client is now a peer.

This is "the heavy thing" in the design. It's why we already need `SimulationSnapshot` to be serializable end-to-end (it largely is), why DTOs need stable schema versioning (`Warden.Contracts` already provides this), and why the seeded RNG must be re-seedable from a snapshot value.

### Desync detection and recovery

Every K ticks (suggested: every 60 ticks ≈ once per network second at 60 Hz), each peer computes a hash of its `SimulationSnapshot` and includes it in the next command bundle. Coordinator verifies all hashes match.

If they don't:
- **Log the divergent tick** to disk on every peer immediately.
- **Pause the session** and surface a desync error to the players.
- **Diff the offending snapshots** offline. The diff identifies the system that produced divergent state — that system has a non-determinism leak that needs fixing.

Desyncs are catastrophic but highly diagnostic. A desync test in CI (run two engines from the same seed and command stream, assert identical snapshots after N ticks) is the cheapest possible insurance and should land *before* multiplayer code does.

---

## The Determinism Contract

This is the price of admission. Every rule below must hold across the entire simulation, not just within multiplayer-aware code.

### Hard rules — never violated

1. **No `DateTime.Now` / `DateTime.UtcNow` inside any system.** Wall-clock time is the single most common source of multiplayer desync in retrofitted games. Use `SimulationClock.CurrentTick` or `SimulationClock.SimulatedTime` for every time-dependent decision. Wall-clock may only appear at the *snapshot boundary* (telemetry timestamps, log lines, save-file metadata) — never inside system logic.
2. **No `Random.Shared` / `new Random()` (default-seeded).** Every random call goes through a seeded RNG owned by the simulation. The seed is part of the lockstep handshake. The RNG is advanced deterministically by the systems that consume it.
3. **No iteration over unordered collections in a way that affects state.** `Dictionary<,>` enumeration order is *implementation-defined* across .NET versions and architectures. If iteration order affects the result, sort first by entity ID or another stable key.
4. **No floating-point math whose result depends on CPU vendor, FPU mode, or compiler flags.** This is a real concern for long-running sims. For now: prefer integer math where possible, document the floating-point dependencies, and add CRC tests to catch drift early.
5. **No `Task.Run` / parallel-for inside the simulation phase.** Determinism requires single-threaded execution within a tick. The test suite is multi-threaded; the engine is not.
6. **No external I/O during a tick.** File reads, network calls, console prints — all happen at the snapshot boundary, not during a system update.

### Soft rules — discipline

7. **Every player-affecting action becomes a serializable command DTO before it touches the simulation.** Today this is partly true (AI verbs are commands). For multiplayer it must be totally true. The CLI's "place a desk" action becomes a `PlaceFurnitureCommand` enqueued for the next tick, not a direct mutation of the world.
8. **Snapshots are diff-friendly.** A snapshot at tick T+1 should be a small structural delta from tick T. This is not strictly required for lockstep (which doesn't ship state) but is required for the late-join bootstrap to be cheap and for desync diagnostics to be human-readable.
9. **The schema is versioned.** `Warden.Contracts` already enforces this. Don't break it.

### Rules that already hold (don't lose them)

The codebase is unusually disciplined about determinism for a project not yet committed to multiplayer. The existing tests prove it:

- `LightingDeterminismTests`
- `DialogDeterminismTests`
- `PhysiologyVetoDeterminismTests`
- `SpatialDeterminismTests`
- `WorkloadDeterminismTests`

These tests are now **multiplayer-load-bearing**. Treat regressions in any of them as P0.

---

## Synergy With Acoustic Glyph System

The acoustic-glyph proposal and this multiplayer proposal are mutually reinforcing. Specifically:

- A **poltergeist's perturbation** is a `SoundGlyphComponent` emission. No new system needed.
- A **rival player's noisy office** is acoustic glyphs leaking under a closed door into the other player's quiet half — the existing wall reflection / aperture attenuation rules already produce the right behavior.
- **NPC personality reactions** to remote-player presence (a paranoid NPC startled by a distant ghost glyph) emerge from `ParanoiaComponent` weighting without multiplayer-aware code.

Build the acoustic glyph system first; the multiplayer system inherits half its gameplay surface for free.

---

## Performance Targets

Based on the lockstep model and the codebase's existing single-player performance:

| Metric | Target | Notes |
|---|---|---|
| Network bandwidth (steady state, 2-player) | < 4 KB/sec per peer | Commands only, plus periodic CRC. |
| Network tick rate | 30 Hz internet, 60 Hz LAN | Configurable; lower if high-latency connections demand it. |
| Bootstrap blob size (late join) | < 5 MB for typical office | Compressed snapshot + recent command stream. One-shot transfer. |
| Engine tick cost (multiplayer overhead) | < 5% over single-player baseline | Lockstep adds command queueing and a CRC every K ticks. That's it. |
| Desync mean-time-to-detect | ≤ 1 second of sim time | Hash every 60 ticks at 60 Hz. |
| Maximum supported peers | 4 in v1, 8 stretch | Lockstep degrades with peer count due to slowest-peer stalling. |

The "no laggy" goal is achievable because lockstep doesn't generate the latency it has to fight. State updates aren't being shipped, so they can't arrive late.

---

## Open Questions

These need answers before any implementation begins. Listed in approximate dependency order — earlier answers constrain later ones. (Two original questions — spectator command budget and spectator visual representation — have been resolved; see the *Resolved Design Decisions* section above.)

1. **Long-term player goal.** The factory-line analog vs. survival vs. drama vs. audit-mode question above. The single most important question in this document and arguably in the whole project. Until this is answered, every other feature is being built against a moving target.
2. **Tick coordinator election.** Static (lobby host) or dynamic (re-elect on drop)? Static is simpler and probably fine for v1; dynamic matters if sessions are long-lived.
3. **What constitutes a "command"?** Specifically: is camera pan a command? Is hovering a tile a command? The temptation is to make everything a command for purity, but most cosmetic state should stay client-local. Draw the line carefully.
4. **Clock-decoupling strategy.** Can the simulation locally run faster than the network tick rate? If yes, what happens to commands queued mid-fast-forward? Probably they fire on the next network-tick boundary, but this needs an answer.
5. **Save / share game state.** Single-player saves already exist via `SimulationSnapshot`. Does a multiplayer save come from one peer or require consensus? (Probably one peer; all peers should have identical state, so any peer's snapshot is the truth — unless they disagree, in which case the desync was already caught.)
6. **Floating-point determinism.** This is the question that will eventually bite. Today the engine uses `float` and `double` freely. At what point do we audit and document every floating-point hot path? Probably before WP-Multiplayer-1 lands. A cross-platform desync test (run the same seed + commands on Windows and Linux, compare snapshots) is the canonical check.
7. **Mod / scenario interaction.** If players have different `SimConfig.json` files, lockstep is impossible. The handshake must verify config equality. Out-of-band config mods need to be a session-level negotiation.
8. **Anti-cheat.** Lockstep is naturally cheat-resistant — if you mutate your local state, the next CRC will desync you out of the game. But it's not cheat-*proof*. Worth thinking about before competitive rival mode ships.
9. **Late-join cost vs. session length.** A 4-hour session generates a long command stream. Replay-from-bootstrap may be too slow. Probably the bootstrap snapshot is taken at tick T directly (not T-K + replay), avoiding replay entirely. Confirm this.

---

## What Should Happen Now (No Code, Just Discipline)

While the simulation foundation matures, these are the cheap, free moves that keep the multiplayer door open:

1. **Audit for `DateTime.Now` / `Random.Shared`** — grep both, replace with sim-clock and seeded-RNG equivalents. Document any holdouts.
2. **Add a `DeterminismIntegrationTest`** that runs the same seed + command stream through two parallel `SimulationEngine` instances for 1000 ticks and asserts identical snapshots. Make it run on every CI build. This is the single highest-value test in the multiplayer prep budget.
3. **Add a hash function over `SimulationSnapshot`** — incremental, stable across architectures, suitable for the periodic CRC in lockstep. Even useful in single-player as a regression detector.
4. **Promote AI verbs to a fully canonical command DTO format.** They're nearly there. Make sure every player-affecting verb has a serializable, schema-validated DTO representation.
5. **Stop adding wall-clock dependencies inside systems.** Easier to prevent than to remove later.

None of these require committing to multiplayer. All of them are good-hygiene improvements that pay off in single-player (better tests, better save/load, better debugging). They simply happen to also be the multiplayer prerequisites.

---

## Suggested Sequencing (When Implementation Begins)

Implementation is deferred. When it begins, the natural split is:

- **WP-Multiplayer-0 (prep):** Determinism audit, parallel-engine determinism test, snapshot hash. No network code. Single-player visible.
- **WP-Multiplayer-1 (the wire):** Lockstep tick coordinator, command bundling, network protocol, two-peer LAN session. Empty command set — peers can connect and tick in sync, that's it.
- **WP-Multiplayer-2 (poltergeist mode):** Spectator role, ghost glyph emissions, host-only authority. First playable multiplayer mode.
- **WP-Multiplayer-3 (co-op build):** Symmetric edit authority, conflict resolution, shared NPC pool.
- **WP-Multiplayer-4 (rival mode):** Zone authority, sabotage mechanics, sim-native victory condition.
- **WP-Multiplayer-5 (internet play):** Late-join bootstrap, NAT traversal or relay, desync recovery UX, persistent session metadata via Firebase or similar (Firebase is right for *this* — lobby, presence, persistent stats — just not for the per-tick wire).

Estimated complexity: WP-0 is small. WP-1 is medium-large. WP-2 is small. WP-3 and WP-4 are gameplay-heavy, complexity dominated by design rather than netcode. WP-5 is medium.

No external dependencies until WP-5 (where a relay service or hosting decision becomes load-bearing).
