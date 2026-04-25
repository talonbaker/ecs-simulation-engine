# ECS Simulation Engine — Fact Sheet

**SimVersion:** ECS Simulation Engine  v0.7.2
**Generated:** 2026-04-25T03:33:18.6472302+00:00
**TelemetrySchema:** world-state.schema.json v0.1.0

## Registered Systems

| # | System | Phase | PhaseOrder |
|:--|:-------|:------|:-----------|
| 1 | `InvariantSystem` | `PreUpdate` | 0 |
| 2 | `MetabolismSystem` | `Physiology` | 10 |
| 3 | `EnergySystem` | `Physiology` | 10 |
| 4 | `BladderFillSystem` | `Physiology` | 10 |
| 5 | `BiologicalConditionSystem` | `Condition` | 20 |
| 6 | `MoodSystem` | `Cognition` | 30 |
| 7 | `BrainSystem` | `Cognition` | 30 |
| 8 | `FeedingSystem` | `Behavior` | 40 |
| 9 | `DrinkingSystem` | `Behavior` | 40 |
| 10 | `SleepSystem` | `Behavior` | 40 |
| 11 | `DefecationSystem` | `Behavior` | 40 |
| 12 | `UrinationSystem` | `Behavior` | 40 |
| 13 | `InteractionSystem` | `Transit` | 50 |
| 14 | `EsophagusSystem` | `Transit` | 50 |
| 15 | `DigestionSystem` | `Transit` | 50 |
| 16 | `SmallIntestineSystem` | `Elimination` | 55 |
| 17 | `LargeIntestineSystem` | `Elimination` | 55 |
| 18 | `ColonSystem` | `Elimination` | 55 |
| 19 | `BladderSystem` | `Elimination` | 55 |
| 20 | `RotSystem` | `World` | 60 |
| 21 | `MovementSystem` | `World` | 60 |

**Total:** 21 systems

## Component Types

All `struct` types from the `APIFramework.Components` namespace.

| Component | Fields |
|:----------|:-------|
| `AcceptingTag` | *(tag — no fields)* |
| `AdmiringTag` | *(tag — no fields)* |
| `AmazedTag` | *(tag — no fields)* |
| `AngryTag` | *(tag — no fields)* |
| `AnnoyedTag` | *(tag — no fields)* |
| `AnticipatingTag` | *(tag — no fields)* |
| `ApprehensiveTag` | *(tag — no fields)* |
| `BedComponent` | *(tag — no fields)* |
| `BladderComponent` | `VolumeML: float`, `FillRate: float`, `UrgeThresholdMl: float`, `CapacityMl: float`, `Fill: float`, `HasUrge: bool`, `IsCritical: bool`, `IsEmpty: bool` |
| `BladderCriticalTag` | *(tag — no fields)* |
| `BolusComponent` | `Volume: float`, `Nutrients: NutrientProfile`, `Toughness: float`, `FoodType: string` |
| `BolusTag` | *(tag — no fields)* |
| `BoredTag` | *(tag — no fields)* |
| `BowelCriticalTag` | *(tag — no fields)* |
| `CatTag` | *(tag — no fields)* |
| `ColonComponent` | `UrgeThresholdMl: float`, `CapacityMl: float`, `StoolVolumeMl: float`, `Fill: float`, `HasUrge: bool`, `IsCritical: bool`, `IsEmpty: bool` |
| `ConsumedRottenFoodTag` | *(tag — no fields)* |
| `ContainerComponent` | `Contents: List`1`, `Count: int`, `IsEmpty: bool` |
| `DefecationUrgeTag` | *(tag — no fields)* |
| `DehydratedTag` | *(tag — no fields)* |
| `DisgustTag` | *(tag — no fields)* |
| `DistractedTag` | *(tag — no fields)* |
| `DriveComponent` | `EatUrgency: float`, `DrinkUrgency: float`, `SleepUrgency: float`, `DefecateUrgency: float`, `PeeUrgency: float`, `Dominant: DesireType` |
| `EcstaticTag` | *(tag — no fields)* |
| `EnergyComponent` | `Energy: float`, `Sleepiness: float`, `IsSleeping: bool`, `EnergyDrainRate: float`, `SleepinessGainRate: float`, `EnergyRestoreRate: float`, `SleepinessDrainRate: float`, `Tiredness: float` |
| `EsophagusTransitComponent` | `Progress: float`, `Speed: float`, `TargetEntityId: Guid`, `Position: int` |
| `ExhaustedTag` | *(tag — no fields)* |
| `FearfulTag` | *(tag — no fields)* |
| `FoodDesireTag` | *(tag — no fields)* |
| `FoodObjectComponent` | `Name: string`, `NutrientsPerBite: NutrientProfile`, `BitesRemaining: int`, `Toughness: float` |
| `FridgeComponent` | `FoodCount: int` |
| `GriefTag` | *(tag — no fields)* |
| `HumanTag` | *(tag — no fields)* |
| `HungerTag` | *(tag — no fields)* |
| `HungryTag` | *(tag — no fields)* |
| `IdentityComponent` | `Name: string`, `Value: string` |
| `InterestedTag` | *(tag — no fields)* |
| `IrritableTag` | *(tag — no fields)* |
| `JoyfulTag` | *(tag — no fields)* |
| `LargeIntestineComponent` | `ContentVolumeMl: float`, `WaterReabsorptionRate: float`, `MobilityRate: float`, `StoolFraction: float`, `Fill: float`, `IsEmpty: bool` |
| `LiquidComponent` | `VolumeMl: float`, `Nutrients: NutrientProfile`, `LiquidType: string` |
| `LoathingTag` | *(tag — no fields)* |
| `MetabolismComponent` | `Satiation: float`, `Hydration: float`, `BodyTemp: float`, `Energy: float`, `NutrientStores: NutrientProfile`, `SatiationDrainRate: float`, `HydrationDrainRate: float`, `SleepMetabolismMultiplier: float`, `Hunger: float`, `Thirst: float` |
| `MoodComponent` | `Joy: float`, `Trust: float`, `Fear: float`, `Surprise: float`, `Sadness: float`, `Disgust: float`, `Anger: float`, `Anticipation: float`, `HasAnyEmotion: bool`, `Valence: float` |
| `MovementComponent` | `Speed: float`, `ArrivalDistance: float` |
| `MovementTargetComponent` | `TargetEntityId: Guid`, `Label: string` |
| `NutrientProfile` | `Carbohydrates: float`, `Proteins: float`, `Fats: float`, `Fiber: float`, `Water: float`, `VitaminA: float`, `VitaminB: float`, `VitaminC: float`, `VitaminD: float`, `VitaminE: float`, `VitaminK: float`, `Sodium: float`, `Potassium: float`, `Calcium: float`, `Iron: float`, `Magnesium: float`, `Calories: float`, `IsEmpty: bool` |
| `PensiveTag` | *(tag — no fields)* |
| `PositionComponent` | `X: float`, `Y: float`, `Z: float` |
| `RagingTag` | *(tag — no fields)* |
| `RefrigeratorComponent` | `BananaCount: int` |
| `RotComponent` | `AgeSeconds: float`, `RotLevel: float`, `RotStartAge: float`, `RotRate: float`, `IsDecaying: bool`, `Freshness: float` |
| `RotTag` | *(tag — no fields)* |
| `SadTag` | *(tag — no fields)* |
| `SereneTag` | *(tag — no fields)* |
| `SinkComponent` | *(tag — no fields)* |
| `SleepingTag` | *(tag — no fields)* |
| `SmallIntestineComponent` | `ChymeVolumeMl: float`, `AbsorptionRate: float`, `Chyme: NutrientProfile`, `ResidueToLargeFraction: float`, `Fill: float`, `IsEmpty: bool` |
| `StarvingTag` | *(tag — no fields)* |
| `StomachComponent` | `CurrentVolumeMl: float`, `DigestionRate: float`, `NutrientsQueued: NutrientProfile`, `Fill: float`, `IsEmpty: bool`, `IsFull: bool` |
| `StoredTag` | *(tag — no fields)* |
| `SurprisedTag` | *(tag — no fields)* |
| `TerrorTag` | *(tag — no fields)* |
| `ThirstTag` | *(tag — no fields)* |
| `ThirstyTag` | *(tag — no fields)* |
| `TiredTag` | *(tag — no fields)* |
| `ToiletComponent` | *(tag — no fields)* |
| `TrustingTag` | *(tag — no fields)* |
| `UrinationUrgeTag` | *(tag — no fields)* |
| `VigilantTag` | *(tag — no fields)* |
| `WaterDesireTag` | *(tag — no fields)* |

**Total:** 71 component types

## SimConfig Keys

| Key Path | Type | Current Value |
|:---------|:-----|:--------------|
| `World.DefaultTimeScale` | `float` | `120` |
| `Entities.Human.Metabolism.SatiationStart` | `float` | `90` |
| `Entities.Human.Metabolism.HydrationStart` | `float` | `90` |
| `Entities.Human.Metabolism.BodyTemp` | `float` | `37` |
| `Entities.Human.Metabolism.SatiationDrainRate` | `float` | `0.002` |
| `Entities.Human.Metabolism.HydrationDrainRate` | `float` | `0.004` |
| `Entities.Human.Metabolism.SleepMetabolismMultiplier` | `float` | `0.1` |
| `Entities.Human.Stomach.DigestionRate` | `float` | `0.017` |
| `Entities.Human.Energy.EnergyStart` | `float` | `90` |
| `Entities.Human.Energy.SleepinessStart` | `float` | `5` |
| `Entities.Human.Energy.EnergyDrainRate` | `float` | `0.001` |
| `Entities.Human.Energy.SleepinessGainRate` | `float` | `0.0012` |
| `Entities.Human.Energy.EnergyRestoreRate` | `float` | `0.003` |
| `Entities.Human.Energy.SleepinessDrainRate` | `float` | `0.002` |
| `Entities.Human.Mood.JoyStart` | `float` | `0` |
| `Entities.Human.Mood.TrustStart` | `float` | `0` |
| `Entities.Human.Mood.FearStart` | `float` | `0` |
| `Entities.Human.Mood.SurpriseStart` | `float` | `0` |
| `Entities.Human.Mood.SadnessStart` | `float` | `0` |
| `Entities.Human.Mood.DisgustStart` | `float` | `0` |
| `Entities.Human.Mood.AngerStart` | `float` | `0` |
| `Entities.Human.Mood.AnticipationStart` | `float` | `0` |
| `Entities.Human.SmallIntestine.AbsorptionRate` | `float` | `0.008` |
| `Entities.Human.SmallIntestine.ResidueToLargeFraction` | `float` | `0.4` |
| `Entities.Human.LargeIntestine.WaterReabsorptionRate` | `float` | `0.001` |
| `Entities.Human.LargeIntestine.MobilityRate` | `float` | `0.003` |
| `Entities.Human.LargeIntestine.StoolFraction` | `float` | `0.6` |
| `Entities.Human.Colon.UrgeThresholdMl` | `float` | `100` |
| `Entities.Human.Colon.CapacityMl` | `float` | `200` |
| `Entities.Human.Bladder.FillRate` | `float` | `0.01` |
| `Entities.Human.Bladder.UrgeThresholdMl` | `float` | `70` |
| `Entities.Human.Bladder.CapacityMl` | `float` | `100` |
| `Entities.Cat.Metabolism.SatiationStart` | `float` | `100` |
| `Entities.Cat.Metabolism.HydrationStart` | `float` | `100` |
| `Entities.Cat.Metabolism.BodyTemp` | `float` | `38.5` |
| `Entities.Cat.Metabolism.SatiationDrainRate` | `float` | `0.004` |
| `Entities.Cat.Metabolism.HydrationDrainRate` | `float` | `0.006` |
| `Entities.Cat.Metabolism.SleepMetabolismMultiplier` | `float` | `0.05` |
| `Entities.Cat.Stomach.DigestionRate` | `float` | `0.01` |
| `Entities.Cat.Energy.EnergyStart` | `float` | `75` |
| `Entities.Cat.Energy.SleepinessStart` | `float` | `30` |
| `Entities.Cat.Energy.EnergyDrainRate` | `float` | `0.0006` |
| `Entities.Cat.Energy.SleepinessGainRate` | `float` | `0.0008` |
| `Entities.Cat.Energy.EnergyRestoreRate` | `float` | `0.003` |
| `Entities.Cat.Energy.SleepinessDrainRate` | `float` | `0.004` |
| `Entities.Cat.Mood.JoyStart` | `float` | `0` |
| `Entities.Cat.Mood.TrustStart` | `float` | `0` |
| `Entities.Cat.Mood.FearStart` | `float` | `0` |
| `Entities.Cat.Mood.SurpriseStart` | `float` | `0` |
| `Entities.Cat.Mood.SadnessStart` | `float` | `0` |
| `Entities.Cat.Mood.DisgustStart` | `float` | `0` |
| `Entities.Cat.Mood.AngerStart` | `float` | `0` |
| `Entities.Cat.Mood.AnticipationStart` | `float` | `0` |
| `Entities.Cat.SmallIntestine.AbsorptionRate` | `float` | `0.01` |
| `Entities.Cat.SmallIntestine.ResidueToLargeFraction` | `float` | `0.35` |
| `Entities.Cat.LargeIntestine.WaterReabsorptionRate` | `float` | `0.0008` |
| `Entities.Cat.LargeIntestine.MobilityRate` | `float` | `0.004` |
| `Entities.Cat.LargeIntestine.StoolFraction` | `float` | `0.55` |
| `Entities.Cat.Colon.UrgeThresholdMl` | `float` | `80` |
| `Entities.Cat.Colon.CapacityMl` | `float` | `160` |
| `Entities.Cat.Bladder.FillRate` | `float` | `0.004` |
| `Entities.Cat.Bladder.UrgeThresholdMl` | `float` | `40` |
| `Entities.Cat.Bladder.CapacityMl` | `float` | `60` |
| `Systems.BiologicalCondition.ThirstTagThreshold` | `float` | `30` |
| `Systems.BiologicalCondition.DehydratedTagThreshold` | `float` | `70` |
| `Systems.BiologicalCondition.HungerTagThreshold` | `float` | `30` |
| `Systems.BiologicalCondition.StarvingTagThreshold` | `float` | `80` |
| `Systems.BiologicalCondition.IrritableThreshold` | `float` | `60` |
| `Systems.Energy.TiredThreshold` | `float` | `60` |
| `Systems.Energy.ExhaustedThreshold` | `float` | `25` |
| `Systems.Brain.EatMaxScore` | `float` | `1` |
| `Systems.Brain.DrinkMaxScore` | `float` | `1` |
| `Systems.Brain.SleepMaxScore` | `float` | `0.9` |
| `Systems.Brain.BoredUrgencyBonus` | `float` | `0.04` |
| `Systems.Brain.MinUrgencyThreshold` | `float` | `0.05` |
| `Systems.Brain.SadnessUrgencyMult` | `float` | `0.8` |
| `Systems.Brain.GriefUrgencyMult` | `float` | `0.5` |
| `Systems.Brain.DefecateMaxScore` | `float` | `0.85` |
| `Systems.Brain.PeeMaxScore` | `float` | `0.8` |
| `Systems.Feeding.HungerThreshold` | `float` | `40` |
| `Systems.Feeding.NutritionQueueCap` | `float` | `240` |
| `Systems.Feeding.Banana.VolumeMl` | `float` | `50` |
| `Systems.Feeding.Banana.Nutrients` | `NutrientProfile` | `117 kcal  C:27.0g P:1.3g F:0.4g  W:89ml` |
| `Systems.Feeding.Banana.Toughness` | `float` | `0.2` |
| `Systems.Feeding.Banana.EsophagusSpeed` | `float` | `0.3` |
| `Systems.Feeding.FoodFreshnessSeconds` | `float` | `86400` |
| `Systems.Feeding.FoodRotRate` | `float` | `0.001` |
| `Systems.Drinking.HydrationQueueCap` | `float` | `15` |
| `Systems.Drinking.HydrationQueueCapDehydrated` | `float` | `30` |
| `Systems.Drinking.Water.VolumeMl` | `float` | `15` |
| `Systems.Drinking.Water.Nutrients` | `NutrientProfile` | `0 kcal  C:0.0g P:0.0g F:0.0g  W:15ml` |
| `Systems.Drinking.Water.EsophagusSpeed` | `float` | `0.8` |
| `Systems.Digestion.SatiationPerCalorie` | `float` | `0.3` |
| `Systems.Digestion.HydrationPerMl` | `float` | `2` |
| `Systems.Digestion.ResidueFraction` | `float` | `0.2` |
| `Systems.Sleep.WakeThreshold` | `float` | `15` |
| `Systems.Interaction.BiteVolumeMl` | `float` | `50` |
| `Systems.Interaction.EsophagusSpeed` | `float` | `0.3` |
| `Systems.Mood.LowThreshold` | `float` | `10` |
| `Systems.Mood.MidThreshold` | `float` | `34` |
| `Systems.Mood.HighThreshold` | `float` | `67` |
| `Systems.Mood.PositiveDecayRate` | `float` | `0.005` |
| `Systems.Mood.NegativeDecayRate` | `float` | `0.003` |
| `Systems.Mood.SurpriseDecayRate` | `float` | `0.05` |
| `Systems.Mood.JoyGainRate` | `float` | `0.01` |
| `Systems.Mood.JoyComfortThreshold` | `float` | `60` |
| `Systems.Mood.AngerGainRate` | `float` | `0.015` |
| `Systems.Mood.SadnessGainRate` | `float` | `0.008` |
| `Systems.Mood.BoredGainRate` | `float` | `0.005` |
| `Systems.Mood.RottenFoodDisgustSpike` | `float` | `40` |
| `Systems.Mood.AnticipationGainRate` | `float` | `0.006` |
| `Systems.Mood.AnticipationHungerMin` | `float` | `15` |
| `Systems.Mood.AnticipationHungerMax` | `float` | `50` |
| `Systems.Rot.RotTagThreshold` | `float` | `30` |

