# 06 — Culture, Religion, and Identity Friction

> Office life smooths over a lot. People with very different lives, beliefs, observances, and histories spend forty hours a week six feet apart. The smoothing costs willpower. The points where it fails are where a lot of the chronicle lives.

---

## What's already in the engine that this builds on

- The personality space, archetype, and deal catalogs.
- `inhibitions` (which can carry an awareness flag — many cultural inhibitions are `hidden` to the NPC themselves).
- `KnowledgeComponent`, `RumorSystem`.
- The simulation calendar.
- `belonging`, `loneliness`, `trust`, `suspicion` drives.

---

## 6.1 — ObservanceCalendarSystem (the religious or cultural calendar bleed-through)

### The human truth

An NPC who fasts during a season is at work during that season. An NPC who doesn't drink doesn't drink at the holiday party. An NPC whose holidays don't match the company calendar is observing alone. An NPC whose religious observance includes prayer at fixed times needs space and time the office doesn't always provide. None of this *has* to be visible; in many offices it isn't. The simulation can surface these patterns without forcing them on the player.

### What it does

Each NPC carries an optional `ObservanceProfile` with:

- `tradition` — `none | catholic | jewish | muslim | hindu | buddhist | sikh | mormon | jehovahsWitness | secularEthnic | spiritualNotReligious | personalAbstainer`. Open-ended.
- `observanceLevel` — `secular | cultural | observant | strict`. Most NPCs run `secular` or `cultural`; a minority run higher.
- `practices[]` — specific behaviors the NPC observes: `dailyPrayer | dietaryRestrictions | seasonalFasting | holidayObservance | sabbathRestriction | abstainsAlcohol`.

Observance generates *scheduled events* and *contextual modifiers*:

- **Scheduled events:** prayer times produce small `WantPrivacy` events. Fasting periods modify `FeedingSystem` drives differently from baseline.
- **Contextual modifiers:** holiday-party invitations land different for an NPC whose tradition doesn't observe the holiday. Catered lunches with non-matching dietary content force the NPC to either eat nothing, eat only the side, bring their own food, or eat anyway and pay willpower.

### Visible behavior

- **The fasting NPC during Ramadan** (or Lent, or Yom Kippur, or a personal fast): their afternoon energy is different. Their lunch break is different. Their patience may be slightly thinner. Coworkers reading the pattern know without being told.
- **The NPC who steps away at certain times each day:** prayer, meditation, scheduled phone calls (a non-religious cousin). Where they go is part of the simulation — the empty conference room, the supply closet, the parking lot.
- **The NPC whose holiday wasn't on the company calendar:** they came in on what their family considered the most important day of the year. Their mood that day is real but unspoken.

### Components

- `ObservanceProfileComponent`.
- Observance-derived events on the simulation calendar.

### Personality + archetype gates

- `observanceLevel` is independent of the existing archetypes. The Climber can be devout. The Hermit can be deeply observant in private. The Cynic can be an ex-observant who has feelings about the season. The Vent can be the one who *decorates* for every holiday including ones that aren't hers (this is one of the clearest character beats).
- **Hidden observance** — an NPC whose tradition is `hidden`-flagged (they don't tell anyone at work) accrues mask draw on relevant days. They observe in private. The willpower cost is the cost of double life.

### Cross-system interactions

- **Lunch system:** existing system extended — observant NPCs' food choices and fasting status are inputs. The NPC who can't eat the catered pizza without violating an observance may go hungry, or break the fast, or quietly decline.
- **Holiday party:** the holiday party is sized differently for these NPCs. The Christmas-themed party is one thing for the secular Catholic; another for the Jewish NPC; another for the Muslim NPC; another for the Jehovah's Witness who cannot attend at all. Each is a small mask cost or absence cost.
- **Affection / belonging:** an NPC who finds a coworker who shares their tradition unexpectedly — the small `belonging` bump is large because it was unanticipated.

### What the player sees

Quiet patterns. Some NPCs eat differently in some weeks. Some are absent on specific days. Some decorate; some don't. Most of this is invisible noise; occasionally it's a moment.

### The extreme end

A Ramadan that overlaps with a quarter-end deadline. The fasting NPC is doing the work-output cost-curve already documented in WorkloadSystem from depleted physical state, plus the willpower draw of fasting around a non-fasting office, plus the observance-pride drive that says they will *not* break the fast for work. By week three they are running on fumes. The deadline lands. They make it. Their KnowledgeComponent records it as a personal high-water mark; the chronicle records nothing publicly because the NPC told no one. The player can read it if they're paying attention.

### Open questions

- Should the simulation calendar contain authored holiday dates, or generate them per-NPC? Per-NPC is more accurate but expensive. Recommended: a small authored calendar of common dates with NPC-attached fasting windows.
- Tone discipline is critical. Religion in particular should be depicted *with* the same maturity that affairs and recovery are depicted — neither sanitized nor caricatured. The system commits the engine to representing observance honestly.

---

## 6.2 — DietaryRestrictionSystem (the allergy, the vegetarian, the keto, the gluten-free, the kosher/halal)

### The human truth

Office catering is a recurring social problem. The vegetarian gets the side salad. The kosher NPC reads the labels and quietly opts out. The gluten-free NPC has been burned before. The "I'm doing keto for three weeks" coworker generates reactions ranging from supportive to mocking. Allergies are non-negotiable medical realities that the office occasionally fails. The *I-just-don't-eat-X* dietary preferences are constantly subject to social negotiation.

### What it does

Each NPC has a `DietaryProfileComponent`:

- `restrictions[]` — `vegetarian | vegan | kosher | halal | glutenFree | nutAllergy | shellfishAllergy | dairyAllergy | keto | paleo | personalDislike[]`.
- `severity` — `medical | strict | flexible`. Medical is not negotiable; strict is rarely; flexible negotiates.
- `disclosureLevel` — public, selective, hidden. (The hidden vegetarian who eats meat at work to avoid attention is real.)

Catering events (existing LunchRitualSystem extension) are evaluated against each NPC's profile. Outcomes:

- **Compatible:** eats normally.
- **Side-only:** eats just what's compatible. Hunger persists. May supplement.
- **Skip:** eats nothing. Goes hungry until next chance.
- **Bring-own:** legitimate choice; some social cost (eating different from group).
- **Break:** the flexible-restriction NPC eats the wrong thing because of social pressure. Pays internal cost.

Severe allergy events:

- A nut-allergy NPC near a nut-product opens an `AllergenExposureRisk` event. Most ends with nothing happening; rarely an actual `AllergicReactionEvent` fires (cluster 09 cross-reference for medical events).

### Cross-system interactions

- **LunchRitualSystem:** dietary profile is a major axis of who-eats-with-whom (the vegetarians find each other; the keto NPCs commiserate; the kosher NPC eats with the kosher NPC).
- **PassiveAggression:** the labeled food in the fridge ("VEGETARIAN ONLY") is an attack pattern when food-stealing has happened.
- **KnowledgeComponent:** dietary preferences are public knowledge for most NPCs; hidden for some. The mismatch is a slow-burn drama.

### The extreme end

The all-hands lunch. Pizza is ordered. Half the floor's dietary restrictions are accidentally violated by the choice. Three NPCs eat nothing. One has an allergic reaction (mild — the cheese had hidden ingredients). The chronicle records who organized the lunch (the Newbie, trying to be helpful) and the outcome. The Newbie is mortified; their `belonging` drops; their `WorkloadComponent` next-task quality is degraded for two days from the shame.

---

## 6.3 — GenerationGapSystem (boomer / Gen-X / millennial in the early 2000s)

### The human truth

The early-2000s office is a generational microcosm. The Boomer who's been here since 1987. The Gen-Xer with the goatee who runs IT. The 22-year-old Millennial who just graduated and treats the job like a gig. They have different work norms, communication norms, technology norms, attitudes toward authority, expectations for hours, and specific phrases that produce immediate eye-rolls in the other camps. The friction is a constant low-grade noise.

### What it does

Each NPC has an `Age` and a derived `Generation` enum (`silent | boomer | genX | millennial | younger`). Generation modifies:

- **Communication preference**: Boomer-default to phone and walk-over; Millennial-default to email or IM.
- **Authority expectation**: Boomer expects deference; Millennial expects collaboration.
- **Workplace norms baseline**: how-late-is-OK-to-be, how-formal-to-dress, how-much-personal-life-at-work-is-acceptable, what-counts-as-rude.
- **Technology comfort**: cross-system multiplier for cluster 08's tech-friction interactions. Boomers have higher friction with the slow PC; younger NPCs have low friction.
- **Calcified phrases that read across the gap as cringe**: the Boomer who says *"think outside the box"* unironically; the Millennial who says *"literally"* every other sentence. Each generation has tics that the others perceive as identity markers.

### Friction events

When NPCs of different generations interact under stake, friction events fire:

- **WorkflowMismatch:** the Boomer brings a printout to a meeting; the Millennial brings their laptop. Tension over which is the right way.
- **CommunicationChannelFriction:** the Boomer left a voicemail; the Millennial doesn't check voicemail. The follow-up over email is sharper.
- **AttitudinalConflict:** the Boomer thinks the Millennial doesn't respect the work; the Millennial thinks the Boomer is wasting time.

### Components

- `GenerationProfileComponent` (derived from age + a small individual variance).

### Cross-system interactions

- **Tech friction (cluster 08):** generation × tech-skill matrix.
- **DialogCalcify:** generational tics calcify faster within their cohort and read as cringe across.
- **MentorMentee relationships:** the cross-generation mentorship relationship pattern from the cast bible carries higher reward when it works, higher friction when it doesn't.

### What the player sees

Patterns of who-talks-to-whom that align suspiciously with generation. Reading them as just-personality vs. just-generation is the player's interpretive work.

### The extreme end

The Boomer Old Hand and the Millennial Newbie are paired for a project. Three weeks of low-grade friction over communication norms. The Newbie thinks the Old Hand is condescending; the Old Hand thinks the Newbie doesn't show up. By week four, a single conversation happens — the Old Hand notices the Newbie is staying late despite their office hours appearance, and asks how they're doing. The conversation cracks the friction. The chronicle records the moment. The relationship pattern shifts from `frictioning_cohabitants` to `mentor / mentee`. The save's social structure quietly reshapes.

---

## 6.4 — AccentAndCodeSwitchSystem

### The human truth

People code-switch at work. The accent strengthens or weakens. The grammar shifts toward "professional." The dialect that's home turns into the dialect that's office. Performing this is exhausting. The home-self leaks through under stress. The unguarded moment when someone speaks in their home register at work is a specific kind of mask-slip that's both vulnerable and warm.

### What it does

Each NPC has a `RegisterProfile`:

- `homeRegister` — their unguarded register (one of the dialog bible's six values, plus possibly accent or dialect tags).
- `officeRegister` — their performed register at work.
- `switchCost` — willpower per sim-hour to maintain the gap. Higher if the home and office registers are far apart.
- `slipProbability` — under high stress + low willpower, the home register breaks through.

The dialog bible's existing register field reads from `officeRegister` normally. Under mask-slip conditions, retrieval shifts to `homeRegister` for a fragment, with high noteworthiness. Coworkers who hear it record an updated register-profile read of that NPC.

### Cross-system interactions

- **Recovering archetype:** the day they go back to their home register at work is often a sign things are stabilizing inside.
- **Affair partners:** sometimes share a private register only used with each other.
- **Dialog calcify:** home-register fragments calcify slower (used less), but when they do, they're significant.

### What the player sees

The NPC who today sounds different. The two-word phrase from a coworker that suddenly reveals where they're really from. Other NPCs hearing it adjust their model of who the speaker is.

### The extreme end

The Climber, who has been performing a `formal` office register for years, has a panic attack at their desk (cluster 03.3). When the Old Hand reaches them and asks if they're OK, the Climber answers in *folksy* — their grandmother's dialect, the home register they've never used at work. The Old Hand registers it. They don't comment. The Climber recovers. From that day forward, when the Old Hand and the Climber are alone, the Climber sometimes uses folksy phrases. Nobody else has ever heard them.

---

## 6.5 — DressCodeSubcultureSystem

### The human truth

The dress code is a written rule and a thousand unwritten ones. Casual Friday is its own subculture. The person who always dresses one notch above. The person who dresses one notch below. The day someone over-dresses for nothing in particular and the office speculates. The day someone under-dresses and the office reads it as not-OK. Dress is a continuous social broadcast.

### What it does

`AttireComponent` per NPC:

- `attireBaseline` — `casual | businessCasual | businessFormal | sloppy | trendy | era-bound`.
- `currentAttire` — today's actual outfit (a small drift from baseline).
- `signalingIntent` — `none | impressing | rebelling | hiding | celebrating | mourning`.

The signaling intent is the load-bearing field. An NPC with `impressing: true` today is up a notch (interview suit; presenting; the new sweater they wanted to be noticed in). The floor reads it. The Climber reads it as competition; the Vent reads it as a cue to compliment; the Hermit doesn't notice.

Casual Friday: the entire floor's `attireBaseline` shifts down one notch. Outliers in either direction are noted.

### Specific signals

- **Suit on a non-suit day:** interviewing somewhere else (high suspicion) or going to a funeral (sympathy). Coworkers can rarely tell which.
- **Sweatpants on a normal day:** something is wrong. Sympathy or concern.
- **Brand new outfit:** small `attention` event for the NPC. They're hoping to be noticed.
- **Same outfit as yesterday:** they slept somewhere they didn't plan to sleep.

### Components

- `AttireComponent`.
- `AttireSignalEvent` on proximity events when noticed.

### Personality + archetype gates

- The Climber dresses with intention. The intention rotates per audience.
- The Hermit's attire is invisible; they wear the same kind of thing every day.
- The Cynic deliberately under-dresses on important days as a form of contempt.
- The Founder's Nephew under-dresses because they can.

### What the player sees

A floor that reads each other every morning. Attire is one of the cheapest information channels for the player.

### The extreme end

The Recovering archetype shows up Monday in a *suit*. They never dress up. Everyone notices. They do not explain. By Wednesday they're back in normal clothes. The chronicle records: "Linda wore a suit on Monday." Three coworkers privately speculate; the rumor-system runs theories ranging from job-search to court-date to AA-anniversary. The truth is one of those things or another or something else. The simulation refuses to flatten it.

---

## 6.6 — SocioeconomicBackgroundQuiet

### The human truth

The office contains people with very different financial situations. The one whose parents owned a vacation home talks easily about ski trips. The one who's barely making rent doesn't say. The one who recently came into money is conspicuous in small ways. Money differences are the most-suppressed visible-class signal at work and the most readable to anyone paying attention.

### What it does

Each NPC carries a `BackgroundProfileComponent` with `socioeconomicBaseline`, `currentFinancialPressure`, and `signaling`. Most NPCs run middle; some run high; some run low. The component modifies:

- **Lunch choices** (cluster 08-style cross-system) — does the NPC routinely eat out, brown-bag, or hit the office snacks.
- **Dress signaling** (6.5 cross-reference).
- **Vacation references in conversation.**
- **Friday-night plans.**

Most of this is calcified atmosphere. The system rarely fires *events*; it modulates the constant stream of micro-interactions.

The exception: a `FinancialCrisisEvent` for an NPC with high `currentFinancialPressure`. This pulls them into specific behaviors — eating less, declining group lunches, working overtime they aren't getting paid for, taking a side-hustle phone call in the parking lot at noon.

### Cross-system interactions

- **Stress system:** chronic financial pressure adds to baseline `StressComponent.ChronicLevel`.
- **Affair / Climber arcs:** financial pressure as a motivator for both.

### What the player sees

A continuous quiet differential. Some NPCs always have suggestions for nice restaurants; some always pack a lunch. The player learns the floor's economic geography even though nobody discusses it.

### The extreme end

The Newbie is sleeping in their car some nights (deal catalog: existing). Their commute ritual (cluster 18 in the existing systems doc) shows it. The hygiene drift (cluster 02.3) shows it. The lunch (skipped or cheap) shows it. None of this gets named directly until somebody — usually the Vent or the Old Hand — figures it out and quietly intervenes.
