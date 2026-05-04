# Future Features Ledger

Features Talon and Opus have committed to in spirit but deferred to a future implementation wave. Each entry records what the feature is, the gate (what needs to be true before it's worked on), and a likely home wave (rough Phase 4.x or beyond bucket).

Distinct from `docs/known-bugs.md` (defects in shipped systems) and `docs/c2-infrastructure/work-packets/` (specs in flight or pending dispatch). This is the wishlist — the place where deferred-but-committed ideas live so they don't get forgotten.

When a deferred feature is ready to ship, it graduates here → into a `WP-NN.x-<slug>.md` packet in `work-packets/`. When the packet merges, the entry here gets a "Shipped: WP-NN.x" note and stays for audit; do not delete shipped entries.

---

## Player verbs

### FF-001: Multi-select for NPC handling

- **What:** Click-and-box to select multiple NPCs; click-and-hold to lift the group; drop-as-group at a target tile. Same gesture family as single-NPC pick-up (UX bible §2.6) extended to many.
- **Gate:** Single-NPC pick-up-and-drop intervention verb is solid in playtests. (PT-NNN sessions confirm the verb feels right; no critical bugs in single-pickup pathing or drop targeting.)
- **Home wave:** Phase 4.3.x player verb expansion.
- **Source:** UX/UI bible v0.2 §2.2.

### FF-002: Role + badge intervention verb

- **What:** Speculative refinement of §2.6's drop-an-NPC verb. Player clicks a *task* (a spill, a workstation that needs filling); a list of badged-eligible NPCs surfaces; player clicks the badge to assign. Alternative pathway to physical placement.
- **Gate:** Drop-an-NPC verb has been exercised in real play and Talon has watched players try it. If players consistently struggle to find the right NPC for a task (e.g., "who's the janitor again?"), the badge verb earns its priority. If players prefer the gestural approach, this stays deferred.
- **Home wave:** Phase 4.3.x. Decide post-playtest evidence.
- **Source:** UX/UI bible v0.2 §2.6.

### FF-003: Throw-with-momentum gesture (player-facing)

- **What:** Release-flick imparts velocity to a held prop above a configurable threshold. Couples to the rudimentary-physics layer (`MassComponent`, `BreakableComponent`, `ThrownVelocityComponent`) shipped in WP-3.2.2.
- **Gate:** Build-mode-v2 lands and resolves BUG-001 alongside related drag/displacement issues.
- **Home wave:** Phase 4.0.x build mode v2 (sibling to BUG-001's home).
- **Source:** UX/UI bible v0.2 §2.5.

---

## HUD surfaces

### FF-004: Mini-map

- **What:** A small always-visible mini-map in the HUD showing the office floor plan with NPC positions. Substrate already exists: `Warden.Telemetry/AsciiMap/AsciiMapProjector` (Unicode box-drawing floor plan from WP-3.0.W). The mini-map renders a subset of that projection.
- **Gate:** Walls and items render reliably in the play scene (currently we don't have items on screen or walls per Talon's 2026-05-01 framing). Once the playable scene is visually complete enough to be worth mapping, this earns priority.
- **Home wave:** Phase 4.0.x or 4.1.x — depends on when scene visual completeness lands.
- **Source:** UX/UI bible v0.2 §3.6.

### FF-005: Notification carrier surface(s)

- **What:** Concrete in-world carriers for external triggers — phone ringing, fax tray filling, CRT email blink, in-tray for printed memos, distributed per-NPC phones, bulletin board, intercom, or some combination. v0.2 commits to the *constraints* (sparse, diegetic, ignore-able, no popups) but not the carrier set.
- **Gate:** Real humans have played a real prototype and revealed what they enjoy doing with their time (managing people, watching, building, organizing, or some mix). Without that evidence, any carrier choice is a guess.
- **Home wave:** Phase 4.2.x or later. Calibrates against playtest data.
- **Source:** UX/UI bible v0.2 §3.2.2.

### FF-006: Notification volume curve

- **What:** Calibrated rate of external triggers per game-day (zero / one / two / more). v0.2 ships a first-pass safety rail (zero–two per day) but does not commit it.
- **Gate:** Same as FF-005 — playtest reveals what cadence of disruption players actually tolerate.
- **Home wave:** Phase 4.2.x or later.
- **Source:** UX/UI bible v0.2 §3.2.2.

---

## Build / economy

### FF-007: Build inventory unlock economy

- **What:** Full design of the progressive-unlock economy committed in UX/UI bible v0.2 §3.5. Principle is settled: players start with cube walls, doors, desks, chairs, monitors/computers, printers; everything else unlocks through three pathways — **employee perks** (hires bring items), **supply closet** (purchasable in-world inventory the office maintains), **office-supply store** (external purchase channel for bigger / specialty items). What needs designing: perk-to-item mapping (which hire unlocks what), supply-closet vs office-supply-store catalog split, pricing curves, restock mechanics for consumables, whether some items are perk-AND-purchase gated. Talon's framing: "this needs more thoughtful answer in the future."
- **Gate:** Money / economy mechanic (FF-008) ships, providing the spending substrate.
- **Home wave:** Phase 4.x economy packet (TBD position in 4.x); couples to FF-008.
- **Source:** UX/UI bible v0.2 §3.5.

### FF-008: Money / economy mechanic

- **What:** The core economic loop. Money earned through *multiple goal types* (client work, milestones, bonuses, opportunistic payouts — not "the company gives you a quota"). Money spent on expansion (rooms, items, hires) and possibly on consumables. The principle: no canonical optimal play because no canonical optimal income source.
- **Gate:** Core architecture is developed enough that there *is a game* to apply economy to. Playtest reveals which income sources feel rewarding.
- **Home wave:** Phase 4.2.x or later. Couples to FF-005 / FF-006 / FF-007 / FF-009.
- **Source:** UX/UI bible v0.2 §3.2.1.

### FF-009: Systemic disruption events

- **What:** Things-thrown-at-the-office over time. ISP switches, roof maintenance, port-a-potty week, HVAC breakdowns, surprise visits, weather. The office reorganizes around them; the player decides how.
- **Gate:** Core gameplay loop is stable enough that disruption can be calibrated against it.
- **Home wave:** Phase 4.2.x emergent gameplay deepening (sibling to plague week / fire / PIP arc).
- **Source:** UX/UI bible v0.2 §3.2.1.

---

## Pause / time

### FF-010: Auto-pause-on-event triggers

- **What:** Specific event vocabulary that auto-pauses the simulation when triggered (death, mass-bereavement, fire, lockout, affair detection, PIP threshold, etc.). The *infrastructure* (event-pause hook on a stable pause menu) is bible-committed in v0.2 §2.4 / §3.9; the trigger vocabulary defers.
- **Gate:** There is enough game to reveal which events warrant the interrupt. Playtest evidence on what players want to be alerted to.
- **Home wave:** Phase 4.x. Off by default; opt-in via settings always.
- **Source:** UX/UI bible v0.2 §2.4.

---

## Selection / conversation

### FF-011: Selection visual final form

- **What:** Halo+outline (current default from WP-3.1.S.1) vs CRT-blinking-box (terminal-cursor cadence, ~500ms on/off; reinforces early-2000s-computer aesthetic). Playtest decides.
- **Gate:** Both rendered in playable build; multiple sessions across players to compare reads.
- **Home wave:** Post-PT-NNN evidence; Phase 4.x polish.
- **Source:** UX/UI bible v0.2 §2.2.

### FF-012: Subtitle styling

- **What:** When the subtitle option for `SpeechFragment` is enabled, how is the corpus fragment rendered — inline at the conversation, screen-bottom, both?
- **Gate:** Subtitle option is exercised in playtest with players who use it (accessibility users, players who want to follow conversations literally).
- **Home wave:** Phase 4.x accessibility polish.
- **Source:** UX/UI bible v0.2 §6.2.

### FF-013: Conversation visualization specifics

- **What:** Per-pixel design of the text-stream-rises-between-conversers cue. v0.2 §3.8 commits to the *vocabulary* and the *severity-scales-with-volume* rule; specific shape is post-prototype.
- **Gate:** Prototype implementation lands and is playtested.
- **Home wave:** Phase 4.1.x art / animation pipeline.
- **Source:** UX/UI bible v0.2 §3.8 / §6.3.

---

## Tutorial / first-launch

### FF-014: Tutorial / first-launch experience

- **What:** Diegetic intro candidates — tape-deck welcome message, HR letter on a desk somewhere, intercom announcement. Or no tutorial at all per axiom 1.3 rule 4.
- **Gate:** Game is complete enough to warrant orienting a new player. Playtest evidence on whether new players get lost.
- **Home wave:** Phase 4.4.2 (already on the Phase 4 roadmap).
- **Source:** UX/UI bible v0.2 §6.2.

---

## Audio

### FF-015: Per-archetype voice profiles

- **What:** Sims-gibberish phoneme profiles per archetype — the Old Hand, the Newbie, the Vent each sound distinguishable. Engine emits `SpeechFragment` triggers; host synthesises against archetype voice profile.
- **Gate:** WP-4.1.0 dispatch. Already on the Phase 4 roadmap.
- **Home wave:** Phase 4.1.0 (scheduled).
- **Source:** UX/UI bible v0.2 §6.2.

---

## Authoring tools

### FF-016: In-game scene authoring loop

- **What:** A live in-game level-design surface — author rooms, lights, windows, and NPCs without recompiling, save the result as a `world-definition.json` file, hot-reload to iterate. Built on the existing `WorldDefinitionLoader` + `IWorldMutationApi` substrate; round-trip via `WorldDefinitionWriter`. WARDEN-only (dev / mod-author surface; not exposed to retail players).
- **Architectural sketch:**
  - `WorldDefinitionWriter` (WP-4.0.I) serializes the live ECS world to the existing JSON format consumed by `WorldDefinitionLoader`. No schema bump in v0.1 (additive `placedProps` deferred to a future packet that resolves build-mode template ids).
  - Author mode (WP-4.0.J) extends `IWorldMutationApi` with six new operations: `CreateRoom`, `DespawnRoom` (with `RoomDespawnPolicy` — OrphanContents | CascadeDelete), `CreateLightSource`, `TuneLightSource`, `CreateLightAperture`, `DespawnLight`. JSON palette catalog at `docs/c2-content/build/author-mode-palette.json` (9 room kinds, 9 light kinds, 4 aperture sizes — modder-extensible). Unity-side `AuthorModeController` (WARDEN-gated MonoBehaviour) toggles via `Ctrl+Shift+A` and exposes the engine API as Unity-side methods for Editor-wired UI.
  - NPC authoring (WP-4.0.K) adds three more operations to `IWorldMutationApi`: `CreateNpc`, `DespawnNpc`, `RenameNpc`. `CreateNpc` reuses `CastGenerator.SpawnNpc` so authored NPCs are indistinguishable from boot-time NPCs (same archetype-correct drives / personality / inhibitions / silhouette). `CastNamePool` wraps `CastNameGenerator` (WP-4.0.M) with collision retry for auto-naming. Cross-cutting: `NpcSlotComponent` and the world-definition JSON gain a `nameHint` field so authored names round-trip through save/reload (a long-standing bug — the JSON had `nameHint` but the loader was silently dropping it).
- **Relationship to undo/redo:** Reuses the existing build-mode v2 (WP-4.0.G) undo stack via `IUndoableMutation` adapters wherever new mutations are exposed through the user-facing palette UI. Single Ctrl+Z affordance covers gameplay-time prop placement *and* author-mode mutations. The engine API itself doesn't ship undo wrappers in v0.1 (the wrappers belong on the UI side, where intent + visual feedback live).
- **Pilot approach:** Engine substrate landed in three sequential packets (M name generator → I writer → J author-mode mutations + Unity controller → K NPC mutations + name pool). Wave-4 ships the foundation; the user-facing palette UI (BuildPaletteUI tab extensions, RoomRectangleTool, LightSourceTool, LightApertureTool, EraserTool, NpcArchetypeSpawnTool with ghost-preview integration, save/load toolbar widget, sandbox scenes) is **deferred to Editor follow-up** because Unity tools need iterative visual feedback to land well. The engine substrate gives Editor work a concrete surface to wire UI buttons against without further engine changes.
- **Deferred deliverable (Opus, post-Editor-verification):** A comprehensive `docs/AUTHORING-GUIDE.md` (3000-5000 words; full workflow / all tools / edge cases / troubleshooting / modder API quickstart with code samples / community-scene PR conventions). Authored by Opus directly after the Unity-side palette is wired and used in anger; Sonnet docs from spec are accurate but lack lived-in clarity.
- **Gate:** None for the engine substrate (shipped wave 4 of Phase 4.0.x). Gate for Unity tools UI: Editor session to wire palette tabs + tools onto the existing BuildModeController / BuildPaletteUI infrastructure.
- **Home wave:** Phase 4.0.x wave 4.
- **Source:** Talon's 2026-05-03 framing ("I don't have a good way to get items into the world without a predefined scene… I need controls and means of level design and architecture"). Aligns with SRD §8.8 (incremental Mod API surfacing) — author mode is the canonical first-class modder surface.
- **Shipped (engine substrate):** WP-4.0.M (cast name generator) — 2026-05-03; WP-4.0.I (world-def writer) — 2026-05-03; WP-4.0.J (author-mode mutations + AuthorModeController) — 2026-05-03; WP-4.0.K (NPC mutations + CastNamePool + NameHint round-trip fix) — 2026-05-03.

---

## Maintenance notes

- **Adding entries:** When a new deferred feature is identified in conversation or in a packet, append it here with the next FF-NNN id (zero-padded, monotonic).
- **Promoting to a packet:** When the gate is satisfied, author the work packet in `docs/c2-infrastructure/work-packets/`, then mark this entry `Shipped: WP-NN.x — YYYY-MM-DD`. Keep the entry; do not delete.
- **Refining an entry:** If the gate or scope changes, update in place and note the change date in a `Revised: YYYY-MM-DD` line at the bottom of the entry.
- **Removing an entry:** Only if the feature is explicitly killed (Talon decides it's no longer wanted). Replace the entry's body with a short `Killed: YYYY-MM-DD — <reason>` note; keep the FF-NNN id reserved.
