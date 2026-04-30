# 05 — Proxemics and Personal Space

> The cubicle is a suggestion. The hallway is a negotiation. The elevator is a punishment. The handshake-or-hug ambiguity is a class of real-time decision the engine can model without anyone speaking a word.

---

## What's already in the engine that this builds on

- The aesthetic-bible's three proximity ranges (conversation, awareness, sight).
- The pathfinding's "step-around-each-other in hallways" with consistent pass-side as personality micro-trait.
- `SocialMaskComponent`, `irritation`, `vulnerability` inhibition.

---

## 5.1 — PersonalSpaceComponent + spatial comfort fields

### The human truth

Different people have different *bubbles.* Some are happy at six inches; some need three feet. The bubble shrinks with closeness (friends, family, romantic partners) and stretches in the wrong context (the boss, the harasser, the coworker who hasn't earned closeness yet). Violations of the bubble produce a small involuntary withdrawal — a step back, a turn, a posture closure. The violator may or may not notice.

### What it does

Each NPC has a `PersonalSpaceComponent` that defines:

- `defaultBubbleRadius` — the comfortable distance from any other NPC. Low-extraversion + high-neuroticism → larger bubble. High-extraversion + high-agreeableness → smaller bubble.
- `relationshipModifiers` — per-other-NPC adjustments. Friends get smaller bubbles; aversion-link targets (cluster 03.5) get larger; romantic partners get near-zero.
- `currentlyTolerable` — a transient field that *shrinks* under social pressure (you can't back away in a meeting) and *expands* in private. Mask cost is paid when the bubble is being violated below tolerable.

A `BubbleViolationEvent` fires when an NPC enters another NPC's bubble below tolerable. The receiving NPC pays a small `mask` draw per tick of violation, a small `irritation` accumulator bump, and *physically* responds — a small step away, a small turn-of-shoulder, a leaning-back. These micro-movements are observable and constitute most of what people read as "they don't want to be that close to her."

### The asymmetry of bubbles

Two NPCs with mismatched defaults are a sustained micro-tension. The close-talker has a small default; the personal-space-defender has a large one. Every conversation between them is a slow chase: close-talker steps in, defender steps back, close-talker pursues, defender ends the conversation early. Neither is conscious of doing this. The relationship matrix records it as low-warmth and neither understands why.

### Components

- `PersonalSpaceComponent`.
- `BubbleViolationEvent` on the proximity bus.

### Personality + archetype gates

- Vent (Donna) is a close-talker. She also touches arms during conversation. Her bubble is small *and* she fails to read others' larger bubbles.
- Hermit has the largest bubble in the office. Conversations at conversation-range still feel violating to him; he prefers awareness-range.
- Climber has a moderate bubble that *adapts* — they read other NPCs' bubbles consciously and modulate. This is a learned skill that costs willpower.
- Newbie's bubble is *uncalibrated* — they don't yet know who in this office takes what kinds of closeness. They overcorrect to formal distance early, then drift.
- The Founder's Nephew has a small bubble and disregards everyone else's.

### Cross-system interactions

- **Conversation flow:** the bubble-mismatch ends conversations early. This produces an observable pattern — short conversations with this pair, longer with that pair.
- **HallwayPassing:** the consistent-pass-side personality trait is part of bubble navigation. Two NPCs with mutual high-discomfort steer to opposite walls.

### What the player sees

The dance of conversations on the floor. Some pairs lean in; some pairs angle outward. Some NPCs have conversations that *spiral* across the floor as one keeps stepping forward and one keeps stepping back.

### The extreme end

The all-hands meeting where everyone is required to stand close together. The NPCs with the largest bubbles are running mask draw the entire time. The Hermit is the one who breaks away and takes up a corner of the room "to see the screen better." Everyone reads it correctly. Nobody comments.

---

## 5.2 — TouchEventSystem (the handshake, hug, shoulder-pat, accidental touch)

### The human truth

Office touch is a load-bearing social technology that nobody ever discusses. The handshake on first meeting. The shoulder-pat from a senior to a junior. The hug at the going-away party. The accidental brush in the breakroom. The hug-or-handshake ambiguity at the moment of greeting someone you haven't seen in a while. The wrong choice is mortifying. The right choice is invisible.

### What it does

A `TouchEvent` is a discrete proximity event with:

- `kind` — `handshake | hug | shoulderPat | armTouch | accidentalBrush | highFive | fistBump | romanticTouch | highHug`.
- `initiator`, `receiver`, `intent` (consensual/ambiguous/unwanted).
- `surprise` — was it expected by the receiver?

The `intent` field is the load-bearing one. The event is computed by:

```
receiverComfort =
   relationshipBaseline(initiator, receiver)
   + currentBubbleTolerable(receiver)
   + touchKindAcceptability(kind, receiver)
```

If `receiverComfort` is positive, the touch lands as warmth (small `affection` and `belonging` bumps for both). If negative, the touch produces visible discomfort, a `mask` draw on the receiver, and a small `suspicion` accumulator entry on the initiator. If strongly negative, the receiver pulls away visibly and the floor reads it.

The hug-or-handshake ambiguity is its own micro-event. When two NPCs approach each other for greeting, both of them roll independently for *their preferred touch*. If they mismatch, an *awkward greeting* event fires — the half-hug-half-handshake, the side-step, the laugh-it-off. Other NPCs in line of sight read it.

### Components

- `TouchEvent`.
- `TouchPreferenceComponent` per NPC: `handshakeBaseline`, `hugBaseline`, `shoulderPatBaseline`, `acceptUnsolicited[]`.

### Personality + archetype gates

- Vent (Donna) is a hugger. She hugs everyone. Some NPCs accept it; some endure it; some have started avoiding her because of it. The simulation lets all three coexist.
- Climber adjusts touch by audience. Big handshake to superiors, light shoulder-pat to peers, nothing to subordinates unless strategic.
- Hermit's touch baseline is near zero. They use the handshake mechanically; they avoid hugs.
- The Founder's Nephew touches without permission. Some of these events are unwanted-touch events. The chronicle should record them.

### Cross-system interactions

- **Affair / Crush archetypes:** touch events between these archetypes carry double-weight `attraction` updates. Accidental brush in the hallway becomes load-bearing data.
- **HarassmentReporting (potential cluster 09 sub-system):** repeated unwanted-touch events from the same initiator accumulate; the receiver's `KnowledgeComponent` records a pattern; eventually a report event fires (or not — the world bible commits to honest depiction of bad-old-2000s norms; some patterns persist unaddressed).
- **Romance arcs:** the slow-burn touch progression (handshake → shoulder-pat → arm-touch → "accidental" longer-touch → handhold → romantic-touch) is a calcifiable arc shape that the engine can produce.

### What the player sees

Greetings, mostly. Some are warm; some are stiff; some are awkward. A few touch events per save are *significant* — the unexpected hug, the rejected one, the slowly-escalating series.

### The extreme end

The going-away party (cluster 10) for an NPC the floor is divided about. The line of goodbyes. Each touch event is a calibrated decision — handshake for the person who didn't really know them; hug for close friends; the awkward half-hug for the two coworkers who slept together once and never talked about it. Reading the line of goodbyes is reading the entire relationship matrix in two minutes.

---

## 5.3 — CubicleVisibilitySystem (the over-shoulder reader, the hover)

### The human truth

The cubicle wall doesn't reach the ceiling. People walk past your screen. The boss leans into your cube to ask a question and stays. The coworker who reads over your shoulder while they wait. The hover — someone standing behind you, not saying anything, expecting you to feel them. These violations happen even when the bubble (5.1) isn't violated; visual intrusion is its own thing.

### What it does

`VisualPrivacyComponent` per NPC tracks:

- `screenSensitivity` — how much they care about screen-watching. High for finance, HR, anyone working on personal mail at the desk.
- `hoverTolerance` — how long they can be passively hovered-over before mask draws.
- `surpriseAvoidance` — the NPC startled by approach-from-behind.

A `HoverEvent` fires when another NPC is in their `awareness range` for >X seconds without conversation, behind them. Each tick of hover draws mask. The hovered-over NPC may turn (acknowledging), continue typing (suppressing), or address ("can I help you" — mask-cost is real for this).

`OverShoulderEvent` fires when an NPC enters conversation range and their facing direction includes the screen. The screen NPC's `irritation` ticks up; they may close the window, angle the monitor, or end whatever they were doing.

### Personality + archetype gates

- Climber hover-watches subordinates as a status move. They know what they're doing.
- Hermit gets the most-aggravated by hover; they have the highest screenSensitivity and surpriseAvoidance.
- Newbie hovers because they don't yet know they shouldn't.
- Founder's Nephew hovers because nobody is going to say anything to them.

### What the player sees

A specific class of micro-confrontation that the floor produces continuously. Some NPCs build a reputation as "the hover person."

### The extreme end

The Hermit (Greg) at his desk. The Newbie hovers behind him for 90 seconds with a question, terrified to interrupt. Greg's mask is draining. The Newbie waits. Greg eventually turns. The Newbie blurts the question. Greg answers in two words. The Newbie leaves. Greg's productivity is shot for the next twenty minutes. The Newbie thinks they did the polite thing. They both did exactly the wrong thing.

---

## 5.4 — HallwayEncounterSystem

### The human truth

Two people approach each other in a hallway. Mid-distance: eye contact starts, a small smile is calibrated. Six feet: do you say "morning"? Three feet: too late to start a conversation. Then the second-pass-in-an-hour problem — what do you do when you saw them already? The third-pass? The pretend-not-to-see? These are real social calculations and they happen dozens of times a day.

### What it does

`HallwayEncounterEvent` fires when two NPCs are about to pass each other in a corridor:

- **First-pass-of-the-day:** acknowledge (small smile, maybe a "morning"). Fairly easy. Most NPCs pass this fine.
- **Second-pass-within-an-hour:** *harder.* The "what do we do" problem. Options: small smile only, small head-bob, short greeting, pretend-not-to-see, stop for a longer chat, take the long way around. Each is a different cost.
- **Third-pass:** awkward becomes baseline. Often the long-way-around solution kicks in.
- **Approaching-the-bathroom-together:** a specific case. Awkwardness peaks because the destination is now known to both.

Calculations involve `agreeableness`, `extraversion`, recent interaction count between the pair, and a small randomness budget. The output is a behavior choice that varies the existing pathfinder (deviate / don't) and may fire a brief dialog moment with the calcify mechanism.

### Cross-system interactions

- **DialogCalcify:** the second-pass greetings calcify into an NPC's signature ("Linda always says *'second time today'*").
- **Movement:** path deviation when an NPC takes the long way to avoid a third-pass is a player-readable signal of their state.

### What the player sees

A floor that's continuously negotiating these tiny calculations. The frequency of long-way-around routing per pair is a quiet relationship signal.

### The extreme end

The Affair and their partner in a hallway during the first week of their affair. Every encounter is a `HallwayEncounterEvent` + a `BubbleViolationEvent` + an attraction-attraction tug. The dance is observable to coworkers within two weeks. Nobody says anything but everyone is starting to know.

---

## 5.5 — ElevatorScene

### The human truth

The elevator is captivity-lite. You're in a small box with one or several people for sixty seconds. Eye contact is regulated by an unwritten rule. Conversation is permitted but constrained. The wrong silence is unbearable; the wrong question is mortifying. Multiply this by being in the elevator with your boss, with someone you don't like, with someone you have a crush on, with someone whose name you forgot.

### What it does

Elevator entity = `BoxEntity` with an `Occupants[]` list and a `floorTransitTime` (the slow elevator from the world bible). On entry, an `ElevatorOccupancyEvent` fires for every occupant.

Each occupant computes a *fast* social calculation:

- **Who's in here?** Read each other occupant's identity, recent encounter history, relationship pattern.
- **Comfort field.** Each pair contributes to a comfort score for the trip. High-trust pairs add positive comfort; aversion-link pairs subtract.
- **Behavior plan.** Outcomes range: small-talk attempt, polite silence, look-at-numbers-only, pull-out-phone-as-cover, pretend-call-on-cell.

The transit time gives drives time to evolve. A high-stress NPC who's been holding it together is now in a small box with their crush for 45 sim-seconds. Mask drains rapidly. Whatever happens next ranges from nothing to a small breakthrough moment.

### Cross-system interactions

- **PhobiaComponent:** claustrophobia triggers in the elevator. The full panic response is possible.
- **RomanceArc:** elevator scenes are low-cost-high-charge moments for the Affair / Crush archetypes.
- **EavesdropSystem:** the elevator is a partial-occlusion zone — what's said in there can be overheard in the hallway as the doors open.

### What the player sees

A small bottle scene every time an NPC takes the elevator. Most are uneventful. Once or twice a save, an elevator scene is *the* moment — the breakthrough, the confrontation, the silence that lasted too long.

### The extreme end

The Climber and the rival they've been undermining for three weeks share the elevator. Doors close. 45 sim-seconds. The Climber tries small-talk; the rival doesn't engage. Mid-trip, the rival looks at the Climber and says one thing — calcifying high-noteworthiness. The Climber hears it. They process. The doors open. The rival walks out. The Climber stands there for an extra moment. Mask is at 20%. They go back to their desk, distracted, for the rest of the day. The chronicle records the elevator scene; nobody else saw it; the relationship matrix shifted in the box.

---

## 5.6 — UnwantedTouch / boundary violation as a recurring concern

### The human truth

The bad-old-2000s office had touch norms that have not aged well. The shoulder-rub from the boss. The hug that lingered. The arm around the waist that could pass for friendly. The "complimenting outfits" that was never about outfits. The world bible commits to depicting these honestly — they're things the world contains and the game can comment on, not endorse.

### What it does

The `TouchEvent` system already has an `intent: unwanted` flag. This system extends it:

- An NPC's record of `unwanted-touch events received` is a state that calcifies. After enough events, the NPC's `vulnerability` and `suspicion` toward the source rise. They begin avoiding the source. They begin warning others (`KnowledgeComponent` transfer, with `someone-needs-to-know` weighting).
- A `floor-warning` mechanic emerges — high-trust transfers about an NPC produce a *protective ring* of NPCs who quietly warn newcomers. This is real workplace behavior.
- The simulation does *not* automatically resolve these arcs through HR action. Sometimes they get reported; sometimes they don't; sometimes they get reported and the report goes nowhere; sometimes the perpetrator leaves on their own and the office quietly heals. The world bible's commitment to mature, honest depiction means the simulation lets bad outcomes happen without authoring them as inevitable.

### What the player sees

Slow patterns. An NPC the floor seems to actively steer newcomers away from. An NPC who has been touching coworkers for years and finally crosses someone who reports it. The chronicle records the trajectory; the player can intervene as ghost-management or not.

### Tone discipline

This is the cluster where tone discipline matters most. The system should be *capable* of representing these patterns without producing them gratuitously. Spawn probability is low. Player-facing handling is restrained. The world is amoral; the simulation gives the player tools to read what's happening; the player decides.

---

## 5.7 — SeatingArrangementSystem (the player as ghost-manager)

### The human truth

The player as ghost-management can rearrange seating. Who sits next to whom is most of the floor's quality-of-life. The world-bible's open question on "controlled proximity as a ghost-management lever" lands here.

### What it does

The player can move NPCs between cubicles. The simulation reacts:

- The territorial claim cost (cluster 04.4) is real. Moving a long-tenured NPC from their desk costs them mask + `irritation` for days.
- The new neighbor adjacency immediately starts producing the cleanliness-mismatch dynamics (cluster 04.1), bubble dynamics (5.1), hallway-traffic dynamics (5.4).
- The proximity-event firing rate between newly-adjacent NPCs spikes. This is the player's lever — putting two NPCs next to each other is forcing their relationship matrix to update.

### What the player sees

A real tool. Moving the Vent away from the Climber relieves daily friction; moving the Newbie next to the Old Hand produces a slow mentor relationship; moving the Hermit anywhere that isn't their preferred space costs the Hermit dearly.

### The extreme end

The player notices Cubicle 12 (named anchor — Mark's old seat). They assign a Newbie to it. The Newbie's `loneliness` and `suspicion` drift slowly, per the world bible's existing commitment to "the emotional charge of the cubicle hasn't been cleaned up." The Newbie doesn't know why they hate sitting there. They ask to move. The player either listens or doesn't. The chronicle records.
