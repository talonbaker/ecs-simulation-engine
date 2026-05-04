# WP-4.2.5 — Affair Detection Mechanic

> **Phase 4.2.x emergent gameplay (post-reorg).** Adds an affair-discovery surface: when two NPCs in an existing relationship have their `activeAffair` arc and a third NPC witnesses them in a compromising adjacency (e.g., long stationary time together in the supply closet, after-hours co-presence), a `WitnessedAffair` narrative event fires. Witness NPCs accrue gossip, inhibition shifts, and a stress cascade. Tests the relationship + memory + dialog cascade under disclosure stress.

> **DO NOT DISPATCH UNTIL WP-4.2.0 IS MERGED** — uses `ZoneIdComponent` for "same zone, different room" detection.

**Tier:** Sonnet
**Depends on:** WP-4.2.0 (zones), WP-3.2.5 (per-archetype tuning — gossip propensity), Phase 1 relationship-as-entity infrastructure (the-affair archetype).
**Parallel-safe with:** Other 4.2.x scenarios.
**Timebox:** 120 minutes
**Budget:** $0.55
**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** Does affair detection feel like an organic discovery (witness happens to be there, gossip spreads naturally) rather than a mechanic firing? Are the cascades proportionate (gossip-heavy archetypes spread it; close-friends suppress it)?

---

## Goal

When the existing `the-affair` archetype's `activeAffair` relationship has a "compromising adjacency" event, generate a witness-discovery cascade:

1. **Detection trigger:** two NPCs in `activeAffair` relationship are co-located (same room) for N consecutive ticks, after work hours, OR in a low-traffic room (supply closet, bathroom corridor). A third NPC enters the same room → `WitnessedAffairEvent`.
2. **Witness reaction:** the witness's drives shift per archetype (the-vent +Status / +Irritation; the-cynic +Suspicion / +Loneliness; the-affair-themselves: +Inhibition / +Stress; the-recovering: relapse risk increase).
3. **Gossip propagation:** witnessed-affair tagged into the witness's per-pair memory + chronicle. Per-archetype `gossipPropensity` determines how quickly the witness spreads it through their adjacency network.
4. **Disclosure resolution:** the affair partners' `Inhibition` rises; possible `affairExposure` arc (NPC tries to confess / spouse contact, future packet).

Single packet ships detection + witness + gossip primitives. Disclosure-arc resolution is deferred.

---

## Reference files

- `docs/c2-content/cast-bible.md` — the-affair archetype reference.
- `APIFramework/Systems/Spatial/ProximityEventSystem.cs` — co-presence detection pattern.
- Phase 1 relationship spawn / `RelationshipKind.activeAffair` references.
- `docs/c2-content/dialog-bible.md` — for dialog corpus extension (future packet integrates).

---

## Non-goals

- Do **not** model spouses (NPC-NPC marriage). v0.1 affair = workplace affair within current cast; no extra-cast spouse model.
- Do **not** model HR investigation, formal complaints. Narrative shorthand only.
- Do **not** ship the disclosure / confrontation arc. Detection + witness + gossip is enough for v0.1.
- Do **not** add cross-zone affair detection. Witness must be in same zone (zone substrate respected).

---

## Design notes

### `WitnessedAffairComponent` on witness NPC

```csharp
public struct WitnessedAffairComponent
{
    public Guid PartnerA          { get; init; }
    public Guid PartnerB          { get; init; }
    public long WitnessedTick     { get; init; }
    public bool HasGossipedAlready{ get; init; }   // tracks whether they've spread it
}
```

### `AffairDetectionSystem`

Per-tick:
1. Find all entities with `activeAffair` relationship and `LongAdjacencyComponent` (NPCs co-located ≥ N ticks).
2. For each such pair, check if a third NPC has entered the room.
3. If yes, attach `WitnessedAffairComponent` to the third NPC; emit `WitnessedAffairEvent`.

`[ZoneLod(CoarsenInInactive)]` — affair detection in inactive zones runs at lower fidelity (witnesses "discover" things they would have noticed anyway, just at coarser resolution).

### Gossip propagation

`GossipPropagationSystem` walks witnesses with `WitnessedAffairComponent.HasGossipedAlready == false`:
1. Find adjacent NPCs (within radius 4).
2. Per-tick gossip-spread roll based on witness archetype `gossipPropensity` × neighbor `gossipReceptivity`.
3. On spread: attach `WitnessedAffairComponent` (with `HasGossipedAlready = false`) to neighbor, transitively spreading. Set source's `HasGossipedAlready = true`.

Per-archetype `gossipPropensity` extended in archetype JSON. The-vent: 0.95 (immediate). The-hermit: 0.05 (might not tell anyone). The-cynic: 0.40.

### Performance

Cheap (rare events; per-tick checks are bounded). LoD safe.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/WitnessedAffairComponent.cs` (new) | State on witness. |
| code | `APIFramework/Systems/Affair/AffairDetectionSystem.cs` (new) | Trigger detection. |
| code | `APIFramework/Systems/Affair/GossipPropagationSystem.cs` (new) | Spread. |
| code | `APIFramework/Components/NarrativeEventKind.cs` (modification — additive) | `WitnessedAffair`, `AffairGossiped`. |
| data | `docs/c2-content/tuning/archetype-*.json` (modification) | Add `gossipPropensity` + `gossipReceptivity`. |
| test | 4+ test files covering detection / witness reaction / gossip propagation / zone-bounded behavior. | unit + integration |

---

## Acceptance tests

| ID | Assertion |
|:---|:---|
| AT-01 | Co-located affair partners + third-NPC arrival fires `WitnessedAffairEvent`. |
| AT-02 | Witness's drives shift per archetype reaction profile. |
| AT-03 | Gossip propagates to adjacent NPCs based on archetype propensity. |
| AT-04 | The-vent witness gossips within 1-2 ticks of detection; the-hermit witness rarely propagates. |
| AT-05 | Detection does NOT cross zones (witness must be in same zone). |
| AT-06 | All Phase 0–3 + 4.0.A–L + 4.2.0 tests stay green. |
| AT-07 | `dotnet build` warning count = 0; all tests green. |

---

## Followups (not in scope)

- Disclosure / confrontation arc (the-affair NPC's spouse-contact, public confession, dramatic exit).
- Spouse-as-NPC modeling (extra-cast).
- Player-driven cover-up mechanic.
- Affair-related dialog corpus extension.

---

## Completion protocol

Standard. **Cost target $0.55.**
