# 11 — Power, Favors, and Micro-Tyranny

> Real office power is rarely the org chart. It's who has the keys, who owns the calendar, who controls the printer toner closet, who runs the meeting that runs the floor. Tiny gatekeepers wield enormous local authority. The Founder's Nephew defies the org chart in the other direction. Favor accounting (already in `new-systems-ideas.md`) is half of this; the other half is *positions* that confer informal power.

---

## What's already in the engine that this builds on

- The existing `FavorEconomySystem` and `FavorLedgerComponent`.
- The existing `WorkloadSystem`, `MeetingSystem`, `CaptiveComponent`.
- `KnowledgeComponent`, `RumorSystem`.
- The Founder's Nephew, Climber, Old Hand, Cynic archetypes — already capture pieces of this.

---

## 11.1 — GatekeeperPositionSystem

### The human truth

Specific positions in the office come with informal power that's totally disconnected from the formal title. The receptionist who controls who gets to whom. The HR person who knows everyone's salary. The IT admin (Greg) who has access to everyone's machine. The office manager who controls the supply order. The exec assistant who controls the boss's calendar. These positions confer a particular kind of authority — quiet, persistent, and known.

### What it does

`GatekeeperRole` is an attribute attached to specific NPCs based on role:

- `kind` — `receptionist | scheduler | calendarKeeper | itAccess | hrDataAccess | supplyOrder | keyHolder | financeApprover`.
- `accessRadius` — what they can see, control, or modify. This is meta-data the engine reads when other NPCs request information or services.
- `usageStyle` — `helpful | neutral | obstructive | weaponizing`. Most gatekeepers are helpful or neutral; a minority are obstructive (passive-aggressive denial); the rare gatekeeper actively weaponizes the role.

When other NPCs need something the gatekeeper controls, a `GatekeeperRequestEvent` fires. The gatekeeper decides:

- **Accept readily.** Standard.
- **Accept with friction.** Slight delay, slight extra paperwork, slight gatekeeping.
- **Defer.** "Can you check back in a couple hours."
- **Deny.** Process-based or judgment-based.

The cumulative pattern of accept/defer/deny across an NPC's gatekeeper interactions is calcified knowledge — *Karen takes forever to approve expense reports* becomes part of the floor's understanding of Karen.

### Cross-system interactions

- **FavorLedgerComponent:** gatekeepers accumulate massive favor balances. Most don't call them in; the ones who do, do so quietly.
- **PassiveAggression:** obstructive gatekeeping is a passive-aggressive output channel.
- **WorkloadSystem:** gatekeeper friction adds task time. Days when the supplier-of-supplies is in a bad mood are days the office is slower.
- **KnowledgeComponent:** gatekeepers *know things* — salaries, schedules, who's been escalated. Their KnowledgeComponent is heavier than most.

### Personality + archetype gates

- The Vent (Donna) as receptionist: helpful with style, gossip-dense, slow on actual paperwork.
- The Hermit (Greg) as IT: neutral by default, friction only when his focus is being broken (cluster 12 — `flowState` interruption-cost as gatekeeper friction).
- The Climber as calendar-keeper-of-the-CEO: weaponizing in a status-strategic way.
- The Cynic in any gatekeeper role: equal-opportunity friction. Doesn't favor anyone.

### What the player sees

The supply request that took three days. The expense report that came back twice. The meeting with the CEO that the assistant *somehow* couldn't fit on the calendar. These are concrete game-state delays the player can read.

### The extreme end

The Climber wants face-time with the CEO. The CEO's calendar-keeper has been stiffed by the Climber socially over the last six months (small slights, dismissive emails). The calendar-keeper *cannot* find time on the calendar. Weeks pass. The Climber escalates. The calendar-keeper mentions, tactfully, that they're juggling many priorities. The Climber finally figures out what's happening and adjusts behavior; the meeting magically appears on the calendar two weeks later.

---

## 11.2 — KeyHolderSystem (the literal keys)

### The human truth

The office has things behind locks. The supply closet has certain items behind a smaller lock. The conference room AV cabinet. The petty cash drawer. The CEO's office. The keys are physical, and somebody has them. They are loaned out reluctantly. The keys are *micro-power*.

### What it does

`KeyEntity` per lockable area. Keys have a `holder: NPCId`. Borrowing requires a request event; the holder approves or not.

The accumulation pattern is meaningful: an NPC who's gradually become the de-facto holder of three keys has accumulated quiet power. They know they have. So does the floor.

### Cross-system interactions

- **FavorLedgerComponent:** key-loans build favor balance.
- **TerritorySystem (cluster 04.4):** key control is territorial.

### What the player sees

The single NPC the floor approaches with key requests. The exchanges where the request is denied with a smile.

### The extreme end

The Old Hand has the only physical key to a 1980s file cabinet that contains records nobody has needed in years. Last quarter, an audit asked for those records. Old Hand opened the cabinet. Now several other NPCs *want* to know what's in there. Old Hand says they'll show them later. They never do. The audit is over; the curiosity persists; the cabinet is power.

---

## 11.3 — CalendarControlSystem

### The human truth

Whoever controls the calendar controls the day. The meeting-organizer. The recurring-meeting setter. The double-booker who forces priorities. Time as a contested resource is most political at the calendar layer.

### What it does

Each meeting has an `Organizer`. The organizer's `calendarPower`:

- They can schedule meetings without consulting the participants' availability deeply (the brute-force schedule).
- They can move meetings.
- They can extend meetings with a single message.

The `calendarPower` translates to real costs to participants — captured time, broken flow, missed lunch.

### Cross-system interactions

- **MeetingSystem (existing):** captivity cost is paid by participants; organizer pays nothing for organizing.
- **CovertResistanceSystem (11.4 below):** participants in chronically-double-booking organizer's meetings begin no-showing or arriving late as quiet protest.

### What the player sees

Which NPC organizes most of the meetings. Their meetings are different in tone. The organizer rarely realizes how much disruption they create.

### The extreme end

The Climber organizes daily 9am stand-ups. Six months in, the floor's collective irritation toward standing meetings is calcified into permanent low-grade resentment. Three NPCs have started consistently arriving five minutes late. Two have stopped attending under invented excuses. The Climber doesn't connect any of this to themselves. The chronicle records the meetings as a slowly-failing institution.

---

## 11.4 — CovertResistanceSystem

### The human truth

When an NPC has a problem with another NPC's authority — formal or informal — direct confrontation is too costly. Instead they resist *quietly*: arriving late to the recurring meeting, "forgetting" the deliverable, slow-walking the email response, taking a different route through the floor to avoid passing them, going to lunch right when they want to talk.

### What it does

`ResistanceLink` between two NPCs (directed):

- `target` — who's being resisted.
- `intensity` — 0–100.
- `methods[]` — which behaviors are active (late-arrivals, silent-noncompliance, avoidance).

Resistance accumulates from chronic friction. Most resistance is unconscious — the NPC isn't *deciding* to resist, their behavior just drifts that way. Some is deliberate. Both produce observable patterns.

### Cross-system interactions

- **MeetingSystem:** chronic-late-arrivals to specific organizer's meetings is a covert-resistance signature.
- **WorkloadSystem:** slow-walking deliverables to specific requesters.
- **MovementSystem:** path-deviation around target's desk.
- **AversionLink (cluster 03.5):** covert resistance often has an aversion-link underneath.

### What the player sees

A floor where some NPCs always seem to *not quite* deliver for some other NPCs. The pattern is observable; the cause is rarely named.

### The extreme end

The Climber's project depends on three coworkers' contributions. All three have low-intensity resistance links to the Climber that they don't understand they have. Deadlines are missed. Status updates arrive incomplete. The Climber escalates; the floor's resistance hardens; the project fails. The Climber blames them. They blame the Climber. None of them know that the resistance was the system, not any single decision.

---

## 11.5 — SympathyHireAwareness

### The human truth

Most offices have one or two NPCs who are *known* to be there for non-merit reasons. The Founder's Nephew is the canonical case in the cast bible. Beyond him: the sympathy hire (a friend's family member who needed work), the political appointee, the legacy hire (taking over for a retired parent). The floor knows. Their behavior toward this NPC is shaped by knowing.

### What it does

`HireProvenance` per NPC: `{normal | founder_relative | sympathy | political | legacy | nepotism}`. Most are `normal`. The flag is *known* to the office (or partially known) and modulates behavior:

- Coworkers are *less* willing to share critical feedback (futile).
- Coworkers are *less* willing to depend on them (won't deliver under pressure, may not be expected to).
- The provenance NPC may or may not know they're in this category. Awareness flag.
- The provenance NPC may *over*-perform to compensate (rare) or *under*-perform with impunity (Founder's Nephew default).

### Cross-system interactions

- **WorkloadSystem:** assignments adjust around the provenance NPC.
- **FavorLedgerComponent:** they accumulate negative balances and the floor doesn't try to collect.
- **ResistanceLink:** chronic frustration with the protected NPC accumulates resistance links the floor cannot direct upward.

### What the player sees

The slow accommodation pattern. The double standard.

### The extreme end

The Founder's Nephew skips a critical deliverable. The deadline slips. The Climber, whose career depends on the project, almost takes the fall. They figure out the chain. The Climber's `irritation` toward the Founder's Nephew calcifies into permanent contempt. They never confront him directly because they can't. The contempt leaks through micro-actions for the rest of the save. The Founder's Nephew never notices.

---

## 11.6 — InformationAsymmetrySystem

### The human truth

In any office, some NPCs know things others don't. The HR person knows salaries. The exec assistant knows the merger discussion. The IT admin knows the email logs. The Old Hand knows where the bodies are buried. Information is power; *strategic information* is calibrated power. Some NPCs use it; some hold it; some leak it; some weaponize it.

### What it does

Extends the existing `KnowledgeComponent` with a `senstivity` field per fact (`mundane | private | confidential | toxic`). NPCs who hold high-sensitivity facts have *meta-knowledge* about who else knows what.

The strategic decisions:

- **Hold quietly** — the fact is privileged; the holder uses it as ambient leverage. Highest-status holding.
- **Trade for favor** — disclose to a specific party in exchange for something.
- **Leak strategically** — disclose to a third party knowing it will reach the target.
- **Burn** — public disclosure. Rare, costly, sometimes necessary.

### Cross-system interactions

- **RumorSystem (existing):** the rumor system handles spread; this system handles the *holding* and *strategic decisions*.
- **EavesdropSystem:** information-asymmetry positions are fed continuously by overheard content.
- **FavorLedgerComponent:** information-trade is a high-yield favor mechanism.

### What the player sees

The Old Hand's quiet suggestions that turn out to be load-bearing. The HR person's careful neutrality. The IT admin's silence on what they obviously know.

### The extreme end

Greg knows about a layoff list two weeks before it's announced (he configured the email distribution system). He tells nobody. Two weeks later, three coworkers he likes are blindsided. He could have warned them. He didn't because of his confidentiality ethic. The layoffs proceed. Greg's mask is at 30% all week. None of the affected coworkers ever learn that Greg knew. He carries it.

---

## 11.7 — MicroTyranny / The PettyAuthorityCharacter

### The human truth

Some characters in offices wield small-radius authority disproportionately and cruelly. The break-room thermostat-toucher who turns it down whenever they enter. The receptionist who decides which packages are for "really" the right person. The supply-closet enforcer who logs every request. These are the NPCs who *enjoy* the small power and use it to compensate for status they don't have elsewhere.

### What it does

`MicroTyrannyDisposition` per NPC (most run zero):

- `enjoymentLevel` — how much they enjoy minor authority.
- `domains[]` — which specific micro-power domains they exercise.

This modulates their behavior in their gatekeeper role (11.1) — they're more likely to obstruct, more likely to demand process, more likely to remember slights against requesters.

### Cross-system interactions

- **GatekeeperPositionSystem:** intensifies obstructive style.
- **AversionLink:** targets of micro-tyranny accumulate aversion links.
- **PassiveAggression:** micro-tyrants generate passive-aggressive responses from below.

### What the player sees

A specific NPC who's *worse* than they should be in their role. The friction they generate accumulates around them.

### The extreme end

The supply-closet enforcer becomes a load-bearing antagonist for the floor. They're not formally important. They make small daily decisions that harm specific coworkers. Three coworkers conspire (cluster 16's emergent culture) to start ordering their own supplies via personal expense, bypassing the enforcer. The enforcer's role is gradually circumvented. They become resentful in turn. The cycle escalates until management notices and either ignores or restructures.

---

## 11.8 — UpwardManagementBlindness

### The human truth

Bosses don't see what reports do unless reports show them. Reports curate what the boss sees. The boss's view of the office is partial and shaped by their reports' filtering. The Climber understands this and exploits it; the Cynic understands it and refuses to play; the Newbie hasn't figured it out yet.

### What it does

Each NPC has an `UpwardSignal` channel — the set of communications they send up the hierarchy. This is curated, calibrated, and selectively-honest.

The `bossKnowsState` for each report-pair tracks what the boss has been told vs. what's actually true. Gaps accumulate. Eventually one of:

- **Confrontation event** — boss discovers a major undisclosed truth. Confrontation arc fires.
- **Mistake event** — boss makes a decision based on bad information; consequences propagate.
- **Steady state** — most pairs run in steady gap-state indefinitely.

### Cross-system interactions

- **PassiveAggression:** the strategic CC and the BCC-the-boss email patterns are upward-management moves.
- **InformationAsymmetry:** part of what the boss doesn't know is what reports collectively know about each other.

### What the player sees

The slow drift between what the player sees as the simulation truth and what the management characters seem to know. Sometimes the gap closes; sometimes it widens; sometimes it explodes.

### The extreme end

The Founder's Nephew has been telling the CEO that everything is fine on his project. Nothing is fine. The project is six months behind. The CEO finds out from a customer email. The fallout is one of the highest-impact chronicled events of the save — promotions are reversed, a Climber benefits, the Nephew is finally fired (because the *customer* knows), and the office's relationship to the CEO permanently shifts based on how the CEO handled it.
