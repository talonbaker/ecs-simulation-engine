# Engine Fact Sheet

> **PLACEHOLDER** — Regenerate this file with `ECSCli ai describe --out docs/engine-fact-sheet.md`.
>
> This file is part of the cached corpus (`cached-corpus.manifest.json` slab 1).
> Until regenerated it contains only structural notes derived from the source at Phase-0 time.

---

## Overview

The ECS Simulation Engine is a headless .NET 8 entity–component–system simulation.
Entities are integer IDs. Components are plain data structs stored in typed arrays.
Systems iterate components in a fixed phase order each tick.

---

## Component types (Phase 0)

| Component | Assembly | Purpose |
|:---|:---|:---|
| `MetabolismComponent` | APIFramework | Satiation, hydration, body temperature, drain rates |
| `StomachComponent` | APIFramework | Food queue, digestion progress |
| `SmallIntestineComponent` | APIFramework | Chyme absorption |
| `LargeIntestineComponent` | APIFramework | Water reabsorption, waste mobility |
| `ColonComponent` | APIFramework | Stool capacity, urge threshold |
| `BladderComponent` | APIFramework | Urine volume, urge threshold |
| `EnergyComponent` | APIFramework | Energy, sleepiness |
| `MoodComponent` | APIFramework | Eight Plutchik emotion axes (0–100) |
| `BrainComponent` | APIFramework | Dominant drive, urgency scores |
| `PositionComponent` | APIFramework | World-space X/Y coordinates |
| `TransitComponent` | APIFramework | In-transit state, destination, progress |
| `WorldItemComponent` | APIFramework | Item metadata (food, water) |
| `WorldObjectComponent` | APIFramework | Object metadata (bed, toilet, fridge) |

---

## Systems and phases (Phase 0)

Systems execute in this order each tick:

1. `MetabolismSystem` — drains satiation and hydration at configured rates
2. `EnergySystem` — updates energy / sleepiness; applies Tired / Exhausted tags
3. `BiologicalConditionSystem` — applies Hungry, Thirsty, Dehydrated, Starving, Irritable tags
4. `DigestionSystem` — processes stomach → small intestine → large intestine → colon/bladder
5. `MoodSystem` — updates all eight emotion axes; applies Plutchik tier tags
6. `BrainSystem` — scores all drives, selects dominant, applies BoredTag when idle
7. `FeedingSystem` — spawns food entities when hunger exceeds threshold
8. `DrinkingSystem` — spawns water entities when thirst exceeds threshold
9. `InteractionSystem` — processes bite/gulp interactions
10. `SleepSystem` — transitions entities to/from sleep state
11. `RotSystem` — advances food rot level; applies RotTag
12. `TransitSystem` — moves entities toward destinations

---

## SimConfig keys (see `SimConfig.json`)

| Key path | Type | Description |
|:---|:---|:---|
| `world.defaultTimeScale` | `double` | Game-seconds per real-second |
| `entities.human.*` | object | All human entity parameters (see SimConfig.json) |
| `entities.cat.*` | object | All cat entity parameters |
| `systems.biologicalCondition.*` | object | Hunger/thirst tag thresholds |
| `systems.energy.*` | object | Tired/exhausted thresholds |
| `systems.brain.*` | object | Drive scoring caps, urgency bonus |
| `systems.feeding.*` | object | Hunger threshold, nutrition queue cap, banana nutrients |
| `systems.drinking.*` | object | Hydration queue caps, water nutrients |
| `systems.digestion.*` | object | Satiation/hydration conversion factors |
| `systems.sleep.*` | object | Wake sleepiness threshold |
| `systems.mood.*` | object | Plutchik thresholds, decay/gain rates |
| `systems.rot.*` | object | Rot tag threshold |
