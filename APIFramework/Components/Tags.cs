namespace APIFramework.Components;

#region Biological Urge Tags
/// <summary>Canonical hunger marker. Applied by BiologicalConditionSystem when satiation is below the hunger threshold; removed when satiation rises back above the threshold. Read by BrainSystem and downstream cognitive systems.</summary>
public struct HungerTag { }     // Standardized to 'Hunger'
/// <summary>Canonical thirst marker. Applied by BiologicalConditionSystem when hydration is below the thirst threshold; removed when hydration rises back above the threshold. Read by BrainSystem and downstream cognitive systems.</summary>
public struct ThirstTag { }     // Standardized to 'Thirst'
/// <summary>Mid-tier hunger sensation. Applied by BiologicalConditionSystem at the hungry threshold; removed when satiation rises out of the hungry band. Read by BrainSystem when scoring the Eat drive.</summary>
public struct HungryTag { }
/// <summary>Severe hunger sensation. Applied by BiologicalConditionSystem when satiation is below the starving threshold; removed when satiation recovers. Read by BrainSystem to escalate the Eat drive and by lockout/starvation pathways.</summary>
public struct StarvingTag { }
/// <summary>Mid-tier thirst sensation. Applied by BiologicalConditionSystem at the thirsty threshold; removed when hydration rises out of the thirsty band. Read by BrainSystem when scoring the Drink drive.</summary>
public struct ThirstyTag { }
/// <summary>Severe dehydration sensation. Applied by BiologicalConditionSystem when hydration is below the dehydrated threshold; removed when hydration recovers. Read by BrainSystem to escalate the Drink drive.</summary>
public struct DehydratedTag { }

/// <summary>
/// Applied by ColonSystem when StoolVolumeMl &gt;= UrgeThresholdMl.
/// BrainSystem reads this to raise DefecateUrgency above baseline.
/// Removed when the colon empties.
/// </summary>
public struct DefecationUrgeTag { }

/// <summary>
/// Applied by ColonSystem when StoolVolumeMl &gt;= CapacityMl (colon is full).
/// This overrides normal drive prioritisation — the entity MUST defecate.
/// Removed when StoolVolumeMl falls back below CapacityMl.
/// </summary>
public struct BowelCriticalTag { }

/// <summary>
/// Applied by BladderSystem when BladderComponent.VolumeML &gt;= UrgeThresholdMl.
/// BrainSystem reads this to raise PeeUrgency above baseline.
/// Removed when the bladder empties.
/// </summary>
public struct UrinationUrgeTag { }

/// <summary>
/// Applied by BladderSystem when BladderComponent.VolumeML &gt;= CapacityMl (bladder is full).
/// Overrides normal drive prioritisation — the entity MUST urinate.
/// Removed when VolumeML falls back below CapacityMl.
/// </summary>
public struct BladderCriticalTag { }
#endregion

#region Vital State Tags
/// <summary>Sleep-urge marker. Applied by EnergySystem when Energy &lt; tiredThreshold; removed when energy recovers. Read by BrainSystem when scoring the Sleep drive.</summary>
public struct TiredTag { }     // Energy < tiredThreshold — sleep urge building
/// <summary>Severe sleep-deprivation marker. Applied by EnergySystem when Energy &lt; exhaustedThreshold; removed when energy recovers. Read by BrainSystem to escalate the Sleep drive.</summary>
public struct ExhaustedTag { } // Energy < exhaustedThreshold — severely sleep-deprived
/// <summary>Compound stress marker. Applied by BiologicalConditionSystem when Hunger OR Thirst exceed the irritable threshold; removed when both fall below it. Read by MoodSystem (Anger inputs) and ChokingDetectionSystem (irritation distraction check).</summary>
public struct IrritableTag { } // Hunger OR Thirst above irritableThreshold
/// <summary>Active sleep marker. Applied by SleepSystem when an NPC enters sleep; removed when they wake. Read by EnergySystem (recovery rate) and behavior systems that should not act on a sleeping NPC.</summary>
public struct SleepingTag { }  // Entity is actively sleeping
#endregion

#region Entity Identity Tags
/// <summary>Marks an entity as a human. Attached at spawn by EntityTemplates.SpawnHuman; never removed. Used by systems that iterate humans specifically.</summary>
public struct HumanTag { }
/// <summary>Marks an entity as a cat. Attached at spawn; never removed. Used by systems that iterate cats specifically.</summary>
public struct CatTag { }
/// <summary>Marks an entity as a swallowed bolus traveling through the digestive tract. Attached when InteractionSystem creates the bolus; removed/destroyed when the bolus is fully digested or expelled. Read by EsophagusSystem, DigestionSystem, and ChokingDetectionSystem.</summary>
public struct BolusTag { }
/// <summary>Generic NPC marker (covers humans, cats, and any other ticked agent). Attached at spawn by EntityTemplates / CastGenerator; never removed. Used by virtually every cognitive and behavior system to query NPCs.</summary>
public struct NpcTag { }
/// <summary>Marks a relationship entity (a directed pairing between two NPCs, not the NPCs themselves). Attached by CastGenerator.SeedRelationships; never removed. Read by RelationshipLifecycleSystem and social cognition.</summary>
public struct RelationshipTag { }
/// <summary>Marks an entity as a room. Used by RoomMembershipSystem to skip room entities during membership checks.</summary>
public struct RoomTag { }
/// <summary>Marks an entity as a light fixture. Used by lighting systems for iteration.</summary>
public struct LightSourceTag { }
/// <summary>Marks an entity as a light aperture (window/skylight). Used by lighting systems for iteration.</summary>
public struct LightApertureTag { }
/// <summary>Marks an immovable entity as a pathfinding obstacle (furniture, walls). PathfindingService skips tiles occupied by obstacle entities.</summary>
public struct ObstacleTag { }
/// <summary>Marks an entity as an NPC spawn slot. Cast generator (WP-1.8.A) reads these and replaces them with full NPC entities.</summary>
public struct NpcSlotTag { }
/// <summary>Marks an entity as an authored world object placed at a named anchor (e.g. box of floppy disks, parking-lot sign).</summary>
public struct AnchorObjectTag { }
/// <summary>Marks a persistent physical spill entity spawned by PhysicalManifestSpawner.</summary>
public struct StainTag { }
/// <summary>Marks a persistent broken-item entity spawned by PhysicalManifestSpawner.</summary>
public struct BrokenItemTag { }
#endregion

#region Desire Tags
/// <summary>Marks an entity (typically a food item) as desired by an NPC. Attached/removed by ActionSelectionSystem and FeedingSystem to mark the current target of the Eat drive.</summary>
public struct FoodDesireTag { }
/// <summary>Marks an entity (typically a water source) as desired by an NPC. Attached/removed by ActionSelectionSystem and DrinkingSystem to mark the current target of the Drink drive.</summary>
public struct WaterDesireTag { }
#endregion

#region Rot / Decay Tags (food entities)
/// <summary>Applied by RotSystem when a food entity's rot level exceeds the rot threshold.</summary>
public struct RotTag { }
/// <summary>Temporary signal: applied to a living entity by FeedingSystem when it consumes rotten food.
/// MoodSystem reads this tag, spikes Disgust, then removes it.</summary>
public struct ConsumedRottenFoodTag { }
#endregion

// ═════════════════════════════════════════════════════════════════════════════
//  EMOTION TAGS  —  Plutchik's Wheel of Emotions
//
//  Each of the 8 primary emotions has three intensity levels applied by MoodSystem.
//  The tags follow Plutchik's naming: outer petal (low) → middle ring → center (high).
//
//  Only the tag is applied — the underlying float lives in MoodComponent.
//  Systems that want to respond to Billy's mood can gate on any of these tags
//  without knowing the exact intensity value.
//
//  Future systems that will drive each emotion are noted below.
// ═════════════════════════════════════════════════════════════════════════════

#region Joy  (needs met, comfort, social warmth)
// Future inputs: full satiation + hydration + energy, positive social events
/// <summary>Low-intensity Joy band (0–33). Applied/removed by MoodSystem based on MoodComponent.Joy. Read by systems gating on calm-contentment behavior.</summary>
public struct SereneTag { }    // Joy  0–33   calm contentment; no strong drives active
/// <summary>Mid-intensity Joy band (34–66). Applied/removed by MoodSystem based on MoodComponent.Joy. Read by systems gating on active-happiness behavior.</summary>
public struct JoyfulTag { }    // Joy 34–66   active happiness; basic needs comfortably met
/// <summary>Peak Joy band (67–100). Applied/removed by MoodSystem based on MoodComponent.Joy. Read by systems gating on exceptional-positive behavior.</summary>
public struct EcstaticTag { }  // Joy 67–100  peak elation; reserved for exceptional events
#endregion

#region Trust  (consistency, safety, familiar environment)
// Future inputs: known entities nearby, stable environment, no threats detected
/// <summary>Low-intensity Trust band (0–33). Applied/removed by MoodSystem based on MoodComponent.Trust.</summary>
public struct AcceptingTag { }  // Trust  0–33   open, receptive — world feels safe
/// <summary>Mid-intensity Trust band (34–66). Applied/removed by MoodSystem based on MoodComponent.Trust.</summary>
public struct TrustingTag { }   // Trust 34–66   confident — environment is reliable
/// <summary>Peak Trust band (67–100). Applied/removed by MoodSystem based on MoodComponent.Trust. Reserved for deep positive bonds.</summary>
public struct AdmiringTag { }   // Trust 67–100  deep positive bond — reserved for relationships
#endregion

#region Fear  (threats, pain, unknown stimuli)
// Future inputs: unknown entity proximity, rapid vital-stat drops, loud/unexpected events
/// <summary>Low-intensity Fear band (0–33). Applied/removed by MoodSystem based on MoodComponent.Fear.</summary>
public struct ApprehensiveTag { }  // Fear  0–33   unease; something feels off
/// <summary>Mid-intensity Fear band (34–66). Applied/removed by MoodSystem based on MoodComponent.Fear.</summary>
public struct FearfulTag { }       // Fear 34–66   active fear; withdrawing from stimulus
/// <summary>Peak Fear band (67–100). Applied/removed by MoodSystem based on MoodComponent.Fear. Read by systems that override drive selection under panic.</summary>
public struct TerrorTag { }        // Fear 67–100  panic; overrides all other drives
#endregion

#region Surprise  (unexpected events, sudden changes)
// Future inputs: unexpected entity spawns, sudden environment changes, large stat deltas
/// <summary>Low-intensity Surprise band (0–33). Applied/removed by MoodSystem based on MoodComponent.Surprise.</summary>
public struct DistractedTag { }  // Surprise  0–33   mild distraction; attention briefly pulled
/// <summary>Mid-intensity Surprise band (34–66). Applied/removed by MoodSystem based on MoodComponent.Surprise.</summary>
public struct SurprisedTag { }   // Surprise 34–66   genuine surprise; brief action interruption
/// <summary>Peak Surprise band (67–100). Applied/removed by MoodSystem based on MoodComponent.Surprise.</summary>
public struct AmazedTag { }      // Surprise 67–100  overwhelmed; stunned for an extended moment
#endregion

#region Sadness  (sustained deprivation, loss, failure)
// Future inputs: prolonged HungerTag/ThirstTag without relief, energy collapse, entity death nearby
/// <summary>Low-intensity Sadness band (0–33). Applied/removed by MoodSystem based on MoodComponent.Sadness.</summary>
public struct PensiveTag { }   // Sadness  0–33   quiet melancholy; low motivation
/// <summary>Mid-intensity Sadness band (34–66). Applied/removed by MoodSystem based on MoodComponent.Sadness.</summary>
public struct SadTag { }       // Sadness 34–66   active sadness; reduced drive engagement
/// <summary>Peak Sadness band (67–100). Applied/removed by MoodSystem based on MoodComponent.Sadness. Read by systems gating drive suppression.</summary>
public struct GriefTag { }     // Sadness 67–100  incapacitating grief; strong drive suppression
#endregion

#region Disgust  (foul stimuli, rot, forced aversion — boredom at low intensity)
// Future inputs: proximity to RotTag entities, forced to eat spoiled food,
//               idle state sustained (Dominant == None for extended period → boredom)
/// <summary>Low-intensity Disgust band (0–33), expressed as Boredom. Applied/removed by MoodSystem based on MoodComponent.Disgust.</summary>
public struct BoredTag { }      // Disgust  0–33   nothing to do; idle state sustained
/// <summary>Mid-intensity Disgust band (34–66). Applied/removed by MoodSystem based on MoodComponent.Disgust.</summary>
public struct DisgustTag { }    // Disgust 34–66   active aversion; avoiding a specific stimulus
/// <summary>Peak Disgust band (67–100). Applied/removed by MoodSystem based on MoodComponent.Disgust.</summary>
public struct LoathingTag { }   // Disgust 67–100  extreme repulsion; total stimulus rejection
#endregion

#region Anger  (blocked needs, sustained irritability, injustice)
// Future inputs: sustained IrritableTag without resolution, drive repeatedly blocked,
//               another entity taking a food/water resource
/// <summary>Low-intensity Anger band (0–33). Applied/removed by MoodSystem based on MoodComponent.Anger.</summary>
public struct AnnoyedTag { }   // Anger  0–33   mild frustration; drive thwarted briefly
/// <summary>Mid-intensity Anger band (34–66). Applied/removed by MoodSystem based on MoodComponent.Anger.</summary>
public struct AngryTag { }     // Anger 34–66   active anger; strong drive with blocked outlet
/// <summary>Peak Anger band (67–100). Applied/removed by MoodSystem based on MoodComponent.Anger. Read by systems that should bypass social restraint.</summary>
public struct RagingTag { }    // Anger 67–100  uncontrolled rage; overrides social restraint
#endregion

#region Anticipation  (upcoming reward, goal-seeking, proximity to need-fulfillment)
// Future inputs: food/water entity in range but not yet consumed, drive urgency rising
//               but not yet dominant, sleep drive approaching threshold at night
/// <summary>Low-intensity Anticipation band (0–33). Applied/removed by MoodSystem based on MoodComponent.Anticipation.</summary>
public struct InterestedTag { }     // Anticipation  0–33   mild attention toward a resource
/// <summary>Mid-intensity Anticipation band (34–66). Applied/removed by MoodSystem based on MoodComponent.Anticipation.</summary>
public struct AnticipatingTag { }   // Anticipation 34–66   goal-directed attention locked on
/// <summary>Peak Anticipation band (67–100). Applied/removed by MoodSystem based on MoodComponent.Anticipation.</summary>
public struct VigilantTag { }       // Anticipation 67–100  hyperaware; primed to act immediately
#endregion

#region Stress Tags
/// <summary>Acute-stress marker. Applied by StressSystem when StressComponent.AcuteLevel reaches the stressed threshold (default 60); removed when AcuteLevel falls back below it. Read by behavior, mood, and willpower systems.</summary>
public struct StressedTag { }     // AcuteLevel ≥ stressedTagThreshold (default 60)
/// <summary>High-acute-stress marker. Applied by StressSystem when AcuteLevel reaches the overwhelmed threshold (default 85); removed when it falls back below. Read by systems that suppress non-essential behavior under overload.</summary>
public struct OverwhelmedTag { }  // AcuteLevel ≥ overwhelmedTagThreshold (default 85)
/// <summary>Chronic-burnout marker. Applied by StressSystem when ChronicLevel reaches the burnout threshold; sticky for the configured cooldown-days window before removal. Read by willpower and mood systems.</summary>
public struct BurningOutTag { }   // ChronicLevel ≥ burningOutTagThreshold; sticky for cooldown days
#endregion

#region Workload Tags
/// <summary>Marks a task entity. Systems use this to iterate all tasks without scanning all entities.</summary>
public struct TaskTag { }
/// <summary>Applied by WorkloadSystem when a task's DeadlineTick has passed. Removed only when the task is completed or destroyed.</summary>
public struct OverdueTag { }
/// <summary>Applied to an NPC when all capacity slots are filled with overdue tasks. Query helper only; no system reacts to it.</summary>
public struct BurnedOutFromWorkloadTag { }
#endregion

#region Structural / Topology Tags
/// <summary>Marks an entity whose position affects pathfinding topology (walls, doors, desks, obstacles).
/// When a StructuralTag entity changes position or is added/removed, StructuralChangeBus emits an event.
/// Mutations to such entities must flow through IWorldMutationApi for cache invalidation.</summary>
public struct StructuralTag { }
/// <summary>Marks an entity that can be moved at runtime via IWorldMutationApi.MoveEntity.
/// Usually attached to StructuralTag entities except walls (which move via add/remove, not move).
/// IWorldMutationApi.MoveEntity rejects moves on entities lacking this tag.</summary>
public struct MutableTopologyTag { }
#endregion

#region Choking / Asphyxiation Tags
/// <summary>
/// Applied to an NPC when they are actively choking (bolus lodged in esophagus,
/// cannot breathe). Removed when the NPC transitions to Deceased.
/// Used to prevent re-triggering choke detection on an already-choking NPC.
/// </summary>
public struct IsChokingTag { }
#endregion

#region Death and Hazard Tags
/// <summary>
/// Applied to a door entity when it is locked. PathfindingService treats locked doors
/// as obstacles (infinite cost), effectively blocking passage through that tile.
/// Removed when the door is unlocked via IWorldMutationApi.DetachObstacle.
/// </summary>
public struct LockedTag { }
#endregion
