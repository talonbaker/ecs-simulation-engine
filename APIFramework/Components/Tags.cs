namespace APIFramework.Components;

#region Biological Urge Tags
public struct HungerTag { }     // Standardized to 'Hunger'
public struct ThirstTag { }     // Standardized to 'Thirst'
public struct HungryTag { }
public struct StarvingTag { }
public struct ThirstyTag { }
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
public struct TiredTag { }     // Energy < tiredThreshold — sleep urge building
public struct ExhaustedTag { } // Energy < exhaustedThreshold — severely sleep-deprived
public struct IrritableTag { } // Hunger OR Thirst above irritableThreshold
public struct SleepingTag { }  // Entity is actively sleeping
#endregion

#region Entity Identity Tags
public struct HumanTag { }
public struct CatTag { }
public struct BolusTag { }
public struct NpcTag { }
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
public struct FoodDesireTag { }
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
public struct SereneTag { }    // Joy  0–33   calm contentment; no strong drives active
public struct JoyfulTag { }    // Joy 34–66   active happiness; basic needs comfortably met
public struct EcstaticTag { }  // Joy 67–100  peak elation; reserved for exceptional events
#endregion

#region Trust  (consistency, safety, familiar environment)
// Future inputs: known entities nearby, stable environment, no threats detected
public struct AcceptingTag { }  // Trust  0–33   open, receptive — world feels safe
public struct TrustingTag { }   // Trust 34–66   confident — environment is reliable
public struct AdmiringTag { }   // Trust 67–100  deep positive bond — reserved for relationships
#endregion

#region Fear  (threats, pain, unknown stimuli)
// Future inputs: unknown entity proximity, rapid vital-stat drops, loud/unexpected events
public struct ApprehensiveTag { }  // Fear  0–33   unease; something feels off
public struct FearfulTag { }       // Fear 34–66   active fear; withdrawing from stimulus
public struct TerrorTag { }        // Fear 67–100  panic; overrides all other drives
#endregion

#region Surprise  (unexpected events, sudden changes)
// Future inputs: unexpected entity spawns, sudden environment changes, large stat deltas
public struct DistractedTag { }  // Surprise  0–33   mild distraction; attention briefly pulled
public struct SurprisedTag { }   // Surprise 34–66   genuine surprise; brief action interruption
public struct AmazedTag { }      // Surprise 67–100  overwhelmed; stunned for an extended moment
#endregion

#region Sadness  (sustained deprivation, loss, failure)
// Future inputs: prolonged HungerTag/ThirstTag without relief, energy collapse, entity death nearby
public struct PensiveTag { }   // Sadness  0–33   quiet melancholy; low motivation
public struct SadTag { }       // Sadness 34–66   active sadness; reduced drive engagement
public struct GriefTag { }     // Sadness 67–100  incapacitating grief; strong drive suppression
#endregion

#region Disgust  (foul stimuli, rot, forced aversion — boredom at low intensity)
// Future inputs: proximity to RotTag entities, forced to eat spoiled food,
//               idle state sustained (Dominant == None for extended period → boredom)
public struct BoredTag { }      // Disgust  0–33   nothing to do; idle state sustained
public struct DisgustTag { }    // Disgust 34–66   active aversion; avoiding a specific stimulus
public struct LoathingTag { }   // Disgust 67–100  extreme repulsion; total stimulus rejection
#endregion

#region Anger  (blocked needs, sustained irritability, injustice)
// Future inputs: sustained IrritableTag without resolution, drive repeatedly blocked,
//               another entity taking a food/water resource
public struct AnnoyedTag { }   // Anger  0–33   mild frustration; drive thwarted briefly
public struct AngryTag { }     // Anger 34–66   active anger; strong drive with blocked outlet
public struct RagingTag { }    // Anger 67–100  uncontrolled rage; overrides social restraint
#endregion

#region Anticipation  (upcoming reward, goal-seeking, proximity to need-fulfillment)
// Future inputs: food/water entity in range but not yet consumed, drive urgency rising
//               but not yet dominant, sleep drive approaching threshold at night
public struct InterestedTag { }     // Anticipation  0–33   mild attention toward a resource
public struct AnticipatingTag { }   // Anticipation 34–66   goal-directed attention locked on
public struct VigilantTag { }       // Anticipation 67–100  hyperaware; primed to act immediately
#endregion

#region Stress Tags
public struct StressedTag { }     // AcuteLevel ≥ stressedTagThreshold (default 60)
public struct OverwhelmedTag { }  // AcuteLevel ≥ overwhelmedTagThreshold (default 85)
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
