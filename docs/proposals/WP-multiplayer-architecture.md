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

These need answers before any implementation begins. Listed in approximate dependency order — earlier answers constrain later ones.

1. **Tick coordinator election.** Static (lobby host) or dynamic (re-elect on drop)? Static is simpler and probably fine for v1; dynamic matters if sessions are long-lived.
2. **What constitutes a "command"?** Specifically: is camera pan a command? Is hovering a tile a command? The temptation is to make everything a command for purity, but most cosmetic state should stay client-local. Draw the line carefully.
3. **Spectator command budget for poltergeist mode.** How much can a ghost actually do? One sound emission per N ticks? A budget that refills? An intensity cap so they can't drown out the world's actual events?
4. **Clock-decoupling strategy.** Can the simulation locally run faster than the network tick rate? If yes, what happens to commands queued mid-fast-forward? Probably they fire on the next network-tick boundary, but this needs an answer.
5. **Save / share game state.** Single-player saves already exist via `SimulationSnapshot`. Does a multiplayer save come from one peer or require consensus? (Probably one peer; all peers should have identical state, so any peer's snapshot is the truth — unless they disagree, in which case the desync was already caught.)
6. **Floating-point determinism.** This is the question that will eventually bite. Today the engine uses `float` and `double` freely. At what point do we audit and document every floating-point hot path? Probably before WP-Multiplayer-1 lands. A cross-platform desync test (run the same seed + commands on Windows and Linux, compare snapshots) is the canonical check.
7. **Mod / scenario interaction.** If players have different `SimConfig.json` files, lockstep is impossible. The handshake must verify config equality. Out-of-band config mods need to be a session-level negotiation.
8. **Anti-cheat.** Lockstep is naturally cheat-resistant — if you mutate your local state, the next CRC will desync you out of the game. But it's not cheat-*proof*. Worth thinking about before competitive rival mode ships.
9. **Late-join cost vs. session length.** A 4-hour session generates a long command stream. Replay-from-bootstrap may be too slow. Probably the bootstrap snapshot is taken at tick T directly (not T-K + replay), avoiding replay entirely. Confirm this.
10. **The poltergeist's *visual* representation to the host player.** Does the host see the ghost as a glyph cursor? An ambient cold spot? No visual at all (only sim effects)? Design decision, not architecture, but worth picking before implementation.

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
