# 02 — Bodily-Function Taboos

> The body does what the body does. The office has agreed the body doesn't. The gap between those two facts is comedy, horror, and most of why office life is exhausting.

---

## What's already in the engine that this builds on

- `BladderComponent`, `ColonComponent`, `FeedingSystem` — biology already simulated.
- `OdorComponent` from the existing SmellSystem.
- `SocialMaskComponent` and `willpower` — the gate between body and visible behavior.
- `inhibitions` — `bodyImageEating`, `publicEmotion`, `physicalIntimacy`, `vulnerability`. New classes added below.
- The `KnowledgeComponent` — facts spread, including embarrassing ones.
- The Women's Bathroom as named anchor — the only place nobody can observe you.

---

## 2.1 — FlatulenceSystem (the fart, simulated)

### The human truth

Everyone farts. Office life has decided everyone doesn't. The gap costs willpower all day, especially on bad-stomach days, especially in meetings, especially for people whose desk sits in a low-ventilation pocket. The dread of an audible release in a quiet room is one of the most universally relatable office anxieties and has never been simulated in a Sims-descended game. Done with restraint, it is funny in exactly the dry-cynical register the world bible commits to. Done badly, it is a single joke that cheapens everything around it. The system should aim for the former.

### What it does

Two distinct events flow out of the existing `ColonComponent`:

- **Audible release** — discrete proximity event, intensity 1–3.
  - 1 (silent-but-detectable-by-self): no audio bus event; no proximity broadcast. The NPC *knows* it happened. Their `SocialMaskComponent` ticks down. They check the room.
  - 2 (audible at conversation range): proximity event fires to NPCs in conversation range. Their `KnowledgeComponent` records the fact with confidence proportional to certainty (some people swear they didn't hear what they heard).
  - 3 (audible at room range): the catastrophic version. Everyone in the awareness range knows. A narrative chronicle event almost certainly fires.
- **Olfactory release** — uses the existing `OdorComponent` mechanics, with `OdorType: silent-but-deadly`. Spawns on the NPC entity (not on the chair, not on the room), follows them for a short duration, propagates by the existing radius decay.

The two events can co-occur or fire independently. A silent-2 with a strong olfactory hit is the worst-case from the perpetrator's perspective: nobody knows it was them, they get to perform innocence, they have to *keep* performing innocence for the duration of the smell.

### Suppression as a willpower draw

`ColonComponent` urgency above a threshold creates a *suppression candidate.* The NPC chooses:

- **Release in private** — go to the bathroom, satisfy the urgency, return clean. Costs time + the bathroom-trip social signal (cluster 02.4 below).
- **Release at desk silently** — the silent-2 gambit. Low willpower draw to commit, high willpower draw to perform innocence afterward.
- **Suppress** — willpower drain proportional to urgency. If suppression breaks, the result is often louder than it would have been if released earlier. The over-pressurized release is a known life experience and the engine can produce it.

A `flatulence` inhibition class is added to the action-gating bible. Most NPCs run mid-strength on this. A few characters run high — the high-strength inhibited NPC suppresses past comfort and pays in willpower; their own physical discomfort feeds back into stress.

### Components

- No new component on the body side — `ColonComponent` already has urgency. Add a sub-event tag `Audible: 1|2|3` and `Olfactory: low|mid|high` to the existing release event.
- New inhibition class: `flatulence`.
- Existing `OdorComponent` handles the smell propagation.

### Personality + archetype gates

- The Cynic does not care. Releases at desk routinely. Has stopped paying willpower for it. Other NPCs adjust around them.
- The Climber spends extreme willpower keeping it controlled in front of superiors and is comparatively careless among peers.
- The Founder's Nephew does it loudly on purpose because they have nothing to lose.
- The Newbie is the most legibly miserable holding it in. Days at high stress are days they're double-suppressing.
- High `agreeableness` + high `neuroticism` produces the worst case — the NPC most likely to suppress past the point of comfort and most ashamed when they fail.

### Cross-system interactions

- **MeetingSystem / CaptiveComponent:** the high-stakes context. Captivity prevents release, urgency keeps climbing, suppression draw keeps draining willpower. Long meetings produce the highest probability of audible release; the moment after is a `noteworthiness: 90+` event.
- **PassiveAggression:** the joke-note on the conference room door that says PLEASE DO NOT BLAME THE COFFEE is one possible passive-aggressive output. There's no target — the office is the target — but it lands.
- **Stress + Bladder/Colon coupling (existing):** existing system already accelerates urgency under stress. This system now does something dramatic with the consequence.
- **KnowledgeComponent:** the audible-3 event is a high-confidence fact. Floor-wide. Everyone has it. The perpetrator knows everyone has it. The mask cost of being-the-person-who-did-that lasts days, sometimes a week, then decays. (See cluster 16's calcified callbacks — under the right NPC, this becomes an inside joke that sticks.)

### What the player sees

Almost nothing, almost all the time. Most flatulence events are silent-1 — invisible to gameplay. Occasional silent-2 olfactory hits land — the player sees three NPCs simultaneously notice something while the perpetrator is performing innocence; nobody says a thing. Once or twice per multi-week save, the audible-3. The chronicle remembers it forever.

### The extreme end

The 90-minute meeting (per the existing MeetingSystem's extreme end, where the HDMI doesn't work). At minute 78 the urgency crosses release threshold for one NPC. They suppress. At minute 81 their suppression willpower expires. Audible-3. Olfactory-3 lags by twenty seconds. Nobody laughs. Three people pretend to look at their notes. The meeting ends at minute 89 instead of 90. The perpetrator does not look at anyone for two days.

### Open questions

- Is this above the line on what the world bible's tone permits? The world bible commits to maturity and to depicting taboo subjects honestly without sensationalizing them. A restrained simulation — silent-1 events the player almost never sees, rare audible events with serious mask cost — fits. A constant comedic stream of fart events does not. The frequency-tuning curve is the load-bearing decision.
- Should the gendered version of this be different? In office norms it often is. Recommended: no — the engine is amoral and the social-cost curve handles it through `inhibitions` with a class field that doesn't reference gender.

---

## 2.2 — Burp, hiccup, sneeze, cough, yawn

### The human truth

The same shape as flatulence but more permitted. A burp is *almost* OK; it depends on volume and on whether it was followed by an "excuse me." A hiccup is a meeting-destroyer because the sufferer can't stop it. A loud sneeze is a shock event to everyone in range. A yawn is contagious and reads as a status signal — *you're boring me* — even when it isn't. A cough during the early-2000s is just a cough; during a flu wave it's an indictment.

### What it does

A small shared system, `InvoluntaryEmissionSystem`, handles all of these as variants of the same proximity-event shape with class-specific consequences:

- **Burp** — proximity event, mask draw on the perp if NPCs are present, small `irritation` drip on listeners, *negligible* if followed by an "excuse me" dialog moment (which most agreeableness-positive NPCs auto-fire).
- **Sneeze** — sharp proximity event with a *startle* signal on receivers. NPCs in close range get a momentary `FlowStateComponent` interruption *and* a small disgust input under any active `IllnessSystem` exposure context.
- **Hiccup** — runs as a *cyclic* event for some duration (30–180 sim-seconds). Each hiccup is a noise event. Nearby NPCs accumulate amusement initially, irritation eventually. The hiccupper's mask drops at each one. Some NPCs respond by trying to scare them (deal-catalog activity); others avoid eye contact.
- **Cough** — varies by `IllnessSystem` infection state. Healthy cough: ignored. Sick cough during a cold: triggers other NPCs to flag the cougher for `stay-home-evaluation` in their KnowledgeComponent.
- **Yawn** — *contagious*. A yawn within sight range (per the aesthetic bible's sight range proximity) has a probability of triggering yawns in other NPCs based on their fatigue level. The chain is observable and funny in the right register. A yawn during a meeting is a status-cost event — it's read as *you're boring me*.

### Personality and archetype gates

- The Hermit's cough is the only sound the office hears from the IT closet for hours. It carries information just from being unusual.
- The Climber suppresses yawns aggressively (mask draw) and reads other people's yawns as bad signs.
- The Newbie is the most contagious yawner — their yawns *catch* faster because they're also exhausted in the way new hires are exhausted.

### Cross-system interactions

- **IllnessSystem:** sneeze and cough are the dominant transmission events. Frequency of each event scales with infection state.
- **MeetingSystem:** hiccups and yawns are the worst captivity exhibits. A hiccup attack at minute 12 of a meeting is the kind of thing the office tells stories about for weeks.
- **DialogSystem:** the "excuse me" / "bless you" dialog fragments are a context the corpus needs (`acknowledge-emission` or similar). Calcify produces per-NPC tics — Linda's `"Bless you, hon"` becomes part of who she is.

### The extreme end

The Newbie gets the hiccups in their first all-hands. It lasts twelve minutes. Three people try the breath-hold suggestion. One person tries the surprise-them tactic and almost gets it. The CEO is on the call. The Newbie is mortified. By the end of the meeting two unrelated NPCs have caught hiccups (placebo / stress / suggestibility — model as a small probability per minute of exposure) and the meeting is functionally a write-off. This becomes "the hiccup meeting" in the office's calcified callbacks for the rest of the save.

---

## 2.3 — Body Odor and personal hygiene drift

### The human truth

The person who started smelling. The person who always smells. The person who used to and doesn't anymore (something at home changed). Office body-odor situations are uniformly handled by *not addressing them* until they're so bad someone has to. The eventual conversation is one of the most painful things an office requires of its members. Hygiene drift is a real-time readout of an NPC's mental state, and everyone reads it before they say anything.

### What it does

Extends the existing `OdorComponent` and `AppearanceComponent` to track *body-source* odor distinct from event-source odor (the microwave, the fridge):

- `BodyOdorState` on the NPC, derived from: time since shower (modeled as a sleep-restored counter that depletes during the day), recent physical exertion, stress level, wearing of the same clothes, time in heat (the AC-broken summer day from the existing ThermalComfortSystem).
- The body-odor `OdorComponent` propagates from the NPC. Its intensity is what the office reads.

### The conversation calculus

When `BodyOdorState` crosses a high threshold, NPCs in proximity accumulate a *concern flag* in their `KnowledgeComponent`. Most NPCs do nothing; the social cost of "saying something" is high. The system tracks a `someone-must-eventually-say-something` aggregate per NPC. When a sufficiently-positioned NPC (high agreeableness + close relationship + meaningful tenure) crosses an internal threshold, an offline-conversation event fires — no audible content, but the player can read its outcome the next day in the target NPC's appearance and demeanor.

### The mask connection

Hygiene drift is one of the *most legible* mask-failure signals. The NPC who's coming apart at home is also failing to shower, failing to wash the shirt, failing to address the smell. Coworkers reading the smell are reading the mask. This is part of what makes the system more dramatic than gross — the smell is information about a life.

### Personality + archetype gates

- The Cynic doesn't care about their own; cares minimally about others'.
- The Climber's hygiene is the last thing to go and goes only when *everything* else is gone, which makes the smell from a Climber a flashing alarm for the floor.
- The Recovering archetype, in a relapse, often has hygiene drift as the first visible sign — *days* before any other behavioral signal.
- The Hermit's hygiene is naturally lower than population norm and the office has accepted this; their drift is harder to read.

### Cross-system interactions

- **AppearanceDecaySystem:** already exists — body odor is the smell-axis of the same drift.
- **PassiveAggression:** notes about deodorant on the breakroom counter ("HELPFUL REMINDER 😊") are an attack pattern.
- **KnowledgeComponent:** hygiene drift is one of the most reliable ways the floor learns somebody is in trouble at home.

### What the player sees

A character whose `OdorComponent` intensity has been climbing for days. The NPCs around them visibly creating distance — adjusting cubicle pathing, eating lunch elsewhere, declining one-on-one meetings. They don't know why; they can't put it into words; their `irritation` is up around that NPC and they can't articulate the reason. The target doesn't know either. Then someone, eventually, says something — not visibly to the player, but readable through the next day's reset.

### The extreme end

A Recovering archetype slipping into relapse. Day-by-day hygiene drift across two game weeks. The floor watches without being able to articulate what they're watching. By week two, three coworkers have shifted lunch groups; one has filed a quiet complaint with HR. The intervention happens off-screen. The player can read it in week three's appearance reset and the small `affection` bumps from the NPCs who were paying attention.

---

## 2.4 — Bathroom etiquette and the bathroom as social space

### The human truth

The office bathroom is one of the few places in the building where the social rules become *more* strict, not less. Adjacent-stall avoidance. The unspoken rule about the middle stall. The encounter where two people who barely know each other are now standing two feet apart at adjacent sinks. The decision to wash hands or not when somebody is watching versus not. Going to a different floor's bathroom because the home floor's bathroom feels too exposed today. The bathroom is also the only refuge — the only room where mask is permitted to be off — so it's where crying happens, where panic attacks recover, where bad-news phone calls get answered.

The Women's Bathroom is already a named anchor in the world bible. The architecture is in place; the social mechanics aren't.

### What it does

`BathroomZone` is a sub-class of room with specific behaviors:

- **Stall occupancy state.** Each stall is an entity with `Occupied: bool`, `OccupiedBy: NPCId?`, `OccupancyStartTick`. Other NPCs entering check stall state and *avoid adjacent* if non-adjacent is available.
- **Sink encounter.** When two NPCs are at adjacent sinks, a forced micro-interaction event fires (similar to the elevator from cluster 13 — proximity + no escape route). Eye-contact discipline matters.
- **Hand-washing decision.** Each NPC has a `washHandsBaseline` (0–100). When alone, the actual washing probability scales with this. *When observed*, the probability shifts toward the social norm (~95%) regardless of baseline. The gap between baseline and observed-norm behavior is calcifiable into a known fact about the NPC ("she doesn't wash when nobody's looking, I've seen it" — high-noteworthiness, hard to act on).
- **Refuge function.** The Women's Bathroom is the named anchor where crying occurs. Per the action-gating bible, willpower regenerates faster in private. The bathroom is designated `refuge: true` per the aesthetic-bible's open question on annotated refuge. NPCs in mask-collapse states path here preferentially.

### Components

- `BathroomZoneComponent` on the room entity.
- `StallOccupancyComponent` on each stall entity.
- `BathroomVisitEvent` on the proximity bus (other NPCs sometimes notice the person leaving the bathroom looks different than the person who entered).

### The bathroom as KnowledgeComponent witness

When NPC A is crying in a stall and NPC B enters the bathroom, B doesn't see A but *hears* — partially-occluded audio, per the existing EavesdropSystem's occlusion model. B's `KnowledgeComponent` records "someone was crying" with the speaker not necessarily identified (voice recognition is an open question). B may or may not look at the stalls. B may or may not knock. Most of the time, B leaves quietly. The system commits to letting either response happen.

### Personality + archetype gates

- The Climber never cries in the bathroom. They go to their car (cluster 18 — threshold space).
- The Vent (Donna) cries in the bathroom often. The bathroom is part of her loop.
- The Hermit doesn't cry in the bathroom because they don't cry. They retreat.
- Hand-washing-baseline is `conscientiousness`-correlated but not perfectly — high-conscientiousness NPCs sometimes have a private ritual of *not* washing as a small rebellion.

### Cross-system interactions

- **EavesdropSystem:** the bathroom is the highest-yield eavesdrop location after the smoking bench. Most of what's heard there is mask-off.
- **SocialMaskComponent:** the bathroom permits mask off without willpower draw. This is why high-stress NPCs spend more time there than their bladder requires.
- **CaptiveComponent (meetings):** the bathroom is the legitimate escape from a meeting. The escape costs less status than other escapes because the body provides the cover.

### What the player sees

The named-anchor that's already in the world bible, now load-bearing. Donna goes there four times a day — twice for actual purpose, twice for refuge. The number of bathroom visits per NPC is a state-readout the player can learn to read.

### The extreme end

The Recovering archetype on a bad day, locked in a stall, having a panic attack. Another NPC enters, hears, doesn't know who it is, doesn't know what to do, leaves quietly. Six minutes later the Recovering archetype emerges with their mask reassembled, washes their hands, goes back to their desk. The other NPC, three desks away, never knows for sure who it was, has the suspicion they know, never confirms it. The KnowledgeComponent fact lives in their head for the rest of the save.

---

## 2.5 — MenstrualCycleSystem (the period as an office variable)

### The human truth

A real, common, monthly biological rhythm that is treated as if it does not exist by the office norms of the early-2000s setting. The full spectrum is real: cramps, fatigue, mood, the leak nobody-warned-them-about, the sweater-tied-around-waist, the sprint to the bathroom, the borrowed-tampon proximity micro-event, the day-three crash. The taboo is operating against actual physical experience and the willpower to perform unaffected costs measurable amounts. The office's failure to acknowledge this is a recurrent dramatic tension that the simulation can model honestly.

### What it does

For NPCs flagged as menstruating (a property assignable at spawn — most adult women, none for many other NPC classes; the property is data, not destiny), a 28-day cycle runs against the simulation calendar:

- **Days 1–2:** elevated `irritation` baseline, mild physical discomfort, supplies-needed flag. `WorkloadComponent` progress rate is reduced by a small percentage.
- **Days 3–5:** discomfort decays. Baseline normal.
- **Days 14ish (ovulation):** small mood lift. Rare.
- **Days 21–28 (PMS window):** mood baseline shifts, sleep quality dips, mild irritation increase.

Discrete events:

- **Onset event** — surprise vs. on-schedule. Surprise events trigger a supplies-emergency proximity moment.
- **Supplies-needed proximity moment** — the NPC pings nearby NPCs with a private request. This is a high-trust signal. The system tracks who got asked. The asker now owes a small ambient favor (per `FavorLedgerComponent`).
- **Visible-leak event** — rare. High mask-cost. `noteworthiness: 95+`. The NPCs in proximity who notice and *don't* say something accrue a small bond with the affected NPC even without acknowledgment.
- **Cramping event** — discrete pain spike during the day-1–2 window. NPC's posture changes; movement speed reduces; productivity drops.

### Components

- `MenstrualComponent` on flagged NPCs: `cycleStartDay`, `currentDayInCycle`, `severity` (mild / typical / severe — 60% / 30% / 10%), `lastSurpriseTick`.
- The supplies-emergency uses existing proximity + `FavorLedgerComponent`.

### Personality + archetype gates

- High `neuroticism` plus `severity: severe` is the most dramatic combination. Low `agreeableness` + `severity: severe` produces a day-one persona that other NPCs recognize and steer around.
- The Vent (Donna) is the most likely to *say* it's that time. The Climber would never. The Hermit doesn't talk about it but their week-shape is observably cyclical.
- The supplies-borrowing moment is an `agreeableness` × `trust` event. Borrowing from a low-trust NPC is itself a cost.

### Cross-system interactions

- **SocialMaskComponent:** the day-one mask draw is significant for severe-cycle NPCs. They may use the bathroom-as-refuge twice as often that week.
- **InhibitionSystem:** a `bodyExpression` inhibition class governs whether the NPC can ever name what they're feeling. High strength = invisible suffering all month. Low strength = openly mentions when she's on her period.
- **WorkloadSystem:** real measurable productivity dip during day-1–2. If a deadline lands there, the system has a built-in cost the player can observe.

### What the player sees

Most months: very little. A few days where the affected NPC moves slower, eats more, takes more bathroom trips. Rarely: an emergency-supplies micro-event that tells the player more about the relationship matrix than anything else that happens that week. Even more rarely: a leak event that the office handles or fails to handle as a character test.

### The extreme end

The Newbie's first office period, on a day she has to present in a meeting. Severity rolled severe. Cramping spike at minute 10 of the meeting. Mask draws hard. She has no one yet — the supplies-borrowing requires trust she hasn't built. The meeting ends. She sprints to the bathroom. The Vent finds her and lends her supplies, no words. The Newbie's `belonging` and `affection` toward the Vent jumps thirty points in one event. Their relationship pattern shifts — they were strangers; now they have *the thing nobody talks about, but in the warmer sense*. The chronicle records "the day Donna saved her."

### Open questions

- Tone discipline. This is a real lived experience for adults and the world bible commits to honest depiction. The risk is sensationalism. The mitigation is to keep the visible events rare, the language clinical, the dramatic load on the *help* and the *witness* rather than the body itself.
- Cycle mismatch with the simulation calendar — does the engine assume a 30-day month? Probably handle by running each NPC's cycle on a 28-day clock independent of the calendar.
- Is this the only system in this folder where biology that's genuinely sex-coded shows up? Yes. Other body-system proposals (pregnancy in cluster 15, prostate concerns in older NPCs as a future open thread) sit on the same flagged-attribute pattern.

---

## 2.6 — Stomach-noise (the audible hunger growl)

### The human truth

The body talks, audibly, in a quiet meeting, at the worst time. The growl that everyone hears. The face of the person whose stomach growled. The choice to cover with a joke vs. to perform innocence vs. to apologize. Tiny moment, universally relatable, never simulated.

### What it does

Existing `FeedingSystem` with hunger drive — when hunger crosses a threshold AND the NPC is in a quiet acoustic environment AND a probability roll fires, a `StomachNoiseEvent` propagates to NPCs in conversation range. The NPC carries the same kind of mask-cost that small-gauge taboos do.

### Personality gates

The Cynic plays it for laughs. The Climber suppresses panic-fast. The Newbie is mortified. The Old Hand jokes about it like a pro.

### What the player sees

Subtle. A small cluster of NPCs in a meeting all glance at the same person for half a beat, then look away. The meeting continues. The chronicle, depending on noteworthiness threshold, may or may not log it.

---

## 2.7 — Tears at desk (when the bathroom isn't available)

### The human truth

Crying at your desk is one of the most visible mask-failure events possible. The Climber dreads it. The Vent half-uses it. The crying-at-desk person becomes an instant subject of the office for as long as the crying lasts. Every NPC in line of sight has to decide what to do — approach? offer tissue? avoid? pretend not to see? Each option is a character test.

### What it does

When `SocialMaskComponent` is at extreme low (below 10%) and a sufficiently-charged event fires (a hard email, a hard call, a hard meeting interaction, anniversary effects), the NPC may cry at desk rather than reach the bathroom. This is *not* a bathroom-pathing failure — it's *the cry happening before they can move*.

The event fires the existing aesthetic-bible's emote-not-speak logic at maximum intensity:

- **Posture shift** — visible drop, head into hands, shoulders down.
- **Audio cue** — a small whisper-level audible event that NPCs in conversation range pick up but do not name.
- **Visibility radius** — bigger than normal proximity events because the NPC is *visibly* not OK from across the floor.

Other NPCs' responses vary by personality and relationship pattern:

- High `agreeableness` + close relationship → `approach: comfort`, which is a proximity event with an `affection` bump on both sides.
- High `agreeableness` + distant relationship → `approach: tissue-and-leave`. A single friendly act. A small `belonging` bump for the cryer.
- Low `agreeableness` → `avoid`. They don't look up. They wait it out at their own desk.
- The Cynic does the *most* humane thing accidentally — they make a small dry joke that lets the cryer save face. This is a known Cynic move.

### Cross-system interactions

- **Refuge / bathroom:** if the cryer reaches the bathroom in time, this event doesn't fire — bathroom permits mask off without public cost.
- **AppearanceComponent:** post-cry face is visibly different (red eyes, runny mascara). The visibility lasts ~30 sim-minutes. The floor reads it.
- **KnowledgeComponent:** the fact that NPC X cried at desk on Wednesday calcifies if it happens more than once. "She's been crying again" becomes a piece of state the office holds about her.

### The extreme end

The Climber cries at desk. Once. Ever. In a save. It is the highest-noteworthiness social event of the run. The chronicle remembers it forever; the office's relationship matrix shifts permanently around it; nobody references it directly again, but every NPC who saw it now treats the Climber slightly differently.

### Open question

Should the system distinguish *kinds* of cry (silent / audible / sob)? Probably yes — the silent cry is much more common than the sob, and the sob carries different social weight.

---

## 2.8 — Visible-physical-condition events

A short cluster of taboo physical events that already exist in the body and just need a `VisibleEvent` channel:

- **Nosebleed.** Small probability under stress + thermal extremes (cluster ThermalComfortSystem). High mortification. NPCs offer tissue or avoid; the bathroom-pathing probability spikes.
- **Vomiting.** Rare, illness-or-stress-driven. Usually-bathroom-pathed. Public vomit is one of the highest-noteworthiness events the engine can produce.
- **Falling / tripping.** Small probability anywhere stairs/parking-lot/uneven-flooring exist. Physical mortification. The Cynic plays it off; the Climber is destroyed by it.
- **Visible injury / cut / bruise.** Notable for what NPCs *don't* ask. The bruise that appeared overnight that nobody references.
- **Choking.** During lunch. Rare. High-stakes — a coworker's response (Heimlich vs. froze vs. helped) is character-defining.
- **Fainting.** Crosses with cluster 03's panic-attack mechanics; can be heat-driven (ThermalComfortSystem), shock-driven (the cockroach), or low-blood-sugar (existing FeedingSystem).

Each gets a class slot under a shared `VisibleBodyEvent` proximity event. Specific consequences vary; the *witness response calculation* is shared.

### What the player sees

Rarely, a visible body event. The character-test moment for whoever is closest. The relationship matrix updates accordingly. Some saves will have one of these events; some will have several; some will have none.
