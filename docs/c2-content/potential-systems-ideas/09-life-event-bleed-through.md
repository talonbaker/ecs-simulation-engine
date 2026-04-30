# 09 — Life-Event Bleed-Through

> The office isn't sealed. The thing that happened at home arrives through the lobby door at 8:47 every morning. The thing that's about to happen at home casts a shadow forward across the day. What gets simulated isn't the home life — it's the *residue.*

---

## What's already in the engine that this builds on

- The threshold-space system from `new-systems-ideas.md` (parking lot, smoking bench, car) — the bleed-in/bleed-out anchors.
- `StressComponent.ChronicLevel`, `SocialMaskComponent`, `willpower`.
- `KnowledgeComponent`, `RumorSystem`, `FavorLedgerComponent`.
- The cast bible's deal catalog (already includes "sleeps in their car some nights," "is in significant debt," "has a child nobody at work knows about").

---

## 9.1 — DomesticStateBleedSystem

### The human truth

Every NPC arrives each morning carrying a state from home that wasn't produced at the office. The argument they had at breakfast. The kid who threw up overnight. The bill that came in the mail. The fight that didn't get resolved before bed. The job application they sent last night and the silence about it. These don't get talked about; they get *carried.*

### What it does

Each NPC has a `HomeStateComponent` that runs *off-screen* between sessions and updates each morning's commute. The component holds:

- `currentDomesticTension` — 0–100. Slow accumulator. Drained by good evenings, bad weekends, weekend recovery, vacations.
- `recentDomesticEvents[]` — last few days of significant home events (rolled per-NPC at session start).
- `bleedThroughRate` — how much home-state survives the parking lot. Personality-gated: high `extraversion` flushes the home state by the time they hit their desk; high `neuroticism` carries it all day.

On arrival each morning, an `ArrivalBleedEvent` fires that applies the home state's modifiers to:

- `MoodComponent` — initial mood for the day shifts up or down.
- `mask` baseline — bad-evening NPCs start the day with mask already at 60% instead of 100%.
- `irritation` baseline — fight-with-spouse NPCs come in with elevated irritation that has no work-source.
- `loneliness` — partner-traveling NPCs start the day below baseline.
- `belonging` — kids-doing-well NPCs walk in with a small lift.

### Specific bleed events

- **The breakfast argument.** Mask starts low. First conversation of the day is at risk.
- **The sick child overnight.** Sleep deprivation tag (existing) plus `worry` accumulator. Concentration is poor; phone-checks are constant.
- **The bill in the mail.** Financial-pressure spike (cluster 06.6) for the day.
- **The application sent.** Quiet anticipation; checking email more than usual; specific email-arrival is high-stakes.
- **The diagnosis.** Heaviest possible bleed. Full week of mask draw. May or may not surface visibly to coworkers.
- **The good evening.** Positive bleed. Some days the office gets a glimpse of an NPC they don't usually see.
- **The wedding-anniversary morning.** A specific date carries specific weight, possibly positive (anniversary-still-good) or negative (anniversary-of-a-failed-marriage).

### Components

- `HomeStateComponent`, `ArrivalBleedEvent`, `DepartureBleedEvent` (the reverse — what work-day state is going home with them).

### Cross-system interactions

- **ThresholdSpaceSystem (parking lot):** the parking lot is where the bleed happens. The car is the assembly room. The walk from the lot to the door is where the mask goes on.
- **StressComponent:** chronic domestic tension feeds chronic stress. NPCs with bad home situations have reservoirs that are always lower.
- **AppearanceDecaySystem:** rough nights show in appearance baseline at 9am.
- **DialogCalcify:** the mood at arrival modulates the day's dialog selection. A bad-bleed day is a clipped-fragment-heavy day for that NPC.

### Personality + archetype gates

- The Climber compartmentalizes by force. Their home-bleed is largest in private signals (early irritation), smallest in observable behavior. Mask draw is huge on bad mornings.
- The Vent (Donna) tells everyone about her morning. The bleed is openly broadcast.
- The Hermit goes silent on bad mornings — more than usual — and the floor reads it.
- The Recovering archetype's recovery program produces both *spikes* of home tension (events that test sobriety) and *anchoring* (sponsor calls, meeting nights). Their bleed shape is volatile.

### What the player sees

Each morning has its own texture. Reading the floor's mood at 9:15am is reading several home situations at once. Most days are normal. Some days the building feels different the moment doors open.

### The extreme end

The Affair partner whose spouse confronted them at home the night before. Mask is at 30% at arrival. The other Affair partner is *also* in the building, also carrying their own version of the same conversation (their spouse may or may not know). Both of them avoid each other deliberately the entire day. The floor reads two unrelated bad moods. By end of day, both have made small mistakes their coworkers note. The relationship matrix shifts by the next morning. The chronicle records what the floor knew without naming what it didn't know.

---

## 9.2 — SickFamilyMemberArc

### The human truth

When someone's parent or partner or child is seriously ill, the office is the place they *go* every day to *not* be at the hospital, and most of them aren't sure that's the right thing. Some are visibly distracted. Some perform extraordinary normality. All of them are carrying it. The office almost always finds out, eventually, through small behavior leaks.

### What it does

`FamilyHealthState` per NPC (most NPCs run `none`):

- `condition` — `none | parent_ill | parent_terminal | partner_ill | child_ill | own_chronic | recently_lost`.
- `intensity` — how acute it is right now.
- `disclosed: bool` — have they told the office?

Behavioral effects (continuous while active):

- Phone-checks per hour increase substantially.
- Mask baseline lowered by `intensity × 30`.
- Stress chronic + 20.
- Periodic phone-call-in-private events — they leave the floor for the parking lot or a stairwell, take a call.
- Weekly sudden-departure probability (had-to-leave events, half-day-out, etc.).

### The disclosure decision

When the office *learns* (through deliberate disclosure, observed behavior, or a leaving-the-meeting moment), the floor's behavior shifts:

- High-agreeableness NPCs offer small acts (covering shifts, dropping off food, sympathy). FavorLedger entries.
- Low-agreeableness NPCs back away — uncertain how to handle, withdraw.
- Specific archetypes (Old Hand) pull the affected NPC aside privately and speak from experience.

### The non-disclosure path

If the NPC chooses not to disclose, the office still infers — observable behavior changes are too noticeable to hide forever. The infer-without-confirming dynamic is uncomfortable for both sides; the affected NPC pays mask cost daily; the inferring coworkers pay a small mask cost too (performing not-knowing).

### Cross-system interactions

- **EavesdropSystem:** the parking-lot phone call is a high-yield eavesdrop event. Three NPCs over six months piece together a partial picture.
- **WorkloadSystem:** missed deadlines may correlate; coworkers reading patterns must decide if/how to compensate.
- **GoingAwayEvents (cluster 10):** a death-in-family event triggers a specific small-ritual response — sympathy card circulation, flowers from the office.

### What the player sees

Slow patterns. The phone-checking. The half-day-outs. The look on a face one Tuesday afternoon. Eventually a day when the chronicle records something direct, or the affected NPC takes leave, or the floor simply absorbs the slow collapse.

### The extreme end

A six-month decline. The Old Hand's parent is in hospice. They never told the office; the office figured it out by month two. Every Friday afternoon they leave at 3pm. By month four, the floor has informally started covering for them — the Vent stops scheduling Friday afternoon meetings; the Newbie offers to take their queue. The Old Hand never asks for any of this. Month six: they come in on a Thursday, eyes red, suit too dark for the day. Three coworkers know without being told. The chronicle records the parent's death even though the Old Hand never names it. The next week the office collectively gives them whatever they need — small, quiet, unannounced. The save's relationship matrix permanently records the floor as a unit that came through.

---

## 9.3 — InProgressDivorceArc

### The human truth

A divorce in progress is a slow public-private event. The wedding ring comes off (or doesn't). The phone calls escalate. The personal energy is depleted in ways that telegraph slowly. Some divorces are clean and brief; some are years; some destroy the affected NPC's productivity. Coworkers know without knowing. The day they *officially* know is months after they actually know.

### What it does

`DivorceArc` is a multi-month state on an NPC:

- `phase` — `something_wrong | first_disclosure | active_proceedings | finalization | aftermath_recovery`.
- `intensity` — how messy the situation is.

Behavioral effects across phases:

- **`something_wrong` (months):** subtle mood shift, occasional outbursts, decline in personal grooming, increased smoking-bench presence (cluster 18 / 12).
- **`first_disclosure` (event):** a small reveal — the absent ring, a shared lunch with a confidant where the words finally came out.
- **`active_proceedings` (months):** legal calls in private, financial pressure (cluster 06.6), erratic schedule (court appearances).
- **`finalization` (event):** a hard week or two; everything compressed.
- **`aftermath_recovery` (months to a year):** rebuilt baseline. The affected NPC is sometimes *better* than they were at the end — the relief is its own kind of mood lift.

### Cross-system interactions

- **AttireComponent (cluster 06.5):** the ring-off is a visible signal. The signaling intent on the day is `hiding` or `processing`.
- **Affair archetype:** an in-progress divorce can be *the* outcome of an affair arc, or its trigger.
- **AppearanceComponent:** rough patches throughout. NPCs read them.
- **Romance arcs (cluster 14):** post-divorce energy shifts the affected NPC's own attraction states — sometimes guarded, sometimes opened up, sometimes scattered.

### What the player sees

A long, slow story arc the player can read in fragments — the ring, the late nights, the half-days, the smoking-bench presence, the day they look ten years younger. Not every save will have one of these; some saves will have several at different stages.

### The extreme end

The Climber, mid-divorce, hides it for as long as possible because it threatens their `status`. They run mask at 80% for months. By month four the cracks are everywhere — productivity drops, missed deadlines, late arrivals. Their boss notices. A performance conversation happens. The Climber finally discloses, midway through the conversation, that they're going through a divorce. The boss's posture changes immediately. The performance conversation transforms into a sympathy conversation. The Climber's `status` is preserved through the disclosure of vulnerability they spent months avoiding. The lesson the Climber takes is wrong: they conclude that hiding worked because the disclosure saved them. The chronicle records the arc honestly.

---

## 9.4 — OnPremisesMedicalEvent

### The human truth

Sometimes someone has a medical emergency at work. Heart attack, asthma attack, allergic reaction, seizure, severe injury. The response is one of the most defining moments of an office's culture. Who runs toward; who freezes; who calls 911; who knows CPR; who pretends they didn't see. The aftermath is days of subdued mood and re-evaluation across the floor.

### What it does

`MedicalEmergencyEvent` fires under specific conditions:

- Pre-existing health conditions (NPCs can carry hidden `chronicCondition` flags — diabetes, cardiac, asthma, severe allergies).
- Acute trigger (the AC-broken summer day from existing ThermalComfortSystem; a stress spike; an allergen exposure from cluster 06.2; a fall from cluster 02.8).

The event progression:

1. **Onset.** Visible signs — shortness of breath, slurring, collapse. NPCs in line of sight enter `attention` state.
2. **Response.** Within ~30 sim-seconds, multiple NPCs converge. One calls 911 (existing phone system). One initiates first aid (high-conscientiousness + high-agreeableness). The rest cluster.
3. **EMS arrival.** Off-screen. Sim time advances; the affected NPC is taken away.
4. **Aftermath.** The floor is in shock for the rest of the day; productivity collapses; lunch is muted. A `floorTrauma` flag is set on the room for ~72 sim-hours; ambient stress baseline is elevated; conversations are quieter; the named anchor where it happened (e.g., the Conference Room) carries residual emotional charge for days or weeks.

### Components

- `MedicalEmergencyEvent`.
- `ChronicConditionComponent` (mostly hidden) per NPC — a short list of optional conditions that interact with thermal, stress, allergen, exertion, and dietary systems.
- `FloorTraumaComponent` per room.

### Cross-system interactions

- **PhobiaComponent (cluster 03.2):** witnesses with hemophobia, emetophobia, glossophobia (witnessing-something-helpless) trigger.
- **FavorLedgerComponent:** the responders accrue significant favor balance.
- **KnowledgeComponent:** "X had a heart attack at work" is a permanent fact about both X and about the office.

### What the player sees

Rare — once or twice across a multi-month save, if at all. Drastic when it happens. The floor afterward is *different.*

### The extreme end

The CEO is visiting from the parent corp. The Old Hand has a cardiac event during the meeting. The CEO does the right thing immediately — calls EMS, starts compressions. The Old Hand survives. The CEO never references it again; their relationship to the office is permanently warmer in a quiet way; the office's relationship to the CEO is forever shifted. The Old Hand, after recovery, comes back to work in three weeks. They are *changed*. The chronicle records their pre-event self and their post-event self as effectively two different characters.

---

## 9.5 — DeathInTheOffice / KnownDeath

### The human truth

A coworker dies. Sudden, expected, suicide, accident. The office goes through a specific sequence — disbelief, public mourning (the email from leadership), private grief (the people who actually knew them), the management of the empty desk, the awkward weeks, the eventual settling. Some saves should have one of these. None should have many.

### What it does

`DeathEvent` is a high-watermark simulation event. It can be:

- An off-screen death of a current NPC (the most weighty case).
- An off-screen death of a *former* coworker, learned about through a phone call or email.
- The Mark situation — the death (or departure-as-death) that's already in the world's pre-history.

Consequences:

- Mass `affection` and `mask` shifts across the floor.
- An empty desk that becomes a named anchor (Cubicle 12 — Mark — is canon for this).
- A chronicle event that calcifies forever.
- Long-tail effects: the friends pay mask cost for months; the close colleagues' KnowledgeComponent records change permanently; the floor's culture has a before-and-after.

### Cross-system interactions

- **NamedAnchorEmotionalCharge:** the empty desk carries decay-resistant emotional charge per the world bible's Cubicle 12 mechanic.
- **GoingAway events (cluster 10):** the funeral is a specific kind of going-away, and *everyone* attends.
- **RumorSystem:** for sudden or unexplained deaths, rumor traffic is intense and high-distortion. The truth (if knowable) and the rumor diverge.

### What the player sees

If at all, once. The floor changes. Some saves run their full course without it; some encounter it as the load-bearing event of the year.

---

## 9.6 — PregnancyArc

### The human truth

A coworker becomes visibly pregnant. The asking-or-not anxiety in early weeks. The disclosure event. The policy questions, the maternity-leave questions, the awkward congratulations from coworkers who don't know how to handle it, the weird congratulations from coworkers who do. The eventual leave. The return (or not). The visible body change is one of the most-managed visible-change arcs in office life.

### What it does

`PregnancyArc` for flagged NPCs:

- `phase` — `pre_disclosure | disclosed | visible | leave | postpartum_return | not_returning`.
- `weeksIn` — counter.

Behavioral effects:

- **Pre-disclosure:** morning sickness reactions; nausea events; bathroom visits up; food aversion changes (DietaryProfileComponent shifts temporarily).
- **Disclosed:** social shift around the affected NPC. Some coworkers warm; some withdraw; some over-perform interest.
- **Visible:** physical changes; thermal sensitivity changes; the *unsolicited belly touch* (cluster 5.2 unwanted touch event).
- **Leave:** absence period. Cubicle stays as-is.
- **Return / not return:** profound relationship-matrix shift. Some return diminished, some return reinvigorated, some don't return.

### Cross-system interactions

- **Touch system (cluster 5.2):** unsolicited belly touches are real, taboo, almost-never-corrected. The system records them.
- **DietaryProfileComponent:** food aversions and cravings.
- **GoingAwayEvents (cluster 10):** the baby shower at the office is a specific event with its own mechanics.

### What the player sees

Slow visible change. Office reorganizes around it. Some saves have one; some don't.

### The extreme end

The Climber is pregnant. Pre-disclosure phase coincides with their biggest career push. They're hiding nausea, hiding fatigue, hiding the bathroom visits. Mask is at full draw. The disclosure event, when it comes, lands during a quarterly review. Their boss's response — supportive vs. punitive vs. uncomfortable — defines the rest of their year.

---

## 9.7 — RecentlyLost (the absence-marker arc)

### The human truth

Someone in the affected NPC's home life recently died (parent, sibling, partner, friend, pet). The office doesn't always know. The grief is private but constant. Specific dates, anniversaries, songs, smells trigger spikes. The first holiday. The day the dog would have turned ten.

### What it does

`GriefState` per NPC:

- `loss` — what was lost, and when.
- `intensity` — fresh grief vs. settled.
- `triggers[]` — specific dates, anniversaries, sensory bindings.

The `SmellMemorySystem` (cluster 07.8) interfaces directly here — specific smells are grief triggers.

Behavioral pattern:

- Quiet days that don't correlate with anything visible at work.
- Anniversary days (engine knows; floor doesn't).
- The *first* holiday after a loss. Major mask draw.
- Gradual settling over months and years.

### Cross-system interactions

- **CalendarRitualSystem (cluster 10):** anniversary dates are private calendar entries that produce private events.
- **HomeStateBleed (9.1):** grief days compound bleed-through.

### What the player sees

A pattern of bad days that don't have a visible cause. Eventually, with enough save time, the player reads it. The simulation refuses to flatten it.

### The extreme end

The first Mother's Day after the Old Hand's mother died. They come in. They go through the day. The floor doesn't know. The Vent, who pays attention to dates, brings them coffee at 11am with no comment. The Old Hand's `affection` toward the Vent jumps thirty points. The chronicle records nothing public; the relationship-matrix records the moment.

---

## 9.8 — NewParenthoodReturn

The flip side of pregnancy / adoption / new-fostering. The NPC returning to work after time at home with a new family member. Different bleed-through profile:

- Sleep deprivation (heavy).
- Distractibility (high).
- Emotional volatility.
- The phone-checks for childcare.
- The visible evidence on clothing (a stain that wasn't there when they left home).

`NewParenthoodReturn` arc shapes the first ~3 months back. Coworkers are mostly tolerant; a few are not; the Climber's career calculation has changed even if they pretend it hasn't.

### What the player sees

A returning NPC who's *visibly different* — exhausted, scattered, sometimes radiant, sometimes near tears. The office adjusts.
