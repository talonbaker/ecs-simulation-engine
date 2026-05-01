# Known Bugs / Backlog

Issues confirmed but deferred — to be revisited when the relevant system is mature enough to support a proper fix.

---

## Build Mode — Drag & Place

### BUG-001: Large prop placed on top of small prop causes disappear / incorrect placement

**Symptom:** Dragging a table onto a banana sitting on the floor causes the table to disappear or settle at an incorrect position. The banana is not displaced.

**Root cause (investigated):** `GetSurfaceYAtXZ` returns the banana's top surface as the floor height, so the table's pivot-to-bottom offset stacks incorrectly. Attempts to auto-displace the banana (socket-snap or raise-to-top) each introduced secondary bugs: `OnDropped` firing on displacement triggering PropToEngineBridge snap-back, free-floating displaced props causing `_dragPlaneY` oscillation ("freak-out") on next pick-up.

**Deferred because:** Prop-on-prop displacement resolution belongs in a broader "build mode v2" spatial pass alongside proper footprint tracking, multi-prop stacking rules, and undo/redo. The core drag workflow (grab → move → snap → socket drop) is unaffected.

**Workaround:** Place small props AFTER large props, or manually move the small prop out of the way before placing the large one.

**Files relevant to fix:** `DragHandler.cs` (`GetSurfaceYAtXZ`, displacement logic), `DraggableProp.cs` (`SnapToSocketSilent`, `CancelDrag` — scaffolding already in place for a future attempt).

---
