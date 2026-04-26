# New Systems Ideas — Office Life Expansion

> Ideas generated from reading the existing ECS biology against the docs. No idea is a bad idea. Nothing here is final.

---

## The thesis

The current simulation knows a lot about **bodies.** It knows when Billy is hungry, tired, needs to pee. The digestion pipeline is genuinely impressive — food transits esophagus, small intestine, colon. The emotion system knows when he's bored or sad.

What the simulation doesn't know yet is what makes all that biology interesting: **the office context that makes biological needs into social problems.** You need to pee in a meeting. You're starving but you have to keep performing. You're furious but you have to keep smiling. The body in private is just biology. The body in public, under hierarchy, on a schedule you didn't choose — that's office life.

The proposals below are systems that bridge that gap. They're ordered roughly by how much they'd change what the simulation *feels like* to watch.

---

## System ideas

---

### 1. StressSystem + StressComponent

**The human truth:** Stress is the defining condition of office life and the engine doesn't have it. Not irritation (which we have — that's just "hungry and thirsty at the same time"). Stress is systemic. It accumulates from unmet work expectations, from social friction, from being observed while performing competence you're not sure you have. It doesn't go away when you eat. It compounds.

**What it does biologically:**
Stress is a cortisol-like modifier that amplifies everything already in the engine.

- High stress → drain rates for hunger and thirst accelerate (you forget to eat OR you stress-eat)
- High stress → urgency thresholds on BladderSystem and ColonSystem drop (you need to go more often when nervous — this is biologically real)
- High stress → SleepSystem WakeThreshold raises (harder to get to sleep, lighter sleep, wake up at 4am with your heart rate up)
- High stress → emotion decay rates slow (the anger doesn't fade; the fear stays)

**Components:**
- `StressComponent`: StressLevel (0–100), ChronicLevel (rolling 7-day average), Sources[], RecoveryRate
- Tags: `StressedTag`, `OverwhelmedTag`, `BurningOutTag`

**Stress sources (inputs):**
- Overdue task (see WorkloadSystem below)
- Recent social conflict
- Biological discomfort while being observed (hungry in a meeting)
- Proximity to specific disliked NPCs
- Being watched by a superior
- Uncertainty (not knowing what's coming)

**The extreme end:**
Stress diarrhea before a big presentation is biologically real, deeply undignified, and the engine already has the plumbing to do it. BladderCritical mid-presentation. ColonComponent urgency spike right before the boss walks in. The body betraying the performance at the worst possible moment. That's the thing people remember from their worst days at work.

**Chronic stress is different from acute stress.** An NPC who's been at high stress for many in-game days develops `BurningOutTag`. The burn-out state changes their behavior: they stop performing the mask (see SocialMaskSystem), start making mistakes, stop caring about status. This is a character arc the engine can generate — watch someone slowly burn out from the outside.

---

### 2. WorkloadSystem + TaskComponent

**The human truth:** There is no work. Nobody is doing anything. They eat, sleep, and pee in a featureless box. Work is the whole point — it's the reason the office exists, the source of all stress, the thing the player is supposed to be managing, and the thing that interferes with every biological need at every wrong moment.

**What it does:**
Tasks are entities too. A task has effort, a deadline, a priority, and an assigned NPC. Work progress is driven by the NPC's current biological state.

- Low energy → work progress rate halves
- High stress → occasional regression (mistakes, rework)
- Bladder urgency during deep focus → NPC finishes current thought before going, bladder fills past threshold → focus collapses → they go → they come back disrupted → output drops
- Hunger during focus → NPC works through it up to a point, then can't anymore

**Components:**
- `TaskComponent` on task entities: Effort (hours), Deadline, Priority, Progress (0–1), QualityLevel (0–1), AssignedNpcId
- `WorkloadComponent` on NPC entities: ActiveTasks[], Capacity, CurrentLoad (0–100), OverdueTasks[]

**The key interaction:**
WorkloadSystem reads the NPC's current biological state when calculating work progress each tick. A well-rested, well-fed, low-stress NPC does good work at full speed. A depleted, stressed, needing-to-pee NPC does poor work at a fraction of speed.

This makes the biological simulation matter. You're not just watching Billy's colon fill up for its own sake. You're watching it fill up while he's trying to hit a deadline, and you can see the quality of his output degrading in real time.

**The extreme ends:**
- The all-nighter: sleep drive maxed, NPC working through it anyway on a critical deadline. Sleep suppression is temporary; the crash after is brutal. Work quality degrades to zero. At some point the system makes them fall asleep at their desk.
- The PIP (Performance Improvement Plan): slow-motion professional death. An NPC's quality output is tracked over days. When it falls below threshold, a flag goes up. Nobody talks about it directly. Everyone knows. The NPC knows. They perform harder, which costs more energy, which accelerates the decline.
- The project that's been "in progress" for two years. From the deal catalog — an NPC who's been working on the same task, marked as active, progress never quite reaching completion. The deadline keeps getting pushed. Nobody asks.

---

### 3. SocialMaskSystem + SocialMaskComponent

**The human truth:** Every office worker is performing. You're hungry, tired, stressed, furious, attracted to your coworker, dreading the meeting in twenty minutes — and you're performing "fine, thanks, how are you." This performance is **effortful.** It costs something. When the energy runs out, the mask slips. That's when things get interesting.

**What it does:**
The mask is the gap between internal state and external presentation. The engine has rich internal state (drives, emotions, biological conditions). The mask determines how much of that leaks into visible behavior.

- `SocialMaskComponent`: MaskStrength (0–100), CurrentEffort (energy being spent maintaining it)
- MaskStrength starts each day fully restored after sleep
- Depletes over the course of the day as internal state diverges from desired presentation
- Personality modulates baseline: high neuroticism = mask starts weaker; high conscientiousness = mask lasts longer
- High stress accelerates mask depletion

**When the mask slips (MaskStrength below threshold):**
- Anger leaks: NPC speaks more sharply than intended; tone changes
- Attraction leaks: proximity-seeking behavior toward the target that's visible to nearby NPCs
- Fear leaks: avoidance behavior, micro-absences (leaving the floor, hiding in bathroom)
- Boredom leaks: visibly not doing anything, sighing, checking non-existent clocks

**What the player sees:**
The simulation's job is to make internal state readable without saying it. A high-mask NPC is opaque — you have to infer from behavior. A low-mask NPC is showing you everything. The player learns to read the mask level from subtle signals: posture, movement speed, response latency.

**The extreme end:**
The mask fully collapses. Every archetype has their version of this. The Cynic finally says what they actually think in a meeting. The Climber cries in the parking lot. The Recovering archetype breaks their sobriety streak after their mask fails under a particularly bad week. The Newbie completely melts down over something small because the small thing was just the last thing.

---

### 4. CoffeeSystem — Stimulant Metabolism (not just hydration)

**The human truth:** Coffee is not just a drink. In the early-2000s office, coffee is ritual, social currency, status signal, and biochemical dependency all at once. Who makes the pot and who just takes from it is a relationship dynamic. The person who brings in good beans is doing something social. The crash at 2pm is a real physiological event. The person who can't function before their first cup is genuinely experiencing mild withdrawal.

**What it does:**
Coffee satisfies thirst but also adds caffeine, which modifies the sleep system:
- CaffeineLevel suppresses sleep drive (the SleepSystem's WakeThreshold is raised while caffeine is active)
- Caffeine peaks ~30 minutes game-time after consumption, then decays
- Crash: when CaffeineLevel falls from peak, energy drain rate briefly doubles → the 2pm slump is a real simulated event
- Dependency: NPCs who consume coffee daily develop a baseline CaffeineLevel expectation. Days without it → irritation spike, headache tag, focus degradation (lower work quality)

**Components:**
- `CaffeineComponent` on NPC: CaffeineLevel (0–100), WithdrawalThreshold, PeakTime
- `CoffeePotComponent` on world object: FullLevel (0–100), LastBrewedBy (NPC id), IsEmpty, IsStale (time since brewed)

**The social mechanics:**
- Making the pot: passive belonging signal. NPCs in range get a subtle trust bump toward the maker.
- Taking without making: if another NPC is nearby and notices (proximity), irritation bumps in the observer. Not a confrontation — just a note, written later, probably.
- Stale pot: NPC pours, discovers it's been there four hours, has to decide whether to drink it anyway. Disgust + pragmatism. This is real.
- The person who brings in the good beans from home: deal-catalog behavior that produces visible social effects (belonging bumps for everyone who tastes it).

**The extreme end:**
The meeting where the coffee pot is empty and has been empty for an hour. Five NPCs are mildly in withdrawal. Irritation levels are elevated across the board. Someone finally makes the pot. The room fractionally relaxes. Nobody acknowledges it. That's an office in a Tuesday morning.

---

### 5. PassiveAggressionSystem

**The human truth:** When you're angry at someone in an office, you can almost never say so. Direct conflict has status costs. So conflict goes sideways — it comes out through objects, through notes, through strategic cc's on emails, through the conspicuously correct behavior that everyone reads as contempt.

**The mechanism:**
When an NPC's anger exceeds a threshold BUT their agreeableness is high OR their status drive warns that direct confrontation is risky → the anger gets expressed indirectly.

The target is usually a shared object (the fridge, the microwave, the coffee area). The expression is a note, a pointed cleaning act, a strategically placed reminder. Everyone can see it. The real target knows it's directed at them. Nobody says anything out loud.

**Components:**
- `PassiveAggressionComponent`: Target (NPC id), Method, Intensity, Audience
- Tags: `PassiveAggressiveTag`

**Methods (examples):**
- Note on shared object (fridge, microwave): visible to all NPCs who pass by
- Ostentatious cleaning: cleaning the microwave while the offending person is nearby, with pointed visibility
- Loud phone call at desk adjacent to disliked NPC
- Making a fresh pot of coffee after someone took the last cup without making more — but doing it with visible energy that communicates something

**What the player sees:**
NPC A is clearly furious at NPC B. A goes to the fridge and attaches a note. The note entity appears on the fridge object with A's irritation level embedded in its content tone. B walks to the fridge, reads the note, stands there for a moment. Then walks away. Their trust score toward A decrements. Their irritation spikes. They go back to their desk. Later: B also attaches a note. This is now a note war.

**The extreme end:**
The note war that escalates over three game-weeks. Both parties are acting completely correctly per office norms. Everyone else knows exactly what's happening. The player can read the entire conflict history in the note chain on the fridge door. Nobody has ever said a single direct word to the other.

---

### 6. RumorSystem + KnowledgeComponent

**The human truth:** What people know and what they believe — and the gap between those things — is the informal information layer of every office. Gossip moves faster than official communication and is often more accurate. It's also distorted in transmission. The affair everyone knows about except the spouse. The rumor about layoffs that's sixty percent true. The thing one person knows that would change everything if they said it out loud.

**Components:**
- `KnowledgeComponent` on NPC: KnownFacts[] — each fact has (subject, content, confidence 0–1, source, timestamp)
- Facts are first-person observations OR things received from other NPCs (secondhand knowledge)

**How information spreads:**
- NPCs transfer facts through conversation (proximity + trust)
- High trust transfer: confidence preserved
- Low trust transfer: confidence degrades, small distortions introduced
- Some facts amplify on their own (high social valence gossip spreads faster, with more confidence, with more distortion)

**The dramatic mechanics:**
- Player can see what each NPC believes vs. what's actually true
- An NPC with false information makes decisions based on it — producing behavior that looks weird from outside but is internally consistent
- The moment someone learns a fact that changes their understanding of a relationship: trust or suspicion component updates, which updates their behavior

**The extreme end:**
NPC A knows about the affair between B and C. B doesn't know A knows. C doesn't know A knows. A has been holding this for thirty in-game days, behavior subtly modulated by it. Then A tells D, who is B's closest friend. D now has to decide what to do with it. The player can watch the knowledge propagation in real time and see behavior change downstream from each transfer.

The thing nobody talks about (from the relationship library): two NPCs know something about each other that neither will reference. Both have the fact in their KnowledgeComponent with high confidence. The silence is structural, not accidental. This is the Mark situation — Cubicle 12, Mark is not discussed. The KnowledgeComponent is why not: everyone has the fact, everyone has assigned it MaxSuppression, and suppressing known facts costs a small but continuous mask-energy drain.

---

### 7. AppearanceDecaySystem + AppearanceComponent

**The human truth:** The way someone looks is a real-time readout of their internal state, and everyone in the office is reading it. 9am Monday is different from 4:30pm Friday. Week one is different from month three. Rumpled shirt, hollow eyes, the hair that didn't quite get brushed right — these are signals, and people respond to them before they've said a word.

**What it does:**
Appearance decays continuously and is restored by specific actions (sleep, bathroom visit, specific grooming behaviors). It's visible to other NPCs and modulates their trust/respect responses.

**Components:**
- `AppearanceComponent`: Tidiness (0–100), PostureQuality (0–100), GroomingLevel (0–100)
- Composite: `PresentationScore` (average, what other NPCs read)

**Decay inputs:**
- Time awake
- StressLevel
- Physical biological drive states being active (hunger, elimination urgency)
- Physical exertion (running to make the elevator)
- Emotional events (crying → appearance impact)

**Restore inputs:**
- Sleep (full restore)
- Bathroom visit (partial restore — straightening clothes, checking mirror)
- Specific deal behaviors (the NPC who always looks polished even at end of day → they're spending mask-energy on it)

**Social effects:**
- Low appearance → incoming trust responses from other NPCs decrease
- Low appearance on The Climber: panic; appearance maintenance becomes a dominant drive, competes with work tasks
- Low appearance on The Cynic: doesn't care, which makes the appearance drop faster, which is accurate to the type
- First impression: new NPCs (hired, temp) — other NPCs' initial trust/suspicion response is heavily weighted by appearance on first-contact tick

**The extreme end:**
The NPC who comes in one day looking like they slept in their car. Because they did. That's in the deal catalog. The office reads it immediately. Nobody asks. Belonging drive in the NPC spikes — they want someone to ask. Nobody does. This is the office.

---

### 8. SmellSystem + OdorComponent

**The human truth:** Offices have a smell. Specific, memorable, inescapable. The microwave. The coffee pot from three days ago. The person who runs at lunch and comes back not-quite-showered. The IT closet (from the world bible: "smells like dust burning off heatsinks"). Smell is a disgust trigger and a social signal, and it's almost never addressed directly.

**What it does:**
Odor entities attach to world objects and NPCs. They propagate via a simple radius decay. NPCs in range receive a disgust input.

**Components:**
- `OdorComponent`: OdorType (food/sweat/chemical/musty/burnt/pleasant), Intensity (0–100), Radius (world units), Decay (rate per game-second)
- Lives on: world objects (microwave, old fridge container, server room), NPC entities (when stress + physical activity reach threshold), food entities with RotTag

**The cascade:**
- NPC A microwaves fish
- OdorComponent spawns on microwave entity with high intensity and wide radius
- Every NPC in range receives disgust input proportional to intensity
- Disgust drives passive aggression responses (see PassiveAggressionSystem)
- The offending NPC either doesn't notice (low agreeableness) or notices and feels shame (high neuroticism)

**The existing RotSystem connection:**
The fridge container that's been there over a year (world bible named anchor) already has a RotComponent in the engine. Connect it to OdorComponent. The smell intensifies over time. NPCs who open the fridge get the full disgust hit. The container has been there so long because opening the fridge means smelling it, and removing it means touching it, so everyone's strategy is to simply not interact with it. This is structurally identical to the passive aggression problem — the correct action (throw it away) has a cost (disgust + social risk of being seen), so the incorrect equilibrium (leave it) persists indefinitely.

**The extreme end:**
The server room in the world bible (IT closet, Greg's domain). The smell there is proprietary — dust on hot metal, the particular quiet of a room with no natural light. When Greg leaves his domain to go somewhere else in the office, he carries a faint version of it. Other NPCs can smell where he came from. It's not unpleasant. It's just *him*. This is identity via OdorComponent. This is the office.

---

### 9. MeetingSystem + CaptivityComponent

**The human truth:** A meeting is a cage. You're in a room with people you may or may not like, you can't leave, you can't eat (usually), your bladder doesn't care, and a significant fraction of the time you're waiting for something to end that shouldn't have been a meeting.

**What it does:**
A meeting entity captures NPCs for a defined duration. While captured:
- FeedingSystem and DrinkingSystem drives still accumulate but behavior is suppressed (they can't act on them)
- BladderSystem and ColonSystem urgency continues to build — and now there's a social cost to leaving (status component reads it as negative)
- The NPC's Dominant drive conflicts with their Captivity context → StressComponent rises from the friction

**Components:**
- `MeetingComponent` on meeting entity: StartTime, Duration, Participants[], Room, Organizer
- `CaptiveComponent` on NPC (present while in meeting): MeetingId, UrgencyThreshold (above which they'll break decorum and leave)

**The mechanics:**
- When a meeting is called, participants' MovementTargetComponent is set to the conference room
- On arrival: CaptiveComponent added; FeedingSystem/DrinkingSystem behavior suppressed
- Drive scoring continues normally — urgency builds during the meeting
- When a drive score exceeds UrgencyThreshold (personality-gated): NPC breaks out of meeting to address it
- High agreeableness NPCs stay longer; low agreeableness break sooner; BladderCritical overrides everyone

**The observable drama:**
- The NPC who visibly needs to pee thirty minutes into a ninety-minute meeting, shifting in their chair, losing focus, deciding whether to interrupt
- The NPC who's starving and brought food to a meeting and is wondering if it's acceptable to eat it (it's not, they know it's not)
- The meeting scheduled over lunch — everyone knows, everyone resents it, nobody says anything
- The NPC who breaks first and leaves: social cost accrued, which they know, which affects their performance in the next meeting

**The extreme end:**
The three-hour meeting that was scheduled for ninety minutes. The TV that can't connect to anything (world bible). The HDMI cable that goes somewhere mysterious. The entire meeting is now about figuring out the HDMI situation instead of the actual thing. Everyone's biological drives are accumulating. The Cynic has their coat on forty minutes in. The Newbie is the one who finally solves the HDMI problem and it's the best thing they've done all week.

---

### 10. TemporalAnxietySystem (Time as Emotional Weight)

**The human truth:** Time doesn't move at the same speed in an office. Friday at 4pm is not the same as Monday at 9am. End-of-quarter is not the same as the dead middle of February. The clock watching is real and varies dramatically by who's watching it and what they're waiting for or dreading.

**What it does:**
Extends the existing SimulationClock with emotional time metadata. The clock already drives circadian rhythm (sleep/wake); this makes it also drive emotional anticipation and dread.

**Temporal anchors that matter:**
- Monday morning: high performance anxiety for The Climber, The Recovering, The Newbie; resignation and flat affect for The Cynic, The Old Hand
- Friday afternoon: attention drops across the board after 3pm; social drives increase; leaving-early behavior emergent from low task urgency competing with high social + belonging drives
- End of day: mask depletion accelerates; biological drives that were suppressed by performance context begin to win
- End of quarter: StressComponent spike across all NPCs with task responsibilities
- The day before a known review/deadline: specific NPC behaviors kick in based on personality (Climber: manic preparation; Cynic: doesn't prep; Recovering: dissociates; Old Hand: has done this before)

**Components:**
- No new component needed — this is modifier logic in MoodSystem and BrainSystem that reads SimulationClock.DayOfWeek, SimulationClock.Hour, and applies archetype-specific emotional multipliers
- `TemporalAnxietyModifier` in BrainSystem: table of (day/time + archetype) → drive/mood modifier

**The extreme end:**
Sunday night. The simulation's clock is running, nobody is in the office, but the NPCs are at home. The ones with high neuroticism are already dreading Monday. Their sleep quality degrades Sunday night — they go to bed, but WakeThreshold is higher than usual, sleep restoration is partial, they arrive Monday morning already slightly depleted. The simulation runs at home too, invisibly, and the state carries into the building. By the time The Climber walks through the door at 8am Monday they're already a little behind.

---

### 11. NoiseSystem + AcousticEnvironmentComponent

**The human truth:** The open-plan office is a noise machine. The person on speakerphone three cubicles down. The guy who laughs with his whole body. The printer that makes that sound. The fluorescent hum you stop hearing until you notice it and then can't un-notice it. Noise is the constant assault of other people's existence on your capacity to think, and it's almost impossible to address directly because doing so requires being the noise-complainer, which has its own status cost.

**What it does:**
Every room has an `AcousticEnvironment` — ambient noise level (0–100), plus discrete noise events that spike it. NPCs in range receive a `focus disruption` input proportional to the spike and how close it is. High ambient noise over time raises irritation and slows work-progress accumulation.

**Components:**
- `AcousticEnvironmentComponent` on room entities: AmbientLevel (0–100), ActiveSources[], DampingFactor (carpet vs. hard floor)
- `NoiseEventComponent` on event entities: Origin, Intensity, Duration, Type (conversation/laughter/phone/mechanical/music)
- Existing `WorkloadComponent` and `StressComponent` are the receivers — no new components needed on NPCs

**Noise sources:**
- Phone calls at desk: volume is personality-gated (low conscientiousness + high extraversion = speakerphone, full volume, door open)
- Laughter: contagious in proximity, pleasant at low levels, disruptive past a threshold
- The printer (see world bible: the supply closet printers nobody knows how to use — but the working floor printer counts too): a long print job is a grinding noise event with no natural end
- The fluorescent flicker hum (from the aesthetic bible): not a spike, a constant ambient that slowly ticks irritation upward for any NPC below it
- Crying in the bathroom: partially muffled, not fully. NPCs in range get an awareness signal that gets filed in KnowledgeComponent as "heard something" with low confidence

**The social mechanics:**
The noise-complainer role is high-cost. Direct complaint has status risk. So NPCs mostly absorb it — focus degrades silently, irritation climbs, eventually it becomes a passive-aggression outlet (the note, the pointed silence after the call ends). The Cynic is the rare NPC who just says "can you take that off speaker" and doesn't care about the cost. Everyone notices.

**The carpet layer:**
Acoustic damping is a world property. The basement (concrete) is loud and echoey. The top-floor offices (better carpet, closed doors) are quiet. The first-floor cube farm is the acoustic disaster. NPCs who spend more time in damped environments have lower ambient irritation drift — this is a quietly structural reason why the top-floor people feel calmer even absent any other status effects.

**The extreme end:**
The speakerphone meeting. One NPC takes an entire conference call at their desk, on speaker, for forty-five minutes. Every NPC in the ambient zone is accumulating irritation. By the end, three NPCs have abandoned their work entirely; focus disruption events keep interrupting their task progress. The call ends. The office is noticeably more tense than it was. Nobody says anything. The note appears on the fridge the next morning. It is not about the phone call.

---

### 12. FlowStateSystem + FlowStateComponent

**The human truth:** There are maybe two hours in a good work day where a person is actually *in it* — fully present, fast, not thinking about being fast, doing real work at a level they couldn't sustain consciously. Interruptions don't just pause that state; they destroy it. The recovery time after an interruption is longer than the interruption. An office full of people interrupting each other is one that structurally prevents the kind of work it's supposedly organized to produce.

**What it does:**
Flow is a fragile emergent state that the NPC can enter when conditions are right. It amplifies work quality and work speed while active. Any disruption event — a noise spike, a proximity event, a biological drive crossing a threshold — breaks it. Re-entering takes time.

**Components:**
- `FlowStateComponent` on NPC: FlowDepth (0–100, builds over time when conditions met), EntryTime, LastInterruptedAt, InterruptionCount
- Flow is entered when: low ambient noise, no recent proximity events, no biological drives above baseline urgency, active task in progress, uninterrupted for some duration
- Work progress multiplier: linear with FlowDepth (x1 at 0, x1.8 at 100)

**What breaks flow:**
- Any noise event above threshold (proximity-scaled)
- Another NPC entering conversation range
- Biological drive reaching urgency — hunger doesn't have to reach critical, just loud-enough to pull attention
- Their own phone ringing
- `CaptiveComponent` being added (meeting pull)

**Re-entry curve:**
Flow doesn't snap back. After an interruption, `FlowDepth` resets to zero. The NPC has to rebuild from scratch. A day of twelve small interruptions is a day of zero deep work, even if each interruption was sixty seconds. This is mechanically accurate to the human experience and produces an observable pattern: some NPCs (low extraversion, high conscientiousness) accumulate long flow periods; others (high extraversion, open door policy, frequent meeting organizers) never enter it.

**The social dynamic:**
The person who interrupts a lot doesn't know they're interrupting. From their perspective they had a quick question, it took thirty seconds, they got their answer. The NPC they interrupted lost forty minutes of accumulated state. The asymmetry is the drama. The Hermit (Greg) has a full-length flow state most of the IT closet day because nobody wants to go in there. His work output is quietly excellent as a result.

**The player-facing read:**
When an NPC is in deep flow, observable signals: movement stops except for task-directed micro-movement, facing direction locked, response latency to proximity events is delayed (they don't immediately notice someone standing near them). Breaking the flow state is visible — the NPC surfaces, takes a moment, readjusts. Then the work-speed indicator starts rebuilding from zero.

**The extreme end:**
The Newbie, who is eager and helpful and answers every question immediately, has never been in flow. Not once in three weeks. Their task output is okay but never better than okay because they've never been uninterrupted long enough to find another gear. Nobody has told them to stop answering every question immediately. The system has its own way of teaching this lesson eventually.

---

### 13. LunchRitualSystem

**The human truth:** Lunch is the most important social decision of the workday and almost nobody acknowledges it as such. Who you eat with is a statement. Where you eat is a statement. Eating at your desk is a statement about your relationship to the work, or to the people, or both. The invitation and the non-invitation are both felt. The hierarchy of who eats with whom is the most legible power map in the building.

**What it does:**
Around noon (SimulationClock-driven), FeedingSystem drives spike across the population. LunchRitualSystem resolves *how* NPCs satisfy that drive — the group formation, the location choice, the social significance of each.

**Components:**
- `LunchGroupComponent` on group entity: Members[], Destination, IsInviteOpen (can others join?)
- No new NPC component needed — resolution reads existing: Belonging drive, Status drive, Trust relationships, floor/location

**Group formation logic:**
- NPCs with Trust ≥ threshold form groups naturally from proximity
- High-Status NPCs form their own groups (The Climber, the exec track) — others wait to be invited
- Low-extraversion NPCs (Greg) self-exclude and go eat in the IT closet, or their car (deal catalog: "always eats lunch in their car")
- Groups of 2–4 are stable; 5+ fragment into two groups, which is itself a social event

**Destinations:**
- The breakroom (low status, high belonging): the communal experience
- The outdoor area (weather-gated): the best option when available; the sunny table has its own hierarchy
- The car (maximum privacy): for NPCs in mask-collapse, emotional distress, or the deal that makes them always do this
- A local restaurant (above the sim's physical scope, but NPC leaves building and returns): signals money, signals social ease, signals they don't eat here by choice

**What the player reads:**
The lunch groupings are the power map, visible twice a day, every day. Shifts in the groupings precede shifts in the relationship matrix. When The Climber starts eating with a different group, something is changing. When The Newbie finally gets invited to sit with the group they've been circling for three weeks, it's a milestone. When someone who's always been in the group eats at their desk alone for the first time, something has happened.

**The social mechanics:**
- Non-invitation is felt but never spoken. Belonging drive dips for the un-invited NPC; they eat at their desk, which they tell themselves is about getting work done
- The lunch topic becomes KnowledgeComponent content — information spread over lunch is high-trust transfer, high-distortion potential, especially after the third week of the same group eating together every day
- Eating at your desk while others invite you reads as status (too busy / too important to lunch) OR as social isolation, depending on the NPC's other signals — the player has to read context

**The extreme end:**
The all-hands lunch. A corporate pizza order for the floor — mandatory togetherness. Every social dynamic that's been diffused over the week is now in one room, at the same time, with paper plates. The Cynic sits as far from management as possible. The Climber positions next to the person they've been working on all week. The Newbie doesn't know where to sit and stands at the table edge for a moment that lasts longer than it should. The Recovering eats nothing. The fridge drama is temporarily suspended because everyone's eating the same pizza. Briefly, it's almost fine.

---

### 14. IllnessSystem + PathogenComponent

**The human truth:** Someone comes in sick because they're behind on something, or because they feel guilty staying home, or because they're the kind of person who comes in sick. And then everyone else gets it. The office plague is a real event, with a real shape: one person, then three, then half the floor, spread by proximity, sealed in by the fact that nobody wants to be the one who goes home first.

**What it does:**
Illness is an entity that propagates through proximity. NPCs who contract it enter a degraded biological state — energy drain accelerates, focus is impaired, work quality drops — while simultaneously having social reasons not to stay home.

**Components:**
- `PathogenComponent` on NPC: Severity (0–100), DaysSinceContraction, InfectionVector (who they got it from), ContagiousWindow
- `ImmuneStateComponent` on NPC: ResistanceLevel (0–100, personality + sleep quality + stress level composite), RecentExposures[], RecoveryTimer
- Illness propagates via the existing proximity system: any NPC entering ConversationRange of an infected NPC during the contagious window has a resistance-weighted chance of contracting

**The stay-home calculation:**
This is the dramatic core. NPCs evaluate whether to come in while sick. Inputs:
- WorkloadComponent.CurrentLoad (high load → come in anyway)
- StatusComponent (fear of appearing weak → come in anyway)
- Agreeableness (high agreeableness → feel guilty about spreading it → more likely to stay home)
- SocialMaskComponent.MaskStrength (low mask → less energy to perform being fine)
- BurnoutTag active → they come in regardless; caring is gone

High-conscientiousness, high-status-drive NPCs are statistically the most likely to infect the office because they are statistically most likely to come in sick. This is accurate to reality.

**Cross-floor propagation:**
The elevator, the breakroom, the bathroom — these are the transmission nodes. An infected shipping-floor worker who comes up for lunch is a vector event. The pathogen moves floor-to-floor through these shared spaces, which are already modeled as proximity zones.

**The StressSystem interaction:**
High ChronicStress suppresses immune resistance. An NPC who's been under workload pressure for two weeks has lower resistance and gets sicker faster when exposed. The system that's already depleting them makes them more vulnerable to the system that's about to deplete them further. This compounds.

**The extreme end:**
The plague week. It starts with one NPC — probably The Newbie, who doesn't have the seniority to comfortably call in sick and doesn't know yet that they should. By day three, four NPCs are visibly ill. By day five, half the floor has compromised immune states. The office productivity graph, if the player is watching work output, collapses visibly. Someone eventually tells The Newbie to go home. They're both relieved and mortified. They go home. The damage is done.

---

### 15. ThermalComfortSystem + ThermalStateComponent

**The human truth:** The AC works on Tuesdays. Nobody controls the heat in any office anyone has ever worked in. There's the person who is always cold and keeps a space heater under their desk (which trips the building's power every few months). There's the person who runs hot and has their window cracked in February. The thermostat is a political object. Whoever controls it has a kind of ambient power that has nothing to do with their title.

**What it does:**
Each room has a thermal state. NPCs have a comfort range. Being outside comfort range degrades focus and raises irritation — slowly, constantly, inescapably. The HVAC as a world system has its own logic: it responds to settings on a delay, overshoots, undershoots, and breaks on the worst days.

**Components:**
- `ThermalEnvironmentComponent` on room entities: CurrentTemp (degrees, era-appropriate Fahrenheit), TargetTemp, HVACState (on/off/broken/stuck), VentilationQuality
- `ThermalStateComponent` on NPC: ComfortMin, ComfortMax, CurrentPerceivedTemp (body-heat adjusted), ThermalIrritationAccumulator

**The control-object mechanics:**
The thermostat is an entity. It has a `LastAdjustedBy` field. NPCs who are outside comfort range will path toward it. High-agreeableness NPCs check whether anyone is watching before adjusting. Low-agreeableness NPCs adjust without checking. When an NPC adjusts the thermostat, other NPCs who were comfortable are now moving toward discomfort — irritation is transferred, invisibly, through a small plastic box on a wall.

**The political layer:**
Whoever sits nearest the thermostat has de facto control. This is a spatial power that has nothing to do with formal hierarchy. An entry-level NPC whose desk happens to be by the thermostat has unusual ambient influence — others have to approach their space to make an adjustment, which is a proximity event with social weight. They can choose whether to adjust it when asked. They can choose not to.

**Floor differential:**
The top floor is always a few degrees warmer — corner offices, sun exposure, the HVAC doesn't reach quite as well. The exec floor is comfortable in winter, brutal in summer. The basement is always cooler than it should be — better for the production floor workers, quietly miserable for anyone who has to go down for something.

**The space heater:**
A specific entity. Provides local thermal comfort to the NPC who owns it. Draws power; trips the breaker every three to four game-weeks. When the breaker trips, one floor segment loses power for some duration. The space heater is a rolling disaster that is also completely non-negotiable for the person who needs it.

**The extreme end:**
July. The AC is broken. Maintenance has been called. The part is on order. Three days of an office running ten degrees too warm. Mask depletion is accelerated — heat is a physical stressor that compounds everything else. Appearance scores are declining by afternoon. Irritation baselines are elevated across the floor. Someone finally propped a door open, which fixes nothing thermally but gives the illusion of doing something. The part arrives Friday afternoon, too late for the week.

---

### 16. EavesdropSystem

**The human truth:** The cubicle wall is not a wall. It's a divider that suggests the convention of privacy without actually providing it. Every conversation at moderate volume is audible to the adjacent cube. Sensitive information leaks constantly — not through malice but through physics. The person who overhears something they weren't meant to has a decision to make about it, and how they make it is a character test.

**What it does:**
Extends the existing proximity system with a directionality and partial-occlusion model. NPC A and NPC B are in conversation. NPC C is in awareness range but outside conversation range — normally they'd receive no content from this conversation. The EavesdropSystem checks whether C has line-of-occlusion (cubicle wall, closed door, different room) or partial occlusion (half-wall, open cubicle, adjacent desk). Partial occlusion: C receives the conversation fact with a degraded confidence and reduced content (key words, emotional tone, names).

**Components:**
- No new NPC component — the output is a fact inserted into KnowledgeComponent with source flagged as `Overheard`
- `OcclusionComponent` on wall/partition entities: OcclusionStrength (0–1), 0 = open air, 1 = closed solid wall

**The knowledge asymmetry:**
The overheard fact has a specific signature: NPC C knows something that A and B don't know C knows. This creates a social asymmetry that's different from normal gossip (where information is openly exchanged). C has a choice: act on it (which reveals they overheard), stay quiet (which costs mask-energy because they're now performing not-knowing), or tell someone else (which puts the information in circulation without tagging C as the source).

**The RumorSystem connection:**
Overheard facts enter the RumorSystem with a modified trust chain — they're firsthand (C heard it themselves, confidence is relatively high) but the source is compromised (it was private). When C passes the fact to D, D gets high-confidence information with no clean source. This is how sensitive information circulates in offices: accurately, fast, with no traceable origin, because everyone can claim they heard it somewhere.

**What the player sees:**
The player knows what was said. The player knows who overheard it. Watching what C does with that information — immediately, over the next few days — is one of the richest character-test windows the engine can produce. Does the Old Hand, who overheard something about a layoff, tell anyone? Does The Affair's partner overhear something that changes everything? Does Greg, in the IT closet, overhear something through the wall about a budget cut to his department, weeks before anyone tells him officially?

**The extreme end:**
NPC A is telling NPC B about their affair in the breakroom, voice low, thinking they're alone. They're not alone. The door was open three inches. NPC C, walking past, caught two sentences. C doesn't know what to do. They walk back to their desk. They don't tell anyone for eleven in-game days. Then they tell D, at lunch, as a thing they "heard somewhere." D knows exactly who the affair involves. D doesn't say how they know. The conversation happened in the breakroom eleven days ago and is now shaping the social structure of the entire floor.

---

### 17. FavorEconomySystem + FavorLedgerComponent

**The human truth:** The real currency of an office isn't money — it's favors. You covered my shift. You kept quiet about the thing. You introduced me to the right person. You took the fall on the report. These aren't tracked anywhere official, but everyone tracks them. The Climber keeps meticulous internal accounts. The Old Hand doesn't bother — they know that being the person who always comes through builds a debt that never needs to be called in explicitly; people just help them. The Founder's Nephew has never done anyone a favor and doesn't understand why this matters.

**What it does:**
Favor is a directed, bilateral informal debt. When NPC A does something for NPC B that cost A something and benefited B, a favor is created. It persists in FavorLedgerComponent. When B does something for A later, the ledger adjusts. Calling a favor in is a social act — it can be done explicitly ("you owe me one") or implicitly (B notices A needs something and does it voluntarily, no words exchanged).

**Components:**
- `FavorLedgerComponent` on NPC: Ledger[] — each entry is (CounterpartyId, Balance, LastEvent, LastEventTimestamp)
- Positive balance: this NPC is owed; negative balance: this NPC owes
- `FavorEventComponent` on event entity: Giver, Receiver, Type, PerceivedValue

**What counts as a favor:**
- Covering someone's work during an absence (WorkloadSystem interaction)
- Not repeating something overheard (EavesdropSystem interaction)
- Making the coffee pot (CoffeeSystem interaction)
- Taking the blame for a mistake on a shared task
- Introducing two NPCs who then form a relationship (relationship-pattern library interaction)
- Staying late to help with a deadline

**The personality gates:**
- The Climber tracks the ledger obsessively — it's a spreadsheet they run mentally
- The Cynic rejects the ledger conceptually but keeps a rough mental account anyway, and knows exactly when they're being used
- The Old Hand is perpetually in positive balance because they give without tracking, and because of this, NPCs feel genuine warmth toward them
- The Recovering archetype gives freely as part of their recovery pattern — generosity is a moral project for them — which can be exploited
- The Founder's Nephew is perpetually in debt, doesn't know it, and couldn't care less

**The call-in mechanics:**
When an NPC needs something and has positive balance with another NPC, they can call it. Calling is a social act that costs the relationship a small amount regardless of outcome (it acknowledges the accounting, which makes both parties aware that it was transactional). NPCs high in agreeableness prefer not to call explicitly; they'd rather let debts exist as ambient warmth. NPCs high in conscientiousness call cleanly and efficiently.

**The extreme end:**
The Climber has been building a favor debt with four NPCs over three in-game months — small things, strategic, designed to produce the exact moment they need. Then the performance review comes. They call in all four, in sequence, over forty-eight in-game hours. None of the four realizes what's happening; each just does what feels like a natural thing to do for someone who's been good to them. The Climber gets what they wanted. The ledger resets to zero. They start building again.

---

### 18. ThresholdSpaceSystem (Parking Lot / Smoking Bench / Car)

**The human truth:** The most honest moments of the workday happen in the spaces that are neither home nor office. The parking lot, the smoking bench, the car you sit in for eight minutes before going in. These are liminal spaces — you're already there but not yet *there*. The mask is being assembled in the car. The mask is coming off the second you hit the parking lot. What happens in the lot often doesn't make it inside, and what happens inside often detonates in the lot on the way home.

**What it does:**
Threshold spaces are designated world zones with specific behavioral rules. NPCs in a threshold zone have:
- Reduced social observation weight (other NPCs in the zone are more tolerant of mask-off behavior)
- Reduced mask maintenance cost (SocialMaskComponent drains slower here)
- Access to private actions not available inside (crying, phone calls to off-screen people, sitting in car, smoking)

**Threshold zones (from the world bible):**
- **The Parking Lot** — arrival/departure transition. On arrival: NPC carries commute state (weather, traffic, quality of sleep, how home was this morning). On departure: NPC carries work-day state home. Both are bleed-through points.
- **The Smoking Bench** — the true confessional of the building. NPCs who smoke know something about each other that non-smokers don't: they know what time the other person leaves for a cigarette. Smoking bench proximity events have higher-trust transfer than equivalent indoor proximity — the context is already informal.
- **The Car** — the private box. Eating lunch in the car (deal catalog: "always eats lunch in their car") is not antisocial, it's recovery time. Crying in the car before going in is one of the most common human behaviors and the engine should be able to produce it.

**The commute-state bleed-through:**
An NPC arrives each morning in a state that wasn't produced by the office. Traffic. The argument they had at home. The news they got on the drive over. The engine doesn't simulate home life, but it can simulate the *residue* of home life. The ThresholdSpaceSystem applies a `CommuteMoodModifier` on arrival — random within personality-appropriate ranges, with some narrative hooks (if the NPC has a known domestic situation flagged in their deal catalog, the modifier is weighted accordingly).

**The smoking bench social graph:**
The smoking bench regulars form a secondary social graph that crosses formal hierarchies. The exec who sneaks out for a smoke and the shipping-floor worker who smokes openly are equals on the bench — this is a real and persistent feature of every office building. The RumorSystem here has its own transfer dynamics: bench conversation is informal enough that high-sensitivity information moves freely, with less distortion than normal gossip because the trust calibration is different.

**The extreme end:**
It's 4:55pm Friday. The Recovering hasn't smoked in four months. Today was the week that accumulated. They're standing at the parking lot edge watching the smoking bench. Two people from the floor are out there. The mask is at 8%. The drive is there. The system knows what they've been through. The decision point is active. This is the moment the sobriety arc the deal catalog references either holds or doesn't — produced without any authored trigger, by the week's worth of simulation state finally reaching a threshold.

---

## Synthesis: what these systems interact into

These aren't ten separate additions. They're a web that produces the scenarios the docs describe.

**The scenario the engine should generate on its own:**
Week three of a big project. The deadline is in four days. One NPC's chronic stress has been above 70 for six days — their mask is thinning, their work quality degrading (which stresses them more), their appearance deteriorating. They had coffee three times yesterday and crashed hard — sleep was bad, they're starting today tired. Their bladder fill rate is slightly elevated from the stress. They have a meeting at 2pm they know they're not prepared for.

By 1:45pm, four biological drives are above their normal urgency. Their mask is at 40%. The Climber, who is their rival, walks past looking exactly as polished as they should. The old-flame relationship between them (from the relationship library) means the trust-suspicion state is complex — irritation spikes.

2pm: the meeting. CaptiveComponent added. The TV doesn't work. The HDMI situation. Thirty minutes in, their bladder is at BladderCritical. They break out of the meeting. When they come back, the Climber has presented first, to everyone present, and done it well.

That's a day. That's office life. That's what the engine should be able to generate without any scripting — just systems running.

---

## Open threads / questions

- The bathroom as emotional space (not just elimination target): NPCs go there when the mask is near collapse, spend time restoring. Should be the private space in the building. The only place nobody can observe you.
- The deal-catalog behaviors as system triggers: "sleeps in car some nights" is an AppearanceComponent input. "Never washes coffee mug" interacts with OdorComponent and PassiveAggressionSystem. "Brings homemade cookies Fridays" is a belonging-drive expression with social consequences. Each deal in the catalog should eventually map to specific system interactions.
- The thing nobody talks about (Mark, Cubicle 12): this is a world-object-level IncidentMemory. The anchor carries state that predates the save. NPCs who were present have the fact suppressed in KnowledgeComponent. It costs mask energy. It's there.
- Seasonal/temporal events (holiday party, end of year): the holiday party is the mask-collapse event everyone anticipates and half the cast dreads. It belongs in the world calendar. The office relationships that break and form at holiday parties are different from any other context — alcohol as mask-removal agent, formal context removed, biological state deliberately altered.
