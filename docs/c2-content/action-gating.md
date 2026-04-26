# Action-Gating Bible — Working Draft

> Co-authored by Talon and Opus. Captures the principle that drives are necessary but not sufficient for behavior. Read alongside the cast bible (drives), the new-systems-ideas doc (stress, social mask, workload), and SRD §8.5 (social state is a first-class component family).

---

## The thesis

Drives don't cause action. A drive is a *capacity to feel*. What turns feeling into doing is a separate set of systems — willpower, inhibitions, and the action-selection logic that arbitrates between them. Without that gate, any NPC with a high drive does the corresponding thing immediately, every time. The world becomes a mechanical reaction surface and the alive feeling collapses.

Real humans are different. The hunger meter says 120% but Sally is not eating because she thinks she's fat. Donna's attraction drive is at 95 but she will *never* sleep with Bob because of something in her past — even she might not know what. Bob avoids Donna because he likes her. People stay in jobs they hate. People skip meals when they're sad. People fail to ask out the person they obviously love. A simulation that lets drives run straight through to action is a simulation that has skipped the most interesting part of being a person.

This document records the gate.

---

## Willpower — the reservoir between feeling and doing

Every NPC carries a single self-state value, **willpower**, on the entity. Two numbers: `current` (today) and `baseline` (typical). Both 0–100.

Willpower is not a drive. It does not push toward an action. It is the *resistance* against drive-driven action when an NPC is trying to suppress, perform, or hold themselves to a different standard than what their drives are asking for.

- Willpower **depletes** with sustained suppression. Holding back tears in a meeting costs willpower. Smiling at a colleague you hate costs willpower. Not eating the cake on the breakroom counter costs willpower. Saying nothing while a coworker takes credit costs willpower.
- Willpower **regenerates** with rest, with privacy, with relief from the stress source. Going home regenerates. A weekend regenerates. Crying in the bathroom regenerates faster than crying at your desk.
- Willpower is **stat-like, not absolute**. A high-willpower NPC (baseline 80) holds out longer than a low-willpower NPC (baseline 30). Both deplete; both eventually break.
- Willpower is **day-specific**. Some Wednesdays Donna feels strong. Some Wednesdays she's barely holding on. The current value drifts and shocks; the baseline pulls it back over time. This is what produces "some days you're chatty, some days you're not."

When willpower is low and a drive is high, the gate breaks open. The action that has been suppressed for days finally happens. The argument finally erupts. The cake finally gets eaten. The crying finally happens — and now it's at a desk in front of everyone.

This is where stories come from. Stress (per the new-systems-ideas doc) is the slow accumulator that depletes willpower over days. The interaction is:

> Stress accumulates → willpower depletes → drives keep accumulating → at some point the gate fails → an action that was held back for a long time breaks through.

The breakthrough moment is what the player sees. The build-up is what makes them root for it or dread it.

---

## Inhibitions — the doors that don't open at all

Willpower is the dam. Inhibitions are the rooms the water never gets into in the first place.

An **inhibition** is a hard blocker on a class of action, attached to an NPC, that takes that action class off the table regardless of how much drive or how little willpower the NPC has. Donna will never sleep with Bob. Frank will never confront his boss directly. Sally will not eat in front of strangers. These aren't willpower struggles. They're things that simply do not happen for that person, full stop.

Each inhibition carries three fields:

- **Class** — what it blocks. An enum drawn from observable office archetypes: `infidelity`, `confrontation`, `bodyImageEating`, `publicEmotion`, `physicalIntimacy`, `interpersonalConflict`, `riskTaking`, `vulnerability`. Open-ended; new classes are easy to add as the cast bible grows.
- **Strength** — 0–100. 100 means *never*. 50 means meaningful drag on the action-search. 20 means a slight resistance. The strength is what action-selection consults; the engine doesn't have to commit to a binary block.
- **Awareness** — `known` or `hidden`. This is the most important field. A `known` inhibition is something the NPC could explain if asked — "I learned not to mix work and romance after the last time." A `hidden` inhibition is something even the NPC doesn't know about — "I just don't, and I couldn't tell you why." The system stores it; the NPC's surface presentation never references it; the player gets to invent the reason.

The hiddenness is the feature, not a limitation. If the engine had to know *why* Donna won't jump Bob's bones, it would have to invent a backstory and commit to it. If the engine just knows *that* she won't, with strength 100 and awareness hidden, the player invents the reason. They imagine a husband who left, a friend who got hurt, a moment in college Donna doesn't talk about. The story is theirs. The simulation refuses to flatten it.

Inhibitions are authored at NPC spawn time from the archetype + the deal + a small randomized roll. They can be added during play — a public failure can install a `vulnerability` inhibition that wasn't there before. They can also weaken over time, slowly, if the NPC has therapeutic experiences (a confidant, a successful similar action with someone safe). Most of the time they're stable.

---

## Approach-avoidance inversion

Drives are not monotonic toward action. A high drive can produce *avoidance* of the drive's apparent target when the stakes are high enough.

Bob's attraction drive is at 80 toward Donna. Action-selection considering "talk to Donna" reads the drive — high — and the inhibition list — `vulnerability: 60, hidden`. The vulnerability inhibition trips at this stake level. Bob's action-selection chooses a *different* path: avoid Donna, take the long way around the cubicle row, suddenly find the supply closet very interesting. The high drive produced avoidance, not approach.

This is the engineering shape:

> Action-selection reads (drive, willpower, inhibitions, situation-stake). For each candidate action, the inhibitions and willpower together produce a *cost*. When the cost exceeds the drive's push, the action loses to its avoidance counterpart. When the stake is *very* high and the inhibition matches, the *opposite* action wins — not just none.

The avoidance branch is what makes high-drive states emotionally interesting. It's why characters in romantic comedies behave so badly. It's why people who clearly love each other spend half a season not speaking. The drive isn't broken; the inhibition is doing its job.

---

## Physiology overridable by inhibition

The simulation already models hunger, thirst, fatigue, bladder, colon. These are biological drives with strong, simple cases for action: when hunger is high, eat. When tired, sleep. The engine treats them roughly the way an instinct would.

**Some inhibitions override physiology.** Sally's hunger meter at 120% with a `bodyImageEating` inhibition at strength 90 produces no eating action. The body says yes; the social/self-image system says no; the social system wins. Hunger keeps climbing. Sally gets weaker. The character plays out through the day visibly diminished.

This is not a bug. It is the social-overrides-biology mechanic that makes the office adult. Eating disorders exist in real offices. Holding it in until the meeting is over exists in real offices. The all-nighter — sleep drive maxed, NPC working through it because their `vulnerability` inhibition won't let them be the person who couldn't make the deadline — exists in real offices. The new-systems-ideas doc's StressSystem and WorkloadSystem are the work-pressure half of this; inhibitions are the social/self-image half. Together they produce the *betrayal of the body by the will*.

Action-selection consults inhibitions for biological actions, not just social ones. The strength field decides whether the override holds — Sally with `bodyImageEating: 30` skips lunch but eats dinner; Sally with `bodyImageEating: 90` doesn't eat in front of anyone all day. Either is human.

---

## How the systems compose

The action-selection sketch, in plain language, for any candidate action an NPC might take next tick:

1. **Drive push.** Read the drive(s) the action would discharge. High drive = strong push.
2. **Willpower draw.** Subtract from the push the willpower cost of *not* doing the action — i.e., suppression cost. (Holding back hunger costs willpower because hunger keeps pushing.)
3. **Inhibition cost.** For every inhibition whose class matches the action class, multiply the action's selection probability by `(1 - strength/100)`. Strength 100 zeroes the action.
4. **Approach-avoidance flip.** If a matched inhibition's strength is above an "avoidance threshold" *and* the situation-stake (proximity to target, observability, irreversibility) is high, the action's *opposite* gains the action's would-be selection weight. The drive flips into avoidance.
5. **Selection.** Highest-weight action wins. Ties broken by personality (conscientiousness biases toward routine actions; openness biases toward novel ones).

This is the design intent. The exact equation is for the v0.2.1+ social-engine packet to commit to and tune. Inhibitions, willpower, and drives all live on the wire (v0.2.1 schema reserves the surface) so the engine implementation is the only thing left to vary.

---

## What this changes about authoring NPCs

The cast bible's archetypes already specify chronically-elevated drives. They now also specify:

- A **willpower baseline** range. The Cynic has high baseline (gives no fucks, harder to break); the Newbie has low baseline (easy to overwhelm); the Recovering has volatile current with mid baseline (good days, bad days).
- A small set of **starter inhibitions**. The Affair archetype probably ships with `infidelity` at low strength (their inhibition is failing, by definition) and `vulnerability` at high strength (they cannot afford to be seen). The Hermit ships with `vulnerability` and `physicalIntimacy` both high. The Climber ships with `publicEmotion` high and `infidelity` low.

These additions are content-authoring concerns for the cast-generator packet, not schema concerns. The schema only commits the surface; the archetype catalog populates it.

---

## What this is deliberately not

- **Not a moral framework.** Inhibitions aren't "good restraints." A high `confrontation` inhibition might keep someone from filing a deserved harassment complaint. A low one might produce constant escalation. The system is amoral; the player attaches the meaning.
- **Not a therapy model.** Inhibitions can weaken with therapeutic experiences but the engine doesn't simulate therapy. Weakening is a slow drift in response to repeated similar-class actions that succeeded without disaster.
- **Not deterministic.** The same drive + willpower + inhibition state produces a *probability distribution* over actions, not a single action. The same NPC in the same state on two different days picks differently. This is what produces emergent surprise.
- **Not visible.** Hidden inhibitions never surface in dialogue, never get explained to the player, never appear in any NPC's spoken thought. They shape what does and doesn't happen and that's all.

---

## Cross-references

- **Cast bible — drive catalog.** The eight drives (`belonging`, `status`, `affection`, `irritation`, `attraction`, `trust`, `suspicion`, `loneliness`) are the input pushes. All eight live on the entity (per `entities[].social.drives`); none are pair-targeted. Differences across NPCs are differences in drive *values*, not in which drives they have.
- **Cast bible — archetype templates.** Each archetype now specifies willpower baseline range and starter inhibition set in addition to drive elevations and personality range.
- **New-systems-ideas — StressSystem.** Stress is the slow accumulator that depletes willpower over days/weeks. Stress and willpower together make the timeline of breakdown legible.
- **New-systems-ideas — SocialMaskSystem.** The mask is the performance the NPC produces while willpower is being spent suppressing the underlying state. Mask cracks happen when willpower is depleted faster than the situation allows for repair.
- **SRD §8.5.** Social state is a first-class component family. Willpower and inhibitions are part of that family on the engine side.

---

## Open questions for revision

- **Per-class willpower vs single global.** Should willpower be one number or split into domains (sexual, dietary, anger, vulnerability)? One number is simpler; per-domain is more expressive. Current commitment: one number, with inhibition `class` carrying the domain specificity. Revisit if behaviors feel undifferentiated in practice.
- **Inhibition install during play.** When does an event create a *new* inhibition vs strengthen an existing one? Threshold rules need definition. Probably a Phase-1.4-or-later concern.
- **Awareness transitions.** Can a hidden inhibition become known? (Therapy moment, breakthrough during a crisis.) Probably yes, rare. Probably never the other direction.
- **Conflict between inhibitions.** Sally has `bodyImageEating: 80` and `publicEmotion: 80`. She's in a meeting, hungry, uncomfortable, near tears. Both inhibitions are at work simultaneously. What does action-selection do? Probably: each inhibition costs separately; the worst-case action class becomes the rarest; the NPC sits frozen. Worth instrumenting so we can see it happening.
- **Player visibility.** Should the player ever see willpower and inhibition values in a debug overlay, or always infer? Probably debug-only at first; player-facing later if it adds rather than removes mystery.
