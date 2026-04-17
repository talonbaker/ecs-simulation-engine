# ECS Simulation Engine — Handoff for Fresh Chat (post-v0.7.0)

Paste this whole document into a new chat as the first message. It captures everything the next model needs to pick up where we left off without losing the thread.

---

## 1. Project context

- **Repo:** `C:\repos\_ecs-simulation-engine`
- **Solution:** `ECSSimulation.sln` (APIFramework + ECSCli + ECSVisualizer)
- **Stack:** C# / .NET, Avalonia for GUI, CommunityToolkit.Mvvm for observable properties.
- **User:** Talon — learning ECS (entities, components, systems) and framework/architecture skills on the path to senior dev. Values clean, educational, well-commented code. Prefers architectural decisions be explained and the *why* captured in comments/changelogs.
- **Current version:** `0.7.0` (in `APIFramework/Core/SimVersion.cs`).
- **The Subject:** The simulation follows "Billy" (a human entity) through a biological day/night cycle. A cat entity is also templated.

### Long-term vision (keep this in mind for every architecture decision)
Billy eventually lives in an apartment and physically interacts with world entities: fridge, sink, toilet, bed, food on counters. Systems must stay decoupled from any spatial/UI layer so a MovementSystem / spatial grid can land later without rewriting biology. World food entities and rot (v0.6 pattern) already exist; the toilet will eventually be a world entity too.

---

## 2. Architecture patterns (follow these religiously)

- **Entity = Guid ID only.** No fields beyond identity.
- **Component = pure data struct.** No methods beyond computed-property helpers.
- **System = stateless `ISystem` class.** Constructor takes its config struct from `SystemsConfig`. Logic runs in `Update(EntityManager em, float deltaTime)`.
- **Tags are empty structs** used as ECS flags (`HungerTag`, `SleepingTag`, `RotTag`, `ConsumedRottenFoodTag`, etc.).
- **Organs-as-components-on-body**, NOT travelling entities. Stomach is a component on Billy; future SmallIntestine, LargeIntestine, Bladder, Rectum will also be components on Billy. Food boluses ARE entities (they travel through the esophagus), but once absorbed the nutrients pool into the body's components.
- **`NutrientProfile` struct** is the universal nutrient carrier. It flows through every digestive stage via operator overloads (`+`, `-`, `*`). When adding a new digestive stage, give it a `NutrientProfile` field for its current contents.
- **All tuning lives in `SimConfig.json`** → deserialized into `SimConfig.cs` classes → injected into each system's constructor. Never hard-code balance values in system bodies.
- **Hot-reload via `SimulationBootstrapper.ApplyConfig()`.** `MergeFlat` reflects over value-type public properties and copies them in-place. When adding a new `XyzSystemConfig` class, also add a `MergeFlat` call in `ApplyConfig`.
- **CLAUDE.md rule:** no emojis in files unless the user asks. Extensive comments explaining the *why* are welcome — this is a learning project.

### Pipeline (current v0.7.0 — 13 systems, in order)
1. InvariantSystem — clamp impossible values
2. MetabolismSystem — drain satiation/hydration (sleep multiplier applied)
3. EnergySystem — drain/restore energy + sleepiness
4. BiologicalConditionSystem — hunger/thirst/irritable tags
5. MoodSystem — 8 Plutchik emotions with decay/gain + intensity tags
6. BrainSystem — score drives, pick dominant
7. FeedingSystem — act if Eat dominant
8. DrinkingSystem — act if Drink dominant
9. SleepSystem — toggle IsSleeping
10. InteractionSystem — held food → esophagus bolus
11. EsophagusSystem — move transits toward stomach
12. DigestionSystem — release nutrients from stomach to body (**takes `DigestionSystemConfig` as of v0.7.0**)
13. RotSystem — age food entities, apply RotTag

---

## 3. What just shipped in v0.7.0 (nutrient decomposition)

The big conceptual change: food is no longer a scalar. Every piece of food, liquid, bolus, stomach queue, and body store carries a full `NutrientProfile`.

### New files
- `APIFramework/Components/NutrientProfile.cs` — 16-field struct (4 macros, water, 6 vitamins, 5 minerals). `Calories` derived via Atwater (4/4/9). `IsEmpty` helper. Operators `+`, `-`, `*` (commutative scalar).

### Updated components
- `BolusComponent` — `NutritionValue` → `Nutrients` (NutrientProfile)
- `LiquidComponent` — `HydrationValue` → `Nutrients`
- `FoodObjectComponent` — `NutritionPerBite` → `NutrientsPerBite`
- `StomachComponent` — `NutritionQueued` + `HydrationQueued` → single `NutrientsQueued`
- `MetabolismComponent` — **new field** `NutrientStores` (cumulative body pool)

### Updated systems
- `DigestionSystem` — now takes `DigestionSystemConfig`. Each tick: release `ratio * NutrientsQueued`, pool into `meta.NutrientStores`, derive `Satiation += released.Calories * SatiationPerCalorie`, `Hydration += released.Water * HydrationPerMl`.
- `EsophagusSystem`, `FeedingSystem`, `DrinkingSystem`, `InteractionSystem` — all updated to use the new NutrientProfile fields.
- `InvariantSystem` — new `GuardNonNegative(ref NutrientProfile ...)` helper that clamps all 16 fields to [0, ∞). Applied to `NutrientStores` and `NutrientsQueued`.
- `EntityTemplates` — initialises `NutrientStores = new NutrientProfile()` on human/cat spawn.

### Config
- `FoodItemConfig.Nutrients` — default banana: 27g carbs, 1.3g protein, 0.4g fat, 3.1g fiber, 89ml water, vitamin B6, C, potassium, magnesium.
- `DrinkItemConfig.Nutrients` — default water: 15 ml water, nothing else.
- **New** `DigestionSystemConfig` — `SatiationPerCalorie = 0.3`, `HydrationPerMl = 2.0`.
- `FeedingSystemConfig.NutritionQueueCap` now in kcal (default 240, was 70 points).
- `DrinkingSystemConfig.HydrationQueueCap`/`...Dehydrated` now in ml (defaults 15/30, was 30/60 points).

### Tuning invariant (critical — don't break this)
Pre-v0.7 feel is preserved by the conversion factors:
- Banana ≈ 117 kcal × 0.3 ≈ **35 satiation** (was flat 35)
- Water 15 ml × 2.0 = **30 hydration** (was flat 30)

### UI
- CLI `CliRenderer.cs` — new NUTRIENTS section (calories, macros, water, vitamins, minerals) + queued-stomach macros line.
- Avalonia `EntityViewModel.cs` + `MainWindow.axaml` — new green NUTRIENTS panel beneath Stomach, auto-hides when empty.

### ⚠ Build not verified
The Linux sandbox I was running in doesn't have dotnet installed, so I couldn't run `dotnet build`. I did grep-verify that no `.cs` file still references any of the removed scalars (`NutritionValue`, `HydrationValue`, `NutritionQueued`, `HydrationQueued`, `NutritionPerBite`). **First thing the next model should do: `dotnet build` and fix any compile errors.** Likely clean, but verify.

---

## 4. Roadmap — next phases the user approved

User's quote authorizing the phased rollout: *"I think that is all a fantastic idea. Go for it. Please keep in mind my end goal with this is to have animations, yes, have simulations with movement and spatial systems where Billy is in an apartment with all these things, toilet, food, frig, and where he does things and interacts with the world around him and the world interacts with him."*

### v0.7.1 — Intestines (next)
- New components: `SmallIntestineComponent`, `LargeIntestineComponent` — each holds a `NutrientProfile Contents` + a `float CapacityMl` + flow rates.
- DigestionSystem no longer deposits directly into `MetabolismComponent.NutrientStores`. Instead it hands chyme (a `NutrientProfile`) into `SmallIntestineComponent.Contents`.
- New `SmallIntestineSystem` — absorbs macros/vitamins/minerals from its contents over time (into body stores), passes residue (mostly fiber + water) to `LargeIntestineComponent.Contents`.
- New `LargeIntestineSystem` — extracts remaining water into body stores, compacts residue into rectal contents for v0.7.3.
- Pipeline grows from 13 → 15 systems (intestine systems slot between DigestionSystem and RotSystem).
- UI: Expand NUTRIENTS panel to show transit through small/large intestine (like the stomach fill bar).

### v0.7.2 — Bladder and kidneys
- `KidneyComponent` + `KidneySystem` — pulls water from body stores, produces urine at a fill rate based on hydration intake.
- `BladderComponent` — `CurrentVolumeMl`, `MaxVolumeMl` (~400ml). Fills from kidneys.
- New desire type: `DesireType.Pee`. New tag: `NeedsToUrinateTag` at bladder fill > 60%. `UrgentTag` at > 85%.
- `BrainSystem` scores Pee urgency from bladder fill. Can override eat/drink/sleep if urgent.
- `EliminationUrgeSystem` — monitors bladder/rectum, sets urge tags. (Same system handles both kinds of urges.)
- Toilet entity is just a world entity with a `ToiletTag` for now — spatial integration comes later.

### v0.7.3 — Rectum and elimination
- `RectumComponent` — `CurrentVolumeMl`, `MaxVolumeMl` (~250ml), `Contents` (NutrientProfile of waste — mostly fiber/water).
- `DesireType.Poop`, `NeedsToDefecateTag`, `UrgentTag` patterns mirror bladder.
- `EliminationSystem` — when a Pee/Poop desire is dominant AND a toilet entity exists, drains the bladder/rectum. For now "uses the toilet" is an instant action (no pathing yet).
- MoodSystem gains inputs: sustained urgent urges raise Anger/Anticipation; relief after elimination raises Joy briefly.

### v0.8+ — Nutrient deficiencies and toxicities
- MoodSystem + BiologicalConditionSystem read `NutrientStores` over time.
- Low VitaminC sustained → `ScurvyTag` (stub at first, just logs).
- High Sodium sustained → `ThirstyTag` amplifier.
- Build out a `BodyMetabolismSystem` that drains `NutrientStores` at a daily burn rate.

### Post-v0.8 — Spatial/movement layer
- `PositionComponent` (x, y), `VelocityComponent`, `MovementSystem`.
- World entities: `FridgeEntity`, `SinkEntity`, `ToiletEntity`, `BedEntity`.
- Billy must move to interact. InteractionSystem grows a proximity check.
- Animations and rendering come after.

---

## 5. Key file locations

```
APIFramework/
  Components/
    NutrientProfile.cs         ← universal nutrient carrier (v0.7.0)
    MetabolismComponent.cs     ← has NutrientStores (v0.7.0)
    StomachComponent.cs        ← has NutrientsQueued (v0.7.0)
    BolusComponent.cs / LiquidComponent.cs / FoodObjectComponent.cs
    EntityTemplates.cs         ← SpawnHuman / SpawnCat factories
    (plus many Tag structs and other components)
  Systems/
    InvariantSystem.cs         ← has GuardNonNegative for NutrientProfile (v0.7.0)
    DigestionSystem.cs         ← takes DigestionSystemConfig (v0.7.0)
    EsophagusSystem, FeedingSystem, DrinkingSystem, InteractionSystem
    BrainSystem, MoodSystem, EnergySystem, SleepSystem, BiologicalConditionSystem, RotSystem
  Config/
    SimConfig.cs               ← all config classes including new DigestionSystemConfig
  Core/
    SimulationBootstrapper.cs  ← composition root, ApplyConfig hot-reload
    SimVersion.cs              ← version constant (currently 0.7.0)
    SimulationEngine.cs, EntityManager.cs, Entity.cs
SimConfig.json                 ← all tuning values
CHANGELOG.md                   ← detailed version history
ECSCli/CliRenderer.cs          ← ASCII snapshot + balancing report
ECSVisualizer/
  ViewModels/EntityViewModel.cs
  Views/MainWindow.axaml       ← Avalonia GUI with nutrient panel
```

---

## 6. Conventions / gotchas

- **Struct mutation pattern:** `var x = entity.Get<XyzComponent>(); x.Field = ...; entity.Add(x);` — must write back because structs are copied on Get.
- **When adding a new system config:** add class in SimConfig.cs → add property to `SystemsConfig` → add `MergeFlat` call in `SimulationBootstrapper.ApplyConfig` → wire constructor in `RegisterSystems`.
- **Config changes need both:** `SimConfig.cs` defaults AND the `SimConfig.json` section, or hot-reload won't see them.
- **No travelling organ entities.** Organs are always components on the body. Only food/liquid boluses travel as entities.
- **Comments welcome, but no emojis in code files.** User prefers heavy explanatory comments for the learning aspect.
- **Dotnet sandbox:** local sandbox doesn't have dotnet. Run builds locally. Build was NOT verified on v0.7.0 — first order of business in a fresh chat is `dotnet build` in the solution dir and fix anything that surfaces.

---

## 7. Suggested first message for the fresh chat

> I'm continuing work on my ECS Simulation Engine project at `C:\repos\_ecs-simulation-engine`. I just shipped v0.7.0 (nutrient decomposition — full NutrientProfile struct flowing through the digestive pipeline). I've attached a HANDOFF doc (`docs/HANDOFF-v0.7.md`) that catches you up on architecture, conventions, what shipped, and the planned v0.7.1 → v0.7.3 roadmap. Please read that first.
>
> Two immediate things:
> 1. Run `dotnet build` on the solution and fix any compile errors (v0.7.0 was grep-verified but not build-verified).
> 2. Then let's start v0.7.1 — intestines. Following the plan in the handoff doc: add `SmallIntestineComponent` and `LargeIntestineComponent` on the body, new `SmallIntestineSystem` and `LargeIntestineSystem`, and rework `DigestionSystem` to hand chyme to the small intestine instead of depositing straight into `NutrientStores`.
>
> Keep in mind my end goal: Billy in an apartment with spatial movement, interacting with toilet/fridge/sink/bed entities. Every architectural choice should leave room for that.

---

End of handoff.
