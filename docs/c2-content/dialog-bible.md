# Dialog Bible — Working Draft

> Co-authored by Talon and Opus. Captures the principle that emotional state is content and spoken language is decoration. Read alongside the cast bible (drives, register, archetypes) and the action-gating bible (willpower, inhibitions). The dialog system implements the cast bible's commitment to "voice emerges from gameplay, no pre-authored catchphrases."

---

## The thesis

Words don't tell the story. The exchange of feelings through observable behavior tells the story. When NPCs speak, the spoken content is decorative — colorant, rhythm, register — but the *meaning* of an interaction is carried by the social-state changes it produces. A conversation lands not because someone said the perfect line; it lands because someone's `irritation` jumped 30 points and they walked out abruptly, and another NPC saw it.

This is the Sims principle. The Sims got it right by going Simlish — refusing to commit to specific words at all, leaning all the way into "language is decorative." This game can be more legible than Simlish (real English fragments, register-appropriate, calcifying into tics), without crossing into generative-LM territory and without giving up the offline-fast-deterministic guarantees the SRD axiom 8.1 protects.

Voice in this game is **selection from a curated palette under emotional pressure with calcification.** Not generation. Not authoring per-NPC. Not a black-box model. A decision tree that picks phrases based on emotional state, an authoring corpus reviewable by hand, and a calcify mechanism that turns repeated selections into a character's signature.

---

## The phrase corpus

The corpus is a flat collection of **phrase fragments** — short utterances 1–10 words long. Each fragment carries metadata describing when it's appropriate. The shape of a single fragment:

- `id` — stable identifier; never reused.
- `text` — the actual English phrase. `"I dunno, whatever."`, `"That's interesting actually."`, `"Could you not."`, `"Mm."`
- `register` — the cast bible's six values: `formal | casual | crass | clipped | academic | folksy`.
- `context` — what the fragment is *for*: greeting, refusal, agreement, complaint, flirt, deflect, lash-out, share, brush-off, encouragement, thanks, apology, acknowledge. Roughly 10–15 contexts.
- `valenceProfile` — a vector of preferred social-drive values for the speaker. `{"irritation": "high", "affection": "low"}` means this fragment fits when the speaker is irritated and not feeling affectionate. Values are ordinal: `low | mid | high`. Most drives are unspecified (any value works).
- `relationshipFit` — optional preference for who the listener is: `closeFriend | colleague | rival | stranger | romantic | superior | subordinate`. Multiple fits allowed; absence means any-listener.
- `noteworthiness` — `0..100`. How memorable the phrase is when uttered. `"Mm."` is 5; `"You know what, fine."` is 40; `"I never want to see you again."` is 95. Shapes whether a calcify event fires.

**Authoring scope.** ~200–400 fragments at v0.1 of the corpus. Distributed roughly:
- Every register × every context combination has 2–4 fragments. 6 × 13 × 3 ≈ 234 fragments.
- Higher-noteworthiness contexts (lash-out, flirt, share-something-personal) get extra coverage; lower-noteworthiness (brush-off, ack) get fewer.

This is hand-authorable. Talon or a Sonnet under tight specification produces it once and iterates over time.

---

## Decision-tree retrieval

When an NPC has a "dialog moment" (a proximity-driven event fires, e.g., `EnteredConversationRange` from WP-1.1.A's bus), the dialog system selects a fragment:

1. **Determine the context.** Action-selection has decided what this NPC wants to express — based on drives, willpower, inhibitions per the action-gating bible. This produces a `context` value (`lash-out`, `share`, `deflect`, etc.).

2. **Filter by register.** Use the speaker NPC's `PersonalityComponent.VocabularyRegister`. Fragments outside that register are excluded.

3. **Filter by relationship fit.** If the listener is a `closeFriend` and a fragment specifies `relationshipFit: rival`, exclude it. Most fragments don't specify, so this is a permissive filter.

4. **Score by valence proximity.** Each remaining fragment gets a score: count how many of its `valenceProfile` constraints match the speaker's current drive values (mapped from 0–100 to `low/mid/high` by SimConfig thresholds). Higher matches = higher score.

5. **Score by recency penalty.** Per-NPC track which fragments have been recently used (within the last 5 sim-minutes). Penalize recent fragments to avoid immediate repetition. The penalty decays with elapsed game-time.

6. **Score by calcify bias.** Per-NPC track how many times each fragment has been used historically. If a fragment has crossed the calcify threshold (default 8 uses in similar emotional contexts), it gets a *positive* bias — this NPC favors its own established phrasings.

7. **Select.** Highest-weighted fragment wins. Ties broken by `id` ascending (deterministic).

The whole decision is microseconds. No model, no embedding, no GPU. A hash table lookup, a small filter pass, a weighted random pick.

---

## The calcify mechanism

Voice emerges through repetition. Specifically:

**Per-NPC fragment counter.** Each NPC carries a `DialogHistoryComponent` with a map `Dictionary<fragmentId, FragmentUseRecord>`. Each record holds:
- `useCount` — total times this NPC has said this fragment.
- `firstUseTick`, `lastUseTick` — bookend timestamps.
- `dominantContextValenceTuple` — the most common (context, valence-profile) tuple this fragment was used under.
- `calcified: bool` — set true when `useCount >= calcifyThreshold` AND the use distribution is concentrated (≥ 70% of uses in the same context).

**A fragment becomes a tic** when `calcified == true`. Once calcified:
- Future selections in similar contexts give this fragment a +30% score boost. The NPC favors their own established phrasings.
- The fragment is part of the NPC's *recognizable voice*. Other NPCs hearing it mark a `RecognizedTic` flag in their own `DialogHistoryComponent` for that speaker — the calcify mechanism propagates through the office.

**Tic propagation.** Per the cast bible, "Other NPCs can recognize it." A listener that has heard speaker X's calcified fragment ≥ 5 times has it marked as "X's thing." The listener's own retrieval gives a small +10% bias to using that fragment when speaking *to* X (mirroring) or *about* X (quoting). Tics spread, slowly, the way phrases spread in real offices.

**De-calcification.** A calcified fragment can lose its calcified status if not used for 30+ sim-days. Voice can shift over very long arcs.

---

## Save/load implications

`DialogHistoryComponent` is persistent state per the SRD axiom 8.2 — it travels in the saved `WorldStateDto`. A future schema bump (v0.5 or later) reserves `entities[].dialogHistory` for this. Until then, the component is engine-internal and saves are partial. Acceptable for the prototype phase; a content authoring concern, not a runtime blocker.

---

## What this is deliberately not

- **Not a spoken-line generator.** No fragment is *generated*. Every fragment is hand-authored. The system selects, doesn't write.
- **Not a per-NPC dialogue tree.** All NPCs share the corpus; their voices differ in *which* fragments calcify, not in which fragments exist.
- **Not an emotion-to-words mapper.** Action-selection decides the speaker wants to "lash out"; the dialog system picks an appropriate lash-out fragment. The word-level emotion mapping isn't at the dialog layer; it's at action-selection.
- **Not visible all the time.** Most "interactions where feelings get exchanged" in this game produce no spoken line at all. A glance, a posture shift, a walk-out — those are first-class. Spoken fragments are a fraction of all interactions, the same way spoken language is a fraction of human communication. The cast bible is explicit: emote more than speak.
- **Not generative AI.** No LLM, no embedding model, no inference pass. All deterministic, all reviewable, all bounded by the corpus.
- **Not Markov chains.** Markov on this corpus would produce ungrammatical, register-bleeding output. The deterministic decision tree is more legible.

---

## Tying it to the cast bible's archetypes

The cast bible's archetypes get extended to specify dialog hints:

- **The Vent** — register `casual` or `crass`. Contexts heavily weighted toward `share` and `complain`. High `valenceProfile` for `irritation` makes complaint fragments more likely to calcify into tics first.
- **The Hermit** — register `clipped`. Contexts mostly `brush-off`, `acknowledge`, occasional `share` when extracted. Most fragments will never calcify because they're rarely used; when one does, it's distinctive (Greg's `"Mm."`).
- **The Climber** — register `formal` in front of superiors, `casual` peer-to-peer. Heavy `flirt` and `agree` weighting. Fragments calcify quickly because the climber says the same things on purpose.
- **The Cynic** — register `clipped` or `casual`. Contexts skew `lash-out` (mild), `deflect`, `acknowledge`. Cynic tics are observational and dry.
- **The Newbie** — register `formal` early, drifts toward `casual` as belonging rises. Fragments calcify slowly because the newbie is still figuring out who they are.
- **The Old Hand** — register `folksy`. Calcified fragments dominate; old hands have established voice.
- **The Affair** — register inconsistent (code-switches between hidden and public modes). Fragments tagged as `flirt` calcify only with the affair partner; in front of others, the same NPC uses different fragments. This is the cast bible's "inconsistent register because they're code-switching" tic — captured in the system through *per-listener* fragment-use tracking, a small extension to `DialogHistoryComponent`.
- **The Recovering** — register `casual` outwardly; emotional fragments rare due to suppression (high willpower draw). Recovery moments occasionally produce intense, single-use fragments that calcify into rare tics.
- **The Founder's Nephew** — register `casual` regardless of formality context. Shamelessness produces a flat dialog profile.
- **The Crush** — register normal; valence-driven retrieval picks affection-heavy fragments around the target. The targeted NPC notices over time (proximity-bus signals) without being told.

These hints become part of the cast generator's spawn function: when an archetype produces an NPC, the NPC inherits both register preference and a calcify-priority tag (which contexts produce tics fastest for this archetype).

---

## What ships in the implementation packet

**Schema:**
- `corpus.schema.json` — defines a phrase corpus file.

**Engine components:**
- `DialogHistoryComponent` on each NPC.
- (Optional) `RecognizedTicComponent` for tracking other NPCs' tics.

**Engine systems:**
- `DialogContextDecisionSystem` — reads action-selection output, maps to `context`. (At v0.1, action-selection isn't fully landed; the system uses heuristics from drive deltas + proximity events.)
- `DialogFragmentRetrievalSystem` — runs the decision tree, picks a fragment, writes the chosen fragment id to a `SpokenFragmentEvent` on the proximity-event bus.
- `DialogCalcifySystem` — updates per-NPC use counts; flips `calcified` when thresholds hit.

**Content data:**
- `corpus-starter.json` — ~200 hand-authored fragments distributed per the authoring scope above. Sonnet-generated under tight specification, reviewed by Talon, iterated over time.

**SimConfig:**
- Recency window, calcify threshold, score weights for valence/recency/calcify bias, decay rates.

**Tests:**
- Decision-tree determinism (same NPC + same emotional state + same listener + same seed → same fragment).
- Calcify mechanism (8 uses in same context → calcified flag).
- Tic propagation (5 hearings → recognized tic on listener).

The dispatch packet is **WP-1.10.A — Dialog Implementation.**

---

## Open questions for revision

- **Corpus scope at v0.1.** 200 fragments enough? 400? Diminishing returns past 500 because most fragments will rarely be selected. Start at 200, add as gaps appear.
- **Per-listener fragment tracking.** Worthwhile? Adds memory cost (`Dictionary<(speakerId, listenerId), Dictionary<fragmentId, ...>>`). Might be cheaper to track only at the speaker level until per-listener differentiation actually surfaces in playtests.
- **Fragment authoring tooling.** A small Sonnet prompt for adding fragments to the corpus once the schema is stable. Could be a Phase-2 tool.
- **Off-corpus action.** When the corpus has no matching fragment, what does the NPC do? Probably: stay silent (emit no `SpokenFragmentEvent`), let the proximity event carry the interaction without dialogue. This is the silent-protagonist principle in action.
- **Calcify across saves.** Should calcified state persist? Yes — it's part of who the NPC is. The schema bump that adds `entities[].dialogHistory` to the wire format is on the v0.5+ roadmap.
- **Mask-slip as a dialog context**: when an NPC's willpower is depleted and they're in a high-exposure space, a mask-slip event could fire a dialog moment using a new context value `mask-slip` — producing an unusually candid or high-noteworthiness fragment outside the NPC's normal register. These would be rare, carry high `noteworthiness` scores, and are likely candidates to calcify into tics precisely because they only surface under specific conditions. Currently the corpus and context list have no slot for this.
- **Space-contingent register shifting**: should an NPC's vocabulary register shift based on the emotional valence of the room they're in? Mask-down spaces like the Smoking Bench could permit crass register even from formal NPCs; high-exposure spaces like the Conference Room could pull casual NPCs toward formal register. This would be a small weight modifier on register selection at retrieval time, driven by the named-anchor valence bundle (see world-bible open questions).
