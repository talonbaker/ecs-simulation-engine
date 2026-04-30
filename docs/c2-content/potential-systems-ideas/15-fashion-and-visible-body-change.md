# 15 — Fashion and Visible Body Change

> The body is the most-public surface of an NPC's life and the most-readable channel the floor has. Cluster 06.5 introduced `AttireComponent`. This cluster goes deeper: visible body changes, the new ring / no ring, the hair, the weight, the makeup, the day they showed up looking ten years older or ten years younger. The office reads everything; nobody references most of it; the relationship matrix updates anyway.

---

## What's already in the engine that this builds on

- `AppearanceDecaySystem` and `AppearanceComponent` from `new-systems-ideas.md`.
- `AttireComponent` from cluster 06.5.
- `KnowledgeComponent`, `RumorSystem`.
- The aesthetic bible's silhouette catalog (height, build, hair, distinctive items, dominant color).

---

## 15.1 — TheRingAxis (wedding rings, engagement rings, the missing ring)

### The human truth

A small piece of metal on a hand carries enormous social information. The wedding ring. The engagement ring (new!). The "I notice your ring is suddenly off" moment. The not-yet-disclosed engagement that the ring announces before the speaker does. The "I keep mine in my desk drawer" deal. The widow who still wears their late spouse's ring on the other hand. Each of these is a status the floor reads silently.

### What it does

`HandJewelryComponent` per NPC:

- `weddingRing` — `present | absent | recentlyRemoved | recentlyAdded | placedElsewhere`.
- `engagementRing` — `none | new | longStanding | placedElsewhere`.
- `otherSignificantRing` — class-D ring as deal-catalog data (signet, family heirloom, support-group token).

State changes are *high-noteworthiness* events:

- **NewEngagement** — the new ring on Monday. The Vent notices in the first hour. The story propagates within a day.
- **RingRemoval** — on Tuesday she didn't have it. The floor notices. Nobody asks. The KnowledgeComponent records the day; speculations propagate.
- **RingReturn** — sometimes the ring comes back after a few weeks. Usually means a fight resolved. Occasionally means a denial.
- **RingMovedToOtherHand** — widowhood signal.

### Cross-system interactions

- **DivorceArc (cluster 9.3):** ring removal is often Phase 1 visible signal months before any disclosure.
- **AffairArchetype:** the partner of an Affair NPC sometimes notices the ring is *removed* on specific days.
- **RumorSystem:** ring changes are high-confidence first-hand observations that propagate fast.
- **CalendarRitualSystem (10.5):** anniversary dates link to ring data.

### What the player sees

A specific channel of information that the player can learn to read across the entire cast. A ring change is a signal-rich event.

### The extreme end

The Climber's ring is suddenly absent on a Tuesday. By Friday the floor has six theories. By Monday the next week, the ring is back. Nobody has asked; everyone has wondered. The chronicle records both the absence and the return. The Climber's `mask` was at maximum draw all week and they don't know the floor noticed.

---

## 15.2 — VisibleWeightChange

### The human truth

An NPC has lost weight. An NPC has gained weight. Is it intentional? Health? Stress? Recovery? Pregnancy? The floor wonders silently and rarely asks. The conversation about it almost never goes well. The complimenter's compliment may land or wound. The non-compliment is its own statement. The very-rapid weight loss is alarming; the slow weight gain is silently noted.

### What it does

`BodyShapeComponent` per NPC:

- `bodyShapeBaseline` — silhouette family (per aesthetic bible).
- `bodyShapeCurrent` — current state, drifts from baseline over sim-time.
- `recentChangeRate` — sim-pounds per week (or proxy unit).

Dramatic changes (above threshold rate) generate `VisibleChangeEvent` in the floor's KnowledgeComponent. Other NPCs notice but rarely speak. Speculation propagates through the rumor system.

### Causes

- **Stress (chronic, existing):** weight loss in some NPCs, gain in others (stress-eating archetype).
- **Diet (cluster 06.2):** intentional change.
- **Illness (cluster 09.4):** loss.
- **Pregnancy (cluster 9.6):** gain.
- **Substance arc (cluster 12):** either direction.
- **Recovery from depression:** sometimes gain (medication side effect), sometimes loss (newfound activity).

### Cross-system interactions

- **KnowledgeComponent:** the *cause* is often unknown to the floor and they speculate. The truth lives in the affected NPC's components; the speculation lives in coworkers' heads.
- **The compliment minefield:** the comment *you look great!* lands variably depending on whether the NPC is gaining (offended), losing intentionally (pleased), losing unintentionally (worried), or recovering from an eating disorder (very offended).

### What the player sees

A coworker who looks visibly different over time. The player reads the cause via simulation state; the floor only sees the change.

### The extreme end

A Recovering archetype who's been struggling with substance use loses 25 sim-pounds over six weeks. The floor reads the change and worries silently. The Newbie compliments the loss enthusiastically — *meaning well*. The Recovering archetype's mask draws hard; they walk out of the conversation. The Newbie has no idea what just happened. The Vent quietly explains later.

---

## 15.3 — TheHaircutEvent

### The human truth

The big haircut. The change in style. The first day back at work with a noticeably different look. Coworkers' responses range from explicit compliment to deliberate ignoring. Some haircuts read as *something happened*. Some read as *signaling availability*. Some are just hair.

### What it does

`AppearanceChangeEvent: haircut` fires when an NPC arrives with a meaningfully different hairstyle. Other NPCs in proximity fire `HaircutNoticeEvent` with three possible flavors:

- **Compliment** — high-agreeableness, low-stakes.
- **Joke about it** — Cynic-style, sometimes lands warmly, sometimes as cutting.
- **Notice without speaking** — most NPCs default here.

The affected NPC's response:

- High agreeableness: small `belonging` lift from compliments; small disappointment from non-comments.
- The Climber is calibrating — if they got the haircut for a specific event, the right amount of attention is targeted.
- The Vent compliments everyone's haircuts. Calibrates her own warmth to whoever's standing in front of her.

### Cross-system interactions

- **DivorceArc / RebrandingArc (14.4):** haircuts are signal events.
- **HolidayParty (10.3):** holiday-party haircuts are common.

### What the player sees

A small predictable office moment when someone's hair is different.

### The extreme end

A long-tenured female NPC, after twenty years of the same hairstyle, gets a drastic cut. The floor stops what it's doing. Three NPCs say something supportive; two say nothing. The cut signals a major life change the affected NPC is not yet ready to discuss. Six weeks later the divorce paperwork lands. The floor pieces it together retroactively.

---

## 15.4 — MakeupAndGroomingDailyState

### The human truth

Some NPCs apply makeup every day; some never; some only on specific days. The day someone normally-with-makeup arrives without it is a flag. The day someone normally-without arrives with it is a flag. Same for shaving, beard length, nail care, eyebrow grooming. These are daily readings of mental state.

### What it does

`GroomingState` per NPC:

- `groomingBaseline` — what they normally do.
- `groomingToday` — what they did today.
- `effortDelta` — the gap between baseline and today.

Negative `effortDelta` (less grooming than baseline) signals: bad-evening bleed, tired, depressed, busy morning, fight at home.

Positive `effortDelta` (more grooming than baseline) signals: presentation today, interview, holiday party, the tension partner will be in the office, important meeting.

### Cross-system interactions

- **AttireComponent:** grooming and attire are often co-modulated.
- **KnowledgeComponent:** the effortDelta calibration is a real channel coworkers read.

### What the player sees

Subtle daily reading that the player learns over time.

### The extreme end

A Climber who never under-grooms arrives on a Thursday with no makeup, hair pulled back hastily. The floor reads it as an emergency. Nobody asks. By the afternoon their `mask` collapses in a small private moment in the bathroom. The floor reads the whole day in a single glance at 9am.

---

## 15.5 — TheBigGlasses / VisionChangeMoments

### The human truth

The reading glasses appearing one Monday — *I'm getting old*. The new prescription that changed the frame style — readjustment. The contact-lens mishap (the day they had to wear glasses unexpectedly). The eyepatch from a procedure. Each is a small visible change.

### What it does

`EyewearComponent` per NPC. State changes generate `EyewearChangeEvent`. Coworkers' responses calibrated by relationship. Reading glasses on someone for the first time is a low-noteworthiness moment that calcifies as a *wait, when did Frank start wearing reading glasses* memory.

### What the player sees

Small office moments around aging and visible change.

### The extreme end

The Old Hand's reading glasses disappear one day. They squint at their screen. Three coworkers notice. By the next day they have new ones. Nobody asks. The chronicle records the small loss-and-replacement as *the day we noticed how much Frank's been depending on those.*

---

## 15.6 — VisibleHealthShifts

### The human truth

The pallor. The dark circles. The bruise from a fall they didn't mention. The cold-sore. The eye-twitch that won't stop. Specific *health* signals are visible but largely unaddressable. The Cynic might say something dry; everyone else stays silent.

### What it does

`HealthVisibilitySignals` derive from existing health, illness, stress, sleep, and chronic-condition components:

- `palor` — function of sleep + nutrition.
- `darkCircles` — sleep deprivation.
- `bruising` — incidental events from cluster 02.8 and 09.
- `coldSore` — small probability event under stress.
- `tremor` — chronic stress + caffeine + low blood sugar combination.

These manifest as `AppearanceComponent` modulations and as text-cues for other NPCs' KnowledgeComponent.

### Cross-system interactions

- **AppearanceDecaySystem (existing):** layers cleanly on top.
- **KnowledgeComponent:** chronic visible health shifts calcify as floor knowledge.

### What the player sees

A floor where health is visible without being narrated. Some NPCs are observably *not OK*; the office handles that or fails to.

### The extreme end

A Recovering archetype mid-relapse has dark circles, palor, weight loss, and a tremor. Every signal is on. The floor reads it. Nobody knows what to do. The chronicle records it as a slow-motion alarm with no clear responder.

---

## 15.7 — VisibleSeasonalChange (the tan, the pale-from-vacation, the cold-weather drift)

The seasonal NPC. The summer-tan office. The pale-but-rested-from-vacation. The winter pale across the whole floor. These are minor texture but real.

### What it does

`SeasonalAppearanceModifier` applies floor-wide (the cold-weather pale of January) and per-NPC (the post-vacation tan). Mostly atmospheric.

### What the player sees

A floor that looks different in different parts of the year.

---

## 15.8 — TheRebrandedSilhouette

### The human truth

The aesthetic bible commits to readable silhouettes — players learn NPCs by silhouette. When an NPC *changes* silhouette (significant weight loss, drastic haircut, posture change from training/illness, dress style overhaul), there's a transition period where the floor is recalibrating.

### What it does

A `SilhouetteRecalibrationEvent` fires when a sufficient combination of changes has accumulated. Other NPCs' visual-recognition of the changed NPC is briefly less reliable (a small probability of looking-twice events). Over a few sim-days the new silhouette calcifies into the recognized identity.

### What the player sees

A specific kind of office moment: *did you see Linda? I almost didn't recognize her.*

### The extreme end

The Recovering archetype's new fitness-and-recovery routine over a year produces a fundamentally different silhouette. The floor's recognition catches up gradually. By month 14, there's a moment in the chronicle when an old client visits and doesn't recognize the Recovering archetype — but a coworker who's been there the whole time looks up and *sees the journey* in a single glance. The chronicle records the moment.
