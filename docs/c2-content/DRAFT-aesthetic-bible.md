# Aesthetic Bible — Foundational Systems and Style (Working Draft)

> Co-authored by Talon and Opus. Defines what makes the world *feel alive* in priority order. Visual rendering style is recorded but not the priority — the systems below are.

---

## What "alive" means here

The world feels alive when small things are observably changing all the time. Light shifts as the sun moves. Someone walks past a window and the shadow crosses the floor. People step around each other in a hallway. Someone leaves a room and the hum of the room changes. None of this is gameplay-critical; all of it is *world-critical*. It's the difference between a doll house and a place.

The systems below are ranked by what produces the most alive-feeling per implementation hour:

1. **Lighting** (priority 1).
2. **Proximity** (priority 2).
3. **Movement** (priority 3).
4. **Visual rendering style** (deferred — the simulation can be alive in a debug renderer).

---

## Priority 1 — Lighting

Procedural in the cheapest possible sense: windows admit light from the outside; that light propagates onto floors and walls; light sources inside the building add to the field. NPCs and players can perceive and respond to light state.

### What the engine commits to

- Each room has an `illumination` field — ambient level, per-tile (or per-region) intensity, color temperature.
- Each window is a light *aperture* that admits a beam from a sun-position vector. The beam falls on whatever is downwind of it (floor, wall, NPCs as they pass through).
- Each interior light source (overhead fluorescent, desk lamp, IT-closet LED bank, the buzzing breakroom strip) is an entity with intensity, color temperature, and a `state` — `on`, `off`, `flickering`, `dying`.
- Time of day drives sun position; sun position drives window-beam direction; window beams drive a falloff cone across the room. Cheap raymarching at low resolution is enough.
- **Lighting is queryable state.** Telemetry exposes per-region illumination so AI and behavior systems can read it. This is what makes lighting a simulation concern, not just a render concern.

### Lighting → behavior mapping

This is where lighting becomes simulation. Mappings are tunable; these are starting baselines:

- **Flickering fluorescent** — NPCs in the affected region get a small `irritation` drive bump per minute exposed. Sustained exposure raises `loneliness` slightly. The buzzing is real.
- **Warm desk lamp** — NPCs in close range get a small `belonging` and `affection` drive nudge. Comfort.
- **Dark hallway** (after-hours, lights off) — NPCs traversing get a `suspicion` and `caution` bump. They walk faster.
- **Server-room LED glow** — neutral, slightly disorienting. Greg has gotten used to it; nobody else has.
- **Sunlight from window** — an NPC who sits in direct sun for a while gets a `mood` lift and small drive recovery. The good seats are valuable.
- **No lighting at all** (basement corner, fluorescent dead) — steady decay of `belonging` and small `loneliness` rise. Why nobody likes the basement.

These mappings are configurable in `SimConfig.json` so tuning doesn't require code changes.

### Time of day

A 24-hour cycle. Office hours roughly 8 AM – 6 PM with stragglers. Sun rises ~6 AM, sets ~6 PM (era-appropriate, not scientifically accurate). Light from windows shifts color and angle through the day:

- **Early morning:** cold, low angle, long shadows.
- **Mid-morning:** warm-bright, narrow beams.
- **Afternoon:** soft, diffuse, golden.
- **Evening:** orange-red if the building faces the right way, otherwise dim.
- **Night:** window beams flip — interior lights spill *out* instead of sun spilling in. Outside passersby visible as silhouette only.

---

## Priority 2 — Proximity

NPCs know who and what is near them. Most social interaction is gated by proximity: you don't gossip with someone three rooms away.

### What the engine commits to

- A spatial index per floor, queryable: *"which entities are within R of position P?"*
- Each NPC has a `proximity awareness` with three ranges:
  - **Conversation range** (~2 tiles): can talk, can exchange feelings, can pass an item.
  - **Awareness range** (same room): notice presence, notice mood, notice arrival/departure.
  - **Sight range** (across an open area): can see, cannot interact.
- Proximity events fire as discrete signals: `entity-entered-conversation-range`, `entity-left-room`, `entity-visible-from-here`. Behavior systems subscribe.
- Proximity is the trigger surface for *most* social drive updates. A character who hasn't been in conversation range with anyone in a sim-hour gets a `loneliness` bump even at their desk. A character who passes a Rival in a hallway gets a small `irritation` bump even with no conversation.

This is what makes the world feel populated rather than a series of stages.

---

## Priority 3 — Movement

Movement should feel intentional, not robotic, even at low fidelity.

### What the engine commits to

- **Pathfinding that prefers natural paths** — around obstacles, through doorways, with slight randomness so two NPCs taking the same trip don't trace identical lines.
- **Step-around-each-other in hallways** — two NPCs approaching head-on shift slightly to one side. The side they pick is consistent for that NPC (always-pass-on-left vs always-pass-on-right is a personality micro-trait).
- **Idle micro-movement when stationary** — small position jitter, occasional posture shift, looking-around behavior. NPCs should not be perfectly still. Stillness reads as broken.
- **Movement speed varies with mood** — high `irritation` walks faster; high `affection` slower; tired walks slower. Observable from a distance, part of what makes the world readable.
- **Eye-line / facing direction is part of entity state.** NPCs face *somewhere*, and that somewhere is meaningful — toward the person they're talking to, toward their work, toward the door if they're about to leave. Facing direction is queryable.

Together, lighting + proximity + movement is what creates the lived-in feel. None of them require a final visual style to be useful — a debug top-down renderer with circles for NPCs and rectangles for furniture is enough to validate that the simulation feels alive.

---

## Visual rendering style (deferred but committed in principle)

This is what the visualizer eventually does with the simulation state. The simulation does not depend on it. It is downstream of everything above.

### Style commitment

- **Pixel art aesthetic at the rendering layer.**
- **Underlying entity geometry is low-poly 3D.** Pixel art is the *render*, not the *underlying model*. This means lighting and shadow can be calculated against real 3D and then quantized to pixel-art style at output. The benefit: real shadows from real light, with a pixel-art look.
- **Texture priority over polygon count.** A thing should read as the thing it is from its texture — the chair looks like a chair because of its wood-grain texture, not because it has 10,000 polygons.
- **Faces are not detailed enough to read expressions.** Emotion reads through silhouette, body language, and observable consequence. (See cast-bible.)

### Reference touchstones (open to revision)

These are calibration points, not copy targets:

- **Stardew Valley meets early-Rare-3D** (Banjo-Kazooie era).
- **The lighting feel of Don't Starve** (Tim Burton-ish but in an office).
- **The readable-at-distance silhouettes of Hotline Miami** (the ability to know who someone is from their shape alone).

Any of these can be wrong; pin them down when you've prototyped a scene and seen what works.

### Color palette

Era-appropriate: muted, slightly desaturated, warm fluorescent yellow indoors, cool daylight from windows, the over-saturated teal-and-orange of late-90s/early-2000s corporate aesthetic. **Beige as the dominant office color.** The occasional bright accent (someone's red sweater, a yellow caution sign, the Coke machine) reads strongly because the rest is muted.

---

## What's deliberately not committed yet

- The exact look of any specific prop, person, or surface. That's per-asset work, not bible work.
- A specific shader pipeline. Pixel-art-from-3D is a recipe with several implementations; the right one depends on what scenes look like in prototype.
- The UI / HUD style. A separate concern, addressed later.

---

## The hierarchy, summarized

1. **Lighting state and lighting → behavior mapping** (engine concern, schema concern, simulation concern).
2. **Proximity awareness and proximity-driven social state** (engine concern).
3. **Movement quality and observability** (engine concern).
4. **Visual rendering style** (visualizer concern, deferred).

Phase 1 should land 1–3. Visual rendering is for whenever the simulation is rich enough to be worth looking at, which won't be Phase 1.

---

## Open questions for revision

- Sun-direction model: is the building's orientation fixed (we always know which windows face east), or is it a parameter? Fixed is cheaper; parameter is more flexible.
- Time-of-day cadence: real-time-but-faster (1 sim-day = 1 player-hour?), or paused-by-default with a step-time control? Affects pacing more than tech.
- Should NPCs have a separate `noticeability` from raw proximity? (e.g., a character in a bright sun-beam is *more* noticed than the same character in shadow even at the same distance.) This would couple lighting to social systems further but adds complexity.
