# WP-3.1.S.3-INT — Wire Inspector Popup into Live Selection + WorldStateDto

> **DO NOT DISPATCH UNTIL WP-3.1.S.3 AND WP-3.1.S.1-INT ARE BOTH MERGED.** This packet binds the validated popup (S.3) to the live selection seam (S.1-INT). Without both, there's nothing to bind.
> **Protocol:** Track 2 integration packet.

**Tier:** Sonnet
**Depends on:** WP-3.1.S.3 (popup sandbox + canvas prefab), WP-3.1.S.1-INT (NPC selection wired, `SelectionManager.SelectedEntityId` exposed)
**Parallel-safe with:** All Track 1 engine packets. WP-3.1.S.0-INT, WP-3.1.S.2-INT.

**Timebox:** 120 minutes
**Budget:** $0.50

---

## Goal

Bind the validated `InspectorPopupCanvas.prefab` to the live engine: when an NPC is selected (via S.1-INT's selection flow), the popup shows that NPC's actual `Name`, `Drives`, `Mood` from `WorldStateDto.Entities`. When selection clears, the popup hides.

After this packet, the click-reveals-data primitive lights up the simulation: click an NPC, see what they're feeling. This is the moment the engine's social-state surface becomes player-visible for the first time.

This packet ships a tiny `WorldStateInspectorBinder.cs` MonoBehaviour that subscribes to `SelectionManager.OnSelectionChanged`, looks up the entity in the latest `WorldStateDto`, and projects the surface tier of UX bible §4.2 (Surface = Name + current action; Tier 2 fields stubbed for now).

It also implements the **three-tier disclosure** from UX bible §4.2 — but only ships Surface tier active. Tiers 2 (Behaviour) and 3 (Internal) are stubbed with placeholder text and a "tiers to come" affordance that disabled-greys the headers. Subsequent packets enable each tier as the engine surface for it stabilises.

This packet ships:

1. `Assets/Scripts/UI/WorldStateInspectorBinder.cs` — subscribes to selection, reads `WorldStateDto`, calls `InspectorPopup.Show(data)` with projected fields.
2. An update to `InspectorPopup.cs` adding three tier sections (Surface / Behaviour / Internal) with one-line headers and a body each. Surface ships filled; Behaviour and Internal ship a placeholder body and a greyed-out section header.
3. An update to `InspectorPopupData.cs`: replace the three flat fields with a `SurfaceTier`, `BehaviourTier`, `InternalTier` shape (each tier = small struct of strings).
4. An update to `InspectorPopupCanvas.prefab` to add three labeled sections in the layout.
5. A `WorldStateInspectorBinder` GameObject in `MainScene.unity`.
6. Integration verification recipe appended to `Assets/_Sandbox/inspector-popup.md`.

---

## Reference files

1. `docs/UNITY-PACKET-PROTOCOL.md`.
2. `docs/c2-infrastructure/work-packets/WP-3.1.S.3-inspector-popup-sandbox.md` — the sandbox.
3. `docs/c2-infrastructure/work-packets/WP-3.1.S.1-INT-selection-into-npc-renderer.md` — the selection seam this packet binds to.
4. `docs/c2-content/ux-ui-bible.md` §4.2 — three-tier disclosure. Surface (Name + current action), Behaviour (drives + mood), Internal (workload + memory snippets). v0.1 ships Surface only.
5. `Warden.Contracts/Telemetry/EntityStateDto.cs` (or equivalent path) — the entity DTO the binder reads from.
6. `ECSUnity/Assets/Scripts/Engine/WorldStateProjectorAdapter.cs` — exposes the latest `WorldStateDto`.
7. `ECSUnity/Assets/Scripts/UI/InspectorPopup.cs` (from S.3) — the file being extended for tiers.

---

## Non-goals

- Do **not** ship Tier 2 (Behaviour) or Tier 3 (Internal) live data. They're placeholder-stubbed; future packets enable each.
- Do **not** add tier-expand animations. Static headers only.
- Do **not** add a separate inspector panel for each tier. One popup, three sections.
- Do **not** rebind to `WorldStateDto` on every frame — once per selection change, plus a re-read each tick if the popup is open. (~60Hz refresh max.)
- Do **not** add live update of the popup as the engine ticks. Snapshot at selection time, refresh on next selection.

   Wait — this is what the popup *should* do for "current action" to stay accurate. Reconsider.

   **Resolution:** the popup re-reads on every engine tick *only while open and only for the selected entity*. Cheap. Implement this; it's a one-line Update method that calls `Show(BuildDataForSelected())`.

- Do **not** add tooltips on tier headers explaining what each tier shows. Future packet.
- Do **not** add a close button. Click-outside dismiss from S.3 is sufficient.

---

## Implementation steps

1. **Verify dependencies merged.** Confirm `Assets/Prefabs/UI/InspectorPopupCanvas.prefab`, `InspectorPopup.cs`, `SelectionManager.SelectedEntityId` all exist.
2. **Edit `InspectorPopupData.cs`** — replace the flat shape with:
   ```csharp
   public struct InspectorPopupData {
       public SurfaceTierData Surface;
       public BehaviourTierData Behaviour;  // placeholder for now
       public InternalTierData Internal;    // placeholder for now
   }
   public struct SurfaceTierData {
       public string Name;
       public string CurrentAction;
   }
   public struct BehaviourTierData {
       public string DrivesSummary;
       public string MoodSummary;
   }
   public struct InternalTierData {
       public string WorkloadSummary;
       public string RecentMemoryFragment;
   }
   ```

3. **Edit `InspectorPopup.cs`** — replace the three `_*Text` fields with three pairs (header + body) per tier. Body for Surface tier is filled from `data.Surface`; body for Behaviour/Internal is filled with the literal string `"(coming soon)"` and the section header is rendered at 50% alpha.

4. **Edit `InspectorPopupCanvas.prefab`** — add three labeled `VerticalLayoutGroup` sections inside the panel. Each section has a header `TextMeshProUGUI` and a body `TextMeshProUGUI`. Wire the new fields.

5. **Create `Assets/Scripts/UI/WorldStateInspectorBinder.cs`:**
   ```csharp
   public sealed class WorldStateInspectorBinder : MonoBehaviour {
       [SerializeField] private EngineHost _engineHost;
       [SerializeField] private SelectionManager _selectionManager;
       [SerializeField] private InspectorPopup _popup;

       private string _trackedEntityId;

       void Awake() => _selectionManager.OnSelectionChanged += OnSelectionChanged;
       void OnDestroy() => _selectionManager.OnSelectionChanged -= OnSelectionChanged;

       void Update() {
           if (string.IsNullOrEmpty(_trackedEntityId)) return;
           // Refresh popup data each frame while popup is open.
           var data = BuildDataForEntity(_trackedEntityId);
           if (data.HasValue) _popup.Show(data.Value);
           else _popup.Hide();
       }

       private void OnSelectionChanged(Selectable s) {
           _trackedEntityId = _selectionManager.SelectedEntityId;
           if (string.IsNullOrEmpty(_trackedEntityId)) _popup.Hide();
       }

       private InspectorPopupData? BuildDataForEntity(string entityId) {
           var ws = _engineHost.WorldState;
           if (ws?.Entities == null) return null;
           foreach (var entity in ws.Entities) {
               if (entity.Id == entityId) {
                   return new InspectorPopupData {
                       Surface = new SurfaceTierData {
                           Name = entity.Name,
                           CurrentAction = entity.IntendedAction?.Kind.ToString() ?? "Idle"
                       },
                       Behaviour = default, // placeholder
                       Internal = default,  // placeholder
                   };
               }
           }
           return null; // entity left the DTO (died, removed)
       }
   }
   ```

6. **Drop a `WorldStateInspectorBinder` GameObject** into `MainScene.unity`. Wire `_engineHost`, `_selectionManager`, `_popup`.

7. **Run `dotnet test` and `dotnet build`.** Green.

8. **Append the integration verification recipe** to `Assets/_Sandbox/inspector-popup.md`.

---

## Test recipe addendum (append to `Assets/_Sandbox/inspector-popup.md`)

```markdown

## Integration verification (after WP-3.1.S.3-INT)

This step confirms the popup binds to live NPC state.

1. Open Assets/Scenes/MainScene.unity.
2. Press Play. Engine ticks; NPCs render.
3. Click an NPC dot. Expect: popup appears with three sections:
   - **Surface** — NPC's name, current action (e.g. "Greg | Eat").
   - **Behaviour** — header at 50% alpha, body says "(coming soon)".
   - **Internal** — header at 50% alpha, body says "(coming soon)".
4. Wait a few seconds. As the NPC's IntendedAction changes (Eat → Move,
   etc.), the Surface tier's "Current Action" updates live in the popup.
5. Click another NPC. Popup repaints with new NPC's data.
6. Click empty floor. Popup disappears.
7. Click a corpse (if any are present). Expect: popup shows the corpse's
   surface data with a stable "Deceased" or similar action label —
   confirms popup handles entities outside Alive state gracefully.

## If integration fails
- Popup appears but has no data → WorldStateInspectorBinder isn't
  finding the entity in WorldStateDto. Check entity ID matching.
- Popup shows stale data → Update() loop isn't firing or the binder
  isn't refreshing. Check the _trackedEntityId logic.
- Popup throws exception when an NPC dies → BuildDataForEntity isn't
  handling the entity-left-DTO case. Should call _popup.Hide().
- Tier 2 / Tier 3 sections show data instead of placeholder → struct
  defaults aren't empty.
```

---

## Acceptance criteria

1. `Assets/Scripts/UI/WorldStateInspectorBinder.cs` exists and follows the implementation sketch above.
2. `InspectorPopupData.cs` has the new tiered shape.
3. `InspectorPopup.cs` renders three tiers; Surface is live, Behaviour and Internal show placeholder text with greyed-out headers.
4. `InspectorPopupCanvas.prefab` has the three-section layout.
5. `MainScene.unity` contains a `WorldStateInspectorBinder` GameObject with all three references wired.
6. Selecting an NPC shows live-updating Surface data; deselecting hides the popup.
7. `dotnet test` passes; `dotnet build` is clean.
8. The 30-NPCs-at-60-FPS perf gate still holds.
9. `Assets/_Sandbox/inspector-popup.md` has the new "Integration verification" section.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

Track 2 integration. Talon's recipe pass is the gate.

Sonnet pipeline: implement → `dotnet test` + `dotnet build` green → stage with cleanup → commit → push → stop. Final commit message line: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/inspector-popup.md (Integration verification section)`.

Talon: open MainScene, press Play, click NPCs, run recipe. Pass = merge. Fail = PR comments or follow-up packet.

### Cost envelope

Target: **$0.50** (120-minute timebox). The DTO field-mapping is mechanical; the tier-stubbing is mechanical. Cost should land cleanly at target.

If the `EntityStateDto` shape doesn't include `Name` or `IntendedAction.Kind` fields directly, **stop and write a `WP-3.1.S.3-INT-blocker.md` note** — extending the DTO is a separate engine-side packet, not in scope here.

### Self-cleanup on merge

Before opening the PR:

1. **Check downstream dependents:**
   ```bash
   git grep -l "WP-3.1.S.3-INT" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   git grep -l "WP-3.1.S.3" docs/c2-infrastructure/work-packets/ | grep -v "WP-3.1.S.3-INT" | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   git grep -l "WP-3.1.S.1-INT" docs/c2-infrastructure/work-packets/ | grep -v "WP-3.1.S.3-INT" | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```

2. **For this packet (S.3-INT):** likely no downstream. `git rm`.

3. **For the sandbox (S.3):** if no other pending dependents, `git rm` the S.3 spec.

4. **For S.1-INT:** if this was the only pending packet referencing it (likely yes), `git rm` the S.1-INT spec.

5. Add `Self-cleanup:` lines for each deletion.

6. UI scripts, popup canvas prefab, and the binder live indefinitely.

---

*WP-3.1.S.3-INT lights up the click-reveals-data loop on real NPCs. After this, the engine's social-state surface is finally player-visible. Tier 2 (Behaviour — drives, mood) and Tier 3 (Internal — workload, memory) light up in subsequent packets as the engine fields stabilise.*
