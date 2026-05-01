# 01 — Vices, Cravings, and Tics

> The body wants something on a schedule the office didn't ask about. The hand goes somewhere the conscious mind didn't authorize. Habits are how a person leaks even when they're trying very hard not to.

---

## What's already in the engine that this builds on

- `StressComponent`, `SocialMaskComponent`, `WorkloadComponent` from `new-systems-ideas.md`.
- `CaffeineComponent` and `CoffeePotComponent` from the existing CoffeeSystem proposal.
- `willpower` + `inhibitions` from the action-gating bible.
- Drives: `irritation`, `loneliness`, `belonging`, `affection`.
- Big Five, particularly `conscientiousness` and `neuroticism`.
- The `deal` catalog. (Most of what's in this cluster is "deal" material that the engine has to actually do something with when the deal fires.)

---

## 1.1 — CravingSystem + CravingComponent (the smoke-break archetype, generalized)

### The human truth

The smoker who has to step out every fifteen minutes is the obvious case, but the same shape covers the e-cig user, the gum-chewer who needs a fresh piece every half hour, the energy-drink-cracker, the snus-tucker, the person who has to walk a lap of the floor every hour or their leg starts shaking. The shape is the same: *a habit that's on its own clock, that the body will start renegotiating with the office about as soon as it's overdue, and that has a social signature when the renegotiation hits.*

### What it does

A `CravingComponent` is a per-NPC bundle of *cyclic cravings*. Each craving has its own cycle and decay shape — the body recovers when the habit is satisfied; without satisfaction, an internal value climbs from `comfortable → restless → jittery → agitated → snapping`. The climb is what the office sees.

Each craving carries:

- `kind` — `nicotine | caffeine | sugar | nicotineSnus | gum | walk | vape | snack | hit-the-vending-machine`. Easy to extend; `kind` is just a tag, the mechanics are the same shape.
- `cycleMinutes` — the comfortable interval. 15 minutes for the chain-smoker; 30 for the casual; 90 for someone who only has one cigarette after lunch; 240 for the person whose deal is "one cigarette a day on the way home and that's it."
- `intensity` — how steep the climb is. Low intensity NPCs barely notice the missed habit; high intensity NPCs are unrecognizable thirty minutes past their cycle.
- `currentDeficit` — 0 at last satisfaction, climbs over time, resets on satisfaction.
- `lastSatisfiedTick` — for proximity events ("they just got back from a smoke" is socially legible state).
- `quitAttemptActive: bool` plus `quitDayCount` — when an NPC is trying to quit, the cycle is suppressed by willpower instead of satisfied. The deficit climbs even when they're back from a "break" they didn't take.

### Visible behavior as deficit climbs

Deficit translates into existing engine signals so the world reads it without new render layers:

- `restless` — small `irritation` drive bump, faster idle micro-movement (per the aesthetic bible's idle-jitter system), more frequent looks toward the door.
- `jittery` — `WorkloadComponent` progress rate halved, finger drumming, `FlowStateComponent` cannot enter (interruptible by their own internal state).
- `agitated` — short verbal-register flip in the dialog system: a normally-`casual` NPC drops to `clipped`; a normally-`formal` NPC drops to `casual`. Speech-time deflection: dialogue-system context preference shifts toward `brush-off` and `lash-out`.
- `snapping` — `SocialMaskComponent` drops by a flat 15–30 points instantly; the next dialog moment fires with `mask-slip` context (per the dialog bible's open question on mask-slip context). Whatever they say next has a high `noteworthiness` score and is a strong calcify candidate.

### Components

- `CravingComponent`: `Cravings[]` of the shape above.
- `CraveSatisfiedEvent` on the proximity-event bus when a craving resets — used by other NPCs to read the smoker's state ("oh, they just came back, they'll be approachable for ten minutes").

### Personality + archetype gates

- `conscientiousness` low + `neuroticism` high produces the most legibly-cyclic smoker. They go on the clock; you can set your watch by them.
- `conscientiousness` high + `neuroticism` low produces the disciplined smoker who can stretch the cycle without snapping — they choose when to go.
- The Recovering archetype's relationship with this system is its whole arc. Their deal often *is* a quit attempt in progress. Their `quitAttemptActive` may be the load-bearing flag of their save.
- The Cynic chain-smokes without trying to hide it. The Climber is a secret smoker — they hide it from everyone above them and the smell becomes a calcified suspicion among coworkers.
- The Old Hand smokes at exactly two known times and the office uses these as informal clock-marks ("must be after 2:30, Linda just stepped out").

### Cross-system interactions

- **Smoking Bench (world bible / threshold-space):** every smoking craving satisfaction ticks the bench's social graph — the bench knows who's a regular, who's an occasional, who's a "I quit but" returnee. NPCs read each other's regularity as a trust signal.
- **MeetingSystem / CaptiveComponent:** captivity prevents satisfaction. A 90-minute meeting for a 30-minute-cycle smoker is *three full deficit climbs* by the end of it. They are different people walking out than walking in. This is part of why meetings cost what they cost.
- **CoffeeSystem:** caffeine craving and nicotine craving are *separate* components on the same NPC and have *coupled* social geography (smoke break ↔ coffee break ↔ the smoking bench / the coffee pot). The dual-craver who hits both circuits per hour is a known office type.
- **PassiveAggressionSystem:** "you smell like cigarettes" notes on the fridge are a calcified attack pattern when a smoker's cycle has been satisfied recently. The smoker reads it; their `irritation` spikes; they go for an extra one.
- **WorkloadSystem:** an overdue task plus a missed cycle is the fastest mask-failure path the engine can produce. Both pressures are pushing the same direction.

### What the player sees

A character disappears for five minutes every cycle. The bench is a status object — when there are three people on it, they're a *group*; when there's one, that's a confessional waiting to happen (per the threshold-space proposal). The new hire smelling like cigarettes during their week-one interview is a deal-catalog signal that the player reads without anyone telling them. Linda, who quit four months ago and is doing fine, walks past the smoking bench on day forty-seven and has a moment.

### The extreme end

Quit-day. The Recovering archetype is at quitDay 1. The bullshit memo from the boss arrives at 10am. WorkloadComponent spikes from a re-prioritized deadline. StressComponent climbs. Nicotine deficit at 11am hits `agitated`. Mask drops to 35. The smoker walks past the bench three times that day on the way to the bathroom and back to their desk. They don't sit on it. By 4pm they're so close to snapping that the smallest interaction carries lash-out energy. Their quit is held by a single thread of willpower against the entire weight of the day. Whether they hold or break is what the simulation produces.

### Open questions

- Should the engine model the *physiological* cost of a craving (e.g., dopamine system changes, bloodstream timing) or treat the deficit-climb shape as the abstraction? Recommended: the abstraction. Going deeper isn't a player-readable improvement.
- Should the cycle reset on *any* break (walk, water, bathroom) for low-intensity cravers, or specifically on the matched habit? Probably specific to the kind, with a small partial-relief from *any* getting-up-from-desk activity.
- Quit-attempt as a visible save-level state: should the `quitAttemptActive` flag be exposed to other NPCs through the `KnowledgeComponent` so coworkers can root for or undermine it? Probably yes — but only to NPCs the smoker has told.

---

## 1.2 — TicComponent (idle motor habits)

### The human truth

Watch any quiet office. Half the people are doing something with their hands. Knuckle cracking, pen clicking, leg jiggling, hair twirling, ear rubbing, lip biting, picking at a cuticle, twirling a wedding ring (or its absence), fiddling with a necklace. These tics are usually invisible to the person doing them and audible-or-visible to everyone within fifteen feet.

### What it does

Each NPC carries 0–3 motor tics in a `TicComponent`. Tics have:

- `kind` — `knuckleCrack | penClick | legJiggle | hairTwirl | nailBite | ringTwirl | pickAtCuticle | clickPenCap | tapFoot | clickTeeth | tongueClick | hummmm | sniffle`. Open-ended.
- `auditoryRadius` — 0 for visual-only, otherwise the tile radius at which other NPCs hear it. (`penClick` and `knuckleCrack` are loud; `legJiggle` is silent unless the desk shakes.)
- `triggerProfile` — the conditions under which the tic surfaces. Three flavors: `idle` (low arousal, NPC is bored), `stress` (high stress, surfaces under pressure), `flow` (paradoxically, some tics surface while concentrating).

The mechanic on the speaker side is small and cheap: each tic emits a `TicEvent` at intervals modulated by their drive state. On the receiver side it's an `OdorComponent`-shaped propagation — listeners in `auditoryRadius` receive a small `irritation` drip per tick, scaled by their *own* sensitivity (see cluster 07).

### Calcify into identity

Like the dialog calcify mechanism, repeated emission of the same tic produces a `RecognizedTic` flag on listeners' memory of the tic-er. Other NPCs *know* Linda jiggles her leg. They use it as a state-read: "leg's going faster than usual today, something's up." This is the same mechanic that turns Greg's `"Mm."` into part of who he is — applied to motor habit.

### Components

- `TicComponent`: list of tics.
- `TicEvent` on the proximity-event bus.
- Existing `KnowledgeComponent` stores the recognized-tic fact ("Linda has the leg-jiggle thing").

### Personality + archetype gates

- High `neuroticism` produces the most legibly-cyclic tics. They're audible from across the floor when she's anxious.
- The Newbie picks up new tics they didn't have before — week one is calm; week three they're nail-biting because the office is contagious.
- The Hermit's tics are silent. Knuckle-cracking is too social for them; they fidget invisibly.
- The Climber's tic surfaces only under their *own* stress, never in front of superiors — `SocialMaskComponent` suppresses it, and the suppression itself costs willpower. When their mask is at 30%, the tic comes back.

### Cross-system interactions

- **NoiseSystem:** auditory tics are noise events. A pen-clicker is a one-NPC noise factory and the rest of the room is paying focus-disruption costs all day.
- **FlowStateSystem:** a flow-triggered tic is a tell that the NPC is *in* flow. Listeners read "they're locked in" and avoid interrupting.
- **PassiveAggression:** the note that says PLEASE STOP CLICKING THE PEN is real. The pen-clicker reads it, doesn't know who it's from, becomes self-conscious, suppresses for a day, returns.

### What the player sees

A faint, regular sound coming from a corner of the floor. The player learns whose sound it is. When the sound changes pace, something is up with that person, even if nothing else has visibly happened.

### The extreme end

The contagious leg-jiggle. One NPC has it. The NPC at the adjacent desk catches it (cluster 07's misophonia mechanic working in reverse — they pick up the rhythm involuntarily). Within an hour two desks are jiggling in sync. The third desk, the high-conscientiousness NPC, is *holding still* by force of will and watching them, paying mask-energy to not make a comment.

---

## 1.3 — CompulsionComponent (OCD-adjacent specifics)

### The human truth

Different from a tic. A tic is autonomic; a compulsion is a thing the person *feels they must do.* The desk that has to be aligned. The doors that have to be checked. The number of times you tap the kettle. The pen pointing in the same direction. The post-it stack that has to be perfectly square. The character who can't *leave* until they've done their thing, even if leaving on time mattered.

The line between "particular about their workspace" and "obsessive-compulsive" is a continuous slider, not a binary. The system handles the slider.

### What it does

`CompulsionComponent` carries 0–2 compulsions per NPC. Each compulsion is a *check* the NPC must complete before transitioning out of certain states (leaving for the day, leaving for lunch, finishing a task, beginning a task).

Each compulsion has:

- `class` — `desk-alignment | door-check | hand-wash | counting | symmetry | double-check-saved | ritual-arrangement | item-orientation`.
- `strength` — 0–100. At 30 the compulsion is a quirk; at 70 it's load-bearing for the NPC's ability to function; at 90 it's the deal that defines them.
- `disrupted` flag — set when the workspace state has been altered by another NPC (someone moved the stapler).

When `disrupted` is true and `strength` is high, the NPC cannot context-switch until they restore. This is a *willpower drain* on suppression — they can power through if the situation demands it, but they pay.

### Components

- `CompulsionComponent`.
- `CompulsionDisruptedEvent` when something is touched.

### Personality + archetype gates

- High `conscientiousness` is a baseline. High `neuroticism` raises strength.
- The Climber's compulsions are presentation-class — desk-alignment, item-orientation, double-check-saved. They look immaculate at all times by design.
- The Hermit's compulsions are private — they have an arrangement of the IT closet that nobody else perceives but that they immediately notice if someone (cleaning staff, a visitor) altered.
- The Old Hand's compulsions are ritualistic — the same coffee in the same mug at the same time on the same chair.
- The Recovering archetype frequently has count-based compulsions tied to their recovery program.

### Cross-system interactions

- **PassiveAggression:** moving someone's pen is a passive-aggressive act *if* the mover knows the target has a compulsion. If they don't know, it's a faux pas. The cleaning crew (an NPC class that exists or could exist as a deal) is an innocent disrupter.
- **TerritorySystem (cluster 04):** desk territory and compulsion territory are coupled. A high-strength compulsive NPC's desk is ringed in invisible lines.
- **WorkloadSystem:** the NPC who can't start the next task until they've squared the post-it stack is a known productivity pattern. Net effect on output is mostly neutral (the squaring is fast) but visibly weird.

### What the player sees

The moment when an NPC's hand goes back to a stapler that another NPC nudged thirty seconds ago. They didn't see it move. They felt that something was off. Their face (silhouette) reorients toward the desk, they pause, they reset the stapler, and only then can they continue what they were doing.

### The extreme end

The day someone moves the Climber's nameplate by half an inch as a prank. They notice immediately. They cannot get back into a task all morning. Their work output is visibly degraded. By 11am they've corrected it three times because they keep finding it slightly wrong on each pass. The pranker doesn't know how badly the day is going for them.

---

## 1.4 — Hyperfixation / DeepNicheInterest

### The human truth

Some people in offices have a thing. A thing-thing. The model trains. The bird identification. The exact knowledge of the New York subway system. The Civil War. The stamps. The thing they will absolutely tell you about if you give them an opening, and that they will *not* tell you about if they're carefully reading the room. The thing is part of who they are. It's also the thing they'll be most alive about all week.

### What it does

Each NPC has 0–1 `Hyperfixation` (most have zero). When a proximity event creates a conversation moment AND the topic graph (a soft data structure on the conversation event — see cluster 16's emergent culture) admits the hyperfixation, the NPC's drive state shifts: `belonging` drops in importance, `status` drops, *interest* spikes. The NPC will *take* the opening, even if it's socially costly.

Hyperfixation conversations:

- Last longer than the speaker realizes (the listener's face is reading exit but the speaker is locked in).
- Calcify dialog tics around the topic faster than normal speech (per the dialog bible's calcify mechanism). The NPC has a *vocabulary* about their thing.
- Return: once a hyperfixation has surfaced once with a listener, the speaker re-broaches it on later encounters even when the listener didn't ask. This is observable and slightly painful to watch.

### Components

- `HyperfixationComponent`: `topic` (string), `intensity`, `lastSurfacedTick`, `listenerHistory[]` (per-listener counter — who they've already brought it up to).

### Cross-system interactions

- **DialogCalcify:** specific phrasings about the hyperfixation calcify exceptionally fast because the NPC says them often. The listener carries them as recognized tics.
- **BelongingDrive:** if the topic lands with anyone (the rare listener who actually shares the interest), an `affection` spike binds the two NPCs. The hyperfixation is a love-magnet for compatible others and a slow liability with everyone else.

### The extreme end

The person who tries to bring up their hyperfixation in the holiday party (cluster 10). It is the wrong room and the wrong audience, but the willpower needed to hold back at the holiday party is exactly the willpower the holiday party has already drained from them. They surface the topic; the conversation collapses; mask-slip event fires; they spend the rest of the night quieter than they came in.

---

## 1.5 — Quit-Attempt arc as a first-class save state

### The human truth

The person who's trying to quit something is a specific *kind* of office worker, and the engine's existing biology + the proposed CravingSystem is most of what's needed to make their week dramatic. What's missing is a top-level *arc shape* — the initial commitment, the day-by-day ratcheting against deficit climbs, the relapse-or-hold moment, the aftermath. A shape that the engine can produce without authoring.

### What it does

When an NPC enters a quit attempt (Recovering archetype default; or arises mid-save in response to a crisis or a deal trigger), a `QuitAttemptArc` flag and a counter (`dayCount`) attaches to them. The arc has stages with characteristic mechanics:

- **Day 1–3 — high willpower, high deficit climb.** Mask is up; tic frequency spikes; sleep quality dips. Other NPCs notice "Linda's a little off this week."
- **Day 4–10 — the wall.** Willpower baseline temporarily lowers; mask drops faster; passive-aggression outlets activate more. The most likely relapse window. Other NPCs who are paying attention know this and either give space or accidentally step on a landmine.
- **Day 11–30 — settling.** Willpower returns to baseline plus a small bonus (the discipline is rebuilding the muscle). Tic frequency normalizes. Recovery has ambient warmth — `affection` and `belonging` baseline shift up.
- **Day 30+ — the milestone.** Either calcified into a new normal *or* relapse risk reignites under the next major life-event bleed-through (cluster 09). The Recovering archetype's defining trait is that this counter resets often.

### What the player sees

A shape they can watch unfold. The first three days look one way; week two looks different; if Linda makes it past the wall the floor reads it as a quiet success and treats her differently from then on. If she doesn't, the relapse moment is a `noteworthiness: 95+` event for the entire floor.

### The extreme end

The Recovering archetype, day 47 of a quit attempt, is in the parking lot at 4:55pm Friday after the worst week of the quarter. The smoking bench has two coworkers on it. Their existing components — willpower depleted by the week, mask at 8%, craving deficit at `snapping`, a passive-aggressive note from earlier in the day still riding the irritation accumulator — combine to a single decision. The simulation does not script the answer. The arc shape commits the engine to producing the moment.

### Open question

Should `QuitAttemptArc` be a generic shape that hosts *any* recovery — substance, eating habit, gambling, infidelity (the affair archetype trying to end the affair), confrontation-avoidance (someone who's deciding to stop tolerating it)? Probably yes. The arc is recovery-shaped at the abstract level; the specific habit is a parameter.
