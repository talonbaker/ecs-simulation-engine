# 03 — Addictions and Dependency Loops

> The user called this out specifically: the smoker who needs a cigarette every 15 minutes and gets jittery / agitated, the coffee drinker on a 30-minute pee chain. These are *clock-driven biology that interrupts office life on a different schedule than work expects.* This cluster covers the substances and behavioral dependencies offices actually run on.

---

## 1. NicotineSystem + NicotineDependencyComponent

### The human truth
The smoker's day is structured around when they next get to step outside. The interval is short — 30 to 90 minutes between cigarettes for a heavy smoker — and missing one produces measurable withdrawal: jitter, irritation, focus collapse, headache. Smokers know this; non-smokers underestimate it; the office's accommodation of it (the bench, the side door, the exhaled breath that lingers) is one of its most consistent rituals.

### What it does
A continuous `NicotineLevel` that decays predictably and is replenished by a `Smoke` action at the smoking bench. Below a personality-tuned threshold, withdrawal symptoms tag in: irritation gain rate up, focus penalty (`flow` breaks immediately), shaking-hand visual marker, voice register shifts (more clipped / more crass).

### Components / data
- New: `NicotineDependencyComponent { Level: float (0..100), DecayPerTick: float, LastDoseTick: long, DependencyStrength: int (0..100), JitterThreshold: float, WithdrawalActive: bool }`
- New: `SmokingDealComponent { CigsPerDay: int, BrandLoyalty: string, IsTryingToQuit: bool, QuitAttemptStartTick: long }`
- New: `CigaretteEntity` (consumable, count tracked)
- Reuse: existing `MoodComponent` (irritation), `WillpowerComponent`, `SocialMaskComponent`

### Mechanics — the 15-minute jitter timer
- Heavy-smoker default decay: full-craving in ~45 minutes; heavy jitter in ~75 minutes; full mask-collapse-grade withdrawal in ~120 minutes.
- The smoke break is a 4–8 minute action at the smoking bench (named anchor in world bible). Restores Nicotine to high. Briefly improves mood, decreases stress, restores willpower fractionally.
- During captive contexts (meetings, the existing `CaptiveComponent`), the NPC cannot leave. Drives accumulate. At a certain point the inhibition against breaking decorum loses to the addiction. The smoker excuses themselves; the social cost is logged.
- The trying-to-quit flag is its own arc. The NPC suppresses the smoke action with willpower; willpower drains continuously; the streak counter ticks. A relapse moment is a high-noteworthiness narrative event.

### Social mechanics — the smoking-bench social graph
- The bench regulars know each other's schedule. The new smoker is welcomed quickly; the rare-smoker is mildly suspect.
- Cross-hierarchy bonding: an exec on a smoke break and a shipping-floor worker are equals on the bench.
- The lighter loan is a small but real `FavorEvent`.
- Non-smokers who pass the bench register the smell on returning smokers. High-agreeableness NPCs hide their reaction; low-agreeableness comment.

### Extreme end
The Climber is in their performance review meeting. It runs forty minutes long. They quit smoking three weeks ago. Their NicotineLevel is at zero — full withdrawal active. Their hands are shaking. The exec interprets the shake as nervousness about the review. The review goes worse than it should have. After the meeting, the Climber stands at the parking lot edge for eleven sim-minutes. They do not light up. They drive home. **The system produced a complete arc — quit attempt + meeting collision + relationship cost — from a few interlocking timers.**

### Archetype fit
- **The Recovering** — known smoker now quitter; the bench is dangerous proximity; the bench-cluster is a relapse trigger.
- **The Cynic** — chain smoker. Low jitter threshold; just steady consumption. Bench is their kingdom.
- **The Affair** — the bench is a private space where The Affair partner can also "happen to" be smoking.
- **The Climber** — quit twice, smokes again, claims they don't.

---

## 2. CaffeineSystem extension — The Coffee→Pee Chain

### The human truth
The heavy coffee drinker's day is also clock-structured. The morning two cups; the post-lunch refill; the 3pm crisis cup. Every cup goes through the bladder in 45–60 minutes. Their bathroom-trip rate is double the non-coffee-drinker's, and they know it, and they plan around it.

### What it does
The existing CaffeineSystem proposal handles the alertness side. This extends to the bladder side: each coffee consumption queues a `BladderFillBoost` that activates on a delay. The coffee drinker has a known higher bladder-fill rate during their consumption window.

### Components / data
- Reuse: existing `BladderComponent` (Fill Rate); existing `CaffeineComponent` (proposed)
- New addition to `CaffeineComponent`: `DiureticBoostQueue: List<(activationTick, magnitude)>`

### Mechanics
- The classic chain: morning coffee at 8:30 → bladder critical at 9:45 → bathroom break → return at 9:55 → second coffee at 10:30 → bladder critical at 11:45 → and so on.
- The meeting-and-coffee collision: an NPC who drinks coffee right before a long meeting is gambling. The captive component (proposed) interacts directly. The shifting-in-chair behavior is observable; the gamble is observable.
- The dehydration bug: many coffee-drinkers don't drink water; existing `MetabolismComponent.Hydration` drains faster than they realize. End-of-day headache is a real biological state with measurable behavior implications.

### Extreme end
The 90-minute meeting was supposed to be 60. Three coffee drinkers in the room. Two drank a fresh cup right before. By minute 75 all three are visibly fidgeting. By minute 80 the first one stands up and excuses themselves. The second follows two minutes later. The third holds out — they are the highest-conscientiousness, the meeting is run by their boss — until minute 87. Stress accumulator just spiked across three NPCs from one scheduling decision.

---

## 3. SugarSystem + EnergyDrinkSystem

### The human truth
The candy drawer at someone's desk. The 3pm Snickers. The Red Bull on the morning of a deadline. The Coke can in the afternoon, the second Coke when the first one wore off. Sugar and caffeine are different but they overlap in the office and produce parallel crashes.

### What it does
A `BloodSugar` modifier with a faster, sharper curve than caffeine. Sugar produces a short-duration mood lift, energy bump, then a crash 60–90 sim-minutes later. The crash is a real measurable productivity-and-mood drop. Sugar-dependent NPCs have a regular mid-afternoon dip that resolves only with another hit.

### Components / data
- New: `BloodSugarComponent { Level: float (0..100), Baseline: float, LastSurgeTick: long }`
- New world-objects: `CandyDrawerComponent` (per-NPC), `SodaMachineComponent` (room object)

### Mechanics
- The candy drawer is a social object. Some NPCs share. Some hoard. The shared-candy person accumulates `Belonging` slowly; the hoarder is privately resented.
- The afternoon trip-to-the-vending-machine is a near-universal pattern. NPCs cluster around it briefly. Conversation happens. Snippets are picked up by the eavesdrop layer.
- An NPC trying to lose weight (deal-catalog) suppresses the candy-reach using willpower. The diet-bet (see foodscape) makes this public.

### Extreme end
The 4:14pm sugar-crash domino. Three NPCs in the cube row peaked at 2:30 on the same vending machine run; all three crash at the same time; productivity vanishes; conversation across cubicles increases (low-energy chatting is the path of least resistance); the engine notices three flow-states broke in one tick. The afternoon has visibly slumped.

---

## 4. AlcoholSystem (the deal-flagged drinker, the after-work bar, the holiday party)

### The human truth
Some offices have it openly (the founder's whisky drawer); some have it secretly (the flask in the desk); some have it ritually (the Friday-after-work bar). Alcohol is a willpower-deflater and a mask-removal agent and a slow-killer hidden by competence.

### What it does
A `BloodAlcohol` modifier that:
- Lowers willpower aggressively
- Lowers inhibition strength fractionally (the dam weakens)
- Slows movement and reaction
- Improves mood briefly
- Impairs judgment in action-selection (suboptimal choices weighted higher)

### Components / data
- New: `BloodAlcoholComponent { Level: float (0..100), Tolerance: int, LastDrinkTick: long }`
- New: `DrinkingDealComponent { ProblemDrinker: bool, Hidden: bool, FlaskInDrawer: bool, IsRecovering: bool }`
- New world-object: `BarLocationComponent` (the after-work spot, off-screen but tracked)
- Reuse: `WillpowerComponent`, `InhibitionsComponent`, `MovementComponent`

### Mechanics
- The hidden drinker has a ritual: drawer-open during privacy, sip, drawer-close. NPCs who walk in during the ritual *almost* notice. The almost-notice has a confidence on the `KnowledgeComponent` entry — they're not sure what they saw.
- The Friday-bar event: an opt-in social event after work. NPCs who go are tagged `AtBarTag`. Trust between attendees increases; gossip and confessions flow more freely.
- The holiday party is the canonical mask-collapse event for the whole office. Multiple NPCs drinking; multiple inhibition-weakenings; relationship-pattern transitions cluster.
- The Recovering archetype (per cast bible) holds zero blood alcohol; every drink-offer is a willpower draw. The relapse is a story-class event.

### Extreme end
The holiday party. The Climber, three drinks in, kisses the wrong person. Two NPCs see it. The Affair partner sees a different kiss happening across the room — the Climber's attention had wandered earlier in the night. Three relationships pivot in a single 90-minute window. The next morning, every NPC who was there is hung over (low energy + headache tag + slight irritation gain). Half the office knows; half doesn't. Knowledge propagates over the next week. Three days later, an HR conversation happens. **A holiday party in this engine should be capable of producing 30+ days of social fallout.**

### Archetype fit / inhibitions
- **The Cynic** — drinks competently; doesn't lose mask; just gets quieter.
- **The Affair** — drinks dangerously; the inhibition weakening is exactly aligned with their dominant pattern.
- **The Recovering** — relapse risk is everpresent.
- **The Founder's Nephew** — the open drawer.
- **The Newbie** — overdrinks at first event, embarrassing event, doesn't drink again for a year.

---

## 5. PrescriptionDependenciesSystem

### The human truth
Some NPCs are on antidepressants. Some are on anxiety medication. Some are on stimulants. Some are on pain meds for a chronic injury. The medication is invisible most of the time; missing a dose is suddenly very visible. The pill bottle in the drawer or the bag is real life.

### What it does
A continuous `MedicationLevel` that decays predictably and is replenished by a `take-medication` action (typically at a specific time-of-day or trigger). Missing doses produces specific behavior shifts:
- Missed antidepressant dose → mood baseline drops; sadness gain rate up
- Missed anxiety med → withdrawal-jitter; panic threshold lower
- Missed ADHD stimulant → focus collapse; impulse-action probability up
- Missed pain meds → pain mood spike; movement-speed reduction

### Components / data
- New: `PrescriptionComponent { Kind: enum, Level: float, DoseSchedule: List<(hour, dose)>, MissedDoses: int, PrivateFromOffice: bool }`
- Hidden by default. The NPC manages it privately; other NPCs do not have it in their `KnowledgeComponent` unless something specific surfaces it (a dropped pill bottle, a witnessed pill-taking moment).

### Mechanics
- The morning-pill-skip cascade: an NPC who oversleeps and skips their dose has a measurably worse day. By 2pm they're noticeably off. The cube neighbor notices; doesn't know why.
- The visible pill bottle in a desk drawer creates a narrative seed. A snooping NPC discovers it; what they do with that knowledge is character-defining.
- Doctor-appointment scheduling is a calendar event that costs work-hours. The appointment is off-screen; the leaving and returning are observable.

### Extreme end
The Recovering archetype is on naltrexone. They miss a dose. The willpower-draw to avoid the bar that evening is doubled. They go. They drink. The relapse arc activates. Two timers — pill schedule and bar proximity — interlock to produce a story moment.

### Mature handling
The soften-toggle (UX bible §4.6) handles this gracefully: medications can be modeled as "morning routine" without the specific pill-bottle visibility, while keeping the underlying simulation consequence.

---

## 6. PhoneScrollAddictionSystem (era-2000s version)

### The human truth
Modern offices: people pull out their phones every few minutes. For era-2000s pre-iPhone: the email-refresh compulsion. The NPC who refreshes Outlook every two minutes. The pager-checker. The desk-phone-fidgeter (picks it up to make sure there's a dial tone).

### What it does
A `CheckCompulsion` rate per NPC: a per-tick probability of triggering a `CheckAction` that interrupts current work, costs ~5 sim-seconds, breaks flow.

### Components / data
- New: `CheckCompulsionComponent { CompulsionLevel: int, TargetKind: enum (Email | Pager | Phone | DeskPhone), LastCheckTick: long }`

### Mechanics
- High-neuroticism + high-openness boosts compulsion.
- Anxiety-driven checking: when stress is high, compulsion triples.
- The cube-neighbor of a heavy checker notices the ritual; passive-aggressive note about "constantly clicking" eventually appears.

### Extreme end
The Climber refreshes their inbox 41 times during one 12pm-1pm hour while waiting for an executive's response. They get nothing done. Their `flow` never enters once. Their stress baseline rises. Every time they check, their cube neighbors notice the chair-creak. Over a month, the cube neighbor's irritation baseline rise has visible relationship cost.

---

## 7. The Snack Reach

### The human truth
The pretzel jar on someone's desk. The bowl of M&Ms at the front desk. The handful absent-mindedly grabbed every time someone passes. Hunger is irrelevant. The reach is automatic. Some NPCs do it; some don't; some do it more when stressed.

### What it does
A separate-from-hunger `SnackImpulse` that fires when:
- Passing a snack source within reach
- Stress above threshold
- Personality bias (high openness, low conscientiousness)

The reach action is fast (~2 sim-seconds), small calorie input, mood-comfort tick. Repeated reaches in a short window stack into a noticeable pattern.

### Components / data
- New: `SnackingHabitComponent { Compulsion: int, FavoriteFoods: List<string> }`
- New: `OpenSnackContainerComponent` on world objects (the pretzel jar, the candy bowl)

### Mechanics
- The maintainer of the snack jar is doing a `Belonging` favor for the office (per FavorEconomy). The taker without contributing is a small running ledger debit.
- The dieter's willpower-draw against the snack reach is high and constant; their proximity to the snack object should be reduce-able by management.
- The candy bowl at reception is a *deal* — the NPC who maintains it is performing belonging at a small cost to themselves.

### Extreme end
A weight-loss-betting pair (deal-flag) sits with the snack jar between them. One of them is winning. The other reaches absent-mindedly. The first sees it. They say nothing — they're winning either way. The reaching one's `willpower` drops faster the rest of the day. By 4pm they break completely and eat half the jar. Their bet partner watches. They smile, kindly. The relationship just got a tiny bit deeper.

---

## 8. The Vape Closet (era-2000s version: bathroom-stall smoker)

### The human truth
The bathroom-stall smoker — illicit, in-building, the sneak. The smell hangs, the smoke alarm fails to register, the cube neighbor of the bathroom knows immediately. It's an act of micro-rebellion and a visible status statement.

### What it does
A specific `IllicitSmokeAction` that an NPC may take in private spaces, faster but riskier than the bench. Risk: the `OdorComponent` puff is detectable; a passing NPC may file a `KnowledgeComponent` entry; if a manager-class NPC notices, formal-trouble flag potentially activates.

### Extreme end
The Newbie, terrified of smoking on the bench because their boss smokes there, has been hitting the basement bathroom stall. The smell is detectable to anyone passing for ~10 sim-minutes after. Maintenance-NPC eventually flags. A polite-but-pointed all-staff email about "policy" ensues. The Newbie's private rebellion has visible-but-undirected office consequences. They suspect everyone knows. Nobody does specifically.

---

## 9. The Quit Streak Counter

### The human truth
"Day 11 nicotine-free." "Three weeks since I had a drink." "I've eaten clean for nine days." The streak is its own object the NPC maintains. It's fragile. It feels like identity. When it breaks, the failure is internal first, social later (or never).

### What it does
A general `QuittingStreakComponent` for any addiction-type. Streak ticks per day. Streak breaks fire a `RelapseEvent` — high-noteworthiness, persistent, can install a new `Vulnerability` inhibition or strengthen an existing one.

### Components / data
- New: `QuittingStreakComponent { StreakDays: int, LongestStreakDays: int, AddictionTarget: enum, StreakStartedTick: long, RelapseCount: int }`

### Mechanics
- Streak ticks themselves produce a tiny `joy` and `belonging-with-self` boost.
- Streak public-shares (mentioning the streak in conversation, calcify-prone) accumulate `accountability` from listeners.
- Relapse silence — failing without telling anyone — produces a `MaskHole` (a constantly-leaking willpower drain from the secret).

### Extreme end
The Recovering archetype has a 47-day no-drink streak. The holiday party. The relapse. The next morning, willpower is at a baseline drag from the secret. They need to tell their `Confidant`. They almost do. They don't. The willpower drag persists for days. The streak counter is not reset publicly — they're still officially "47 days." A new `inhibition` against vulnerability has installed. Multiple system effects from one break.
