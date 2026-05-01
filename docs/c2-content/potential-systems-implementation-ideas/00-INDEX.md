# Potential Systems — Implementation Ideas Index

> Generated against the bibles (world / cast / aesthetic / dialog / ux-ui / action-gating) and the existing `new-systems-ideas.md`. This set is a complement to the parallel `potential-systems-ideas/` brainstorm — same goal, different cuts. The aim is the next layer in: things office sims usually flinch from, the absurd-but-true, the body in public, the small uncomfortable rituals, the moments people remember from real jobs.
>
> Every idea here respects the engine's commitments: capability over content, emote-more-than-speak, drives gated by willpower + inhibitions, mature but not sensational, diegetic surface (no popups), 2.5D top-down readability.

---

## How these are organized

Each numbered document is a thematic cluster of system proposals. Inside a cluster, each idea follows roughly the same shape:

- **The human truth** — the lived thing the system simulates.
- **What it does** — the mechanism, in engineering terms.
- **Components / data sketch** — what the ECS layer would carry.
- **Mechanics** — how it interacts with existing systems.
- **Extreme end** — the kind of moment the system should be capable of producing without authoring.
- **Archetype / personality fit** — how the cast generator's existing knobs (Big Five, archetype, deal, register, inhibitions) shape its expression.

Where an idea reuses an existing component on the wire (`StressComponent`, `SocialMaskComponent`, `BladderComponent`, `ColonComponent`, `MoodComponent`, `WillpowerComponent`, `InhibitionsComponent`, etc.) it says so explicitly. New components are flagged.

---

## The clusters

- [01-bodily-taboos.md](01-bodily-taboos.md) — flatulence, burping, snot, BO, lipstick on teeth, the things mature offices contain that nobody references in games.
- [02-vermin-and-startle-events.md](02-vermin-and-startle-events.md) — spider on shoulder, cockroach from fridge, mouse in supply closet, fly that won't leave.
- [03-addictions-and-dependency-loops.md](03-addictions-and-dependency-loops.md) — the smoker's 15-minute jitter timer, the coffee→pee chain, vape, prescription bottle, snack reach.
- [04-fastidious-vs-slob.md](04-fastidious-vs-slob.md) — the contamination sphere, the desk territory dispute, who has to sit next to whom.
- [05-culture-religion-belief.md](05-culture-religion-belief.md) — prayer breaks, dietary rules, holiday calendars, modesty differences, code-switching, faith-based composure.
- [06-stims-and-sensory-quirks.md](06-stims-and-sensory-quirks.md) — pen-tappers, leg-jigglers, hummers, throat-clearers, mouth-breathers, gum-poppers.
- [07-romance-attraction-jealousy.md](07-romance-attraction-jealousy.md) — open-secret crushes, jealous spouses, holiday-party kisses, pregnancy reveals, flirt asymmetries.
- [08-shame-and-public-failure.md](08-shame-and-public-failure.md) — pants split, food on shirt, reply-all, hot mic, peed-self.
- [09-secrets-surveillance-snooping.md](09-secrets-surveillance-snooping.md) — locked drawers, sticky-note passwords, browser history, calendar-invite spying, badge swaps.
- [10-grief-and-life-events.md](10-grief-and-life-events.md) — pet died, parent dying, divorce, miscarriage, baby pictures, the worst weeks.
- [11-objects-with-personality.md](11-objects-with-personality.md) — the chair that squeaks, the printer that hates one person, the haunted vending machine, the elevator that opens early.
- [12-foodscape.md](12-foodscape.md) — food theft, mandatory potluck dishes, birthday cake hierarchy, the diet bet, the office drinker.
- [13-mental-health-and-panic.md](13-mental-health-and-panic.md) — panic attack, dissociation, OCD ritual, ADHD task-thrash, agoraphobia at the all-hands.

---

## Author's note on filtering

These are *ideas*, not commitments. Treat the list as a buffet. A useful next step is probably:

1. Pick 6–8 that excite you most.
2. For each, confirm the cast / world / aesthetic / dialog bibles support it.
3. Sketch which existing components it reuses vs new ones it needs.
4. Sequence by which earn the most "office is alive" feeling per implementation hour (the aesthetic bible's ranking principle).

Some ideas in here will feel obvious in retrospect. Some will feel wrong for this game. A few will feel exactly right. The deep cuts — the ones that produced the strongest "yes" when read — are usually the ones to ship.
