# World Bible — Working Draft

> Co-authored by Talon and Opus. The canonical world-bible the engine and Phase 1+ Sonnets read against. Iterated as we go.

---

## What this game is

A 2.5D top-down office management sim for adults who grew up on The Sims. Sims-style emergent social play, Rimworld-style management, and the moment-to-moment lived-in mess of *The Office* and *Office Space*. The player is a ghost-camera who watches and influences fifteen-ish people working through their day — and through the slow accumulation of their lives — in an early-2000s office building that has been here too long and seen too much.

The game is mature. Sex, drugs, depression, infidelity, shame, midlife regret. Real adult life, not its sanitized cartoon. Sexual humor lands when it lands; some characters roll with it and some are quietly disgusted, the same way a real office runs. Office affairs happen. People sleep with each other's spouses. People cope with substances. People grieve. The game does not flinch from this — but it doesn't sensationalize it either. The default register is mundane. The spikes are what make it.

---

## Tone

**Primary register:** dry-cynical office sitcom — *Office Space*, the office floor of *Severance*.
**Secondary register:** warm-absurd ensemble — *The Office (US)*, *Parks & Rec* moments.

**Comedy contract:**

- Sexual humor is allowed and characters react in-character (some are crass, some are uncomfortable, some are oblivious).
- Profanity is normal but not constant.
- Drug references are casual.
- Depression and shame are depicted honestly.
- Conflict is interpersonal, not violent.
- Workplace harassment in the bad-old-2000s sense exists as a thing the world contains and the game can comment on, not as something it endorses.

The game is *about* office culture, including its uncomfortable parts.

---

## Setting

Early 2000s. Specifically: post-Y2K, pre-iPhone. CRT monitors on every desk. Cables everywhere. Pagers still dominant, flip phones just emerging. Square cubicles, square monitors, square everything. The fluorescent lights buzz. The carpet is older than half the staff. The AC works on Tuesdays.

The company is unspecified for now — "the company" is enough placeholder. It does *something* — it has shipping, has production, has cubicles full of people doing knowledge-work-or-something. The point is the people, not the product. (We'll commit to a specific industry later when an order-types catalog actually needs it.)

---

## The building

Three floors:

- **Lower level (basement)** — Shipping and production. Loading dock. Fluorescent-lit, cooler than upstairs, smells like cardboard and machine oil. Fewer people but the people who are here are different from upstairs people.
- **First floor (ground)** — The bulk of the workforce. ~15 cubicles (not always all filled). Benches, workstations, and assembly areas mixed in with the cubicles. The IT closet is down here — its server room glows a pale yellow from all the blinking LEDs and runs ten degrees cooler than the rest of the floor.
- **Top floor** — ~10 offices with doors. The execs and the big wigs. Carpet is nicer up here. Coffee is better. The view is the parking lot.

Hallways and stairwells connect the floors. One slow elevator that nobody trusts. A parking lot with assigned spots that nobody respects. A small outdoor area where people eat lunch when the weather permits.

---

## Population

**15 typical, 30 hard cap.** Beyond 30, the engine starts paying measurable per-Haiku-call costs and the player can't realistically track that many lives. 15 is the sweet spot for an ensemble where every face is a name.

---

## Named anchors — specific places, not categories

These are the load-bearing locations the simulation cares about. Each is a specific *thing*, not a category — the place has its own gravity. Add to this list as the world fills out; this is a starting set.

- **The Microwave** (first-floor breakroom) — has a smell. The smell is older than three of the current employees. Cleaning it is a passive-aggressive volunteer rotation.
- **The Fridge** (first-floor breakroom) — covered in passive-aggressive notes. *PLEASE LABEL YOUR FOOD. WHOSE TUPPERWARE.* There's one container in the back that's been there over a year and nobody will touch it.
- **The Window** (first floor, north wall) — the one everyone gathers at when there's a delivery, weather, or anything else worth seeing. The good gossip spot.
- **The Supply Closet** (first floor) — calls itself a supply closet, is actually a graveyard of obsolete office equipment going back to the late 1990s. Three printers nobody knows how to use. A box of floppy disks. A coffee maker missing its carafe.
- **The IT Closet / Server Room** (first floor) — Greg lives here, mostly. The lights are off because the LEDs are enough. Smells like dust burning off heatsinks.
- **The Parking Lot** (outside) — assigned spots. People do not respect the assignments. There have been incidents. There is a sign, hand-laminated, that reads NO FIGHTING IN THE PARKING LOT — ASSIGNED SPOTS ARE ASSIGNED.
- **The Smoking Bench** (outside, side of building) — half the staff says they quit. They didn't.
- **The Women's Bathroom** (first floor) — Donna haunts this. Stories are told.
- **The Conference Room** (top floor) — has a TV nobody can connect to. The HDMI cable goes somewhere mysterious. Important meetings happen here despite the TV problem.
- **Cubicle 12** (first floor) — currently empty. Used to be Mark's. Mark is not discussed.

Each anchor carries a small bundle of state at game start: the things stained on it, the notes attached to it, the smell associated with it, the relationships people have to it. New anchors get the same treatment.

---

## Capability over content

This is the design axiom. The bible commits the engine to *capabilities*, not *content lists*:

- If there are humans in a room, they can interact with each other.
- If there is a fridge, it can be used to store food.
- If there is a window, it lets in light, and that light propagates.
- If a thing exists in the world, the player or an NPC can engage with it in some way — even if the result is just "nothing happened, but it was acknowledged."
- Dialog exists between any two adjacent NPCs, with feelings exchanged. Most exchanges are passive and forgettable. A small fraction land — offend, intrigue, hurt, charm. Those are the ones that get remembered.
- NPCs emote more than they speak. Like silent film at a distance. The player reads body language, position, and observable consequence. Spoken language is decorative; emotional state is content.

We do not pre-author content lists. We author *systems that produce content* and let them run.

---

## Gameplay loop

Watching little humans go to work is not, by itself, a game. The loop is:

**Orders come in. Nobody told you. You're not prepared.**

The player, as ghost-management, is handed work-product expectations from upstairs — a deadline, a delivery, a thing-that-must-happen. The little humans don't all know about it. Some are equipped, some aren't. Most of them have things going on personally that the work-pressure now interferes with. The player nudges (assigns, rearranges, intervenes). The little humans react in-character — some thrive under pressure, some snap, some sleep with the wrong person on the way to a deadline.

Things happen. Some of those things stick.

This is the core: **real office life simulated as a management challenge where the management challenge is the people, not the product.**

---

## Persistence threshold

Per architectural axiom 8.4, some events stick for the duration of the save. The threshold:

**Sticks:**

- Anything that meaningfully changes a relationship (an affair, a fight, a betrayal, a kindness in a crisis).
- Anything that changes a character's standing (firing, promotion, public failure).
- Anything physical that's hard to undo in real life (a coffee stain, a broken window, the chair-mark on the breakroom wall).
- Anything an NPC names. If two NPCs talk about a thing more than once, it sticks.

**Doesn't stick:**

- Moods, drives, ambient conversations that didn't reach anyone in particular.
- Generic minor friction — dropped pens, unread emails, "I stubbed my toe."
- Atmospheric clutter that resets between sessions.

When in doubt: would the staff still be talking about this in a month? If yes, it sticks.

---

## What's deliberately not committed

- A specific company name or industry. Placeholder until needed.
- Specific scripted story arcs. The game is not narrative; the game is generative. Stories happen because systems run.
- Specific NPC catchphrases or dialogue trees. Voice emerges from gameplay (see cast-bible).
- Specific visual style at the per-asset level (see aesthetic-bible).

---

## Open questions for revision

These would sharpen the bible further whenever you have an opinion. None blocks Phase 1 dispatch.

- The company has *something* it does. Roughly what? (Not for content — for the kind of orders that "come in." Software contractor? Manufacturing? Consultancy? "Making widgets for a vague parent corp" works fine if you don't want to commit yet.)
- A working title for the game.
- Additional named anchors you specifically want — the ten above are starting points. If you've imagined a specific corner that has a story, write it in.
- The seasonal / weekly cadence. Does the game have a calendar that matters? Friday-feeling, end-of-quarter, holiday parties? Or is it abstract office-time?
