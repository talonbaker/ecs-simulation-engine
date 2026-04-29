# 14 — Romance and Attraction Spectrum

> The cast bible already has the Affair archetype, the Crush archetype, and an `attraction` drive. This cluster covers the broader spectrum: unspoken tensions, slow-burns, age-gap weirdness, post-divorce energy, the rebound, the reciprocated-but-mistimed, the *I noticed and then stopped noticing.* The world bible commits to honest depiction of office attraction — including the parts that are inappropriate, awkward, or unrequited. The simulation should produce these patterns in the same restrained register as everything else.

---

## What's already in the engine that this builds on

- The cast bible's `attraction` drive (per-target).
- The Affair archetype and the relationship-pattern library (`old flame`, `secret crush`, `active affair`, `slept with their spouse`).
- `inhibitions` — `infidelity`, `vulnerability`, `physicalIntimacy`.
- `SocialMaskComponent`, `willpower`.
- The proposed touch system (cluster 5.2).

---

## 14.1 — UnspokenTensionSystem

### The human truth

Two NPCs whose `attraction` drives are mutually elevated but neither has acted, neither has spoken, and most of the time neither is fully aware of the reciprocity. They linger in conversations longer than necessary. They find reasons to be in the same rooms. They make eye contact and look away. The tension is observable to coworkers within a few weeks. It may resolve, may dissipate, may calcify into a permanent low-grade hum.

### What it does

A `TensionLink` exists between two NPCs when both have elevated `attraction` toward each other AND `inhibitions` are active enough to prevent action. The link tracks:

- `mutuality` — high (both elevated) / asymmetric (one-sided) / declining.
- `awareness` — neither knows / one knows / both know but neither has named.
- `observabilityScore` — how visible the tension is to coworkers.

Behavioral effects:

- Conversations with the tension partner last 1.5–3x normal length without intentional cause.
- Hallway pathing introduces small detours past the partner's desk.
- Eye-contact-and-look-away events fire more frequently in line of sight.
- Touch events (cluster 5.2) between them carry double-weight `affection` updates.

### Cross-system interactions

- **KnowledgeComponent (other NPCs):** observability above threshold begins to register in coworkers' KnowledgeComponent — *I think those two have a thing.* The Vent notices first. The Hermit notices but says nothing.
- **AffairArchetype:** the Affair archetype is one of the *outputs* of high-mutuality unspoken tension that escalates.
- **HolidayParty (cluster 10.3):** maximum-risk venue for tension-resolution events.
- **Inhibitions:** if either NPC's `infidelity` inhibition is high, the tension persists indefinitely as a hum without action.

### Personality + archetype gates

- The Crush archetype is the unspoken-tension archetype taken to extremes.
- The Climber turns tension into strategy fast — they do not leave it unspoken.
- The Hermit's tensions are deepest and most-suppressed.
- The Recovering archetype recognizes tension as a vulnerability and steers away.

### What the player sees

Two NPCs whose paths cross more than chance would explain. A coworker noticing. The slow build over months. Eventually either resolution (acted on, ended badly, or successfully) or fade (one of them moves on, gets transferred, has a major life event).

### The extreme end

Two NPCs share an unspoken tension link for fourteen sim-months. Both are married. Both have high `infidelity` inhibition. Tension is high-mutuality, both-aware, observabilityScore high. The whole floor knows. Nothing happens. The chronicle records: *the longest-running tension on the floor.* Eventually one of them transfers. The other watches them leave. The chronicle records nothing visible; the relationship matrix records a permanent calcified-tension entry that affects how the remaining NPC processes future romantic candidates for the rest of the save.

---

## 14.2 — SlowBurnArc

### The human truth

A romance that builds across months, against initial obstacles. The work-friendship that becomes more. The two coworkers who started as adversaries. The one who's been patient. The one who finally noticed. Slow burn arcs are a specific shape — they need real time to be believable.

### What it does

`SlowBurnArc` is a multi-phase pattern that some NPC pairs enter:

- **Phase 0 — neutral coexistence.** Standard relationship.
- **Phase 1 — recognition.** A specific event triggers heightened awareness. Could be a help moment, a witnessed vulnerability, a side-by-side challenge.
- **Phase 2 — accumulation.** Repeated small positive interactions build `affection`, `trust`, `attraction` simultaneously. Coworkers start to notice over many weeks.
- **Phase 3 — uncertainty.** Both NPCs are aware of feelings but unsure of reciprocity or appropriateness. High mask draw on both. Slow goodbyes (cluster 13.2) emerge.
- **Phase 4 — resolution event.** A high-stakes moment forces a decision. Could be an external pressure (transfer, layoff, life event) or an internal threshold (one NPC finally says something).
- **Phase 5 — aftermath.** Either relationship transitions or they recede; either way the relationship is permanently changed.

### Cross-system interactions

- **KnowledgeComponent:** the slow accumulation generates noteworthy events at slow intervals — each one calcifies as part of *their* story.
- **Holiday party / threshold spaces:** high-leverage Phase 4 venues.
- **Transfer / leaving events (cluster 10.2):** common Phase 4 catalysts.

### What the player sees

A relationship that grows visibly across many sim-months. The player can watch the arc as it unfolds without it being scripted.

### The extreme end

The Newbie and a long-tenured coworker enter a slow-burn arc starting in month 3. By month 9 the floor is informally rooting for them. By month 12 a transfer offer arrives for one of them. The Phase-4 decision moment lands during a quiet evening at a threshold space. The chronicle records the moment regardless of outcome.

---

## 14.3 — UnrequitedSpectrum

### The human truth

Some attraction is one-sided. The Crush archetype canonicalizes the case where one NPC has *not yet* acted on attraction toward another who's unaware. The unrequited spectrum extends this: situations where the attraction is unreciprocated and known to be (the *I know they don't feel the same way and I'm here anyway*); the awkward-after-rejection state; the fading of unrequited attraction into something else.

### What it does

`UnrequitedAttraction` is a directed link with these states:

- `secret` — the canonical Crush. Target unaware.
- `disclosed_unrequited` — the source confessed; target declined; both are now navigating the aftermath.
- `fading` — the source's attraction is decaying; relationship is reverting to neutral.
- `recalcified` — the source's attraction has stabilized into a long-term low-grade ache that will not act but won't fade.

Each state has different daily-cost shapes:

- `secret` — small daily mask draw, increasing over time, in proportion to proximity.
- `disclosed_unrequited` — heavy mask cost for several sim-weeks; eventually settles either to fading or to recalcified.
- `recalcified` — small but permanent baseline drag on the source's mood. They've made peace with it; the peace costs.

### Personality + archetype gates

- The Crush archetype is born in `secret` state.
- The Newbie's first crush is the most-dramatic shape — pure intensity.
- The Old Hand's recalcified attractions from years ago are part of who they are; they don't talk about them but they shape behavior.
- The Recovering archetype's unrequited attractions interact with their recovery (a healthy unrequited-and-fading is recovery progress; a recalcified-and-suffering can be relapse fuel).

### Cross-system interactions

- **Dialog calcify:** the language an NPC uses around their unrequited target calcifies in specific ways visible to coworkers paying attention.
- **WorkloadSystem:** chronic recalcified attraction has small ongoing productivity drag in scenarios where the target is in proximity.

### What the player sees

The patient, slow shape of one-sided affection. Sometimes the source acts; sometimes never; sometimes the target finally notices and the dynamic shifts.

### The extreme end

The Hermit (Greg) has had a recalcified attraction to Donna for seven years (pre-save). It surfaces as: he watches her, unobtrusively. He brings her IT solutions faster than he brings them to anyone else. He rarely speaks to her. Donna has *no idea*. The save's events include Donna having a hard week (cluster 09.7); Greg's calibrated response — extra IT availability, a coffee from the IT closet without explanation — registers in Donna's KnowledgeComponent as *Greg is a sweet weird guy who's been kind to me.* The recalcified attraction never resolves; the kindness is its only output.

---

## 14.4 — RebrandingAndRebound (the post-divorce / post-breakup pattern)

### The human truth

An NPC newly-single comes back to work different. The hair changed. They went to the gym. They're more available. They're less available. They have new energy directed somewhere unspecified. The reverberations through the office's relationship matrix are real — some coworkers move closer; some move away.

### What it does

When an NPC's `DivorceArc` (cluster 9.3) completes or a long relationship ends, a `RebrandingArc` may fire:

- `phase` — `numb | activating | exploring | overdoing | settling | settled`.
- Behavioral effects: appearance changes (cluster 15), social availability changes, signaling intent shifts (cluster 6.5 dress code).

Other NPCs read the rebrand:

- The Vent reads it as *they're finally getting out there!* and tries to set them up.
- The Climber reads it as opportunity (status, alliance, possibly more).
- The Affair archetype reads it as a possible new candidate.
- The recently-separated NPC's own attraction drives are erratic — high baseline, low specificity, often misread by themselves.

### Cross-system interactions

- **Affair archetype:** newly-single NPCs are both targets and potential perpetrators of new arcs.
- **TouchSystem (5.2):** rebranded NPCs' touch baselines can shift; calibration with prior coworkers gets disrupted.
- **AppearanceComponent (existing):** visible rebrand is observable.

### What the player sees

The Tuesday they came back from a long weekend looking different. The new dynamic that emerges over the next sim-month.

### The extreme end

A long-tenured married NPC's divorce finalizes. They take a week off. They come back with a haircut and ten pounds lost. The Vent reads it as glow-up. The Climber reads it as opportunity. Within two months, two new attraction links involve them; one becomes a slow-burn (14.2); the other becomes an aborted-affair (their new partner has cold feet). The chronicle records the season as their most romantically-charged time in the save.

---

## 14.5 — AccidentalIntimacySystem

### The human truth

The brush in the hallway. The hand on the back of a chair when leaning over to look at a screen. The shared umbrella from the parking lot. The accidental hand-touch reaching for the same pen. These tiny moments — usually nothing — sometimes spark something. The reading of *was that intentional* is its own micro-event, and the answer is often unknown even to the person who did it.

### What it does

`AccidentalContactEvent` fires from existing proximity events when:

- A specific physical-collision event happens (hand-on-pen, brush-in-hallway, shared-doorway).
- One or both NPCs have elevated `attraction` toward the other.

The recipient's interpretation:

- Low attraction baseline → no interpretation. Just an accident.
- High attraction → high probability of *meaning-making.* The recipient runs the moment in their head; their attraction drive bumps small amount.
- Reciprocity (both attracted) → simultaneous meaning-making; both NPCs calibrate cautiously after.

### What the player sees

A small repeated phenomenon between two NPCs who keep almost-touching. Minor on its own; significant in aggregate.

### The extreme end

The two NPCs in a slow-burn arc have an accidental-hand-touch reaching for the same coffee mug. Both freeze for half a second. Both pull back. Both pretend not to notice. Both noticed everything. The chronicle records nothing public; their internal arc state advances by a phase.

---

## 14.6 — AgeGapAndPositionalAttraction

### The human truth

Office attraction often crosses age and rank lines, and the asymmetries matter. The intern and the senior. The married parent and the recently-graduated. The boss and the report. Each of these has different mask costs, different stakes, different social readings. Some are taboo in the early-2000s setting; some are openly happening; some are happening secretly.

### What it does

The existing attraction drive is augmented with `crossAxis` flags indicating the kind of asymmetry:

- `ageGap` — large or small.
- `rankAsymmetry` — superior/subordinate.
- `marriedStatus` — one or both married.
- `tenureGap` — long-tenured vs. new.

Social cost of acting scales with the count and severity of `crossAxis` flags. The simulation does *not* prohibit any combination; it costs differently.

### Cross-system interactions

- **Inhibitions:** specific inhibitions may interact with cross-axis attractions (the `vulnerability` inhibition for the subordinate; the `position-protection` flag for the superior).
- **KnowledgeComponent:** acted-on cross-axis attractions are very-high-confidence office facts that propagate fast.
- **HR / power systems (cluster 11):** the inappropriate attraction may produce reports; may not.

### What the player sees

Different shapes of attraction-arcs depending on which axes they cross. The simulation refuses to flatten them into a single pattern.

### The extreme end

The 22-year-old intern and the 50-something Old Hand have a slow-burn arc that the floor reads with a mixture of delight and discomfort. The Old Hand recognizes the inappropriateness; the intern is genuinely smitten. The arc resolves with the Old Hand quietly stepping back. The intern recovers over months. The chronicle records both perspectives.

---

## 14.7 — RejectionAftermathSystem

### The human truth

When an NPC asks another out and is told no, the workplace continues. The two NPCs who must continue working together carry the moment forward. The first week is awful. The second week is awkward. By the third or fourth week things settle, or they don't. Sometimes one of them transfers. Sometimes they become close friends. Sometimes the rejected NPC carries a low-grade resentment that affects their work for months.

### What it does

After a `disclosed_unrequited` event, both NPCs enter a `RejectionAftermath` substate:

- **Week 1 — acute.** Heavy mask draw on both. Avoidance behaviors active. Conversations clipped.
- **Week 2 — managed.** Some normalization. Conversations possible but stiff.
- **Week 3–4 — settling.** Either back to working-functional, or one party decides this isn't sustainable.
- **Long tail.** Permanent small calibration shift.

### What the player sees

A tension that resolves slowly. The long-tail shift in how the two NPCs interact for the rest of the save.

### The extreme end

The Newbie asked the Climber out. The Climber declined. Six months later they're still calibrating around each other. The Newbie's `vulnerability` inhibition strengthens; their next attraction event will be even harder to act on. The chronicle records the original event and its aftermath.

---

## 14.8 — RomanceArchetypeMix (when archetypes collide)

### The human truth

Different cast-bible archetypes have different relationships to romance. The Affair archetype entering a relationship with a Recovering archetype is a different shape than a Climber-Newbie connection. The simulation should produce these collisions with their characteristic textures.

### What it does

A small matrix of archetype pairs with characteristic arc shapes:

- **Affair × Recovering:** unstable. The Affair's secrecy threatens the Recovering's stability. Often ends badly.
- **Climber × Climber:** strategic. Both calculating; sometimes works as alliance, often dissolves when interests diverge.
- **Hermit × Vent:** unlikely. When it happens, the Vent becomes the Hermit's only social link; the Hermit becomes the Vent's quiet center.
- **Newbie × Old Hand:** mentor-mentee with romantic crossover; high cross-axis cost; high warmth potential.
- **Cynic × Recovering:** the Cynic's lack of judgment and dry presence is calming for the Recovering; works surprisingly often.
- **Crush × anyone:** the Crush's fixation may or may not produce reciprocation; the unrequited spectrum applies.

### What the player sees

Romances that *feel* shaped by who's involved. Not every Climber-Climber relationship goes the same way, but the shape rhymes across saves.

### The extreme end

The Affair and the Recovering form a relationship while both are actively in their archetype arcs. The Affair's secrecy violates the Recovering's stability. Six months in, a high-stakes confrontation. One of them recovers; one of them backslides. The chronicle records this season as one of the save's most-dramatic.
