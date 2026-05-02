# PlaytestScene — First-Light Recipe

> Run before merging WP-PT.0. Time budget: 10–15 minutes. Goal: verify every Phase 3 surface lights up.
> If any check fails: file as BUG-NNN per docs/playtest/README.md severity rubric and route per the bug-fix wave. Do NOT ask Sonnet to iterate ad-hoc on this packet — failed first-light items are normal program intake.

## Setup (one-time)

1. Open Unity Editor on the `sonnet-wp-pt.0` worktree branch.
2. Open `Assets/Scenes/PlaytestScene.unity`.
3. Confirm the PLAYTEST SCENE indicator is visible top-left (dim, 12pt).

## Boot check (1 minute)

1. Press Play.
2. Observe: no compile errors, no NullRefs in console, no pink magenta-missing-shader meshes.
3. Confirm 15 NPCs spawn within 2 seconds.
4. Confirm FPS gauge (top-right, FrameRateMonitor) reads ≥ 58.

## Camera (1 minute)

1. Pan with arrow keys / left-mouse-drag. Smooth, no overshoot.
2. Rotate with Q / E. Lazy-susan rotation works.
3. Zoom with mouse wheel. Bounded — can't zoom under cubicles or above ceiling.
4. Double-click an NPC. Camera glides toward them.

## Selection + inspector (2 minutes)

1. Single-click an NPC. Halo + outline appear under/on them. Inspector glance opens (5 fields).
2. Click drill button. Drill layer opens (drives, willpower, schedule, task, stress, mask).
3. Click again. Deep layer opens (full vectors, relationships, memory).
4. Click off. Halo + outline disappear; inspector closes.
5. Click a chair. Object inspector opens (named anchor, current state, interactors).
6. Click an empty floor tile. Room inspector opens.

## Build mode (2 minutes)

1. Press B. World tints beige-blue. Build palette appears on the right.
2. Drag a wall ghost into the world. Red tint where invalid (overlapping NPC); green where valid. Click to place.
3. Confirm an NPC re-paths around the new wall on next tick.
4. Drag a door, place it. Right-click → lock.
5. Press B again. Tint clears; palette closes.

## Time control (1 minute)

1. Cycle pause / ×1 / ×4 / ×16 via number keys.
2. Confirm sim speed visibly changes; FPS holds ≥ 58 at all speeds.
3. Press space — pauses. Press space — resumes at last speed.

## Event log (1 minute)

1. Open the event log (icon or default keybind — see TimeHudPanel for binding).
2. Confirm reverse-chronological list shows recent events.
3. Filter by NPC = (any selected NPC name); filter narrows. Clear.
4. Click an event entry; inspector opens pinned to that NPC.

## Dev console (1 minute)

1. Press backtick (`~`). Console opens.
2. Type `help`. List of existing commands appears (no `scenario *` yet — that ships in WP-PT.1).
3. Type `force-faint <name>` for a visible NPC. They drop.
4. Type `force-kill <name>` for the same NPC. They die. Bereavement cascade fires on witness NPCs (you'll see chibi-emotion cues + log entries).
5. Close the console.

## Sound (1 minute)

1. With audio on, focus camera on an active NPC.
2. Sit for 30 seconds. Confirm you hear at least: footsteps, chair-squeaks, ambient hum.
3. If no audio at all: file BUG with `Severity: High`. If audio present but specific triggers missing: note in PT-001.

## Save/load round-trip (1 minute)

1. Pause sim mid-day.
2. Save (default name "Tuesday Day N" or whatever the day is).
3. Click "Load most recent autosave."
4. Sim restores to identical state. Selected NPC is the same; clock matches; build edits persist.

## Performance gate (sustained — 2 minutes)

1. Resume sim at ×4.
2. Watch the FPS gauge for 2 minutes. p95 should be ≥ 58.
3. Note any frame stutters (camera jerks, audio glitches) with timestamps.

## Pass criteria

- All Boot, Camera, Selection, Build, Time, Event log, Dev console items pass without exception.
- Audio: at least 3 distinct sound triggers heard.
- Save\load: round-trip preserves state visually.
- Performance: p95 FPS ≥ 58 at ×4 with 15 NPCs.

If 80%+ of the above passes, the scene is mergeable; remaining items become BUG-NNN entries in known-bugs.md (severity per rubric) and feed PT-001's session focus.

If less than 80% passes, return to Sonnet for a fix wave (the spec was incomplete or a primitive regressed).
