# WP-Acoustic: Acoustic Glyph System

## Concept Overview

Sound in this simulation is not a UI abstraction. It is physical matter — emitted, shaped, directed, bounced, absorbed, and eventually silenced by the geometry it travels through. The Acoustic Glyph System renders every sound source in the world as a cluster of Unicode characters that propagate across the ASCII floor-plan in real time, collide with walls and furniture, decay as they travel, and register on NPCs who happen to be in their path. There is no sound icon, no HUD indicator, no invisible radius check. If an NPC hears something, you can watch it happen. If a room is loud, it looks loud. The acoustic layer is diegetic — it exists inside the simulation world, not as a commentary on it.

---

## The Core Idea

Every sound source in the world, when it fires, emits a cluster of Unicode glyphs. The *shape* of that cluster is not arbitrary — it encodes the perceptual character of the sound at a glance.

An angry argument produces jagged, asymmetric, attack-shaped glyphs: `! ╱ ╲ ∧`. They feel violent even as text. Normal conversation produces soft undulating characters: `~ ∼ ≀ ⌇` — readable as gentle, non-threatening pressure. A phone ringing emits concentric expanding rings: `◌ ○ ◯` — their circular symmetry immediately reads as mechanical periodicity. A copy machine outputs dense rhythmic dashes: `▬ ▬ ▬` — relentless, directional, monotonous. Footsteps leave sparse directional dots: `· · ·` — quiet, pointed, purposeful. A pane of glass shattering produces a radiating starburst — `✦ ✧ ⁕ ∗` — with a fast decay rate that communicates the event's brevity even as the glyphs scatter.

None of this is cosmetic. Each glyph is an entity with velocity, direction, a time-to-live counter, and an intensity value. The shape of the cluster is the system's natural vocabulary, but its behavior is what the simulation actually computes.

---

## Why This Is Original

Sound visualization exists in games — equalizer bars, ripple effects, colored indicators. But those systems are decorative layers drawn *over* the world, not participants *in* it. They don't bounce. They don't get blocked by a closed door. They don't accumulate on the other side of a thin wall.

What this system proposes is different: acoustic energy as a first-class simulation layer, governed by the same geometry that governs everything else. A wall isn't just a visual boundary — it's an acoustic reflector. An open door isn't just a traversal affordance — it's an acoustic aperture. A heavy filing cabinet doesn't just block sight lines — it absorbs a propagating glyph cluster and kills its momentum. Room materials affect decay. Soft furnishings dampen. Hard surfaces amplify and redirect.

No known game — and certainly no ASCII simulation — does this at this level of physical fidelity. The closest analogues are abstract noise-radius circles in stealth games, which are invisible, static, and geometry-ignorant. This system treats sound the way the rest of the simulation treats everything else: as something that actually happens in the world.

---

## System Architecture (High Level)

### SoundGlyphComponent

Each emitted glyph is a lightweight ECS entity carrying the following fields:

- `SourceEntity` — the entity that originally produced the sound (NPC, object, event trigger)
- `Glyph` (char) — the Unicode character representing this particular glyph
- `Velocity` (Vector2) — direction and speed in tiles per tick
- `TTL` (int) — remaining ticks before this entity despawns
- `DecayRate` (float) — the multiplier applied to TTL on each bounce or absorption event
- `Intensity` (float) — weight used in NPC perception calculations; starts at emission strength, reduced by decay
- `GlyphClass` (enum) — one of: `Speech`, `Mechanical`, `Impact`, `Ambient`, `Alarm`

The `GlyphClass` is what allows NPCs with different cognitive profiles to respond differently to the same physical event. A jagged `!` impact glyph and a soft `~` speech glyph may carry identical intensity values but trigger completely different behavioral responses depending on who receives them.

---

### SoundPropagationSystem (World Phase)

This system runs each tick and is responsible for moving glyphs through the world and handling collisions.

On each tick, every active `SoundGlyphComponent` advances along its `Velocity` vector. When a glyph reaches a tile boundary, the system checks what occupies the destination tile:

- **Hard wall**: reflect the glyph using angle-of-incidence logic, apply `DecayRate` penalty to TTL and Intensity
- **Soft collision (furniture, body)**: absorb some or all velocity, sharply accelerate decay
- **Open door**: pass through at full intensity — acoustically transparent
- **Closed door**: heavy attenuation — the glyph's Intensity is significantly reduced and most of the cluster fails to penetrate
- **Locked door**: near-total absorption — effectively a wall for acoustic purposes

This means the acoustic behavior of a space changes dynamically with player and NPC actions. Opening a door between two rooms isn't just a traversal decision. It's an acoustic decision. That consequence should be legible from the glyph behavior without any tooltip.

---

### AuditoryPerceptionSystem (Cognition Phase)

This system runs in the cognition phase, after propagation has settled for the tick. For each NPC, it:

1. Queries all `SoundGlyphComponents` within the NPC's perception radius
2. Sums their `Intensity` values, weighted by `GlyphClass` (an `!` impact glyph counts for more than a `~` ambient murmur)
3. Compares the sum against the NPC's `AuditoryThreshold`
4. If the threshold is exceeded, emits the appropriate response based on the delta and the NPC's current cognitive state

Responses include: `Wakeup`, `Startle`, `Investigate`, and `Ignore`. Sleeping NPCs receive a special path — threshold exceedance fires a `LifeStateTransitionRequest → Awake`, routing through the existing life-state machinery rather than bypassing it.

NPCs with personality components can weight the perception step differently. An NPC with a `ParanoiaComponent` might apply a 2× multiplier to `Mechanical` and `Impact` class glyphs — turning the hum of a copy machine into a potential threat. An NPC with a `CuriosityComponent` might lower its investigation threshold for `Speech` glyphs regardless of intensity, always drawn toward conversation.

---

### Rendering Layer

Sound glyphs render above the floor, walls, furniture, and hazards — but below NPCs. The Z-order is:

> floor → walls → furniture → hazards → **sound glyphs** → NPCs

This placement means glyphs are always visible but never occlude the agents they might be affecting. Dense glyph clusters on a tile make it visually "loud" — a cramped office hallway during a fire alarm should look like visual noise. An empty corridor at 3am should look like silence: bare floor, no glyphs, nothing moving.

This makes acoustic state readable at a glance without any HUD element. A player who sees `∧ ! ╲ ∼ ✧` bleeding around a corner knows something significant is happening before they round it.

---

## The Sleeping NPC Example

Consider an NPC named Marcus. He is asleep at his desk. His `AuditoryThreshold` is `12.0` — he's a light sleeper, but not paranoid.

Two rooms away, an argument breaks out between two other NPCs. The source entity emits a cluster of jagged glyphs: `! ∧ ╱ ╲ !` — five glyphs, each with starting Intensity `5.0`, GlyphClass `Speech`, moving outward in all directions.

The glyphs radiate. Some dissipate into walls. Several travel down the corridor. They bounce off a hard tile wall — DecayRate reduces their Intensity to `4.2` each. They pass through an open doorway — full intensity preserved, `4.2` each. Further down, two of them clip a filing cabinet — absorbed. Three continue, clipping off a second wall for a minor decay hit, arriving at `3.8` each. Four glyphs total reach Marcus's tile. Cumulative intensity: `4 × 3.8 = ~14.3`.

Threshold exceeded. `LifeStateTransitionRequest` fires. Marcus wakes up.

Now run the same scenario with the connecting door closed. The closed door's attenuation is severe — only 2 glyphs penetrate, each at reduced intensity `1.55`. Cumulative on Marcus's tile: `3.1`. Threshold not exceeded. Marcus sleeps on.

The door made the difference. No invisible radius, no arbitrary integer check — the player can watch that causal chain play out as physical glyph movement through the map.

---

## Connections to Existing Systems

**NarrativeEventBus**: A high-intensity acoustic cluster arriving at an NPC tile is a natural narrative candidate. The bus can detect threshold-exceeding events and generate candidates like *"The argument woke Marcus"* — the glyph system provides causally complete evidence for the narrative layer to interpret.

**SoundTriggerBus**: The glyph emission point is the natural place to also fire any diegetic audio event. The two systems share a source but serve different purposes — one propagates physically through the world, one communicates to the player's ears.

**AsciiMapProjector**: Glyphs render on top of existing floor-plan infrastructure. This is a new Z-layer in the projector, not a separate rendering pipeline. The map already knows how to render tiles at different layers; glyphs slot in without restructuring the renderer.

**LifeStateTransitionSystem**: Wakeup, startle, and faint-from-shock all route through the existing `LifeStateTransitionRequest` pattern. The acoustic system doesn't invent new state machinery — it fires requests into infrastructure that already handles the consequences.

**Cognition systems**: `CuriosityComponent`, `ParanoiaComponent`, and future personality traits can intercept the `AuditoryPerceptionSystem`'s intensity weighting step to shape how individual NPCs respond to the same acoustic event. The architecture supports divergent NPC behavior without special-casing.

---

## Open Questions / Design Decisions

1. **Glyph stacking vs. intensity merging**: Should multiple glyphs on the same tile render as stacked characters (visually dense, accurate) or should the system merge co-located glyphs into a single weighted entity (cheaper, less granular)? The rendering implications are significant and the choice affects both performance and legibility.

2. **Velocity scaling with source volume**: Is a shout twice as fast as a whisper, or does it simply emit twice as many glyphs at the same speed? Faster glyphs feel more urgent but complicate reflection math. Higher glyph count preserves the physics model but increases entity load. This needs a deliberate answer before architecture is locked.

3. **Glyph entity type**: Should glyphs be full ECS entities (queryable, composable, fully integrated) or should they be managed internally by the propagation system as a performance-optimized lightweight structure? Full entities give the most flexibility but could stress the ECS on busy floors. A hybrid — lightweight internal representation with periodic ECS projection — may be the right answer.

4. **Tick budget at simulation speed**: At 120× time-scale, a single loud event could flood the active glyph pool with hundreds of short-lived entities per second. The system needs either a tick-budget cap, a glyph-pooling strategy, or an early-cull heuristic to stay performant during high-density moments. This is a real constraint that needs scoping before WP-1.

5. **Passive NPC emission**: Should NPCs passively emit low-intensity glyphs — breathing `· ·`, heartbeat `◦ ◦`, nervous pacing `· · ·` — that other NPCs can detect? This would enable a genuine stealth sub-layer (a paranoid NPC perceiving a hidden player through breathing) but adds continuous ambient emission load. Worth considering as a Phase 2 capability but needs a clear design boundary before Phase 1 ships.

---

## Suggested Next Step

If this proposal is greenlit, the implementation splits cleanly into three work packets:

- **WP-Acoustic-1**: `SoundGlyphComponent` definition + `SoundPropagationSystem` (movement, wall reflection, attenuation rules)
- **WP-Acoustic-2**: `AuditoryPerceptionSystem` (NPC threshold checks, GlyphClass weighting, wakeup/startle dispatch)
- **WP-Acoustic-3**: Renderer layer integration (Z-layer in `AsciiMapProjector`, glyph visual vocabulary, density legibility testing)

Estimated complexity: moderate across all three. No external dependencies. WP-Acoustic-1 and WP-Acoustic-3 can likely be developed in parallel once the component schema is locked. WP-Acoustic-2 depends on WP-Acoustic-1's component shape but is otherwise independent of the renderer.
