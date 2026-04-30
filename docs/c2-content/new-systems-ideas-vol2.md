# New Systems Ideas — Vol. 2

> Companion to `new-systems-ideas.md`. Picks up where the first document left off. Same rules: no idea is a bad idea, nothing is final, do not program any of this. Read for review.

---

## On the gaps

The first document covered the body in the office: stress, work, noise, smell, coffee, meetings, illness, temperature, the mask. What it didn't cover fully is the *calendar* of office life — the scheduled social events that reorganize everything. It also didn't cover what happens to the social graph when the formal hierarchy moves: a promotion, a firing, a new hire, a long absence. And it left largely untouched the cost of *knowing things* — the information that has to be held, suppressed, carried. Those are the gaps these proposals try to fill.

---

## System ideas

---

### 19. PerformanceReviewSystem

**The human truth:** The performance review is the most anticipated and most dreaded formal event in office life, and it happens on a schedule everyone knows but nobody feels ready for. It's the moment the informal ledger gets read aloud — sort of. The official version is always a translation of the real version. Everyone knows what the feedback actually means and nobody says what it actually means.

**What it does:**
Performance reviews are scheduled events (SimulationClock-driven, quarterly or annually). In the lead-up, NPCs' behavior is modulated by anticipation: work quality temporarily improves for status-driven NPCs, mask maintenance effort increases, Climber types enter a heightened preparation loop that reads as both impressive and slightly desperate.

**Components:**
- `PerformanceReviewComponent` on NPC: LastReviewDate, ScheduledReviewDate, ReviewScore (0–100 composite of recent WorkloadComponent output quality), Outcome (pending/positive/neutral/negative/PIP)
- `ReviewAnticipationModifier` in BrainSystem: applied in the N ticks before a scheduled review; scales with NPC StatusDrive level — high status drive → strong behavior change in anticipation; low StatusDrive → barely noticed

**The outcomes and their consequences:**
- **Positive review + raise:** StatusDrive satisfied; Belonging drive bumped from celebratory acknowledgment (others find out); The Climber's mask strengthens briefly — they have something to perform *from* again. The people who didn't get one file this in KnowledgeComponent.
- **Neutral review:** The slow wound. NPC expected better. They know what neutral means. Their Loneliness drive ticks upward for a period; work motivation drifts; some respond by trying harder (Conscientiousness gate), some by quietly beginning to look elsewhere (Neuroticism + Openness gate).
- **PIP (Performance Improvement Plan):** Social signal before any conversation happens. Other NPCs read the shift in behavior — avoidance, over-performance, visible stress — and the rumors begin. The subject of the PIP performs normalcy at enormous mask cost. Their work quality, which is what got them here, continues to degrade under the stress of trying to fix it. The system that produced the problem now makes the problem harder to solve.
- **Firing:** Handled by existing LifeState-adjacent removal mechanism. But the NPC's last day, and the empty desk afterward, have their own system (see AbsenceRippleSystem, below).

**The manager dynamic:**
The NPC who gives reviews has their own experience of the process. High-agreeableness managers delay difficult reviews, soften outcomes past usefulness, leave the recipient confused about where they stand. Low-agreeableness managers are accurate but abrupt — the feedback lands hard and the relationship doesn't recover quickly. The review is a character test for the reviewer as much as the reviewed.

**The extreme end:**
The Climber gets a positive review and a small raise. They've been building toward this. They accept it, thank the manager, walk back to their desk, sit down, and feel nothing — or something that isn't satisfaction. The amount is less than they believed they deserved. The mask goes back up. Nobody can see the calculation happening behind it. They begin building their ledger for the next cycle before the current one has been formally closed.

---

### 20. SubstanceDriftSystem + SubstanceStateComponent

**The human truth:** The early-2000s office had a relationship with alcohol that is hard to explain to people who weren't there. The after-work drinks were mandatory in a way nobody said out loud. The holiday party had an open bar and the bar was used. There was a flask in someone's desk on this floor — you don't know whose. The Recovering archetype exists in the cast bible specifically because the office context is one of the hardest environments to maintain sobriety in: every social event is built around a substance, every milestone is toasted, the one person who doesn't drink is the one who has to explain why.

**What it does:**
Alcohol is an environmental substance available at specific world events (holiday party, happy hours, the flask entity in the desk drawer) and at off-site venues NPCs visit. Consumption produces a temporary `SubstanceStateComponent` that modifies behavior in ways that are initially prosocial (lower mask cost, higher trust-transfer rate, suppressed irritation thresholds) and then become disruptive (emotional leakage, reduced behavioral inhibition, appearance decay acceleration, next-day energy penalty).

**Components:**
- `SubstanceStateComponent` on NPC: SubstanceLevel (0–100), CurrentAgent (alcohol/caffeine/other), PeakTime, HangoverPending
- `SobrietyStreakComponent` on The Recovering archetype specifically: StreakDays, StrainAccumulator (accumulated environmental pressure toward breaking streak), LastStrainEvent
- `FlaskComponent` on world entity: ContentsLevel, Owner, IsHidden (visible only to proximity-range NPCs)

**The social mechanics:**
In non-event contexts, alcohol doesn't circulate — the flask is private and mostly stays private. At office events, substance-free participation is conspicuous. When an NPC declines a drink, other NPCs notice (proximity-range awareness event). The social cost of not drinking is real — it registers as slightly strange, slightly unknowable. High-agreeableness NPCs quickly explain it away and move on; high-suspicion NPCs file it.

**The Recovering archetype's specific arc:**
`SobrietyStreakComponent.StrainAccumulator` builds from environmental pressure events: the holiday party invitation, watching others drink nearby, high-mask-strain days, favor requests that involve "come out with us," the flask being noticed in the proximity zone. When `StrainAccumulator` crosses a personality-gated threshold, the decision event fires. The player can watch the accumulation build over weeks. The decision, when it happens, looks sudden from outside. It wasn't.

**The morning-after mechanics:**
NPCs with `HangoverPending` arrive the next day with reduced energy ceiling, elevated irritation baseline, appearance decay starting below normal, and — importantly — elevated shame response to anything that happened the previous night. If they said something they shouldn't have (`PassiveAggressionSystem` interaction, `NarrativeEventBus` events from the party), the shame component reads those events in KnowledgeComponent and produces avoidance behavior.

**The extreme end:**
It's the Monday after the holiday party. Two NPCs who had a conversation they shouldn't have are now at adjacent desks. Both have the event in KnowledgeComponent. Both have `HangoverPending` resolved but the shame component is active. One of them sent an email at 11pm they're now rereading. They don't know the other person has already seen it. The other person hasn't decided what to do with it yet.

---

### 21. PersonalTerritorySystem + TerritorialClaimComponent

**The human truth:** The desk is yours. The chair is yours. The mug is yours — especially the mug. The mug says something on it, probably. Nobody else uses the mug. When someone uses the mug, even innocently, even once, something happens inside you that is disproportionate to the mug. This is not about the mug. The mug is the symbol of the only thing you actually own in this building you spend eight hours a day in.

**What it does:**
NPCs make territorial claims on desk objects, chairs, specific bathroom stalls, specific parking spots, specific microwave time slots. Claims are informal, invisible to the formal hierarchy, and enforced only through social pressure — which means enforcement is extremely expensive and almost never happens directly.

**Components:**
- `TerritorialClaimComponent` on NPC: Claims[] — each claim is (EntityId, ClaimType, ClaimStrength 0–100, ViolationHistory[])
- `ViolationEventComponent` on event entity: Territory, ViolatingNpc, WitnessingNpcs[], OwnerPresent
- Tags: `TerritoriallyStressedTag` — applied when a NPC is in the same room as a violation in progress

**Claim formation:**
Claims form automatically from repeated behavior: an NPC who uses the same chair five times has a developing claim. At ten consistent uses, the claim is established. Other NPCs in proximity observe the pattern and add it to their KnowledgeComponent as informal social fact.

**Violation dynamics:**
When a new NPC (hire or temp) sits in the wrong chair, they don't know the claim exists. The NPC who owns the claim receives an irritation spike. The violation is disproportionate to the actual harm — this is mechanically correct. The response options are personality-gated:
- High agreeableness: say nothing, stew, mention it to a third party later (PassiveAggressionSystem interaction)
- Low agreeableness + low conscientiousness: say something immediately, which produces a scene
- The Cynic: say something, make it funnier than it needs to be, move on
- High conscientiousness: redirect the new person, no confrontation, but the violation is logged internally

**The parking spot:**
A named world anchor already — "assigned spots, people do not respect the assignments." This is a TerritorialClaimComponent on a parking space entity. The violation happens in ThresholdSpace, before the workday has begun. The NPC arrives at their spot, finds another car. They carry that irritation in through the door. The day starts already slightly wrong. Nobody knows why they're slightly off all morning.

**The sacred mug:**
A specific deal behavior: an NPC has a `TerritorialClaimComponent` on one mug entity with maximum ClaimStrength. The mug is kept at their desk, not in the common area. The day someone moves it, or uses it, or breaks it — the day is over. This is one of the cleanest deal-to-system mappings in the catalog: the mug isn't listed in the deal as important; what the deal says is "has been here long enough to have opinions about the mug," and the system renders that.

**The extreme end:**
The temp. They've been here four days. They don't know the seating geography yet — nobody told them. They sat in The Climber's chair for one afternoon while The Climber was in a meeting. The Climber came back, clocked it, said nothing. The temp moved when asked, apologized, thought it was over. It wasn't over. The Climber filed it. Three weeks later, when a decision gets made about extending the temp's contract, The Climber's input is quietly negative. The chair is not mentioned. It doesn't need to be.

---

### 22. BodyLanguageReadSystem + ObservableSignalComponent

**The human truth:** You can tell, usually, before anyone says anything. The way someone walks in. The quality of their stillness at their desk. Whether they greet people on the way by. The eyes. Especially the eyes. You can read a room from the door and know who's having a bad day before you've spoken to anyone. This skill — reading bodies in motion — is something every office worker has and uses constantly, and it's something the simulation should be generating and making readable.

**What it does:**
NPCs emit observable signals derived from their internal state. These signals are visible to other NPCs in awareness range (wider than conversation range, based on line of sight). Other NPCs read signals and adjust their approach behavior — whether to initiate conversation, whether to reroute, whether to file the read in KnowledgeComponent.

**Components:**
- `ObservableSignalComponent` on NPC: MovementSignature (normal/hurried/slow/erratic), PostureSignal (open/closed/collapsed), GazePattern (avoidant/direct/unfocused), FacialComposure (high/low — an expression of MaskStrength without naming it), AggregateReadability (composite signal other NPCs receive)
- Signal values are derived each tick from internal state: StressLevel, MaskStrength, MoodComponent drives, BiologicalUrgency levels

**What gets emitted and when:**
- High StressLevel → hurried MovementSignature + avoidant GazePattern
- Low MaskStrength → low FacialComposure + collapsed PostureSignal
- High BladderUrgency → specific micro-movement pattern that reads as restlessness
- FlowState active → unfocused GazePattern (absorbed, not avoidant) + slow movement between micro-tasks
- High irritation → direct gaze that's slightly too sustained, slightly too flat

**How other NPCs read the signals:**
NPCs in awareness range process `ObservableSignalComponent` each tick against their own EmpathyScore (personality-derived). High-empathy NPCs read signals accurately and adjust behavior: they don't initiate conversation with someone whose signals read as overloaded; they check in if signals read as distressed. Low-empathy NPCs process signal at lower accuracy — they approach anyway, or they miss distress that's obvious to everyone else.

**What the player sees:**
The simulation renders internal states through observable behavior without requiring text labels. An NPC who's at 15% MaskStrength looks different at their desk than one at 90%. The player learns to read the encoding. A good design moment: the player spots a signal they recognize, predicts what's coming, watches it unfold.

**The archetypes:**
- The Cynic: low `FacialComposure` by default — they don't conceal. Their signals are readable to everyone, which is their point. They're not trying to be opaque.
- The Climber: artificially high `FacialComposure` maintained at high mask-energy cost. Their signals are unreliable — they've trained themselves not to emit. Other high-empathy NPCs find this faintly uncanny.
- Greg: movement patterns are slow and deliberate. He's not avoidant — he's just unhurried in a way that reads as strange in a context where hurry is the baseline signal of seriousness.

**The extreme end:**
The NPC nobody has approached all day because every observable signal they've emitted since 9am has read as stay away. Nothing has happened. No confrontation, no event. But the day's signal has been clear enough that other NPCs have, collectively, rerouted around them. By 4pm, they've been in effective isolation for seven hours. Nobody planned this. It's just what bodies in a shared space do.

---

### 23. HolidayEventSystem + MaskCollapseEventComponent

**The human truth:** The holiday party is the mask-collapse event everyone anticipates and half the cast dreads. It has an open bar, which is mandatory, and a formal context that has been deliberately suspended, which means everyone has to perform normal relaxation. The relationships that break and form at the holiday party are different from any other context. What happens at the party lives in KnowledgeComponent forever. The Monday after is a specific social topology.

**What it does:**
The holiday party is a world-calendar event with its own system phase. All NPCs are expected to attend — `CaptiveComponent`-adjacent but softer. The party environment has unique behavioral rules: mask maintenance is explicitly suspended (the context says relax), alcohol is available, the formal hierarchy is nominally dissolved, and the space is ThresholdSpace rules with the whole floor present.

**Components:**
- `HolidayPartyEventComponent` on the event entity: Date, Location, AlcoholAvailable, AttendanceExpected[], ActualAttendance[]
- `PartyBehaviorComponent` on NPC (active during event): SocialModeActive (relaxed vs. performing-relaxed vs. avoidant), TrustTransferRate (elevated by 40% — informal context), MaskDecayMultiplier (alcohol + context = fast depletion)
- `PartyIncidentComponent` on incident entities: Actor, Subject, WitnessIds[], IncidentType, Severity

**What happens during the event:**
Every NPC at the party has their MaskStrength decaying at 2× normal rate before accounting for alcohol. Conversations that wouldn't happen in the regular work context become available. Trust thresholds lower. The Old Hand finally tells the story they've been sitting on all year. The Affair's partners find themselves in the same room at the same time again. The Newbie has their first encounter with the informal social rules of the office at full expression, with no filter.

**The incidents that fire:**
Party incidents are a product of two NPCs whose internal states, lowered masks, and existing relationship state produce a social event the system wouldn't normally generate. These include:
- The confession (high trust + low mask + alcohol → a true thing said)
- The confrontation (high irritation + low mask + audience → a true thing said wrong)
- The formation (two NPCs who've been in neutral proximity all year discover, in this context, that they like each other)
- The revelation (someone sees something they weren't supposed to)

All incidents generate `PartyIncidentComponent` entities that persist in KnowledgeComponent for all witnesses.

**The Monday after:**
`HolidayEventSystem` generates a `PostEventStateComponent` for each attendee with a `SocialRecalibrationRequired` flag. Relationships that shifted during the party are now in a superposition — did the thing that happened count? NPCs manage this through avoidance, through the pretense-of-normalcy, through behavior that is slightly but unmistakably different. The system runs this for as long as it takes for the new equilibrium to form.

**The extreme end:**
The Founder's Nephew has been openly insufferable for eleven in-game months. At the party, in front of six witnesses, he says something that tips past a threshold nobody could have predicted. The Cynic responds, out loud, in front of everyone, with exactly what they've been thinking since October. The witnesses disperse over the next four minutes. By Monday, the `PartyIncidentComponent` is in eighteen KnowledgeComponents. The Nephew's behavior doesn't change. The entire floor's relationship to him does.

---

### 24. SharedResourceContentionSystem + ContestableEntityComponent

**The human truth:** Every shared object is a political object. The printer. The good scissors. The conference room schedule. The parking spot (named anchor). The one working USB hub. Whoever needs it most right now is rarely the one who has it — and the mechanism for resolving competing needs is never fair and never acknowledged as political.

**What it does:**
Certain world entities are marked `ContestableEntityComponent` — they can only be used by one NPC at a time, they have queue state, and the queue is resolved through a combination of formal priority (title, seniority) and informal priority (who got there first, who's more assertive, whose need is most visible).

**Components:**
- `ContestableEntityComponent` on shared world objects: CurrentUser, Queue[], AccessHistory[], UseTypicalDuration
- `QueuePositionComponent` on NPC when waiting: TargetEntity, ArrivalTime, PriorityScore (formal + informal composite)
- Contest resolution: personality-gated. High-assertiveness NPC jumps queue without formal justification. High-agreeableness NPC waits even when they have more urgent need. The Climber references formal hierarchy when it benefits them; ignores it when it doesn't.

**The printer:**
The canonical contested object. The 47-page document sent to the shared printer two minutes before someone needed it for a meeting is a mechanical output of this system. The NPC who sent the long document didn't do it to hurt anyone — they just didn't think about the queue. This is the entire tragedy.

The print job runs for six minutes. The NPC who needed one page for their meeting prints from the printer on another floor, which required going down stairs, which required missing the first five minutes, which required apologizing at the meeting's start, which cost them in a context that was already fragile. The person who sent the 47 pages finishes collecting their document, returns to their desk, never knows.

**The conference room calendar:**
A contested resource with a booking system that nobody respects fully. NPCs who hold recurring blocks rarely release them, even when they don't need them. NPCs who need the room for something important discover it's technically booked by a recurring meeting that nobody has actually attended in three weeks. The meeting organizer would release it if asked but nobody has asked because asking is a social act.

**The "good" whatever:**
Every office has one. The good chair is in the conference room. The good scissors are in someone's desk. The only stapler that actually works is labeled with a name in permanent marker. `ContestableEntityComponent` makes the good version of objects scarce in a way that produces a small but constant ambient friction.

**The extreme end:**
The supply closet has two working printers and three that are listed as online but aren't. The first person to arrive in the morning has access to the working ones. The person who arrives at 8:58 and needs to print before a 9am meeting has a problem. The printer that was working at 8:40 is now jammed. The NPC knows how to clear the jam. Clearing the jam takes longer than they have. They go to the other floor. The 9am meeting has already started.

---

### 25. EmotionalTopographySystem + RoomHistoryComponent

**The human truth:** Some rooms are heavy. Not from the air or the temperature — from what happened in them. The conference room where someone was fired. The break room where the announcement was made. Cubicle 12 (named anchor, world bible: "Mark is not discussed"). The room doesn't remember, but the people who were in it do, and their behavior in the room afterward is different. The room *feels* different, even to people who weren't there, because the people who were there are different in it.

**What it does:**
Significant events that occur in a room generate a `RoomMemoryRecord` on that room's `RoomHistoryComponent`. NPCs who were present for the event have that event in their KnowledgeComponent and carry a personal `RoomAssociationModifier` — a mood and behavior modifier applied whenever they enter that space.

**Components:**
- `RoomHistoryComponent` on room entities: EventHistory[] — each entry is (EventType, Timestamp, WitnessIds[], EmotionalCharge, DecayRate)
- `RoomAssociationModifier` in BrainSystem: per-NPC, per-room lookup. When NPC enters room, applicable modifiers are applied to mood and approach behavior.
- Tags: `AvoidingRoomTag` — applied to NPCs whose RoomAssociationModifier for a given room is above avoidance threshold; their pathfinding weights that room negatively

**Cubicle 12 (Mark's desk, named anchor):**
This is the system that gives the "Mark is not discussed" named anchor its mechanical meaning. Cubicle 12 has a `RoomHistoryComponent` record from whatever happened (high EmotionalCharge, DecayRate near zero — it's not decaying). NPCs who were present have high negative `RoomAssociationModifier`. NPCs who weren't there receive a weaker, secondhand version through KnowledgeComponent transfer. The suppression cost in SocialMaskSystem (holding the known fact that can't be spoken) is why NPCs adjacent to Cubicle 12 drift toward `suspicion` and `loneliness` — they're spending mask energy on the silence, every day, near the place.

**Event types that generate room history:**
- Firing (high charge, long decay)
- Confession or breakdown (medium charge, medium decay — time heals)
- Confrontation (medium charge, fast decay for witnesses, slow decay for participants)
- Party incident (variable, depends on severity)
- Death or serious injury (maximum charge, near-zero decay — this space is changed)
- Significant positive event (rare, but it works the other direction too — the room where they celebrated the big win carries warmth)

**The player-facing effect:**
The player notices, over time, that NPCs behave differently in specific rooms. The break room after the argument. The elevator that was last used by two people who stopped speaking. The hallway outside HR. These aren't labeled. The player reads it from behavior: the rerouting, the clipped conversations, the NPC who always takes the stairs now.

**The extreme end:**
Six months pass. New NPCs join. They've never heard of Mark. They sit near Cubicle 12. They don't know why they're slightly more anxious at that desk, why their loneliness drive ticks up, why the NPCs around them have this subtle pattern of not quite gathering near that space. The emotional topography of the floor is older than they are. They're inheriting it without being able to see it.

---

### 26. NewHireIntegrationSystem + IntegrationStateComponent

**The human truth:** The first four weeks determine almost everything. How someone enters a social system — who is kind to them first, what mistake they make before they have social capital to absorb it, which informal rules they violate before anyone has told them the informal rules exist — locks in their social position faster than anything that comes after. By week five, they're already whoever they are to this floor.

**What it does:**
NPCs entering the simulation for the first time (new hire, temp, transfer) have `IntegrationStateComponent` for their first N ticks. This modifies how they're processed by other systems and how other NPCs respond to them.

**Components:**
- `IntegrationStateComponent` on NPC: StartDate, IntegrationTick (current), IntegrationPhase (arriving/orienting/positioning/established), FirstImpressionData[], KeyRelationshipIds[], IntegrationRiskScore
- `FirstImpressionRecord` in KnowledgeComponent: created when two NPCs have their first proximity event — captures AppearanceComponent state, context, initial vibe read (trust/suspicion seed)

**The phases:**
- **Arriving (0–3 days):** New NPC has no social graph. Movement is uncertain. They don't know where things are. `FirstImpressionRecord` entries are being written by every NPC they encounter. AppearanceDecaySystem input is high — they're in performance mode constantly. Mask energy burns fast.
- **Orienting (3–10 days):** Some relationships forming. First invitations — or conspicuous non-invitations — to lunch. First mistake. First favor given or received. The social position is beginning to calcify.
- **Positioning (10–25 days):** The new NPC's identity on the floor is mostly set. The stories about them that circulate in RumorSystem are now established. They're getting invited to the things they'll always get invited to; not invited to the things they'll never be invited to.
- **Established (25+ days):** IntegrationStateComponent removed. They're just an NPC now.

**The mistakes:**
The integration period contains a heightened probability of accidental social violations: territorial claims they don't know about (see PersonalTerritorySystem), informal hierarchies they haven't mapped, conversational topics that are quietly off-limits (Mark, Cubicle 12). How existing NPCs respond to these violations determines the new NPC's social trajectory. Forgiveness from high-agreeableness NPCs builds trust fast. Cold correction from high-conscientiousness NPCs builds wariness. Silence (no response to an awkward thing said) builds mystery.

**The first person who is kind:**
Whoever the new NPC has their first positive proximity event with tends to become their anchor relationship. This is mechanically true in the social graph — their initial trust seed is higher with that NPC, which drives proximity-seeking, which compounds. The social mentor relationship is not assigned by the system; it emerges from whoever happened to be nearby at the right moment and was readable as approachable.

**The extreme end:**
The Newbie's week three. They've figured out most of the rules. They don't yet know about Cubicle 12. They mention Mark — casually, asking who used to sit there — in front of three NPCs who were present. The silence is immediate, complete, and informative. They understand instantly that they've stepped in something, but not what or where. They spend the rest of the day in a state of social anxiety they can't name. They don't mention Mark again. They don't know why Mark can't be mentioned. They join the suppression without having access to the fact being suppressed.

---

### 27. AbsenceRippleSystem + AbsenceStateComponent

**The human truth:** When someone is gone — sick day, vacation, firing — their absence is a presence. The work doesn't stop. The desk sits there. Other people fill in, which costs them. The returning NPC re-enters a social system that continued without them, which is always slightly disorienting. And if the NPC doesn't return (fired, quit, transferred), the empty desk is a fact the building has to metabolize, over time, at its own pace.

**What it does:**
When an NPC is absent for more than a threshold duration, `AbsenceRippleSystem` generates effects on the remaining population: task reallocation, social awareness of the gap, relationship adjustment upon return.

**Components:**
- `AbsenceRecord` on absent NPC (or room if permanently empty): DurationTicks, Reason (sick/vacation/terminated/unknown), CoverageAssignedTo[], TasksAccumulated[], SocialAwarenessLevel (how much the absence is being noted)
- `CoverageStressComponent` on NPC assigned coverage: CoverageLoad, OriginalAssignee, CoverageStartTick — this adds directly to WorkloadComponent and StressComponent

**The task spillover:**
An absent NPC's active tasks don't pause. Deadlines continue. WorkloadSystem assigns spillover to whoever has the closest skill overlap and the lowest current load — which is never actually someone with capacity, because the people with capacity are overloaded from the last time someone was absent. Coverage is always a tax on NPCs who are already running hot.

**The vacation return:**
The NPC who returns from vacation re-enters to find:
- Tasks accumulated (WorkloadSystem catch-up period)
- Social graph slightly shifted — conversations happened without them, relationships moved
- A brief `SocialRecalibrationRequired` flag as they re-integrate
- KnowledgeComponent gap: things happened while they were gone that they're learning secondhand with delayed confidence

The return from vacation has a specific emotional topology: relief at being back (familiar context), fatigue from the re-entry load, subtle awareness of the gap, and the unspoken performance of "the vacation was fine" regardless of whether it was.

**The empty desk after a firing:**
This is the heaviest version of absence. The desk sits. Personal items may still be on it — the period between firing and desk-clearing is its own social event. NPCs walk past it. NPCs whose desks are nearby have a low-grade `EmotionalTopographySystem` effect (see above). NPCs who were close to the fired NPC have a grief-adjacent response. NPCs who weren't close, who in fact didn't like them, also have a response — muted, complicated, not grief but not nothing.

The desk gets cleared on some tick determined by the HR calendar. A new person may eventually sit there, which triggers `NewHireIntegrationSystem`, which means the new person is inheriting the social charge of whoever they replaced before they ever know who that was.

**The extreme end:**
The person who was there the longest leaves. Voluntary retirement, or otherwise. They'd been there before the second printer was installed, before the current carpet was laid, before half the current staff arrived. Their desk is cleared. Their parking spot — the one closest to the door, the one everyone has always known was theirs, the one they earned through sheer duration — is now contested. Three NPCs arrive the first Monday after and are surprised to find it empty and available. All three feel the strangeness. None of them park there.

---

### 28. PromotionRippleSystem + HierarchyShiftComponent

**The human truth:** A promotion changes everything, and the person being promoted is the last to understand what has changed. The relationships they had at one level don't fully survive the transition to another. The people they were close to now have a status differential that wasn't there before. The people who wanted the promotion and didn't get it know it. Everyone recalculates.

**What it does:**
When an NPC's formal status changes — promotion, demotion, transfer to a different level — `PromotionRippleSystem` propagates effects through the social graph. The promoted NPC's `StatusComponent` updates; all NPCs with relationships to them receive a recalibration event.

**Components:**
- `HierarchyShiftComponent` on event entity: NPC, OldLevel, NewLevel, EffectiveDate, AnnouncedTo[]
- `RelationshipRecalibrationComponent` on NPC: triggered for each NPC with a relationship to the promoted NPC; contains (RecalibratingWith, StatusDelta, OldTrust, ProjectedNewTrust, RecalibrationProgress)

**The relational math:**
Trust and belonging between NPCs are calibrated partly to parity. When parity breaks, the calibration has to reset. An NPC who was a peer and is now a manager produces:
- For high-status-drive NPCs: immediate recalibration toward access-seeking (the promotion is a resource)
- For low-status-drive NPCs: genuine warmth preserved, but with new awkwardness around the differential
- For NPCs who wanted the promotion: a wound. Not expressed directly. Expressed through behavior change over weeks — subtle avoidance, slightly colder tone, stories about the promoted NPC that start circulating differently in RumorSystem.
- For NPCs who didn't want the promotion and didn't compete: mild relief that the social structure has resolved, followed by normal recalibration.

**The announcement as event:**
The promotion is announced — formally, or through the grapevine (RumorSystem lag). The grapevine version arrives first and is distorted: the version that circulates before the official announcement may have the wrong NPC, wrong level, or wrong timing. NPCs make relationship decisions based on the rumor, then have to recalibrate again when the official version lands. This is how offices work.

**The Climber's arc:**
The Climber is building toward a promotion. The system tracks their approach behaviors, favor ledger, mask maintenance, and work output as a composite score. When they're promoted, the arc changes: they're no longer climbing, they're consolidating. New behaviors emerge — the people below them are now a different kind of resource, and the people at their new level are now competition. The social persona recalibrates entirely. The Climber doesn't become a different person. They become a more advanced version of the same person.

**The extreme end:**
Someone unexpected gets promoted. Not The Climber. Someone who wasn't running for it, who has been in the simulation long enough to have a lot of social equity but no visible ambition. The announcement goes through RumorSystem first: heard it from someone who heard it from the manager's assistant. The Climber processes this information in a way that is entirely internal and entirely invisible. Their mask goes to 100% and holds there. Their favor ledger begins a new calculation. Nobody can see what's happening. The promoted NPC notices something in The Climber's eyes on Monday morning and can't name it. It's fine. Probably it's fine.

---

### 29. SecretWeightSystem + SecretBurdenComponent

**The human truth:** Holding a secret costs something. Not morally — mechanically. The cognitive and emotional load of knowing a thing that must not be surfaced, of tracking every conversation to make sure you don't slip, of watching the person whose secret you hold and performing not-knowing — it's effortful in a way that doesn't show up in any job description but absolutely shows up in behavior. The longer the secret, the heavier it gets. Secrets held in groups are especially expensive — the group has to maintain a shared suppression, and the cost of the suppression is distributed.

**What it does:**
Extends the KnowledgeComponent with a suppression modifier for specific facts. Facts flagged as `MustSuppressFlag` (either by the NPC's own decision or by social norm) generate a `SecretBurdenComponent` on the holder — a continuous drain on MaskStrength and a subtle irritation accumulator.

**Components:**
- `SecretBurdenComponent` on NPC: SuppressedFacts[] — each entry is (FactId, SuppressLevel 0–100, BurdenStartTick, CarryingCost)
- `CarryingCost` is derived from: FactSensitivity × SuppressLevel × DaysSinceAcquired × RelationalProximityToSubject
- The cost flows into: `SocialMaskComponent.CurrentEffort` (holding the secret costs mask energy) and a slow `IrritabilityAccumulator` (the suppressed truth looking for an exit)

**How secrets are acquired:**
- Direct: NPC was a witness or participant
- Overheard: EavesdropSystem flagged as `MustSuppressFlag` by social norm (sensitive topic in wrong context)
- Told in confidence: the fact transferred with explicit or implicit expectation of non-disclosure
- Inferred: NPC has assembled pieces from observation and now knows something they've never been directly told — this is the hardest to suppress because it has no clear owner

**The group secret:**
When multiple NPCs share a suppressed fact, `SecretWeightSystem` tracks group coherence. If one NPC's burden is high enough that they start to leak (through behavioral signals, through near-slips in conversation), other group members receive an awareness event — their own suppression cost spikes. They become slightly more careful. Slightly more watchful of the leaking NPC. The group's suppression is now a team project, which is its own dynamic.

**Cubicle 12 (Mark, named anchor):**
This is the named instance. Multiple NPCs have the same suppressed fact. The `CarryingCost` has been accumulating since whatever happened. Some NPCs have carried it long enough that the cost is just background — normalized. Some are still paying it actively. New NPCs get the fact transferred through RumorSystem with a `MustSuppressFlag` attached automatically — they inherit the suppression norm before they've had time to decide whether they agree with it.

**The leak:**
When `IrritabilityAccumulator` crosses a threshold, the NPC becomes at risk of an involuntary disclosure. Not a decision — an event. Under stress, in proximity to the subject, at low MaskStrength, after the holiday party. A reference made. A name said. The other holder of the secret in the room is suddenly very still.

**The extreme end:**
The NPC who has held the thing for the longest. Years of game-time. The carrying cost has compounded. They're fine most of the time. Then the new hire, who doesn't know any of it, asks an innocent question. Not about Mark specifically — about something adjacent. About the desk, about the old carpet, about why no one ever uses the conference room on Thursdays. The NPC responds. Accurately, carefully. And in the response there is one word that wasn't supposed to be there. A name. Just the first name. The other NPCs in the room hear it. The new hire doesn't know why the room changed.

---

### 30. MandatoryComplianceSystem + ComplianceStateComponent

**The human truth:** Every three months, or six months, or annually, everyone on the floor has to complete a mandatory training module. Sexual harassment prevention. Data security. Fire safety. OSHA something. The content is mostly not relevant to anything anyone does. The module has existed since 1998 and the interface has not been updated. There is a quiz at the end with questions so obviously keyed to specific answers that everyone passes. The company is legally covered. Nothing changes. And yet the shared ordeal of it — the eye-rolling, the timing-out-the-minimum-time-per-slide — is a genuine social bonding moment.

**What it does:**
Compliance events are scheduled world-calendar events. Each NPC receives a `ComplianceStateComponent` with a deadline. Completing the training is a desk-bound activity that blocks work progress and produces frustration. Not completing before the deadline produces HR attention, which produces StatusComponent anxiety.

**Components:**
- `ComplianceStateComponent` on NPC: AssignedModule, Deadline, CompletionPercent, TimeOnSlide[], PassedQuiz, MethodOfCompletion
- `ComplianceEventComponent` on world event: Module, AssignedNpcs[], Deadline, HrEnforcerId

**The completion strategies (personality-gated):**
- High conscientiousness: reads every slide, takes notes (on paper, it's 2001), actually thinks about the quiz questions, finishes first, mentions having finished to nobody in particular
- Low conscientiousness: opens the module, immediately looks for the skip button (there isn't one), leaves the tab open at the minimum time per slide, advances, gets the quiz right through process of elimination
- The Climber: completes it immediately upon assignment, mentions the completion in a context where it makes them look organized
- The Cynic: completes it at 11:47pm on the deadline day, submits, writes a note in the feedback field that is technically professional and absolutely not

**The solidarity moment:**
Two NPCs discover they're both doing the module at the same time. They make brief eye contact across the floor. One makes a face. The other makes the same face back. This is a genuine trust-building event — they've shared an authentic moment at the organization's expense. The trust bump is small, unprompted, and real.

**The person who fails the quiz:**
Someone fails the quiz. This is genuinely difficult. The answers are right there. They have to retake it. They tell no one. It is in their ComplianceStateComponent forever.

**The extreme end:**
The deadline is Friday. It is Thursday at 4:45pm. Eleven NPCs are in the module simultaneously. The shared server that hosts the module slows to a crawl. Slides take forty-five seconds to load. Three NPCs get knocked back to slide one by a session timeout. The fire-safety quiz has a question about an extinguisher type that doesn't match any extinguisher in the building. Someone gets it wrong. The Founder's Nephew emails HR to ask if there's a waiver. There is not a waiver. He completes the module at 9:40am Friday, seventeen minutes before the deadline, and considers this close but acceptable.

---

## Synthesis: the new layer

The first document's systems made the body matter in the office. These systems make *time* matter — the calendar, the milestones, the events that are anticipated and then survived. They also make *information* matter in a different way: not just what people know, but what they have to hold, suppress, or act as if they don't know.

What these systems interact into is an office with a **history** — and a population that's shaped by that history in ways that aren't always legible but are always mechanically real. The Newbie enters a room and inherits a charge they can't see. The promoted NPC navigates a social recalibration that nobody is acknowledging. The holder of the secret watches the name get closer to the surface.

None of this is authored. It compounds from systems running.

---

## Open threads

- **The exit interview:** When an NPC leaves (voluntary or not), the exit interview is a unique social context — formal, bounded, finally safe to say certain things. What gets said in the exit interview should enter KnowledgeComponent for the interviewer (HR NPC) and eventually diffuse. What doesn't get said is also information.
- **The institutional memory:** The Old Hand knows things about this office that are not in any document. When they leave, that knowledge leaves with them. The building is subtly less navigable for a period after. Can the engine model this?
- **The reference call:** When an NPC leaves and later someone is asked about them — the reference. What do they say? The KnowledgeComponent has everything. The FavorLedgerComponent has the accounting. The relationship is what determines the call.
- **The all-hands meeting:** Distinct from a regular meeting. The entire building in one room. Different floor populations mixed. Formal context, but with the whole ensemble present. A different kind of event — information officially disseminated, reactions immediately visible, a moment where the building knows what it knows together.
