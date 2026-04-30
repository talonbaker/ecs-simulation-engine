# WP-3.1.S.2-INT — Wire DraggableProp into Build Mode + IWorldMutationApi

> **DO NOT DISPATCH UNTIL WP-3.1.S.2 IS MERGED. Also DO NOT DISPATCH UNTIL WP-3.0.4 IS MERGED.** Requires both the validated `Table.prefab` / `Banana.prefab` / `DraggableProp.cs` from S.2 *and* the engine's `IWorldMutationApi` from WP-3.0.4. Without the engine API, drops would have nowhere to land in the engine state.
> **Protocol:** Track 2 integration packet. Modifies live build-mode glue.

**Tier:** Sonnet
**Depends on:** WP-3.1.S.2 (sandbox + validated draggable prefabs), WP-3.0.4 (`IWorldMutationApi`, `StructuralChangeBus`)
**Parallel-safe with:** All other Track 1 / Track 2 packets except S.2 itself.

**Timebox:** 120 minutes
**Budget:** $0.50

---

## Goal

Replace the existing build-mode prototype's interaction layer with the validated `DraggableProp` flow from S.2, and route drop events through `IWorldMutationApi` so the engine learns about every prop placement / move / removal in real time.

After this packet, build-mode in `MainScene.unity`:
1. Allows the player to drag any `DraggableProp`-tagged prop with the same feel Talon validated in the sandbox.
2. Calls `IWorldMutationApi.AddEntity` (or `MoveEntity`) on drop, with a payload describing the prop's archetype + position.
3. Emits a `StructuralChangeEvent` via the bus (handled by 3.0.4 internally), invalidating pathfinding cache automatically.
4. The existing build-mode toggle UI from WP-3.1.D continues to work — entering build mode activates the `DragHandler`, exiting deactivates it.

This packet ships:

1. An update to the existing build-mode controller (likely `Assets/Scripts/BuildMode/BuildModeController.cs` or similar — Sonnet identifies the actual filename) so it activates/deactivates `DragHandler` with build-mode toggle.
2. A new `Assets/Scripts/BuildMode/PropToEngineBridge.cs` MonoBehaviour. Subscribes to `DraggableProp.OnDropped` (a new event the bridge wires) and translates the drop into an `IWorldMutationApi` call.
3. An archetype-mapping table — a small ScriptableObject `PropArchetypeMap.asset` mapping prefab → engine entity archetype string. Tables and bananas have entries; future props extend the table.
4. The integration verification recipe appended to `Assets/_Sandbox/draggable-prop.md`.

---

## Reference files

1. `docs/UNITY-PACKET-PROTOCOL.md`.
2. `docs/c2-infrastructure/work-packets/WP-3.1.S.2-draggable-prop-sandbox.md` — the sandbox.
3. `docs/c2-infrastructure/work-packets/WP-3.0.4-live-mutation-hardening.md` — the engine API contract this packet calls.
4. `ECSUnity/Assets/Scripts/BuildMode/` — read whatever's there. `BuildModeController.cs` (or equivalent) is the build-mode toggle owner; this packet wires `DragHandler` into it.
5. `ECSUnity/Assets/Scripts/Engine/EngineHost.cs` — exposes the engine instance the `IWorldMutationApi` interface lives on.
6. `APIFramework/Mutation/IWorldMutationApi.cs` — the API surface this packet calls. (Path may differ; confirm during implementation.)
7. `examples/office-starter.json` — defines the existing prop archetypes. The `PropArchetypeMap` ScriptableObject's entries should match real archetypes.

---

## Non-goals

- Do **not** modify `DraggableProp.cs`, `DragHandler.cs`, `PropSocket.cs`, `Table.prefab`, or `Banana.prefab` from S.2.
- Do **not** add prop spawning from a palette / inventory. Drag-existing-props only at v0.1.
- Do **not** add collision-with-walls validation. The engine's `IWorldMutationApi` may reject some placements; surface that as a "snap back to original" visual fallback. No fancy "invalid placement" feedback.
- Do **not** add multi-select drag.
- Do **not** add undo / redo. Future packet.
- Do **not** modify `IWorldMutationApi` itself or any engine code.
- Do **not** ship per-archetype drop sound effects (sound bus integration is a separate packet).

---

## Implementation steps

1. **Verify dependencies merged.** Confirm `Assets/Prefabs/Table.prefab`, `Banana.prefab`, all S.2 scripts exist, and the `IWorldMutationApi` interface is accessible from `APIFramework`.
2. **Inspect the existing build-mode controller.** Read `Assets/Scripts/BuildMode/BuildModeController.cs` (or whatever exists). Identify how the existing build-mode toggle is wired. The integration adds `DragHandler.Activate()` / `Deactivate()` calls to that toggle; do not refactor the toggle logic itself.
3. **Add `OnDropped` event to `DraggableProp.cs`.**

   Wait — this would modify a sandbox-validated file from S.2. Re-read the non-goals.

   **Resolution:** the S.2 sandbox already validated the drop *behaviour*. Adding a public event is additive and doesn't change the visible behaviour. Acceptable. Document in the completion note.

   ```csharp
   public event Action<DraggableProp, Vector3> OnDropped;  // fires after snap, before parent
   ```

4. **Create `PropToEngineBridge.cs`** in `Assets/Scripts/BuildMode/`. Subscribes to all in-scene `DraggableProp.OnDropped` events. On drop:
   - Look up the prop's prefab in `PropArchetypeMap` to get the archetype string.
   - Call `_engineHost.WorldMutationApi.AddEntity(archetype, position)` (or `.MoveEntity(entityId, newPosition)` if the prop already exists in the engine — track via a `Dictionary<DraggableProp, Guid>` of "this prop currently maps to this engine entity").
   - On engine rejection (the API returns a result indicating invalid placement), revert the prop's transform to its pre-drag position.
5. **Create `PropArchetypeMap.asset`** ScriptableObject. Two entries:
   - `Table.prefab` → archetype `"table"` (or whatever the existing archetype identifier is in `office-starter.json`).
   - `Banana.prefab` → archetype `"banana"`.
6. **Wire `DragHandler.Activate()` and `.Deactivate()`** into the existing build-mode toggle. Identify the toggle's `OnEnable` / `OnDisable` (or equivalent) and call the handler's lifecycle methods accordingly.
7. **Drop a `PropToEngineBridge` GameObject** into `MainScene.unity`. Wire its `_engineHost` reference.
8. **Run `dotnet test` and `dotnet build`.** Must be green.
9. **Append the integration verification recipe** to `Assets/_Sandbox/draggable-prop.md`.

---

## Test recipe addendum (append to `Assets/_Sandbox/draggable-prop.md`)

```markdown

## Integration verification (after WP-3.1.S.2-INT)

This step confirms drag-place in MainScene mutates engine state.

1. Open Assets/Scenes/MainScene.unity.
2. Press Play. Engine ticks; NPCs render.
3. Toggle build mode (the existing build-mode UI button or shortcut).
4. Click and hold an existing prop in the scene (a desk, a chair —
   whatever the office-starter spawns with the DraggableProp tag wired
   on it). Move it. Snap behaviour identical to sandbox.
5. Drop it.
6. Observe: NPCs whose path passed through the prop's old or new
   location re-pathfind on the next tick (proves StructuralChangeBus
   fired, cache invalidated).
7. Save the world (existing save/load — WP-3.2.0 if shipped — or
   inspect the live WorldStateDto via dev console). Confirm the prop's
   new position is reflected in engine state.
8. Toggle build mode off. Click on a prop. Expect: nothing happens
   (DragHandler deactivated).

## If integration fails
- Drag does nothing in build mode → BuildModeController not calling
  DragHandler.Activate(). Check the toggle wiring.
- Drag works but engine state doesn't change → PropToEngineBridge not
  subscribed to OnDropped, or _engineHost reference broken.
- Engine state changes but NPCs walk through the moved prop →
  StructuralChangeBus not firing. Check IWorldMutationApi
  implementation in 3.0.4.
- Prop snaps back to original position → engine rejected the
  placement (expected behaviour for invalid spots; if happening
  unexpectedly, check the archetype string in PropArchetypeMap).
```

---

## Acceptance criteria

1. `Assets/Scripts/BuildMode/PropToEngineBridge.cs` exists with the described behaviour.
2. `Assets/Resources/PropArchetypeMap.asset` (or wherever the project's ScriptableObject convention places it) exists with at least table + banana entries.
3. `DraggableProp.cs` has a new `public event Action<DraggableProp, Vector3> OnDropped;` and fires it appropriately.
4. The existing build-mode controller activates/deactivates `DragHandler` on toggle.
5. `MainScene.unity` contains a `PropToEngineBridge` GameObject with `_engineHost` wired.
6. `dotnet test` passes; `dotnet build` is clean.
7. The 30-NPCs-at-60-FPS perf gate still holds.
8. `Assets/_Sandbox/draggable-prop.md` has the new "Integration verification" section.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

Track 2 integration. Talon's recipe pass is the gate.

Sonnet pipeline: implement → `dotnet test` + `dotnet build` green → stage with cleanup → commit → push → stop. Final commit message line: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/draggable-prop.md (Integration verification section)`.

Talon: open MainScene, press Play, toggle build mode, run recipe. Pass = merge. Fail = PR comments or follow-up packet.

### Cost envelope

Target: **$0.50** (120-minute timebox). The `IWorldMutationApi` integration may surface unexpected friction (Unity Coroutine vs Engine tick boundaries, marshalling between Unity main thread and the engine update loop). If friction emerges, **stop and write a `WP-3.1.S.2-INT-blocker.md` note** rather than burning budget on a refactor.

Cost-discipline:
- `PropToEngineBridge` is a thin adapter, not a new architecture. ~50–100 lines max.
- Don't add a queue / batch-mutation system. One drop = one `IWorldMutationApi` call. Future packets can batch if needed.

### Self-cleanup on merge

Before opening the PR:

1. **Check downstream dependents** of this packet and of S.2:
   ```bash
   git grep -l "WP-3.1.S.2-INT" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   git grep -l "WP-3.1.S.2" docs/c2-infrastructure/work-packets/ | grep -v "WP-3.1.S.2-INT" | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. **For this packet (S.2-INT):** likely no downstream dependents. `git rm` the spec file.

3. **For the sandbox packet (S.2):** if the second grep returns no other pending dependents, `git rm` the S.2 spec file too in this commit.

4. **For WP-3.0.4 spec:** check whether *any* pending packet still depends on `WP-3.0.4`. If not, `git rm` it too. (Likely still has dependents at this point — WP-3.0.3, WP-3.2.x packets.)

5. Add `Self-cleanup:` lines for each deletion to the commit message.

6. Sandbox prefabs, scripts, and ScriptableObjects stay. **Do not touch** `_completed/` / `_completed-specs/`.

---

*WP-3.1.S.2-INT closes the build-mode loop with engine mutations. After this, every prop placement is live engine state and the chronicle records each one (assuming WP-3.2.0 has shipped — if not, mutations work but aren't persisted across save/load).*
