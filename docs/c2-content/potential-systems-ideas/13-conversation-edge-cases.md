# 13 — Conversation Edge Cases

> Most office conversations follow a script. The interesting ones are the ones where the script breaks — the silence that lasts too long, the goodbye that won't end, the second time you're passing the same person in the hallway. The dialog bible covers what gets *said*; this cluster covers the meta-shape of conversations: their entries, exits, and failure modes.

---

## What's already in the engine that this builds on

- The dialog bible's corpus + decision-tree retrieval + calcify mechanism.
- Proximity ranges (conversation, awareness, sight) and the proximity-event bus.
- `SocialMaskComponent`, `willpower`, `inhibitions`.
- The cast bible's six vocabulary registers.

---

## 13.1 — AwkwardSilenceSystem

### The human truth

Conversations have a beat. When the beat is missed, silence accrues. Most silences are nothing — pauses for breath, looking-things-up. Some silences are *the silence* — the one where neither party knows what to say, where one was expecting more, where someone said something and the other didn't know how to respond. That silence has a cost both parties pay.

### What it does

A `ConversationContext` exists between two NPCs in conversation range. It tracks:

- `lastUtteranceTick` — when the last spoken or emoted exchange happened.
- `expectedResponseLatency` — derived from register, relationship, topic.
- `silenceDuration` — current run since last beat.

When `silenceDuration` exceeds `expectedResponseLatency × 1.5`, an `AwkwardSilenceEvent` fires. Each tick of the silence beyond that threshold:

- Both parties pay a small `mask` draw.
- Both parties' next-utterance retrieval is biased toward `deflect` and `acknowledge` contexts (per dialog bible) — the recovery move.
- A small `irritation` or `vulnerability` accumulator on whoever is "supposed to speak next."

Silences end when:

- Someone speaks (high probability).
- Someone *physically exits* the conversation — the more visible escape, costs status.
- An external interruption (a phone, a passing NPC, a fire-alarm) breaks the frame.

### Components

- `ConversationContext` between any two conversing NPCs.
- `AwkwardSilenceEvent` on the proximity bus.

### Personality + archetype gates

- High `extraversion` NPCs break silences fast (low tolerance).
- The Hermit *creates* silences and is comfortable in them — but the *other* party isn't. The Hermit's conversational signature is leaving silence the other party can't comfortably hold.
- The Climber pre-loads conversation fillers; they almost never produce silences.
- The Newbie produces silences accidentally because they don't yet know which beat is theirs.
- The Cynic *uses* silence as a weapon. The deliberate-silence move.

### Cross-system interactions

- **Dialog calcify:** the recovery utterance after a long silence calcifies fast because the situation is high-noteworthiness. ("Anyway... yeah.") becomes a tic.
- **Bubble (5.1):** silences in close proximity are worse than silences at conversation-range edge.

### What the player sees

Two NPCs in conversation. Beat. Beat. The silhouettes shift. One looks away. The other says something. The conversation ends, slightly off. The chronicle may not record the moment but the relationship matrix logs the small drag.

### The extreme end

The Climber and the rival, alone in the elevator (cluster 5.5). Doors close. The Climber preloaded an opener; the rival doesn't engage. Silence, 8 seconds. Climber tries again; rival gives a one-word reply. Silence, 14 seconds. The math has gone wrong; mask is at 40%; the Climber is *losing this without anyone speaking*. Doors open. Rival walks out. Climber stands a beat longer. The chronicle records: *the elevator silence.*

---

## 13.2 — SlowGoodbyeSystem

### The human truth

Some conversations cannot end. *"OK, well..."* — *"Yeah, totally."* — *"Anyway, I'll let you go."* — *"Yeah, sounds good."* — *"OK, talk soon."* — *"Yeah, definitely."* — twenty seconds later they're still standing there. The *I should head out* announcement that doesn't actually result in leaving. Some NPCs cannot exit a conversation cleanly. The cost is their entire afternoon.

### What it does

When a conversation reaches a `goodbye-attempt` context (a dialog moment with the explicit intent to end), the conversation enters a `goodbyeProtocol` substate:

- Several rounds of mutual goodbye-fragment exchange are normal.
- Round count above a threshold (~3) → `SlowGoodbyeEvent` fires.
- Each extra round costs both parties small willpower and tags the relationship with a `slow-exit-tendency` between them.

Some pairs produce calcified slow-goodbye loops where every conversation ends in a five-round goodbye. Both parties *know it's happening* and are powerless to stop it.

### Components

- `ConversationGoodbyeState` per active conversation.

### Personality + archetype gates

- High `agreeableness` produces the worst slow-goodbyers — they cannot afford the tiny rudeness of a clean exit.
- The Vent (Donna) is famously slow-goodbye. Conversations with her have a mandatory tail.
- The Cynic exits cleanly: *"OK. Bye."* + walks. Reads as cold; saves them hours per year.
- The Hermit doesn't goodbye at all — they just leave. Other NPCs find this disorienting.

### Cross-system interactions

- **WorkloadSystem:** slow goodbyes are real time-thieves. An NPC chronically slow-goodbye'd has measurable productivity cost.
- **Dialog calcify:** the per-NPC goodbye signature calcifies. Donna's *"Alright, well I better let you go, hon"* becomes hers.

### What the player sees

A floor where some NPCs are visibly *trying to leave* a conversation for forty seconds before they actually do.

### The extreme end

Donna and the Old Hand have a shared slow-goodbye loop that takes between 90 and 180 sim-seconds every time. Three rounds at desk. A drift toward the elevator. A round in the hallway. A round at the elevator door. A final round when the door starts closing. Both of them know. Neither can break it.

---

## 13.3 — GreetingCalibrationSystem

### The human truth

Every encounter starts with a greeting. The greeting calibrates: how warm, how formal, how brief. Get it wrong and the rest of the conversation is fighting uphill. The hi-or-hey choice. The good-morning-or-just-morning. The wave-from-far-away calibration. The accidentally-too-warm-greeting from someone you barely know. The accidentally-too-cold from someone who expected warmth.

### What it does

When proximity event fires for two NPCs, a `GreetingCalibrationEvent` decides each NPC's greeting choice:

- `intensity` — `nod | mumble | brief | warm | enthusiastic`.
- `register` — formal / casual / familial.
- `eyeContact` — duration in beats.

Both NPCs select independently. A **mismatch event** fires when intensities differ by more than one notch — the warm-greeter feels rebuffed; the brief-greeter feels imposed-on. Mask draws on whoever feels worse about the mismatch.

### Personality + archetype gates

- The Vent always greets warmly. Mismatches cost her belonging.
- The Climber calibrates by audience. Their greetings are diagnostic of who they think you are this morning.
- The Hermit greets minimally always. The floor has accepted this. Mismatch cost is muted because the expectation is calibrated.
- The Newbie miscalibrates often in their first month because they don't yet know each NPC's preferred intensity.

### Cross-system interactions

- **Dialog calcify:** per-NPC greeting signatures calcify into recognizable tics.
- **Hallway second-pass (5.4):** the second-pass-greeting requires *de*-escalation from the first-pass — failure to de-escalate produces awkward.

### What the player sees

A continuous stream of small greetings whose mismatch creates faint texture in the floor's relationship temperature.

### The extreme end

The new boss is making the rounds on day one. Their greeting style is an unfamiliar register for the floor. Three NPCs miscalibrate in different directions in the same hour. By end of day, the boss has been read as cold by Donna, distant by the Newbie, and *normal* by the Cynic. Each NPC's first-impression has been built from a single mismatched calibration. The chronicle records the day; subsequent interactions either reinforce or correct.

---

## 13.4 — ConversationHijackingSystem

### The human truth

Some NPCs change conversation topic in ways the prior speaker didn't authorize. The redirector who keeps bringing it back to themselves. The interrupter who finishes your sentence wrong. The tangenter who's already off in a different direction. The rerouting NPC who responds to *what you said implies* instead of *what you said*. These are micro-aggressions that accumulate over months.

### What it does

When NPC A speaks, an internal `topicHandle` is established. When NPC B's response either:

- Switches the topic without acknowledgment, or
- Interrupts mid-utterance, or
- Reframes A's content into a different topic,

a `HijackEvent` fires. A pays a small `irritation`; B is unaware (mostly).

Calcified hijacking from B is recognized by A and other NPCs. The reputation builds: *every conversation with B somehow becomes about B.*

### Cross-system interactions

- **DialogCalcify:** the redirect phrases of a chronic hijacker calcify ("That reminds me..." said by The Vent on autopilot).
- **AversionLink (cluster 03.5):** chronic hijacking is a top contributor to slow aversion accumulation.

### What the player sees

The pattern becomes legible by month two of a save. NPCs avoiding B for one-on-ones; group conversations where B's redirects are quietly resisted.

### The extreme end

The Climber chronically hijacks. After six months, three coworkers have stopped initiating conversations with them entirely. The Climber's `loneliness` baseline rises mysteriously. They blame it on others' coldness. The simulation knows the cause; the Climber doesn't.

---

## 13.5 — TopicMinefieldSystem

### The human truth

Some topics are charged for specific pairs. *Don't ask Sandra about kids.* *Don't bring up the merger to Frank.* *Don't mention the Yankees to Greg.* These are landmines whose locations are partially shared knowledge — coworkers who know steer around; coworkers who don't blunder in. The blunder is the high-cost event.

### What it does

Each NPC carries a `TopicSensitivities[]` — topics that produce outsized mask draw or `irritation` when raised by another NPC. The topic-sensitivity is partially-public knowledge — some coworkers know to avoid; others don't.

When a conversation broaches a topic on a participant's sensitivity list:

- The participant pays a heavy mask draw.
- Their next-utterance retrieval shifts to `deflect`, `lash-out`, or `silent-exit`.
- The instigator may or may not have intended the trigger; their own KnowledgeComponent records the result either way.

### Cross-system interactions

- **KnowledgeComponent:** knowledge of a topic-sensitivity *is itself* a piece of office knowledge that propagates. New NPCs gradually learn the floor's minefield.
- **Recovering / Affair / Climber arcs:** specific topic sensitivities derive from these archetypes' specific situations.

### What the player sees

Conversations that suddenly turn cold. Coworkers steering away from specific subjects. The Newbie's slow learning of the floor's geography of avoidance.

### The extreme end

The Newbie, unaware of Sandra's losing her child arc (cluster 09 reference), asks a casual *do you have kids?* in the elevator. Sandra's mask drops to single-digit. She walks out at the next floor without answering. The Newbie spends the rest of the day not knowing what they did wrong. Eventually the Vent quietly explains. The Newbie carries the shame for weeks.

---

## 13.6 — InterruptionEtiquetteSystem

### The human truth

Walking up to two coworkers already in conversation requires a calibration. Do you stand near them and wait? Hover? Catch eye? Loudly clear your throat? Walk past and come back? Each option has a cost. The conversation-in-progress reads as either *closed loop* or *open invitation* and getting that read wrong is socially expensive.

### What it does

A `ConversationOpenness` flag exists on active conversations:

- `closed` — leaning in, low voices, body angle inward.
- `neutral` — normal posture; ambiguous.
- `open` — facing outward, casual, ready to be joined.

A third NPC approaching reads the openness and acts:

- High-agreeableness + closed → backs off, comes back later.
- High-extraversion + closed → joins anyway (mismatch event).
- Low-conscientiousness + neutral → blunders in.
- Newbie reads conservatively in their first weeks; gets bolder over time.

### What the player sees

Tiny social negotiation events at the edge of conversations. Mostly invisible; occasionally a blunder.

### The extreme end

The Climber walks up to the Affair partners deep in private conversation (closed). The Climber reads neutral and joins. The Affair partners' mask draws hard. They cover; conversation pivots. The Climber doesn't know what they walked in on. The Affair partners know they may have just been seen. The chronicle records nothing direct; their relationship matrix toward the Climber tightens.

---

## 13.7 — TheReturnedConversationLoop

### The human truth

Some pairs have *the same* conversation every time they meet. The recap of the same complaint. The same anecdote, told as if new. The kid update from the parent NPC. The political-news commentary from the Cynic. Hearing it again costs the listener. Telling it again costs the speaker nothing — they don't know they're repeating.

### What it does

Each NPC's `KnowledgeComponent` tracks what they've told whom. When the same content is repeated to the same listener, a `RetoldEvent` fires — the listener's `irritation` spikes, but social rules forbid commenting. The teller is oblivious because their internal record (what they've said) is fuzzier than the listener's (what they've heard).

Asymmetry compounds: high-extraversion talkers re-tell the most; their per-listener counters climb fast.

### What the player sees

Donna telling the same story to the Newbie that Donna already told the Newbie last Tuesday. The Newbie nods and pays the cost.

### The extreme end

The Vent's chronic-retell pattern with one specific coworker hits a year-long calcified state. The coworker has heard the same six anecdotes ~30 times each. Their mask cost when Donna approaches is now visible from across the floor — they brace before the conversation starts. Donna doesn't know.

---

## 13.8 — TheUnspokenAcknowledgment

### The human truth

The most-meaningful office conversations sometimes contain no actual content. The pair who exchange a single look during a meeting that says *can you believe this.* The eye-roll across the room that lands. The lifted eyebrow at the Founder's Nephew. These are conversations conducted entirely through micro-expression and proximity, and they build relationships faster than anything spoken.

### What it does

A `SilentExchangeEvent` fires when two NPCs have:

- Mutual line of sight.
- A shared external stimulus (a meeting line, an outburst, an event).
- Personality + relationship preconditions for *catching* each other's micro-expression.

The result: a small `affection` and `belonging` lift between the two. The chronicle records it as a noteworthy social event despite zero spoken content.

### Cross-system interactions

- **Inside jokes (cluster 16):** silent exchanges are the seed for calcified inside jokes. Repeated silent acknowledgments around the same kind of event become a relationship signature.

### What the player sees

The two NPCs who exchange a glance during the worst meeting of the quarter. Across the floor. The look lasts a second. The relationship matrix updates more from this than from a thousand greetings.

### The extreme end

The Old Hand and the Hermit, neither of whom is socially connected to most of the floor, share silent exchanges during one specific kind of meeting (the ones where a particular exec speaks). Over months, the silent-exchange counter calcifies into a *recognized friendship* despite the two of them rarely speaking. The chronicle records the eventual moment when the Hermit, unprovoked, brings the Old Hand a coffee. They never discuss why.
