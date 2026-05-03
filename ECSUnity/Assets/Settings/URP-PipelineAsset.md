# URP Pipeline Asset — Settings Notes

> **WP-4.0.A artifact.** Documents the non-default settings applied to the URP pipeline asset and explains each choice.

## How the assets are created

`UrpSetupEditor.cs` creates these assets automatically on first Unity Editor open after WP-4.0.A is checked out:

| File | Description |
|:---|:---|
| `URP-PipelineAsset.asset` | UniversalRenderPipelineAsset — the active SRP asset assigned in Graphics Settings and all Quality tiers. |
| `URP-PipelineAsset_Renderer.asset` | UniversalRendererData — the Universal Renderer configuration (forward rendering path; no custom renderer features yet). |

After `UrpSetupEditor.cs` runs:
1. Graphics Settings → Scriptable Render Pipeline Settings → points to `URP-PipelineAsset.asset`.
2. All Quality tiers (Very Low through Ultra) → Custom Render Pipeline → points to `URP-PipelineAsset.asset`.

## Non-default settings

All settings are left at URP defaults for v0.2. No tweaks applied. The goal of WP-4.0.A is parity with the built-in pipeline; visual improvement is out of scope.

Notable defaults (for future reference):

| Setting | Default Value | Notes |
|:---|:---|:---|
| Rendering Path | Forward | Adequate for ECSUnity's lighting model (~10 simultaneous point lights max). Revisit if many lights become a perf concern. |
| HDR | Enabled | Default on for Unity 6 URP. No impact at v0.2 quality. |
| Anti-aliasing | None | Per-Quality-tier AA handled in QualitySettings. |
| Shadow Distance | 50 m | URP default. ECSUnity's camera pitch means shadows rarely show; this is fine. |
| Cascade Count | 1 | URP default for low + medium quality tiers. Shadows aren't a visual focus in v0.2. |

## Renderer features

None added in WP-4.0.A. The pixel-art downscale + palette-quantize pass (MAC-009 first consumer) ships in WP-4.0.A1 as a `ScriptableRendererFeature` added to this renderer asset.

## Material conversion

After visual verification, run the URP Converter once:
- *Window → Rendering → Render Pipeline Converter*
- Select **Built-in to URP** pipeline
- Check **Material Upgrade** and **Read-only Material Converter**
- Click **Convert**

This re-points any Unity built-in materials (Standard, etc.) to their URP equivalents. The four custom ECSUnity shaders (`BeamProjection`, `LightHalo`, `Outline`, `RoomTint`) were manually rewritten in WP-4.0.A and do not need the converter.

## Followup packets

- **WP-4.0.A1** — Adds the pixel-art Renderer Feature to `URP-PipelineAsset_Renderer.asset`.
- **WP-4.0.D / E / H** — May add additional renderer features and post-process volumes.
- Future: per-quality-tier renderer assets (different feature stacks for low/medium/high). Not in scope for v0.2.
