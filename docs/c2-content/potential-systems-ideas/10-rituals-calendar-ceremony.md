# 10 — Rituals, Calendar, and Ceremony

> Birthdays, going-aways, anniversaries, the holiday party, Secret Santa, the retirement, the baby shower. Office life has a calendar of small ceremonies that are obligatory in different degrees, generate political negotiations nobody wants to have, and are some of the most legible-mood-of-the-floor moments the simulation can produce.

---

## What's already in the engine that this builds on

- Simulation calendar.
- `KnowledgeComponent`, `RumorSystem`.
- `FavorLedgerComponent`.
- The named anchors — Conference Room (where most of these happen), Cubicle 12 (Mark's anniversary).
- The existing `LunchRitualSystem`.

---

## 10.1 — BirthdayCardCirculationSystem

### The human truth

Someone has a birthday. A card circulates. It moves desk to desk; signatures accumulate; some people write paragraphs, some just sign their name, some skip it, some sign and don't read what others wrote. The card *itself* carries information: who organized it, who signed first, who wrote the longest, who was conspicuously absent. The recipient reads the card afterward and reads the entire floor's relationship to them in a single artifact.

### What it does

A `Card` is an entity:

- `recipient` — the birthday NPC.
- `currentHolder` — who has it on their desk right now.
- `signatures[]` — list of `(signer, message, addedAtTick)`.
- `targetCirculationCount` — number of expected signers.
- `circulationDeadline` — when the recipient gets it.

The card moves desk-to-desk via small `CardHandoff` events. Each NPC who receives it pays a small attention tax (must sign before passing) and either:

- **Signs warmly** — paragraph, personalized.
- **Signs perfunctorily** — name only.
- **Holds it for too long** — guilty, doesn't know what to write, eventually defaults to perfunctory.
- **Skips** — passes without signing. Rare, calcifies as a pointed snub.

### The organizer role

A `CardOrganizer` is the NPC who initiated the card. This is a small unpaid social-labor role. The Vent (Donna) does it for everyone. Some floors have a default organizer who has been doing it for years; their absence (vacation, sick) means *cards don't happen* for the duration.

### Cross-system interactions

- **FavorLedgerComponent:** card-organizing is positive favor to the recipient and to the office at large. Calcified Vent-energy.
- **KnowledgeComponent:** the recipient *reads* the signatures and updates their model of who-cares.
- **PassiveAggression:** a deliberately-perfunctory signature from someone who normally writes paragraphs is a passive-aggressive act.
- **Birthday-not-organized:** no card → the recipient knows. The day passes; they say nothing; the floor pays a small `belonging` debt to them.

### Personality + archetype gates

- The Vent organizes cards.
- The Climber writes calibrated messages — warmer to superiors, perfunctory to subordinates, friendly-but-distant to peers.
- The Hermit signs his name. That's it. It's calcified.
- The Cynic *skips* sometimes. Pointedly.
- The Old Hand writes a real message. It always lands.

### What the player sees

A card moving across the floor. The day it lands. The look on the recipient's face when they read the messages. Some birthdays land warmly; some land cold; some don't happen and that's the loudest of all.

### The extreme end

The Newbie's first birthday at the company. The card barely circulates — only the people they directly work with sign. They open it. The signatures are sparse. They don't say anything. Their `belonging` drops 20 points; their loneliness climbs. Three months later the Vent notices and quietly makes sure the next round of cards starts at the Newbie's desk.

---

## 10.2 — GoingAwayEvents

### The human truth

When someone leaves (resigns, fired, retired, transferred), the office goes through a specific sequence: the announcement, the awkward week (or two-weeks), the going-away gathering, the empty desk, the slow forgetting. Each phase carries different social mechanics. Going-aways for fired NPCs are different from voluntary leaves; retirements are different from job-hops; the temp-leaving is different from the long-tenured-leaving.

### What it does

`GoingAwayArc` per leaving NPC:

- `phase` — `announced | transition | leaveDay | postLeave`.
- `kind` — `resigned | fired | retired | transferred | temp_ending`.
- `daysUntilLeave` — counter.

Each phase's mechanics:

- **Announced.** Wave of `AnnouncementEvent`. Floor's `KnowledgeComponent` updates collectively. Reaction varies by kind. Resignations carry suspicion (why?); firings carry awkwardness; retirements carry warmth.
- **Transition.** Two-weeks-or-equivalent. Behavioral changes — leaving NPC's mask is different (less effort to maintain), workload tapers, knowledge transfer happens. Other NPCs negotiate around the absence.
- **LeaveDay.** The going-away gathering (Conference Room or breakroom), the cake, the speeches. The hug-or-handshake calibration moment (cluster 5.2) plays out for every coworker.
- **PostLeave.** Empty desk. Slow forgetting. Their Slack/IM ID stays in autocomplete for a year. Some of their personal artifacts left behind. Mark's Cubicle 12 charge is the world-bible canon of this state at maximum-decay-resistance.

### Going-away gathering mechanics

A `GoingAwayEvent` in the Conference Room or breakroom:

- Some NPCs attend; some don't. Attendance reads as relationship-to-leaving-NPC.
- The cake. Dietary restrictions matter (cluster 06.2).
- The speeches. The leaving NPC says something. Coworkers say something or don't. Mask draws are real for everyone.
- The hugs. The handshakes. The awkward-half-hugs.
- The card (cluster 10.1) plus any group gift.

### Cross-system interactions

- **NamedAnchorEmotionalCharge:** the empty desk acquires a `Cubicle 12`-style charge that decays slowly.
- **FavorLedgerComponent:** unresolved favor balances at departure are realized. The favors-still-owed balance is carried as warm-ambient memory; favors-unpaid are forgiven or quietly resented.
- **RelationshipMatrix:** patterns referencing the leaving NPC carry forward as memory but stop generating new events.

### What the player sees

A predictable arc that nonetheless varies dramatically by kind and circumstance. The fired-NPC going-away is one of the most uncomfortable scenes the engine produces. The retiring Old Hand going-away, by contrast, is a warmth event that the floor remembers.

### The extreme end

A firing that happened on a Friday. The fired NPC's last day is the same day. They walk to their desk. They pack a box. The HR person walks them out. The floor goes silent for the rest of the day. No card; no gathering; no speech. By Monday the desk is cleared. The NPC who was fired had relationships, had favors-owed, had unfinished tasks. None of it gets resolved through normal goodbye channels. Three coworkers privately email or call them over the next month; one becomes a long-term friend; two never speak again. The chronicle records the firing as a Friday-afternoon mass-mood event.

---

## 10.3 — HolidayPartySystem

### The human truth

The company holiday party is a known mask-collapse event. Alcohol is provided; formal context is partially relaxed; coworkers see each other in clothes they don't normally wear; the affair gets advanced (or starts); someone says the thing they've been holding back; the next morning is the most-uncomfortable Monday of the year.

### What it does

A `HolidayPartyEvent` is a major calendar event with its own room (off-site, or the Conference Room scaled up) and its own mechanics:

- **Alcohol available.** NPCs choose to drink based on `inhibitions.substance` (low → drink), home-state pressure, and social-pressure-to-drink-with-the-group.
- **Mask draw is reduced** — the party context is *understood* to permit some unmasking, but only some. Mask still draws when the mask is way past appropriate-for-context.
- **Touch boundary loosened** — hug events more common, shoulder-pats from people who never normally do them.
- **Conversation cross-section** — NPCs who never normally talk find themselves in conversation. Information transfers across normally-disconnected pairs at high rates.
- **Specific archetype effects:**
  - Climber drinks one glass deliberately and works the room.
  - Vent gets emotionally honest after two glasses.
  - Hermit attends for thirty minutes and leaves.
  - Recovering archetype spends the entire party near the soda table or doesn't attend.
  - Cynic mocks the whole thing while attending fully.
  - Old Hand has been to twenty of these and pacing themselves.
  - Newbie over-drinks and doesn't realize it's a problem until the next morning.
- **The dance floor** — if there is one. Some NPCs go; most don't; the ones who do reveal something.
- **The early-leaver vs. the closer** — the NPC who's home by 9pm vs. the NPC who closes the bar. Each is character data.

### Day-after mechanics

- **Mass mood lull.** Whole floor's productivity dips.
- **Specific high-noteworthiness events from the night before** — calcified into permanent floor knowledge.
- **The face-saving negotiations** — NPCs who said too much hover near the relevant coworker to triangulate whether the conversation was as bad as they remember.
- **The relationship-matrix shifts** — pairs that talked at the party either warm up or cool down based on what was said.

### Cross-system interactions

- **SubstanceSystem (cluster 12):** alcohol mechanics here are the most public version of the substance system.
- **AffairArchetype:** the holiday party is *the* anchor for affair starts and breakdowns.
- **DialogCalcify:** specific lines from the party calcify with extreme noteworthiness. "Remember when X said Y at the holiday party" becomes part of the office's permanent dialect.

### What the player sees

A multi-hour event that runs through one game evening. The chronicle records dozens of small events. The next week is *colored* by the party.

### The extreme end

The Recovering archetype attends despite their better judgment. The Climber tries to make a deal at the party. The Affair becomes visible to a third party who didn't know. The Old Hand has a heart-to-heart with the Newbie that they wouldn't have had at the office. The Cynic and the Vent end up dancing together — they hate each other professionally — and don't speak about it on Monday. By Tuesday the floor's relationship matrix has substantially reshuffled. The chronicle records dozens of events. Nobody discusses most of them.

---

## 10.4 — SecretSantaSystem

### The human truth

Mandatory-but-voluntary gift exchange. The drawing of names. The matching that turned out unfortunate. The careful-vs-careless gift. The NPC who clearly didn't try. The NPC who tried too hard. The reveal moment where the giver and receiver are publicly paired.

### What it does

Every December (or its equivalent), a `SecretSantaEvent` runs:

- Names are drawn. Each NPC's `SecretSantaTarget` is set.
- A budget is announced. NPCs choose gifts within or outside it.
- Gift effort scales with the giver's relationship to the target. The Climber drawing a superior gives a *thoughtful* gift; the Climber drawing a subordinate gives the minimum.
- Reveal day in the Conference Room. Each gift opened publicly.

### Specific events

- **The bad match.** The NPC who drew their aversion-link target (cluster 03.5) is paying mask cost the entire month.
- **The thoughtful gift to a near-stranger.** Sometimes a Secret Santa pairing produces a real moment of connection. The chronicle records.
- **The phoned-in gift.** A perfunctory item, clearly last-minute. The recipient reads it.
- **The over-the-budget extravagance.** Reads as showing-off or as ingratiation depending on who.
- **The accidentally-personal gift.** The giver knew the recipient's deal somehow; the gift lands. Sometimes warmly, sometimes invasively.

### Cross-system interactions

- **KnowledgeComponent:** the gift requires the giver to *know* something about the recipient. The Secret Santa exchange is a *test* of how well coworkers know each other.
- **FavorLedgerComponent:** the thoughtful gift moves favor balance.
- **AffairArchetype:** Affair partners drawing each other (or *not* drawing each other) is significant.

### The extreme end

The Hermit (Greg) draws the Vent (Donna). His gift is something specific, perfect, that he had to have noticed something about her over years. Donna sees it, pieces it together that *Greg knew she liked X*, looks at him from across the room, doesn't know what to do with this information. Their relationship matrix shifts permanently. Greg doesn't say anything. He never does.

---

## 10.5 — AnniversaryDateSystem (private and shared)

### The human truth

Specific dates carry specific weight. The day a coworker died, six years ago. The day Mark left. The day someone got divorced. The day an NPC stopped drinking. Many of these are private; some are shared. The simulation calendar can encode them and the engine can produce subtle behavior shifts on the date without anyone naming the date.

### What it does

Each NPC has a `PrivateCalendar[]` of private anniversary dates. Each entry: `{date, kind, intensity}`. On matching dates, the NPC's mood and willpower baseline shift accordingly. They don't tell anyone.

The world has a `PublicCalendar[]` of shared anniversary dates: Mark's day. The day the company almost folded. The day of the big merger. On these dates the *floor's* baseline mood shifts — the whole office is a little quieter.

### Cluster of private anniversaries that should exist by default

- Death anniversary of a parent / partner / sibling / child / friend.
- Sobriety anniversary (positive, but charged).
- Divorce-finalization anniversary.
- Birthday of someone they lost.
- The accident anniversary.
- The diagnosis anniversary.

These are *generator* outputs at NPC spawn. Most NPCs have 0–2; some have more.

### Cross-system interactions

- **HomeStateBleed (cluster 09):** anniversary dates compound bleed-through.
- **SmellMemory (cluster 07.8):** anniversaries can be sensory-triggered too.
- **NamedAnchorEmotionalCharge:** the Cubicle 12 charge has a *date* component — it spikes on the actual day Mark left.

### What the player sees

Subtle. An NPC who has a quiet day on a date that the player can read in a debug overlay. With enough save time, a pattern emerges.

### The extreme end

The Old Hand has a date in their PrivateCalendar — fifteen years since their first child died. Every year, on that date, they're at work. They never told anyone. The Vent figured it out three years ago because she pays attention. Each year on that date the Vent brings them coffee and doesn't say why. The Old Hand knows the Vent knows. They've never spoken about it. The chronicle records: every year, on this date, the Vent and the Old Hand share a coffee.

---

## 10.6 — RetirementArc

### The human truth

A long-tenured NPC retires. The arc is months long. The countdown. The handoffs. The growing detachment. The retirement party. The empty cubicle that will never quite be filled. Some retirees come back as consultants; some are never seen again; some die within a year and the office hears about it through the grapevine.

### What it does

`RetirementArc` is a specific kind of going-away with longer pre-leave runway:

- **6 months out:** the NPC has decided. They may or may not have told anyone.
- **3 months out:** they've told. The floor adjusts; new energy goes into them; the Old Hand gets *more* warmth from the floor in their final months than they had for years.
- **1 month out:** active handoff. They show younger coworkers things that took them twenty years to learn.
- **Final week:** quiet. They clean their desk. They take artifacts home.
- **Retirement day:** the party. Speeches. Hugs. The CEO might attend.
- **Post:** the empty desk. The folklore that builds around them.

### Cross-system interactions

- **GoingAwayEvents (cluster 10.2):** the retirement gathering is a kind of going-away.
- **NamedAnchorEmotionalCharge:** their desk acquires charge proportional to tenure.
- **DialogCalcify:** their tics and phrases live on as the office's calcified memory of them. Years later, NPCs say "as Frank used to say" without explanation.

### What the player sees

A long arc that's mostly warmth. The retirement party is one of the most-positive events the simulation can produce.

### The extreme end

The Old Hand retires. Their retirement party is the highlight of the calendar year for the office. Everyone attends. The chronicle records dozens of small moments. Three years later, when the Newbie has become the Old-Hand-equivalent, they reference Frank's tic the way Frank referenced his predecessor's tic. The simulation has produced *generational continuity* — calcified culture passed down across departures.

---

## 10.7 — BabyShowerAndOtherLifeMilestones

### The human truth

Wedding. Engagement. Pregnancy disclosure. Adoption. Graduation. House purchase. These are *life events* in a coworker's life that the office decides whether to acknowledge. The decision, the awkward gathering, the weird-feeling gift, the people who feel left out, the people who don't know what to do.

### What it does

`LifeMilestoneEvent` for any of the above. Mechanics are similar to going-away gatherings — a small in-office event, a card, sometimes a gift. The differences are texture-only.

### What the player sees

Episodic warm events. The pregnant Climber's baby shower happens whether the floor is comfortable with the disclosure or not. The chronicle records who attended.

---

## 10.8 — CalcifiedAnnualRecurrences

### The human truth

The office produces its *own* annual events through accumulated calcification. The Christmas decorating that one specific coworker does every year. The birthday cake from a specific bakery. The trip to the trade show that the same three people go to. These weren't authored at office founding; they emerged. Once they exist, breaking them is a small drama.

### What it does

The simulation tracks *behavior patterns that recur* and, when a pattern hits a threshold (e.g., 3 consecutive years of Vent decorating the breakroom for Halloween), it gets registered as a `CalcifiedRitual`. The ritual then has its own:

- `expectedExecutor` — the NPC who's been doing it.
- `expectedDate` — the date or window.
- `successCount` — how many years it's run.

When the expected executor *fails* to perform the ritual (vacation, illness, departure), the office notices. Other NPCs may pick it up; the ritual may die; the dying ritual is itself a chronicle event ("nobody decorated for Halloween this year").

### Cross-system interactions

- **DialogCalcify:** language calcifies the same way; cluster 16 captures this.
- **NamedAnchorEmotionalCharge:** rituals may attach to specific anchors (the breakroom decorated for Halloween every year).

### What the player sees

The save's culture, slowly accumulating into recognizable patterns over months and years.

### The extreme end

The Vent's mother dies in November. She doesn't decorate for Halloween. The breakroom is bare. Three coworkers who never paid attention before realize how much they depended on her doing this. The next year, two of them help her decorate. The ritual evolves. The chronicle records the year the Vent didn't decorate; future years reference it.
