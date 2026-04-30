# WP-3.1.B — Silhouette Renderer + Layered Animator Skeleton

> **DO NOT DISPATCH UNTIL WP-3.1.A IS MERGED.**
> This packet replaces the dot-render established in 3.1.A with per-NPC silhouettes. The Unity project, `EngineHost`, `WorldStateProjectorAdapter`, and the `NpcDotRenderer` it supersedes must already exist on `main`. The 30-NPCs-at-60-FPS performance gate from 3.1.A also remains binding — silhouette rendering must not violate it.

**Tier:** Sonnet
**Depends on:** WP-3.1.A (Unity scaffold), cast-bible (silhouette catalog)
**Parallel-safe with:** WP-3.1.C (lighting — different render pass), WP-3.1.D (build mode — different system surface), WP-3.1.F (JSONL stream — telemetry, not render)
**Timebox:** 150 minutes
**Budget:** $0.60

---

## Goal

Donna stops being a dark-plum dot. Greg stops being a pale-yellow-green dot. They become readable silhouettes — short stocky body in a long skirt for Donna; tall thin frame in a band tee and lanyard for Greg; thicker round-shouldered shape with the perpetual coffee mug for Frank. The cast bible's silhouette catalog (height, build, hair, headwear, distinctive item, dominant color) becomes visual reality.

The render is still flat — pixel-art-from-3D shader work is deferred to 3.1.C-adjacent. This packet ships layered 2D sprite assembly: a base body sprite + hair sprite + headwear sprite + distinctive-item sprite, each per-NPC, composed at runtime from the cast generator's spawned silhouette descriptor. An animator skeleton with the minimum viable state machine — Idle, Walk, Sit, Talk, Panic, Sleep — drives sprite swaps for posture and motion.

The packet also stubs the **chibi-emotion overlay slot** (per UX bible §3.8): a dedicated child renderer per NPC where 3.1.E (player UI) will later inject the chibi anger lines, sweat drops, sleep-Z's, etc. This packet doesn't ship the emotion icons — it ships the slot, the parent transform, the visibility toggle. Future content packets author the icon sprites.

After this packet, the office reads. The player can tell Donna from Frank from Greg without clicking. Conversation visualization (text streams) and lighting beams remain deferred (3.1.E and 3.1.C respectively); silhouettes alone are the win.

---

## Reference files

- `docs/c2-infrastructure/work-packets/WP-3.1.A-unity-scaffold-and-baseline-render.md` — what's already on disk: `EngineHost`, `WorldStateProjectorAdapter`, `NpcDotRenderer`, `RenderColorPalette`. The dot renderer is replaced (or wrapped) by this packet's silhouette renderer.
- `docs/c2-content/cast-bible.md` — **read fully.** The silhouette catalog: height (short/medium/tall), build (slim/average/stocky), hair (short/medium/long + color), headwear (none/cap/glasses/headphones), distinctive item (mug/lanyard/clipboard/etc.), dominant color (per-archetype). Donna, Greg, Frank, the Newbie, the Old Hand, the Cynic, the Vent, the Climber, the Founder's Nephew, the Crush, the Recovering, the Affair, the Hermit — verify all 13 archetypes (or however many are in the bible) are covered.
- `docs/c2-content/aesthetic-bible.md` — §"Visual rendering style" — pixel-art-aesthetic, low-poly underlying, beige-dominant palette, readable-at-distance silhouettes (Hotline Miami reference). This packet honors the readable-at-distance commitment; the pixel-art-from-3D shader is the *next* step.
- `docs/c2-content/ux-ui-bible.md` §3.8 — the chibi-emotion overlay vocabulary the slot serves. Read so the slot's hooks match the future packet that fills them.
- `APIFramework/Components/CastSpawnComponents.cs` — `NpcArchetypeComponent`, `SilhouetteDescriptorComponent` (or whatever the cast generator attaches; verify name). The renderer reads from this.
- `APIFramework/Systems/CastGenerator/*` (whichever directory) — read for what fields the silhouette descriptor carries.
- `Warden.Telemetry/Projectors/*` — `WorldStateDto.npcs[].silhouette` (or equivalent) — what the renderer consumes per frame.
- `ECSUnity/Assets/Scripts/Render/NpcDotRenderer.cs` (from 3.1.A) — replaced by `NpcSilhouetteRenderer`; preserved as a fallback path if the silhouette assets are missing.

---

## Non-goals

- Do **not** ship the chibi-emotion overlay icons themselves. The *slot* is in scope; the icons (anger lines, hearts, sleep-Z's, stink lines) ship in 3.1.E or a content packet.
- Do **not** ship per-archetype voice profiles or audio. UX bible §3.7 audio is its own packet.
- Do **not** implement the pixel-art-from-3D rendering pipeline. That's a render-pipeline packet. Silhouettes here are flat 2D sprite composition.
- Do **not** modify the cast generator or `SilhouetteDescriptorComponent`. The renderer reads what's already there.
- Do **not** ship animation transitions for special states like "choking" or "fainting" — those will be cued by 3.1.E listening to engine narrative events. The animator's slots for these states exist (Panic), but the activation hooks are stubbed for future work.
- Do **not** modify the 30-NPCs-at-60-FPS performance gate from 3.1.A. The gate must continue to pass with silhouettes.
- Do **not** ship facial detail beyond what the aesthetic bible permits ("Faces are not detailed enough to read expressions"). Emotion reads through silhouette + chibi overlay, not face.
- Do **not** add a sprite-baking editor tool. Sprites are authored by hand or by an external pipeline; the renderer composes at runtime.
- Do **not** retry, recurse, or "self-heal." Fail closed per SRD §4.1.

---

## Design notes

### Silhouette composition

Each NPC's silhouette is composed from up to four layered 2D sprites:

1. **Base body** — height × build combination (9 combinations: short/medium/tall × slim/average/stocky). Sprite name: `body_<height>_<build>.png`.
2. **Hair** — length × color combination. Sprite name: `hair_<length>_<color>.png`.
3. **Headwear** — none / cap / glasses / headphones / hat / hair-tie. Sprite name: `headwear_<kind>.png`. Empty when "none."
4. **Distinctive item** — mug / lanyard / clipboard / phone / cigarette / pager / tie / etc. Sprite name: `item_<kind>.png`.

Per-NPC color overlay tints the base body to the archetype's dominant color (per cast bible). Tint is multiplicative on the base layer, leaving hair / headwear / item sprites at their authored colors.

Sprite assembly: the renderer creates a single `SpriteRenderer` parent per NPC with four child `SpriteRenderer` children. Layering is z-ordered (body back, hair next, headwear above hair, item topmost). Anchor points pre-defined so head/hand placement is consistent across body sizes.

### `NpcSilhouetteRenderer`

Replaces `NpcDotRenderer` from 3.1.A:

```csharp
public sealed class NpcSilhouetteRenderer : MonoBehaviour
{
    [SerializeField] EngineHost _host;
    [SerializeField] SilhouetteAssetCatalog _catalog;     // ScriptableObject mapping descriptor → sprite

    Dictionary<Guid, NpcSilhouetteInstance> _instances = new();

    void LateUpdate()
    {
        var dto = _host.Snapshot();
        foreach (var npc in dto.npcs)
        {
            if (!_instances.TryGetValue(npc.entityId, out var inst))
            {
                inst = SpawnSilhouette(npc);
                _instances[npc.entityId] = inst;
            }
            inst.UpdatePosition(npc.position, npc.facing);
            inst.UpdateAnimationState(npc.intent, npc.lifeState, npc.mood);
        }
        // remove instances whose entity is no longer in the snapshot
        // (deceased NPCs stay rendered until 3.0.2's CorpseComponent path triggers a "dead" pose)
    }
}
```

Determinism is preserved (rendering is read-only of engine state).

### Layered animator skeleton

A Unity `Animator` per NPC with the following states:

| State | Triggered by | Visual |
|:---|:---|:---|
| Idle | `IntendedAction.Kind == Idle` | Subtle breathing sway; occasional posture shift; eye-line drift |
| Walk | `IntendedAction.Kind == Approach` AND velocity > 0 | Walking sprite cycle; speed-scaled by `MovementSpeedFactor` |
| Sit | At a chair entity, `Activity == AtDesk` | Sitting pose; subtle keyboard-typing motion if Working |
| Talk | `IntendedAction.Kind == Dialog` | Hand gesture cycle; mouth-suggestion (no detail per aesthetic bible) |
| Panic | `IsChokingTag` OR (`MoodComponent.PanicLevel >= 0.5`) | Frozen-and-clutching pose; facing locked forward |
| Sleep | `LifeStateComponent.State == Incapacitated` (fainting) OR scheduled sleep | Slumped or supine; sleep-Z chibi-overlay slot active |
| Dead | `LifeStateComponent.State == Deceased` | Static slumped pose; no animation; remains until corpse removed |

Transitions are state-machine-driven by reading `WorldStateDto.npcs[].intent` and friend fields.

The state list is intentionally minimal at v0.1. Walking-while-distressed, eating, drinking, defecating-in-cubicle, and other specialised states are deferred to follow-up packets.

### Chibi-emotion overlay slot

Each `NpcSilhouetteInstance` has a child transform `EmotionOverlay` that's empty at v0.1 but reachable by name. WP-3.1.E (player UI) populates it with chibi sprites (anger lines, sweat drops, hearts, etc.) keyed off `WorldStateDto.npcs[].mood` and drive vector.

This packet ships:
- The `EmotionOverlay` child transform, attached to head-anchor.
- A `ChibiEmotionSlot.cs` component on the overlay with a `Show(IconKind kind)` and `Hide()` API.
- `IconKind` enum stub — `None, Anger, Sweat, SleepZ, Heart, Sparkle, QuestionMark, Exclamation, Stink` — but no actual sprites loaded.
- Tests confirming the slot exists and the API doesn't throw on a no-sprite-loaded call.

### Performance gate (carry-forward from 3.1.A)

The 30-NPCs-at-60-FPS test from 3.1.A is preserved and re-run. Silhouettes have higher per-NPC cost than dots (4 SpriteRenderers vs 1 quad). The packet must:

- Stay under the same FPS gate (min ≥ 55, mean ≥ 58, p99 ≥ 50).
- Use sprite-batching: all silhouettes share materials (per-layer; one shared material per layer) so a single batched draw call per layer covers all 30 NPCs.
- Avoid per-frame allocations. Sprite swaps reuse the existing `SpriteRenderer.sprite` field; do not create new `SpriteRenderer` GameObjects per frame.

If the gate fails after silhouette swap, the packet escalates as blocked. Diagnosis order: (1) draw call count > expected (sprite-batching not engaging), (2) per-frame allocations from animator state changes, (3) sprite lookup hits a non-cached path.

### Sprite asset placeholders

V0.1 ships **placeholder pixel-art sprites** — not final art, but enough for tests and visual sanity. Sprites are 32×64 pixel-perfect billboards, hand-drawn or generated via simple shape primitives. Per-archetype anchor calibration documented in `silhouette-catalog.json`:

```jsonc
{
  "schemaVersion": "0.1.0",
  "archetypes": [
    {
      "archetypeId": "the-old-hand",     // Donna's archetype
      "height": "short",
      "build": "stocky",
      "hairLength": "medium",
      "hairColor": "graying-brown",
      "headwear": "none",
      "item": "purse-on-shoulder",
      "dominantColor": "#5C2A4B"          // dark plum
    },
    // ... all archetypes from cast bible
  ]
}
```

Sprite art replacement is a future content/art-pipeline packet; the catalog and the renderer are stable.

### Tests

- `NpcSilhouetteRendererSpawnTests.cs` (Play-mode) — boot scene; assert one silhouette instance per NPC; instance has 4 child sprites (body, hair, headwear, item).
- `NpcSilhouetteRendererTrackTests.cs` (Play-mode) — over 100 frames, silhouette transform tracks NPC `PositionComponent`; max delta ≤ 0.5 world-units.
- `NpcSilhouetteRendererTintTests.cs` (Play-mode) — Donna's body sprite has tint matching `dominantColor: #5C2A4B`.
- `AnimatorStateTransitionTests.cs` (Play-mode) — set `IntendedAction.Kind = Approach` on an NPC; animator state advances to Walk within 1 tick.
- `AnimatorPanicStateTests.cs` (Play-mode) — set `IsChokingTag` on an NPC; animator advances to Panic; facing freezes.
- `AnimatorSleepStateTests.cs` (Play-mode) — set `LifeState = Incapacitated`; animator advances to Sleep; sleep-Z chibi slot is `Show(SleepZ)` (slot exists, even if sprite isn't authored).
- `AnimatorDeadStateTests.cs` (Play-mode) — set `LifeState = Deceased`; animator advances to Dead; pose static.
- `ChibiEmotionSlotApiTests.cs` (Edit-mode) — `Show(Anger)` and `Hide()` don't throw with no sprite loaded.
- `SilhouetteAssetCatalogJsonTests.cs` (Edit-mode) — `silhouette-catalog.json` loads; all archetypes from cast bible present; all `dominantColor` values are valid hex.
- `PerformanceGate30NpcAt60FpsWithSilhouettesTests.cs` (Play-mode) — same 30-NPCs scenario, now with silhouettes. Same FPS thresholds. **No weakening.**
- `SpriteBatchingTests.cs` (Play-mode) — at 30 NPCs, draw call count is bounded (≤ ~10 per frame for silhouettes; verify via `UnityEngine.Profiling.Profiler` or render-debug capture).

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSUnity/Assets/Scripts/Render/NpcSilhouetteRenderer.cs` | Replaces `NpcDotRenderer`. |
| code | `ECSUnity/Assets/Scripts/Render/NpcSilhouetteInstance.cs` | Per-NPC silhouette state. |
| code | `ECSUnity/Assets/Scripts/Render/SilhouetteAssetCatalog.cs` | ScriptableObject mapping descriptor → sprite. |
| code | `ECSUnity/Assets/Scripts/Render/ChibiEmotionSlot.cs` | Overlay slot for 3.1.E. |
| code | `ECSUnity/Assets/Scripts/Render/IconKind.cs` | Enum stub for chibi vocabulary. |
| code | `ECSUnity/Assets/Scripts/Animation/NpcAnimatorController.cs` | Animator state machine wiring. |
| asset | `ECSUnity/Assets/Animations/NpcAnimator.controller` | Unity Animator Controller asset. |
| asset | `ECSUnity/Assets/Sprites/Silhouettes/*.png` | Placeholder body/hair/headwear/item sprites. |
| asset | `ECSUnity/Assets/Settings/SilhouetteAssetCatalog.asset` | Catalog ScriptableObject populated. |
| data | `docs/c2-content/silhouette-catalog.json` | Archetype-to-descriptor mapping. |
| test | `ECSUnity/Assets/Tests/Play/NpcSilhouetteRendererSpawnTests.cs` | Spawn semantics. |
| test | `ECSUnity/Assets/Tests/Play/NpcSilhouetteRendererTrackTests.cs` | Position tracking. |
| test | `ECSUnity/Assets/Tests/Play/NpcSilhouetteRendererTintTests.cs` | Color tinting. |
| test | `ECSUnity/Assets/Tests/Play/AnimatorStateTransitionTests.cs` | State machine. |
| test | `ECSUnity/Assets/Tests/Play/AnimatorPanicStateTests.cs` | Panic state. |
| test | `ECSUnity/Assets/Tests/Play/AnimatorSleepStateTests.cs` | Sleep state. |
| test | `ECSUnity/Assets/Tests/Play/AnimatorDeadStateTests.cs` | Dead state. |
| test | `ECSUnity/Assets/Tests/Edit/ChibiEmotionSlotApiTests.cs` | Slot API. |
| test | `ECSUnity/Assets/Tests/Edit/SilhouetteAssetCatalogJsonTests.cs` | Catalog validation. |
| test | `ECSUnity/Assets/Tests/Play/PerformanceGate30NpcAt60FpsWithSilhouettesTests.cs` | **FPS preserved.** |
| test | `ECSUnity/Assets/Tests/Play/SpriteBatchingTests.cs` | Draw call bounded. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.1.B.md` | Completion note. SimConfig defaults. Sprite art status (placeholder vs final). FPS measurements with silhouettes. Draw call count at 30 NPCs. Whether the chibi slot was tested with a sample sprite or only the no-sprite path. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | One `NpcSilhouetteInstance` per NPC; instance has body+hair+headwear+item children. | play-mode test |
| AT-02 | Silhouette transform tracks NPC `PositionComponent` across 100 frames; max delta ≤ 0.5 world-units. | play-mode test |
| AT-03 | Donna's body sprite tint = `#5C2A4B`; Greg's = pale-yellow-green; Frank's = brown. (Sample 3 archetypes; full coverage by catalog test.) | play-mode test |
| AT-04 | Animator: `IntendedAction.Kind = Approach` → state Walk within 1 tick. | play-mode test |
| AT-05 | Animator: `IsChokingTag` → state Panic; facing locked forward. | play-mode test |
| AT-06 | Animator: `LifeState = Incapacitated` → state Sleep; chibi slot `Show(SleepZ)` invoked. | play-mode test |
| AT-07 | Animator: `LifeState = Deceased` → state Dead; pose static. | play-mode test |
| AT-08 | Chibi slot API: `Show(IconKind.Anger)` and `Hide()` execute without exception, even when no sprite is loaded. | edit-mode test |
| AT-09 | `silhouette-catalog.json` loads; all cast-bible archetypes present; `dominantColor` values valid hex. | edit-mode test |
| AT-10 | **Performance gate.** 30 NPCs with silhouettes: min FPS ≥ 55, mean ≥ 58, p99 ≥ 50. | play-mode test |
| AT-11 | Sprite batching: 30 NPCs render in ≤ ~10 draw calls (per-layer batched). | play-mode test |
| AT-12 | All Phase 0/1/2/3.0.x and 3.1.A tests stay green. | regression |
| AT-13 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-14 | `dotnet test ECSSimulation.sln` — all green. | unit-test |
| AT-15 | Unity Test Runner: all 3.1.A and 3.1.B tests pass. | unity test runner |

---

## Followups (not in scope)

- **Pixel-art-from-3D rendering pipeline.** Per aesthetic-bible. Underlying low-poly 3D, pixel-art shader at output. Future packet, likely paired with 3.1.C lighting (so lighting and shader land together).
- **Chibi sprite content authoring.** Anger lines, sweat drops, hearts, sleep-Zs, stink lines, etc. Art-pipeline work. Slot is ready; content fills it.
- **Per-archetype animation variants.** The Hermit's walk is different from the Climber's; the Crush's idle has more fidgeting. Future polish.
- **Walking-while-distressed states.** A walking NPC under high stress walks faster, more rigidly. Animator state subdivision; future.
- **Eating, drinking, defecating-in-cubicle, sleeping-at-desk states.** Specialised animations for the physiology surface. Future.
- **Per-NPC voice profile generation.** Sims-gibberish per archetype. Audio-pipeline packet.
- **Silhouette LOD.** At zoomed-out camera distances, drop hair/headwear/item layers. Future perf optimisation if needed.
- **Conversation visualization (text streams).** UX bible §3.8. Lives in 3.1.E or its own packet.
- **Hot-swap silhouette art.** Editor-time hot-reload of sprite assets. Tooling polish; future.
