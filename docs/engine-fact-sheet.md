# ECS Simulation Engine — Fact Sheet

**SimVersion:** ECS Simulation Engine  v0.7.2
**Generated:** 2026-04-27T02:16:19.0789554+00:00
**TelemetrySchema:** world-state.schema.json v0.1.0

## Registered Systems

| # | System | Phase | PhaseOrder |
|:--|:-------|:------|:-----------|
| 1 | `SpatialIndexSyncSystem` | `Spatial` | 5 |
| 2 | `RoomMembershipSystem` | `Spatial` | 5 |
| 3 | `SunSystem` | `Lighting` | 7 |
| 4 | `LightSourceStateSystem` | `Lighting` | 7 |
| 5 | `ApertureBeamSystem` | `Lighting` | 7 |
| 6 | `IlluminationAccumulationSystem` | `Lighting` | 7 |
| 7 | `ProximityEventSystem` | `Lighting` | 7 |
| 8 | `LightingToDriveCouplingSystem` | `Coupling` | 8 |
| 9 | `InvariantSystem` | `PreUpdate` | 0 |
| 10 | `ScheduleSpawnerSystem` | `PreUpdate` | 0 |
| 11 | `StressInitializerSystem` | `PreUpdate` | 0 |
| 12 | `MaskInitializerSystem` | `PreUpdate` | 0 |
| 13 | `WorkloadInitializerSystem` | `PreUpdate` | 0 |
| 14 | `TaskGeneratorSystem` | `PreUpdate` | 0 |
| 15 | `MetabolismSystem` | `Physiology` | 10 |
| 16 | `EnergySystem` | `Physiology` | 10 |
| 17 | `BladderFillSystem` | `Physiology` | 10 |
| 18 | `BiologicalConditionSystem` | `Condition` | 20 |
| 19 | `ScheduleSystem` | `Condition` | 20 |
| 20 | `MoodSystem` | `Cognition` | 30 |
| 21 | `BrainSystem` | `Cognition` | 30 |
| 22 | `PhysiologyGateSystem` | `Cognition` | 30 |
| 23 | `DriveDynamicsSystem` | `Cognition` | 30 |
| 24 | `ActionSelectionSystem` | `Cognition` | 30 |
| 25 | `WillpowerSystem` | `Cognition` | 30 |
| 26 | `RelationshipLifecycleSystem` | `Cognition` | 30 |
| 27 | `SocialMaskSystem` | `Cognition` | 30 |
| 28 | `FeedingSystem` | `Behavior` | 40 |
| 29 | `DrinkingSystem` | `Behavior` | 40 |
| 30 | `SleepSystem` | `Behavior` | 40 |
| 31 | `DefecationSystem` | `Behavior` | 40 |
| 32 | `UrinationSystem` | `Behavior` | 40 |
| 33 | `InteractionSystem` | `Transit` | 50 |
| 34 | `EsophagusSystem` | `Transit` | 50 |
| 35 | `DigestionSystem` | `Transit` | 50 |
| 36 | `SmallIntestineSystem` | `Elimination` | 55 |
| 37 | `LargeIntestineSystem` | `Elimination` | 55 |
| 38 | `ColonSystem` | `Elimination` | 55 |
| 39 | `BladderSystem` | `Elimination` | 55 |
| 40 | `RotSystem` | `World` | 60 |
| 41 | `PathfindingTriggerSystem` | `World` | 60 |
| 42 | `MovementSpeedModifierSystem` | `World` | 60 |
| 43 | `StepAsideSystem` | `World` | 60 |
| 44 | `MovementSystem` | `World` | 60 |
| 45 | `FacingSystem` | `World` | 60 |
| 46 | `IdleMovementSystem` | `World` | 60 |
| 47 | `NarrativeEventDetector` | `Narrative` | 70 |
| 48 | `PersistenceThresholdDetector` | `Narrative` | 70 |
| 49 | `MemoryRecordingSystem` | `Narrative` | 70 |
| 50 | `DialogContextDecisionSystem` | `Dialog` | 75 |
| 51 | `DialogFragmentRetrievalSystem` | `Dialog` | 75 |
| 52 | `DialogCalcifySystem` | `Dialog` | 75 |
| 53 | `StressSystem` | `Cleanup` | 80 |
| 54 | `WorkloadSystem` | `Cleanup` | 80 |
| 55 | `MaskCrackSystem` | `Cleanup` | 80 |

**Total:** 55 systems

## Component Types

All `struct` types from the `APIFramework.Components` namespace.

| Component | Fields |
|:----------|:-------|
| `AcceptingTag` | *(tag — no fields)* |
| `AdmiringTag` | *(tag — no fields)* |
| `AmazedTag` | *(tag — no fields)* |
| `AnchorObjectComponent` | `Id: string`, `RoomId: string`, `Description: string`, `PhysicalState: AnchorObjectPhysicalState` |
| `AnchorObjectTag` | *(tag — no fields)* |
| `AngryTag` | *(tag — no fields)* |
| `AnnoyedTag` | *(tag — no fields)* |
| `AnticipatingTag` | *(tag — no fields)* |
| `ApprehensiveTag` | *(tag — no fields)* |
| `BedComponent` | *(tag — no fields)* |
| `BladderComponent` | `VolumeML: float`, `FillRate: float`, `UrgeThresholdMl: float`, `CapacityMl: float`, `Fill: float`, `HasUrge: bool`, `IsCritical: bool`, `IsEmpty: bool` |
| `BladderCriticalTag` | *(tag — no fields)* |
| `BlockedActionsComponent` | `Blocked: IReadOnlyCollection`1` |
| `BolusComponent` | `Volume: float`, `Nutrients: NutrientProfile`, `Toughness: float`, `FoodType: string` |
| `BolusTag` | *(tag — no fields)* |
| `BoredTag` | *(tag — no fields)* |
| `BoundsRect` | `X: int`, `Y: int`, `Width: int`, `Height: int`, `Area: int` |
| `BowelCriticalTag` | *(tag — no fields)* |
| `BrokenItemComponent` | `OriginalKind: string`, `Breakage: BreakageKind`, `CreatedAtTick: Int64`, `ChronicleEntryId: string` |
| `BrokenItemTag` | *(tag — no fields)* |
| `BurnedOutFromWorkloadTag` | *(tag — no fields)* |
| `BurningOutTag` | *(tag — no fields)* |
| `CatTag` | *(tag — no fields)* |
| `ColonComponent` | `UrgeThresholdMl: float`, `CapacityMl: float`, `StoolVolumeMl: float`, `Fill: float`, `HasUrge: bool`, `IsCritical: bool`, `IsEmpty: bool` |
| `ConsumedRottenFoodTag` | *(tag — no fields)* |
| `ContainerComponent` | `Contents: List`1`, `Count: int`, `IsEmpty: bool` |
| `CurrentScheduleBlockComponent` | `ActiveBlockIndex: int`, `AnchorEntityId: Guid`, `Activity: ScheduleActivityKind` |
| `DefecationUrgeTag` | *(tag — no fields)* |
| `DehydratedTag` | *(tag — no fields)* |
| `DialogHistoryComponent` | `UsesByFragmentId: Dictionary`2`, `UsesByListenerAndFragmentId: Dictionary`2` |
| `DisgustTag` | *(tag — no fields)* |
| `DistractedTag` | *(tag — no fields)* |
| `DriveComponent` | `EatUrgency: float`, `DrinkUrgency: float`, `SleepUrgency: float`, `DefecateUrgency: float`, `PeeUrgency: float`, `Dominant: DesireType` |
| `DriveValue` | `Current: int`, `Baseline: int` |
| `EcstaticTag` | *(tag — no fields)* |
| `EnergyComponent` | `Energy: float`, `Sleepiness: float`, `IsSleeping: bool`, `EnergyDrainRate: float`, `SleepinessGainRate: float`, `EnergyRestoreRate: float`, `SleepinessDrainRate: float`, `Tiredness: float` |
| `EsophagusTransitComponent` | `Progress: float`, `Speed: float`, `TargetEntityId: Guid`, `Position: int` |
| `ExhaustedTag` | *(tag — no fields)* |
| `FacingComponent` | `DirectionDeg: float`, `Source: FacingSource` |
| `FearfulTag` | *(tag — no fields)* |
| `FoodDesireTag` | *(tag — no fields)* |
| `FoodObjectComponent` | `Name: string`, `NutrientsPerBite: NutrientProfile`, `BitesRemaining: int`, `Toughness: float` |
| `FridgeComponent` | `FoodCount: int` |
| `GriefTag` | *(tag — no fields)* |
| `HandednessComponent` | `Side: HandednessSide` |
| `HumanTag` | *(tag — no fields)* |
| `HungerTag` | *(tag — no fields)* |
| `HungryTag` | *(tag — no fields)* |
| `IdentityComponent` | `Name: string`, `Value: string` |
| `Inhibition` | `Class: InhibitionClass`, `Strength: int`, `Awareness: InhibitionAwareness` |
| `InhibitionsComponent` | `Inhibitions: IReadOnlyList`1` |
| `IntendedActionComponent` | `Kind: IntendedActionKind`, `TargetEntityId: int`, `Context: DialogContextValue`, `IntensityHint: int` |
| `InterestedTag` | *(tag — no fields)* |
| `IrritableTag` | *(tag — no fields)* |
| `JoyfulTag` | *(tag — no fields)* |
| `LargeIntestineComponent` | `ContentVolumeMl: float`, `WaterReabsorptionRate: float`, `MobilityRate: float`, `StoolFraction: float`, `Fill: float`, `IsEmpty: bool` |
| `LightApertureComponent` | `Id: string`, `TileX: int`, `TileY: int`, `RoomId: string`, `Facing: ApertureFacing`, `AreaSqTiles: double` |
| `LightApertureTag` | *(tag — no fields)* |
| `LightSourceComponent` | `Id: string`, `Kind: LightKind`, `State: LightState`, `Intensity: int`, `ColorTemperatureK: int`, `TileX: int`, `TileY: int`, `RoomId: string` |
| `LightSourceTag` | *(tag — no fields)* |
| `LiquidComponent` | `VolumeMl: float`, `Nutrients: NutrientProfile`, `LiquidType: string` |
| `LoathingTag` | *(tag — no fields)* |
| `MemoryEntry` | `Id: string`, `Tick: Int64`, `Kind: NarrativeEventKind`, `ParticipantIds: IReadOnlyList`1`, `RoomId: string`, `Detail: string`, `Persistent: bool` |
| `MetabolismComponent` | `Satiation: float`, `Hydration: float`, `BodyTemp: float`, `Energy: float`, `NutrientStores: NutrientProfile`, `SatiationDrainRate: float`, `HydrationDrainRate: float`, `SleepMetabolismMultiplier: float`, `Hunger: float`, `Thirst: float` |
| `MoodComponent` | `Joy: float`, `Trust: float`, `Fear: float`, `Surprise: float`, `Sadness: float`, `Disgust: float`, `Anger: float`, `Anticipation: float`, `HasAnyEmotion: bool`, `Valence: float` |
| `MovementComponent` | `Speed: float`, `ArrivalDistance: float`, `SpeedModifier: float`, `LastVelocityX: float`, `LastVelocityZ: float` |
| `MovementTargetComponent` | `TargetEntityId: Guid`, `Label: string` |
| `NamedAnchorComponent` | `Tag: string`, `Description: string`, `SmellTag: string` |
| `NoteComponent` | `Notes: IReadOnlyList`1` |
| `NpcArchetypeComponent` | `ArchetypeId: string` |
| `NpcDealComponent` | `Deal: string` |
| `NpcSlotComponent` | `X: int`, `Y: int`, `ArchetypeHint: string`, `RoomId: string` |
| `NpcSlotTag` | *(tag — no fields)* |
| `NpcTag` | *(tag — no fields)* |
| `NutrientProfile` | `Carbohydrates: float`, `Proteins: float`, `Fats: float`, `Fiber: float`, `Water: float`, `VitaminA: float`, `VitaminB: float`, `VitaminC: float`, `VitaminD: float`, `VitaminE: float`, `VitaminK: float`, `Sodium: float`, `Potassium: float`, `Calcium: float`, `Iron: float`, `Magnesium: float`, `Calories: float`, `IsEmpty: bool` |
| `ObstacleTag` | *(tag — no fields)* |
| `OverdueTag` | *(tag — no fields)* |
| `OverwhelmedTag` | *(tag — no fields)* |
| `PathComponent` | `Waypoints: IReadOnlyList`1`, `CurrentWaypointIndex: int` |
| `PensiveTag` | *(tag — no fields)* |
| `PersonalityComponent` | `Openness: int`, `Conscientiousness: int`, `Extraversion: int`, `Agreeableness: int`, `Neuroticism: int`, `VocabularyRegister: VocabularyRegister`, `CurrentMood: string` |
| `PersonalMemoryComponent` | `Recent: IReadOnlyList`1` |
| `PositionComponent` | `X: float`, `Y: float`, `Z: float` |
| `ProximityComponent` | `ConversationRangeTiles: int`, `AwarenessRangeTiles: int`, `SightRangeTiles: int` |
| `RagingTag` | *(tag — no fields)* |
| `RecognizedTicComponent` | `RecognizedTicsBySpeakerId: Dictionary`2`, `HearingCounts: Dictionary`2` |
| `RefrigeratorComponent` | `BananaCount: int` |
| `RelationshipComponent` | `ParticipantA: int`, `ParticipantB: int`, `Intensity: int`, `Patterns: IReadOnlyList`1` |
| `RelationshipMemoryComponent` | `Recent: IReadOnlyList`1` |
| `RelationshipTag` | *(tag — no fields)* |
| `RoomComponent` | `Id: string`, `Name: string`, `Category: RoomCategory`, `Floor: BuildingFloor`, `Bounds: BoundsRect`, `Illumination: RoomIllumination` |
| `RoomIllumination` | `AmbientLevel: int`, `ColorTemperatureK: int`, `DominantSourceId: string` |
| `RoomTag` | *(tag — no fields)* |
| `RotComponent` | `AgeSeconds: float`, `RotLevel: float`, `RotStartAge: float`, `RotRate: float`, `IsDecaying: bool`, `Freshness: float` |
| `RotTag` | *(tag — no fields)* |
| `SadTag` | *(tag — no fields)* |
| `ScheduleBlock` | `StartHour: float`, `EndHour: float`, `AnchorId: string`, `Activity: ScheduleActivityKind` |
| `ScheduleComponent` | `Blocks: IReadOnlyList`1` |
| `SereneTag` | *(tag — no fields)* |
| `SilhouetteComponent` | `Height: string`, `Build: string`, `Hair: string`, `Headwear: string`, `DominantColor: string`, `DistinctiveItem: string` |
| `SinkComponent` | *(tag — no fields)* |
| `SleepingTag` | *(tag — no fields)* |
| `SmallIntestineComponent` | `ChymeVolumeMl: float`, `AbsorptionRate: float`, `Chyme: NutrientProfile`, `ResidueToLargeFraction: float`, `Fill: float`, `IsEmpty: bool` |
| `SocialDrivesComponent` | `Belonging: DriveValue`, `Status: DriveValue`, `Affection: DriveValue`, `Irritation: DriveValue`, `Attraction: DriveValue`, `Trust: DriveValue`, `Suspicion: DriveValue`, `Loneliness: DriveValue` |
| `SocialMaskComponent` | `IrritationMask: int`, `AffectionMask: int`, `AttractionMask: int`, `LonelinessMask: int`, `CurrentLoad: int`, `Baseline: int`, `LastSlipTick: Int64` |
| `StainComponent` | `Source: string`, `Magnitude: int`, `CreatedAtTick: Int64`, `ChronicleEntryId: string` |
| `StainTag` | *(tag — no fields)* |
| `StarvingTag` | *(tag — no fields)* |
| `StomachComponent` | `CurrentVolumeMl: float`, `DigestionRate: float`, `NutrientsQueued: NutrientProfile`, `Fill: float`, `IsEmpty: bool`, `IsFull: bool` |
| `StoredTag` | *(tag — no fields)* |
| `StressComponent` | `AcuteLevel: int`, `ChronicLevel: double`, `LastDayUpdated: int`, `SuppressionEventsToday: int`, `DriveSpikeEventsToday: int`, `SocialConflictEventsToday: int`, `OverdueTaskEventsToday: int`, `BurnoutLastAppliedDay: int` |
| `StressedTag` | *(tag — no fields)* |
| `SunStateRecord` | `AzimuthDeg: double`, `ElevationDeg: double`, `DayPhase: DayPhase` |
| `SurprisedTag` | *(tag — no fields)* |
| `TaskComponent` | `EffortHours: float`, `DeadlineTick: Int64`, `Priority: int`, `Progress: float`, `QualityLevel: float`, `AssignedNpcId: Guid`, `CreatedTick: Int64` |
| `TaskTag` | *(tag — no fields)* |
| `TerrorTag` | *(tag — no fields)* |
| `ThirstTag` | *(tag — no fields)* |
| `ThirstyTag` | *(tag — no fields)* |
| `TiredTag` | *(tag — no fields)* |
| `ToiletComponent` | *(tag — no fields)* |
| `TrustingTag` | *(tag — no fields)* |
| `UrinationUrgeTag` | *(tag — no fields)* |
| `VigilantTag` | *(tag — no fields)* |
| `WaterDesireTag` | *(tag — no fields)* |
| `WillpowerComponent` | `Current: int`, `Baseline: int` |
| `WorkloadComponent` | `ActiveTasks: IReadOnlyList`1`, `Capacity: int`, `CurrentLoad: int` |

**Total:** 127 component types

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
| `Social.DriveDecayPerTick` | `double` | `0.15` |
| `Social.DriveCircadianAmplitudes` | `Dictionary` | `{belonging=3, status=2.5, affection=3, irritation=4, attraction=2, trust=1.5, suspicion=2, loneliness=5}` |
| `Social.DriveCircadianPhases` | `Dictionary` | `{belonging=0.4, status=0.3, affection=0.65, irritation=0.55, attraction=0.7, trust=0.45, suspicion=0.8, loneliness=0.85}` |
| `Social.DriveVolatilityScale` | `double` | `1` |
| `Social.WillpowerSleepRegenPerTick` | `int` | `1` |
| `Social.RelationshipIntensityDecayPerTick` | `double` | `0.05` |
| `Spatial.CellSizeTiles` | `int` | `4` |
| `Spatial.WorldSize.Width` | `int` | `512` |
| `Spatial.WorldSize.Height` | `int` | `512` |
| `Spatial.ProximityRangeDefaults.ConversationTiles` | `int` | `2` |
| `Spatial.ProximityRangeDefaults.AwarenessTiles` | `int` | `8` |
| `Spatial.ProximityRangeDefaults.SightTiles` | `int` | `32` |
| `Lighting.DayPhaseBoundaries.EarlyMorningStart` | `double` | `0.2` |
| `Lighting.DayPhaseBoundaries.MidMorningStart` | `double` | `0.3` |
| `Lighting.DayPhaseBoundaries.AfternoonStart` | `double` | `0.45` |
| `Lighting.DayPhaseBoundaries.EveningStart` | `double` | `0.65` |
| `Lighting.DayPhaseBoundaries.DuskStart` | `double` | `0.8` |
| `Lighting.DayPhaseBoundaries.NightStart` | `double` | `0.85` |
| `Lighting.FlickerOnProb` | `double` | `0.7` |
| `Lighting.DyingDecayProb` | `double` | `0.05` |
| `Lighting.ApertureRangeBase` | `int` | `5` |
| `Lighting.SourceRangeBase` | `int` | `3` |
| `Lighting.DriveCouplings` | `List` | `[6 entries]` |
| `Movement.StepAsideRadius` | `float` | `3` |
| `Movement.StepAsideShift` | `float` | `0.4` |
| `Movement.IdleJitterTiles` | `float` | `0.05` |
| `Movement.IdlePostureShiftProb` | `float` | `0.005` |
| `Movement.SpeedModifier.IrritationGainPerPoint` | `float` | `0.005` |
| `Movement.SpeedModifier.AffectionLossPerPoint` | `float` | `0.0033` |
| `Movement.SpeedModifier.LowEnergyLossPerPoint` | `float` | `0.005` |
| `Movement.SpeedModifier.MinMultiplier` | `float` | `0.3` |
| `Movement.SpeedModifier.MaxMultiplier` | `float` | `2` |
| `Movement.Pathfinding.DoorwayDiscount` | `float` | `1` |
| `Movement.Pathfinding.TieBreakNoiseScale` | `float` | `0.1` |
| `Narrative.DriveSpikeThreshold` | `int` | `15` |
| `Narrative.WillpowerDropThreshold` | `int` | `10` |
| `Narrative.WillpowerLowThreshold` | `int` | `20` |
| `Narrative.AbruptDepartureWindowTicks` | `int` | `3` |
| `Narrative.CandidateDetailMaxLength` | `int` | `280` |
| `CastGenerator.ElevatedDriveRange` | `List` | `[2 entries]` |
| `CastGenerator.DepressedDriveRange` | `List` | `[2 entries]` |
| `CastGenerator.NeutralDriveRange` | `List` | `[2 entries]` |
| `CastGenerator.CurrentJitterRange` | `List` | `[2 entries]` |
| `CastGenerator.RivalryCount` | `int` | `2` |
| `CastGenerator.OldFlameCount` | `int` | `1` |
| `CastGenerator.MentorPairCount` | `int` | `1` |
| `CastGenerator.SleptWithSpouseCount` | `int` | `1` |
| `CastGenerator.FriendPairCount` | `int` | `2` |
| `CastGenerator.ThingNobodyTalksAboutCount` | `int` | `2` |
| `CastGenerator.RelationshipIntensityRange` | `List` | `[2 entries]` |
| `Chronicle.MaxEntries` | `int` | `4096` |
| `Chronicle.ThresholdRules.IntensityChangeMinForRelationshipStick` | `int` | `15` |
| `Chronicle.ThresholdRules.IrritationSpikeMinForPhysicalManifest` | `int` | `70` |
| `Chronicle.ThresholdRules.DriveReturnToBaselineWindowSeconds` | `double` | `60` |
| `Chronicle.ThresholdRules.TalkAboutMinReferenceCount` | `int` | `2` |
| `Chronicle.StainMagnitudeRange` | `List` | `[2 entries]` |
| `Chronicle.BrokenItemMagnitudeRange` | `List` | `[2 entries]` |
| `Dialog.CalcifyThreshold` | `int` | `8` |
| `Dialog.CalcifyContextDominanceMin` | `double` | `0.7` |
| `Dialog.TicRecognitionThreshold` | `int` | `5` |
| `Dialog.RecencyWindowSeconds` | `int` | `300` |
| `Dialog.ValenceMatchScore` | `int` | `5` |
| `Dialog.RecencyPenalty` | `int` | `-10` |
| `Dialog.CalcifyBiasScore` | `int` | `3` |
| `Dialog.PerListenerBiasScore` | `int` | `2` |
| `Dialog.ValenceLowMaxValue` | `int` | `33` |
| `Dialog.ValenceMidMaxValue` | `int` | `66` |
| `Dialog.DecalcifyTimeoutDays` | `int` | `30` |
| `Dialog.CorpusPath` | `string` | `docs/c2-content/dialog/corpus-starter.json` |
| `Dialog.DriveContextThreshold` | `int` | `60` |
| `Dialog.DialogAttemptProbability` | `double` | `0.05` |
| `ActionSelection.DriveCandidateThreshold` | `int` | `60` |
| `ActionSelection.IdleScoreFloor` | `double` | `0.2` |
| `ActionSelection.InversionStakeThreshold` | `double` | `0.55` |
| `ActionSelection.InversionInhibitionThreshold` | `double` | `0.5` |
| `ActionSelection.SuppressionGiveUpFactor` | `double` | `0.3` |
| `ActionSelection.SuppressionEpsilon` | `double` | `0.1` |
| `ActionSelection.SuppressionEventMagnitudeScale` | `int` | `5` |
| `ActionSelection.PersonalityTieBreakWeight` | `double` | `0.05` |
| `ActionSelection.MaxCandidatesPerTick` | `int` | `32` |
| `ActionSelection.AvoidStandoffDistance` | `int` | `4` |
| `Stress.SuppressionStressGain` | `double` | `1.5` |
| `Stress.DriveSpikeStressDelta` | `int` | `25` |
| `Stress.DriveSpikeStressGain` | `double` | `2` |
| `Stress.SocialConflictStressGain` | `double` | `3` |
| `Stress.AcuteDecayPerTick` | `double` | `0.05` |
| `Stress.StressedTagThreshold` | `int` | `60` |
| `Stress.OverwhelmedTagThreshold` | `int` | `85` |
| `Stress.BurningOutTagThreshold` | `int` | `70` |
| `Stress.BurningOutCooldownDays` | `int` | `3` |
| `Stress.StressAmplificationMagnitude` | `double` | `1` |
| `Stress.StressVolatilityScale` | `double` | `0.5` |
| `Stress.NeuroticismStressFactor` | `double` | `0.2` |
| `Schedule.ScheduleAnchorBaseWeight` | `double` | `0.3` |
| `Schedule.ScheduleLingerThresholdCells` | `float` | `2` |
| `Memory.MaxRelationshipMemoryCount` | `int` | `32` |
| `Memory.MaxPersonalMemoryCount` | `int` | `16` |
| `Workload.TaskGenerationHourOfDay` | `double` | `8` |
| `Workload.TaskGenerationCountPerDay` | `int` | `5` |
| `Workload.TaskEffortHoursMin` | `float` | `0.5` |
| `Workload.TaskEffortHoursMax` | `float` | `6` |
| `Workload.TaskDeadlineHoursMin` | `float` | `4` |
| `Workload.TaskDeadlineHoursMax` | `float` | `48` |
| `Workload.TaskPriorityMin` | `int` | `30` |
| `Workload.TaskPriorityMax` | `int` | `80` |
| `Workload.BaseProgressRatePerSecond` | `double` | `0.0001` |
| `Workload.ConscientiousnessProgressBias` | `double` | `0.1` |
| `Workload.QualityDecayPerStressedTick` | `double` | `0.0002` |
| `Workload.QualityRecoveryPerGoodTick` | `double` | `0.0001` |
| `Workload.WorkActionBaseWeight` | `double` | `0.4` |
| `Workload.OverdueTaskStressGain` | `double` | `1` |
| `SocialMask.MaskGainPerTick` | `double` | `0.5` |
| `SocialMask.MaskDecayPerTick` | `double` | `0.3` |
| `SocialMask.LowExposureThreshold` | `double` | `0.3` |
| `SocialMask.PersonalityMaskScale` | `double` | `0.2` |
| `SocialMask.PersonalityExtraversionScale` | `double` | `0.1` |
| `SocialMask.CrackThreshold` | `double` | `1.5` |
| `SocialMask.StressCrackContribution` | `double` | `0.5` |
| `SocialMask.BurnoutCrackBonus` | `double` | `0.3` |
| `SocialMask.LowWillpowerThreshold` | `int` | `30` |
| `SocialMask.SlipCooldownTicks` | `int` | `1800` |
| `PhysiologyGate.VetoStrengthThreshold` | `double` | `0.5` |
| `PhysiologyGate.LowWillpowerLeakageStart` | `int` | `30` |
| `PhysiologyGate.StressMaxRelaxation` | `double` | `0.7` |

