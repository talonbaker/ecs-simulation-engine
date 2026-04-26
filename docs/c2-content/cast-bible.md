# Cast Bible — Generator Pattern (Working Draft)

> Co-authored by Talon and Opus. Defines how characters are *made*, not who they are.

---

## What this is

The cast is not a fixed roster of hand-authored people. The cast is a **generator system** — a small set of archetype templates, a personality dimension space, a drive baseline catalog, a relationship-pattern library, a silhouette pool — that combine to produce coherent characters on demand.

We need to be able to make Donna. We also need to be able to make a new HR person when Donna gets fired, a fifteenth NPC the player just hired, or a one-off temp who's around for two weeks and then vanishes. The goal is that any character produced by this system feels real and specific in the same way Donna does — because the *system* is what produces specificity, not pre-authoring.

---

## Voice via emergence, not pre-authored catchphrases

This is critical. **Catchphrases and tics are not authored in advance.** They emerge from gameplay:

- An NPC starts with a personality (Big Five), drive baselines, and a vocabulary register (formal / casual / crass / clipped / academic). They have *no* canned utterances.
- During play, when an NPC produces an emotive response — frustration, joy, disgust, intrigue — the dialogue subsystem records what they said and the situation that produced it. After enough repetitions of similar emotional situations, recurring phrasings calcify into that NPC's voice.
- The game tracks *"this NPC has said variants of X enough times that X is now part of who they are."* That phrase becomes their tic. Other NPCs can recognize it. The player can recognize it.
- A fresh NPC has a flat voice for the first hour or two of game time, then **develops** one organically. Like meeting someone in real life: at first they're a polite blank; over weeks you learn their phrasings.

### The Sims principle: emote more than speak

Most communication is body language, position, observable mood, and observable consequence — like a silent film viewed from across the room. Spoken language is decorative; **emotional state and visible action is the content.**

The dialogue subsystem's job is not to generate clever lines. It is to generate *interactions where feelings get exchanged.* Most exchanges are passive and forgettable. A small fraction land — offend, intrigue, hurt, charm. The game's interesting moments are the landings, and the landings are detected by deltas in social-drive state and relationship state, not by line quality.

---

## Archetype templates

An archetype is a pattern, not a person. Each archetype carries:

- A role (HR, IT, exec, mid-tier, shipping, intern, etc.)
- A defining trait (the one thing that's always true)
- A personality range (a region in Big Five space, not a fixed point)
- Three or four chronically-elevated drives
- A vocabulary register
- A schedule shape (when they're at desk, when they wander, when they break)
- A silhouette family (see silhouette catalog)
- One "deal" — an unusual personal thing (see deal catalog)
- Common starting-relationship patterns

When the engine spawns a character from an archetype, it picks specific values from each range, applies them to the social-state DTOs, and the character is born. Every spawn is unique. Every spawn feels like that archetype.

### Initial archetype catalog

(Open to revision — these are starting points for the generator. New archetypes are easy to add; the cost of a new archetype is one entry in this list and a few ranges of values.)

- **The Vent** — treats every conversation as a chance to offload. High extraversion, high neuroticism. *Belonging* and *irritation* drives chronically elevated. Vocabulary register: casual-direct, sometimes crass. Common roles: HR, mid-tier admin. *Donna fits this.*
- **The Hermit** — retreats from people; communicates only when extracted. Low extraversion, high openness, low agreeableness. *Loneliness* drive elevated but they don't want company; *trust* drive low. Common roles: IT, archive, lone specialist. *Greg fits this.*
- **The Climber** — performs ambition. *Status* drive maxed. High conscientiousness; agreeableness deceptively high. Will sleep with whoever advances them. Common roles: mid-tier on the exec track.
- **The Cynic** — worked here too long; done caring. Speaks plainly. Low across most drives. The character who tells the player how it actually is. Any role; tenure does the work.
- **The Newbie** — knows nothing, works hard, is easy to break. High agreeableness, low conscientiousness from inexperience. Drive baselines erratic. Entry-level.
- **The Old Hand** — has seen everything. Mostly stable. Will absorb shock without much movement. Might surprise everyone in a crisis. Any long-tenured role.
- **The Affair** — has something going on with someone they shouldn't. *Attraction* drive elevated toward a specific other NPC; high neuroticism; their relationship to spouse (off-screen or implied) is strained.
- **The Recovering** — in recovery from something. Divorce, addiction, grief, public failure. Stable on the surface; volatile under pressure. Drive states are masked from other NPCs at first. Any role.
- **The Founder's Nephew** — knows they can't be fired. Performs at 40%. Other NPCs know. Status weirdly inverted — low respect, high job security.
- **The Crush** — is liked by someone who hasn't said anything. Doesn't know yet. Their drive state is normal; *another NPC's* drive state is modulated by them.

**Archetypes compose.** An NPC can be The Climber AND The Affair, with the affair being part of the climbing strategy. Layered archetypes produce more interesting people. The generator should support 1–2 archetypes per NPC; three is over-determined.

---

## Personality dimension space

Big Five, each on −2 to +2:

- **Openness** — curiosity vs convention. High openness ventures; low openness sticks.
- **Conscientiousness** — organized vs scattered. High follows through; low does not.
- **Extraversion** — energized by people vs by solitude. High seeks; low avoids.
- **Agreeableness** — trusts and gives benefit of doubt vs suspicious and combative. High is pleasant; low is friction.
- **Neuroticism** — emotional volatility. High spikes easily; low is stable.

These are deliberately broad. Two −2-Conscientiousness characters can still be very different humans because their other four dimensions and their archetype layers differ.

---

## Social drive catalog

The eight social drives (v0.2 schema). Each on 0–100, with a baseline per-archetype that drifts during play:

- **Belonging** — want to be part of the group.
- **Status** — want to be respected, ranked.
- **Affection** — want to be loved, romantically or platonically.
- **Irritation** — want to express annoyance.
- **Attraction** — toward a specific other NPC.
- **Trust** — toward a specific other NPC.
- **Suspicion** — toward a specific other NPC.
- **Loneliness** — opposite of belonging-fulfilled.
- **Exposure** *(proposed ninth drive — open question)* — the cost of being seen while internally raw. Rises when an NPC is in a high-visibility, high-traffic area while running an emotionally elevated internal state. Differs from `loneliness` (too little contact) and `irritation` (directed at a cause) — exposure is about the effort of appearing composed when you aren't. High-neuroticism and low-extraversion archetypes are more sensitive to it; The Hermit, The Recovering, and The Affair archetype would have it as a chronically elevated baseline drive.

An archetype specifies which of these are *chronically elevated* (baseline higher than population mean). This shapes behavior even before any specific events have happened.

---

## Vocabulary register

Each NPC has a register. The register constrains what their language sounds like (when language is produced), without prescribing specific lines:

- **Formal** — full sentences, low contraction, no slang.
- **Casual** — contracted, slang-tolerant, profanity occasional.
- **Crass** — profanity normal, sexual references casual, body humor available.
- **Clipped** — short phrases, low engagement, often ends conversations.
- **Academic** — references and qualifiers, "actually" common, can feel condescending.
- **Folksy** — sayings, indirect, warm.

Register is a property the dialogue subsystem reads when generating a turn. It's not a fixed line-set.

---

## Relationship-pattern library

Relationships instantiate from a small catalog of patterns. Each pattern has typed parameters and a visible signature in behavior:

- **Rival** — competitive history, low cooperation, public.
- **Old flame** — past romantic involvement, complicated present, mostly hidden.
- **Active affair** — current romantic involvement, hidden, high-stakes.
- **Secret crush** — one-sided attraction; the other doesn't know.
- **Mentor / mentee** — asymmetric respect, asymmetric vulnerability.
- **Boss / report** — formal asymmetry; can be warm or cold.
- **Friend** — mutual positive regard, no power asymmetry.
- **Allies of convenience** — cooperate without warmth.
- **Slept with their spouse** — the other doesn't know yet, or does and hasn't said anything.
- **Confidant** — knows something compromising about the other.
- **The thing nobody talks about** — both remember, neither references.

Every NPC pair has zero, one, or two patterns active. Patterns can transition (a Rival can become an Old Flame can become Slept-With-Their-Spouse over the arc of a save). Pattern transitions are the *plot* the game generates.

---

## Silhouette catalog

Pixel-art-distant readability is the constraint. A player should learn to recognize an NPC by silhouette within an hour. Catalog (open-ended; additive):

- **Heights:** short / average / tall.
- **Build:** slight / average / stocky.
- **Hair:** bald / short / medium / long / distinctive (mohawk, ponytail, perm, etc.).
- **Headwear:** none / hat / cap / glasses / glasses+cap.
- **Distinctive item:** clipboard / coffee mug / phone-glued-to-ear / cigarette / lanyard / always-carrying-a-folder / etc.
- **Dominant color:** a single dominant clothing color per NPC at game start, persistent for the save (the "blue shirt guy" effect).

An archetype suggests a silhouette family but doesn't lock one in. Donna doesn't have to be tall; Donna has to be *recognizable*.

---

## Deal catalog (the unusual thing)

Every NPC has one **deal** — a specific personal weirdness that's not encoded in archetype or drives but in their individual record. The deal is mostly invisible until it surfaces in gameplay. When it does, it's a moment.

Examples:

- Always eats lunch in their car.
- Never washes their coffee mug.
- Is secretly writing a novel.
- Owns a parrot.
- Is dating someone at a competing company.
- Has a child nobody at work knows about.
- Sleeps in their car some nights.
- Is in significant debt.
- Used to play in a band.
- Is in recovery.
- Has been working on the same project for two years and nobody has asked.
- Brings homemade cookies on Fridays without saying why.

This list is a starter; deals are easy to add and the deeper the pool the better.

---

## Starting-relationship sketch (for the initial cast spawn)

When the engine boots an office, it instantiates ~15 NPCs. Each NPC gets one archetype (sometimes two, layered). The relationship matrix is then seeded:

- Most pairs get *no* pattern by default. Most office relationships are neutral.
- For each archetype that includes a relational pattern (The Affair, The Crush), spawn the relevant relationship.
- Roll a few additional relationships from the pattern library to seed dynamics — say, two rivalries, one old flame, one mentor/mentee, one slept-with-their-spouse. Random but seeded for replay determinism.
- Tag a couple of relationships as "the thing nobody talks about" for atmosphere.

The starting state should feel like a real office: most people are pleasantly neutral with most other people, a few hate each other, a few are secretly involved, and a few have a history the rest of the office doesn't know about.

**Then play happens.**

---

## What's deliberately not committed

- Specific lines of dialogue per NPC. (Voice emerges; see top of doc.)
- Specific story arcs per NPC. (Plot generates from pattern transitions.)
- Specific facial features. (See aesthetic-bible — emotion reads through silhouette and motion, not faces.)
- Hand-authored characters by name. The generator is the cast.

---

## Open questions for revision

- Should archetype layering be capped at two, or allow three? Two reads cleaner; three may produce more interesting outliers.
- Vocabulary registers above are a starting set. Does anything you imagined for the office not fit one? (Example: a character who *sometimes* swears and sometimes doesn't because they're code-switching — that's a separate axis we haven't modelled. Worth adding?)
- Deal catalog is short. Add more as you think of office-people you've known.
- The silhouette catalog dimensions look reasonable for pixel art at distance — but if the eventual visualizer pushes us toward higher fidelity, this list grows.
- Masking as a system cost: the Recovering and Affair archetypes currently mask internal drive states as a narrative trait. Should masking generalize? Every NPC has some gap between their internal drive state and what they project socially; maintaining that gap draws from willpower (per the action-gating schema). When willpower runs low in a high-exposure space, drives surface visibly in behavior — the mask slips. This is not currently described as a simulation event. A `maskStrength` parameter per NPC (or derived from neuroticism + willpower reserve) could produce it without a new system, and the slip would register as a high-noteworthiness social event.
