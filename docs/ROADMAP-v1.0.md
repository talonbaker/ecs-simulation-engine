# ECS Simulation Engine — v1.0 Release Roadmap

**Author:** Talon Baker  
**Written:** April 2026, post-v0.7.0 release  
**Status:** Working draft, covers v0.7.1 through v1.0

---

## What "1.0" Means for This Project

Version numbers carry a promise. A 1.0 should mean that the core architectural contract is stable, the feature set is coherent, and someone picking up this project would understand it as a complete system rather than a scaffold. That's the bar we're holding ourselves to here.

For this engine specifically, 1.0 means four things:

**The biology is honest.** Billy eats, digests nutrients, absorbs them into body stores, burns them over time, and develops measurable deficiencies if he doesn't eat a balanced diet. The full digestive pipeline runs from mouth to elimination, including small intestine, large intestine, kidney, bladder, and rectum. This doesn't need to be medically accurate — it needs to be *mechanically coherent*. Every system in the body does a job, and that job connects meaningfully to the next system downstream.

**The world exists.** Billy lives somewhere. There is a fridge, a sink, a toilet, and a bed. He walks to them. He cannot eat by magic — the `FeedingSystem` must find food in the world and initiate movement toward it. This spatial layer is the single biggest architectural leap between where we are now and 1.0, and it cannot be bolted on as an afterthought. It has to be designed so that none of the digestive, mood, or brain systems need to know it exists.

**The systems are decoupled.** This is already largely true at v0.7.0, but there are fragile seams — particularly around food conjuring in `FeedingSystem` and the absence of any spatial contract. By 1.0, no system should reach for a world entity without going through a spatial interface. Biology stays biology. Movement stays movement. The brain mediates.

**The codebase is defensible.** This doesn't mean tests for everything. It means the architecture can be explained to a stranger in 20 minutes, every major decision has a comment justifying it, the configuration system covers all tuning knobs, and the CLI and visualizer both work without needing pre-knowledge of internal wiring. A developer learning ECS from this project should be able to trace a banana from the fridge into the bloodstream and understand every handoff.

That's 1.0. It is not a shipped game, not a complete human simulation, and not a multiplayer framework. It is a pedagogically clear, architecturally honest simulation of one human in one small apartment.

---

## The Build Situation: Before Anything Else

v0.7.0 was grep-verified but not build-verified. Before any new work begins, `dotnet build` must pass. The prior refactor replaced all scalar nutrient fields (`NutritionValue`, `HydrationValue`, `NutritionQueued`, `HydrationQueued`) with `NutrientProfile` struct references throughout the pipeline. The grep pass confirmed no old field names remain in `.cs` files, but the compiler may surface issues that grep cannot — type mismatches, struct initialization gaps, missing operator overloads on edge paths, or configuration classes that were partially updated.

This is not a reason to hold up planning, but it is the mandatory first task of any continuation session. If the build fails, fix it before writing new code. Discovering that v0.7.0 has a broken build while halfway through v0.7.1 creates ambiguous attribution and makes it harder to know what you actually broke.

---

## v0.7.x: Completing the Excretory Pipeline

The remaining v0.7.x milestones share a common pattern: a new organ or organ pair, new component(s) to carry state, a new system or two to move content through, and a new desire or tag to surface the organ's urgency to the brain. Each one follows the same architectural template that was established by `StomachComponent` + `DigestionSystem`. Keeping to this template is what makes the codebase teachable.

### v0.7.1 — Small Intestine and Large Intestine

`DigestionSystem` currently absorbs nutrients directly from the stomach into `MetabolismComponent.NutrientStores`. This was the right shortcut for v0.7.0 — it preserved the feel of the pre-nutrient system and kept the scope of the NutrientProfile migration bounded. Now it needs to be extended one stage further.

The design: `DigestionSystem` continues to release chyme from the stomach (same logic, same rate). But instead of writing directly to `NutrientStores`, it passes a `NutrientProfile` packet to a new `SmallIntestineComponent` — a component that sits on the human entity, just like `StomachComponent`. The `SmallIntestineSystem` runs after `DigestionSystem` in the pipeline. It absorbs macronutrients (carbohydrates, protein, fat) plus fat-soluble vitamins and most minerals into `NutrientStores`. The residue — fiber, unabsorbed water, water-soluble vitamins that exceeded absorption capacity — passes to `LargeIntestineComponent`.

`LargeIntestineSystem` then handles water recapture. The large intestine is primarily a water-reclamation organ; roughly 90% of the water that enters it is reabsorbed. This matters for hydration dynamics: a food like a banana, which carries real water in its `NutrientProfile`, should contribute meaningfully to hydration but the final amount absorbed depends on how much of it made it through small intestine transit. The compacted residue accumulates in the large intestine until it reaches `LargeIntestineConfig.TransitThreshold`, at which point it moves to the rectum (which arrives in v0.7.3).

The architectural decision here is that `SmallIntestineComponent` and `LargeIntestineComponent` are components on the human entity, not traveling entities. This is consistent with how `StomachComponent` works. Organs are stateful bags — they hold contents and describe their current condition. It's the systems that move things between them. Food boluses are traveling entities because they physically transit the esophagus. Chyme and intestinal contents don't need to be entities because there's no branching path they could take: the pipeline is strictly linear, and no other system needs to query "what's currently in the small intestine" except `SmallIntestineSystem` and `LargeIntestineSystem`. If that changes, elevating them to entities is straightforward.

The UI should expose this. The NUTRIENTS panel in `ECSVisualizer` currently shows `NutrientStores`; it should grow a "TRANSIT" section that shows what's queued in each intestinal stage. This makes the pipeline legible in real time and is a good teaching tool.

### v0.7.2 — Kidneys, Bladder, and the Urination Desire

The kidneys are the body's filtration system. Their job in this simulation is to read `NutrientStores.Water` (the water that has been absorbed into Billy's blood), compare it against an ideal setpoint, and — if there's excess — produce urine that flows into `BladderComponent`.

`KidneyComponent` should track filtration rate (ml per game-minute) and a minimum threshold below which the kidneys stop producing urine to conserve water during dehydration. This directly ties the urinary system to the hydration system: a dehydrated Billy produces dark, concentrated urine at a much lower rate. This is medically plausible and mechanically useful because it means urination desire doesn't spike when Billy is already thirsty, which would create competing drives that the brain can't meaningfully resolve.

`BladderComponent` holds the accumulated urine volume. `BladderSystem` receives kidney output each tick. When `BladderComponent.VolumeMl` exceeds a threshold (say 60% of max capacity), `NeedToPeeTag` is applied. When it reaches 85–90%, an `UrgentUrinationTag` fires. `BrainSystem` scores `PeeUrgency` — it should be capable of overriding eat/drink/sleep when urgency is critical, which is an interesting emergent behavior: a Billy who desperately needs to urinate will prioritize that over eating, even if hungry.

The elimination action itself is intentionally simple at this stage. `EliminationSystem` checks whether `PeeUrgency` is dominant (or above a hard threshold), confirms Billy is "at" a toilet (which at v0.7.2 is a no-op check that always passes — the spatial layer isn't here yet), and drains `BladderComponent` to zero. The `MoodSystem` should reward this: bladder relief should spike Joy and reduce Anger/Discomfort. An entity that has been holding urgency for a long time should experience a noticeable mood swing.

The toilet entity should be seeded at v0.7.2 even though it does nothing spatially. A `ToiletTag` on an entity in the `EntityManager` serves as a placeholder that `EliminationSystem` can query. When the movement system arrives in v0.8 or v0.9, it replaces the no-op proximity check with a real one — and `EliminationSystem` itself doesn't change at all.

### v0.7.3 — Rectum, Defecation, and the Poop Desire

This follows the exact same pattern as v0.7.2. `RectumComponent` accumulates waste from the large intestine. `NeedToPoopTag` fires when contents exceed a threshold. `BrainSystem` scores `PoopUrgency`. `EliminationSystem` (which now handles both elimination types) drains the rectum when appropriate.

The notable behavioral design here is timing. Food doesn't reach the rectum instantly — it has to transit the stomach (hours in game time), then the small intestine (several game-hours), then the large intestine (many game-hours). The simulation runs at 120× real-time by default, so the end-to-end transit time should land somewhere in the 6–12 game-hour range — meaning Billy eats a meal and might not feel the downstream consequences until well into the next game-day. This creates a satisfying cause-and-effect arc that is only possible because the full pipeline is in place.

The MoodSystem treatment should mirror bladder relief: urgency held too long drives Anger and Discomfort; relief drives Joy. There may also be a mild Disgust edge here (the act of defecation is inherently unpleasant), which is a small but flavorful touch if it doesn't overcomplicate the tuning.

---

## v0.8 — Metabolism, Deficiencies, and Nutritional Stakes

At the end of v0.7.3, the digestive pipeline is complete but nutritionally toothless. Billy absorbs nutrients into `NutrientStores`, and they just... sit there. They accumulate but are never consumed. Nothing bad happens if he eats nothing but bananas for six game-days. This is the core thing v0.8 needs to fix.

### BodyMetabolismSystem

The first new system of v0.8 is `BodyMetabolismSystem`. It runs once per tick and drains `NutrientStores` at a per-nutrient burn rate configured in `SimConfig`. Calories are consumed continuously based on activity level (awake burns more than asleep). Water is consumed continuously regardless. Vitamins and minerals are consumed at much slower rates — their deficiencies emerge over days, not hours.

The caloric drain rate should be tuned so that Billy genuinely needs to eat 2–3 times per game-day to maintain `Satiation` above the `HungryTag` threshold. If the existing `MetabolismSystem` drain rate on `Satiation` is already achieving this, `BodyMetabolismSystem` can take over that responsibility and retire the legacy drain from `MetabolismSystem` — or the two systems can coexist with the body metabolism drain being an *additional* layer on top. The safer option is coexistence at first, with the tuning bringing the two channels into alignment.

### NutrientDeficiencySystem

This system reads `NutrientStores` and applies deficiency tags when any nutrient falls below a configured threshold. A few to implement at v0.8:

`LowProteinTag` — muscles aren't being repaired; energy drain rate increases. This gives protein a reason to exist in the simulation beyond just calories.

`LowVitaminCTag` — mild debuff to mood (Joy decay rate increases). Full scurvy is a long-arc deficiency that takes weeks of real-world deprivation; in game time at 120×, this might emerge after 3–4 game-days without any Vitamin C.

`HighSodiumTag` — thirst amplifier. Eating too much salty food makes Billy drink more, which eventually means more urination, which at 120× play rate creates a feedback loop that's observable in a single session.

`LowIronTag` — energy drain rate increases; EnergySystem's awake burn accelerates slightly.

These should all be tagged states with clear threshold configs in `SimConfig.json` — no magic numbers in system bodies. The tags feed into `MoodSystem` and `BiologicalConditionSystem` as additional inputs, exactly the same way `HungerTag` and `ThirstTag` already work. The key architectural point: deficiency tags are a read-only observation, not a direct command. Systems don't tell Billy he has low Vitamin C — they report a fact about his body, and downstream systems decide what to do with it.

### Food Variety and the Drive to Eat Differently

Once deficiencies exist, the `FeedingSystem` faces a more interesting problem: Billy should be more attracted to foods that address his current deficiencies. This doesn't need to be complex — a simple urgency bonus when a deficiency is active and a suitable food is available is enough. But it requires that the world entity pool have more than one food item in it.

For v0.8, seed the world with at least: banana (potassium, carbs), chicken (protein, B vitamins), water glass (hydration), orange (Vitamin C). This keeps the scope small while making deficiency-driven feeding observable.

---

## v0.9 — The Spatial Foundation

This is the most architecturally significant milestone on the path to 1.0. Everything up to this point has been a global simulation — Billy has no position, no address in the world, and no distance between himself and anything. v0.9 changes that. It is also the one most likely to create regressions if not designed carefully.

### The Core Constraint

The digestive pipeline must not know that space exists. `DigestionSystem`, `SmallIntestineSystem`, `EliminationSystem`, and every organ system is pure biology — it reads components on Billy's entity and doesn't care whether Billy is standing at the fridge or asleep in bed. Similarly, `MoodSystem` and `BrainSystem` must only be *informed* by spatial context, not *wired* to it. The brain says "I need food." The movement system decides to walk toward the fridge. The interaction system checks proximity, not biology.

This clean separation is what makes the long-term vision viable. It also happens to be a clean architectural boundary to learn from: the line between "what the body needs" and "how the body acts in space" is one of the sharpest separations in real systems architecture too.

### PositionComponent and MovementSystem

Add `PositionComponent` (a simple `(float X, float Y)` struct) and `VelocityComponent` to every entity that exists in space — Billy, world entities (fridge, sink, toilet, bed, food items). `MovementSystem` reads `VelocityComponent` and updates `PositionComponent` each tick. Billy's velocity is set by a new `NavigationSystem` that receives a movement intent (a target entity ID) and computes a straight-line or simple pathfinding vector.

The spatial grid doesn't need to be complex. A flat 2D grid (say 20×20 tiles) is sufficient for an apartment. The `NavigationSystem` doesn't need A* for v0.9 — direct vector movement to a target is fine, and the grid is small enough that walls can be handled as bounding checks. True pathfinding can come later if it matters.

### World Entities

The apartment entities should be spawned at `SimulationBootstrapper.RegisterEntities()` alongside Billy. Each gets a `PositionComponent` (fixed), a `WorldEntityTag`, and a type-specific component: `FridgeComponent`, `SinkComponent`, `ToiletComponent`, `BedComponent`. Fridge holds a list of `FoodItem` references (or child entity IDs). The fridge doesn't need its own ECS system — it's just a container entity that `FeedingSystem` can query.

This is the place where `FeedingSystem` needs its most significant rewrite. Currently, `FeedingSystem` checks for world food entities, then conjures a banana if none exist. In v0.9, it should instead: identify that the `EatUrgency` is dominant, look for food entities in the world (or in the fridge), set a movement intent on Billy (via a new `MovementIntentComponent`), and wait. The actual eating doesn't happen until Billy is within interaction range. The `InteractionSystem` gets a proximity gate: `if (distance(Billy, targetEntity) < InteractionRange) { ... }`. Before v0.9, that check always passes (or the intent is auto-fulfilled). After v0.9, Billy has to walk first.

The banana conjuring fallback should be removed entirely in v0.9. Once the spatial layer exists, it would be wrong for food to appear from nowhere. If the fridge is empty, Billy should go hungry until the simulation seeds more food (or hunger escalates through desperation tags into a mood spiral — which is itself interesting behavior).

### UI Changes for v0.9

The Avalonia visualizer needs a spatial view. At minimum, a simple 2D grid panel showing entity positions with ASCII or geometric icons. This doesn't have to be animated or beautiful — it needs to show Billy moving through the apartment in real time. The existing MOOD, NUTRIENTS, and STOMACH panels can remain in a sidebar while the spatial view takes center stage.

The CLI snapshot format should include a simple spatial map in its output.

---

## v1.0 — Integration, Polish, and Stability

By the end of v0.9, all the pieces exist. v1.0 is about integration, honest documentation, and closing the gaps that accumulated during fast development.

### Integration Testing

The full scenario should be validated end-to-end: Billy wakes up, gets hungry, walks to the fridge, eats breakfast, eventually needs to urinate, walks to the toilet, eliminates, feels relief, gets tired in the evening, walks to bed, sleeps. Every one of those steps involves at least three or four systems collaborating. Run this scenario at several time scales and validate that nothing breaks at 1× or 480×.

`SimMetrics` should cover the new pipeline stages by v1.0. Track first urination, first defecation, first deficiency tag, first movement event. The end-of-run report should include a "biological completeness" section — did every system fire at least once? This is the simulation equivalent of a test suite.

### The Invariant System Audit

`InvariantSystem` currently guards `Satiation`, `Hydration`, and `Energy`. By v1.0 it needs to guard every organ fill level: `StomachComponent.CurrentVolumeMl` ≤ max, bladder fill ≤ max, intestinal transit queues ≤ max, `NutrientStores` fields ≥ 0. Any violation should log clearly enough that a developer can identify which system caused it.

The philosophical point here is that `InvariantSystem` is a sentinel — it enforces the assumption that "components cannot hold physically impossible values." If it fires, something upstream is doing arithmetic wrong. By v1.0, every value that could be pushed out of bounds by any system should be covered.

### Configuration Completeness

Every hardcoded value in every system file should be in `SimConfig.json` by v1.0. This includes the new absorption rates added in v0.7.1, the kidney filtration rate added in v0.7.2, the deficiency thresholds added in v0.8, and the movement speed and interaction range added in v0.9. A user should be able to tune every meaningful behavior without touching a `.cs` file.

### API Documentation

`ISystem`, `IComponent`, `EntityManager`, and `SimulationEngine` need XML doc comments. Not for every method — for the contract. What is an entity? Why are components value types? What's the system ordering contract? What does `Query<T>()` return and when is it safe to call? These should be in the code, not just in HANDOFF documents that can drift.

### Semantic Versioning Promise

At 1.0, the public API (the ECS framework layer, not the simulation-specific components) should be stable. That means `Entity`, `EntityManager`, `ISystem`, `IComponent`, and `SimulationBootstrapper` can be referenced by an external project without expecting breaking changes in future patch releases. Biology systems can still change — that's the simulation layer, not the framework layer. But the framework itself should be frozen.

---

## Architectural Risks and Gaps

These are the places that could become problems if not addressed intentionally.

### The Banana Conjuring Problem

`FeedingSystem` currently conjures a banana if no world food is available. This is a pragmatic shortcut that made it possible to test the digestion pipeline before the world existed. The risk is that it becomes load-bearing — other systems implicitly assume food is always available, and the conjuring fallback papers over failures. Before v0.9, audit `FeedingSystem` to confirm which behaviors depend on guaranteed food availability. Consider replacing the conjure with a `FoodUnavailableTag` and testing what happens to Billy's mood and drive system when he genuinely can't eat.

### Organ System Coupling Through DigestionSystem

`DigestionSystem` currently writes directly to `MetabolismComponent.NutrientStores`. In v0.7.1, this write should move to `SmallIntestineSystem`. The risk is that the intermediate step — `DigestionSystem` now writes to `SmallIntestineComponent` instead of `MetabolismComponent` — breaks the pre-v0.7 behavior feel if the absorption dynamics change. Be explicit about this: write a config comment in `SimConfig.json` explaining which conversion factors govern each stage and what the expected feel should be (banana ≈ 35 satiation per eating event). If the numbers drift from this, the config is broken, not the system.

### MoodSystem Has Stub Inputs

`Trust`, `Fear`, and `Surprise` are decaying with no inputs. This is fine architecturally — it means they'll all drift toward zero and stay there, which isn't *wrong*, just neutral. The risk is that the presence of these fields creates an implicit promise that spatial events will fill them (a strange sound → Surprise; an approaching threat → Fear). If spatial events don't feed mood by v0.9, consider removing the stub inputs from the MoodSystem comment documentation, or accepting that these emotions are not active in v1.0. Stub systems that do nothing mislead developers reading the code about what's actually happening.

### TimeScale Sensitivity

All drain rates and timing behaviors are tuned against the default 120× time scale. If the time scale changes dramatically — say, someone runs a 1× demo for a presentation — the simulation should still feel coherent. This means every rate in `SimConfig` should carry a comment explaining what the value produces at 120×. It doesn't mean the system needs to be time-scale-invariant (though that's a nice long-term property). It means the reader can quickly recalibrate.

### Multi-Entity State

Only Billy is instantiated. The `Cat` template exists. Before v1.0, decide whether multi-entity simulation is in scope. If it is, `FeedingSystem`, `DrinkingSystem`, and the upcoming `EliminationSystem` need to handle the case where two entities are competing for the same world resource (fridge, toilet). If it isn't, make that explicit in a comment in `EntityTemplates.cs` — the cat is a future exercise, not an omission.

### The No-Build-Verification Pattern

The v0.7.0 handoff noted that the build was not verified in the Linux sandbox. This pattern is risky as the codebase grows. Strongly consider adding a CI step — even a simple GitHub Actions workflow that runs `dotnet build` on push — so that every release can be stated with confidence to be build-clean. Grep verification is fast and valuable, but it cannot catch type errors, missing struct fields, or interface mismatches.

---

## Proposed Version Sequence

The milestones below describe the logical grouping of work from now to 1.0. Patch versions within each minor are intentionally left flexible — they'll be defined as work unfolds and the scope of each task becomes clearer in practice.

**v0.7.1** — SmallIntestineComponent, LargeIntestineComponent, SmallIntestineSystem, LargeIntestineSystem. Chyme moves through both stages. Water recapture from large intestine. Visualizer shows intestinal transit.

**v0.7.2** — KidneyComponent, BladderComponent, KidneySystem, BladderSystem, NeedToPeeTag, UrgentUrinationTag. BrainSystem scores PeeUrgency. EliminationSystem (pee path only). ToiletTag stub entity.

**v0.7.3** — RectumComponent, NeedToPoopTag. EliminationSystem gains defecation path. MoodSystem receives elimination relief inputs. Full excretory pipeline complete.

**v0.8.0** — BodyMetabolismSystem (daily burn). NutrientDeficiencySystem. At least four deficiency tags (LowProtein, LowVitaminC, HighSodium, LowIron) feeding into mood and energy. World food variety (minimum four food items). Banana conjure fallback begins deprecation.

**v0.8.x** — Tuning pass. Validate full biological loop (eat → absorb → burn → deficiency → seek food). InvariantSystem audit, organ volume guards. Config completeness pass for all new systems.

**v0.9.0** — PositionComponent, VelocityComponent, MovementSystem, NavigationSystem. World entities with fixed positions (fridge, sink, toilet, bed). FeedingSystem rewritten to set movement intent. InteractionSystem gains proximity gate. Banana conjure fully removed. Basic 2D visualizer panel.

**v0.9.x** — Spatial scenario validation. Full day-in-the-life end-to-end run: wake → eat → pee → poop → sleep, all via spatial movement. CLI snapshot includes apartment map. SimMetrics covers all new events.

**v1.0.0** — Integration audit. Invariant coverage complete. Config completeness complete. XML doc comments on framework interfaces. Multi-entity decision documented. No stub systems in the pipeline without explicit notes. API stability promise made. Release notes comprehensive.

---

## A Note on Learning This Alongside Building It

This project is being built as a learning exercise in ECS architecture and framework design, alongside the goal of producing something real. That dual purpose shapes what counts as good work here.

Some shortcuts that would be acceptable in a production deadline — conjuring bananas, stub mood inputs, no build CI — are actively educational liabilities here, because the codebase is the curriculum. Every place where the code doesn't match the stated architecture is a place where the learning is incomplete.

The strongest argument for the architecture choices in this project — the strict system ordering, the tag-based communication, the separation of biology from space — isn't that they're the only right way to build a simulation. It's that they force you to think about dependencies explicitly. When you have to add a `NeedToPeeTag` through `BiologicalConditionSystem` rather than checking bladder fill directly in `BrainSystem`, you're learning that observation and decision should live in separate systems. That's a principle that transfers to every distributed system, every large backend, every domain-driven design you'll ever encounter.

By 1.0, this project should be something you could show a hiring manager not just as working code, but as evidence that you understand why it's structured the way it is. That means the comments should explain the why, the CHANGELOG should tell the story of how the architecture evolved, and the roadmap should be honest about what was hard.

Keep that in mind as each milestone ships.
