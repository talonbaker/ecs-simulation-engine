# 07 — Sensory Environment and Misophonia

> Specific sounds, smells, and sights are unbearable to specific people. Nobody can explain why their coworker's chewing makes them want to scream. The office produces these stimuli constantly and the affected NPCs are paying composure tax all day.

---

## What's already in the engine that this builds on

- `NoiseSystem`, `OdorComponent`, `AcousticEnvironmentComponent`.
- The aesthetic-bible's lighting layer including flicker.
- `irritation`, `mask`, `flow`.
- `KnowledgeComponent` for shared facts about who-does-what.

---

## 7.1 — MisophoniaProfileSystem

### The human truth

Specific sounds — chewing, lip smacking, gum snapping, knuckle cracking, pen clicking, throat clearing, sniffing, keyboard typing intensity — produce a disproportionate, near-rage response in the affected listener. Misophonia is real, common, and almost never named in the office. The sufferer hides the response because they know it's "weird." The willpower draw of suppressing the response is enormous when the trigger is sustained.

### What it does

Each NPC has a `MisophoniaProfile` with 0–3 triggers. Each trigger:

- `kind` — `chewing | lipSmack | gumSnap | knuckleCrack | penClick | throatClear | sniff | keyboardSlam | nailFile | foodWrapperCrinkle | pageShuffle | breath`.
- `severity` — 0–100. At 30 it's a mild irritation; at 70 it becomes hard to focus; at 90 the listener cannot remain in the room.

When a trigger source is in awareness range and producing the trigger, the affected listener's:

- `irritation` ticks at multiplied rate.
- `flow` cannot enter; if currently in flow, breaks.
- `mask` draws continuously while the trigger is active.
- A small `directedSuspicion` toward the source accumulates — irrational, but real. Over weeks, the listener has a calcified low-trust toward the NPC making the sound, *not* understanding that it's about the sound.

The source NPC almost never knows. They're chewing the way they chew. Their mug is on their desk. The sound is theirs.

### The asymmetry

This is a one-way thing. Source has no idea. Listener can't articulate it. Office norms make it almost impossible to bring up. The simulation lets the asymmetry persist for the duration of the save.

### Cross-system interactions

- **PassiveAggression:** the rare passive-aggressive note about sound (cluster 02.6 indirect — *please chew with your mouth closed*). Almost never written; almost never unmistakable.
- **HeadphoneUseSystem (cluster 07.4):** the listener's existing strategy is headphones. The headphone gives them respite at the cost of not hearing meeting calls and not noticing approaches.
- **TerritorySystem:** listeners eventually request seating moves "to be closer to the project I'm on." The real reason is the trigger.

### Personality + archetype gates

- High `neuroticism` correlates with stronger trigger response.
- The Hermit has the most-severe misophonia in the building. The IT closet is partially a haven.
- The Newbie sometimes *develops* misophonia mid-save in response to a specific repeated stimulus.

### What the player sees

An NPC who keeps glancing toward another NPC's desk while doing nothing visible about it. Headphones going on at consistent times. A request to move that the player can grant or deny. The chronicle eventually records when the affected NPC snaps.

### The extreme end

The Climber sits next to a colleague who chews with their mouth open. Severity 90. Six months of save time pass. The Climber's calcified suspicion toward the chewer is a primary input to a backstabbing arc that the Climber pursues at year-end. The Climber genuinely believes the chewer is incompetent and untrustworthy. The chewer never did anything wrong; the Climber's brain rebuilt the relationship in their head from the sound.

---

## 7.2 — OlfactorySensitivityProfile (cologne, perfume, body products)

### The human truth

Some people can't be near other people's cologne. The over-applied cologne is a real stimulus. The signature perfume that lingers in the elevator. The body wash that smells like a diner. The shampoo that nobody else can smell except this one coworker who *cannot*. Migraine triggers, allergy triggers, headache triggers — and almost universally socially unaddressable.

### What it does

Extends the existing `OdorComponent` with a `SensitivityProfile` per NPC:

- `triggerScents[]` — `colognes | perfumes | scented-soap | scented-detergent | smoke-residue | hairspray | scentedCandle | airFreshener`.
- `severity` — 0–100. High severity triggers `headache` tag with measurable productivity drag.

NPCs *carrying* an applied scent broadcast a small radius `OdorComponent`. Affected NPCs in range pay irritation and possibly a focus-disruption tax.

### Cross-system interactions

- **Elevator scenes (cluster 5.5):** confined-space scent encounters are the worst. A few minutes is enough to ruin the affected NPC's morning.
- **HVAC:** the ThermalComfortSystem's `VentilationQuality` modulates how long applied scent lingers in a room. Poor ventilation = the cologne cloud persists for the rest of the meeting.

### Personality + archetype gates

- The Vent (Donna) wears strong perfume daily. She's been doing it for twenty years. Three coworkers have suffered silently the whole time.
- The Hermit wears nothing. The smell of him is *the absence of product*.
- The Climber's cologne is calculated — applied light, expensive, signal-grade.
- The Founder's Nephew wears too much.

### What the player sees

An NPC who avoids elevators when a specific other NPC is around. An NPC who keeps their door closed when another NPC is in for a meeting. Headache events in the chronicle that correlate with proximity to specific others.

### The extreme end

Open-floor team meeting. Donna's perfume wave. One affected NPC has migraines. They sit at the far end of the table and *still* the smell reaches. Mid-meeting, they have to leave for a sip of water and to clear the air. They don't come back. The meeting concludes without them. Donna doesn't know. The affected NPC takes Aleve at their desk and works for two hours at degraded productivity. The chronicle records "Sandra had another migraine day" in their KnowledgeComponent for those who notice.

---

## 7.3 — VisualSensitivityProfile (screen brightness, fluorescent flicker)

### The human truth

Specific lighting kills specific people. The flickering fluorescent is the famous one (already in the aesthetic bible). The over-bright window in the corner office. The screensaver across the floor that's a strobing gradient. Some NPCs cannot work near a particular light condition; they don't always know that's why their day is bad.

### What it does

`VisualSensitivityProfile`:

- `flickerSensitivity` — 0–100. Modulates the existing aesthetic-bible's flickering-fluorescent irritation impact.
- `glareIntolerance` — 0–100. Strong sun-window NPCs in direct beams accumulate fatigue.
- `screenSensitivity` — applies to coworkers' bright/animated screens in their visual field.

A mismatch between sensitivity and environment is a daily mask draw. The NPC's solutions are limited: angle a monitor, request relocation, bring a desk lamp, wear a hat indoors (a recognized character beat).

### Cross-system interactions

- **Lighting:** the existing aesthetic-bible system already commits to the visible flicker. This adds the per-NPC sensitivity.
- **TerritorySystem:** the desk under the bad fluorescent is *not* a popular desk. NPCs assigned to it pay a continuous tax.

### What the player sees

Some NPCs always have hats on indoors. Some have polarized sunglasses. The player can see who's affected by the building.

### The extreme end

The Newbie is assigned the desk under the chronically flickering light. They don't know it's a problem. After three weeks they have headaches every afternoon. Their work output is 40% below baseline. They think they're failing at the job. The Old Hand notices and quietly trades desks with them. The Newbie doesn't understand what just happened; their headaches stop; their output recovers. They never connect the desk to the difference. The Old Hand suffers no headaches because the Old Hand stopped noticing the flicker years ago.

---

## 7.4 — HeadphoneCultureSystem

### The human truth

Headphones are a social technology. They mean *I am not available right now*. They mean *my music is part of my mood*. They mean *I don't want to hear you*. The headphone-on signal is read by the floor as do-not-interrupt; some respect it; some override it. The headphone-removal-when-approached is an entire little ritual.

### What it does

Each NPC has a `HeadphoneState` (`off | on | partial`). The state is set by the NPC based on:

- Active task (high-focus task → on).
- Misophonia trigger active (on, immediately).
- Mood (low mood + extraversion-low → on as defense).
- Music as ritual (some NPCs always have headphones; it's their style).

Other NPCs respond differently:

- **Respect** — high-agreeableness NPCs don't approach; they email instead.
- **Override** — low-agreeableness NPCs approach anyway, expect the headphones-off response.
- **Wave** — soft override; signal at the edge of the cube.

### Cross-system interactions

- **Flow:** headphone-on enables flow entry by raising the ambient noise floor's *psychoacoustic* threshold even if actual `AcousticEnvironment` is loud.
- **Hover events (cluster 5.3):** headphones-on NPCs are the most-vulnerable to hover events because they don't hear the approach.
- **Misophonia:** headphones are the listener's rescue. The willpower-saving effect of *headphones on* is real and measurable in the simulation.

### Personality + archetype gates

- The Hermit is headphones-on most of the day. They're not even playing music half the time.
- The Climber refuses headphones — looks unprofessional. They suffer the noise and pay willpower.
- The Newbie watches who else has headphones on and decides whether they're allowed yet.
- The Cynic has headphones on as contempt-broadcast: *I have decided you all are not worth my attention right now*.

### What the player sees

A floor where roughly a third of NPCs are usually wearing headphones. The pattern shifts with mood and task. Reading the headphones is reading the floor.

### The extreme end

The Newbie's first month, no headphones (they don't think they're allowed). By month two they tentatively try them on. Nothing bad happens. By month three they're on by default. Their flow time tripled. Their work output visibly improved. The Climber walks past, sees the headphones, files it as *this person isn't engaged*. The Newbie doesn't know they're now in the Climber's bad books. The misperception bleeds into their performance review months later.

---

## 7.5 — Soundleak from headphones / leaking music

### The human truth

The reverse problem: someone's headphones are leaking. You can hear their music from the next desk. They don't know. You can't tell them. The music is bad. The music is good but loud. The music is something you'd be embarrassed to be heard listening to. The leak is a small humiliating broadcast the wearer didn't authorize.

### What it does

Each `HeadphoneState: on` NPC has a `volumeLevel` (0–100). High volumes generate a `LeakingMusicEvent` that propagates as a low-intensity noise event to adjacent NPCs.

Adjacent NPCs accumulate small irritation. Eventually one of:

- Mentions it (rare).
- Writes a note (passive-aggressive).
- Reads the leaking content as identity data (the country fan; the metalhead; the audiobook listener).

### Cross-system interactions

- **MisophoniaProfileSystem:** for sufferers of any music or specific genres, this is unbearable.
- **KnowledgeComponent:** the leaked content is a small calcified identity fact. ("The new IT guy listens to country, who knew.")

### What the player sees

A small revelation about a quiet NPC. The Hermit leaking *something unexpected* through his headphones is the kind of moment that humanizes him for the floor.

### The extreme end

The Hermit, who never speaks unnecessarily, has a headphone leak event during a quiet afternoon. The leak is *opera*. The Vent across the floor hears it. She looks up. She doesn't say anything. She files it. Two days later in casual conversation she mentions opera tickets. The Hermit looks at her differently. Their relationship pattern shifts a small amount.

---

## 7.6 — SyncopatedNoiseInterference

### The human truth

Two competing rhythmic noises in proximity is a special kind of agony. The pen clicker and the leg jiggler. The two phones ringing at slightly different intervals. The HVAC click and the printer churn. Two pieces of background noise become one piece of foreground noise the moment they're not in sync.

### What it does

The existing `AcousticEnvironmentComponent` is extended with a `RhythmCorrelation` calculation across active sources. When two rhythmic sources have *near*-but-not-aligned cadence, an extra `irritation` multiplier applies to all NPCs in range.

### What the player sees

A specific bad-day-on-the-floor where the pen-clicker and the leg-jiggler are sitting next to each other and the entire row is paying composure tax for nothing they can name.

---

## 7.7 — TextureAndTactileSensitivity

### The human truth

The chair upholstery is wrong. The pen feels weird. The carpet is scratchy. The keyboard is too loud-and-clicky for the typer. Some NPCs are tactile-sensitive in ways that gradually wear them down. Almost never spoken about; constantly affecting mood.

### What it does

`TactileProfile`:

- `chairTolerance` — modulates a small daily mask draw if the chair is wrong.
- `materialPreferences` — affects clothing changes, attire choices.
- `pressureSensitivity` — modulates response to tight clothing, bra-pinch (a real thing, daily, for many NPCs), too-tight watchband.

This is one of the lowest-amplitude systems in the folder. Most of the time it's noise. Occasionally it's the *thing* that finally pushed someone over.

### What the player sees

Mostly nothing. Occasionally an NPC who couldn't say what was wrong with their day, but everything was just *off* — the simulation actually has a reason in the form of accumulated small tactile mismatches.

---

## 7.8 — SmellMemory (the Proust effect)

### The human truth

A specific smell carries a specific memory. The cleaning chemical that smells like the hospital from the year someone's parent died. The cologne that smells like an ex. The pine-wood smell that smells like a specific summer. NPCs encountering these smells at work get a small unauthorized emotional spike that they cannot parse in real time.

### What it does

`SmellMemoryComponent` per NPC carries 0–3 specific scent-memory bindings:

- `scent` — references an `OdorType`.
- `valence` — positive or negative.
- `magnitude` — drive impact when triggered.

When the matched scent is detected in proximity, the NPC's mood and drive state shift involuntarily. They cannot articulate why. Coworkers can't see the cause. The afternoon goes off in a way that nobody on the floor understands.

### What the player sees

An NPC whose mood broke for no apparent reason. The player, with debug visibility, can read the smell-memory match. The NPC themselves doesn't know.

### The extreme end

The Recovering archetype has a smell-memory binding to a specific cleaning chemical from a hospital where they spent a month. The cleaning crew uses that brand on Tuesdays. Their Tuesday afternoons are inexplicably worse than their Monday afternoons. Six months into the save, the Vent notices the pattern (high-empathy archetype) and begins quietly running interference on Tuesdays — small acts of warmth, an extra coffee, a check-in. The Recovering archetype doesn't know what's helping; they just know Tuesdays are bearable.
