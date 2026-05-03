# WP-4.0.A1 — Pixel-Art Renderer Feature Sandbox

> **DO NOT DISPATCH UNTIL WP-4.0.A IS MERGED.** This packet authors a URP `ScriptableRendererFeature`; URP must be the active pipeline first.
> **Wave 2 of the Phase 4.0.x foundational polish wave.** First visual feature on the URP foundation. Track 2 sandbox packet under `docs/UNITY-PACKET-PROTOCOL.md`. Companion `-INT` packet (not yet drafted) will integrate into PlaytestScene + MainScene.

**Tier:** Sonnet
**Depends on:** WP-4.0.A (URP migration foundation must be merged).
**Parallel-safe with:** WP-4.0.B, WP-4.0.C (engine packets, disjoint surface).
**Timebox:** 120 minutes
**Budget:** $0.60
**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** the pixel-art look's read at 30 NPCs scale (post-`-INT`); iconography crispness at intended camera altitudes; the FPS gate.

---

## Goal

Ship a **URP `PixelArtRendererFeature` (ScriptableRendererFeature subclass)** plus the sandbox scene that validates it. The feature renders the world's low-poly 3D geometry at a low internal resolution and upscales with point-filtering to produce the early-2000s pixel-art look the aesthetic bible commits to. Real shadows and URP's lighting are preserved; the pixel-art treatment is purely an output filter.

After this packet, Talon can:
- Open `Assets/_Sandbox/pixel-art-shader.unity`, press play, see test primitives rendered in the target style, toggle the feature on/off, and adjust internal-resolution + palette parameters.
- Confirm the URP Renderer Feature pattern is the right Mod API surface for visual extensions (MAC-009, post-revision in WP-4.0.A).
- Author future visual passes (CRT scanline, outline, film grain) following the same pattern — a stub second pass is included in the sandbox to validate the chain.

The 30-NPCs-at-60-FPS gate is the safety rail. URP's leaner per-draw-call cost should preserve headroom; if the pixel-art feature regresses below 60 FPS in a synthetic 30-cube test, settings are wrong.

---

## Reference files

- `docs/UNITY-PACKET-PROTOCOL.md` — read in full. Rule 6 (feel-verified-by-playtest) applies.
- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` — read MAC-009 (post-WP-4.0.A revision: URP ScriptableRendererFeature pattern). This packet is the first consumer.
- `docs/c2-content/aesthetic-bible.md` — read the rendering commitments. Pixel-art-from-3D, real shadows from real lights, era-appropriate palette (CRT-beige-and-blue + saturated chibi accents).
- `docs/c2-content/ux-ui-bible.md` §1.6 — iconography vocabulary. The pixel-art treatment must not destroy chibi emotion-cue legibility.
- `ECSUnity/Assets/Settings/URP-PipelineAsset.asset` (created in WP-4.0.A) — the active URP pipeline asset.
- `ECSUnity/Assets/Settings/URP-PipelineAsset_Renderer.asset` (created in WP-4.0.A) — the renderer data; this packet adds the new feature to its feature list (in the sandbox-scene context only — see Design notes).
- `ECSUnity/Assets/Prefabs/CameraRig.prefab` — the canonical camera. Sandbox scene uses an instance.
- URP documentation on ScriptableRendererFeature: <https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/manual/renderer-features/scriptable-renderer-features/inject-a-pass-using-a-scriptable-renderer-feature.html> — Sonnet should consult for current API.

---

## Non-goals

- Do **not** modify the URP pipeline asset itself such that the pixel-art feature is forced on for all scenes. The sandbox uses a *scene-local* renderer-data variant or a feature toggle. MainScene + PlaytestScene are unaffected by this packet.
- Do **not** modify `MainScene.unity` or `PlaytestScene.unity`. Integration is the `-INT` packet.
- Do **not** add lighting features (lighting upgrades are WP-4.0.D's territory).
- Do **not** add particle effects (those are WP-4.0.H).
- Do **not** add NPC silhouettes to the sandbox. Bare primitives only.
- Do **not** introduce dependencies on third-party shader packs.
- Do **not** roll back to a built-in `OnRenderImage`-style implementation. URP is the substrate; ScriptableRendererFeature is the pattern.

---

## Design notes

### Architecture: ScriptableRendererFeature + ScriptableRenderPass

URP extension model:
- A `ScriptableRendererFeature` is a serializable class that can be added to a URP renderer data asset.
- It owns one or more `ScriptableRenderPass` instances and injects them into the URP frame at the right injection point (`AfterRenderingTransparents`, `AfterRenderingPostProcessing`, etc.).
- The render pass executes during URP's frame and can read/write camera color and depth.

For the pixel-art effect:

```csharp
public class PixelArtRendererFeature : ScriptableRendererFeature {
    [Serializable]
    public class Settings {
        public PixelArtPreset preset = PixelArtPreset.Chunky;  // Crisp / Chunky / Custom
        public Vector2Int customResolution = new(320, 180);
        public Texture2D paletteTexture;  // 1xN or NxN palette
        public bool paletteQuantize = true;
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public Settings settings = new();
    PixelArtRenderPass _pass;

    public override void Create() { _pass = new PixelArtRenderPass(settings); }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        renderer.EnqueuePass(_pass);
    }
}

class PixelArtRenderPass : ScriptableRenderPass {
    Material _quantMaterial;
    RTHandle _lowResTarget;
    Settings _settings;

    public PixelArtRenderPass(Settings s) {
        _settings = s;
        renderPassEvent = s.injectionPoint;
        _quantMaterial = CoreUtils.CreateEngineMaterial("Custom/PixelArtQuantize");
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data) {
        Vector2Int res = ResolveResolution(_settings, data.cameraData.cameraTargetDescriptor);
        var desc = data.cameraData.cameraTargetDescriptor;
        desc.width = res.x; desc.height = res.y;
        desc.depthBufferBits = 0;
        RenderingUtils.ReAllocateIfNeeded(ref _lowResTarget, desc, FilterMode.Point);
    }

    public override void Execute(ScriptableRenderContext ctx, ref RenderingData data) {
        var cmd = CommandBufferPool.Get("Pixel Art");
        // Down-sample camera target to low-res with bilinear; apply palette in shader.
        Blitter.BlitCameraTexture(cmd, data.cameraData.renderer.cameraColorTargetHandle, _lowResTarget,
            _quantMaterial, _settings.paletteQuantize ? 0 : 1);
        // Up-sample low-res back to camera target with point filtering.
        Blitter.BlitCameraTexture(cmd, _lowResTarget, data.cameraData.renderer.cameraColorTargetHandle);
        ctx.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
```

(The above is illustrative; Sonnet adapts to the precise URP 17.x API.)

### Companion shader

`Assets/Shaders/PixelArtQuantize.shader` — URP-style HLSL shader with two passes:
- Pass 0: down-sample + palette quantize (samples source, snaps each pixel's color to nearest palette entry).
- Pass 1: down-sample only (no quantize), for the toggle-quantize-off mode.

Use `UnityCG.cginc` URP equivalents: `#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"`.

### Sandbox scene contents

`Assets/_Sandbox/pixel-art-shader.unity`:

- 30×30 grid plane (gray, lit) so the camera has something to look at.
- Six test primitives at varied distances:
  - Three reference cubes (red, green, blue) at `(5, 0.5, 5)`, `(15, 0.5, 5)`, `(25, 0.5, 5)`.
  - Two PBR spheres (one metallic, one smooth) at `(10, 0.5, 15)`, `(20, 0.5, 15)`.
  - One capsule with a checker texture at `(15, 0.5, 25)`.
- One Directional Light (warm tint, intensity 1.1, rotation Euler `(50, -30, 0)`) and one Point Light (cool tint, intensity 0.6, position `(15, 3, 15)`).
- The `CameraRig.prefab` instance.
- A scene-local URP renderer data asset (`Assets/_Sandbox/SandboxURP-Renderer.asset`) that includes the `PixelArtRendererFeature`. The sandbox scene is configured to use this renderer-data variant. **Production scenes are not affected.**
- An in-scene UI:
  - Top-left: "Pixel-Art Sandbox — Press [P] to toggle effect on/off"
  - Bottom-left: live readouts of internal resolution + palette-quantize on/off + current FPS (sample over 60 frames)
  - "Spawn 30" button (calls a tiny editor-time helper to spawn 30 cubes for FPS testing)
- A small `SandboxToggle.cs` script wired to `P` that flips the feature's `isActive` field on the sandbox renderer data at runtime.
- A stub second feature (`OutlineStubRendererFeature`) included in the sandbox renderer data with a clearly-visible effect (e.g., draws a 1-pixel red outline on opaque geometry) — solely to validate that multiple features chain correctly. **Not a shipping feature; explicitly stub-tagged.**

### Default palette

Same approach as before: a 16-color palette texture biased toward CRT-era beige/blue plus saturated chibi accents. `Assets/Settings/DefaultPixelArtPalette.png` (1×16 RGBA). Sonnet authors a reasonable starting palette; iteration is post-playtest.

### Internal resolution presets

- `Crisp` — 480×270
- `Chunky` — 320×180 (sandbox default)
- `Custom` — Inspector-set width/height

### Test recipe (5–10 minutes, sibling `Assets/_Sandbox/pixel-art-shader.md`)

Same shape as the original draft, adapted for URP terminology. The recipe walks Talon through: open scene, press play, verify pixel-art look reads, toggle effect with P, change resolution preset in Inspector, toggle palette quantize, verify FPS holds at 30 cubes, verify stub outline pass chains correctly, verify visual parity if effect is disabled.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSUnity/Assets/Scripts/Render/PixelArtRendererFeature.cs` | URP renderer feature; serializable Settings; settings-driven render pass. |
| code | `ECSUnity/Assets/Scripts/Render/PixelArtRenderPass.cs` | The render pass class. |
| code | `ECSUnity/Assets/Scripts/Render/Sandbox/OutlineStubRendererFeature.cs` | Stub second feature for chain validation. Sandbox-only; not for production use. |
| code | `ECSUnity/Assets/Scripts/Render/Sandbox/SandboxToggle.cs` | `P`-key toggle for sandbox. |
| shader | `ECSUnity/Assets/Shaders/PixelArtQuantize.shader` | URP-style two-pass quantize/blit shader. |
| asset | `ECSUnity/Assets/_Sandbox/SandboxURP-Renderer.asset` | Sandbox-only URP renderer data variant. |
| asset | `ECSUnity/Assets/Settings/DefaultPixelArtPalette.png` | 16-color palette texture. |
| asset | `ECSUnity/Assets/Settings/DefaultPixelArtPalette.md` | Documents palette colors + rationale. Brief. |
| scene | `ECSUnity/Assets/_Sandbox/pixel-art-shader.unity` | Sandbox per Rule 4. |
| doc | `ECSUnity/Assets/_Sandbox/pixel-art-shader.md` | 5–10 minute test recipe. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Confirm MAC-009 reflects this packet's role as first consumer. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `dotnet build` is green; Unity test runner edit-mode + play-mode all green. | build + test |
| AT-02 | `Assets/_Sandbox/pixel-art-shader.unity` opens without errors. | Editor open |
| AT-03 | The 5–10 minute test recipe passes for Talon. | manual visual |
| AT-04 | Pixel-art look reads as intended at default Chunky and at Crisp resolution. | manual visual |
| AT-05 | `P` key toggle correctly enables/disables the feature without errors. | manual visual |
| AT-06 | "Spawn 30" cubes test holds FPS ≥60 with the feature on, in non-debug Play. | manual visual |
| AT-07 | Stub outline feature visibly chains after the pixel-art pass. | manual visual |
| AT-08 | `MainScene.unity` is unchanged. `PlaytestScene.unity` is unchanged. The production URP renderer data asset is unchanged. | git diff |
| AT-09 | The sandbox renderer-data variant is referenced only by the sandbox scene's pipeline-asset override. | review |
| AT-10 | MAC-009 entry in `MOD-API-CANDIDATES.md` correctly cites this packet as first consumer. | review |

---

## Mod API surface

This packet is the **first consumer of MAC-009 (post-WP-4.0.A revision)**: URP ScriptableRendererFeature pattern. The pixel-art feature is the canonical example modders study to add their own visual passes.

The stub outline feature shipped in the sandbox is intentional — it proves the chain works with two features. It is **not** a shipping feature and is not integrated into MainScene or PlaytestScene by the `-INT` packet.

This packet does NOT introduce new MAC entries. MAC-009 graduates from *fresh* to *stabilizing* once a third consumer lands (likely WP-4.0.D or WP-4.0.E adding a one-pixel rim-light pass or a floor-tile-edge pass).

---

## Followups (not in scope)

- `WP-4.0.A1-INT` — integrate `PixelArtRendererFeature` into the production URP renderer asset (and thus into MainScene + PlaytestScene). Talon-decided; one-line addition once the sandbox is validated.
- Palette curation pass with art-pipeline collaborator. Future content packet.
- Additional render passes (CRT scanline, film grain, real outline shader). Future packets.
- Per-camera renderer overrides (e.g., a debug camera that bypasses the pixel-art pass). Future tooling.
- Shader Graph version of the quantize shader. Future, only if HLSL-direct becomes painful to maintain.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

Track 2 sandbox packet. Visual verification by Talon required.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm worktree at `.claude/worktrees/sonnet-wp-4.0.a1/` on branch `sonnet-wp-4.0.a1` based on recent `origin/staging` (which by now includes WP-4.0.A's URP migration).
1. Verify URP is the active pipeline (`Edit > Project Settings > Graphics`); if not, WP-4.0.A hasn't merged yet — stop and notify Talon.
2. Implement the spec.
3. Run all Unity tests + `dotnet test`. All must stay green.
4. Stage all changes.
5. Commit on the worktree's feature branch.
6. Push the branch.
7. Stop. Do **not** open a PR yet. Do **not** merge.
8. Notify Talon: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/pixel-art-shader.md`.

### Feel-verified-by-playtest acceptance flag

**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN (post-`-INT`):** the pixel-art look at 30 NPCs scale; iconography crispness at intended camera altitudes; FPS gate.

### Cost envelope

Target: **$0.50–$1.20**. If costs approach $1.50, escalate via `WP-4.0.A1-blocker.md`.

URP-specific cost discipline:
- Use URP's `Blitter.BlitCameraTexture` API (not deprecated `Graphics.Blit` patterns) for the down/up-sample.
- Don't fight the renderer-data injection point — `AfterRenderingPostProcessing` is the right slot for camera-output filters.
- Don't experiment with multiple shader variants. Two passes (quantize / no-quantize) is enough.

### Self-cleanup on merge

Same protocol as other Track 2 packets. Check for `WP-4.0.A1-INT` as a likely dependent; retain spec if it's pending.
