# Pixel-Art Shader Sandbox

Tests the URP PixelArtRendererFeature in isolation against six test primitives.
Validates: pixel-art look, palette quantize toggle, FPS gate, and two-feature chain.

---

## Setup (one-time)

1. Confirm URP is active: `Edit > Project Settings > Graphics` → Scriptable Render Pipeline
   Asset should be `URP-PipelineAsset`.
2. Open `Assets/_Sandbox/pixel-art-shader.unity`.
3. In the **SandboxController** Inspector, verify:
   - `_feature` references the **PixelArtRendererFeature** sub-asset inside
     `Assets/_Sandbox/SandboxURP-Renderer.asset`.
   - `_statsText` references the **StatsText** canvas object.
   - `_sandboxRendererIndex` = **1**.
4. If Unity shows a yellow warning "renderer feature outdated" on `SandboxURP-Renderer.asset`,
   click **Fix** once — Unity will regenerate the feature map.

---

## Test (every run)

1. Open `Assets/_Sandbox/pixel-art-shader.unity`.
2. Press **Play**.

---

## Expected results

### AT-02 / AT-03 — Scene opens and pixel-art look reads

- Scene loads without errors in Console.
- The six primitives (3 cubes, 2 spheres, 1 capsule) render with a chunky low-res look:
  pixels are clearly visible and blocky — **not** smooth/anti-aliased.
- Colors are palette-quantized: should look like an early-2000s PC game palette
  (navy blues, warm beiges, saturated accent colors).
- Shadows from the directional light are visible and real (not baked).
- Bottom-left HUD shows: `Resolution: 320×180 (Chunky)  |  Palette Quantize: ON  |  Effect: ON  |  FPS: NNN`

### AT-04 — Resolution presets

1. Select **SandboxController** in the Hierarchy.
2. In the `_feature` field, click the sub-asset arrow to reveal **PixelArtRendererFeature**.
3. Change `Settings > Preset` to **Crisp** (480×270). Pixels should become noticeably finer.
4. Change back to **Chunky** (320×180). Chunky look should return.
5. Switch to **Custom** and set `customResolution` to e.g. `160 × 90`. Effect should be extreme.

### AT-05 — P key toggle

1. Press **P**. Effect turns OFF. Scene renders at full resolution (smooth/sharp).
2. Press **P** again. Effect turns ON. Pixel-art look returns.
3. HUD `Effect:` line updates in sync.
4. No errors in Console during toggling.

### AT-06 — FPS gate (30 cubes)

1. Click **Spawn 30** button (bottom-left of screen).
2. 30 cubes appear at random positions on the ground plane.
3. Wait 2–3 seconds for FPS to stabilise (60-frame rolling average).
4. **Expected:** FPS ≥ 60 with effect ON, in a non-debug Play mode build.
   (In Editor with profiler overhead, FPS may be lower — that's expected.)
5. Click **Spawn 30** again; confirm no crash or significant FPS drop below 60.

### AT-07 — Stub outline chain

1. While in Play, screen edges should have a red vignette tint —
   this is the `OutlineStubRendererFeature` proving the second pass executes.
2. Open `Assets/_Sandbox/SandboxURP-Renderer.asset` in Inspector.
3. Confirm it shows two features: **PixelArtRendererFeature** and **OutlineStubRendererFeature**.
4. Toggle `OutlineStubRendererFeature > Active` off → red vignette disappears.
5. Toggle back on → red vignette returns. No errors.

### AT-08 — Production scenes unchanged

After exiting Play:
- `Assets/Scenes/MainScene.unity` — open, confirm no pixel-art effect, no errors.
- `Assets/Scenes/PlaytestScene.unity` — same.
- `Assets/Settings/URP-PipelineAsset_Renderer.asset` — open, confirm `m_RendererFeatures`
  is still empty (no features were added to the production renderer).

---

## If it fails

### No pixel-art look on Play

- Check `SandboxURP-Renderer.asset` is listed at index 1 in `URP-PipelineAsset`.
- Check `SandboxController._sandboxRendererIndex` = 1 in Inspector.
- Verify `Custom/PixelArtQuantize` shader compiled (check Console for shader errors).
- Verify `DefaultPixelArtPalette.png` is assigned to `PixelArtRendererFeature.settings.paletteTexture`.

### Red vignette missing (stub chain not working)

- Check `OutlineStubRendererFeature` is present in `SandboxURP-Renderer.asset` and Active.
- Verify `Custom/OutlineStub` shader compiled (Console).

### FPS below 60 with 30 cubes

- URP batch draw calls should handle 30 cubes easily; check for accidental shadow maps on
  the point light (disable shadows on Point Light if present).
- Confirm `m_IntermediateTextureMode: 1` in `SandboxURP-Renderer.asset` (compatible mode).
