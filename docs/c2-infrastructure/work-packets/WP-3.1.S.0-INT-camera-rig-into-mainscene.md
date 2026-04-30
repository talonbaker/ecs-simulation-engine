# WP-3.1.S.0-INT — Wire CameraRig into MainScene

> **DO NOT DISPATCH UNTIL WP-3.1.S.0 IS MERGED.** This packet wires the validated `CameraRig.prefab` into the live engine scene. If S.0's prefab doesn't exist, this packet has nothing to integrate.
> **Protocol:** Track 2 integration packet under `docs/UNITY-PACKET-PROTOCOL.md`. Modifies `MainScene.unity` and retires reflection-bootstrap code.

**Tier:** Sonnet
**Depends on:** WP-3.1.S.0 (camera rig prefab + validated Inspector defaults)
**Parallel-safe with:** All Track 1 engine packets. Other Track 2 sandbox packets (`WP-3.1.S.1`, `S.2`, `S.3`) are parallel-safe.
**Timebox:** 60 minutes
**Budget:** $0.30

---

## Goal

Replace `SceneBootstrapper.SetupCamera`'s programmatic camera setup in `MainScene.unity` with the validated `CameraRig.prefab` instance from WP-3.1.S.0. After this packet, the live engine scene uses the same camera rig Talon tuned in the sandbox; the reflection-based field-injection in `SceneBootstrapper.cs` for camera setup is retired.

This packet ships:

1. An updated `MainScene.unity` containing one `CameraRig.prefab` instance, with `_engineHost` and `_wallFadeController` references wired to the existing scene's `EngineHost` and `WallFadeController` GameObjects (Talon does this wiring during the visual verification pass; the Sonnet leaves the references unset and documents the wiring step in the test recipe).
2. An updated `SceneBootstrapper.cs` with `SetupCamera` removed (the prefab provides the camera; no runtime injection needed). Other bootstrapper logic (renderers, light, performance monitor) is preserved.
3. An updated `Assets/_Sandbox/camera-rig.md` test recipe extended with the integration verification steps (so Talon's single recipe covers both sandbox and integrated use).

The integration deletes more code than it adds. That's the point of the protocol.

---

## Reference files

1. `docs/UNITY-PACKET-PROTOCOL.md` — protocol.
2. `docs/c2-infrastructure/work-packets/WP-3.1.S.0-camera-rig-sandbox.md` — the sandbox spec being integrated. Especially the Inspector contracts table.
3. `ECSUnity/Assets/Prefabs/CameraRig.prefab` — the artifact being instantiated in MainScene.
4. `ECSUnity/Assets/Scripts/Engine/SceneBootstrapper.cs` — the reflection-bootstrap code being removed.
5. `ECSUnity/Assets/Scenes/MainScene.unity` — the scene being modified.
6. `ECSUnity/Assets/Scripts/Render/Lighting/WallFadeController.cs` — the controller the camera rig optionally wires to.

---

## Non-goals

- Do **not** modify `CameraController.cs`, `CameraInputBindings.cs`, `CameraConstraints.cs`, or `CameraRig.prefab` itself. The prefab is settled.
- Do **not** touch `SceneBootstrapper.cs` logic for renderers, lights, or the performance monitor. Camera-only retirement.
- Do **not** modify any sandbox scene under `Assets/_Sandbox/`.
- Do **not** modify any other live scene. There's only `MainScene.unity` to touch.
- Do **not** modify `EngineHost.cs`, `WorldStateProjectorAdapter.cs`, or any engine-side file.
- Do **not** add new camera features in this packet. Pure integration.
- Do **not** retire other parts of `SceneBootstrapper.cs`. Future integration packets retire their own pieces.

---

## Implementation steps

The Sonnet executes these in order:

1. **Verify the prefab exists.** Confirm `Assets/Prefabs/CameraRig.prefab` is present. If not, escalate — WP-3.1.S.0 hasn't merged.
2. **Open `MainScene.unity` in the Unity Editor.** Find the existing `Main Camera` GameObject. Note its current Inspector references (`_engineHost`, `_wallFadeController`).
3. **Drag `CameraRig.prefab` into the scene root.** Place at world origin (the prefab's internal transform handles position).
4. **Wire the prefab's `CameraController._engineHost` field** by dragging the existing `EngineHost` GameObject onto it.
5. **Wire the prefab's `CameraController._wallFadeController` field** by dragging the existing `WallFadeController` GameObject onto it. (If `WallFadeController` is on a child object, navigate the hierarchy to find it.)
6. **Delete the existing `Main Camera` GameObject** that was created by `SceneBootstrapper.SetupCamera`. The prefab's child `Main Camera` replaces it. Confirm the new camera is tagged `MainCamera`.
7. **Save `MainScene.unity`.** Verify the scene's serialised YAML now references `CameraRig.prefab`.
8. **Edit `SceneBootstrapper.cs`:** remove the `SetupCamera()` method body and the call to it. Leave a one-line comment: `// Camera setup removed in WP-3.1.S.0-INT — see CameraRig.prefab in MainScene.unity.` Keep the rest of the bootstrapper unchanged.
9. **Run `dotnet test` and `dotnet build`.** Both must pass.
10. **Update `Assets/_Sandbox/camera-rig.md`** to add an "Integration verification" section that walks Talon through opening MainScene, pressing Play, and confirming camera control still works. (See **Test recipe addendum** below.)

---

## Test recipe addendum (append to existing `Assets/_Sandbox/camera-rig.md`)

The Sonnet appends this section verbatim to the existing recipe file (do not replace prior content):

```markdown

## Integration verification (after WP-3.1.S.0-INT)

This step confirms the camera rig works in the live MainScene with the
real engine.

1. Open Assets/Scenes/MainScene.unity.
2. Press Play. Engine ticks; rooms render; NPCs render as dots.
3. Pan, rotate, zoom — same controls as the sandbox.
4. Confirm the camera framing matches the sandbox feel — same altitudes,
   same pan speed, same start focus point.
5. Toggle wall fade by moving the camera close to a wall. Confirm walls
   fade as expected (this proves _wallFadeController is wired).
6. Confirm 60+ FPS with 30 NPCs (existing perf gate).

If integration verification fails:
- Camera doesn't move → CameraController._engineHost reference broken.
  Re-wire in the Inspector.
- Walls don't fade → CameraController._wallFadeController reference
  broken. Re-wire.
- Two cameras visible / "Two AudioListeners" warning → the old
  Main Camera GameObject wasn't deleted. Delete it.
```

---

## Acceptance criteria

1. `MainScene.unity` contains exactly one `CameraRig.prefab` instance and zero standalone `Main Camera` GameObjects (the camera is the prefab's child).
2. `SceneBootstrapper.SetupCamera()` is removed; the call site in `Boot()` is removed; the rest of `Boot()` is unchanged.
3. `SceneBootstrapper.cs` compiles with no warnings.
4. `dotnet test` passes; `dotnet build` is clean.
5. `Assets/_Sandbox/camera-rig.md` has the new "Integration verification" section appended.
6. **No other file is modified.** Verify with `git diff --name-only`.
7. The Inspector wiring (drag-references in `MainScene.unity`) is the *only* manual step required after a fresh checkout. Sonnet documents this in the integration verification recipe; Talon performs it during visual verification.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

This is a Track 2 integration packet. The xUnit suite cannot verify Inspector wiring or scene composition. Talon's manual pass through the integration verification recipe is the gate.

The Sonnet executor's pipeline:

1. Implement the spec — modify scene, retire `SetupCamera`, append recipe section.
2. Run `dotnet test` and `dotnet build`. Must be green.
3. Stage changes (including any `git rm` for the spec file per self-cleanup; see below).
4. Commit on the worktree's feature branch.
5. Push.
6. Stop. The commit message's final line: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/camera-rig.md (Integration verification section)`.

Talon's pipeline:

1. Open MainScene in Unity Editor.
2. **Wire the two Inspector references** (`_engineHost`, `_wallFadeController`) on the prefab instance. Save the scene.
3. Run the integration verification recipe.
4. If passes: open PR, merge.
5. If fails: PR review comments or follow-up packet.

### Cost envelope (1-5-25 Claude army)

Target: **$0.30** (60-minute timebox). This is a small integration. If costs approach $0.60, **escalate** with `WP-3.1.S.0-INT-blocker.md`.

Cost-discipline:
- The scene-modification step is mostly drag-and-drop in the Editor. Don't try to script it.
- Don't refactor `SceneBootstrapper.cs` beyond the camera-removal. That's scope creep.

### Self-cleanup on merge

Before opening the PR (after Talon's visual verification passes), the executing Sonnet must:

1. **Check downstream dependents:**
   ```bash
   git grep -l "WP-3.1.S.0-INT" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   git grep -l "WP-3.1.S.0" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. **For this packet (S.0-INT):** if no downstream dependents, `git rm` the spec file. Add `Self-cleanup: spec file deleted, no pending dependents.` to the commit message.

3. **For the sandbox packet (S.0):** the second grep checks whether `WP-3.1.S.0`'s spec file is now an orphan (this packet was the only dependent). If yes (only this -INT packet referenced it, and we're shipping this -INT now), `git rm` the S.0 spec file too in the same commit. Add a second cleanup line to the commit message: `Self-cleanup: WP-3.1.S.0 spec file also deleted, all dependents shipped.`

4. **The prefab and sandbox scene from S.0 stay** — they live in `Assets/Prefabs/` and `Assets/_Sandbox/` indefinitely. Only the spec markdown is subject to cleanup.

5. **Do not touch** files under `_completed/` or `_completed-specs/`.

---

*WP-3.1.S.0-INT closes the camera rig loop. The reflection-based scene bootstrap retires for camera; renderers and lighting follow in their own integration packets when their sandbox packets ship.*
