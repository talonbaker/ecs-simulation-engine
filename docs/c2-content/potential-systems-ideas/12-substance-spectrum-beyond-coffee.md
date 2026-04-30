# 12 — Substance Spectrum Beyond Coffee

> The existing CoffeeSystem covers caffeine. Cluster 01 covers nicotine and the broader CravingSystem. This cluster covers what the world bible refers to as "drugs" — alcohol, prescription pills, recreational, the in-recovery NPC's exposure surfaces. The world bible commits to honest depiction without sensationalizing. The simulation can model these patterns with the same restraint it brings to affairs and grief.

---

## What's already in the engine that this builds on

- `CravingSystem` and `QuitAttemptArc` (cluster 01).
- `CoffeeSystem` and `CaffeineComponent`.
- `inhibitions` — including a likely `substance` class.
- The Recovering archetype.
- The smoking bench named anchor.
- HolidayParty (cluster 10.3).

---

## 12.1 — AlcoholSystem + AlcoholConsumptionComponent

### The human truth

Alcohol is the dominant office substance. After-work drinks. The flask in the desk drawer for the rare NPC. The holiday party. The lunch-meeting wine. The "I'll have a coke" person whose history is unknown to the floor. The hungover NPC at 9:15am Monday. The Recovering archetype walking through it. Everyone has a different relationship to it.

### What it does

`AlcoholConsumptionComponent` per NPC tracks:

- `currentBAC` — alcohol level, decays per sim-hour.
- `dailyConsumption` — tracked against a per-NPC baseline; deviation upward signals stress or escalation.
- `tolerance` — long-tail; chronic-drinkers metabolize differently (a real biological fact the simulation can model without judgment).
- `consumptionPolicy` — `none | social_only | functional | escalating | recovering | relapsed`.

### Where consumption fires

- **Holiday party** — primary venue. Cluster 10.3 mechanics handle social drinking. Each NPC's `consumptionPolicy` shapes their evening.
- **After-work drinks** — sub-group event with regulars. The Climber goes when strategic; the Vent goes always; the Hermit never; the Cynic sometimes.
- **Lunch drinks** — out-of-the-office lunch with one or more drinks. More common in the early-2000s setting than now. Affects afternoon productivity.
- **Hidden-at-desk** — the flask in the drawer. Rare; high stakes. The deal-catalog already mentions "in significant debt"; "drinking at desk" is an adjacent deal that accumulates over a save.
- **Birthday cake / event** — the cake-with-rum situation; a few ounces in a party context.

### BAC-to-behavior mapping

- **0.02–0.05:** loosened; mask draw reduced; small `affection` and `belonging` lifts.
- **0.05–0.08:** clearly mellow; vocabulary register may flip to *casual* even in formal NPCs; touch boundaries soften.
- **0.08–0.12:** noticeable to others; speech changes register, calcifying *high-noteworthiness* fragments; judgment is shaping toward future regret.
- **0.12+:** visibly drunk; coordination affects movement; mask is at 0; the NPC is now in *anything-can-happen* mode for the simulation.

### Components

- `AlcoholConsumptionComponent`.
- `AlcoholConsumptionEvent` per consumption.
- `HangoverComponent` for the morning after.

### Cross-system interactions

- **HolidayParty (10.3):** primary venue.
- **AffairArchetype:** alcohol is the most-common precondition for affair-start events.
- **DialogCalcify:** lines said while drunk calcify with extreme noteworthiness; they outlive the night.
- **RecoveringArchetype:** the entire archetype's arc is anchored in this system.
- **HomeStateBleed (9.1):** chronic over-drinking at home produces hungover-arrival mornings as a frequent bleed-through pattern.

### Personality + archetype gates

- Climber: calibrated; one or two drinks always. Calculated.
- Vent: openly social; consistent moderate.
- Hermit: rarely drinks at all; the IT closet is sober.
- Cynic: drinks freely; doesn't care; doesn't escalate.
- Old Hand: drinks at the rituals only.
- Recovering: zero. The system tracks adjacency events (other NPCs drinking around them as exposure).
- Founder's Nephew: drinks too much; nobody comments.

### What the player sees

A floor with subtle daily and weekly consumption patterns. A few NPCs are drinking more this month than last; one might be escalating. The holiday party is where it all becomes visible at once.

### The extreme end

A high-functioning alcoholic NPC (a relatively new addition to the office, or a long-tenured one) whose consumption has been quietly escalating for a year. The flask in the drawer is now used multiple times a day. Their behavior is *calibrated* — they don't appear drunk because tolerance is high. The simulation tracks the truth. Eventually a precipitating event happens — they're on a sales call and slur a word; a coworker smells the bourbon; the chain of events leads to disclosure or denial. The Recovering archetype, watching from across the floor, recognizes the pattern they once lived. The chronicle records.

### Open questions

- Tone discipline. The high-functioning alcoholic arc must be depicted *with* the same maturity as affairs. Not glamorized; not punished narratively. Just shown.
- Frequency tuning: most saves should have one or zero of these arcs active at any time.

---

## 12.2 — PrescriptionPillSystem

### The human truth

The early-2000s office is full of pills. SSRIs becoming common; benzos for anxiety; ADHD medication; pain killers from a back injury that never fully resolved; opioids from a surgery that turned into a dependence. The pill organizer in the desk drawer. The pharmacy run at lunch. The week the prescription wasn't refilled in time. The friend who's been borrowing meds. Most of this is silent.

### What it does

`PrescriptionProfile` per NPC (most run zero meds):

- `medications[]` — `{class, doseSchedule, sideEffects, withdrawalPattern}`.
- `compliance` — high / moderate / poor.
- `disclosure` — known to office / disclosed to specific friends / hidden.

Effects on behavior:

- **SSRIs:** subtle long-term mood baseline modulation. Side effects: occasional fatigue, occasional sleep disturbance.
- **Benzos:** acute anxiety relief; sleep effect; high cost if missed (withdrawal anxiety spike).
- **ADHD meds:** focus boost; sometimes mistaken for caffeine; the day the prescription runs out is a day of significantly degraded productivity (similar to the existing CoffeeSystem withdrawal mechanics, but with attention-not-energy as the cost).
- **Opioids:** pain relief; alertness reduction; risk of escalation. The recovering-from-opioid-dependence arc is a specific kind of QuitAttemptArc.
- **Pain killers:** chronic pain NPC's daily reality. Productivity affected by both pain and the meds.

### Cross-system interactions

- **WorkloadSystem:** medication effects are real productivity inputs.
- **AppearanceComponent:** the day the medication didn't get taken is a day appearance and demeanor are off.
- **CravingSystem:** the missed-dose mechanic uses the same shape as the nicotine deficit climb.
- **Affair / Climber arcs:** access to others' medication is a deal-catalog territory.
- **Lunch system:** the pharmacy-run-at-lunch is a recurring small event.

### What the player sees

Subtle. A coworker who steps out at 2pm every day to take a pill. The desk drawer that always has an organizer. The day someone is *clearly off* and the player can read the medication-skip in the simulation.

### The extreme end

A long-tenured Old Hand has been on chronic pain medication for a back injury from 1996. The dose has been creeping up for years. They're now in a low-grade dependence that the floor reads as "Frank moves slower in the afternoons." The save's events include a regulatory tightening (the doctor reduces the prescription); Frank's withdrawal arc fires; he's miserable for two weeks; the floor doesn't know what's happening; eventually he stabilizes at a lower dose. The chronicle records the bad weeks. The relationship matrix shifts around the people who quietly covered for him.

---

## 12.3 — IllicitSubstanceSystem (the rare arc)

### The human truth

Some NPCs use recreational substances outside the office. Weed, occasional cocaine, the sometimes-MDMA. This was real in the early-2000s office, mostly not at the office, sometimes at the office. The simulation can have NPCs whose deal-catalog includes recreational use; the engine doesn't make them users gratuitously, but doesn't pretend the world doesn't contain them.

### What it does

For flagged NPCs (small minority), `RecreationalProfile`:

- `substances[]` — what they use.
- `frequency` — daily / weekly / monthly / rarely.
- `usagePattern` — `weekend_recreational | functional | escalating | recovering`.

Effects on simulation:

- Mostly off-screen. Monday-morning recovery from a weekend of use is a `HomeStateBleed` pattern — slightly tired, mildly mood-shifted.
- Bathroom-during-workday for use is rare and would be a specific NPC's deal.
- The smell of weed on someone's clothes after lunch is an `OdorComponent` event other NPCs may detect.

### Cross-system interactions

- **HomeStateBleed (9.1):** Monday-mornings.
- **OdorComponent (existing):** detection events.
- **InhibitionSystem:** `substance` inhibition class.
- **Recovering archetype:** for NPCs in active recovery, casual mention of substances by colleagues is exposure surface.

### Tone discipline

The system permits these NPCs to exist; it doesn't make every save include them. The frequency-tuning curve keeps these arcs rare. The depiction is documentary, not stylized.

### The extreme end

A Climber who occasionally uses cocaine to handle the heaviest weeks. The simulation tracks the energy spike + crash pattern. The floor reads it as a Climber-on-the-warpath, not knowing why. The arc proceeds for months before either resolving (stops on their own) or escalating (quietly gets worse). The chronicle records the calendar pattern; the *why* is in the deal-catalog.

---

## 12.4 — SubstanceAdjacencyExposure (the in-recovery NPC's exposure surface)

### The human truth

The Recovering archetype is, all day, exposed to other people's substances. The smoke smell on a coworker. The wine talked about. The Friday after-work drinks invitation. The pills in someone else's drawer. The casual "I need a drink" comment. Each is an exposure event. Each costs willpower. The recovering NPC's days are continuously paying a cost the office doesn't see.

### What it does

For NPCs with active `QuitAttemptArc` or established `Recovering` archetype:

- Every adjacent substance event in proximity range is an `ExposureEvent`.
- Cost per event: small willpower draw, small `irritation` or `loneliness` bump (depending on the substance and the NPC's history).
- Cumulative exposure over a day: high days *measurably* deplete the recovering NPC's reserves.

### Cross-system interactions

- **CravingSystem (cluster 01):** exposure events accelerate craving deficit climb.
- **HolidayParty (10.3):** maximum exposure in a single evening. The Recovering archetype's holiday party is an arc unto itself.
- **FavorLedger:** an NPC who *protects* the recovering NPC from exposure (the friend who keeps them out of the bar, the Vent who steers conversations away) builds substantial favor balance, often unspoken.

### What the player sees

The Recovering NPC walking past the smoking bench five times in a week without sitting on it. The visible cost of choosing not to drink at the holiday party. The end-of-week exhaustion that's invisible to most coworkers but real.

### The extreme end

Holiday party night. The Recovering archetype attends. They're surrounded by alcohol. Their willpower is being drawn at maximum rate. Three hours in, mask at 12%, they're in a corner with the Vent who is talking about some unrelated topic and *holding their drink at her side without taking a sip* — a calibrated act of solidarity she may not be fully conscious of. The Recovering archetype reaches the end of the night sober. The chronicle records the night as a triumph nobody else fully sees.

---

## 12.5 — TheClubMonday (the weekend-recovery pattern)

### The human truth

Some NPCs bring their weekends to work as Monday morning evidence. The hungover Climber. The smell of last night still on someone. The bloodshot eyes. The 'I went out too hard' acknowledgment with a small grin. The Monday-coffee desperation is the visible side; the simulation has the rest.

### What it does

A simulation-level pattern where NPCs flagged with `recreational` or `social-drinker` usage bring `HomeStateBleed` patterns into Mondays:

- Hangover events, mild fatigue, slightly degraded judgment.
- Specific dialog calcify around Monday-morning context (the recurring "I'm dying" / "never again" lines that calcify per-NPC).
- Productivity lower in the morning; recovers by afternoon.

### What the player sees

The texture of office Mondays. Recognizable across saves.

---

## 12.6 — OvertheCounterAndSelfMedication

### The human truth

Beyond prescription, the office contains coffee (existing), Advil, allergy meds, antacids, melatonin, cough drops. The NPC who keeps a pharmacy in their desk drawer. The office that depends on one person having Advil. The NPC who's *constantly* on something — caffeine in the morning, ibuprofen in the afternoon, Tylenol PM at night. Self-medication is the dominant office health practice.

### What it does

A small additional layer atop the existing Coffee + Caffeine systems:

- `OTCInventory` per NPC: small probability collection of items in their desk.
- `OTCBorrowEvent` — adjacent NPC asks for an Advil. This is a small high-warmth event. The lender accumulates favor balance.
- The NPC who has the *most-stocked* pharmacy in the floor becomes the de-facto medic. Often not the one anyone would expect.

### Cross-system interactions

- **FavorLedger:** OTC-lending is the most-frequent micro-favor in the office.
- **ChronicCondition (cluster 09.4):** OTC use is a daily reality for chronic-pain or chronic-headache NPCs.

### What the player sees

A small predictable pattern of Advil-borrowing across the floor that becomes character data over time.

### The extreme end

The Vent (Donna) has *everything* in her desk. Three years of save time pass. She's lent out hundreds of pills, tampons, cough drops, hair ties. Her cumulative favor balance with the floor is enormous. She's never called any of it in. The floor's `affection` toward her is permanently elevated. When she finally has a bad month (cluster 09.7 — recently lost), the floor responds with the kind of warmth she's been spending all this time generating. The system was always reciprocal; she just collected last.
