<!--
Copy this file to docs/playtest/PT-NNN-<short-slug>.md (next monotonic NNN).
Delete this comment block. Fill in every section. Keep it terse — feel observation
matters more than prose. Bugs filed below are appended verbatim to docs/known-bugs.md.
-->

# PT-NNN — <short slug describing focus>

**Date:** YYYY-MM-DD
**Duration:** ~XX minutes
**Build:** <git SHA of HEAD at session start>
**Scene:** `Assets/Scenes/PlaytestScene.unity`
**Focus:** <one sentence — e.g., "first end-to-end choke→rescue→bereavement loop">

---

## Setup

- NPC count: NN
- Pre-session scenario seeds (if any):
  ```
  scenario seed-stains 5
  scenario chore-microwave-to Donna
  ```
- Time started in-sim: <morning / midday / dusk / night / specific HH:MM>
- Soften toggle: on / off
- Time speed used: <pause / ×1 / ×4 / ×16 — list switches as they happened>

---

## What I did

<2–6 sentences of plain narrative. The story of the session.>

---

## What I observed

### Worked as expected

- <bullet>
- <bullet>

### Felt off / wrong / surprising

- <bullet — feel observation; not necessarily a bug. Examples:
  - "Selection halo lags the NPC by one frame at ×4 — readable but feels mushy."
  - "Donna's chore-refusal grumble doesn't synch with her CoughingFit start; one or the other should yield." >

### Confirmed bugs (filed below)

- BUG-NNN: <title>
- BUG-NNN: <title>

### Open questions for Opus / Talon

<Anything play surfaced that the bibles don't decide.>

---

## Bugs filed this session

<!--
Each entry below is appended verbatim to docs/known-bugs.md with the next
monotonic BUG-NNN id. Keep the shape exact so the append is a copy-paste.
-->

### BUG-NNN: <title>

**Symptom:** <one-sentence observable defect>

**Repro:**
1. <step>
2. <step>
3. <expected vs actual>

**Severity:** Critical / High / Medium / Low

**Files relevant (if known):** <paths or n/a>

**Suggested fix wave:** <e.g., "inline 4.0.x fix" / "build mode v2" / "polish wave" / "v2 — defer">

**Workaround (if any):** <text or n/a>

**Discovered in:** PT-NNN

---

## Performance notes

- FPS observed: <range, e.g., 58–62>
- Frame stutters: <none / list with timestamps and triggering events>
- Audio glitches: <none / list>
- Memory pressure (subjective — Editor responsiveness): <fine / sluggish / hitchy>

---

## Phase 3 surface coverage this session

Tick what was actually exercised. Untouched surfaces are fine — sessions have focus.

**Engine systems:**

- [ ] Life-state — `Choked`
- [ ] Life-state — `SlippedAndFell`
- [ ] Life-state — `StarvedAlone`
- [ ] Life-state — `Died` (general)
- [ ] Fainting (Incapacitated → Alive recovery alone)
- [ ] Rescue — Heimlich
- [ ] Rescue — CPR
- [ ] Rescue — door-unlock
- [ ] Bereavement cascade
- [ ] Live mutation — build placement triggers re-pathing
- [ ] Sound triggers — `Cough`
- [ ] Sound triggers — `ChairSqueak`
- [ ] Sound triggers — `BulbBuzz`
- [ ] Sound triggers — `Footstep`
- [ ] Sound triggers — `SpeechFragment`
- [ ] Sound triggers — `Crash` / `Glass` / `Thud`
- [ ] Sound triggers — `Heimlich` / `DoorUnlock`
- [ ] Physics — break event
- [ ] Chore rotation — refusal observed
- [ ] Animation — Eating / Drinking / Working / Crying / CoughingFit / Heimlich
- [ ] Save mid-state, load, verify identity

**Unity host surface:**

- [ ] Camera — pan / rotate / zoom / recenter
- [ ] Camera — wall-fade-on-occlusion
- [ ] Selection — single-click glance
- [ ] Selection — drill (one click deeper)
- [ ] Selection — deep (one click further)
- [ ] Build mode — enter / exit
- [ ] Build mode — place wall / door / prop
- [ ] Build mode — ghost preview valid / invalid tints
- [ ] Time HUD — pause / ×1 / ×4 / ×16 cycle
- [ ] Event log — open, filter
- [ ] Dev console — open, scenario verbs reachable
- [ ] Soften toggle — verified reachable

---

## Next session focus suggestion

<One sentence — what to exercise next time. Becomes PT-(NNN+1)'s focus unless a Critical bug rerouted dispatch.>

---

## Packets touched (if any feel-verified-by-playtest packets are evaluated this session)

<!--
For phase packets that shipped with the `feel-verified-by-playtest` flag, this
session is the formal acceptance evaluation. List them.
Example:
  - WP-4.0.2 (shader pipeline) — verified: pixel-art-from-3D look reads correctly
    at ×1 and ×4; no stutter on time-control switch. ACCEPTED.
  - WP-4.1.0 (voice profiles) — partial: Old Hand and Newbie distinguishable;
    Climber sounds wrong (filed as BUG-NNN). RETURN FOR FIX.
-->

- <packet-id> — <verdict: ACCEPTED / RETURN FOR FIX / DEFERRED / N/A>: <one-line note>
