# Pixel-Art Dithering + Contact Shadow — How It Works

This document explains every layer of the visual system active in `pixel-art-shader.unity`:
the Bayer dithering post-process and the custom contact shadow. It is written so you can
follow each step from raw 3-D render to final dithered image.

---

## 1. Where This Lives Inside Unity's Render Pipeline

Unity's **Universal Render Pipeline (URP)** replaces the old "Built-in" renderer. Instead of
one fixed render loop, URP lets you inject custom code at specific points in the frame using
**ScriptableRendererFeature** and **ScriptableRenderPass**. Think of the renderer as an
ordered list of passes that execute one after another to build up the final image.

The default renderer for this project is `URP-PipelineAsset_Renderer.asset`. It contains one
custom feature: **PixelArtRendererFeature**. The sandbox scene also uses this same renderer.

### 1.1 Frame Timeline (simplified)

```
URP frame for one camera
│
├── Shadow map pass          (only if a light has shadows enabled)
│
├── Depth prepass            (writes depth buffer)
│
├── Opaque geometry pass     (renders solid objects: ground, spheres, cubes)
│   └── Each object's material shader runs here
│       → URP Lit shader: reads ambient + lights, writes colour + depth
│
├── Skybox
│
├── Transparent geometry pass   ◄── BlobShadow disc renders here
│   └── Blend DstColor Zero (multiply): darkens ground under the disc
│
├── Built-in post-process (URP's bloom, tone-map, etc.)
│
└── PixelArtRendererFeature (event 600 = AfterRenderingPostProcessing)
    └── Copy camera colour → temp buffer
    └── Bayer-dither temp → camera colour     ◄── converts everything to palette + dots
```

The critical ordering: the blob shadow renders *before* the Bayer dithering runs. This means
the soft grey gradient of the shadow disc is dithered just like the ball's shading — they go
through the same quantize step and naturally match.

---

## 2. The Bayer Dithering System

### 2.1 Overview

The goal is a retro look where every colour is snapped to a limited palette (16 entries), but
gradients still read as smooth because adjacent pixels alternate between two nearby palette
colours in a structured pattern. This is **ordered dithering**.

### 2.2 Files

| File | Role |
|------|------|
| `PixelArtRendererFeature.cs` | ScriptableRendererFeature: the "plug" that tells URP to run the pass |
| `PixelArtRenderPass.cs` | Two-pass RenderGraph implementation |
| `PixelArtQuantize.shader` | HLSL: Bayer threshold + palette snap |
| `DefaultPixelArtPalette.png` | 16×1 texture: the 16 allowed colours |

### 2.3 PixelArtRendererFeature.cs

`ScriptableRendererFeature` is the entry point. URP calls two methods on it:

- **`Create()`** — called once on load. Creates the `PixelArtRenderPass` and gives it the
  `Settings` object (palette texture, dither strength, injection point).
- **`AddRenderPasses(renderer, ref renderingData)`** — called every frame. If the feature is
  active, it enqueues the pass into the renderer's pass list for this frame.

The `Settings` class holds everything the shader needs:
```
paletteTexture    — the 16-colour PNG
paletteQuantize   — whether to snap to palette
ditherStrength    — how strong the Bayer offset is (0 = hard snap, 1 = full spread)
injectionPoint    — RenderPassEvent.AfterRenderingPostProcessing (value 600)
```

**P-key toggle**: `SandboxToggle.cs` calls `_feature.SetActive(true/false)`. When the feature
is inactive, `AddRenderPasses` returns immediately and the pass is never enqueued — no
performance cost.

### 2.4 PixelArtRenderPass.cs — RenderGraph (Unity 6 / URP 17)

Unity 6 uses a new **RenderGraph** API. You no longer write `Execute()` commands directly;
instead you *declare* what resources each pass reads and writes, and the RenderGraph executes
them in order.

The pass runs in two steps:

**Step 1 — Copy**
```
activeColorTexture  →  [plain blit, pass 1]  →  _PixelArtTemp
```
We cannot read from and write to the same texture in the same pass (GPU restriction), so the
current frame colour is copied to a temporary buffer first.

**Step 2 — Dither**
```
_PixelArtTemp  →  [Bayer dither, pass 0]  →  activeColorTexture
```
The shader reads from the temp copy and writes the dithered result back to the camera target.

The code pattern for each step:
```csharp
using (var builder = renderGraph.AddRasterRenderPass<PassData>("...", out var passData))
{
    passData.src      = someTextureHandle;
    builder.UseTexture(passData.src);              // declare READ
    builder.SetRenderAttachment(target, 0);        // declare WRITE
    builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) =>
        Blitter.BlitTexture(ctx.cmd, d.src, new Vector4(1,1,0,0), d.material, passIndex));
}
```

`Blitter.BlitTexture` draws a fullscreen triangle using the given material and pass index.
`requiresIntermediateTexture = true` on the pass tells URP to always provide a separate
intermediate colour buffer (required for the copy step to work reliably).

### 2.5 PixelArtQuantize.shader — Pass 0 (Bayer Dither)

This is where the visual magic happens. For every pixel on screen:

**Step A — Sample the source colour**
```hlsl
half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
```

**Step B — Find screen-space pixel coordinates**
```hlsl
uint2 pixelPos = (uint2)(input.texcoord * _ScreenParams.xy);
```
`_ScreenParams.xy` is the render target width/height in pixels. `input.texcoord` is 0–1 UV.
Multiplying gives the actual pixel index on screen.

**Step C — Look up the Bayer threshold**
```hlsl
const float bayer[16] = {
     0/16,  8/16,  2/16, 10/16,
    12/16,  4/16, 14/16,  6/16,
     3/16, 11/16,  1/16,  9/16,
    15/16,  7/16, 13/16,  5/16
};
float threshold = bayer[(pixelPos.y & 3) * 4 + (pixelPos.x & 3)];
```
This 4×4 matrix tiles across the screen. Each cell holds a different threshold (0/16 to 15/16).
The `& 3` operation is `mod 4` — it wraps the coordinates back to 0–3 so the pattern repeats
every 4 pixels.

**Why this matrix?** The Bayer 4×4 is specifically designed so that at any grey level, the
lit and dark pixels are distributed as evenly as possible. It avoids the clumping you'd get
from random noise. The structured dot pattern is what gives the retro look.

**Step D — Offset the colour by the threshold**
```hlsl
col.rgb = saturate(col.rgb + (threshold - 0.5) * _DitherStrength);
```
`(threshold - 0.5)` shifts the threshold so it's centred on zero (range -0.5 to +0.47).
Multiplied by `_DitherStrength` (default 0.25), the maximum push is ±0.125.

A pixel that is mid-grey (0.5) will be pushed to 0.5 ± 0.125 depending on its Bayer cell.
After palette quantize, some cells snap up to the lighter palette entry, some snap down to the
darker one — creating the characteristic checkerboard-like gradient.

**Step E — Snap to nearest palette colour**
```hlsl
half3 NearestPaletteColor(half3 col) {
    // Brute-force loop over the 16 palette entries
    // Returns whichever entry has the smallest squared RGB distance to col
}
col.rgb = NearestPaletteColor(col.rgb);
```
The palette texture is a 16×1 PNG. The shader samples it at `u = (i + 0.5) / 16` for each
entry and picks the closest one by squared colour distance.

---

## 3. The Custom Contact Shadow System

### 3.1 Why Not Use Unity's Shadows?

Unity's real-time shadow system generates a **shadow map**: the scene is rendered from the
light's point of view to a depth texture, then each fragment in the main pass checks whether
it is occluded from the light. This produces a binary sharp edge.

That sharp edge, when it passes through the Bayer dithering step, becomes an aliased staircase
of dither dots — which looks bad. More fundamentally, the shadow system is tightly coupled to
light objects. When you disable the light, shadows disappear.

The custom system replaces both concerns: it renders a smooth gradient independently of any
light, and the Bayer pass naturally converts that gradient into a dithered result that matches
the ball shading.

### 3.2 Files

| File | Role |
|------|------|
| `BlobShadowCaster.cs` | MonoBehaviour: spawns and repositions a shadow disc each frame |
| `BlobShadow.shader` | HLSL: gradient from dark contact tip to transparent far end |
| `BlobShadow.mat` | Material using the shader; carries `_Opacity` and `_Falloff` defaults |

### 3.3 BlobShadowCaster.cs — Geometry

`OnEnable` creates a Unity Quad primitive (a flat 1×1 mesh) as a standalone GameObject. It
sets its `MeshRenderer.sharedMaterial` to `BlobShadow.mat` and turns off shadow casting and
light probe usage so it has no effect on other objects.

Every frame in `LateUpdate`, the component repositions and rescales the disc:

**1. Contact point**
```csharp
Vector3 contactPoint = new Vector3(pos.x, 0f, pos.z);
```
This is directly below the sphere's centre on the ground plane — where the ball touches the
floor. This will become the darkest tip of the shadow.

**2. Flat shadow direction**
```csharp
Vector3 flatDir = new Vector3(dir.x, 0f, dir.z).normalized;
```
The light direction projected onto the ground plane. This is the direction the shadow tail
extends away from the contact point.

**3. Shadow length**
```csharp
float shadowLength = pos.y * (flatMag / Mathf.Abs(dir.y)) * _lengthScale + _radius;
```
`pos.y / |dir.y| * flatMag` is the geometric shadow length: how far the light ray from the
sphere centre travels along the ground before it reaches zero height. `_lengthScale` is a
multiplier to stretch or shrink the tail; `_radius` adds a minimum base length.

**4. Disc centre and scale**
```csharp
Vector3 discCenter = contactPoint + flatDir * (shadowLength * 0.5f);
_disc.transform.localScale = new Vector3(_radius * 2f, shadowLength, 1f);
```
The disc is centred halfway along the shadow tail. Its local Y axis covers the full
shadow length; its local X axis covers the disc width. This placement ensures:
- UV.y = 0 edge of the quad → contact point (where ball meets ground)
- UV.y = 1 edge of the quad → far shadow end

**5. Rotation**
```csharp
_disc.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
```
`Euler(90, 0, 0)` lays the quad flat (rotates it from facing +Z to facing +Y/up).
The yaw then spins it around the world Y axis so local +Y aligns with `flatDir`.
After this rotation, UV.y = 0 is at the contact point and UV.y = 1 is at the far end.

### 3.4 BlobShadow.shader — The Gradient

**Blend mode: multiply**
```hlsl
Blend DstColor Zero
```
The fragment output is multiplied against whatever colour is already in the framebuffer.
Output of `(1, 1, 1)` = no change. Output of `(0.3, 0.3, 0.3)` = darkens by 70%.

**Depth: no Z-write, with polygon offset**
```hlsl
ZWrite Off
Offset -1, -1
```
The disc sits at Y = 0.005 (just above the ground). `ZWrite Off` means it does not block
other transparent objects behind it. `Offset -1, -1` nudges the fragment slightly closer to
the camera in clip space, ensuring it passes the depth test against the ground even if they
share almost the same depth value.

**The gradient — forward direction**
```hlsl
float forwardT = uv.y;   // 0 = contact point, 1 = far shadow end
float forward  = pow(max(0.0, 1.0 - forwardT), _Falloff);
```
`pow(1 - t, falloff)`:
- At `t = 0` (contact): `pow(1, 1.5) = 1.0` → full opacity
- At `t = 0.5` (halfway): `pow(0.5, 1.5) ≈ 0.35` → 35% opacity
- At `t = 1.0` (far end): `pow(0, 1.5) = 0.0` → transparent

`_Falloff` controls the curve shape. 1.0 = linear. 1.5 = decays faster near the tip.
Higher values (2+) create a steep drop-off right at contact with a long faint tail.

**The gradient — lateral direction**
```hlsl
float halfW    = lerp(0.3, 1.0, saturate(forwardT * 1.4));
float lateralT = abs(uv.x - 0.5) * 2.0;
float lateral  = saturate(1.0 - smoothstep(0.5, 1.0, lateralT / halfW));
```
The shadow narrows near the contact point (halfW = 0.3 at the tip) and is full-width toward
the far end (halfW = 1.0 by about 70% along the shadow). `smoothstep(0.5, 1.0, ...)` gives
a soft rather than hard lateral edge.

**Final output**
```hlsl
float shadow = forward * lateral * _Opacity;
float mult   = 1.0 - shadow;
return half4(mult, mult, mult, 1.0);
```
`mult` is 1.0 (no effect) where there is no shadow, and approaches `1 - _Opacity` at the
darkest contact point. With `_Opacity = 0.75`, the contact point multiplies the ground
colour by 0.25 — making it 75% darker.

---

## 4. How the Two Systems Combine

The reason the shadow looks like it belongs to the ball shading, rather than looking like a
pasted-on overlay:

1. The BlobShadow disc renders a *smooth analogue gradient* onto the ground during the
   transparent pass. At this point it looks like a soft grey smear — no dots yet.

2. The `PixelArtRendererFeature` then runs. It has no knowledge of the shadow; it just
   sees the final camera colour, which now includes the dark gradient from the shadow disc.

3. The Bayer dithering applies the same threshold-and-snap process to every pixel uniformly.
   Pixels in the deep shadow are nudged and snapped to dark palette colours. Pixels in the
   transition zone alternate between nearby dark/medium palette entries. The result is the
   same kind of structured dot pattern that shades the ball.

Because both effects are subject to the same Bayer step, they are visually coherent —
shadow dots and shading dots come from the same matrix and the same palette.

---

## 5. Key URP Concepts Used

| Concept | What it means here |
|---------|-------------------|
| `ScriptableRendererFeature` | A plugin slot in the renderer. Survives scene loads. |
| `ScriptableRenderPass` | A single GPU work unit. Declares resources, records commands. |
| `RecordRenderGraph` | Unity 6 API: pass declares its reads/writes; RenderGraph schedules execution. |
| `RasterRenderPass<T>` | A pass that writes to a texture (rasterisation). T is the per-pass data struct. |
| `builder.UseTexture` | Declares a texture as read-only input for this pass. |
| `builder.SetRenderAttachment` | Declares the texture this pass writes to (colour attachment). |
| `Blitter.BlitTexture` | URP utility: draws a fullscreen triangle using a material+pass index. |
| `UniversalResourceData` | Provides `activeColorTexture` — the live camera colour buffer. |
| `UniversalCameraData` | Provides `cameraTargetDescriptor` for creating temporary textures. |
| `requiresIntermediateTexture` | Forces URP to allocate a separate intermediate buffer — required when a pass reads and writes the camera colour in two steps. |
| `RenderPassEvent (600)` | `AfterRenderingPostProcessing` — runs after URP's own post-process. |
| `m_IntermediateTextureMode: 1` | "Always" in the renderer asset — ensures the intermediate buffer is available regardless of platform. |
| `Blend DstColor Zero` | Multiply blend: `result = source * destination`. Source < 1 darkens. |
| `SetPropertyBlock` | Sets per-renderer shader values without creating a new material instance. |

---

## 6. Tuning Reference

### PixelArtRendererFeature (Inspector on `URP-PipelineAsset_Renderer.asset`)

| Property | Effect |
|----------|--------|
| `ditherStrength` | How far each pixel is pushed before palette snap. 0 = flat palette with no pattern. 0.25 = visible but subtle. 0.5+ = loud retro dots. |
| `paletteQuantize` | Toggle palette snap on/off. Off = only dithering, no colour restriction. |
| `paletteTexture` | Swap in a different 1×N PNG to change the colour palette entirely. |

### BlobShadowCaster (Inspector on `SphereSmooth`)

| Property | Effect |
|----------|--------|
| `_lightDirection` | Should match the direction of your primary light. Determines which way the shadow tail points. |
| `_radius` | Half-width of the shadow disc. Scale with the object's visual size. |
| `_lengthScale` | Stretches the shadow tail. Lower = compact under-body shadow. Higher = long theatrical shadow. |
| `_opacity` | Darkness at the contact point. 0.5 = subtle. 0.75 = strong. 1.0 = black. |

### BlobShadow.mat

| Property | Effect |
|----------|--------|
| `_Falloff` | Decay curve of the shadow. 0.5 = very gradual (lots of dark area). 1.5 = steep (dark only near contact). 3.0 = almost a pure contact dot. |

---

## 7. Extending to Multiple Casters

`BlobShadowCaster` is self-contained on each object. To add a shadow to any other object:
1. Add a `BlobShadowCaster` component to it.
2. Assign `BlobShadow.mat` to the `_shadowMaterial` slot.
3. Set `_lightDirection` to match the scene's primary light direction.
4. Adjust `_radius` to match the object's footprint.

Each caster creates its own disc — they render independently and overlap correctly because
multiply blends stack (two shadows in the same area produce a darker result).

---

## 8. Independence from Unity's Lighting

The blob shadow has zero dependency on any Unity light object. The illumination on the ball
and ground comes from URP's ambient environment lighting (always on), not from a light
GameObject. The shadow is produced purely by the multiply-blend disc geometry.

This matches the goal of the game engine: custom agent-driven light sources will supply their
own illumination model. When you implement those, you will:
1. Compute a light vector per-agent and pass it to `BlobShadowCaster._lightDirection`.
2. Drive `_opacity` from the light's computed intensity at the caster's position.
3. Keep the Bayer post-process in place — all shadows and shading will automatically adopt
   the same dithered palette look.
