# 05 — Culture, Religion, and Belief

> The user explicitly called out "different cultures and religions all kinds of different people all forced to be in the same place." This cluster covers belief systems and cultural backgrounds as personality dimensions that produce schedule patterns, dietary inhibitions, holiday rhythms, modesty differences, and code-switching. The intent is *capability* — the office can contain a Muslim coworker who prays at midday, a Jewish coworker who leaves early Friday, an evangelical Christian coworker, a witchy astrology-believer, an atheist who can't help debating, a Sikh who wears a turban — without authoring any of them specifically.

---

## 1. CulturalBackgroundSystem + CulturalProfileComponent

### The human truth
Where someone grew up, what they were raised believing, what language is spoken at home — these are foundational personality vectors that shape office behavior in ways nothing else does. The simulation needs to support difference without flattening or stereotyping.

### What it does
A `CulturalProfileComponent` carrying:
- Religious affiliation (or none) — drives observance behaviors
- Cultural background — drives food preferences, communication style, holiday calendar
- Native language — drives accent, code-switching tendencies, occasional first-language slips
- Modesty calibration — drives dress, body-conversation comfort, distance preferences

### Components / data
- New: `CulturalProfileComponent { Religion: enum (None | Christian | Jewish | Muslim | Hindu | Buddhist | Sikh | Spiritual | Atheist | Other), Background: string (free-form), NativeLanguage: string, ModestyLevel: int (0..100), ObservanceStrictness: int (0..100) }`
- New: `ObservancePracticesComponent { DailyPrayerTimes: List<float>, WeeklyObservanceDay: enum DayOfWeek, ForbiddenFoods: List<string>, HolidayCalendar: List<(date, severity)> }`

### Mechanics
- Spawn-time: each NPC rolls a CulturalProfile from a population distribution (configurable in SimConfig). Most NPCs are non-strict whatever-they-are; a few are strict.
- Behaviors derive from profile. Prayer times drive a daily schedule entry; dietary rules drive food-action filtering; observance day drives schedule shifts.

### Capability over content
The bibles' "capability over content" axiom applies hard here: the engine doesn't author culture *specifics*; it provides hooks (forbidden foods, prayer schedules, holiday calendars) that allow *any* tradition to be expressed. The corpus author populates the SimConfig with examples; player save-files can be edited to add more.

---

## 2. PrayerTimeBehaviors

### The human truth
The Muslim coworker who excuses themselves five times a day for a few minutes. The Christian who silently says grace before lunch. The Jewish coworker who leaves early Friday. The Sikh who keeps their head covered. Each is a small recurring interruption to "default office time" that the office gradually accommodates or doesn't.

### What it does
- A `PrayerScheduledBlock` component on the NPC fires at configured times. The NPC seeks a `PrayerSpace` (which can be: a private cubicle, the IT closet, the supply closet, an empty conference room, or — in a hostile-to-faith office — the bathroom). They take 3–10 sim-minutes.
- Prayer is a willpower-restorer (small) and a stress-reducer (small). It is also a `Belonging-with-faith` drive recoverer for observant NPCs.
- Failed prayer (interrupted, displaced) produces small persistent stress baseline rise across the day.

### Components / data
- New: `PrayerSpaceComponent` (room or object): `Suitability: int (0..100), PrivacyLevel: int`
- Reuse: existing `ScheduleComponent`

### Mechanics
- The lack of a designated prayer space is a structural friction. The Muslim NPC who has to use the supply closet is paying a cost the office isn't aware of. The accommodation gradient is itself a story arc — the engine can produce the day a permanent prayer space appears, with all the sub-stories of who advocated for it.
- The casual coworker who walks in on the prayer space midway through prayer: a vulnerability event. The reaction varies by both NPCs' personality and trust level.

### Extreme end
The Newbie is Muslim. Their prayer schedule conflicts with a recurring 12pm meeting they're newly invited to. They miss prayer the first day. By the end of the week, their stress is high and they haven't told anyone why. A high-trust coworker notices the pattern. The conversation that ensues is a real character moment. The meeting time gets moved. The relationship just upgraded. **A scheduling collision produced an arc of trust-building.**

---

## 3. DietaryInhibitionSystem

### The human truth
The vegetarian. The kosher-keeper. The halal-keeper. The lactose-intolerant. The gluten-avoider. The sugar-quitter. Each lives in the office's foodscape with a different filter. The catered-lunch-without-options moment, the breakroom-pizza-with-only-pepperoni moment — these are real micro-frictions for a sizable share of any office.

### What it does
A general food-filtering layer: each food entity carries `FoodTags` (meat, pork, beef, dairy, gluten, alcohol, shellfish, etc.). Each NPC has a `DietaryInhibitionsComponent` listing tags they avoid and the strength.

### Components / data
- New: `FoodTagsComponent { Tags: HashSet<string> }` — applied to all food entities
- New: `DietaryInhibitionsComponent { Avoid: Dictionary<string, int> }` — on NPCs
- Reuse: existing `InhibitionsComponent` model — these are inhibitions in the action-gating bible's sense

### Mechanics
- Eat-action selection filters food by NPC's avoidance list. High-strength avoidances are absolute (no override no matter how hungry). Low-strength avoidances yield to high hunger (the vegetarian who's been trying for a year and lapses on the bacon).
- The lapse moment is a high-noteworthiness event. The vegetarian who ate the bacon notices. They don't tell anyone. The streak resets.
- The catered-lunch problem: an order entity carries `FoodTags`. The number of NPCs whose `DietaryInhibitions` block 80%+ of the catering is a surfacable office-failure.

### Extreme end
A potluck. The kosher-keeper has avoided every dish that has cross-contamination concern (most of them). They have eaten only the obvious-fruit-plate. They are hungry. The Newbie, well-meaning, brings them a piece of cake. They cannot eat it (the cream had been near the ham). They smile and accept it and find a way to discreetly not eat it. The Newbie notices halfway through and doesn't know what to do. *The event is small and real and the engine produced it from a tag system.*

---

## 4. HolidayCalendarSystem

### The human truth
Some holidays are universal in the office (Christmas, Thanksgiving). Some are tradition-specific (Diwali, Yom Kippur, Eid, Lunar New Year). The mismatch — an NPC out for Yom Kippur in a building running normal Wednesday operations — is a cultural visibility event with social weight.

### What it does
A calendar of holidays per `CulturalProfileComponent`. On a holiday, the affected NPC may take the day off, request flex, or work but with a `holidaySpiritActive` mood baseline shift.

### Components / data
- New: `HolidayCalendarComponent { OffDays: List<(date, severity, observance)> }`
- New world calendar: `HolidaysToday` queryable global state

### Mechanics
- The "I won't be in tomorrow, it's [holiday]" announcement is a calcify-prone dialog moment. Some coworkers ask about the holiday warmly; some respond awkwardly; some pretend not to have heard.
- An NPC whose holiday is widely-observed (Christmas) takes it for granted. An NPC whose holiday is rare faces a small recurring decision: explain or not?
- The "but I have a deadline" pressure: a workload conflict on a holiday is a willpower-cost moment.

### Extreme end
A Jewish NPC fasts for Yom Kippur. They came in to work. By 2pm, energy is at 18, focus is collapsing, mood is low, willpower is slipping. The cube neighbor asks if they're okay. They almost lie. They tell the truth: it's Yom Kippur, they're fasting, they shouldn't have come in. The neighbor sends them home. The relationship deepens. **The simulation produced a moment of caretaking from a fasting timer.**

---

## 5. CodeSwitchingAndAccents

### The human truth
The coworker on a phone call with their family who suddenly switches into a different language. The accent that surfaces under stress. The phrasing that shows the speaker is doing a real-time translation. These are small windows into who someone *also* is, outside this office.

### What it does
- A `LanguageRegisterComponent` per NPC: native language(s), default office language, code-switching triggers (caller-ID detected, family member, very high or very low willpower).
- During code-switch, the dialog corpus uses a different set of fragments OR (cheaper) the same fragments are tagged as "unfamiliar" to the listener.
- The accent-under-stress mechanic: when willpower drops below a threshold, the NPC's English-as-second-language accent thickens and produces dialog tagged with `accentThick` modifier.

### Components / data
- New: `LanguageRegisterComponent { NativeLanguages: List<string>, OfficeDefaultLanguage: string, AccentLevel: int }`
- Reuse: dialog corpus + register filter; this is one extra dimension

### Mechanics
- Witnesses to a code-switch register a small `Surprise` mood and a `KnowledgeComponent` entry about the speaker. It's vulnerability information — the speaker has a richer life than the office knew.
- A bilingual NPC switching to their native language with a same-language coworker is a private-language moment that excludes others; trust between the two grows; mild `Suspicion` from the excluded.

### Extreme end
The Climber is on a call with their elderly mother. They've been performing perfect-English-corporate-speak for years. The mother is hard of hearing and the Climber is forced to repeat themselves loudly in Mandarin. Three coworkers are in awareness range. None of them knew the Climber was Chinese. The post-call moment is awkward. The Climber's mask has slipped permanently for those three witnesses — they've seen a version of the Climber the Climber has been carefully hiding. The relationships are subtly different from this moment forward.

---

## 6. ModestyAndPersonalSpaceCalibration

### The human truth
The cousin who hugs everyone. The cousin who shakes hands only. The cousin who bows. Different communicative-touch standards collide in an office and produce small constant micro-readings — most go fine, a few don't.

### What it does
A `ModestyAndContactProfileComponent`: per-NPC settings for:
- Default greeting (none / nod / handshake / hug / cheek-kiss / two-cheek-kisses)
- Touch tolerance (back-pat, shoulder-touch, hug-from-behind)
- Hand-shake preferences (firm / soft / no)
- Gender-of-other modulation (some NPCs greet same-gender differently)
- Hair-touching, hand-holding, or other specific modesty rules

Mismatch events between NPCs produce small `awkwardness` moments — both parties register a slight discomfort. Repeated mismatches produce calibration over time (one of them adjusts).

### Components / data
- New: `ModestyAndContactProfileComponent { GreetingPreference: enum, TouchTolerance: int, GenderModulation: dict }`

### Mechanics
- The first-meeting calibration: handshake-vs-hug-vs-bow attempt produces an awkward moment that both parties remember. Subsequent meetings smoothly accommodate.
- Cross-cultural friction is realistic: the boss who claps everyone on the back triggers small mask-slips in NPCs whose modesty is high. They register but don't comment. The boss notices nothing.

### Extreme end
A new hire from a culture where men don't shake hands with women starts work. The female manager extends her hand on day one. The new hire freezes — both inhibitions (don't refuse boss; don't shake hand) collide. After a long beat, he bows. The manager is briefly confused, then graceful. A small initial-trust deficit develops on her side that gets cleared up over weeks once she understands.

---

## 7. The Quiet Religious Person and the Loud Atheist

### The human truth
The atheist who can't resist debating. The quiet faith-keeper who never proselytizes. The two of them seated near each other produce a slow, asymmetric tension that erupts only in specific contexts (someone's birthday, a death in the office, a holiday party).

### What it does
- The atheist with high-extraversion + high-openness + low-agreeableness has a `proselytizationDriveAtheist` micro-trait: in conversations near religious topics, they can't help themselves.
- The quiet religious NPC has high-conscientiousness + high-agreeableness, doesn't take the bait, but accumulates `Suspicion` and small `Irritation` per encounter.
- Over months, the asymmetry produces a one-sided rivalry — only one of them experiences it as a rivalry; the other one experiences themselves as friendly-debating.

### Components / data
- Reuse: `PersonalityComponent` (Big Five) + `CulturalProfileComponent` for the calibration
- New optional: `ProselytizationStyleComponent { Active: bool, Domain: enum (Faith | Atheism | Politics | Diet | Productivity), Intensity: int }`

### Extreme end
After a coworker's death, the quiet faith-keeper finds comfort in private prayer at their desk. The loud atheist, walking by, makes a half-joke about religion. The quiet faith-keeper looks up. The atheist suddenly sees the room clearly. Apologizes. The relationship recalibrates from one-sided rivalry to mutual friendship. *The simulation produced a real growth moment under grief pressure.*

---

## 8. AstrologyAndOfficeSpiritualism

### The human truth
The coworker who reads tarot cards at lunch. The one who knows everyone's sign. The one who always asks "Mercury retrograde?" when something goes wrong. Spiritualism without organized religion is its own office subculture.

### What it does
- A `SpiritualPracticesComponent` carrying tarot, astrology, crystals, manifestation, etc.
- Adds new dialog contexts (`prediction`, `interpretation`) that fragments can fill.
- Other NPCs' reactions vary widely — some lean in, some politely tolerate, some are hostile.

### Mechanics
- A `BadPredictionPattern` would erode trust; a `GoodPredictionPattern` builds it (oddly accurate).
- The non-believer who scoffs and is later proven wrong gets a friendly ribbing from the spiritualist. Calcify-prone.

### Extreme end
The astrologer correctly predicted, six weeks in advance, that "this will be a hard October." Three crises landed in the first two weeks. By mid-month, three other NPCs are quietly asking the astrologer to read their charts. The astrologer's `Status` rises. The Cynic refuses to engage and the rivalry between them deepens. **The engine produced an emergent prophet via persistence threshold + retroactive significance.**

---

## 9. The Politics Topic (avoided everywhere except by certain NPCs)

### The human truth
Most offices have an unspoken "no politics" rule. Most. Some NPCs cannot help themselves. The political-topic broacher in a 2026 office is a known threat — coworkers track who can and can't be trusted to keep politics out of work conversation.

### What it does
- A `PoliticalEngagementComponent` per NPC: opinion strength, frequency-of-bringing-it-up, topic-of-choice.
- Most NPCs avoid. A few introduce. The introducers have varying levels of social tact.
- Introducer + listener-disagrees-with-them = irritation event. Repeated = `Rival` pattern installs.

### Components / data
- New: `PoliticalEngagementComponent { Frequency: int, Topics: List<string>, Tact: int }`

### Mechanics
- The election week is a measurable office-wide stress increase.
- The "I shouldn't have said that" moment after a political slip is a high-stakes mask-slip; the NPC's reputation can shift permanently in one comment.

### Extreme end
The election results post a Wednesday morning. Three NPCs come in obviously affected. The Climber, who has been carefully neutral, accidentally lets a phrase slip that reveals which side they're on. Two coworkers who had assumed otherwise re-evaluate the Climber. One of those coworkers is the Climber's `Mentor`. The mentor's `Trust` toward Climber takes a small hit. The Climber doesn't realize for weeks.

---

## 10. Generational Divides

### The human truth
The Boomer who keeps mentioning when they were young. The Gen-Xer with the dry sarcasm and "I don't care" energy. The Millennial juggling burnout and side-hustle. The era-2000s setting puts these three in the same building with different working styles, different references, and different expectations.

### What it does
- A `GenerationComponent`: birth-year cohort, generational reference set, work-norm expectations.
- Reference incomprehension produces small flat moments: a younger NPC doesn't catch a movie quote; an older NPC doesn't catch a new-slang.
- Work-norm differences produce ambient friction: the Boomer who values face-time vs the Millennial who values output.

### Components / data
- New: `GenerationComponent { Cohort: enum, ReferenceSet: List<string>, WorkNormExpectation: enum }`

### Mechanics
- Reference incomprehension is a calcify-prone dialog moment: the bridge across the gap, when it works, is a `Belonging` event.
- The "kids these days" eye-roll and the "ok boomer" eye-roll are equal and opposite micro-aggressions; both register; both fuel the rivalry pattern over months.

### Extreme end
The Old Hand and the Newbie have a generation gap. The Old Hand keeps making references the Newbie can't follow. The Newbie tries to bridge. Eventually the Old Hand makes one specific reference the Newbie *does* catch (a band, an event, a film). The look exchanged is a real moment. The relationship upgrades from `Boss/Report` to `Mentor/Mentee` in a single tick.

---

## 11. The Family-Food Gesture (visiting culture for the office)

### The human truth
The Indian coworker whose family visits for two weeks and they bring sweets to the office. The Filipino coworker who brings lumpia for everyone after a holiday. The Mexican coworker whose tamale-making mother sends a tray. These shared-food gestures are belonging-rituals from one culture to the office at large.

### What it does
- A `BringingFamilyFood` event: a culturally-specific food shared with the office. Spawns a tray entity in the breakroom.
- The food has a `noveltyBonus` modifier: NPCs who eat it get a slightly larger `Belonging` boost than from generic food.
- The bringer accumulates `Belonging` and `Affection` from the office at large; their `Status` modestly increases.

### Mechanics
- Calcify-prone moments: the conversation about the food, who liked it, the asking-for-the-recipe. Real bonding is generated.
- The bringer who feels under-appreciated (low engagement from the floor) registers a real `Loneliness` and `Sadness` over days. This is a significant social cost they didn't expect.

### Extreme end
The Newbie brings their mother's signature dish. They are nervous. Three NPCs love it visibly. Two pretend not to notice. The Climber asks about it warmly. The Newbie's `Belonging` doubles. The Newbie's mother gets to hear about it that evening. The next time the Newbie has a hard week, they remember this moment and it carries them through. **A persistent moment generated from a tray of food.**
