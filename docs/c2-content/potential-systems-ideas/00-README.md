# Potential Systems Implementation Ideas

> A working idea-pool. Nothing here is committed. Each cluster is written so individual entries can be lifted into a future work-packet without the cluster around them needing to be approved as a whole.

---

## Why this folder exists

The existing `new-systems-ideas.md` covers eighteen big systems — stress, mask, coffee, passive aggression, rumor, smell, captivity in meetings, and so on. That's a lot, but reading it back against the world, cast, dialog, action-gating, and aesthetic bibles makes a different kind of gap visible: most of those systems handle the *legible* friction of office life. The gap is in the *illegible* friction — the spectrum of things people deal with at work that are weird, taboo, embarrassing, idiosyncratic, or culturally specific, and that almost never get talked about in a normal office and have never been simulated in a Sims-descended game.

This folder is the brainstorm into that space.

The brief, paraphrased: *what if someone had to step out for a smoke every fifteen minutes? what if someone screamed when a spider crawled on them? what if a fastidious person sat next to a slob? farting is taboo — what other taboos are there?*

Every idea in this folder tries to honor that brief by going one step past the obvious system. The smoking break is not just "step outside on a timer"; it's the *bench social graph*, the *withdrawal jitters that bleed into a meeting*, the *day they try to quit and snap at the first person who asks them a question*. The spider is not just "scream"; it's *who they tell, who comes to deal with it, the difference between the person who screams and the person who calmly carries it outside, the way the floor remembers there was a spider here three weeks ago.*

---

## What this folder is not

- Not a roadmap. The systems here are pre-prioritization. Triage happens after review.
- Not duplication of the existing `new-systems-ideas.md`. Where the obvious extension overlaps something that already exists (e.g., smell, stress, passive aggression), the new entry sharpens or *crosses* the existing system rather than restating it. Cross-references point at the relevant existing system instead of redescribing it.
- Not a rewrite of the bibles. Everything here builds on the world / cast / dialog / action-gating / aesthetic bibles. Where a proposal needs a bible amendment to land, that amendment is called out as an open question, not assumed.
- Not pitched at any specific phase. Some ideas are 2.x-sized; some are 3.x or later; some are content-only. Each entry tries to flag its rough size class.

---

## How each cluster is structured

Each numbered cluster (`01-…` through `16-…`) is a themed collection of related system proposals. Inside each cluster, individual systems follow a consistent shape:

1. **The human truth** — the real-life thing the system models. One paragraph, no jargon.
2. **What it does** — the simulation mechanic, in the language of the existing engine (drives, willpower, inhibitions, components, proximity events, mask, calcify, stress).
3. **Components / data** — the rough ECS shape, named in the project's existing convention.
4. **Personality and archetype gates** — which Big-Five values, drive baselines, deals, and archetype tags modulate the behavior.
5. **What the player sees** — the observable consequence. (Per the cast bible's "emote more than speak" axiom, observable matters more than spoken.)
6. **The extreme end** — the high-stakes version of the system in motion, the kind of moment that's the reason the system exists.
7. **Open questions** — anything I noticed but didn't try to resolve.

Some entries omit a section when there's nothing meaningful to say there.

The two final docs are different:

- `17-interaction-matrix.md` is the cross-cluster view — which proposed systems multiply each other, which ones multiply existing systems, where the compound effects produce stories the individual systems wouldn't.
- `18-extreme-end-vignettes.md` is a small set of emergent-story sketches that bake several proposed systems together, the way `new-systems-ideas.md` ends with its "synthesis" scenario.

---

## Index

- [01 — Vices, cravings, and tics](01-vices-cravings-and-tics.md). Smoke break dependence, caffeine variants, knuckle cracking, leg jiggle, hair pulling, the OCD-adjacent NPC who can't leave a desk asymmetric.
- [02 — Bodily-function taboos](02-body-taboos-and-bodily-functions.md). Farting, burping, sneezing, body odor, period mechanics, bathroom etiquette, the visible body change nobody asks about.
- [03 — Pests, phobias, and acute reactions](03-pests-phobias-and-acute-reactions.md). The spider, the cockroach, the mouse, the wasp at the window. Fainting, panic attacks, public-speaking dread, claustrophobia.
- [04 — Cleanliness, mess, and territory](04-cleanliness-mess-and-territory.md). Fastidious-vs-slob desk adjacency, plant care drama, mug ownership, the borrowed-without-asking, the visual-disgust accumulator.
- [05 — Proxemics and personal space](05-proxemics-and-personal-space.md). The close-talker, the over-shoulder reader, the hugger, the hallway second-pass, the elevator, the unwanted touch.
- [06 — Culture, religion, and identity friction](06-culture-religion-identity-friction.md). Observance schedules, dietary, generational gap, accent strengthening under stress, the dress-code microculture.
- [07 — Sensory environment and misophonia](07-sensory-environment-and-misophonia.md). Chewing-and-clicking sound sensitivity, perfume assault, the screen-brightness war, the throat clearer, the leaking headphones.
- [08 — Async and tech friction](08-async-and-tech-friction.md). Email reply-all, sent-from-wrong-account, ringtone incidents, the slow PC, the printer, the dial-tone hold music.
- [09 — Life-event bleed-through](09-life-event-bleed-through.md). Domestic crisis residue, sick parent, in-progress divorce, on-premises medical event, death-in-family.
- [10 — Rituals, calendar, ceremony](10-rituals-calendar-ceremony.md). Birthdays, going-aways, anniversaries (Mark, Cubicle 12), the holiday party, the secret santa, the retirement.
- [11 — Power, favors, and micro-tyranny](11-power-favors-micro-tyranny.md). Gatekeepers, the meeting canceller, the keyholder, the sympathy hire, the founder's-nephew gravity well.
- [12 — Substance spectrum beyond coffee](12-substance-spectrum-beyond-coffee.md). Drinking on the job, prescription pills, the flask in the drawer, recovery exposure.
- [13 — Conversation edge cases](13-conversation-edge-cases.md). Awkward silence, slow goodbye, greeting calibration, hijacking, topic minefields, interruption etiquette, the unspoken acknowledgment.
- [14 — Romance and attraction spectrum](14-romance-attraction-spectrum.md). Unspoken tension, slow-burn arc, unrequited spectrum, post-divorce rebrand, accidental intimacy, age-gap, rejection aftermath, archetype mix.
- [15 — Fashion and visible body change](15-fashion-and-visible-body-change.md). Ring axis, weight change, haircut event, makeup state, eyewear, visible health, seasonal, silhouette recalibration.
- [16 — Emergent culture and inside jokes](16-emergent-culture-and-inside-jokes.md). Floor-culture calcification, callbacks, private inside jokes, floor memes, ritual genesis, scapegoating, folklore, cultural drift from turnover.
- [17 — Interaction matrix](17-interaction-matrix.md). Cross-cluster substrate analysis (mask, knowledge, inhibitions, proximity bus); high-yield combinations; implementation ordering.
- [18 — Extreme-end vignettes](18-extreme-end-vignettes.md). Five emergent-story sketches that bake several systems together: the 78-minute meeting, the Friday quit-attempt, the Newbie's first period, the conference-room cockroach, the long goodbye.

---

## Tone discipline

Some of the topics here — periods, panic attacks, divorce, recovery, substance use, religious observance — are real things that real adults navigate at real offices. The world bible already commits this game to depicting them honestly without sensationalizing them. Every proposal in this folder is written in that register: the goal is *recognition*, not shock value. The Sims-side feeling — "oh god, I have done that, I have been there" — is the bar. If a proposal is shock for shock's sake, it doesn't belong here and should be cut on review.

That said: the brief asked for the absurd end of the human spectrum. Things that are unusual, taboo, never simulated. Some of these proposals will read as too much on first pass. They're documented here so the *too much* can be discussed and trimmed deliberately rather than left out by self-censorship.

---

## What I cross-checked while writing

- All eighteen systems in `new-systems-ideas.md` (stress, workload, mask, coffee, passive aggression, rumor, appearance, smell, meeting/captivity, temporal anxiety, noise, flow, lunch, illness, thermal, eavesdrop, favor, threshold spaces).
- The cast bible's eight drives (`belonging`, `status`, `affection`, `irritation`, `attraction`, `trust`, `suspicion`, `loneliness`) plus the proposed `exposure`.
- Big Five (`openness`, `conscientiousness`, `extraversion`, `agreeableness`, `neuroticism`).
- The action-gating gate — `willpower` (current/baseline) + `inhibitions` (class, strength, awareness).
- The cast bible's archetype catalog (Vent, Hermit, Climber, Cynic, Newbie, Old Hand, Affair, Recovering, Founder's Nephew, Crush) and deal catalog.
- The dialog bible's corpus + decision tree + calcify.
- The world bible's named anchors (Microwave, Fridge, Window, Supply Closet, IT Closet, Parking Lot, Smoking Bench, Women's Bathroom, Conference Room, Cubicle 12).
- The aesthetic bible's lighting / proximity / movement layer, including the proposed crowding and refuge-space additions.

Where a proposal lands cleanly inside an existing system, I lift the existing component name (`StressComponent`, `SocialMaskComponent`, `KnowledgeComponent`, `OdorComponent`, `FavorLedgerComponent`, etc.) instead of inventing a parallel one.
