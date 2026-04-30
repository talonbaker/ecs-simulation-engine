# 17 — Interaction Matrix

> The previous sixteen clusters describe individual systems. This doc is the cross-cluster view: which proposed systems multiply each other, which ones multiply existing engine systems, and where the compound effects produce stories the individual systems wouldn't.

---

## Reading this matrix

Each section below picks an existing engine concept and lists which new proposals from this folder *act on* it, *are gated by* it, or *compound with* it. The goal is to make it visible when one new system implies the need for another, and where seemingly-distant proposals actually share a substrate.

---

## Substrate: `SocialMaskComponent` (existing)

The mask is the most-shared substrate in the folder. Almost every proposal draws mask, restores mask, or makes mask cost legible.

**Things that draw mask faster:**

- 01.1 Craving deficit climb (especially `agitated` → `snapping`).
- 01.3 Compulsion disruption (the moved stapler).
- 02.1 Flatulence suppression in captive contexts.
- 02.3 Hygiene drift in social proximity.
- 02.5 Menstrual day-1 + presentation requirement.
- 03.1 Pest sighting under composure-required context.
- 03.2 Phobia trigger.
- 03.3 Panic attack (collapse).
- 04.1 Adjacent-desk cleanliness mismatch.
- 05.1 Bubble violation (sustained).
- 05.5 Elevator with charged occupant.
- 06.1 Religious observance during incompatible office context.
- 06.4 Code-switching (the gap between home and office register).
- 07.1 Misophonia trigger sustained.
- 08.1 Email tone-misread feedback loop.
- 09.1 Bad-evening home-state bleed.
- 09.3 In-progress divorce mid-day.
- 11.1 Gatekeeper friction received.
- 12.4 Substance-adjacency exposure for the recovering NPC.
- 14.1 Unspoken-tension proximity.

**Things that restore mask faster:**

- 02.4 Bathroom as refuge.
- 18 (existing) Threshold spaces.
- 09.6 Cluster of post-event recovery moments.

**Mask-slip events that produce high-noteworthiness output:**

- 01.1 Craving snapping point.
- 02.7 Tears at desk.
- 03.3 Panic-attack mid-meeting.
- 06.4 Home-register slip in office context.
- 09.3 Disclosure of divorce mid-conversation.
- 12.1 Drunk-at-holiday-party honesty.

**Implication.** Implementing the mask system is the single highest-leverage engineering investment that flows into all of these. The proposed `mask-slip` dialog context (per the dialog bible's open question) becomes a core retrieval branch.

---

## Substrate: `KnowledgeComponent` + `RumorSystem` (existing)

The information layer is what makes most of these systems narratively legible.

**Sources of new high-confidence facts:**

- 02.1 Audible-3 fart (floor-wide).
- 02.5 Visible-leak event.
- 02.7 Tears-at-desk.
- 02.8 Visible-body events (faint, vomit, choke).
- 03.1 Pest events.
- 03.4 Fainting.
- 06.5 Attire signal (visible).
- 08.1 Reply-all disaster (forwarded content).
- 09.5 Death.
- 14 Romance arcs becoming visible.
- 15.1 Ring change.

**Sources of low-confidence inferences (rumor mill input):**

- 02.3 Hygiene drift cause unknown.
- 06.6 Socioeconomic pressure inferences.
- 09.1 Home-state bleed inferences.
- 09.2 Sick-family inferences.
- 14.1 Unspoken tension observability.
- 15.2 Visible weight change cause unknown.

**Calcification of facts as floor knowledge:**

- 16 Floor-culture references derived from chronicle events.
- 16.7 Folklore items.
- 10.5 Anniversary date awareness (the people who *know* what date it is).

**Implication.** The KnowledgeComponent + RumorSystem pair is doing extraordinary load-bearing work for these proposals. A facts-with-sources-and-confidences model is *the* substrate for most of the cluster's narrative output.

---

## Substrate: `inhibitions` (action-gating bible)

New inhibition classes implied by this folder:

- `flatulence` (cluster 02.1).
- `bodyExpression` (cluster 02.5 — naming what's happening physically).
- `phobia(class)` per cluster 03.2.
- `confrontation` (already exists; multiple clusters depend on it).
- `disclosure` (clusters 09.2/9.3 — telling coworkers what's happening at home).
- `infidelity` (existing; cluster 14 leans hard on it).
- `substance` (cluster 12).
- `vulnerability` (existing; cluster 03.5 + 14.7 + others lean on it).

**Implication.** The inhibition system can carry most of these as *strength + class + awareness* tuples without architectural changes. The class enum just grows.

---

## Substrate: Proximity event bus + ranges (existing)

New event types proposed across clusters:

- `CraveSatisfiedEvent` (01.1).
- `TicEvent` (01.2).
- `CompulsionDisruptedEvent` (01.3).
- `FlatulenceEvent` (02.1) — with `Audible: 1|2|3`.
- `StomachNoiseEvent` (02.6).
- `BathroomVisitEvent` (02.4).
- `PestSightingEvent` / `PestHandledEvent` (03.1).
- `PhobiaTriggerEvent` (03.2).
- `PanicAttackEvent` lifecycle (03.3).
- `FaintingEvent` (03.4).
- `BoundaryCrossingEvent` (04.1).
- `MissingItemEvent` (04.2).
- `BubbleViolationEvent` (05.1).
- `TouchEvent` (5.2).
- `HoverEvent` / `OverShoulderEvent` (5.3).
- `HallwayEncounterEvent` (5.4).
- `ElevatorOccupancyEvent` (5.5).
- `AcousticTriggerEvent` (07.1).
- `LeakingMusicEvent` (07.5).
- `EmailComposedEvent` / `EmailReadEvent` / `ToneMisreadEvent` (08.1).
- `RingtoneEvent` (08.6).
- `ArrivalBleedEvent` / `DepartureBleedEvent` (09.1).
- `MedicalEmergencyEvent` (09.4).
- `CardHandoff` (10.1).
- `GoingAwayEvent` (10.2).
- `GatekeeperRequestEvent` (11.1).
- `AlcoholConsumptionEvent` (12.1).
- `AwkwardSilenceEvent` (13.1).
- `SlowGoodbyeEvent` (13.2).
- `GreetingCalibrationEvent` (13.3).
- `HijackEvent` (13.4).
- `InterruptionEtiquetteEvent` (13.6).
- `RetoldEvent` (13.7).
- `SilentExchangeEvent` (13.8).
- `AccidentalContactEvent` (14.5).
- `RingChangeEvent` (15.1).
- `HaircutNoticeEvent` (15.3).
- `EyewearChangeEvent` (15.5).
- `CallbackEvent` (16.2).

**Implication.** A unified event bus already exists in the engine. These events all fit the existing shape; what's needed is event-class registration + per-class subscriber wiring, not new infrastructure.

---

## Cross-system multiplications: the high-yield combinations

These are the pairs where two new systems multiply each other to produce simulation-scale stories.

### 1. CravingSystem × MeetingSystem captivity

A 90-minute meeting is multiple deficit climbs for any cyclic craver. The longer the meeting, the more dramatic the post-meeting behavior. The mask-slip after a captive smoker finally exits is one of the most reliable scene-generators the cluster produces.

### 2. SocialMaskComponent × HomeStateBleed × DialogCalcify

An NPC who arrives at low mask baseline, carries it through the day, eventually slips, and produces a high-noteworthiness fragment that calcifies. The full pipeline runs from car-to-cubicle through chronicle entry. Single most-load-bearing chain in the cluster.

### 3. PhobiaComponent × MedicalEmergencyEvent × FavorLedgerComponent

The medical emergency tests the floor's response. Phobic NPCs freeze; non-phobic NPCs respond; the responders accumulate massive favor balance with the affected NPC permanently. The same event reads differently for each witness.

### 4. UnspokenTensionLink × HolidayParty × AlcoholSystem

The holiday party is the highest-leverage venue for any unspoken-tension pair. Alcohol drops mask. Touch-system boundaries loosen. The outcome — escalation, retreat, or no-change — is generated, not authored.

### 5. EavesdropSystem (existing) × InformationAsymmetrySystem × Power

Greg in the IT closet, who knows everything because of his role and his physical position, becomes the floor's silent information node. His decisions about what to share — never theatrical, often invisible — shape entire arcs.

### 6. AppearanceComponent × HomeStateBleed × KnowledgeComponent (other NPCs)

The slow visible decline of an NPC under stress that the floor *reads* without naming. Some NPCs help; some withdraw; some observe and do nothing. The character-test for the entire floor is the response calibration.

### 7. CalcifiedRitual (10.8) × TurnoverEvent × FolkloreLayer (16.7)

A long-tenured NPC's departure permanently shifts the floor's culture. Their tics calcify into folklore. Some rituals die; some persist; new ones emerge. The save's office is *measurably different* before and after.

### 8. RecoveringArchetype × SubstanceAdjacencyExposure × QuitAttemptArc × HolidayParty

The Recovering archetype's holiday-party night is the single highest-stakes evening of any save where they exist. Every substance system hits them simultaneously. The outcome is the entire arc condensed into one evening.

### 9. AffairArchetype × PrivateInsideJokes × FloorCultureCalcification

The Affair partners' private references *leaking* into recognizable patterns is how the floor learns what's happening. The information chain runs from private joke → floor recognition → rumor → confrontation.

### 10. MaskSlipDialogContext × CalcifyMechanism × FolkloreLayer

The rare mask-slip generates a high-noteworthiness fragment. That fragment calcifies hard. Years later it's part of folklore. *"Remember when X said Y."* The pipeline runs from the moment to the legend.

---

## Implementation ordering suggestions

If only a fraction of the cluster ships, this is a rough priority based on multiplicative leverage:

1. **Mask-slip dialog context + calcify integration.** Single highest-leverage. Most narrative output flows here.
2. **CravingSystem (01.1) + integration with MeetingSystem captivity.** Cheap, high return.
3. **Pest/phobia/fainting (cluster 03).** The character-test surface for the entire floor. Memorable.
4. **HomeStateBleed (09.1).** Once this exists, every morning has different texture for free.
5. **EmailSystem with tone-misread (08.1).** The information substrate for the office's official record.
6. **Floor-culture calcification (16.1).** Saves accumulate identity over time.
7. **DeskState + ArtifactDrift (04.1, 04.2).** Cheap to render; high readability.
8. **Touch system (05.2).** Required for anything in cluster 14 to land.
9. **Ring axis (15.1).** Tiny implementation; massive narrative payoff.
10. **Bathroom-as-refuge (02.4).** Required for many other proposals.

Lower priority (more expensive or narrower utility):

- Cluster 11 (power systems) — high impact but requires more org-chart structure.
- Cluster 06 (culture/religion) — tone-sensitive, high authoring requirement.
- 12.2 prescription pills — narrow utility, can be sketched abstractly.

---

## Things this cluster does NOT need

- A new component model. ECS as it stands handles all of this.
- A new persistence layer. The chronicle is sufficient.
- A new dialog model. The corpus + calcify mechanism handles dialog.
- LLM-generated content. Every proposal stays within the offline-fast-deterministic guarantees per SRD axiom 8.1.

The overwhelming majority of the cluster is **content + tuning**, not new architecture. The architectural seeds are already in the engine.

---

## Open meta-questions

- **Frequency tuning.** The cluster proposes many systems whose output frequency must be carefully calibrated to keep the office *recognizable* rather than chaotic. What's the tuning surface? Recommend: per-system probability multipliers in `SimConfig.json`, with conservative defaults and a player-facing "intensity" knob.
- **Tone discipline at scale.** A few systems (cluster 06 religion, 02.5 menstrual, 12 substance, 14 romance) carry tone-discipline cost. Each needs review at content-authoring time, not just at architecture time.
- **Player visibility.** What does the player *see* of the simulation's internal state? Debug overlay vs. all-inferential. Recommend: phase-1 debug overlay; phase-2+ shifts toward inferential reading.
- **Save-shape variability.** The cluster's leverage is in its *combinatorial* output. Two saves should be different in fundamentally narrative ways, not just stat-different. The interaction matrix above is what produces this. The risk is *flat outputs* — every save runs the same patterns. The mitigation is per-NPC variance in the gates (inhibitions, deals, archetype layering).
