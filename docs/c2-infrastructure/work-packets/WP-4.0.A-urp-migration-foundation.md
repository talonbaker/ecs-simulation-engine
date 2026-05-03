# WP-4.0.A — URP Migration Foundation

> **Wave 1 of the Phase 4.0.x foundational polish wave.** Per the 2026-05-02 calibration: the project adopts URP (Universal Render Pipeline) immediately rather than later, before custom-built-in-shader inventory grows further. URP migration now is bounded; URP migration after WP-4.0.D / E / H ship custom built-in pipeline content would compound into a multi-week refactor. This packet lays the rendering substrate every subsequent visual packet (4.0.A1 pixel-art Renderer Feature, 4.0.D floor identity, 4.0.E NPC visual readability, 4.0.H particle vocabulary, 4.1.x art pipeline) authors against. Track 2 (Unity) packet — but a *pipeline-migration* packet, not a typical sandbox-feature packet.

**Tier:** Sonnet
**Depends on:** Existing 3.1.x Unity scaffold (CameraRig, NpcSilhouetteRenderer, RoomRectangleRenderer, lighting scripts). No engine changes.
**Parallel-safe with:** WP-4.0.B (engine-only NPC anti-overlap), WP-4.0.C (engine + additive Unity read; the Unity touch is in `DragHandler.cs` which is independent of the render pipeline).
**Timebox:** 240 minutes (4 hours — pipeline migration is real work; the four existing custom shaders need URP-equivalent rewrites).
**Budget:** $1.20 (upper bound of the standard envelope; this packet is at the high end. Escalate if costs approach $1.50.)
**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** visual parity with pre-migration build; FPS-gate confirmation; no regression in any existing visual feature (silhouettes, lighting, beams, room tints, outlines, build mode, dev console, event log, time HUD).

---

## Goal

Migrate `ECSUnity` from Unity's built-in render pipeline to URP (Universal Render Pipeline). The project currently uses Unity 6 (6000.4.3f1) with built-in pipeline; URP is Unity's recommended modern pipeline and the substrate every subsequent Phase 4 visual packet should author against.

The migration is **purely architectural**: zero visible difference between pre-migration and post-migration builds. Same FPS, same lighting, same silhouettes, same outlines, same room tints, same beams. The packet's success criterion is *parity, not improvement*.

After this packet:
- ECSUnity uses URP as its active render pipeline.
- The four existing custom shaders (`BeamProjection`, `LightHalo`, `Outline`, `RoomTint`) have URP-equivalent versions that produce visually identical output.
- All existing scenes (`MainScene`, `PlaytestScene`, four sandbox scenes under `_Sandbox/`) render correctly under URP with no missing materials, no pink "shader not found" rendering, no broken lighting.
- The 30-NPCs-at-60-FPS gate holds (per-test verified).
- All existing Unity Edit-mode and Play-mode tests stay green.
- Every subsequent visual packet (4.0.A1, 4.0.D, 4.0.E, 4.0.H) inherits a URP-native foundation: `ScriptableRendererFeature` for render passes, URP shader template for new shaders, URP 3D Lighting for real shadows.

The Mod API consequence (MAC-009 revised): Unity's `ScriptableRendererFeature` IS the modder extension surface for visual passes. We don't author a custom `ICameraRenderPass` interface; we standardize on URP's existing extension pattern. This is genuinely better — modders work against Unity's documented interface, not our bespoke one.

---

## Reference files

- `docs/UNITY-PACKET-PROTOCOL.md` — read in full. Rule 3's "no MainScene modification" gets a packet-level exception here (see Design notes).
- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` — read MAC-009. This packet revises it from "custom ICameraRenderPass interface" to "URP ScriptableRendererFeature pattern (standardized)."
- `docs/c2-infrastructure/00-SRD.md` §8.7, §8.8 — read for the host-agnostic engine and incremental-API axioms. URP migration does not change either.
- `docs/PHASE-4-KICKOFF-BRIEF.md` — the 2026-05-02 restructure section; read for context on this packet's place in the wave.
- `ECSUnity/Packages/manifest.json` — confirm built-in pipeline (no URP package present); this packet adds it.
- `ECSUnity/ProjectSettings/ProjectVersion.txt` — Unity 6000.4.3f1. URP 17.x is the target version.
- `ECSUnity/Assets/Shaders/BeamProjection.shader` — read in full. Built-in pipeline; needs URP rewrite.
- `ECSUnity/Assets/Shaders/LightHalo.shader` — read in full. Built-in pipeline; needs URP rewrite.
- `ECSUnity/Assets/Shaders/Outline.shader` — read in full. Inverted-hull technique; needs URP rewrite (likely simpler than the others).
- `ECSUnity/Assets/Shaders/RoomTint.shader` — read in full. Built-in pipeline; needs URP rewrite.
- `ECSUnity/Assets/Scripts/Render/Lighting/*.cs` — read for context on how lighting is currently driven. Most should work unchanged with URP; double-check `LightSourceHaloRenderer` and `BeamRenderer` since they touch shaders.
- `ECSUnity/Assets/Scenes/MainScene.unity` — touched in this packet (material conversion). The scene file itself may not need editing, but its referenced materials get re-pointed to URP shaders.
- `ECSUnity/Assets/Scenes/PlaytestScene.unity` — same.
- `ECSUnity/Assets/_Sandbox/*.unity` — same.

---

## Non-goals

- Do **not** introduce new visual features. URP migration is parity-only. The pixel-art Renderer Feature is `WP-4.0.A1`, a separate packet.
- Do **not** convert to URP's 2D Renderer. The project is 2.5D top-down with real 3D geometry; the Universal Renderer (3D) is correct. (URP also has a 2D Renderer; this is *not* what we want.)
- Do **not** add any post-process volume effects (bloom, vignette, depth-of-field, etc.). Future packets only.
- Do **not** migrate to HDRP (overkill for 2.5D top-down).
- Do **not** rewrite the camera controller, input bindings, or constraints. Those are pipeline-agnostic.
- Do **not** change `WorldStateDto`, schemas, or any engine code. This is a Unity-side packet only.
- Do **not** touch `APIFramework.dll` or `Warden.*.dll` referenced by ECSUnity.
- Do **not** add Shader Graph-authored shaders in this packet. Future packets may use Shader Graph; the four existing shader rewrites are HLSL-direct (consistent with how they were originally authored).
- Do **not** modify lighting setups beyond what URP migration requires. (E.g., do not add additional point lights; do not change the directional light's intensity. Match pre-migration lighting exactly.)

---

## Design notes

### Why URP, why now (Talon's call, 2026-05-02)

Pre-Talon's-pivot, this packet leaned built-in pipeline ("ship visuals now, defer URP"). Talon overrode with the architectural argument: zero-cost-now vs compounding-cost-later. The four existing built-in shaders are bounded; the dozen+ built-in shaders the next 5 packets would ship are not. Migrate now.

Additional justifications Talon cited:
- URP is leaner on CPU per draw call — preserves CPU budget for the heavy ECS backend.
- URP is the multi-platform-friendly path (Apple Silicon, mobile, console) for the project's portability commitments.
- Shader Graph + VFX Graph (URP/HDRP only) become available for future visual work.
- Unity's prioritization is on URP/HDRP, not built-in.

### Rule 3 packet-level exception

UNITY-PACKET-PROTOCOL.md Rule 3 forbids modifying `MainScene.unity` outside of integration packets. This packet's exception rationale: URP material migration *requires* re-pointing material references project-wide; MainScene's referenced materials are part of "project-wide." The scene file YAML may or may not change (depends on whether materials are referenced by GUID + asset path or by inline definition); if it changes, the change is purely the renderer-pipeline-asset reference.

The Sonnet executor minimizes the MainScene diff: ideally the only scene-file changes are the auto-generated material reference updates from running Unity's Render Pipeline Converter. Manually editing MainScene to add features is forbidden (no new GameObjects, no Inspector tweaks, no transform changes).

### URP version + package selection

Add to `ECSUnity/Packages/manifest.json`:
```json
"com.unity.render-pipelines.universal": "17.0.4"
```

(Version 17.x is the Unity 6 LTS-aligned URP. Sonnet may bump to the latest 17.x patch available in the registry; do not jump to 18.x or 19.x without explicit reason.)

Adding this package automatically pulls in `com.unity.render-pipelines.core` and `com.unity.shadergraph` as transitive dependencies. That's expected.

### Migration sequence (the Sonnet's pipeline)

1. **Pre-flight.** Confirm worktree on `sonnet-wp-4.0.a` based on recent `origin/staging`. Confirm `dotnet build` clean and existing tests green BEFORE making any changes.
2. **Add URP package** to `manifest.json`. Open Unity Editor; let it import.
3. **Create URP assets.** `Assets/Settings/URP-PipelineAsset.asset` (URP Asset) + auto-created `URP-PipelineAsset_Renderer.asset` (Universal Renderer Data). Use Unity menu *Assets > Create > Rendering > URP Asset (with Universal Renderer)*.
4. **Set Graphics Settings.** *Edit > Project Settings > Graphics > Scriptable Render Pipeline Settings* points to the new URP Asset. Also set in *Quality* settings for each tier (URP supports per-quality renderer assets; for v0.2, point all quality tiers at the same asset).
5. **Run the URP Converter.** *Window > Rendering > Render Pipeline Converter*. Select "Built-in to URP" pipeline; check at minimum "Material Upgrade" and "Read-only Material Converter". Run.
6. **Address custom shader fallout.** The four custom shaders won't auto-convert. Manually rewrite each:
   - `BeamProjection.shader` → URP-equivalent. Use URP's lit/unlit shader includes (`#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"` etc.). Match output visually.
   - `LightHalo.shader` → URP-equivalent.
   - `Outline.shader` → URP-equivalent. Inverted-hull technique works in URP; the `Cull Front` pass structure transfers directly.
   - `RoomTint.shader` → URP-equivalent.
   For each, the `Shader "Custom/X"` name can stay the same (or rename to `Universal Render Pipeline/Custom/X` if Sonnet prefers — both work; first is less disruptive to existing material references).
7. **Validate every scene visually.** Open `MainScene`, `PlaytestScene`, `_Sandbox/camera-rig.unity`, `_Sandbox/draggable-prop.unity`, `_Sandbox/inspector-popup.unity`, `_Sandbox/selection-outline.unity`. Verify each renders without pink materials, without missing-shader errors, without broken lighting. Compare side-by-side to pre-migration behavior using a screenshot-pair if necessary.
8. **Run all Unity tests.** Edit-mode + Play-mode. All must stay green. Particularly the `PerformanceGate30NpcAt60FpsTests` and its variants.
9. **Run `dotnet build` and `dotnet test`.** Engine + non-Unity test projects must stay green.
10. **Commit and push.** `READY FOR VISUAL VERIFICATION — open MainScene + PlaytestScene + each _Sandbox scene; visual parity check.`

### Custom-shader URP rewrite notes

For each of the four shaders, the pattern is:

**Built-in (pre):**
```hlsl
Shader "Custom/X" {
    Properties { ... }
    SubShader {
        Tags { "RenderType"="Opaque" }
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            ...
            ENDCG
        }
    }
}
```

**URP (post):**
```hlsl
Shader "Custom/X" {
    Properties { ... }
    SubShader {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Use UnityCG.cginc replacements: TransformObjectToHClip instead of UnityObjectToClipPos, etc.
            ...
            ENDHLSL
        }
    }
}
```

Standard URP migration patterns:
- `UnityObjectToClipPos(v.vertex)` → `TransformObjectToHClip(v.vertex.xyz)`
- `_WorldSpaceLightPos0` → URP per-pass light data via `_MainLightPosition`
- `UNITY_LIGHTMODEL_AMBIENT` → `unity_AmbientSky` / `unity_AmbientEquator` / `unity_AmbientGround`
- `mul(UNITY_MATRIX_MV, v.vertex)` → `TransformObjectToView(v.vertex.xyz)`

Sonnet should consult the URP Shader Library headers directly when uncertain about a built-in macro's URP equivalent. URP documentation is current and complete for these patterns.

### What if a shader is harder than expected?

Each of the four shaders is ≤ 120 lines. None should be a multi-day rewrite. If Sonnet hits a shader that's genuinely hard to port (e.g., uses a built-in feature URP doesn't have an equivalent for), commit a `WP-4.0.A-blocker.md` note describing the specific issue and stop. Do not silently substitute different visual behavior.

### FPS-gate verification

Run `PerformanceGate30NpcAt60FpsTests` and its variants. URP should improve or hold per-frame cost on 30 NPCs; if it regresses by more than 5%, investigate (likely a renderer feature setting or a shader pass count issue) before considering the packet done.

### Existing scripts touching shaders

Verify these don't break:
- `LightSourceHaloRenderer.cs` (uses `LightHalo.shader`)
- `BeamRenderer.cs` (uses `BeamProjection.shader`)
- `OutlineRenderer.cs` or wherever `Outline.shader` is consumed (there's a SelectionOutline somewhere)
- `RoomAmbientTintApplier.cs` or whoever uses `RoomTint.shader`

If any of these scripts reference `Camera.OnRenderImage`, `Graphics.Blit` to camera target, or other built-in-only APIs, flag in the packet and assess: most likely they don't (they're shader-property setters, not camera-stack code), but check.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| config | `ECSUnity/Packages/manifest.json` | Add `com.unity.render-pipelines.universal: 17.0.x`. |
| asset | `ECSUnity/Assets/Settings/URP-PipelineAsset.asset` | URP pipeline asset. Default settings except for project-specific tweaks documented in a sibling `.md`. |
| asset | `ECSUnity/Assets/Settings/URP-PipelineAsset_Renderer.asset` | Universal Renderer Data. |
| project-settings | `ECSUnity/ProjectSettings/GraphicsSettings.asset` | Updated to reference URP-PipelineAsset. |
| project-settings | `ECSUnity/ProjectSettings/QualitySettings.asset` | Each quality tier points to URP-PipelineAsset. |
| shader | `ECSUnity/Assets/Shaders/BeamProjection.shader` | Rewritten URP-equivalent. Same name; same visual output. |
| shader | `ECSUnity/Assets/Shaders/LightHalo.shader` | Rewritten URP-equivalent. |
| shader | `ECSUnity/Assets/Shaders/Outline.shader` | Rewritten URP-equivalent. |
| shader | `ECSUnity/Assets/Shaders/RoomTint.shader` | Rewritten URP-equivalent. |
| material updates | (project-wide, auto-generated by URP Converter) | Materials re-pointed from built-in shaders to URP equivalents. |
| scene | `ECSUnity/Assets/Scenes/MainScene.unity` | Rule 3 exception — only the auto-converted material reference updates. No manual scene editing. |
| scene | `ECSUnity/Assets/Scenes/PlaytestScene.unity` | Same. |
| scene | `ECSUnity/Assets/_Sandbox/*.unity` | Same for the four sandbox scenes. |
| doc | `ECSUnity/Assets/Settings/URP-PipelineAsset.md` | Documents which non-default settings were applied to the URP asset (e.g., shadow distance, rendering scale) and why. Brief — under 50 lines. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Update MAC-009 to reflect "URP ScriptableRendererFeature pattern (standardized)" instead of "ICameraRenderPass interface". |
| test | `ECSUnity/Assets/Tests/Edit/UrpPipelineConfigTests.cs` (new) | Confirms URP is the active pipeline; URP asset references the right renderer data; quality tiers all point at URP. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `manifest.json` contains `com.unity.render-pipelines.universal` at 17.x. | unit-test |
| AT-02 | `GraphicsSettings.asset` references the URP pipeline asset. | unit-test |
| AT-03 | All quality tiers in `QualitySettings.asset` reference the URP pipeline asset. | unit-test |
| AT-04 | The four custom shaders compile under URP without warnings. | Editor open + check Console |
| AT-05 | `MainScene.unity` opens in Editor, presses Play, renders correctly with no pink materials and no missing-shader errors. | manual visual |
| AT-06 | `PlaytestScene.unity` same. | manual visual |
| AT-07 | Each `_Sandbox/*.unity` scene same. | manual visual |
| AT-08 | Visual parity check: side-by-side comparison (pre-migration screenshot vs post-migration screenshot) of MainScene shows no perceptible difference in lighting, materials, silhouettes, outlines, room tints, beams. | manual visual |
| AT-09 | `PerformanceGate30NpcAt60FpsTests` and all its variants pass; FPS does not regress by more than 5%. | play-mode test |
| AT-10 | All existing Edit-mode tests pass. | edit-mode test |
| AT-11 | All existing Play-mode tests pass (including the lighting, beam, halo, outline, build mode, dev console tests). | play-mode test |
| AT-12 | `dotnet build` warning count = 0 for engine + non-Unity test projects. | build |
| AT-13 | `dotnet test` for `APIFramework.Tests` and other non-Unity test projects all green. | test |
| AT-14 | MAC-009 in `MOD-API-CANDIDATES.md` is updated to reflect URP ScriptableRendererFeature standardization. | review |

---

## Mod API surface

This packet **revises MAC-009**. Pre-revision: "ICameraRenderPass interface (custom)". Post-revision: "URP ScriptableRendererFeature pattern (standardized — Unity's documented extension surface)."

This is a substantively better Mod API answer. Modders adding visual passes (CRT scanline, film grain, outline pass, custom emotion-cue overlay shader) implement Unity's documented `ScriptableRendererFeature` subclass and add it to the URP renderer asset's Feature list. They learn a Unity-standard skill that transfers to any URP project; we don't carry maintenance burden on a bespoke interface.

The pixel-art Renderer Feature shipped in `WP-4.0.A1` will be the *first consumer* of MAC-009 (post-revision) and the canonical example modders study.

URP also opens (but this packet does not commit) future Mod API surfaces that the v0.2 Mod API sub-phase may formalize: Shader Graph templates for modder-authored materials, VFX Graph templates for modder-authored particle effects, Volume Profiles for modder-authored post-processing presets. These get tracked as new MAC entries when relevant packets land.

---

## Followups (not in scope)

- **WP-4.0.A1 — Pixel-art Renderer Feature.** Depends on this packet merging. The first consumer of MAC-009 (post-revision); ships the down-sample + palette-quantize pass as a `ScriptableRendererFeature`.
- **WP-4.0.D — Floor & room visual identity.** Authored against URP foundation.
- **WP-4.0.E — NPC visual state communication.** Authored against URP foundation.
- **WP-4.0.H — Particle vocabulary.** Likely uses VFX Graph (URP/HDRP-only feature now available).
- Shader Graph adoption for new shaders. Future packets pick this up; the four existing shaders stay HLSL-direct because they're already authored that way and rewriting them in Shader Graph adds risk without value.
- Per-quality-tier renderer assets (different feature stacks for low/medium/high). Future polish.
- Forward+ vs Forward vs Deferred renderer choice. Forward is URP's default and adequate for v0.2; revisit if many simultaneous lights become a perf concern.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

This is a Track 2 (Unity) packet, even though it's a pipeline migration rather than a typical sandbox-feature packet. xUnit + Unity test runner are necessary but **not sufficient** — Talon must visually verify parity in Unity Editor across MainScene + PlaytestScene + each sandbox scene before PR is mergeable.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm you are in a dedicated worktree at `.claude/worktrees/sonnet-wp-4.0.a/` on branch `sonnet-wp-4.0.a` based on recent `origin/staging`. If anything is wrong, stop and notify Talon.
1. Run pre-flight build + test confirmation: `dotnet build` and `dotnet test` must be green BEFORE you make any changes. If they're already failing, stop and notify Talon — that's not your packet to fix.
2. Implement the migration sequence in Design notes step-by-step.
3. Run all Unity tests via Unity Test Runner. All must stay green.
4. Run `dotnet test` and `dotnet build`. Must be green.
5. Stage all changes including the self-cleanup deletion (see below).
6. Commit on the worktree's feature branch.
7. Push the branch.
8. Stop. Do **not** open a PR yet. Do **not** merge.
9. Notify Talon with the commit message's final line: `READY FOR VISUAL VERIFICATION — open MainScene + PlaytestScene + each _Sandbox scene; visual parity check vs pre-migration.`

Talon's pipeline (after Sonnet's push):

1. Open the Unity Editor on the feature branch.
2. Open each scene in turn; verify visual parity vs pre-migration baseline.
3. Run the Unity Test Runner; verify FPS gate test passes.
4. If everything passes: open the PR, merge to `staging`.
5. If something regresses: file the failure as PR review comments or a follow-up packet. Do not ask the original Sonnet to iterate ad-hoc.

### Feel-verified-by-playtest acceptance flag

**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** visual parity confirmation across MainScene + PlaytestScene; FPS-gate confirmation under typical PT load; no regression in any visual feature exercised during a PT-NNN session.

The session immediately after this packet merges should specifically open MainScene and run through the standard PT scenarios with attention on visual parity. Bugs surface as `BUG-NNN` entries referencing this packet.

### Cost envelope (1-5-25 Claude army)

Target: **$1.20 upper bound** for this packet (high end; reflects the four-shader rewrite + project-wide material conversion). If costs approach **$1.50**, **escalate to Talon** by stopping work and committing a `WP-4.0.A-blocker.md` note explaining what burned the budget.

Cost-discipline:
- Read each shader file once and rewrite from the in-memory copy. Don't re-read.
- Use Unity's Render Pipeline Converter for material work — don't hand-edit materials.
- The four shader rewrites are formulaic substitutions of URP includes for built-in includes; treat them mechanically, not creatively.
- If a shader rewrite is genuinely hard (a built-in feature without URP equivalent), STOP and file a blocker rather than spending hours improvising.
- Do not investigate Shader Graph "as long as I'm here." Future packets.
- Do not iterate URP renderer asset settings for visual improvement. Parity-only.

### Self-cleanup on merge

The active `docs/c2-infrastructure/work-packets/` directory should contain only **pending** packets.

Before opening the PR (after Talon's visual verification passes):

1. **Check downstream dependents:**
   ```bash
   git grep -l "WP-4.0.A" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. **Expected dependents:** `WP-4.0.A1` (pixel-art Renderer Feature) and likely later `WP-4.0.D`, `WP-4.0.E`, `WP-4.0.H` will reference this packet. If any pending dependents exist: leave the spec file in place. Add a one-line status header:
   ```markdown
   > **STATUS:** SHIPPED to staging YYYY-MM-DD. Retained because pending packets depend on this spec: <list>.
   ```
   Add `Self-cleanup: spec retained, dependents: <list>.` to the commit message.

3. **If no pending dependents** (unlikely given the wave): include `git rm docs/c2-infrastructure/work-packets/WP-4.0.A-pixel-art-post-process-sandbox.md` in the staging set. Add `Self-cleanup: spec file deleted, no pending dependents.` to the commit message.

   Note: the spec filename is currently `WP-4.0.A-pixel-art-post-process-sandbox.md` from the pre-revision draft; the contents have been rewritten as URP migration. The filename is misleading post-rewrite. Either: rename to `WP-4.0.A-urp-migration-foundation.md` as part of the implementation commit (preferred), or accept the misleading filename and note it in the commit. Rename is cleaner.

4. **Do not touch** files under `_completed/` or `_completed-specs/`.
