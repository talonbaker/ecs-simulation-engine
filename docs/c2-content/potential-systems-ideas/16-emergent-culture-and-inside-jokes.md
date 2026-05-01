# 16 — Emergent Culture and Inside Jokes

> The dialog bible's calcify mechanism turns repeated phrases into an NPC's signature. This cluster scales calcify *up*: from individual-voice to *floor-culture.* The catchphrase that took over. The recurring joke. The shared reference. The single moment from a meeting two years ago that still gets quoted. These emerge from the simulation; they're not authored. They're how a save-specific office becomes its own place.

---

## What's already in the engine that this builds on

- The dialog bible's calcify mechanism per-NPC.
- `KnowledgeComponent` and `RecognizedTic` flags.
- The chronicle (high-noteworthiness events).
- Proximity-event bus.

---

## 16.1 — FloorCultureCalcificationSystem

### The human truth

A specific phrase, joke, reference, or anecdote becomes office property. It started somewhere — a meeting, a slip-up, a typo, a passing comment — and over weeks it spread until everyone knew it. New hires had to be inducted. Years later, people who weren't there still understand the reference. It's part of the office's culture.

### What it does

The dialog calcify mechanism extends from per-NPC to *floor-level* through a shared structure:

- A `FloorCultureItem` is a phrase / joke / reference that has been used by ≥ 3 distinct NPCs in similar contexts within a sliding window.
- Each item carries:
  - `originEvent` — the chronicle entry that produced it (if known).
  - `originNPC` — who said it first.
  - `propagationCount` — how many NPCs have used it.
  - `usageContext` — when it gets deployed (after a meeting? when fixing a printer? when the boss is mentioned?).
  - `noteworthiness` — derived from origin event and current usage rate.
- Propagation happens via the existing dialog calcify with cross-NPC tic recognition. A phrase that gets `RecognizedTic`'d by ≥ 5 NPCs and then *re-used* by them crosses into floor-culture status.

### What floor-culture items look like

- **The catchphrase.** *"That's a Tuesday problem."* (Cynic-origin, used by everyone after six months.) Deploys when something annoying-but-not-urgent appears.
- **The shared joke.** *"HDMI."* (References the conference room's broken cable from `new-systems-ideas.md`.) Said with a sigh whenever any tech doesn't work.
- **The recurring reference.** *"Like Mark."* (Cubicle 12's pre-history canon.) Said when describing someone who's about to leave.
- **The typo that calcified.** *"Defenitly."* (Email typo someone made in 2002 that became how the floor signs off ironically.)
- **The mispronunciation.** *"Ex-press-O."* (One NPC always says it; the floor uses it lovingly when ordering coffee.)

### Cross-system interactions

- **Dialog corpus:** floor-culture items are runtime-added to a *floor-specific* fragment pool that any NPC can draw from with appropriate context.
- **KnowledgeComponent:** *who knows the reference* is itself information. Onboarding a Newbie includes them slowly learning the floor's references.
- **HolidayParty (10.3):** is the highest-yield venue for new floor-culture items. The drunk story that becomes a permanent reference.

### Personality + archetype gates

- The Cynic generates the most floor-culture items. Their dry comments are highly calcify-eligible.
- The Vent propagates them — she's the channel by which a reference reaches NPCs who weren't there.
- The Hermit *uses* floor-culture items rarely but knows them all. When he deploys one, it lands harder.
- The Newbie is the one who hasn't heard the reference yet. The moment they finally *get* it is a small inclusion event.

### What the player sees

A save's office, over months, develops its *own dialect*. The same phrase wouldn't appear in a different save. Players replaying the game encounter a different culture each time.

### The extreme end

Year two of a save. The floor has accumulated ~30 floor-culture items. New hires take weeks to catch up. A Climber's failed presentation from year one is still being quoted. The Vent organizes a retirement party for the Old Hand and her speech is 60% calcified-floor-references — the room laughs at lines that would mean nothing to outsiders. The chronicle records the speech as one of the warmest moments of the save.

---

## 16.2 — CallbackCulture (the recurring reference network)

### The human truth

Within a floor's culture, certain references *call back* to specific events. The callback is a tiny act of memory. Mention something, get a knowing look. The right callback at the right moment is high-warmth.

### What it does

`CallbackEvent` fires when a `FloorCultureItem` is deployed *contextually* — its `usageContext` matches the current situation closely. The deployment triggers:

- Recognition events on listeners — `belonging` and `affection` micro-bumps.
- A small `inclusionScore` increment between speaker and listener pair.
- A noteworthiness increment on the underlying item.

### Cross-system interactions

- **DialogCalcify:** callbacks are calcify hits at the floor level rather than per-NPC level.
- **Inside-joke pairs (16.3):** specific pairs of NPCs may have private callbacks not shared with the rest.

### What the player sees

A subtle continuous warmth across the floor: NPCs nodding at each other when references land. The pattern of which NPCs catch which callbacks is itself a relationship-matrix readout.

---

## 16.3 — PrivateInsideJokeSystem (pair-level)

### The human truth

Some inside jokes are not floor-wide. They're between two or three NPCs. A specific moment they shared, a specific thing one said to the other, a specific running gag they've kept alive. The third NPC hearing it doesn't get it. Asking *what's that about* breaks the spell.

### What it does

`PrivateJokeLink` between a small set of NPCs:

- `members[]` — usually 2, occasionally 3–4.
- `referenceContent` — what the joke references.
- `useContext` — when one of them deploys it.
- `outsiderResponse` — what happens when an outsider hears it (mild confusion, sometimes envy, sometimes the outsider asks and the moment is broken).

### Cross-system interactions

- **Affair / slow-burn:** private inside jokes are often a tell of a deepening private relationship.
- **TensionLink (14.1):** unspoken-tension pairs often build private inside jokes as a *safe* way to share something the rest can't.

### What the player sees

Two NPCs who reference *something* the rest don't understand. The smile that crosses both their faces. The relationship matrix shows the pair-level warmth.

### The extreme end

The Affair partners' private joke set becomes *visibly identifiable* to coworkers — they've been deploying the same callbacks too consistently. Once a third NPC realizes there's a pattern, the rumor system fires. The Affair's exposure escalates from a glance to a knowing.

---

## 16.4 — FloorMemeticEpoch

### The human truth

Some weeks an office has a *thing.* Everyone is talking about the same news event, the same TV show, the same complaint. The shared focus binds the floor briefly and then dissipates. The collective shape is real and recognizable.

### What it does

A `FloorMeme` runs across a sim-window (days to weeks):

- `topic` — what's being discussed.
- `intensity` — how often it surfaces in conversations.
- `peakDate` — when usage is highest.
- `decay` — how it fades.

Floor-memes can originate from:

- An external news event (not directly modeled, but assumed to exist).
- An internal high-noteworthiness chronicle event.
- A shared annoyance or grievance.

During a floor-meme, conversations across the floor have a higher probability of touching the same topic. Floor `belonging` baselines tick up slightly during shared-meme periods.

### Cross-system interactions

- **DialogCalcify:** meme-related phrasings calcify quickly and then de-calcify when the meme passes.
- **FloorCulture (16.1):** some memes graduate to permanent floor culture; most fade.

### What the player sees

A specific week feels different from other weeks. Conversations rhyme. Then it passes.

### The extreme end

The Newbie has a public failure. For two weeks, every floor conversation contains some indirect reference to it. The Newbie's mask is in continuous draw the entire period. By week three the meme dissipates; the Newbie can finally start to recover. The chronicle records both the precipitating event and the meme cycle.

---

## 16.5 — TheRitualThatStarted

### The human truth

A small thing that happened once and became a ritual. Whoever brought donuts that one Friday started doing it weekly. The standing 4pm coffee break that emerged. The specific spot people gather to watch the parking lot during snow. These started somewhere and persist because *somebody is now doing them on a schedule*.

### What it does

The simulation tracks recurring patterns and, when one crosses a threshold, registers a `ProvisionalRitual`. The ritual has an `expectedExecutor` and `expectedDate/Time`. If the executor fails to perform multiple times, the ritual decays. If they continue, the ritual reaches `EstablishedRitual` status (per cluster 10.8 — calcified annual recurrences, but with a finer time grain).

### Cross-system interactions

- **CalcifiedRitual (10.8):** annual recurrence is the long-tail version of this same mechanic.
- **NamedAnchorEmotionalCharge:** rituals attach to specific anchors over time.

### What the player sees

A save's office develops its own week-shape. The donut Fridays. The Wednesday afternoon coffee circle. These aren't authored.

### The extreme end

The Vent brought brownies one Monday after a rough week. The Newbie thanked her warmly. The next Monday she did it again, by accident-on-purpose. By Monday number five it was a ritual. By month three the Vent felt obligated. By month nine she resented it but couldn't stop. By year one it had become *a thing the office has*. The simulation records both the ritual and the Vent's quiet bitterness about it.

---

## 16.6 — TheCalcifiedScapegoat

### The human truth

Some offices develop a person who *gets blamed for things.* The blame is calcified — when something goes wrong, the floor's first guess is them, even when it's someone else. The scapegoat may or may not be aware. Once calcified, this is hard to undo.

### What it does

When an NPC has accumulated multiple `incidentTagged` events (being blamed for visible failures), a `ScapegoatFlag` may set on them. While set:

- New incidents have a higher probability of being initially attributed to them in coworkers' KnowledgeComponent (low-confidence guesses).
- The NPC's `mask` baseline drops as a long-tail effect.
- The chronicle records the recurring attributions.

The flag can be reset by a public *exoneration* event (rare) — a clear case where the scapegoat is publicly cleared.

### Cross-system interactions

- **PassiveAggression:** the scapegoat is the most-frequent target of passive-aggressive notes.
- **Mask:** chronic scapegoating depletes the affected NPC's reservoirs faster.

### What the player sees

A specific NPC who keeps getting blamed for things. The pattern is observable; sometimes deserved, often not.

### The extreme end

The Founder's Nephew inverts the pattern — they're a *reverse* scapegoat (nobody blames them no matter what). The Newbie, by contrast, gets blamed for the Nephew's failures repeatedly. By month four, the Newbie has the ScapegoatFlag set. The chronicle records the asymmetry; the player can intervene as ghost-management or watch it persist.

---

## 16.7 — TheFolkloreLayer

### The human truth

Long-running offices accumulate folklore. The story about what happened at the 1998 Christmas party. The legendary screwup that's now safe to mention. Mark, in Cubicle 12. The vendor who showed up drunk. These become part of *the office's history* — told to new hires, calcified as identity.

### What it does

High-noteworthiness chronicle events (`noteworthiness > 90`), after sufficient sim-time has passed, may transition to `FolkloreItem` status. Folklore items:

- Are *pre-loaded* into the dialog corpus as references.
- Calcify the named anchor where they happened (if applicable) with persistent emotional charge.
- Are *told* to new hires by long-tenured NPCs in informal moments.

### Cross-system interactions

- **NamedAnchorEmotionalCharge:** Cubicle 12 is the canonical example. Folklore items and named-anchor charges are deeply linked.
- **DialogCalcify:** folklore references in dialog calcify special — they're how a save's culture transmits across NPC turnover.

### What the player sees

A long-running save develops its own legends. The player remembers them too.

### The extreme end

Year three of a save. A new hire arrives. Within their first month, three different long-tenured NPCs have told them the story of *the time the printer died for two days during quarter-close*. Each tells it slightly differently. The Newbie pieces together the truth. By the time they're a year in, they're telling it themselves to even-newer hires.

---

## 16.8 — CulturalDriftFromTurnover

### The human truth

When core NPCs leave (especially the Old Hand), a percentage of the floor culture goes with them. Some references die because nobody who remembers is left. Other references are passed down. The culture *changes* over many sim-years — like real offices that feel different a decade later.

### What it does

Per-`FloorCultureItem`, a `survivorshipScore` is updated:

- When an NPC departs, items they were a primary user of lose intensity.
- Items propagated by ≥ 5 NPCs decay slowly even when originator leaves.
- Items still actively-used after a *decade* of save time are deeply-calcified core culture.

### What the player sees

A save that *feels* like its history. Year-one references that are gone in year-five; year-three references that are still alive in year-ten. The chronicle reflects the slow evolution.
