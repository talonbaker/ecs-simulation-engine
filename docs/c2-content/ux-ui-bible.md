# UX/UI Bible — v0.1 Working Draft

> Co-authored by Talon and Opus. Defines the player-facing surface of the office sim. Reads with: world-bible, cast-bible, aesthetic-bible, dialog-bible, action-gating.
>
> **Status:** v0.1 — load-bearing decisions captured; the gameplay-loop / challenge-surface question (§3.2 and §6) and the player-embodiment + direct-intervention questions (§3.4 and §6) are open and will be revised in v0.2.

---

## What this bible commits to

The player is a watcher of an office full of slightly-too-extreme humans. The interface job is to let them watch, nudge, and rearrange — without ever feeling like a spreadsheet. Every UI decision below traces to one of two questions: *does this preserve the lens?* and *can mom play this?* If a proposal fails either, it doesn't ship.

The world is mature. The interface is gentle.

---

## 1. The lens (axioms — non-negotiable)

These are the inviolable rules. Every verb, every surface, every visual element conforms to all six. Future packets that propose to violate one are rejected on architectural grounds.

### 1.1 Ghost-camera framing

The player is a watcher, not an inhabitant. They float over the office, lean in, drift between cubicles, and observe. They have no body in the world by default. (Per world-bible §1 and SRD axiom 8.6.) Whether they have a *desk* — a notification overlay representing them as a non-character presence — is open; see §3.4.

### 1.2 Diegesis rule

The world commentates on itself through observable consequence. **No popups, no `[!]` icons, no toast notifications, no modal dialogs over gameplay.** The player notices what an NPC would notice — through ambient cues, body language, environmental signals, and diegetic carriers (a phone ringing on a desk, a fax tray filling, a stink line rising from the bathroom).

The HUD allowance is austere: time control, build mode indicator, save state. Everything else is in-world.

### 1.3 Mom can play

The target player is someone who has never strafed-while-aiming, who doesn't fly two analog sticks, who installed the game on her laptop and has no patience for hotkey rosters. Five operating rules, all required:

- **Single-stick-equivalent control.** Camera is one stick *or* one mouse. There is no situation in default play that requires two analog inputs simultaneously.
- **Discrete actions over continuous holds.** A click-to-focus is better than a hold-to-zoom. A toggle is better than a modifier key. A button-tap is better than a stick-scrub.
- **Defaults must produce good play with no input.** The simulation is alive at ×1 with no clicks. The player adds nudges; the absence of nudges is not a broken game.
- **No required precision.** No drag-to-aim, no timing windows under 500ms, no double-clicks-that-mean-different-things. Targets are large.
- **No accidental power-user mode.** Free camera, time-zoom, skip-to-morning are creative-mode-only — reachable only through settings, never via accidental input.

### 1.4 Layered disclosure

Glance → drill → deep. The default view of an NPC, an object, or a room reveals as little as possible. Each click goes one layer deeper. The player sees what they ask to see — never more, never less.

### 1.5 Stress and lull come from the simulation's rhythm

The fire arrives because the player wasn't watching when they should have been. **Default play has no skip-to-morning, no time-zoom, no fast-forward beyond ×16.** Stress is consequence; lull is the office at quiet. The interface offers the four-speed set (pause / ×1 / ×4 / ×16) and nothing faster. Creative-mode opt-in unlocks more.

### 1.6 Iconography over text

The visual language is **early-2000s computer / phone iconography + chibi-anime emotion vocabulary + environmental cues**. Symbols carry meaning where text would; text appears only where a symbol can't (NPC names, clock readouts, the inspector's deeper drill layers).

This axiom is the bible's distinctive signature, and it pairs with the aesthetic bible's "low-poly 3D rendered as pixel art" commitment. The art style is era-appropriate; the UI vocabulary matches.

The vocabulary, in three families:

**Emotional iconography** (chibi-anime tradition, on-NPC, subtle):
- Anger lines on a forehead — irritation rising. Red face — embarrassment or social mask under strain. Green face — nausea or disgust. Sweat drops — stress under exposure. Sleep-Z's — exhaustion or sleeping. Hearts — affection (rare, earned). Sparkles — moment of pride. **No theatrical cartwheels — Donna can't dance her feelings.** Cues sit on or near the face/head while the NPC continues their actual activity.

**Environmental iconography** (in-world, ambient):
- Stink lines — bad smells (bathroom that hasn't been cleaned, the fridge container that's been there over a year, an NPC who needs to wash). Green fog — concentrated bad smell (the bathroom after the wrong person used it). Dust motes — neglect (the basement supply closet). Buzzing wave-lines — flickering fluorescent. Light beams — sun through window. Z's above an unused desk — empty or forgotten.

**UI iconography** (HUD, era-appropriate):
- CRT-style icons. Chunky, low-res, beige-and-blue palette. Phone receiver, fax page, email envelope, floppy-disk save, magnifying-glass inspect. Loading bars where needed. **A blinking cursor block as a candidate selection cue** (see §2.2).

These are starting palettes; specific designs are art-pipeline work. The bible commits to the *vocabulary*, not the per-icon pixels.

---

## 2. Player verbs

### 2.1 Camera

Single-stick-equivalent. Default mode is the diorama view: camera floats at a fixed altitude just under the ceiling, looks down at the office floor. Walls fade to translucent when they occlude the player's line to the focused area. The world reads like a lego model on a lazy susan — you can spin it, lean in, drift across.

**Bindings (default):**
- Pan: left mouse drag, or arrow keys, or one analog stick.
- Rotate: right mouse drag (lazy-susan), or `Q` / `E`, or shoulder buttons.
- Zoom: mouse wheel, or `+` / `-`, or trigger buttons. Bounded — the player cannot zoom into a desk drawer or under a chair.
- Recenter on selected entity: double-click, or `F`, or one button.

**Constraints (axioms within the verb):**
- Camera height is **fixed** in default mode. Small zoom range is allowed (between just-above-cube-top and just-under-ceiling) but the camera never drops below cube-top or rises above ceiling. The diorama lens stays intact.
- Walls fade by occlusion to player line — never auto-dropped, never made permanently invisible.
- Cannot enter desks, drawers, or sub-cube interior space. The inspector handles deep readouts; the camera doesn't dive.
- Multi-floor: **single-floor v0.1.** When multi-floor lands, the floor-switch verb is `D-pad up` / `Q` / `E` (or a side-panel button); camera floats out, swaps height, floats back in. Never a literal stairwell traversal.

**Creative-mode camera (settings-toggle only):** full 3D control, zoom under desks, free altitude. Burying it behind the toggle protects the default lens for first-time players.

### 2.2 Selection & inspection

Single-click on an entity (NPC, object, room) opens the inspector with the camera staying put. **Double-click** recenters the camera with a smooth glide toward the target. The inspector is layered (§3.1).

**Selection visual cue (open for playtest):** the bible commits to a *soft, visible-at-glance* selection indicator without taking a final position on which one. Two candidates to playtest:

- **Halo + outline (classic).** Soft white halo on the ground tile under the selected entity; subtle outline on the entity itself.
- **CRT-blinking selection box.** A slowly blinking box (terminal-cursor cadence — ~500ms on, 500ms off) frames the selected entity. Reinforces the early-2000s-computer aesthetic; reads as "the player is operating a CRT terminal looking into the world."

V0.1 ships the halo+outline as the default; the blinking-box is an alternative the team experiments with in playtests. The bible documents both; the final choice is post-playtest.

**Multi-select:** open question. Default v0.1: single-target selection only. Multi-select for "give all NPCs this nudge" or "compare two NPCs side by side" is a follow-up if needed.

### 2.3 Build mode

A **toggle**, not always-on. Entered via `B`, or an on-screen build-mode button (CRT-style icon — a wrench or a ruler-and-pencil). Exit returns to watch mode.

In build mode:
- The world tints slightly (a soft beige-blue overlay) so the player always knows they're in build vs watch.
- Structural items (walls, doors, desks, the named anchors) get visible outlines.
- A build palette (§3.5) appears on one side of the screen.
- Mutable-topology items (pickup-able props, chairs, small objects) can be grabbed; structural items can be added or removed.
- NPCs continue to do their thing. **If the player rearranges during work hours, NPCs react** (irritation spikes; schedule disruption events fire). This is intentional friction — see §4.3.

V0.1 build mode does not include the pickup-and-throw-with-camera-momentum verb. That earns its own packet (~3.2.x) once Unity is real and the rudimentary physics layer ships.

### 2.4 Time control

Default mode: pause / ×1 / ×4 / ×16. Nothing faster.

The HUD time widget is small and always visible (§3.6). The four speeds are large click targets; pause is also `space`. No speed transitions are smoothly interpolated — flips are instant.

**No skip-to-morning, no time-zoom in default mode.** Both are creative-mode-only (§5.2). Off-hours play happens at the player's chosen default speed; ×16 will get you through the night fast enough.

**Auto-pause-on-event:** off by default. Player can opt-in via settings to auto-pause on death events, mass-bereavement, fire (future), or other configurable triggers. Default: the simulation runs through; the player decides whether to react.

### 2.5 [Deferred] Pickup & manipulate

Committed but deferred. The pickup-and-throw verb (camera momentum carries the held item; releasing transfers velocity; breakable items break on hard-surface impact above a hit-energy threshold) is its own packet (~3.2.x), shipped after the rudimentary-physics packet lands. Not in the v0.1 bible's verb set; documented here so future packets know the slot exists.

---

## 3. Player-facing surfaces

### 3.1 Inspector

Layered disclosure on selection. Three tiers:

**Glance (default on single-click, ~5 fields):**
- Name (e.g., "Donna")
- Current activity (e.g., "Heading to women's bathroom")
- Mood — represented as one icon from the emotional vocabulary (§1.6) plus a one-word text label (e.g., 😤 *frustrated*; 😴 *exhausted*)
- One contextual fact relevant to right now (e.g., "overdue task: yes" or "in conversation with Frank")
- A single CRT-style "drill" button (magnifying glass) that opens the next layer

**Drill (one click deeper):**
- Drives (irritation, affection, suspicion, etc. — top 3 active)
- Willpower current / cap
- Schedule block (e.g., "AtDesk until 12:00")
- Active task or none
- Stress level (acute / chronic, with the stress-source breakdown)
- Mask state (felt-vs-performed gap if any)

**Deep (one more click):**
- Full drive vector
- Inhibition vector
- Personality (Big Five values)
- All known relationships (sortable by affection / distance / recency)
- Personal memory entries (most-recent-N persistent ones)
- Current and pending intended action

Each tier reveals more text density. Glance is icon-heavy, deep is text-heavy. The player chooses how much they want to know about Donna at any moment.

**Object inspector** (clicking a chair, desk, fridge, microwave): shows the object's named-anchor description (if any), its current state (e.g., "needs cleaning, 4 days overdue"), who interacts with it on a typical day, and any persistent state attached. The microwave's smell-meter is here, not above the microwave in the world.

**Room inspector** (clicking an empty floor tile or a room outline): shows the room's lighting state, current occupants, named anchors in the room, and any persistent stains / hazards.

### 3.2 Notifications [open — see §6]

The challenge surface is **NPC management, not task overload.** The world bible's "orders come in, nobody told you, you're not prepared" loop is a real commitment, but the *volume* and *carrier* of those orders is being recalibrated. The bible v0.1 stakes out the conservative position:

- **Sparse.** Orders are rare. The player is not deluged. A typical game-day may have zero, one, or two new orders — not a dozen.
- **Diegetic.** Carriers are in-world (phone ringing, fax tray, email blink on a CRT) — never popups.
- **Ignore-able with consequence.** A ringing phone the player ignores will eventually go to voicemail, and the consequence (missed deadline, upset upstairs, nobody's-fault stress) rolls in days later through the regular simulation. The player isn't punished by the UI; they're punished by the world.

**The bigger gameplay-loop question is open.** The challenge model the bible leans toward (per Talon's 2026-04-26 framing): challenge comes from NPC management — keeping NPCs alive, relatively happy, and out of each other's worst dynamics. The constant-choker NPC, the food-thief NPC, the chore-rotation that nobody wants. Tasks-from-upstairs exist but are not the primary stress source.

This shifts the notification surface's design from "central command center" to "ambient signals that something needs attention." Final shape lands in v0.2 after Talon's revision pass.

### 3.3 Event log

A CDDA-style chronicle reader. Always accessible via a small icon (a notebook, era-appropriate). Lists persistent events in reverse chronological order — the affairs, the deaths, the firings, the bereavements, the stains that stayed, the relationship shifts that mattered.

**Filtering:** by NPC (Donna's events only), by event type (deaths only, social conflicts only), by time range (this game-week, all-time). Default view is "last seven game-days, all events."

The event log is the player's tool for catching up after they've been away (the simulation ran while they were paused-but-AFK, or they want to know what happened across last week). It is **not** a gameplay verb — clicking an entry doesn't undo or edit; it only opens the inspector pinned to that NPC at that point in time.

### 3.4 Save / load [partially open]

**Save format:** `WorldStateDto` JSON (SRD axiom 8.2). Same shape as agents read.

**Save slots:** unlimited named manual slots. Default name is `<weekday> Day N` (e.g., "Tuesday Day 14"). Player can rename.

**Autosave cadence (locked v0.1):**
- **End-of-game-day** at 11:59 PM sim time, before the night-build window opens. Two rotating slots so a major rearrangement can be rolled back.
- **Periodic autosave every ~5 minutes of real-time play** for crash recovery. One rotating slot, separate from the daily save.
- **On entering build mode for the first time each night,** an additional checkpoint slot.

**Load:** named slots in a CRT-style file picker. Confirmation prompt before loading (irreversible action). Quick-load hotkey (`F9`) reserved for the most recent autosave.

**Player embodiment in the save UI is open.** If the player has a "manager's office" overlay (§1.1, §6), the save UI may live in that office (a CRT on the manager's desk, file cabinet next to it). If the player is pure ghost, the save UI is a HUD overlay. Decision lands in v0.2.

### 3.5 Build palette

Visible only in build mode. A side panel (CRT-styled — chunky beige border, low-res icons) showing categories of placeable items: walls, doors, desks, chairs, props, named anchors. Player drags from the palette into the world.

**Inventory model:** open. Two candidates:
- **Unlimited** — the palette has every available item, no quantity limit. Simplest; fits relaxed play.
- **Quartermaster / supply-room** — items must be requisitioned through a system; the player has a budget or a stock cap. Adds resource-management; risks the "task overload" anti-pattern flagged in §3.2.

V0.1 leans unlimited; the resource-management variant is a future toggle if the design wants it.

**Ghost preview:** when placing, a translucent preview shows where the item will land. Red tint if the placement is invalid (overlapping wall, blocking a path, no floor under). Click to commit; right-click or `Esc` to cancel.

### 3.6 Time HUD

Small, always visible, top-right of the screen. CRT-styled.

Contents:
- Current time (sim clock, e.g., "Tuesday 2:47 PM")
- Speed indicator (one of: ⏸ ▶ ▶▶ ▶▶▶ — pause, ×1, ×4, ×16). Click to cycle, or use number keys 1-4.
- Day-of-week + day-number badge

Nothing else. No mini-map (yet — open question), no notification bell (notifications are diegetic; the player notices the ringing phone, not a HUD bell), no stat readouts.

### 3.7 Audio

Engine emits sound triggers; host (Unity) synthesises. Diegetic.

**Trigger vocabulary** (initial set; engine packets emit these):
- `Cough` — emitted by the choking detection system, by general NPC physiology when sick.
- `Gasp`, `Wheeze` — at choke onset / during incapacitation.
- `Footstep` — per-tile, attenuated by floor surface (carpet vs tile vs basement-concrete).
- `ChairSqueak` — at sit / stand transitions.
- `KeyboardClack` — while typing (work activity).
- `MicrowaveDing`, `FridgeHum`, `PrinterChug`, `ElevatorDing`, `PhoneRing`, `FaxChug`, `EmailBeep` — per object affordance.
- `BulbBuzz` — emitted continuously by flickering / dying fluorescent fixtures; host throttles per camera proximity.
- `OfficeAmbient` — HVAC, distant hum, the building's general noise floor. Gets quieter at night when the office empties.
- `SpeechFragment` — for each spoken-fragment event from the dialog corpus. The host synthesises Sims-style phoneme gibberish against the speaker's archetype voice profile (future packet authors voice profiles per archetype — the Old Hand, the Newbie, the Vent each sound distinguishable).

**No HUD beeps.** Save confirmations, time-control switches, build-mode entry/exit are silent or use minimal in-world cues (a brief tape-deck-click for build mode entry, e.g.).

**Default audio state:** on at moderate volume. First-launch prompts the player to confirm or adjust ("Sound is on — adjust here, or mute"). Always-accessible volume slider in pause menu.

**Camera-proximity attenuation:** sound triggers near the camera focus play at full volume; sounds far from focus attenuate. Bulb buzz is only audible within ~3 tiles of the camera. Footsteps attenuate over ~5 tiles. The player hears what they're paying attention to.

### 3.8 Emotional & environmental iconography (the diegesis layer)

This surface is the cousin of the inspector — what the player reads from the world *without* clicking. Per §1.6, the vocabulary is chibi-anime + early-2000s-computer + environmental cues.

**On NPCs (subtle, persistent-while-relevant):**
- Anger lines on forehead — irritation drive in the upper third.
- Red-face flush — embarrassment, mask under strain, sexual attraction.
- Green-face — nausea, disgust, hangover.
- Sweat drops (one or two, small) — stress in the upper half.
- Sleep-Z's — drowsiness or actually sleeping.
- Hearts — affection or attraction, rare, magnitude-scaled.
- Sparkles — pride, satisfaction. Brief, on event.
- Question mark — confusion or new event noticed.
- Exclamation — surprise. Brief, on event.

These do not float dramatically above heads in modern-game style. They are small, near the NPC's head/face, anime-chibi-grade. They appear when the underlying drive / mood / event is strong enough to be observable, and fade when it isn't.

**Conversation visualization:**
- Quiet conversation between two NPCs in conversation range — a small, slow stream of letters / soft text fragments rises between them. Light gray. Reads as "they're talking" without the player needing to know what they're saying.
- Heated conversation — the stream gets larger, faster, color-shifts (red for anger, blue for sad, etc.). The player notices a fight from across the office without clicking.
- Mask-slip events — brief sharp punctuation (a comic-style `!?`) at the moment of slip, before the conversation visualization resumes.

This is rich design space; specific shape is post-prototype. The bible commits to the *vocabulary* and the *severity-scales-with-volume* rule.

**Environmental (in-world, ambient):**
- Stink lines — emanate from sources of bad smell (fridge container, bathroom-after-event, an NPC in need of hygiene, a stain).
- Green fog — concentrated bad smell (the bathroom on a bad day; near rotting food).
- Dust motes — neglect (basement supply closet, cubicle 12 over time).
- Wave-buzz lines — flickering fluorescent.
- Light beams — sun through windows, slicing through dust motes.
- Sleep-Z's over an empty desk — that desk has been empty too long (the world bible's Cubicle 12 cue).

**HUD iconography:**
- All HUD elements use CRT-era icon style. Time HUD, save/load, build palette, inspector buttons. Chunky, low-res, beige-and-blue palette.
- Player notifications, when they need a HUD presence, use the same vocabulary — but the rule is **always prefer diegetic over HUD.**

---

## 4. Policies

### 4.1 Diegetic vs HUD allocation

Default allocation (HUD allowance is austere):

| Element | Allocation |
|:---|:---|
| Time control | HUD (small, top-right) |
| Build mode indicator | HUD (visible while in build mode only) |
| Inspector | HUD on selection (panel slides in) |
| Event log | HUD on opening (panel) |
| Save / load | HUD (file picker) |
| Build palette | HUD (visible in build mode only) |
| Volume / settings | HUD (pause menu) |
| Notifications | **Diegetic** (phone, fax, email — in world) |
| NPC mood | **Diegetic** (chibi cues on the NPC) |
| Conversation | **Diegetic** (text streams above conversers) |
| Environmental signals | **Diegetic** (stink lines, fog, etc.) |
| Death / event signals | **Diegetic** (the dead NPC's silence; bereaved NPCs' postures; absence of the choke sound is the choke ending) |
| Selection cue | **Anchored-to-entity** (halo+outline OR blinking box) |

### 4.2 Mom-can-play implementation rules

Operationalisation of axiom 1.3. Every UX/UI proposal runs through this checklist:

1. Could the proposal be operated with a single hand on a mouse? If no, redesign or move to creative-mode.
2. Are all hover/click targets at least 24×24 pixels at the default zoom? If no, enlarge.
3. Is the default state a good play experience with zero clicks? If no, defaults need rethinking.
4. Does the proposal require a tutorial? If yes, the proposal is too complex.
5. Is there an accidental-input path that exits to a power-user mode? If yes, gate the power-user mode behind settings.

### 4.3 Gameplay rhythm — work hours vs off-hours

The office runs morning-to-dusk. Build mode is freely available at all times, but the *intent* is:

- **Work hours (worker NPCs present):** rearrangement during this window disrupts the office. NPCs react with irritation, schedule misses, accidents. The player can do it — sometimes the player should — but they bear the cost.
- **Off-hours (NPCs gone home, office quiet):** the player's natural restructuring window. They have as long as they want (or, in future hardcore mode, as long as the time-pressure budget allows).

This rhythm is what gives build mode meaning. The bible commits to the rhythm; the implementation is up to the build packet (3.1.D).

### 4.4 Failure modes

How the UI handles edge cases, all consistent with the diegesis rule:

- **NPC dies in view.** No popup. The NPC stops moving. If the camera is on them, the player sees it. If the camera isn't, the bereavement system surfaces consequence later (witnesses' mood, the empty cubicle, the chronicle entry).
- **Player tries to pick up a corpse.** If WP-3.0.4's `IWorldMutationApi.MoveCorpse` is wired, the corpse moves. If not, the cursor shows a "can't" icon (CRT-style) and audio plays a brief denial click. No error popup.
- **Player locks all doors at once.** The locks apply. The world responds — eventually with starvation deaths. The UI does not warn the player. The player learns by consequence.
- **Build mode placement invalid.** Ghost preview tints red. Click does nothing. Brief CRT-style denial click sound. No popup.
- **Save fails** (disk full, etc.). One small CRT-style modal: "Save failed — disk space?" with `OK` and a "view details" drill. This is the only HUD-modal exception in the bible because it's a **system-level failure outside the simulation**.

### 4.5 Accessibility

V0.1 commitments:

- **Color-blind palette.** Default HUD uses color cues that are also distinguished by shape / icon (e.g., the speed indicator uses both color and chevron count). A high-contrast alternative palette is reachable in settings.
- **Text scaling.** Inspector text obeys an OS-level or in-game scale slider (3 levels: small / default / large).
- **Sticky control toggle.** Hold-to-rotate becomes click-to-rotate-then-click-to-stop. Enabled in settings.
- **Volume per-channel.** Master, ambient, NPCs, music (when added), UI clicks. Per-channel sliders.
- **Mute-on-launch option.** First-launch prompt asks about audio; the answer persists.
- **Subtitle option for NPC speech.** When `SpeechFragment` synthesises Sims-gibberish, an opt-in subtitle shows the underlying corpus fragment as text. (Default off — gibberish-only — preserves the silent-film-at-distance feel.)

Future polish: full keyboard navigation (no mouse required); screen-reader compatibility for menus.

### 4.6 Mature content opt-outs (the "soften" toggle)

The world bible commits to depicting sex, drugs, depression, infidelity, death honestly but not sensationally. Mature content is **default-on**; the game stays rated mature.

A **soften toggle** in settings provides a mercy lever:

- **Death:** body fades after a short duration (configurable; default ~1 game-hour). No persistent corpse. Bereavement still happens; the body just isn't a visible long-term presence.
- **Sexual humor:** in-world sexual references continue (mask-slips, dialog corpus); **explicit visual depictions** (if any are added in future packets) blur or soften.
- **Drug use:** continues to occur; **paraphernalia visible in the world** softens (no syringe-prop visibility, e.g.).
- **Depression / suicide:** continues to be modeled; **explicit content warnings** appear when entering scenes that center on it.

Soften is **not censorship** — it's a content-warning + visual-softening blend. The world stays mature; the player stays in control of what they look at.

### 4.7 Telemetry / cadence

Per the design philosophy (and SRD §2 Pillar A): **JSONL telemetry is host-side, configurable, never per-frame.** Default cadence: every N ticks, where N is runtime-tunable. The Unity host respects this; agents and dev-time observability never demand 60 FPS streams.

For Unity host (post-WP-3.1.A): emit `WorldStateDto` to `worldstate.jsonl` on a background thread per N-tick cadence, default N = 30 (~once per game-second at standard tick rate). Adjustable from the dev console (WP-3.1.H).

---

## 5. Game modes

### 5.1 Default

The canonical play experience. All bible commitments apply. Time controls are pause / ×1 / ×4 / ×16. Camera is locked to diorama altitude. Skip-to-morning is unavailable. Mature content is default-on (mediated by the soften toggle if the player set it).

### 5.2 Creative / sandbox

Settings opt-in. Unlocks:

- Free-floating camera (full 3D, can dive under desks)
- Skip-to-morning verb (jumps clock to next 8:00 AM)
- Time-zoom (variable-rate slider beyond ×16)
- Spawn-anything build palette (no resource constraints; immediate placement)
- World-state inspector with read-write (edit NPC components directly — debug-grade)
- Disable death / disable bereavement (sandbox-only consequence-suppression)

Creative mode is for messing around, designing layouts, debugging, sharing world-states with friends. The gameplay loop is suspended; the world is a toy.

Entering creative mode is a settings toggle (not a button-cycle). Exiting is the same path. The first-launch player will not enter creative mode by accident.

### 5.3 [Future] Hardcore / challenge mode

Out of scope for v0.1. Documented here so future packets know the slot exists.

Likely additions:
- Time-limited off-hours build window (e.g., 4 sim-hours of pause budget per night; after that, time advances regardless).
- Reduced save-slot count (one or two; no quick-load).
- Disabled speed-up beyond ×4.
- Auto-pause-on-event off.
- Higher base hazard rates (more chokes, more slips, more lockout incidents).

Hardcore is a polish packet; ships post-1.0 if at all.

---

## 6. Open questions for revision (carry-forward)

These are real ambiguities that v0.2 will resolve. v0.1 stakes out tentative leans where useful; they are not committed.

- **Q4 — Notification carrier model.** Centralized (manager's office overlay), distributed (per-NPC phones), or hybrid? Larger question: what *is* the gameplay-loop's challenge surface? Talon's 2026-04-26 framing leans **NPC-driven challenge over task overload** — the constant-choker, the food-thief, the chore rotation. Notifications are sparse and ambient under that model. Decision lands when the gameplay-loop commitment lands.
- **Q5 — Player embodiment.** Pure ghost-camera, manager's office overlay, or manager NPC? The bible v0.1 leans **manager's office overlay** (a CRT desk somewhere in the building hosts notifications and save UI), but the decision is open.
- **Q6 — Direct NPC intervention.** No direct verbs (environmental only), soft suggestions (nudge a task, send-NPC-home), or direct commands? The bible v0.1 leans **environmental-only at v0.1** with soft suggestions as a follow-up packet, but the decision is open.
- **Selection visual final choice.** Halo+outline vs CRT-blinking-box. Playtest decides.
- **Multi-select.** Default v0.1 is single-target only. Multi-select for NPC comparison or batched nudges is open.
- **Build inventory model.** Unlimited palette vs supply-room / quartermaster. v0.1 leans unlimited; resource-management variant is a future toggle.
- **Mini-map.** Not in v0.1 HUD. Open whether one is needed at all.
- **Floor-switch verb final shape.** When multi-floor lands, the camera-float-out-and-back animation needs a reference design. Sketch is in §2.1; specifics deferred.
- **Auto-pause-on-event triggers.** Off by default. Which events qualify for opt-in (death, mass-bereavement, fire, lockout)? Open.
- **Voice profiles per archetype.** The Old Hand, the Newbie, the Vent each sound distinguishable. Future audio packet authors the profile vocabulary.
- **Subtitle styling.** When subtitle option is enabled, how is the corpus fragment rendered? Inline at the conversation? At screen bottom? Open.
- **Tutorial / first-launch experience.** v0.1 commits to "no tutorial required" (axiom 4.2 rule 4). But a brief diegetic introduction (a tape-deck welcome message, an HR letter on the player's manager's-office desk) may be warranted. Open.

---

## 7. What's deliberately not committed yet

- The exact pixel art of any specific icon or HUD element. Per-asset art-pipeline work, not bible work.
- The shader pipeline for wall-fade-on-occlusion. Implementation choice; bible commits to the *behavior* (walls fade when occluding the focused area), not the technique.
- The specific input-binding table per platform. Bible commits to single-stick-equivalent; the table is per-platform polish.
- The specific copy text for inspector labels, settings menus, etc. Localisation- and writing-pass work.
- The font choice for HUD text. CRT-era pixel font is the leaning direction; specific font is art-pipeline.

---

## 8. The hierarchy, summarized

1. **The lens (axioms)** — ghost-camera, diegesis, mom-can-play, layered disclosure, simulation-rhythm-stress, iconography.
2. **Player verbs** — camera, selection, build, time-control. (Pickup-and-throw deferred.)
3. **Player surfaces** — inspector, notifications, event log, save/load, build palette, time HUD, audio, emotional/environmental iconography.
4. **Policies** — HUD allocation, mom-can-play rules, gameplay rhythm, failure modes, accessibility, soften toggle, telemetry.
5. **Game modes** — default, creative, future hardcore.
6. **Open questions** — Q4 (notifications/challenge surface), Q5 (embodiment), Q6 (intervention), and twelve smaller items.

V0.1 is a working draft. Future v0.2 lands the open questions. This bible reads alongside the world / cast / aesthetic / dialog / action-gating bibles; nothing here overrides their commitments.
