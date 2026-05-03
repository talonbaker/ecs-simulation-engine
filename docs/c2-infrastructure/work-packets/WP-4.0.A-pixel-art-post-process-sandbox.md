# WP-4.0.A — Pixel-Art Post-Process Sandbox

> **Wave 1 of the Phase 4.0.x foundational polish wave.** Per the 2026-05-02 brief restructure: the shader pipeline is promoted to lead because every subsequent visual polish packet should author against the final aesthetic. This is a Track 2 sandbox packet under `docs/UNITY-PACKET-PROTOCOL.md`. The companion `WP-4.0.A-INT` packet (not yet drafted) will integrate the validated effect into PlaytestScene.

**Tier:** Sonnet
**Depends on:** Existing 3.1.x Unity scaffold (camera rig, render scripts). No engine changes.
**Parallel-safe with:** WP-4.0.B (engine-only NPC anti-overlap, disjoint surface), WP-4.0.C (engine + build-mode files, disjoint from render), WP-PT.* (playtest scene work; this packet does not modify PlaytestScene).
**Timebox:** 120 minutes
**Budget:** $0.60
**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** the pixel-art shader's read at 30 NPCs scale (post-`-INT`), the iconography crispness at intended camera altitudes, the FPS gate.

---

## Goal

Ship a **camera-side pixel-art post-process effect** plus the sandbox scene that validates it, on the existing **built-in render pipeline** (no URP migration in this packet — see Design notes for rationale). The effect renders the world's low-poly 3D geometry at a low internal resolution and upscales with point-filtering to produce the early-2000s pixel-art look the aesthetic bible commits to. Real shadows from real lights are preserved; the pixel-art treatment is purely an output filter.

After this packet, Talon can open `Assets/_Sandbox/pixel-art-shader.unity`, press play, see a small set of test primitives rendered in the target style, toggle the effect on/off live, and adjust the internal-resolution + palette parameters in the Inspector. The effect's `ICameraRenderPass` registration interface is in place so future packets (CRT scanline, film grain, outline) — and future modders — can add their own passes without touching the core.

The 30-NPCs-at-60-FPS gate is the safety rail. If the pixel-art post-process regresses below 60 FPS in a synthetic 30-cube test, the effect's settings are wrong; iterate until the gate holds.

---

## Reference files

- `docs/UNITY-PACKET-PROTOCOL.md` — read in full. All five rules apply, plus Rule 6 (feel-verified-by-playtest, added 2026-05-01).
- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` — read MAC-009 entry. This packet introduces it.
- `docs/c2-content/aesthetic-bible.md` — read the rendering commitments. Pixel-art-from-3D, real shadows from real lights, era-appropriate palette (CRT-beige-and-blue + saturated chibi accents).
- `docs/c2-content/ux-ui-bible.md` §1.6 — iconography vocabulary. The pixel-art treatment must not destroy chibi emotion-cue legibility (a single sweat-drop must remain visible at intended camera altitude).
- `ECSUnity/Packages/manifest.json` — confirm built-in render pipeline; no URP package present. **Do not add URP in this packet.**
- `ECSUnity/Assets/Scripts/Camera/CameraController.cs` — read for context. The post-process effect attaches to the camera's `OnRenderImage`; no changes to controller logic.
- `ECSUnity/Assets/Prefabs/CameraRig.prefab` — the canonical camera. Sandbox scene drags this in; the post-process script is added as an additional component on the prefab's child Camera, behind a Sandbox-only override.

---

## Non-goals

- Do **not** migrate to URP. (See Design notes for rationale; FF-007/MOD-CANDIDATES will track URP as a future option.)
- Do **not** modify the camera controller, input bindings, or constraints.
- Do **not** modify `MainScene.unity` or `PlaytestScene.unity`. Integration is the `-INT` packet.
- Do **not** add lighting features (lighting upgrades are WP-4.0.D's territory; this packet preserves whatever light setup the sandbox needs to test the shader).
- Do **not** add particle effects (those are WP-4.0.H).
- Do **not** add NPC silhouettes to the sandbox. Use bare primitives (cubes, spheres, capsules) so the shader test surface is pure.
- Do **not** ship an `OnRenderImage`-only solution that breaks if the camera component setup changes; the effect must be a standalone, addable/removable component.
- Do **not** introduce dependencies on third-party shader packs or asset-store assets.

---

## Design notes

### Why built-in pipeline, not URP

URP migration on a Unity project of this size is a multi-day operation: every material re-authored, every shader URP-equivalent, post-processing stack swapped, lighting setup migrated, packages added, FPS gate re-validated. The benefits (shader graph tooling, 2D lighting system, cleaner post-process pipeline) are real but **none are required to ship the v0.2 visual goals**. A render-texture-based pixel-art post-process effect, real 3D lighting, and the standard particle system all work in built-in.

The honest pragmatic call: ship the visual work in built-in now; reserve URP migration as a future packet IF we hit limits we can't work around with built-in (e.g., we want shader-graph-authored modder shaders, or the 2D lighting system's renderer-feature stack becomes important). MAC-CANDIDATES tracks URP migration as a known future option.

If the Sonnet executing this packet discovers a hard built-in limit while implementing (e.g., a specific camera-stack feature URP makes much easier), commit a `WP-4.0.A-blocker.md` note documenting the limit and stop. Do not silently start a URP migration.

### Effect approach (camera-side render-texture)

Standard pixel-art post-process technique:

1. Camera renders the scene at full resolution to a render texture.
2. The post-process script down-samples that to a low internal resolution (e.g., 320×180 or 480×270 — Inspector-configurable). Sampling: nearest-neighbor.
3. Optional: palette-quantization pass (snap colors to a fixed palette of N entries — Inspector-configurable, with a default palette inspired by the aesthetic bible).
4. Up-sample to screen resolution with point filtering. The result reads as crisp pixel art.

Implementation: a `MonoBehaviour` on the camera with `OnRenderImage(RenderTexture src, RenderTexture dst)`. Two materials, one shader: the down-sample + palette-quantize is a fragment shader; the up-sample is just a `Graphics.Blit` with `FilterMode.Point` on the intermediate render texture.

### `ICameraRenderPass` interface (Mod API surface — MAC-009)

Define this interface in the same packet:

```csharp
public interface ICameraRenderPass {
    string PassName { get; }
    int Order { get; } // lower runs first
    void Apply(RenderTexture src, RenderTexture dst, Camera camera);
}
```

The pixel-art effect implements `ICameraRenderPass` with `Order = 100`. A registry component (`CameraRenderPipeline` MonoBehaviour) on the camera holds a `List<ICameraRenderPass>`, sorts by Order, and chains src→dst through each pass in `OnRenderImage`. Modders add new passes by implementing the interface and registering via `CameraRenderPipeline.RegisterPass(...)`.

In v0.2 only the pixel-art pass exists. The interface costs nothing now and pays back the moment a second pass lands (CRT scanline, outline, film grain, all on the roadmap).

### Default palette

The default palette is a starting point, not a final commitment. ~16 colors, biased toward CRT-era beige-blue plus a few saturated accents for chibi cues. Sonnet authors the default palette as a Texture2D asset (`Assets/Settings/DefaultPixelArtPalette.asset` or similar) referenced by the shader. Talon iterates on the palette in the Inspector during sandbox testing.

### Internal resolution

Two presets and a custom slider:
- `Crisp` — 480×270 (3 sim-pixels per screen pixel at 1440p)
- `Chunky` — 320×180 (4.5 sim-pixels per screen pixel at 1440p; more aggressive pixelation)
- `Custom` — Inspector-set width and height

The sandbox scene defaults to `Chunky` because the iconography test (the chibi cue legibility) is the harder constraint; if iconography reads at Chunky, it reads at Crisp.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSUnity/Assets/Scripts/Render/ICameraRenderPass.cs` | Interface (MAC-009). |
| code | `ECSUnity/Assets/Scripts/Render/CameraRenderPipeline.cs` | MonoBehaviour. Holds + sorts + chains passes. |
| code | `ECSUnity/Assets/Scripts/Render/PixelArtRenderPass.cs` | The pixel-art down-sample + palette-quantize pass. Implements `ICameraRenderPass`. |
| shader | `ECSUnity/Assets/Shaders/PixelArtDownsample.shader` | Fragment shader for the down-sample + palette-quantize. |
| asset | `ECSUnity/Assets/Settings/DefaultPixelArtPalette.png` | 16-color palette texture (1×16 or 4×4). Default colors documented in a sibling `.md` next to the file. |
| scene | `ECSUnity/Assets/_Sandbox/pixel-art-shader.unity` | Sandbox scene per Rule 4 of UNITY-PACKET-PROTOCOL. |
| doc | `ECSUnity/Assets/_Sandbox/pixel-art-shader.md` | 5–10 minute test recipe. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Confirm MAC-009 entry is accurate post-implementation; revise if interface shape changed. |

The sandbox scene contents:

- A 30×30 grid plane (gray, lit) so the camera has something to look at.
- Six test primitives at varied distances:
  - Three reference cubes (one each: red, green, blue — solid, unshaded test) at `(5, 0.5, 5)`, `(15, 0.5, 5)`, `(25, 0.5, 5)`.
  - Two spheres with PBR metallic / smooth materials at `(10, 0.5, 15)`, `(20, 0.5, 15)` — to verify the shader handles non-flat lighting.
  - One capsule with a complex texture (any built-in Unity texture; checker pattern is fine) at `(15, 0.5, 25)`.
- One Directional Light (warm tint, intensity 1.1, rotation Euler `(50, -30, 0)`) and one Point Light (cool tint, intensity 0.6, position `(15, 3, 15)`) so shadows + multi-light interaction are visible.
- The `CameraRig.prefab` instance with the `CameraRenderPipeline` and `PixelArtRenderPass` components added on the child Camera. The prefab itself is unmodified; only the sandbox-scene instance carries the additional components.
- A small in-scene UI text overlay (Unity UI canvas, `TextMeshPro` or `Text`):
  - Top-left: "Pixel-Art Sandbox — Press [P] to toggle effect on/off"
  - Bottom-left: live readouts of internal resolution + palette-quantize on/off + current FPS (sample over 60 frames).

The toggle key `P` flips the `enabled` flag on `PixelArtRenderPass` (or detaches/reattaches it from the pipeline list — Sonnet picks whichever is cleaner). Toggling lets Talon A/B compare the look in real time.

The test recipe:

```markdown
# Pixel-Art Shader Sandbox

## What this validates
The PixelArtRenderPass renders the scene at low internal resolution with
palette quantization, upsampled to screen with point filtering, and the
result reads as the early-2000s pixel-art look the aesthetic bible commits
to. The 30-FPS-at-30-objects performance gate holds.

## Setup (one-time)
None — the scene already has CameraRig + render pipeline configured.

## Test (every run, ~5–10 minutes)

1. Open Assets/_Sandbox/pixel-art-shader.unity.
2. Press Play.
3. Verify:
   - The default view shows the cubes/spheres/capsule rendered as crisp
     pixel art. Edges are blocky in the intended way; no anti-aliasing
     fuzz; colors are quantized to a small palette.
   - Lighting reads: the spheres still show specular highlights and shading
     gradients (just quantized); shadows from the directional light still
     fall on the ground plane.
   - The point light's color contribution is visible on nearby primitives.

4. Press P. The effect detaches; you should see the underlying high-res
   render. Press P again to restore. Toggle a few times to compare.

5. With the effect on, in the Inspector on the camera:
   - Change "Internal Resolution" between Crisp / Chunky. Confirm
     visible difference.
   - Toggle "Palette Quantize" off (sample-only, no quantization).
     Confirm colors smooth out.
   - Try a custom internal resolution (e.g., 240×135). Confirm scaling.

6. Performance check: spawn 30 cubes via the in-scene "Spawn 30" button
   (a tiny editor-time helper script the packet includes). FPS readout
   should hold ≥60 with the effect on. If it doesn't, file as a follow-up.

7. (Optional, takes ~2 minutes) Enable the experimental "Outline Pass"
   (a stub second pass the packet includes solely to validate the
   ICameraRenderPass registration works). Confirm it visibly draws outlines
   on top of the pixel-art result. Disable it. The stub exists only to
   prove the chain order works; it's not a shipping feature.

## Pass / fail
- Pass: pixel-art look reads as intended; toggle works; FPS holds at 60+
  with 30 cubes; outline-pass stub validates the chain.
- Fail: any of the above. File a follow-up packet describing the failure mode.
```

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `dotnet build` is green; `dotnet test` is green for any affected test projects. | build + test |
| AT-02 | `Assets/_Sandbox/pixel-art-shader.unity` opens without errors in Unity Editor. | Editor open |
| AT-03 | The 5–10 minute test recipe passes for Talon. | manual visual |
| AT-04 | The pixel-art look reads as intended at default Chunky resolution and at Crisp resolution. | manual visual |
| AT-05 | The `P` key toggle correctly attaches/detaches the pass without errors. | manual visual |
| AT-06 | The "Spawn 30" button keeps FPS at ≥60 with the effect on, in a non-debug Editor play session. | manual visual |
| AT-07 | The stub outline pass validates `ICameraRenderPass` ordering and chaining. | manual visual |
| AT-08 | `MainScene.unity` is unchanged. `PlaytestScene.unity` is unchanged. | git diff |
| AT-09 | No new package dependency added to `ECSUnity/Packages/manifest.json`. | git diff |
| AT-10 | MAC-009 entry in `MOD-API-CANDIDATES.md` reflects the actual interface shape shipped. | review |

---

## Mod API surface

This packet introduces **MAC-009: ICameraRenderPass**. See `docs/c2-infrastructure/MOD-API-CANDIDATES.md`.

The interface shape, registration mechanism, and ordering semantics are likely to settle quickly (one fresh consumer here, the second one will land within Phase 4.0.x as another visual pass arrives). Future packets that add render passes (4.0.D floor identity may need an outline pass; 4.0.E NPC silhouette polish may want a one-pixel rim-light pass; 4.4.x ship-prep may add a CRT scanline option) all consume this interface without modifying the pipeline core.

The stub outline pass shipped with the sandbox scene is intentional: it exists solely to prove a second consumer of the interface works. It is **not** a shipping feature; it does not get integrated into MainScene or PlaytestScene by the `-INT` packet.

---

## Followups (not in scope)

- `WP-4.0.A-INT` — integrate `CameraRenderPipeline` + `PixelArtRenderPass` into `PlaytestScene.unity` (and possibly `MainScene.unity`). Talon-decided; one-line wiring change once the sandbox is validated.
- URP migration evaluation. Future packet, only if built-in pipeline limits become real.
- Palette curation pass with art-pipeline collaborator. Future content packet.
- Additional render passes (CRT scanline, film grain, real outline shader). Future packets.
- Per-camera render-pipeline config (different cameras get different passes). Future, if needed.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

This is a Track 2 (Unity) packet. xUnit tests are necessary but **not sufficient** — the visual layer must be verified by Talon in Unity Editor before PR is mergeable.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm you are in a dedicated worktree at `.claude/worktrees/sonnet-wp-4.0.a/` on branch `sonnet-wp-4.0.a` based on recent `origin/staging`. If anything is wrong, stop and notify Talon.
1. Implement the spec — write scripts, build the shader, compose sandbox scene per spec.
2. Add or update xUnit tests where applicable (logic-level only; visuals are not unit-tested).
3. Run `dotnet test` and `dotnet build`. Must be green.
4. Stage all changes including the self-cleanup deletion (see below).
5. Commit on the worktree's feature branch.
6. Push the branch.
7. Stop. Do **not** open a PR yet. Do **not** merge.
8. Notify Talon (via the commit message's final line: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/pixel-art-shader.md`) that the branch is ready for Talon's manual sandbox-recipe pass.

Talon's pipeline (after Sonnet's push):

1. Open the Unity Editor on the feature branch.
2. Run the test recipe in `Assets/_Sandbox/pixel-art-shader.md`.
3. If the recipe passes: open the PR, merge to `staging`.
4. If the recipe fails: file the failure in a follow-up packet or as PR review comments. Do not ask the original Sonnet to iterate ad-hoc — failed visual recipes mean the spec was incomplete or the implementation diverged; either way, a fresh packet captures the fix cleanly.

### Feel-verified-by-playtest acceptance flag

**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN (post-`-INT`):** the pixel-art shader's read at 30 NPCs scale, iconography crispness at intended camera altitudes, the 30-NPCs-at-60-FPS gate.

The sandbox test recipe covers first-light: does the effect compile, render, toggle, and chain correctly? The post-`-INT` PT session will evaluate whether the look feels right with the actual office scene populated.

### Cost envelope (1-5-25 Claude army)

Target: **$0.50–$1.20** per packet. If costs approach the upper bound without acceptance criteria nearing completion, **escalate to Talon** by stopping work and committing a `WP-4.0.A-blocker.md` note.

Unity-specific cost-discipline:
- Don't iterate the shader through Editor-recompile cycles repeatedly. Use the Frame Debugger and shader cache.
- Don't probe the Asset database in a loop — load palette texture once, hold the reference.
- Sandbox geometry is the smallest thing that exercises the feature. Do not build elaborate scenes.

### Self-cleanup on merge

The active `docs/c2-infrastructure/work-packets/` directory should contain only **pending** packets. Shipped packets are deleted, not archived to `_completed-specs/`.

Before opening the PR (after Talon's visual verification passes):

1. **Check downstream dependents:**
   ```bash
   git grep -l "WP-4.0.A" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. **If the grep returns no results**: include `git rm docs/c2-infrastructure/work-packets/WP-4.0.A-pixel-art-post-process-sandbox.md` in the staging set. Add `Self-cleanup: spec file deleted, no pending dependents.` to the commit message.

3. **If the grep returns the pending `WP-4.0.A-INT` packet (likely)**: leave the spec file in place. Add a one-line status header:
   ```markdown
   > **STATUS:** SHIPPED to staging YYYY-MM-DD. Retained because pending packets depend on this spec: WP-4.0.A-INT.
   ```
   Add `Self-cleanup: spec retained, dependents: WP-4.0.A-INT.` to the commit message.

4. **Sandbox prefabs and scenes are NOT deleted** — they live in `Assets/_Sandbox/` indefinitely.

5. **Do not touch** files under `_completed/` or `_completed-specs/`.
