# 04 — Cleanliness, Mess, and Territory

> The desk is the closest thing to a private home most office workers have at work. What happens at it, what's on it, what crosses its invisible border, and who notices is most of what's interesting about cubicle life. Adjacent desks are also adjacent value systems.

---

## What's already in the engine that this builds on

- `OdorComponent`, `AppearanceComponent`, `PassiveAggressionSystem`, `KnowledgeComponent`.
- The `RotComponent` already on the year-old fridge container.
- Spatial index, room/cubicle entities, named anchors.
- Big Five — `conscientiousness` is the load-bearing axis here.

---

## 4.1 — DeskStateSystem + DeskStateComponent

### The human truth

Every desk in an office is a continuous broadcast about its owner. Coffee rings, paper drift, the precise cleanliness of the keyboard, the photos, the toys, the file-pile. A desk reads in five seconds and the read is mostly accurate. The desk is also under continuous ambient threat from the world (drift) and from the owner's own mental state (entropy). Maintaining the desk costs willpower; letting it go signals something.

### What it does

Each cubicle/desk is an entity with:

- `DeskStateComponent`: `clutter` (0–100), `crumb` (0–100, food residue), `paperDrift` (0–100), `knickKnackCount`, `personalArtifacts[]` (photos, plants, toys), `surfaceStains`, `visibleFromHallway` (a flag derived from cubicle geometry).
- `clutter` and `crumb` have natural drift rates per sim-day. NPCs *clean* by triggering small clean-actions (bursts of effort that reset clutter/crumb partially or fully).
- The owner's `conscientiousness` and the existence of any `compulsion` of class `desk-alignment` (cluster 01.3) determine the *equilibrium* — high-conscientiousness NPCs hold the desk near zero clutter; low-conscientiousness NPCs let it climb until something forces a reset.

### What other NPCs do with desk state

When NPC Y walks past NPC X's desk and X's desk is in their `visibleFromHallway` field, Y reads:

- **High clutter** — small `irritation` if Y is high-conscientiousness; small *reassurance* if Y is low-conscientiousness ("not just me"); slight `suspicion` of competence if Y is the Climber (read as disorganization).
- **Personal artifacts** — `KnowledgeComponent` content. The wedding photo. The dog photo. The kid drawing. The disturbing collectible. The political pin. Artifacts are *legible character data* for whoever sees them.
- **Crumbs and stains** — small disgust input. Drives `IrritationDrip` toward the owner. Also drives `pestSpawnProbability` upward in the relevant room (cluster 03.1 — cockroaches like crumbs).

### The neighboring-desk gradient

The most interesting case is *adjacent* desks with mismatched cleanliness equilibria. The fastidious + slob neighbor pair is a sustained low-grade conflict that the engine can produce continuously:

- Fastidious neighbor's `irritation` baseline rises with sloppy neighbor's `clutter` value.
- The shared boundary (the cubicle wall, a shared surface, a half-height divider) is a contested space. Items drifting from the slob's side onto the fastidious side trigger a `BoundaryCrossingEvent`.
- The fastidious neighbor's mask draws daily from suppression of the urge to comment.
- Eventually: passive-aggressive note (existing system), or ostentatious cleaning (existing), or a direct conversation (rare, status-costly), or a *seating swap* request to management (the player as ghost-management can accommodate or not).

### Components

- `DeskStateComponent` on each desk entity.
- `BoundaryCrossingEvent` on the proximity bus.
- Personal artifacts as their own small entities with `meaning` metadata for KnowledgeComponent reads.

### Personality + archetype gates

- The Climber's desk is performative — a few carefully-chosen artifacts, no clutter, the framed certificate from the conference. Maintained at high willpower cost.
- The Hermit's desk is utilitarian. No artifacts. Greg's desk in the IT closet is literally just hardware.
- The Old Hand's desk is a *layered* space — artifacts from twenty years of working there. The lava lamp from someone who left in 2003. The mug from a software vendor that doesn't exist anymore. A history.
- The Cynic's desk is sloppy on purpose. They could clean it. They don't.
- The Recovering archetype's desk often has a *single* sentimental artifact that the floor reads as their lifeline.
- The Newbie's desk is initially *blank* — they haven't earned the right to personalize yet, in their own self-understanding. After about three weeks they put up one photo, tentatively.
- The Vent (Donna)'s desk is overflowing with personal artifacts — a calendar of cat-of-the-day, a candy dish, three plants, multiple framed photos. It's visible from the hallway and it's *the* hangout cube for the floor.

### Cross-system interactions

- **OdorComponent:** crumbs and stale-food artifacts produce smell over sim-days.
- **PestEntitySystem:** desks with high `crumb` are more likely to spawn pests (specifically cockroaches and ants) within their tile radius.
- **PassiveAggressionSystem:** the note-on-the-cubicle-wall about "kindly clean up after yourself" is a known attack pattern. The recipient has plausible deniability about which neighbor wrote it because both have grievance.
- **TerritorySystem (4.4 below):** the desk *boundary* is the most important micro-territory in the building.

### What the player sees

A floor where, even at low render fidelity, the desks are *different*. Some are pristine; some are catastrophic; most are middling. The player learns who's whose just from desk silhouette.

### The extreme end

The clean-desk-policy edict from upstairs. An exec announces that desks must be cleared at end of day. The Vent's desk *cannot be cleared* — there are hundreds of artifacts. Three days of trying. Mask draws hard. Productivity collapses. By Friday, three NPCs (her closest friends) have stayed late to help her find homes for the artifacts. The Vent cries (at desk, in front of others). The chronicle records the loss. The Climber, who proposed the policy, is read as the villain by the floor for the rest of the save.

---

## 4.2 — ArtifactDriftSystem (the small-object-trespass)

### The human truth

Pens migrate. Mugs end up at someone else's desk. Staplers vanish for a week. The hair tie on the conference room table that's been there for two months. Office objects have *low intentionality* in their movement; they accumulate where social currents drop them, like small flotsam.

### What it does

Each small object has an `OwnerHistory[]` (a stack of recent positions and possessors) and a `nominalOwner` (zero or one NPC). Objects without nominal owner are public; objects with one are someone's. NPCs may *borrow* (proximate use), *take* (move to own desk for indefinite use), or *abscond* (move and lose track of) public-class objects.

When an NPC's nominal-owner item is at someone else's desk for more than X sim-time, a `MissingItemEvent` fires for the owner (small `irritation` bump per check that fails to find it). When it appears back, an `ItemReturnedEvent`.

The *who-took-the-stapler* pattern is then a simulation event: NPC X's stapler is at NPC Y's desk because Y borrowed-and-forgot. X's irritation is up. X may approach Y (high-agreeableness) or take it back without saying anything (low-agreeableness) or write a note (high-passive-aggression). Y may not have known they had it.

### Cross-system interactions

- **TerritorySystem (4.4):** the borrowed-without-asking is a territory event when the borrowing crossed a boundary the owner cared about.
- **FavorLedgerComponent:** chronic borrowers accumulate small negative balances they don't know they have.
- **PassiveAggression:** the engraved label on the mug. The "PROPERTY OF X" sticker on the stapler. Defensive territoriality made object.

### Personality + archetype gates

- The Founder's Nephew borrows freely and never returns. The office has accepted this.
- The Climber maintains tight inventory; their items don't drift.
- The Old Hand has objects in their drawer that originally belonged to people who left years ago.

### What the player sees

A subtle continuous current of objects moving around the floor. Most NPCs barely notice. The few who care, care a lot.

### The extreme end

The Cubicle 12 problem (existing world-bible anchor). Mark's desk was cleared, but several of his items are still at other people's desks — borrowed before he left. The objects exist in `OwnerHistory[]` lookups as last-owned-by-Mark. NPCs who pick them up read the history; their `KnowledgeComponent` records *Mark's pen* with the slow-suspicion drift the world bible notes for adjacent NPCs. The objects themselves are vectors for the unspoken.

---

## 4.3 — VisualDisgustAccumulator

### The human truth

Some sights, sustained, are corrosive. The trash can that hasn't been emptied in three days. The ceiling stain. The one fluorescent that's been flickering for a month. The poster that's hanging slightly crooked. These don't trigger an event each time you see them; they tick at you constantly. By the third week the same person who walked past it without noticing every day is now actively angry about it.

### What it does

`VisualDisgustComponent` on NPCs accumulates a small per-tick `disgust` signal whenever they're in line-of-sight of an offending world object. The signal is small per tick, but persistent, and *non-decaying when actively visible*. After enough exposure, the signal crosses thresholds that produce action:

- **Threshold 1:** mention in dialog (low-noteworthiness) — "Has anyone called about that ceiling stain."
- **Threshold 2:** passive-aggressive intervention — small note, attempt to fix it themselves, complaint to the receptionist.
- **Threshold 3:** disproportionate emotional response — the NPC who finally snaps about something tiny, where the snapping is fueled by months of looking at it and never being able to fix it.

### Sources

- Ceiling stains, water marks, paint chips, exposed wiring, missing ceiling tiles.
- Burnt-out fluorescents that maintenance hasn't replaced.
- The framed picture on the wall that's been crooked for two months.
- A trash can that's overflowing.
- The doorknob held on with a single screw.
- The carpet stain. (The world-bible mentions "the chair-mark on the breakroom wall" — same shape.)

### Components

- Per-room: `VisualOffenseEntity[]` — small entities with `severity`, `noticedBy[]`, `firstNoticedTick`, `daysVisible`.
- Per-NPC: aggregated `VisualDisgustComponent` accumulator that decays only when the offense is removed *or* the NPC sleeps.

### Personality + archetype gates

- High `conscientiousness` accumulates fast.
- The Climber sees them but doesn't act unless they affect a meeting.
- The Old Hand has stopped noticing them; they've lived with it. Their disgust accumulator has a calcified-tolerance flag for these specific offenses.
- The Newbie *sees* them — their accumulator is empty, fresh — and is the most likely to actually report them.
- The Hermit has high tolerance for visual offense in their own domain (the IT closet) but cannot stand it elsewhere.

### Cross-system interactions

- **PassiveAggression:** the "please replace this fluorescent" note on the maintenance whiteboard.
- **WorkloadSystem:** persistent visual disgust at peripheral vision is a small but real `FlowStateComponent` drag.

### What the player sees

A floor where, over time, certain *places* become slowly more aggravating to live around. Some get fixed (a specific NPC takes initiative); some don't. The rate of fix vs. accumulate is part of the floor's character.

### The extreme end

The conference room ceiling stain that has been there since before the save started. Three NPCs have been silently accumulating disgust about it for months. One day, mid-meeting, the Newbie casually mentions it ("does anyone know what that is?"). Two months of suppressed irritation in three NPCs hits the floor at once. A meeting that was about the project becomes a meeting about facilities. Maintenance is finally called. The stain is fixed. The disgust accumulators reset. Six weeks later, a *new* stain appears. The cycle resumes.

---

## 4.4 — TerritorySystem (the cubicle, the chair, the parking spot)

### The human truth

Office territory is a layered field. The desk is mine. The chair at the desk is mine. The mug on the desk is mine. The drawer is mine. The parking spot is *technically* assigned and *practically* contested. The seat in the breakroom that you always sit in is informally yours after about six weeks. The bathroom stall you always use. Other people crossing these lines is irritating in a way that scales with the *intensity* of your ownership claim, not with any objective harm.

### What it does

Each NPC carries a `TerritoryClaims[]` — a list of `(target, intensity, awareness)`. Most claims are weak (the breakroom seat). A few are strong (the desk, the chair). Some are contested (the parking spot).

When another NPC violates a claim:

- Low-intensity claim + low-stakes violation: nothing happens.
- High-intensity claim + low-stakes violation: small `irritation` bump in the claimant.
- High-intensity claim + high-stakes violation: significant `irritation` + suspicion + sometimes a confrontation.

The *intensity* of a claim is a function of:

- `conscientiousness` (claimants who care more about order claim harder).
- `tenure` at the company (the Old Hand's claims are deep; the Newbie's are tentative).
- Recent emotional charge near the territory (an NPC who cried at their desk last week claims it harder for a while).

### Specific named territories

- **Desk and chair** — the strongest claim. Sitting in someone's chair when they're at lunch is a near-violation in the early-2000s office.
- **Parking spot** (named anchor) — the laminated NO FIGHTING sign is real because parking-spot violations are real. NPCs check the lot on arrival; finding the wrong car in their spot is a high-irritation event.
- **Breakroom chair / lunch seat** — informal, but six-week claims are real.
- **Bathroom stall** — surprisingly intense; people have a stall they prefer.
- **Microwave time-slot** — the noon rush. Some NPCs *always* microwave at noon. Their slot is informally claimed.
- **Coffee mug** — strong, owner-named.
- **Specific walking path** — some NPCs have *paths* they always take. The Hermit's path through the floor to the bathroom and back. Crossing paths is fine; *blocking* a habitual path is a small violation.

### Cross-system interactions

- **DeskStateSystem:** territory claim on the desk is the deepest; it's their whole desk-state.
- **PassiveAggression:** territorial defense is one of the largest sources of passive-aggressive output.
- **MovementSystem:** habitual paths exist as soft-state in the pathfinder (per the aesthetic-bible's "consistent pass-side" personality micro-trait).
- **FavorLedgerComponent:** recognized respect for territory builds positive favor balance with the claimant. NPCs who *refuse* to sit in someone's chair when offered earn a small ambient warmth.

### What the player sees

The floor is layered with *invisible boundaries* the player can learn to read. Watching NPCs respect or violate them is a continuous information channel.

### The extreme end

The parking-lot incident. The Climber's spot is taken on a Wednesday morning by the Founder's Nephew, who couldn't be bothered to read the sign. The Climber finds another spot. Their `irritation` climbs all morning. By 11am, Mask is at 50%. They confront the Nephew at lunch — a confrontation they would normally avoid because the Nephew is socially untouchable. The confrontation goes badly. The Climber's `status` takes a hit because the office reads them as the one making a scene. The Nephew couldn't care less. The Climber's parking-lot situation has now been added to their KnowledgeComponent as a calcified humiliation — a memory they re-read involuntarily on every commute.

---

## 4.5 — PlantCareDrama

### The human truth

A communal plant in the office is a slow drama. Someone bought it. Someone takes care of it. Someone forgets. It dies, slowly, visibly. Or it thrives because *one* NPC took it on as a project and now half the floor knows them as the plant person. The plant becomes a small symbolic register of how the office is doing.

### What it does

`PlantEntity` with `health` (0–100), `lastWateredTick`, `caretakerId` (nullable), `position`. Health drops daily without water; rises with water within a window.

NPCs occasionally water the plant based on a personal `plantCareTendency` (dispositional, mostly correlated with `agreeableness` × low `conscientiousness` — the agreeable, slightly-disorganized NPC is the plant's best friend). Once an NPC has watered the plant N times, they become the implicit caretaker. Other NPCs *defer* — they don't water it because "Karen waters it." If Karen is on vacation for two weeks, the plant nearly dies. If Karen leaves the company, the plant *does* die unless an explicit handoff happens (rare).

### Symbolic resonance

The plant's `health` is a quiet mood indicator for the floor. A thriving plant correlates with high collective `belonging`; a dying plant correlates with chronic stress. The simulation doesn't enforce this; it emerges because the same conditions that drain attention drain plant care.

### Cross-system interactions

- **KnowledgeComponent:** the caretaker is a known social fact.
- **VisualDisgustAccumulator:** a dying plant is a visual offense that ticks at increasing rates as it browns.
- **GoingAway events (cluster 10):** a leaving NPC's plant is a handoff drama. Or it isn't, and the plant dies a month later, and people remember.

### What the player sees

A plant in the breakroom that the player follows. They learn whose plant it really is. When it dies, it means something.

### The extreme end

The big ficus that's been in the corner of the breakroom since 1998. The original owner left the company in 2001. Three caretakers have come and gone since. It's currently maintained by the Old Hand, whose own arc has them considering retirement. The plant is in `health: 35` and dropping. The Old Hand's leaving means the plant ends. The player can read the plant as a register of the Old Hand's emotional state through the entire arc; when the Old Hand finally retires, the chronicle records the plant's death the same week.

---

## 4.6 — Communal-Object-Decay

### The human truth

The microwave. The fridge (already a named anchor). The coffee pot. The conference room dry-erase board with sentences from a meeting in February still on it. The communal printer. These are objects that nobody owns, that everyone uses, and that decay continuously without ownership. The *who-cleans* problem is a real social-coordination failure that produces simulation-ready content.

### What it does

Each communal object has:

- `decayState` (0–100) — fills with use, depletes with cleaning.
- `lastCleanedBy: NPCId?`
- `lastCleanedTick`
- `cleanThreshold` — the level above which other NPCs begin to *notice* and accumulate `disgust`.
- `whoUsedTodayList` — recent users.

Cleaning is voluntary. NPCs choose to clean based on:

- `agreeableness` (high → more likely to volunteer).
- `conscientiousness` × disgust accumulator (the bothered-fastidious NPC eventually does it).
- Status drive (low — cleaning is low-status; high-status NPCs do not clean communal objects, this is a real workplace pattern).
- Whether they were the last to use it (mild guilt → cleans).
- Whether they are *aware* they were the last to use it.

The breakroom's microwave, conditional on the existing `OdorComponent` engine integration, becomes a slow-drama object as smell accumulates with every fish-warmer who uses it without wiping the splatter.

### Cross-system interactions

- **PassiveAggressionSystem:** notes on every communal object. The fridge is the existing anchor; the microwave gets its own.
- **SmellSystem:** uncleaned communal objects generate `OdorComponent` entities.
- **KnowledgeComponent:** the *known fact* of who cleans / who never cleans calcifies. The floor remembers.

### What the player sees

The breakroom counter accumulating crumbs. The microwave's interior visibly worse. The eventual someone-snaps cleaning event. The cycle resets. Some NPCs systematically clean (silent contributors); some never do (silent non-contributors). The latter are noted.

### The extreme end

The microwave that hasn't been cleaned by anyone but the Vent (Donna) for nine months. She's been doing it because she can't stand it. She's not been thanked. She finally writes a note on it announcing she's stopping. Three days of accelerated decay. The first real cleaner is — surprisingly — the Cynic, who decided privately that they wouldn't let Donna lose a small fight. The cleaning is wordless. Donna sees it from across the room. Their `affection` pair-state shifts. The Cynic doesn't acknowledge that they did it; Donna doesn't acknowledge that she saw. An entire relationship reconfigures around *who finally cleaned the microwave*.
