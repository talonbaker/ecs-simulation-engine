# 03 — Pests, Phobias, and Acute Reactions

> The thing crawls out of the fridge. The mouse runs across the breakroom floor. The bee is in the conference room. The presentation starts in three minutes. The NPC's cortisol does what cortisol does.

---

## What's already in the engine that this builds on

- Proximity system, line-of-sight, awareness range.
- `StressComponent`, `SocialMaskComponent`, `willpower`.
- `KnowledgeComponent`, `RumorSystem`.
- The named anchors — the fridge, the supply closet, the basement, the parking lot.
- The Big Five — `neuroticism`, `agreeableness`, `extraversion`.

---

## 3.1 — PestEntitySystem (the spider, the cockroach, the mouse, the wasp)

### The human truth

Pests are intermittent, low-probability, high-impact events that the building produces on its own schedule and that the office responds to as a *whole.* A cockroach in the breakroom is not one NPC's problem — it's the entire breakroom's problem. The screamer, the killer, the catch-and-release person, the I'm-leaving-for-the-day person, the never-seen-it person — they all reveal themselves in the same sixty-second event. There's almost no other simulation moment that draws out so much character at once for so little narrative cost.

### What it does

A pest is a small entity with its own short-lifecycle:

- `PestEntity` with `kind`, `position`, `velocity`, `visibility`, `lifespanTicks`, `fearScore`, `originRoom`.
- `kind` includes: `spider | cockroach | mouse | rat | wasp | fly | ant-trail | silverfish | moth | wolf-spider`. Easy to extend.
- `fearScore` — a per-kind base value (cockroach 70, wolf-spider 90, mouse 55, fly 10, moth 25, ant-trail 30, wasp 65). This is the *raw stimulus* before per-NPC modulation.

Pests spawn rarely (low spawn probability per sim-day), with weighted location probability — cockroaches are most likely to spawn near the Fridge or the Microwave (per existing world-bible anchors); mice in the basement and the supply closet; spiders anywhere with low light; wasps near windows in summer.

When a pest spawns, no NPC has noticed it yet. It moves until an NPC's *line-of-sight* sweeps over it (the aesthetic bible's facing-direction system). On detection, a `PestSightingEvent` fires.

### The reaction calculation

When NPC X detects a pest of kind K:

```
reactionScore =
   pestKind.fearScore
   * (1 + 0.5 * neuroticism)
   * phobiaMultiplier(X, K)
   * (1.0 - composureModifier(X))
```

Where:
- `phobiaMultiplier` checks whether the NPC has a registered specific phobia of K (see PhobiaComponent below). 1.0 if none; 2.5–4.0 if yes.
- `composureModifier` reads `willpower / 100` plus `mask / 100` plus a small `extraversion` bonus (extroverts perform composure for the audience).

Reaction *categorical* outcome from `reactionScore`:

- **0–25 — `notice`.** A glance. The NPC may swat at it (fly, moth) or step around it. No proximity broadcast.
- **25–50 — `flinch`.** Small visible startle. NPCs in awareness range see the flinch and look in the same direction. Often this triggers their own `PestSightingEvent` (reaction cascades).
- **50–75 — `verbalAlert`.** Audible exclamation. Proximity event broadcasts to room range. Other NPCs orient.
- **75–95 — `scream`.** Loud audible event broadcasts to floor range. Stops conversations elsewhere. NPCs path toward the source.
- **95+ — `panicResponse`.** Scream + flight or freeze. NPC leaves the room or stands frozen. Mask collapses to 0. May trigger a `FaintEvent` if `neuroticism` extreme + thermal load + low blood sugar conditions met (cluster 2 cross-reference).

### The handler-NPC role

When a pest event has fired and is unresolved, NPCs in the building are evaluated for a `handler` role. The willingness to handle is computed from:

```
handlerWillingness =
   agreeableness  * 0.5
   + (1 - phobiaMultiplier) * 0.3
   + status_drive * 0.2  (people perform competence to gain status)
   - currentFlow * 0.4   (don't break flow for this)
```

The first NPC whose willingness crosses threshold *and* who is in pathing range becomes the handler. They walk over. They handle the pest. The handling style varies:

- **Stomp** — direct, satisfying, leaves a mark (an `OdorComponent` event, visual stain on the floor that calcifies — see cluster 04). The room reads stomping as competence + slight alpha-male energy.
- **Catch and release** — high-agreeableness, often-female-coded, the mug-and-paper move to the parking lot. Reads as kindness; the released pest can re-enter the building (low-probability respawn).
- **Spray** — a chemical kill. Smelly, slightly disturbing, leaves the room toxic for ~10 sim-minutes (NPCs avoid it).
- **Wait it out** — the handler stands near the pest, waits for it to wander into a corner, addresses it there. Deliberate, slow, often Hermit-coded.
- **Trap** — for mice/rats. Sets a trap entity that lives in the world for days; eventual outcome is a `TrapResultEvent` (caught vs. abandoned vs. moved). Old Hand specialty.

### After the event

- The room carries a `PestSightingMemory` flag for some sim-time. NPCs entering check the memory and *expect* a recurrence; their composure starts slightly lower in this room for a while.
- Rumor propagates: the cockroach in the breakroom is *the* breakroom story for the rest of the day. KnowledgeComponent transfer fires across proximity events for the next several hours.
- Specific NPCs may install or strengthen a `phobia` inhibition class as a result of the experience (an NPC who saw a roach climb onto their food now has elevated phobia toward roaches).

### Components

- `PestEntity` — the pest itself.
- `PhobiaComponent` on NPCs — `phobias[]` of the shape `{kind: PestKind | other, strength: 0..100, sourceMemory: TickStamp}`.
- `PestSightingEvent` and `PestHandledEvent` on the proximity-event bus.
- `PestSightingMemory` on room entities (decays over sim-days).

### Personality + archetype gates

- The Newbie's first office cockroach is a high-noteworthiness day-of event.
- The Old Hand has handled cockroaches before. They handle this one calmly; their `status` drive bumps slightly because the office sees calm competence.
- The Cynic announces it without panic. "Cockroach. Big one. By the fridge."
- The Climber must *appear* unfazed. If they have a true phobia underneath, the willpower draw to perform composure is significant. Mask drops accordingly.
- Greg in the IT closet has seen things in there. Greg handles the wolf-spider himself without telling anyone. The chronicle records nothing.
- The Founder's Nephew finds it funny.

### Cross-system interactions

- **OdorComponent:** stomped pests leave a faint smell entity that lingers.
- **KnowledgeComponent + RumorSystem:** the pest event is high-confidence first-hand information that spreads with calcification ("The Cockroach Incident" becomes a referenced phrase in the dialog corpus per cluster 16).
- **AppearanceComponent:** scream-fainting events damage appearance for the rest of the day (disheveled).
- **PassiveAggressionSystem:** notes about the fridge (CLEAN OUT YOUR FOOD, THE COCKROACHES ARE HERE BECAUSE OF YOU) are a known follow-up.

### What the player sees

Rare, high-energy moments. Three or four pest events per game-week, most resolved in seconds, one or two memorable. The most memorable ones become world-history.

### The extreme end

A cockroach the size of a thumb crawls out of the fridge during the Friday morning all-hands. Donna sees it first. She screams. Three other NPCs pivot, see it. The Climber is mid-presentation. The cockroach moves toward the conference room. The Climber's mask draws hard — they keep talking. Greg, who's been in the back of the room out of obligation, *handles it* — not stomp, not spray; he scoops it into his coffee cup, walks it to the door, releases it into the hallway. The presentation continues. Nobody acknowledges what just happened. Greg's `status` drive bumps in everyone's recorded perception of him. He sits back down. The chronicle records it: *Greg saved the all-hands.* Greg himself didn't notice that he did anything.

### Open questions

- Pest persistence: should a cockroach the office failed to handle continue to live in the floor and re-emerge weekly? Possibly — a `PestColonyComponent` per floor that accumulates from un-handled sightings could produce *infestations* as a slow-burn arc. (Risk: tone. The early-2000s office is grimy but not horror-grimy. Recommended: yes, but with a low cap and a player-visible "exterminator called" event that resolves it.)
- Are pests *seasonal*? Mice in winter, wasps in summer, ant-trails in spring? Yes, easy with the existing simulation calendar.

---

## 3.2 — PhobiaComponent + specific phobia mechanics

### The human truth

Beyond pests, NPCs can be afraid of specific *things* — heights, enclosed spaces, blood, needles, fire, public speaking, dogs, being-the-center-of-attention. Most phobias are dormant for most of an office life. When the trigger lands, behavior changes drastically and the office sees a side of the NPC they didn't know existed.

### What it does

`PhobiaComponent` carries 0–3 phobias per NPC, drawn from a phobia catalog:

- `arachnophobia | entomophobia | musophobia | claustrophobia | acrophobia | hemophobia | trypanophobia | pyrophobia | cynophobia | glossophobia | agoraphobia | emetophobia | thanatophobia`
- Plus a free-form `custom` slot for character-specific deal-tier phobias (the NPC who can't be in a room with a balloon; the NPC who can't open mail).

Each phobia: `{class, strength, knownToSelf, knownToOthers}`. Like inhibitions, phobias have an awareness field — a `glossophobia` (public speaking fear) NPC who is *aware* of it manages around it; one who *isn't* aware genuinely doesn't understand why they sweat in meetings.

When a triggering stimulus is detected (a spider, a closed elevator, a needle in a first-aid kit, a candle), the relevant phobia fires a `PhobiaTriggerEvent` with severity proportional to phobia strength × stimulus intensity. The reaction palette is shared with the pest reaction system: notice / flinch / verbalAlert / scream / panicResponse.

### The cross-cluster crossover

- **Glossophobia** triggers in any meeting where the NPC is asked to present (cluster 11's power dynamics put NPCs in this position involuntarily). The performance task imposes both a `WorkloadComponent` requirement and a `PhobiaTriggerEvent` ongoing for the duration of the speaking.
- **Claustrophobia** triggers in elevators (cluster 13's elevator scene), small offices, certain meeting rooms when crowded.
- **Agoraphobia** is a baseline that affects parking-lot transitions (cluster 18 in `new-systems-ideas.md`); affected NPCs path more carefully through open spaces, take longer at the threshold.
- **Emetophobia** (fear of vomit/illness) interacts with `IllnessSystem` — the affected NPC avoids the office plague harder than other NPCs and is more likely to stay home.
- **Hemophobia / trypanophobia** trigger at first-aid events (cluster 09's on-premises medical events), at the visible-injury cluster (2.8).

### Personality + archetype gates

- The Cynic is the rare archetype with low phobia rolls — they've been desensitized.
- The Newbie has the highest probability of a hidden phobia surfacing in their first month — the office contains stimuli they didn't anticipate.
- The Recovering archetype's specific recovery often correlates with a related phobia (recovering alcoholic with hemophobia, recovering from a public failure with glossophobia).

### What the player sees

Most saves: a small handful of `PhobiaTriggerEvents`, mostly mild, mostly handled in private. Occasionally a *visible* phobia event — the NPC who freezes in the elevator, the NPC who refuses to give the presentation when their boss asks, the NPC whose sleeve rolls up and reveals they have a band-aid because they just had blood drawn at lunch.

### The extreme end

The fire alarm goes off in the building (cluster 09 / cluster 18 — emergency drill or the real thing). One NPC has hidden pyrophobia — strength 80, awareness `hidden`. The full panic response fires. They run. They don't stop in the parking lot — they keep walking, off the lot, down the block. They can't say why. Their coworkers see it and *don't* know what to do. By Monday they're back at their desk and nobody asks. The chronicle records the day. Their KnowledgeComponent acquires a public-knowledge entry (`pyrophobia: visible`). The relationship matrix shifts in small ways — some NPCs avoid them; some bond with them; the Old Hand says nothing and quietly lets them have first dibs on the desk farthest from the kitchen.

---

## 3.3 — PanicAttackSystem

### The human truth

A panic attack is biological, observable, and often unrelated to whatever is *literally happening* at the moment it fires. Heart rate spikes; breath shortens; vision narrows; the sense of needing to leave is overwhelming. Many adults have them; almost nobody talks about them. They happen in meetings, on the phone, at desks, in bathrooms. The aftermath is recognizable to anyone who has had one: shaky hands, an emotional crash, a need for privacy.

### What it does

A panic attack is a `PanicAttackEvent` that fires on an NPC under specific conditions:

- **Trigger.** Multiple existing engine signals must align: `StressComponent` very high (chronic > 70 AND recent spike), `SocialMaskComponent` very low (< 25%), willpower current near zero, a triggering stimulus (a hard email, a confrontation, a specific anniversary, a phobia hit). Probability scales with the alignment.
- **Onset.** Over ~30 sim-seconds, the NPC's visible signals shift: posture closes, movement freezes, facing direction breaks (no eye contact), respiratory pattern (modeled abstractly as a `FocusDisruption` rate spike) intensifies.
- **Peak.** ~2–5 sim-minutes. NPC cannot path-plan, cannot respond to proximity events, cannot accept new task commands. Other NPCs reading the state may approach (high-agreeableness) or back away (low). The NPC is essentially `frozen`.
- **Resolution.** Either the NPC reaches a refuge space (bathroom, car, parking lot, smoking bench) where willpower recovers fast, OR another NPC's intervention helps (a quiet voice, a glass of water, walking them outside), OR the attack runs its course (~5–15 sim-minutes).
- **Aftermath.** Mask is at 0 for ~30 sim-minutes after. Willpower current is at 0. Productivity for the rest of the day is severely degraded. Other NPCs who witnessed the event update their KnowledgeComponent with the fact ("she had what looked like a panic attack today") at confidence proportional to their proximity.

### Components

- `PanicAttackComponent` (transient — exists only during an active event): `phase: onset | peak | resolution`, `triggerStimulus`, `startTick`, `interventionApplied: bool`.

### Personality + archetype gates

- High `neuroticism` is the necessary baseline.
- The Recovering archetype, the Affair archetype, and any NPC with a `vulnerability` inhibition above 70 are the most likely candidates.
- The Cynic, the Old Hand, and the Founder's Nephew approximately do not get panic attacks at all.

### Cross-system interactions

- **EavesdropSystem:** the bathroom is the most common refuge during an attack; the partial occlusion model means other NPCs sometimes hear the attack without seeing it.
- **MeetingSystem:** the meeting-during-attack is one of the most dramatic scenarios. Captivity prevents refuge. The NPC asks to be excused, or doesn't, depending on inhibition class.
- **FavorLedgerComponent:** an NPC who walked an attacking NPC outside builds a high-value favor that is rarely called in directly but produces ambient warmth for years of save time.

### What the player sees

Rare. Most saves: zero or one panic attack. Some saves: an arc where one NPC is having them with increasing frequency under sustained pressure. The visible signal is unmistakable; what other NPCs do with it is the dramatic content.

### The extreme end

The Climber, in a board meeting, mid-presentation, has their first office panic attack. The setup: three weeks of chronic stress, an affair that's just been discovered (private knowledge), a quarterly review that morning. The trigger: a slide that shows a number they can't justify. Onset starts at minute 14 of the presentation. They can't speak. The room watches. The Old Hand, who's been on the board, takes over the slide deck without comment, walks the room through it, lets the Climber sit. The meeting concludes. The Old Hand never references it. The Climber's `affection` toward the Old Hand jumps thirty points; their `status`-debt to them is permanent.

---

## 3.4 — FaintingEvent

### The human truth

People faint. Not often. Heat, low blood sugar, a sudden shock, a vasovagal response to needles or blood. The fall is the visible event; the *moments before* — the swaying, the pale face, the "I need to sit down" — is the readable lead-up that other NPCs may or may not catch.

### What it does

A `FaintingEvent` fires under one of several conditions:

- **Vasovagal:** stimulus from a hemophobia/trypanophobia trigger when phobia strength is high.
- **Heat:** when `ThermalStateComponent.CurrentPerceivedTemp` is far above comfort range AND the NPC has been moving + low hydration.
- **Hypoglycemic:** when `FeedingSystem` hunger is at extreme AND a stress spike + standing-too-long aligns.
- **Shock:** the spider crawled across their face. The cockroach was inside their lunch. Aligns with cluster 03.1 panicResponse outcomes.

The faint has a brief warning window (~10–30 sim-seconds of `pre-faint` signals — pale `AppearanceComponent`, slowed movement, looking-for-a-chair behavior). Then the fall: an audible event, a visible drop, the NPC is `unconscious` for some duration (~10–60 sim-seconds), then groggy recovery.

### Witness response calculation

The fall is the highest-attention event the simulation can produce. Almost every NPC in line of sight redirects:

- **Catch-them-on-the-way-down.** High-agreeableness + close proximity + high `physical` capability score (a personality variant). Dramatic. The catcher and the caught form a relationship event.
- **Run-toward-and-help.** Most agreeable NPCs default here. Group of 2–3 forms.
- **Call for help.** The Newbie usually defaults here.
- **Freeze.** High-neuroticism witnesses freeze. Cost: willpower draw to recover composure.
- **Continue what they were doing.** Almost nobody, but the Founder's Nephew and the deeply-burnt-out Cynic occasionally do this. It is read as monstrous.

### The aftermath

- The fainted NPC has reduced productivity the rest of the day. They go home if `agreeableness`-positive coworkers can convince them.
- The witnesses who responded build favor balance with the fainted NPC.
- The chronicle records it permanently.
- Some witnesses develop a small phobia or `vulnerability` inhibition increase (the visible-fainting moment imprinted).

### What the player sees

Rare. Mostly the warning window — the NPC who looked pale all morning. The actual fall is one of those moments the player remembers from the save.

### The extreme end

The Recovering archetype, in withdrawal from quitting something, hasn't eaten since yesterday because their appetite is gone, is in the conference room for a meeting on a hot day. Pre-faint signals fire in minute 8. Nobody sees the warning because everyone's looking at the screen. They go down at minute 14. The Hermit, who happens to be in the meeting because of an IT issue, catches them. The meeting ends. The Hermit walks them outside. They sit on the smoking bench together for fifteen minutes in silence. When they go back inside, the Hermit's `affection` toward the Recovering archetype is permanently +25; the relationship pattern shifts from `colleague` to `confidant`. The Hermit will not tell anyone about it. The Recovering archetype barely remembers the fall. They will remember the Hermit forever.

---

## 3.5 — Phobia-of-people / specific-NPC-aversion

### The human truth

Some NPCs are afraid of *specific other NPCs* — not in a phobia-DSM sense, but in the everyday sense that one person's presence makes them feel small, watched, unable. The boss they can't speak to. The coworker who reminds them of their abusive ex. The peer who outshines them and triggers the scarcity reflex. Their presence is a stimulus.

### What it does

`AversionLink` is a directed, asymmetric edge between two NPCs that adds to the standard relationship matrix:

- `source` → `target`, with `strength` (0–100) and `awareness` (known to source / hidden).
- When the target enters source's awareness range, source's `irritation`, `suspicion`, or `vulnerability` (depending on flavor) bumps. Mask draws.
- When the target is in conversation range, source's vocal register may flip (formal where they were casual; clipped where they were chatty).
- High-strength aversion + high target-stake (e.g., target is the boss) produces *phobia-class* avoidance behaviors — the source paths around the target, finds reasons to leave the room when the target enters.

Aversion is silent by default. The target rarely knows; the source rarely tells anyone. Two NPCs can have mutual aversion without either ever realizing the other reciprocates.

### Components

- `AversionLinkComponent` on source NPCs, list of `(targetId, strength, awareness)`.

### Cross-system interactions

- **Proximity / pathing:** an aversion-active NPC reroutes hallway paths when target is in line. This is an observable effect — the player notices that NPC X never walks past NPC Y's desk.
- **MeetingSystem:** when source and target are both required at a meeting, source's stress climbs through the meeting and mask drops faster.
- **FavorLedgerComponent:** an NPC who *un*-aversions the source by repeated low-stakes positive interaction creates an unusual emotional debt — the source feels they've been done a kindness but can't quite explain what.

### What the player sees

A small floor-pattern: NPC X always seems to be busy when NPC Y is in the breakroom. They don't sit at the same lunch tables. When Y is on vacation, X looks slightly lighter for the week. The player infers the dynamic without it being named.

### The extreme end

The Newbie has a hidden aversion link to the Climber from week one — something about the Climber registers as a threat the Newbie can't articulate. Strength 60, awareness hidden. Over six game-weeks, every interaction between them costs the Newbie willpower they don't have. When the Climber finally compliments the Newbie's work directly, the Newbie cries at desk (cluster 02.7). The Climber doesn't know what they did. The Newbie doesn't either. The save now has a story about *why one person's kindness can be unbearable.*
