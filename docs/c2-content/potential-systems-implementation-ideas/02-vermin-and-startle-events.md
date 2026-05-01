# 02 — Vermin and Startle Events

> Real offices have small biological invaders that produce wildly disproportionate reactions. The user explicitly called these out: "what would someone do if a spider crawled on them or a cockroach crawled out of the fridge." This cluster covers vermin, surprise, the body's fight-or-flight reflex translated into ECS primitives, and the social aftermath of the moment after.

---

## 1. VerminSystem + VerminComponent + Phobia layer

### The human truth
Most people have a low-grade dread of spiders, cockroaches, mice, or wasps that surfaces only in the seconds when one is actually present. The reaction is involuntary — a scream, a leap, a chair-knock-over. After the reaction, there's the *story* of the reaction. Did anyone see? Who handled it? Who froze? Who laughed? The vermin itself is gone in thirty seconds. The social residue is permanent.

### What it does
- A vermin is an entity in the world with its own movement, attractants (smell, water, dark spaces, food crumbs), and lifetime. Spawns are rare and weighted by world state (the fridge container, the supply closet, the basement).
- An NPC who comes within `noticeRange` of a vermin entity rolls a phobia check against their `PhobiaComponent`. If it lands, a `StartleEvent` fires.
- The startle event has magnitude, kind (scream / freeze / flee / faint / handle), and a public visibility computed from witnesses in awareness range.

### Components / data
- New: `VerminComponent { Kind: enum (Spider | Cockroach | Mouse | Fly | Wasp | Ant | Centipede), Speed: float, AggroRadius: float, Lifetime: int }`
- New: `PhobiaComponent { Phobias: Dictionary<VerminKind, int> // strength 0..100 }` per NPC
- New: `StartleEvent { source: NpcId, trigger: EntityId, magnitude: int, response: enum }`
- Reuse: `MoodComponent` for the fear spike; `LifeStateComponent` for the rare faint case.

### Reaction taxonomy
The startle response is selected by personality + phobia strength + situation:
- **Scream** — high neuroticism, high extraversion, high phobia. Most public.
- **Freeze** — high neuroticism, low extraversion. Stuck in place; nearby NPC has to intervene or the vermin escalates.
- **Flee** — high openness or low conscientiousness. Path-departure event.
- **Faint** — extreme phobia + already low energy + high stress. Triggers the existing `LifeStateComponent` `Incapacitated` transition for ~30 sim-seconds.
- **Handle** — low phobia. The NPC who steps up and squashes / scoops / shoos. Status reward.
- **Frozen + cried** — most neurotic combination. The mask collapses simultaneously.

### Mechanics
- The vermin can be killed (squash event, a `BrokenItemComponent` analog for the corpse), captured (cup-and-paper handled by a high-handle-rating NPC), or escape (it disappears, which means the NPC who saw it now knows it's *somewhere* in the building, producing ambient suspicion).
- Witnesses register the responder's behavior in their `KnowledgeComponent`. The NPC who handles a wasp at the all-hands is *visibly competent* and gets a small status bump from every witness.
- Compounded interaction: the freeze response makes the next vermin appearance worse — a `RecentTraumaTag` on the NPC raises their phobia strength temporarily.

### Extreme end
The all-hands lunch. A cockroach emerges from the back of the catered tray. The Climber, who has been performing executive composure, screams. Multiple high-status witnesses see this. The mask-slip is total — high-noteworthiness, persistence-threshold-meeting. Greg, three steps away, calmly picks up a napkin, captures the roach, walks it outside. Greg's status bump from the witnesses is permanent. The Climber's social capital takes a hit they will be quietly compensating for for game-weeks. **The engine produced this without any authoring.**

### Archetype fit / phobias as personality knobs
Phobias are hidden inhibitions per the action-gating bible — the player invents the reason. Spawn-time roll for each NPC: a small chance per vermin kind of carrying a low/mid/high phobia. The vast majority have only mid-strength common phobias (spider, wasp); a few have severe specific ones; rarely, an NPC has zero phobias (the Old Hand who grew up on a farm, the Cynic who simply doesn't react).

---

## 2. JumpScareSystem (the non-vermin startle)

### The human truth
Someone tapping you on the shoulder when you're focused. The microwave beep right behind you. The fire-alarm test that wasn't announced. The pop of a chip bag in dead silence. These are mundane interruptions that produce a measurable stress / cortisol spike, and a small embarrassment if the reaction is visible.

### What it does
Generalize startle into a `JumpScareEvent` with arbitrary triggers:
- Tap-on-shoulder by another NPC during high-flow state
- Loud unexpected sound (printer chug, fax, fire alarm test, balloon pop)
- Light suddenly turning on/off
- A door slamming somewhere out of sight

### Components / data
- Reuse: `StartleEvent`, `MoodComponent` (Surprise spike already in MoodComponent)
- New optional: `JumpinessComponent { Baseline: int, RecentExposureBuildup: int }`

### Mechanics
- Jumpiness builds with current stress, low energy, and `RecentTraumaTag`.
- The post-jump moment matters more than the jump: the NPC who jumps in front of others has a brief mask-slip (the `! ?` cue per UX bible's emotional iconography).
- The serial scarer — a deal-catalog "always sneaks up on people" trait — accumulates negative affection over time even though each scare seems small.

### Extreme end
The Newbie, in deep flow at 3:14pm Tuesday, is approached by the Climber from behind. The Climber says their name. The Newbie jumps hard enough that the chair tips. Coffee spills (`StainComponent` event). Three nearby NPCs see it. The Climber is mortified — they were trying to be friendly. The Newbie is mortified for the same reason. The Climber's `Trust` toward Newbie spikes (vulnerability shared); the Newbie's `Suspicion` toward Climber spikes (perceived ambush). Their relationship just got more interesting in two opposite directions simultaneously.

---

## 3. The Mouse in the Supply Closet (slow-burn vermin)

### The human truth
The mouse isn't the spider. It moves through the office over days. Evidence appears — droppings, gnawed corners, a scratching sound at night. The collective consciousness of the office gradually acknowledges its existence. Some NPCs care intensely; others don't. A trap appears. Then maybe another. The mouse is sometimes seen, then not for two weeks. Eventually it's caught or it isn't.

### What it does
A long-lived `RodentInfestationEntity` with state: `dispersed evidence`, `confirmed presence`, `multiple sightings`, `caught`, `escalated to pest control`.
- Evidence: small `EvidenceComponent` entities (droppings, gnaw marks, food-bag holes) that spawn periodically in low-traffic zones.
- Sightings: rare, disproportionately impactful — a single sighting fires a `SightingEvent` to all in awareness range and seeds gossip via the proposed RumorSystem.
- Trap mechanics: an NPC takes a `setTrap` action; the trap is a placed entity that resolves probabilistically over time.

### Components / data
- New: `RodentInfestationComponent { State: enum, FirstEvidenceTick: long, ConfirmedSightings: int, CaughtTick: long? }`
- New: `EvidenceComponent { Kind: enum, Location: tile, NoticedBy: HashSet<NpcId> }`
- New: `MouseTrapComponent { Placed: tile, BaitState: enum, TriggerProbability: float }`

### Mechanics
- The Cynic notices droppings and doesn't tell anyone for three days. The Climber sees a sighting and is the first to escalate. The Old Hand has handled mice before and quietly manages it; the Newbie is terrified.
- The trap is itself a social object. Whose desk it goes near matters. A poorly-placed trap snaps on someone's tie or shoelace and that's a story for game-weeks.

### Extreme end
A mouse lives behind the IT closet for six in-game weeks. Greg knows. He has named it. He doesn't tell anyone. The mouse appears one day in the breakroom during lunch. Twelve NPCs scream. Greg, walking in casually, says its name out loud. Everyone learns simultaneously that *Greg has known and named the mouse for six weeks*. This is one of the most character-revealing moments the engine could produce.

---

## 4. The Fly That Won't Leave

### The human truth
Singular fly. Inside. Bouncing against the window for forty minutes. Some NPCs ignore it indefinitely. Some try to herd it out. Some try to swat it. The collective attention of the room slowly orients around the fly. Productivity drops. Someone eventually gets up.

### What it does
A `FlyEntity` with a wandering AI bound to bright windows and warm bodies. Distance-attenuated noise. A continuous low-grade `irritation` injection on NPCs in the same room.

### Components / data
- New: `FlyComponent { Energy: float, BoundToWindow: EntityId? }`
- Reuse: existing irritation drive accumulator; existing `NoiseEventComponent` from the proposed NoiseSystem

### Mechanics
- The kill action requires line-of-sight + a brief swat targeting; success is probabilistic.
- The Conscientious NPC is most likely to get up. The Crass NPC is most likely to swat dramatically with a rolled-up document, missing repeatedly. The Hermit (Greg) opens a window and shoos.
- Multiple flies in a day produce a `FlyDayTag` on the room — a passive small irritation modifier, until cleaned.

### Extreme end
A fly lands on the conference room CEO's plate during the lunch meeting. The CEO doesn't notice. Three executives in a row notice and don't say anything. The Newbie, attending their first all-hands as a presenter, sees it. They have a decision. They say something. The CEO reacts. The Newbie's status either rises a lot or falls a lot depending on how the CEO took it. The system produced a character-test moment from one fly's wander path.

---

## 5. The Wasp at the Window (delicate hostage situation)

### The human truth
A wasp inside is a slow-motion crisis. Everyone knows the threat. Nobody wants to confront. The wasp's path determines the office's path — desks are vacated, meetings relocated, the room becomes a no-go zone. Eventually someone with a magazine handles it or it leaves on its own.

### What it does
A specialized vermin with elevated phobia coupling AND a sting-cost that's not just startle:
- A wasp-sting event has a real biological consequence (mild swelling = small movement-speed reduction for hours; in rare cases a known-allergy NPC has a much more serious response — engaging the existing `LifeStateComponent`).
- The sting is rare and its possibility is what shapes the *avoidance*.

### Components / data
- New: `AllergiesComponent { BeeStingSeverity: int, FoodAllergies: List<string>, EnvironmentalAllergies: List<string> }`
- Reuse: `LifeStateComponent` for severe-allergy edge case

### Mechanics
- The known-allergy NPC must leave the room — this is a high-information action that other NPCs notice and update on.
- The hero who handles the wasp gets disproportionate status. The hero who *fails* (gets stung) gets sympathy + a bump from the survival.

### Extreme end
A wasp is in the conference room mid-presentation. The presenter has a known severe allergy (their `AllergiesComponent.BeeStingSeverity` is 90). They calmly excuse themselves. A junior NPC, unaware of the allergy, makes a joke. The presenter does not return. The junior's status drops across the room invisibly, because everyone except them knew. The senior NPC who handled the wasp is now the unspoken hero. Three intersecting social-vector changes from a single insect.

---

## 6. The "Something Crawled On Me" Phantom

### The human truth
The brush of a hair on the neck that you swear is a spider. The piece of dust that fell on the arm. The static electric tickle. The reaction is identical to an actual vermin moment, but the source is ambiguous, and the embarrassment of having flailed at nothing in front of a coworker is its own micro-event.

### What it does
A low-probability `PhantomTouchEvent` fires on idle NPCs based on personality (high neuroticism, high openness — imagination provides the rest). The reaction is a real `StartleEvent`. The witnesses register it as such. There is no vermin entity. The NPC has a moment of confusion and an awkward laugh.

### Mechanics
- The follow-up: high-conscientiousness NPC inspects their workspace, performs the cleaning ritual to make sure. Low-conscientiousness shrugs and moves on.
- The serial-imaginer (deal: "thinks they have spiders in their hair") accumulates a pattern that other NPCs notice and gently comment on.

### Extreme end
The Recovering archetype, three days into a particularly bad stretch, has a phantom-touch moment in front of the entire breakroom. They shriek. Nothing is there. Everyone is silent. The shame compounds the existing stress. They leave the room.

---

## 7. The Ant Trail

### The human truth
A line of ants from a windowsill to the breakroom, three days running, eventually traced to a single forgotten food item. The investigation, the indignation, the plate of evidence on the offender's desk.

### What it does
A trail entity that propagates from `food source` to `nest source` in tiles. Other NPCs see it, follow it, and the offending food's owner is identifiable (food entities carry `OwnerNpcId`). Public-shaming dynamics ensue. Per the existing PassiveAggressionSystem proposal, a note appears.

### Mechanics
- Calcify-prone moment — the ant trail discovery becomes part of office lore: "remember when [Newbie] left the half-banana in the drawer."
- The offending NPC's `Belonging` drives drop sharply for ~24 sim-hours; high-conscientiousness ones overcorrect with elaborate cleaning.

### Extreme end
The ant trail leads to Cubicle 12. *Mark's old cube.* It hasn't been used in months. Nobody understands. Investigation reveals a forgotten container of someone *else's* food shoved into the bottom drawer by a janitor weeks ago. The mystery seeds rumor and renewed trauma about Mark. Cubicle 12's persistent emotional charge gets refreshed. **The engine produces a Mark-Cubicle-12 narrative beat from an ant's path.**
