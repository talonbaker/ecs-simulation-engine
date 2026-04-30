# 01 — Bodily Taboos

> The mature-office bible commits to depicting real adult life; the engine already simulates the digestive pipeline, bladder fill, and choking. What's missing is the **socially-gated body** — the everyday eruptions that office decorum pretends don't happen and that everyone present has a personal-rule about.

---

## 1. FlatulenceSystem + GasComponent

### The human truth
Everyone farts. Almost nobody admits it at work. The held-in fart is one of the most universal office-discomfort experiences, and the moment one slips out is a high-stakes social event with a precisely-calibrated etiquette: pretend it didn't happen, pretend you didn't hear, walk away if you can.

### What it does
A pressure variable on the colon pipeline (already exists) that occasionally produces a `GasReleaseEvent`. The release has audibility, smell intensity, and an active willpower draw if suppressed. Suppression is not free — it costs willpower per tick like any other inhibition-mediated act.

### Components / data
- New: `GasComponent` (per NPC) — `Pressure: float (0..100)`, `BuildRate: float`, `LastReleaseTick: long`, `SuppressionDrainPerTick: int`
- Reuse: `WillpowerComponent`, `InhibitionsComponent` (`publicEmotion`, new optional class `bodilyDecorum`), `SocialMaskComponent`, the proposed `OdorComponent`
- New event type: `GasReleaseEvent { audibility: 0..100, magnitude: 0..100, source: NpcId }`

### Mechanics
- Pressure builds with food intake (especially specific food types — beans, dairy, the breakroom chili — flagged in `FoodObjectComponent.NutrientsPerBite` or a new `GasGenerationFactor`).
- High-strength `bodilyDecorum` inhibition + high willpower → release suppressed. Suppression drains willpower at a rate proportional to pressure.
- When willpower < pressure-derived threshold OR the NPC is in a low-exposure space (alone, smoking bench, IT closet) the release fires.
- Release emits an `OdorComponent` puff, an audio trigger (`Bfft`, `Phrrrt`, dignified `Pft`), and a proximity event tagged `bodilyEmission`.

### Social fallout
- NPCs in awareness range receive a `disgust` mood spike scaled by smell + audibility.
- The polite reaction is the *non-reaction*: high-conscientiousness NPCs spend mask-energy actively pretending they didn't notice. This is itself a willpower draw.
- The Cynic and the Founder's Nephew register no shame. The Climber registers maximum.
- Blame attribution: if the source denies it (low audibility), one of the witnesses gets falsely-suspected. Add a `BlameEvent { suspect, accuser, confidence }` that propagates through `KnowledgeComponent`.

### Extreme end
The Climber, mid-presentation in the conference room, has been holding pressure for forty minutes. Stress is at 78. Willpower is at 12. The release is audible. Nobody acknowledges it. The presentation continues. Three NPCs have it filed as `KnowledgeComponent` entries tagged `noteworthy`. It will be referenced indirectly by passive-aggression for two game-weeks.

### Archetype fit
- **The Cynic** — `bodilyDecorum: 10`, releases freely, doesn't care.
- **The Climber** — `bodilyDecorum: 90`, suppression-dominant, calibrates spaces.
- **The Founder's Nephew** — `bodilyDecorum: 5`, performative shamelessness.
- **The Newbie** — high baseline, melts down on first slip.

---

## 2. BurpSystem (lightweight cousin)
A simpler discharge, much higher frequency, much lower social cost. Builds with carbonation intake (the soda machine is a key vector). High-extraversion + low-conscientiousness produces the open belch. High-agreeableness produces the swallowed-back. The "excuse me" is a calcify-prone dialog fragment around context `apology`. Half the office uses it; half doesn't bother.

---

## 3. PickersSystem (Nose / Ear / Teeth / Cuticle)

### The human truth
People reach for their face when nobody's looking. They check. They pick. The moment of being *seen* picking is small but real — both parties know what just happened, and both pretend they don't.

### What it does
While idle and unobserved (no NPC in awareness range, head facing away from windows), an NPC may enter a brief `PickingState`. It is interrupted instantly by a proximity event. The interruption produces a small `embarrassment` mood spike on the picker AND a small `disgust` spike on the accidental witness — both filed as suppressed knowledge.

### Components / data
- New tag: `PickingTag` (transient)
- New: `PickingHabitsComponent { Likelihood: int 0..100, Targets: enum flags Nose | Ear | Teeth | Cuticle | Lips }`
- Reuse: `FacingComponent`, `ProximityComponent`

### Mechanics
- Likelihood biased by neuroticism (anxious-pickers), low-conscientiousness, and current stress.
- Witness reaction depends on relationship — close friend gives a permissive shrug; rival files a `KnowledgeComponent` entry tagged `gross`.
- Repeat caught-by-same-witness develops a `KnownPickerOf(NpcId)` flag. Future proximity events between them have a small ambient `disgust` from the witness side that decays slowly.

### Extreme end
Greg, alone in the IT closet, is picking his nose. The Newbie walks in unannounced because the new badge needs setup. Greg's facing direction is *toward the door*. They lock eyes. Neither speaks. The Newbie sets the badge on the desk and leaves. For three game-weeks, the Newbie's `Trust` toward Greg is suppressed by 12 points and neither of them knows why exactly.

---

## 4. BodyOdorSystem

### The human truth
The colleague who runs at lunch and comes back not-quite-showered. The one whose deodorant ran out three days ago. The one whose perfume is so strong it induces headaches. BO is a continuous *aura*, not an event, and its social cost is paid passively over hours.

### What it does
An NPC carries a continuous `PersonalOdor` profile that increases with physical exertion, stress (sweat is real), un-bathed hours, and decreases with morning shower (already implicit in scheduled grooming) and bathroom-sink visits. Strong perfume / cologne overlays it as a separate aura.

### Components / data
- New: `PersonalOdorComponent { Sweat: float, Body: float, Perfume: float, PerfumeFamily: string, Radius: float }`
- Reuse: existing `OdorComponent` from the proposed SmellSystem

### Mechanics
- Aura propagates via the existing proximity/awareness range. NPCs within range receive disgust input proportional to severity AND inversely proportional to their personal tolerance (a personality trait — high openness = wider tolerance).
- The high-perfume NPC produces *positive* responses for some, negative for others. Reactions are not symmetric.
- The lunch-runner has a daily 12pm–2pm spike that propagates through the cube row. Cube neighbors are systematically more irritated post-lunch even when they can't articulate why.

### Extreme end
A sustained-perfume NPC (deal: "always wears too much perfume") produces a chronic low-grade headache for the cube neighbor. Over weeks, the neighbor's chronic stress climbs. Eventually, passive-aggression fires — a note about "scent-free workplace policy" appears on the breakroom board. The perfume NPC reads it. They know it's about them. They wear less for two days. Then they forget.

---

## 5. SneezeAndCoughSystem

### The human truth
A single sneeze in a quiet office is a public event. The sneezer expects a "bless you." The non-blessing is a small slight nobody discusses. A productive sneeze (the wet one) elicits visible disgust. The chronic cougher in week-three of an unresolved cold is one of the most universally resented office presences.

### What it does
- Discrete sneeze events fire from the existing `IllnessSystem` (proposed) AND from environmental triggers (dust in the supply closet, perfume aura, pollen day, sun-in-eyes for the photic-sneeze archetype).
- `BlessYouEvent` propagates from any NPC in awareness range; whether it's said is gated by:
  - Vocabulary register (formal yes; clipped maybe; crass usually no)
  - Trust toward sneezer
  - Distance — the close blesser is sincere, the far blesser is performative
- Calcify potential: "bless you," "gesundheit," "shut up," and silence are all valid responses; the choice calcifies into voice over time.

### Components / data
- New event: `SneezeEvent { wetness: 0..100, audibility: 0..100, source: NpcId }`
- Reuse: `DialogContextDecisionSystem` to inject a `bless-you` context
- Optional new: `BlessYouLedgerComponent` — tracks who blesses whom, modulating belonging drives

### Extreme end
Greg sneezes in the IT closet. There is no one in awareness range. There is no blessing. He sneezes again. Still nothing. The `loneliness` drive ticks up half a point. The engine produced meaning from absence.

---

## 6. The Visible Stain (clothing, not the world-object kind)

### The human truth
Lunch happened. There is a small spot of marinara on the white shirt. The wearer doesn't know yet. Some coworkers see it and tell. Some see it and don't. The duration between *first witness* and *first informer* is a character test for the witnesses.

### What it does
- An NPC who eats has a small probability of acquiring a `ClothingStainComponent` (color, location, magnitude).
- Witnesses in awareness range register it but do not automatically inform.
- The decision to inform is gated by trust + agreeableness + a `helpfulness` micro-trait.
- High-status witnesses informing low-status NPCs is mostly performed; low-status witnesses informing high-status is harder (status cost).

### Components / data
- New: `ClothingStainComponent { Color: string, Location: enum, Magnitude: int, NoticedBy: HashSet<NpcId>, InformedBy: NpcId? }`
- Reuse: existing `StainComponent` model — this is the wearable cousin

### Mechanics
- Stain visibility scales with proximity and lighting (a bright sunbeam exposes it instantly; the basement hides it indefinitely).
- The stain decays only with bathroom-sink visit (`SinkComponent` already exists) — and the NPC has to know about it to fix it, so an un-informed stain rides through the day.
- Calcify-prone dialog context: `informing-stain` (`"You've got something on your shirt."`) shapes the social graph quietly — the helpful informer accumulates positive trust ledger entries over time.

### Extreme end
The Climber is in a meeting with the CEO. There is a coffee stain on their tie. Three coworkers noticed before the meeting and said nothing — two for status reasons, one because they're a `Rival`. The CEO notices. The meeting outcome is colored by it. The Climber doesn't know why the meeting went the way it did. They go to the bathroom afterward and discover the stain. Their `Suspicion` toward the three potential informers spikes simultaneously.

---

## 7. Lipstick / Spinach / Open-Fly Detector (the "tell-them" microsystem)

### The human truth
Lipstick on teeth. Spinach in teeth. Fly down. Tag sticking out of the back of the shirt. Hair sticking up after the bathroom. These are all *informable visible defects*, and the office economy of telling-or-not-telling is a real layer.

### What it does
Generalize the stain mechanic into an `InformableDefectComponent` family. Each defect:
- Is visible to anyone in awareness range with a chance based on proximity, lighting, and observability (face vs back vs feet).
- Decays only on a self-repair action gated by knowledge (the NPC has to be informed OR catch it themselves in a mirror — adding a new low-traffic affordance: bathroom mirrors, the breakroom microwave's reflective door).
- Tracks who knew when.

### Components / data
- New base: `InformableDefectComponent` with subtypes `LipstickOnTeeth`, `OpenFly`, `HairUp`, `TagOut`, `SpinachInTeeth`, `BoogerVisible`, `EarHairProtruding`.

### Extreme end
The Newbie has had a tag sticking out of their shirt for five hours. Eleven NPCs noticed. Nobody told them. The Newbie discovers it at 4:47pm on the way to their car. The trust hit toward the entire floor is small but global. They go home and cry. The office has no idea this happened. The next morning they're cooler to everyone in subtle ways nobody else notices.

---

## 8. The Period

### The human truth
Periods exist in real offices. The bathroom run for supplies, the borrowed tampon, the discreet ask, the specific kind of cramps that interfere with focus, the mood shift, the staining accident, the missed-period scare. A mature office sim cannot pretend these don't happen.

### What it does
- A monthly cycle component on female NPCs (gated by player-soften-toggle for the more visceral aspects).
- Modulates physiological drains (slight energy decrease in the late-luteal phase, slight irritation gain), mood baselines, and the small-but-real `crampDiscomfort` modifier on focus.
- Stocks-and-supplies layer: a `MenstrualSuppliesComponent` on the NPC and a stock-of-supplies in the women's bathroom (named anchor exists). Stock can run low. The discreet ask is a high-trust dialog context.
- Surprise-onset: a low-probability event that produces a `ClothingStainComponent` (visible only from the back) AND requires bathroom + supply-stock + a sweater-tied-around-the-waist intervention.

### Components / data
- New: `MenstrualCycleComponent { CurrentPhase: enum, DaysInCycle: int, CycleLength: int, NextOnsetTick: long }`
- New: `MenstrualSuppliesComponent { Quantity: int }`
- New room object: `SuppliesDispenserComponent` in women's bathroom

### Mechanics
- Borrowing between two female NPCs is a high-trust transfer that bumps `Affection` and seeds a quiet alliance pattern. Asking is a vulnerability event — it costs willpower to ask, less if there's a known `Confidant` relationship.
- Soften-toggle handling per UX bible: visceral aspects can be muted to "stained-clothing" + "tense day" without removing the simulation underneath.

### Extreme end
The Recovering archetype, on a particularly bad cycle day, with willpower already at 18 from a stressful week, snaps at the Newbie over something trivial. The mask collapses publicly. An hour later, in the bathroom, the Recovering is crying. The Newbie is in the next stall, also crying, for unrelated reasons. A vulnerability-bonded conversation happens. The relationship library installs a `Confidant` pattern between two NPCs who had nothing to do with each other before.

---

## 9. The Held Sneeze / Cough / Burp / Yawn (suppression as cost)

### The human truth
Holding it in is real and costs something. The held-in sneeze that almost ruptures something. The held cough that becomes a fit. The held yawn that produces tears. These are micro-suppression events and they all draw a tiny willpower cost, and they all produce a delayed louder-version event when the suppression fails.

### What it does
A unified `SuppressedReleaseComponent` per kind. Each tick, the NPC pays a small willpower cost to hold. When willpower drops below a per-kind threshold OR the NPC enters a low-exposure space, the release fires *louder* than if it had been allowed naturally.

### Components / data
- New: `SuppressedReleaseComponent { Kind: enum, BuiltUpMagnitude: int }`
- Reuse: `WillpowerComponent`

### Mechanics
- Generalized: applies to fart, sneeze, cough, burp, yawn, laugh, scoff, eye-roll.
- The held laugh is the most narratively rich — it produces visible body-shake, often a watering eye, a turning-red face. Other NPCs in awareness range *notice* the suppression (`! ?` mask-slip cue per the UX bible). The shared-suppressed-laugh between two trusted NPCs is one of the quiet bonding moments offices produce.

### Extreme end
The Cynic and Greg are in a horrible all-hands meeting. The exec says something accidentally, beautifully stupid. The Cynic catches Greg's eye. They both register the moment. Both willpowers drop. Both are now actively suppressing a laugh. Greg breaks first — he laugh-coughs into his sleeve. The Cynic loses control too, two seconds later. The exec asks if they're okay. They are not. The relationship between them just upgraded to `Friend`.

---

## 10. Crying (the public version and its aftermath)

### The human truth
Sometimes someone cries at work. Sometimes they make it to the bathroom. Sometimes they don't. The aftermath — the red eyes, the blown nose, the quiet hour after — is observable, and the office's response to it is one of its most calibrated rituals (mostly: pretend it didn't happen unless you're close enough to ask).

### What it does
A `CryingState` enters the NPC when mood Sadness + Stress + Mask-collapse exceed a high threshold. Three phases:
1. **Welling.** Public-visible water-eyed, suppressed. Willpower draining fast.
2. **Crying.** The breakdown. Tag added.
3. **Recovering.** Red-eyed, sniffly, voice hoarse. Lingering visible state for ~1 sim-hour.

The NPC's `bathroom-go` action gets a strong priority bonus during phases 1 and 2.

### Components / data
- New: `CryingComponent { Phase: enum, StartTick: long, ObservedBy: HashSet<NpcId> }`
- Reuse: `MoodComponent`, `StressComponent`, `SocialMaskComponent`, `WillpowerComponent`

### Mechanics
- Phase-1 detection by other NPCs is *partial* — they can read the welling but the crier hasn't spilled yet. High-trust NPCs may approach; others retreat.
- Phase-2 in public is a high-noteworthiness event for everyone in awareness range, persistence-threshold-meeting (per the world bible's stick rule).
- Phase-3 is a delicate window. A check-in by a friend during recovery produces a strong `Affection` and `Trust` bump; a clumsy joke from a low-agreeableness NPC produces a `Rival` seed.

### Extreme end
The Climber, after months of escalating stress, breaks at their desk in front of three witnesses who all have different relationships to them. The witnesses' reactions over the next 24 sim-hours generate a rich social graph: who approached, who avoided, who said the right thing, who said the wrong thing. The Climber's relationship with each of them is permanently re-graded by the encounter.
