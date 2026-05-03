# Sandbox: NPC Visual State Communication
## Test Recipe — WP-4.0.E
**Estimated time:** 10–15 minutes

---

## Prerequisites

1. Open Unity with the ECSUnity project.
2. Run **Assets → ECS → Generate Chibi Cue Placeholder Sprites** (once per machine) to produce the colored-square placeholder sprites in `Sprites/Silhouettes/`.
3. Ensure the `PlaytestScene` or a copy is open; or use the dedicated sandbox scene setup described below.

---

## Step 0: Scene Setup (first run only)

If you want a dedicated sandbox scene rather than PlaytestScene:

1. Create a new scene (`File → New Scene`) and save as `Assets/_Sandbox/npc-visual-state.unity`.
2. Add an `EngineHost` GameObject and wire it to `office-starter.json`.
3. Add `NpcSilhouetteRenderer`, `ChibiEmotionPopulator`, and `SilhouetteAnimator` GameObjects.
4. Assign the `NpcVisualStateCatalogLoader` ScriptableObject to both `ChibiEmotionPopulator` and `SilhouetteAnimator` Inspector fields.
   - Create the loader asset via: **Assets → Create → ECS → NpcVisualStateCatalogLoader**.
   - Assign the `visual-state-catalog.json` TextAsset to its `_jsonAsset` field.
5. Play mode — the office spawns with NPCs.

---

## Step 1: Verify All 8 States Are Legible

For each state, use the DevConsole (`~`) to force an NPC into the state.

| State | DevConsole command | What to look for (at 15m altitude) |
|:---|:---|:---|
| **Idle** | `move <npc> <tile-with-nothing-to-do>` | Subtle weight shift; standing pose; not moving |
| **Walk** | `move <npc> <distant-tile>` | Forward lean + arm swing; clearly directional |
| **Eating** | `scenario choke <npc>` then cancel; or wait for lunch | Hand-to-mouth; head tilt |
| **Drinking** | Wait for drink urgency; or `set-drive <npc> DrinkUrgency 0.95` | Cup silhouette; head tilt |
| **Working** | `set-time 9:00` | Seated at workstation; typing arm motion; head bob |
| **Crying** | `seed-bereavement <npc>` | Head down; shoulder shake; sweat-drop or tear cue |
| **CoughingFit** | `scenario choke <npc>` | Body shake; hand to mouth; bent-over |
| **Heimlich** | `scenario choke <npc>` then wait for rescuer | Two NPCs; rescuer behind patient |

**Pass:** Each state reads distinctly without needing to click the NPC. Two-second-glance answerable.

---

## Step 2: Verify All 9 Chibi Cues Are Visible

At **default 15m altitude** (camera starting position), verify each cue:

| Cue | Trigger | Pass? |
|:---|:---|:---|
| Anger lines | `set-drive <npc> Irritation 80` (when social drives project) | Tight radiating marks around head |
| Sweat drops | `set-drive <npc> EatUrgency 0.9` | Arcing drops from forehead |
| Sleep-Z's | `set-drive <npc> Energy 10` | Floating Z; check at 8m, 15m, and 25m |
| Red-face flush | Future: `set-mood <npc> AngerSpike` | Red cheek/face overlay |
| Green-face nausea | Future: `set-mood <npc> Disgust 0.8` | Green face tinge |
| Heart | Future: Attraction >= 80 | Pink floating heart |
| Sparkles | Future: Joy >= 0.9 | Star burst |
| Exclamation | Surprise spike event | White ! ; check at 8m, 15m, and 25m |
| Question mark | Amazement tier | Floating ? |

**Altitude camera tests (use the Camera Controller zoom):**
- 8m: all active cues visible
- 15m: all active cues visible (default target)
- 25m: Sleep-Z, Red-face flush, Green-face nausea, Exclamation must remain visible

**Pass:** No cues disappear before their catalog fade altitude. Sweat + anger may fade above 25m (acceptable per spec).

---

## Step 3: Verify State Transitions Are Smooth

1. Force an NPC to Walk: `move <npc> <distant-tile>`
2. While walking, trigger a cough: `scenario choke <npc>`

**Expected:** The transition from Walking to CoughingFit uses ~3 intermediate frames (360ms crossfade), not a single-frame snap. The NPC's arm position smoothly transitions rather than snapping from walk-arm to cough-clutch.

3. Force Eating → CoughingFit: let an NPC eat, then `scenario choke <npc>`.

**Expected:** 240ms smooth transition (2 intermediate frames).

**Pass:** Significant transitions read as smooth. Non-catalogued transitions (e.g. Idle → Talk) may snap; that is correct behavior.

---

## Step 4: A/B Compare Under Pixel-Art Shader

1. In the Renderer Features Inspector (URP Pipeline Asset Renderer), toggle the **PixelArtRendererFeature** on and off.
2. At 15m altitude, repeat the visual checks for Steps 1 and 2.

**Pass:** All 8 states and all currently-active cues remain legible with the pixel-art shader on.

---

## Step 5: Performance Sanity

1. With 15+ NPCs active, run for 60 seconds.
2. Check `Window → Analysis → Profiler` or the in-game FPS counter.

**Pass:** FPS stays ≥ 55 min, ≥ 58 mean with SilhouetteAnimator and ChibiEmotionPopulator both active.

---

## Known Limitations (WP-4.0.E scope)

- **Sprites are placeholders.** Colored squares are the placeholder for final hand-drawn chibi cue art (WP-4.1.2).
- **Social drives not yet projected.** Anger, RedFaceFlush, GreenFaceNausea, Heart, and Sparkle cues are stubs — they won't fire until WorldStateDto v0.5+ projects social drives. Sweat and SleepZ are live.
- **Production scene integration is WP-4.0.E-INT.** This sandbox-only pass does not modify PlaytestScene or MainScene — that's Talon's hands in the -INT packet.

---

## Acceptance Sign-Off (Talon)

- [ ] AT-05: All 8 states read distinctly at 15m altitude without clicking.
- [ ] AT-06: All visible cues (Sweat, SleepZ active; others when triggered) fade per spec altitude table.
- [ ] AT-07: Significant state transitions (Walk→CoughingFit, Eating→CoughingFit) read smoothly.
- [ ] AT-09: Two-second-glance answerable — I can identify what each NPC is doing at a glance.

Record any visual issues as `WP-4.0.E-visual-feedback.md` for the -INT pass or WP-4.1.x art pipeline.
