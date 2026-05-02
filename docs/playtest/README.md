# Playtest Program

> **Status:** Live as of 2026-05-01. Continuous, parallel to phase development.
> **Authority:** `docs/PLAYTEST-PROGRAM-KICKOFF-BRIEF.md` is the founding doc. This README is the operational entry point.

---

## What this is

A continuous, structured manual-verification surface running in parallel with phase development. The xUnit suite verifies engine contracts. The Unity sandbox protocol verifies isolated visual primitives. **Neither can verify whether the integrated whole *feels* right** — and the 3.1.x bundle incident (eight packets compiled and tested clean against contracts, six follow-up fix commits required after Talon actually looked at the screen) is the cautionary tale.

The Playtest Program addresses the gap with three artifacts:

1. **A unified playable scene** (`Assets/Scenes/PlaytestScene.unity`) that exercises every shipped player-facing surface through normal play.
2. **A WARDEN-only dev-console scenario menu** so Talon can trigger any death / rescue / chore / sound / build event on demand instead of waiting for organic occurrence.
3. **A structured session-report flow** that captures observation, surfaces bugs in the canonical `BUG-NNN` shape (continuing from BUG-001 in `docs/known-bugs.md`), and feeds them into the existing dispatch flow.

Sessions are **Talon-paced**. No calendar; no minimum cadence. Whenever Talon wants to play, he plays.

---

## Identifier conventions

| Artifact | Convention | Location |
|---|---|---|
| Session report | `PT-NNN` (zero-padded, monotonically increasing) | `docs/playtest/PT-NNN-<short-slug>.md` |
| Program work packet | `WP-PT.NN` (parallel namespace to `WP-3.x.x`, `WP-4.x.x`) | `docs/c2-infrastructure/work-packets/` |
| Bug entry | `BUG-NNN` (continues from BUG-001) | `docs/known-bugs.md` |
| Bug-fix work packet | `WP-FIX-BUG-NNN-<slug>` | `docs/c2-infrastructure/work-packets/` |

---

## How a session runs

### Pre-session

1. Open Unity to `Assets/Scenes/PlaytestScene.unity` on the latest `staging` branch.
2. Decide a focus. Examples: "first end-to-end choke→rescue→bereavement loop," "build mode under sustained play," "30-NPC FPS gate verification post-shader-pipeline."
3. Open scratch — a notebook, voice memo, anything. You'll write notes during the session and the formal report after.

### During the session

1. Press play. Watch the office for ~5 minutes with no input — confirm defaults produce coherent play (per UX bible §1.3 rule 3).
2. Drive the focus area. Use the camera, selection, build mode, time controls naturally — like a player would.
3. When organic occurrence is too slow, use the dev-console scenario verbs (`~` to open). See `Assets/_Sandbox/scenarios.md` (shipped by WP-PT.1) or `help scenario` in the console.
4. Note anything that **felt off**, **looked wrong**, or **didn't match the bibles**. Distinguish:
   - **Bugs** — observable defects with reproducible steps.
   - **Feel notes** — subjective, not necessarily a bug, but flagged.
   - **Open questions** — things the bibles don't decide and play surfaces.

### Post-session

1. Copy `docs/playtest/PT-TEMPLATE.md` to `docs/playtest/PT-NNN-<slug>.md` (next monotonic NNN).
2. Fill it in. Be honest about feel; the report's value is the unfiltered observation, not a sanitized summary.
3. **Bugs filed this session** section contains canonical-shape entries. Append each to `docs/known-bugs.md` with a fresh `BUG-NNN` id.
4. Commit the report + ledger updates on `staging` (or a thin branch if you prefer; the report is doc-only).
5. **Critical bugs trigger an immediate `WP-FIX-BUG-NNN-<slug>` packet** — Opus authors, dispatched into whatever phase wave is in flight. Non-critical bugs queue against the appropriate future polish or `vN` wave (e.g., BUG-001's "build mode v2" home in 4.0.x).

---

## Bug intake — canonical shape

Each entry appended to `docs/known-bugs.md` follows the BUG-001 model:

```markdown
### BUG-NNN: <title>

**Symptom:** <one-sentence observable defect>

**Repro:**
1. <step>
2. <step>
3. <expected vs actual>

**Severity:** Critical / High / Medium / Low

**Files relevant (if known):** <paths>

**Suggested fix wave:** <e.g., "inline 4.0.x fix" / "build mode v2" / "polish wave" / "v2 — defer">

**Workaround (if any):** <text or n/a>

**Discovered in:** PT-NNN
```

### Severity rubric

| Severity | Definition | Triage |
|---|---|---|
| **Critical** | Engine crash, save corruption, FPS gate broken, scene fails to load, fundamental player-verb broken (camera doesn't move, build mode doesn't enter). | Interrupts current phase wave. Next dispatched packet is the fix. |
| **High** | A Phase 3 surface is materially broken: chore rotation never completes, rescue never fires, save round-trip drops a component family, build placement bug that destroys props. | Fix in next wave at latest. |
| **Medium** | A surface works but feels wrong: animation timing, stutter on transition, audio glitch, edge-case build placement (BUG-001 sits here). | Home in the appropriate `v2` / polish / wave-aligned packet. |
| **Low** | Cosmetic, rare, doesn't impair play. | Ledger entry for visibility; no scheduled fix. |

---

## Scope of what each session must check

The unified scene is designed to make every Phase 3 surface reachable. A session need not exercise all of them — pick a focus per session — but the program over time should cover all of them. A rough rolling checklist Talon updates session-to-session:

### Engine systems

- [ ] Life-state surface — all four narrative deaths reached (`Choked`, `SlippedAndFell`, `StarvedAlone`, `Died`)
- [ ] Fainting (Incapacitated → Alive recovery alone)
- [ ] Rescue (Heimlich, CPR, door-unlock — bystander recovery)
- [ ] Bereavement cascade — witnesses grieve per affection, stress rises, chronicle records
- [ ] Live mutation — build mode mutates topology, NPCs re-path next tick
- [ ] Sound triggers — all 10 kinds reachable (`Cough`, `ChairSqueak`, `BulbBuzz`, `Footstep`, `SpeechFragment`, `Crash`, `Glass`, `Thud`, `Heimlich`, `DoorUnlock`)
- [ ] Rudimentary physics — throw / drop / breakage
- [ ] Chore rotation — refusal cascade, over-rotation stress, archetype bias
- [ ] All seven per-archetype tuning JSONs load and bias behaviour
- [ ] Animation states — all six (Eating, Drinking, Working, Crying, CoughingFit, Heimlich)
- [ ] Save/load round-trip — mid-choke / mid-faint / mid-build / mid-chore / mid-rescue

### Unity host surface

- [ ] Camera rig — pan, rotate, zoom, recenter, wall-fade
- [ ] Selection + inspector — three-tier glance / drill / deep
- [ ] Build mode — toggle, ghost preview, place / remove, NPC re-pathing
- [ ] Time control — pause / ×1 / ×4 / ×16
- [ ] Event log — CDDA-style chronicle, filterable
- [ ] WARDEN dev console — opens, scenario verbs available, doesn't appear in retail
- [ ] Soften toggle — reachable in settings

### Performance gate

- [ ] 30-NPCs-at-60-FPS holds throughout the session

---

## Cadence and integration with phase dispatch

- **Sessions are Talon-paced.** No calendar.
- **Phase dispatch is unaffected** unless a Critical bug surfaces. The wave order (4.0.0 critique → 4.0.1 multi-floor + 4.0.2 shader pipeline → 4.1.x → ...) holds.
- **The playtest scene must be re-validated after every Phase wave.** A new session post-4.0.1 verifies the floor-switch verb works and the playtest scene still loads. Adding multi-floor seeding to the playtest scene is a follow-up sub-packet (`WP-PT.NN`) authored by Opus when 4.0.1 closes.

---

## Relationship to the Unity Packet Protocol

The Unity Packet Protocol (`docs/UNITY-PACKET-PROTOCOL.md`) governs how Track 2 packets ship: sandbox-first, atomic, prefab-driven, with a 5-minute Editor recipe. **It does not address feel.** A camera prefab can pass its sandbox recipe (the camera pans smoothly) and still feel wrong inside an integrated scene with 30 NPCs and active build mode.

**Rule 6 of the Unity Packet Protocol** (added 2026-05-01) declares: when a packet's acceptance criteria contain feel-level claims — visual perception, motion perception, integrated-system behavior under sustained play — that packet must mark itself `feel-verified-by-playtest`. The flag does not gate merge (the playtest program is parallel). It declares that the next post-merge PT-NNN session evaluates the work and any bugs found feed the normal `BUG-NNN` intake.

The two protocols compose:

| Stage | Verifier | Failure surface |
|---|---|---|
| Contract correctness | xUnit | Logic bugs, transition errors, schema drift |
| Visual primitive in isolation | Sandbox protocol — Talon's 5-minute recipe per `WP-3.1.S.NN` | Wiring, axis, scale, framing |
| Integrated whole over time | Playtest program — PT-NNN session | Feel, cross-system interference, sustained-play emergent issues |

---

## When the program needs tooling

The intake flow is intentionally lightweight. Talon writes the report by hand; bugs are appended to `known-bugs.md` by hand. If session volume rises and the manual append becomes friction, a follow-up `WP-PT.NN` adds tooling — a `playtest-extract-bugs` script, a session-history dashboard, etc. **Don't over-tool early.** The program proves itself via PT-001 through PT-005 first.

---

## File map

```
docs/
  PLAYTEST-PROGRAM-KICKOFF-BRIEF.md      ← the founding doc; living
  UNITY-PACKET-PROTOCOL.md               ← Rule 6 governs feel acceptance
  known-bugs.md                          ← bug ledger; BUG-NNN entries
  playtest/
    README.md                            ← this file
    PT-TEMPLATE.md                       ← canonical session-report template
    PT-001-<slug>.md                     ← first session (post-WP-PT.0 merge)
    PT-002-<slug>.md
    ...
  c2-infrastructure/
    work-packets/
      _PACKET-COMPLETION-PROTOCOL.md     ← feel-verified-by-playtest flag spec
      WP-PT.0-unified-playtest-scene.md  ← Sonnet-dispatchable; ships PlaytestScene
      WP-PT.1-dev-console-scenario-verbs.md  ← gated on PT.0 merge
      WP-PT.NN-...                       ← future re-validation / tooling packets
ECSUnity/
  Assets/
    Scenes/
      MainScene.unity                    ← production / phase-development scene
      PlaytestScene.unity                ← shipped by WP-PT.0; the verification surface
```

---

## First-move quickstart

To run **PT-001** once `WP-PT.0` and `WP-PT.1` have merged:

1. `git checkout staging && git pull`
2. Open Unity. Open `Assets/Scenes/PlaytestScene.unity`.
3. Press play.
4. Spend 30–45 minutes. Pick a narrow focus — the brief recommends "first end-to-end choke→rescue→bereavement loop" — and exercise it.
5. Use scenario verbs when organic occurrence stalls.
6. Copy `docs/playtest/PT-TEMPLATE.md` → `docs/playtest/PT-001-<slug>.md`. Fill it in.
7. Append any bugs to `docs/known-bugs.md` with `BUG-002` and on.
8. Commit. Open a PR if you'd like Opus or Sonnet review on the report; merge directly otherwise.

The first session sets the cadence. Don't over-engineer it.
