# 04 — Fastidious vs Slob (and the Spectrum Between)

> The user's specific framing: "imagine what would it be like if you are very fastidious and you sat next to someone who was an extreme slob? How would you feel?" This cluster covers tidiness as a personality dimension, contamination spheres, territory disputes, and the structural conflicts that arise from putting opposite ends of the cleanliness spectrum next to each other every day.

---

## 1. TidinessSystem + TidinessTraitComponent

### The human truth
Some people cannot focus when their desk is messy. Some people cannot find anything when their desk is tidy. The two are physically incompatible coworkers, and the office layout decides the daily cost they pay.

### What it does
A new dimension on personality (or a deal-catalog-style trait): `TidinessAxis` from -2 (extreme slob) to +2 (extreme fastidious). It governs:
- Per-second drift on workspace tidiness (slob accumulates clutter; fastidious actively cleans)
- Tolerance for ambient disorder in the room (fastidious takes ongoing irritation from ambient mess; slob is unaffected)
- Tolerance for *other people's* mess

### Components / data
- New: `TidinessTraitComponent { Personal: int (-2..2), TolerancePersonal: int, ToleranceShared: int }`
- New: `WorkspaceTidinessComponent` per cubicle: `Tidiness: int (0..100), AccumulatorRate: float, OwnerNpcId: NpcId`
- Reuse: `MoodComponent` (irritation, disgust); existing irritation accumulator

### Mechanics
- A high-fastidious NPC's workspace tidiness stays high (active cleaning costs willpower and time, eats into work-progress).
- A low-tidy NPC's workspace tidiness drifts down naturally; visible clutter accumulates over days; eventually crosses thresholds that make adjacent NPCs uncomfortable.
- The cube boundary is permeable — the slob's clutter spills onto the shared aisle, into the shared armrest, into the shared corner of desk against the wall.
- The fastidious NPC clean-aggressively-into-other-territory: they re-organize the shared shelf without asking, which is a small `controlling` event that the slob registers as territorial violation.

### Extreme end — the seating disaster
The seating chart pairs Greg's old cube neighbor with the Newbie. Greg is a +2 fastidious; the Newbie is a -2 slob (deal: "lives out of fast-food bags"). Day one: clutter on the shared aisle. Day three: Greg has reorganized it once. Day seven: a passive-aggressive note on the Newbie's monitor. Day ten: a real conversation that goes badly. Day fourteen: a seating-change request that the player can either approve or deny. *This is the player's first really meaningful management decision and it's caused by a mismatch in tidiness alone.*

---

## 2. ContaminationSphereComponent (the fastidious POV)

### The human truth
The fastidious have an invisible boundary around them that other people's stuff cannot cross. The colleague who eats over their keyboard. The colleague who licks fingers and turns pages. The colleague who reaches across to grab a pen without asking. Each is a contamination event that produces visible flinch.

### What it does
A `ContaminationSphereComponent` per high-tidy NPC. Defines:
- A radius around their workspace where other NPCs' actions register as `contamination events`.
- A list of triggering actions (eating over surface, finger-licking, sneeze-without-cover, drink-spill, hand-to-face-then-touch).
- Per-event reactions: a brief mood spike (disgust + mild irritation), a visible flinch (movement event), a `cleanLater` task added to the NPC's queue.

### Components / data
- New: `ContaminationSphereComponent { RadiusTiles: float, Triggers: enum flags }`
- Reuse: existing `MoodComponent`, the proposed `WorkloadComponent` (cleanLater task)

### Mechanics
- The fastidious NPC's day has a continuous baseline `contamination irritation` that scales with proximity to other NPCs and event frequency.
- A bottle of hand sanitizer on the fastidious NPC's desk is a status-signaling object. They use it after every contamination event. The cube neighbor reads the use-pattern and registers it (sometimes as judgment).
- The slob's tolerance for the hand-sanitizer ritual is mostly amused. Until it becomes pointed.

### Extreme end
The fastidious NPC eats their packed lunch with formal silverware on a clean napkin. The slob next door eats a sandwich with both hands while typing. Crumbs land on the keyboard. The fastidious NPC uses sanitizer 6 times during lunch. The slob notices and grins. The fastidious NPC's `Suspicion` toward slob spikes. The slob's `Affection` toward fastidious slowly decreases (they feel judged). Both registered the same event entirely differently.

---

## 3. CleanRotationSystem (whose turn is it)

### The human truth
The cleaning of the breakroom microwave. The dishes piling up in the sink. The garbage bag that won't quite fit. These are unowned chores and they trigger one of the most universal office dynamics: the silent ledger of who-cleaned-last and the simmer of who's-dodging-it.

### What it does
A `SharedChoreComponent` on certain world objects (microwave, sink, fridge, breakroom counter, conference-room-trash). Each tick the chore accumulates "dirtiness" or "obligation." A `CleanRotationLedger` tracks who has cleaned each object recently. NPCs with high tidiness will clean unprompted; low-tidiness NPCs will not, regardless of accumulated debt.

### Components / data
- New: `SharedChoreComponent { Object: EntityId, Dirtiness: int, AcceptableThreshold: int }`
- New: `CleanRotationLedgerComponent` (one per world object): `LastCleanedBy: NpcId, CleanCount: Dictionary<NpcId, int>`
- Reuse: passive-aggression for the next-step

### Mechanics
- The over-cleaner accumulates positive ledger entries that they secretly resent. Their `Suspicion` toward never-cleaners ticks up daily by tiny amounts.
- A note on the offending object is the canonical passive-aggression manifestation.
- The ledger is queryable by the player via the inspector — the player can see the under-/over-contributing pattern and use it as a management input.

### Extreme end
The microwave-rotation conflict has been simmering for two months. The Old Hand has cleaned it eleven times. Greg has cleaned it once. The Climber has cleaned it zero times because they don't use the microwave. The Old Hand finally puts up a chore-list with names. People are outraged. The Climber is *especially* outraged because they don't even use it; they don't see why their name belongs. The chore-list comes down. The microwave doesn't get cleaned for two more weeks. The Old Hand cleans it again. **Office-eternal-recurrence achieved.**

---

## 4. WhoseMugIsThisSystem (object ownership in shared space)

### The human truth
The mugs in the breakroom cabinet. Some are personal property. Some are office-pool. Some are claimed-but-not-officially. Theft of someone's mug is a microscopic crime with disproportionate emotional weight. The lost mug is mourned; the discovered-in-the-wrong-cube mug is a small *crime scene*.

### What it does
- World objects can have `OwnerNpcId`, `OwnershipPublic: bool`, `Sentimental: bool` flags.
- A claimed mug used by a non-owner produces a `Theft` event. Theft of a non-sentimental object is a soft `Irritation` bump on the owner; theft of a sentimental object is a real `Suspicion` and `Anger` bump.
- Recovered objects produce a `relief` mood spike; never-recovered objects produce a long-decay `Sadness` baseline.

### Components / data
- New: `OwnedObjectComponent { OwnerNpcId: NpcId, OwnershipPublic: bool, Sentimental: bool }`
- New event: `OwnershipViolation { violator: NpcId, object: EntityId, magnitude: int }`

### Mechanics
- The mug taken to a meeting and forgotten on the conference table for two days. The mug's owner is searching. Other NPCs walk past it. Whether they bring it back is a `helpfulness` test.
- The favorite mug *broken* by another NPC is a high-noteworthiness event. The break is observable. The NPC who broke it has to decide: confess, replace, deny.

### Extreme end
The Cynic's WORLD'S BEST GRANDPA mug, given to him by his late father, has gone missing. He doesn't talk about it. Everyone knows it's missing because they know it's normally on his desk. Days pass. Greg, in the IT closet, finds it accidentally in a drawer when looking for a cable. He returns it, silently, to the Cynic's desk during off-hours. The Cynic finds it the next morning. He doesn't say anything. He never knows it was Greg. *He never finds out, ever, in the entire save.* Their relationship has shifted permanently. **The engine produced a perfectly silent kindness.**

---

## 5. Desk Plant Care (proxies for self-care)

### The human truth
The desk plant is a tiny ongoing commitment. Some NPCs have many. Some have none. The dying-plant moment is its own kind of public failure. The `keeping a plant alive` track is a known proxy for `keeping self alive` track and the simulation can read it as such.

### What it does
- Plant entities with `WaterLevel`, `LightExposure`, `Health` (0..100).
- Health correlates over weeks with the owner's average mood, stress, and willpower. The plant is a slow lagging indicator of internal state.
- A plant's death produces a mood spike on the owner, sometimes a `Grief` flag if it was long-cared-for.

### Components / data
- New: `PlantComponent { OwnerNpcId: NpcId, WaterLevel: float, Health: float, LastWateredTick: long, Species: string }`
- New: `PlantCareDealComponent { CarefulCarer: bool, Forgetful: bool }`

### Mechanics
- Plants benefit from the existing lighting system: a plant in a sunny corner thrives; a plant in the basement dies. This couples to existing systems beautifully.
- Plant-watering is a small daily ritual that's a willpower buffer — performing the ritual produces a tiny `competence` and `belonging-with-self` boost.
- Plant-killing-NPCs get an in-office reputation. The Climber's third dead succulent is noted by the office; their composure is mildly questioned.

### Extreme end
The Recovering archetype's desk has eight plants. They are visibly thriving. The plants are thriving because the Recovering is thriving — sober eight months, willpower steady. The Recovering relapses. Within two weeks, three plants have visibly declined. The cube neighbor notices the plants before they notice the person. They ask, casually, about the plants. The Recovering can answer the plant question in a way they couldn't answer "how are you." *This is the kind of indirect emotional probe the engine should support.*

---

## 6. The Smell-of-Lunch-at-the-Desk Problem

### The human truth
The fastidious NPC packs a salad. The slob heats up tuna casserole at their desk at 11:47am Tuesday. The cube row is now their cube row. The microwaved-fish situation is the canonical case but the desk-eat is its own specific subproblem.

### What it does
The desk-eat is a different action than the breakroom-eat: it bypasses the breakroom-microwave-smell propagation (the smell stays in the cube, more concentrated, smaller radius) but the cube neighbor takes the entire hit. Plus desk-crumbs (clutter accumulator), plus visible-eating-while-cube-neighbor-tries-to-work (focus disruption).

### Components / data
- Reuse: `OdorComponent`, `WorkspaceTidinessComponent`, the proposed `FlowStateComponent`

### Mechanics
- The desk-eat is selected by NPCs with low extraversion + high workload + the social barrier of eating-with-others-feels-wrong (the Hermit deal for sure).
- The desk-eater is *unaware* of how strong the smell is to a neighbor — the proximity asymmetry: the eater has olfactory adaptation, the neighbor does not.
- The neighbor's response: a tightening of the mask, an irritation accumulation, eventually a passive-aggressive note about "scent-free policy" or a real seating-change request.

### Extreme end
The Hermit (Greg) eats curry leftovers at his desk. The smell propagates to the IT-closet door. A passing exec is briefly hit. The exec has to stop in to ask Greg something. The exec lingers as long as politeness requires, which is shorter than Greg expects. Greg doesn't know why the meeting was so short. He suspects he's in trouble. He's not. He just smells like curry.

---

## 7. The Fastidious Boss Problem

### The human truth
A boss who is +2 fastidious is a special hazard. They walk by your cube. They notice the clutter. They say nothing. You feel the noticing anyway. The boss-walks-by event becomes a regular small-stress moment for slob NPCs.

### What it does
- A high-tidy NPC moving through low-tidy territory does an automatic `silent-judgment` event: they don't comment, but their `facing` and a slight pause-and-look register to anyone watching.
- The slob whose cube was scanned: a small `embarrassment` mood spike + small `anger-toward-self` accumulator.

### Components / data
- New event: `SilentJudgmentEvent { judge: NpcId, target: NpcId, observable: bool }`
- Reuse: existing `FacingComponent` already supports the look.

### Extreme end
The CEO walks the floor every Friday at 11am. Three slob NPCs spend each Thursday-evening anxiously straightening up. The CEO's silent-judgment is the *most powerful intervention in the entire game*, applied without ever directly speaking. **The engine should be able to produce this from a single weekly schedule entry on one NPC.**

---

## 8. The Slob's Defense (counterfastidiousness)

### The human truth
Slobs aren't just messy. The committed slob has a *politics*. They view the fastidious as repressed, as judgmental, as wasting their lives on aesthetic order. The slob's mess is sometimes performative — a small protest against the office's small dictatorships.

### What it does
- A `slobIsPolitical` deal: the slob actively *resists* cleanup pressure. Their cube gets messier in *direct response* to passive-aggressive notes. Threatened cleanup is itself a stressor that triggers more clutter as comfort.
- The Slob's `Suspicion` toward fastidious-NPCs is structurally elevated — they read tidiness signals as social control.

### Mechanics
- The cycle: passive-aggression note → slob-defense response → escalation → passive-aggression-note pile → seating-change request.
- The seating-change is a player decision point. The player must choose between siding with order or with creative-chaos. Both have downstream consequences.

### Extreme end
The slob is The Hermit. Their mess includes an entire shelf of obsolete computer parts that they actually use. A fastidious cube neighbor mistakes them for trash and throws some out. The Hermit, who never confronts, spends two days quietly reconstructing what was lost. The relationship is now permanently `Rival` with a side of `Suspicion`. The fastidious NPC will never know what they did.
