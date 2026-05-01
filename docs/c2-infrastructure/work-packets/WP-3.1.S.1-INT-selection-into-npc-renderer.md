# WP-3.1.S.1-INT — Wire Selection into NPC Renderer

> **DO NOT DISPATCH UNTIL WP-3.1.S.1 IS MERGED.** Requires the validated `Selectable.prefab` + outline shader from S.1.
> **Protocol:** Track 2 integration packet under `docs/UNITY-PACKET-PROTOCOL.md`. Modifies live engine scripts.

**Tier:** Sonnet
**Depends on:** WP-3.1.S.1 (selection sandbox + Selectable.prefab + outline shader)
**Parallel-safe with:** All Track 1 engine packets. WP-3.1.S.0-INT, WP-3.1.S.2-INT.

**Timebox:** 90 minutes
**Budget:** $0.40

---

## Goal

Make NPC dots in `MainScene.unity` clickable. After this packet, clicking an NPC dot in the live engine scene attaches the validated outline (from S.1) to that dot, and the `SelectionManager` exposes `CurrentSelection` keyed to the NPC's `EntityId` for downstream consumers (the inspector popup integration `WP-3.1.S.3-INT` is one such consumer).

This packet ships:

1. An update to `NpcDotRenderer.cs` so each per-NPC quad is created with `Selectable` + `OutlineRenderer` components attached, plus a small `NpcSelectableTag` MonoBehaviour that records the entity's `EntityId` so `SelectionManager.OnSelectionChanged` can be mapped back to a `WorldStateDto.Entities` entry.
2. A `SelectionManager` GameObject added to `MainScene.unity` (or a prefab instance dropped in — Sonnet picks whichever is simplest given the existing scene).
3. An update to `SelectionManager.cs` adding a `public string SelectedEntityId { get; }` accessor that downstream code reads. (Implementation: when selection changes, the manager looks up the selected GameObject's `NpcSelectableTag` and caches the entity ID.)
4. An update to `Assets/_Sandbox/selection-outline.md` adding an "Integration verification" section.

---

## Reference files

1. `docs/UNITY-PACKET-PROTOCOL.md`.
2. `docs/c2-infrastructure/work-packets/WP-3.1.S.1-selection-outline-sandbox.md` — the validated sandbox.
3. `ECSUnity/Assets/Scripts/Render/NpcDotRenderer.cs` — the file gaining the `Selectable` attachment in `CreateNpcView`.
4. `ECSUnity/Assets/Scripts/Selection/SelectionManager.cs` — the file gaining `SelectedEntityId`.
5. `ECSUnity/Assets/Prefabs/Selectable.prefab` — the prefab being attached (or its components copied) onto each NPC quad.
6. `ECSUnity/Assets/Scenes/MainScene.unity` — the scene gaining the `SelectionManager` GameObject.
7. `Warden.Contracts/Telemetry/EntityStateDto.cs` (or wherever `EntityStateDto.Id` lives) — the entity ID field the tag stores.

---

## Non-goals

- Do **not** modify `Selectable.cs`, `OutlineRenderer.cs`, or `Outline.shader` from S.1. The sandbox-validated artefacts are settled.
- Do **not** ship the inspector popup binding. That's WP-3.1.S.3-INT.
- Do **not** add multi-select, hover preview, or modifier-key behaviour. Single-click single-select preserved.
- Do **not** touch room rectangles, walls, or any non-NPC renderable. NPC dots only.
- Do **not** ship per-archetype selection feedback variations.
- Do **not** modify any sandbox scene under `Assets/_Sandbox/`.
- Do **not** modify any engine code (`APIFramework/`, `Warden.*/`).
- Do **not** introduce a new dependency (Cinemachine, URP, etc.).

---

## Implementation steps

1. **Verify S.1 prefab + shader exist.** Confirm `Assets/Prefabs/Selectable.prefab`, `Assets/Shaders/Outline.shader`, and the three Selection scripts are present. If not, escalate.
2. **Add `NpcSelectableTag.cs`** to `Assets/Scripts/Selection/` with one public field: `public string EntityId;`. Tooltip-only; no logic.
3. **Edit `NpcDotRenderer.CreateNpcView`** to:
   - Add a `BoxCollider` (or `SphereCollider`) to the quad. The Selectable raycast from S.1 needs a collider to hit.
   - Attach a `Selectable` component.
   - Attach an `OutlineRenderer` component, configured with the validated default colour from S.1's tuned prefab. (Read the Inspector default off `Selectable.prefab` once at `Awake` and apply to each new NPC view, or simply hard-code the default value matching the prefab's tuning.)
   - Attach a `NpcSelectableTag` and set its `EntityId = npc.Id`.
4. **Add `SelectionManager` to `MainScene.unity`.** A simple `GameObject` named `SelectionManager` at world origin. Wire its `_camera` field to the prefab's main camera (or leave null and rely on `Camera.main`).
5. **Edit `SelectionManager.cs`** to add `public string SelectedEntityId { get; private set; }`. Update it when `OnSelectionChanged` fires by reading the selected GameObject's `NpcSelectableTag` (if present).
6. **Run `dotnet test` and `dotnet build`.** Must be green.
7. **Append the integration verification recipe** to `Assets/_Sandbox/selection-outline.md`.

---

## Test recipe addendum (append to `Assets/_Sandbox/selection-outline.md`)

```markdown

## Integration verification (after WP-3.1.S.1-INT)

This step confirms NPC dots in MainScene are clickable and selection
state is exposed.

1. Open Assets/Scenes/MainScene.unity.
2. Press Play. Engine ticks; NPCs render as dots.
3. Click an NPC dot. Expect: outline appears around it (same colour as
   sandbox tuning).
4. Click another NPC dot. Expect: previous outline clears, new outline
   appears.
5. Click an empty floor area. Expect: outline clears.
6. With the dev console open (if WARDEN build), type
   `selection.entityId` (or open SelectionManager in the Inspector
   while paused). Expect: SelectedEntityId equals the engine's
   EntityId for the previously-clicked NPC.

## If integration fails
- Click on NPC does nothing → BoxCollider missing on the quad
  (CreateNpcView didn't attach it), or LayerMask excluding the layer.
- Outline appears but jitters with NPC movement → outline renderer
  isn't following the dot's position. The prefab's outline should be
  parented to the dot and inherit position.
- SelectedEntityId always empty → NpcSelectableTag not attached or
  SelectionManager isn't reading it. Check the SelectionManager's
  OnSelectionChanged handler.
```

---

## Acceptance criteria

1. `Assets/Scripts/Selection/NpcSelectableTag.cs` exists.
2. `NpcDotRenderer.CreateNpcView` attaches `BoxCollider`, `Selectable`, `OutlineRenderer`, `NpcSelectableTag` to each quad.
3. `SelectionManager.SelectedEntityId` returns the NPC entity ID when an NPC is selected; returns null/empty when nothing is selected.
4. `MainScene.unity` contains a `SelectionManager` GameObject.
5. `Assets/_Sandbox/selection-outline.md` has the new "Integration verification" section appended.
6. `dotnet test` passes; `dotnet build` is clean.
7. Existing 30-NPCs-at-60-FPS perf gate still holds (adding a collider + two components per NPC is cheap; if the gate fails, escalate).

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

Track 2 integration. Talon's recipe pass is the gate.

The Sonnet executor's pipeline:

1. Implement the spec.
2. Run `dotnet test` and `dotnet build`. Green.
3. Stage all changes (including any cleanup `git rm`).
4. Commit on the feature branch.
5. Push.
6. Final commit message line: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/selection-outline.md (Integration verification section)`.

Talon: open MainScene, press Play, run recipe. Pass = merge. Fail = PR comments or follow-up packet.

### Cost envelope

Target: **$0.40** (90-minute timebox). Escalate with a blocker note if costs approach $0.80.

Cost-discipline:
- `NpcDotRenderer.CreateNpcView` already exists; the integration is additive lines, not a rewrite.
- Don't add a new Material per NPC; reuse the outline shader instance.

### Self-cleanup on merge

Before opening the PR:

1. **Check downstream dependents** of this packet and of the sandbox spec it integrates:
   ```bash
   git grep -l "WP-3.1.S.1-INT" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   git grep -l "WP-3.1.S.1" docs/c2-infrastructure/work-packets/ | grep -v "WP-3.1.S.1-INT" | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. **For this packet (S.1-INT):** if no downstream dependents, `git rm` the spec file. (Note: WP-3.1.S.3-INT references S.1-INT for selection-driven popup binding. If S.3-INT is still pending, retain S.1-INT spec.)

3. **For the sandbox packet (S.1):** if the second grep returns no other pending dependents, `git rm` the S.1 spec file too in this commit. Otherwise retain.

4. Add appropriate `Self-cleanup:` lines to the commit message describing what was deleted.

5. Sandbox prefabs and shaders stay. **Do not touch** `_completed/` or `_completed-specs/`.

---

*WP-3.1.S.1-INT closes the click-to-highlight loop on NPCs. After this, `SelectedEntityId` is the seam any future packet uses to know who the player is looking at.*
