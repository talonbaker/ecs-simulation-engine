# 18 — Extreme-End Vignettes

> Five emergent-story sketches. Each one combines several systems from this folder (plus the existing engine) to produce a moment that the simulation generates without authoring. The vignettes are calibrated *high* — the kind of moment a save remembers — but each piece is a routine output of a system that runs all the time.
>
> The point isn't that every save will produce these specific scenes. The point is that the systems are correlated enough that *some* moment of this size is plausible from any given save's combination of NPCs and pressures.

---

## Vignette 1 — The 78-Minute Meeting

**Systems composed:** existing MeetingSystem captivity, existing StressSystem, existing SocialMaskComponent, existing CoffeeSystem, 01.1 CravingSystem, 02.1 FlatulenceSystem, 03.3 PanicAttackSystem, 13.1 AwkwardSilenceSystem, 16.1 FloorCultureCalcification.

It's Wednesday. The all-hands is scheduled for 60 minutes. The HDMI cable doesn't work (per the existing world bible). It runs over.

**At minute 15:** The Climber, mid-presentation, has caffeine deficit climbing. Coffee was finished at 8:30am; it's now nearly 11am. Their next utterance is slightly clipped — calcify-eligible, but nobody notices yet.

**At minute 28:** The Recovering archetype, who's on day 47 of a quit attempt and has been holding mask all morning, notices their own deficit. Their hands are still. Their face is performing engagement. Mask is at 38% and dropping.

**At minute 41:** A different NPC's `ColonComponent` urgency crosses suppression threshold. They have `flatulence` inhibition strength 70 — they will not release here. Suppression willpower draw begins.

**At minute 54:** The HDMI cable is finally fixed by Greg, who has been silently working on it from the back of the room for 22 minutes. He returns to his seat. The presentation resumes. Nobody thanks Greg directly.

**At minute 67:** The Climber's caffeine deficit hits `agitated`. They speed-talk through the next slide. Their language register flips from `formal` to `casual` mid-sentence. Donna notices and remembers it later.

**At minute 71:** The flatulence-suppressing NPC's willpower expires. Audible-2 olfactory-2 release. They sit in panicked stillness. Three NPCs in proximity simultaneously look at their notes. Nobody acknowledges. Mask draws hard for the perpetrator.

**At minute 74:** The Recovering archetype's mask hits 12%. The PanicAttackSystem condition stack is being assembled — high stress, low mask, low willpower, the trigger is the room itself. Onset begins.

**At minute 76:** AwkwardSilenceEvent fires after the Climber finishes a slide and *no one has questions*. 11-second silence. The silence breaks the Recovering archetype's mask further. They stand up. They leave the room without a word.

**At minute 78:** The meeting ends.

**Aftermath:** The Recovering archetype recovers in the bathroom (cluster 02.4 refuge). The flatulence-perp does not look at anyone for 48 hours. The Climber goes for coffee immediately. Greg goes back to the IT closet. The Vent compiles what she just saw and tells the story to two people by end of day. The phrase *"the 78-minute meeting"* enters floor culture (16.1) within a week and is a calcified callback for the rest of the save. Nobody references the specific incidents — only the *length*.

The chronicle records the meeting. The chain of small failures is not authored. It's all gates and accumulators that crossed thresholds within the same 78 minutes.

---

## Vignette 2 — The Friday Quit-Attempt

**Systems composed:** 01.1 CravingSystem, 01.5 QuitAttemptArc, existing ThresholdSpaceSystem (smoking bench), existing SocialMaskComponent, 09.1 HomeStateBleed, 12.4 SubstanceAdjacencyExposure, 18 (existing — threshold spaces).

The Recovering archetype is on quit-day 47. The week has been the worst of the quarter — workload spiked Tuesday, the divorce arc one of her closest coworkers is in (cluster 9.3) ate her emotional reserve, the Climber gave her a small public dressing-down at Wednesday's meeting that's still riding her irritation accumulator.

**Friday 4:30pm.** Mask is at 14%. Nicotine deficit is at `agitated` and has been for two hours. The morning's home-state bleed (a fight with her sister on the phone before leaving the house) has not decayed. Her HomeStateComponent's `currentDomesticTension` is the highest it's been in weeks. WorkloadComponent says she has 30 minutes of work left.

**Friday 4:43pm.** She walks out of the building toward the parking lot. The smoking bench is in her path. Two coworkers are on it — the Cynic and one peer.

**Friday 4:45pm.** She stops at the edge of the parking lot. She watches the bench. Her ExposureEvent counters fire. Her craving deficit is at maximum. Her mask is at 6%.

**She has three options the simulation tracks:**

- Walk to her car and drive home. (Does nothing acute. Goes home to the empty house. Tomorrow is Saturday. Recovery time available.)
- Sit on the bench and ask for a cigarette. (Relapse. The arc resets. The chronicle records a noteworthy fall.)
- Sit on the bench and don't ask. (Test. Hold the line in the hardest possible context.)

**The simulation does not script the outcome.** It runs the action-selection on her current drives, willpower, inhibitions, mask, and personality, with the situation-stake set high. The probability distribution favors *walk to her car* under most parameter values, but one of the other two will sometimes win.

**If she sits on the bench and doesn't ask:** The Cynic looks at her. He can see her mask is at zero. He doesn't say anything. He hands her his unlit cigarette without comment, takes it back when she doesn't reach for it, and they sit in silence for four minutes. The chronicle records *the Friday Linda almost smoked.* Her relationship matrix toward the Cynic permanently shifts. The Cynic says nothing about it ever.

**If she sits and asks:** The chronicle records the relapse. Her arc resets to day 1. By Monday morning her hygiene drift has begun to show. The floor reads it without anyone naming it.

**If she walks to her car:** Nothing visible. Tuesday she's at work as if nothing happened. The chronicle records nothing. The simulation knows what she chose.

The shape is the same in all three branches: a Friday afternoon, a parking lot edge, a moment generated by the entire week's worth of state crossing thresholds simultaneously.

---

## Vignette 3 — The Newbie's First Period at the Office

**Systems composed:** 02.5 MenstrualCycleSystem, 02.4 BathroomZoneSystem, existing FavorLedgerComponent, 06.6 SocioeconomicQuiet, 01.5 belonging arc.

Day 12. The Newbie is six weeks in. She's just finished a presentation in the conference room. Severity rolled severe — the simulation determined this at her spawn. She did not anticipate the onset. The sweater she was going to use as cover is at her desk.

**Minute 1 after the meeting:** She's standing up. Cramping spike. She doesn't have supplies. Her bag is at her desk. She has not built the trust-network yet to ask anyone here.

**Minute 2:** She walks toward the bathroom (per existing pathing + cluster 02.4 refuge). Her movement speed is reduced from cramping (per existing AppearanceComponent posture mod). The Vent (Donna) is at her own desk and notices.

**Minute 3:** The Newbie is in the bathroom. Her stall has nothing. She is alone. Her mask is at 8%.

**Minute 4:** Donna enters the bathroom. She doesn't say anything. She knocks twice on the Newbie's stall and slides supplies under the door.

**Minute 5:** The Newbie's `affection` and `belonging` toward Donna jumps thirty points. She finishes in the bathroom. When she emerges, Donna is at the sink reapplying lipstick in the mirror, looking at nothing in particular. They make brief eye contact. Donna says *"happens to all of us, hon"* and leaves.

**The chronicle records:** Minute 4, two simultaneous events — Donna's intervention and the Newbie's belonging spike. The supplies-borrowing event also writes a small entry on the FavorLedger; the Newbie now owes Donna an unspecified small thing. They both know.

**Long-tail effect:** From this day forward the Newbie's relationship pattern with Donna shifts from `colleague` to `confidant` (cast bible relationship-pattern library). Six months later, when Donna is having her bad week (cluster 09.7 — Donna's mother's deathiversary), the Newbie will be the one who quietly puts a coffee on Donna's desk without explanation. Neither will reference the bathroom day. The pattern was set at minute 4.

This vignette is generated by exactly two events firing in proximity: a `MenstrualEmergency` event and a `Donna-was-paying-attention` proximity event. Everything that follows is downstream.

---

## Vignette 4 — The Conference Room Cockroach

**Systems composed:** 03.1 PestEntitySystem, existing SocialMaskComponent, existing KnowledgeComponent, 11.1 GatekeeperPositionSystem, 16.1 FloorCultureCalcification, 16.7 FolkloreLayer.

Tuesday morning. The all-hands is starting in the conference room. The CEO is visiting from the parent corp.

**Minute 1:** The Climber is standing at the front, getting ready to present. The CEO is at the head of the table. Twelve NPCs are seated.

**Minute 2:** A cockroach (specifically a wolf-spider-class fearScore-high pest, spawned hours ago in the supply closet, has migrated through the wall) emerges from under the conference-room table. The first NPC to see it is Donna.

**Minute 2.4:** Donna's reaction calculation runs. Her phobiaMultiplier on `cockroach` is 2.5 (mild). Her agreeableness gates her toward verbalAlert. Her result: a small audible exclamation. The CEO turns. Twelve NPCs orient.

**Minute 2.5:** Several NPCs run their reaction calculations simultaneously. Three flinch. The Climber freezes — they have a hidden phobia of pests at strength 60. Their mask draws hard. The CEO's reaction is calm, neutral.

**Minute 2.7:** The handler-NPC selection runs. Greg, in the back of the room (he's there to fix the HDMI before the meeting started), has the highest handlerWillingness — high agreeableness, low phobia, currently not in flow. He stands up.

**Minute 2.9:** Greg walks around the table, unhurried. He kneels. He scoops the cockroach into his coffee cup. He walks it to the door, opens it, releases it into the hallway, closes the door. He returns to his seat. The whole sequence takes 18 sim-seconds.

**Minute 3:** The Climber, who has not yet recovered, is still mid-introduction-slide. The CEO speaks into the silence: *"Well. Shall we?"* The Climber nods, attempts the opening slide, recovers their professional register over about 20 sim-seconds.

**Aftermath:**

- The chronicle records *the cockroach incident* with noteworthiness 95.
- Greg's `status` drive bumps in every NPC's KnowledgeComponent who saw it (~12 entries).
- The Climber's `mask` baseline takes a small permanent hit — they were caught publicly frozen, even briefly.
- Donna tells the story to four NPCs by end of day, with appropriate dramatic embellishment.
- Within two weeks, the phrase *"Greg saved the all-hands"* enters floor culture (16.1).
- Within two months, the phrase calcifies into a permanent FloorCultureItem.
- Within two years, when a new hire is being told the office's lore, *the cockroach story* is one of the first told. By then it's been embellished into folklore (16.7).
- Greg himself never references it. He didn't think he did anything notable.
- The Climber, every time they have to present in that conference room thereafter, has a small `irritation` accumulator activation associated with the room. It's calcified anchor charge (per the world bible's Cubicle 12 mechanic).

This entire sequence, from pest spawn to permanent folklore, is generated by individual systems running their own logic. Nothing about the cockroach was scripted into a save.

---

## Vignette 5 — The Long Goodbye

**Systems composed:** 10.6 RetirementArc, 16.1 FloorCultureCalcification, 16.8 CulturalDriftFromTurnover, existing GoingAwayEvents (cluster 10.2), existing FavorLedgerComponent, 04.5 PlantCareDrama.

Year three of a save. The Old Hand has decided to retire. They told leadership six months ago. The floor learned three months ago. The retirement date is Friday.

**The week before:** The pace of warm interaction with the Old Hand visibly increases. Coworkers who've been tangential to him for years are stopping by. The Vent has organized the going-away party. The card has been circulating for two weeks; signatures are paragraph-long.

**Wednesday afternoon:** The Old Hand quietly transfers his maintenance of the breakroom plant (per cluster 04.5) to the Newbie. He doesn't make a big deal of it — he stops by her desk, says *"Could you make sure that gets watered Mondays. It's been doing OK."* and walks back to his desk. The Newbie's `affection` toward him is permanently elevated. She doesn't know what just happened, but it lands.

**Thursday morning:** Greg, of all people, brings the Old Hand a coffee. They sit together for ten minutes in the IT closet, mostly silent. The chronicle records the visit; it's noteworthy because Greg almost never invites anyone in.

**Thursday afternoon:** The Climber asks the Old Hand a substantive work question — not strategic, just real. The Old Hand answers in detail, sharing knowledge it took him 25 years to accumulate. The Climber's KnowledgeComponent records the entire transfer. Later that night the Climber writes notes from memory, the only time they've done that voluntarily in the save.

**Friday — going-away party:** Conference room. Cake. Speech. The Vent's speech is 60% calcified-floor-culture references — *"and remember when you fixed the printer that day in 2003 and..."* — that the room laughs at. Newer hires don't get most of the references. The Old Hand is moved.

**Friday 4:55pm:** The Old Hand cleans his desk. He takes the lava lamp that's been there for sixteen years. He leaves the framed photo for whoever sits there next. He hugs Donna (cluster 5.2 — touch event). He shakes Greg's hand longer than typical. He nods at the Climber. He walks out.

**Monday:** The desk is empty. The Newbie waters the plant.

**Six months later:**

- The plant is dying. The Newbie has been forgetting (cluster 04.5).
- The Old Hand's tics are no longer producing dialog calcify hits, but his calcified phrases are *still being used* by NPCs who picked them up from him. Several of his recognized tics are now floor culture.
- The empty desk has accumulated `NamedAnchorEmotionalCharge` (per the world-bible Cubicle 12 mechanic) — adjacent NPCs receive a slow `loneliness` drift.
- Greg has been quieter than usual. The chronicle hasn't recorded anything specific.
- The Climber has been making decisions that the Old Hand would have advised against. Some of them are working out. Some aren't.

**One year later:**

- The plant is dead. Nobody replaced it.
- A new hire arrives. The Vent tells them about the Old Hand on day three. By day ten, the new hire is using a phrase the Old Hand calcified six years ago.
- The desk is filled with someone else, but Donna still calls it *"Frank's desk"* sometimes by mistake.
- The cultural drift (cluster 16.8) is real and measurable. The save's office is *different* than it was a year ago. Some references have died; some persist.

The retirement was scheduled. Nothing else here was. The save's office has changed, slowly, in ways that came from the systems running.

---

## Why these vignettes matter

None of these were authored. Each one is a confluence of small mechanical systems whose interactions produce the moment. The simulation's job is to make these moments *plausible* under the right combination of state. Each save will produce its own version — different cast, different timing, different outcomes — but moments of this scale and shape should be generated by simulation pressure alone.

The brief asked for *outside-the-box* and *outside the typical*. The vignettes are the proof-of-concept that the systems in this folder, taken together, can produce the kind of moment a player remembers months after the save ended.
