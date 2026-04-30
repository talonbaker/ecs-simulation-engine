namespace APIFramework.Components;

#region Biological Urge Tags
/// <summary>Canonical hunger marker. Applied by MetabolismSystem when Satiation is low; read by BrainSystem and downstream sensation systems.</summary>
public struct HungerTag { }     // Standardized to 'Hunger'
/// <summary>Canonical thirst marker. Applied by MetabolismSystem when Hydration is low; read by BrainSystem and downstream sensation systems.</summary>
public struct ThirstTag { }     // Standardized to 'Thirst'
/// <summary>Mid-tier hunger band. Applied/removed by MetabolismSystem based on configured threshold; gates behaviour-level reactions.</summary>
public struct HungryTag { }
/// <summary>Severe hunger band. Applied/removed by MetabolismSystem at the starvation threshold; allows systems to react to life-threatening hunger.</summary>
public struct StarvingTag { }
/// <summary>Mid-tier thirst band. Applied/removed by MetabolismSystem based on configured threshold.</summary>
public struct ThirstyTag { }
/// <summary>Severe thirst band. Applied/removed by MetabolismSystem at the dehydration threshold.</summary>
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
/// <summary>Applied by EnergySystem when Energy &lt; tiredThreshold — sleep urge building.</summary>
public struct TiredTag { }     // Energy < tiredThreshold — sleep urge building
/// <summary>Applied by EnergySystem when Energy &lt; exhaustedThreshold — severely sleep-deprived.</summary>
public struct ExhaustedTag { } // Energy < exhaustedThreshold — severely sleep-deprived
/// <summary>Applied when Hunger or Thirst exceed the irritable threshold; consumed by mood/anger inputs.</summary>
public struct IrritableTag { } // Hunger OR Thirst above irritableThreshold
/// <summary>Applied by SleepSystem while the entity is actively sleeping. Read by MetabolismSystem to switch to sleep-rate drains.</summary>
public struct SleepingTag { }  // Entity is actively sleeping
#endregion

#region Entity Identity Tags
/// <summary>Marks an entity as a human (player-controlled species). Set at spawn by <c>EntityTemplates.SpawnHuman</c>; never removed.</summary>
public struct HumanTag { }
/// <summary>Marks an entity as a cat. Set at spawn by <c>EntityTemplates.SpawnCat</c>; never removed.</summary>
public struct CatTag { }
/// <summary>Marks an entity as a bolus (chewed-food projectile in the esophagus). Removed when the bolus is destroyed on stomach arrival.</summary>
public struct BolusTag { }
/// <summary>Marks an entity as an autonomous NPC (subject of social/cognitive systems). Set at spawn; never removed.</summary>
public struct NpcTag { }
/// <summary>Marks an entity as a relationship row (sibling component to <see cref="RelationshipComponent"/>).</summary>
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
/// <summary>Applied to an NPC entity expressing an active desire to eat. Drives FeedingSystem candidate enumeration.</summary>
public struct FoodDesireTag { }
/// <summary>Applied to an NPC entity expressing an active desire to drink. Drives DrinkingSystem candidate enumeration.</summary>
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
/// <summary>Applied by MoodSystem when Joy is in 0–33: calm contentment; no strong drives active.</summary>
public struct SereneTag { }    // Joy  0–33   calm contentment; no strong drives active
/// <summary>Applied by MoodSystem when Joy is in 34–66: active happiness; basic needs comfortably met.</summary>
public struct JoyfulTag { }    // Joy 34–66   active happiness; basic needs comfortably met
/// <summary>Applied by MoodSystem when Joy is in 67–100: peak elation; reserved for exceptional events.</summary>
public struct EcstaticTag { }  // Joy 67–100  peak elation; reserved for exceptional events
#endregion

#region Trust  (consistency, safety, familiar environment)
// Future inputs: known entities nearby, stable environment, no threats detected
/// <summary>Applied by MoodSystem when Trust is in 0–33: open, receptive — world feels safe.</summary>
public struct AcceptingTag { }  // Trust  0–33   open, receptive — world feels safe
/// <summary>Applied by MoodSystem when Trust is in 34–66: confident — environment is reliable.</summary>
public struct TrustingTag { }   // Trust 34–66   confident — environment is reliable
/// <summary>Applied by MoodSystem when Trust is in 67–100: deep positive bond — reserved for relationships.</summary>
public struct AdmiringTag { }   // Trust 67–100  deep positive bond — reserved for relationships
#endregion

#region Fear  (threats, pain, unknown stimuli)
// Future inputs: unknown entity proximity, rapid vital-stat drops, loud/unexpected events
/// <summary>Applied by MoodSystem when Fear is in 0–33: unease; something feels off.</summary>
public struct ApprehensiveTag { }  // Fear  0–33   unease; something feels off
/// <summary>Applied by MoodSystem when Fear is in 34–66: active fear; withdrawing from stimulus.</summary>
public struct FearfulTag { }       // Fear 34–66   active fear; withdrawing from stimulus
/// <summary>Applied by MoodSystem when Fear is in 67–100: panic; overrides all other drives.</summary>
public struct TerrorTag { }        // Fear 67–100  panic; overrides all other drives
#endregion

#region Surprise  (unexpected events, sudden changes)
// Future inputs: unexpected entity spawns, sudden environment changes, large stat deltas
/// <summary>Applied by MoodSystem when Surprise is in 0–33: mild distraction; attention briefly pulled.</summary>
public struct DistractedTag { }  // Surprise  0–33   mild distraction; attention briefly pulled
/// <summary>Applied by MoodSystem when Surprise is in 34–66: genuine surprise; brief action interruption.</summary>
public struct SurprisedTag { }   // Surprise 34–66   genuine surprise; brief action interruption
/// <summary>Applied by MoodSystem when Surprise is in 67–100: overwhelmed; stunned for an extended moment.</summary>
public struct AmazedTag { }      // Surprise 67–100  overwhelmed; stunned for an extended moment
#endregion

#region Sadness  (sustained deprivation, loss, failure)
// Future inputs: prolonged HungerTag/ThirstTag without relief, energy collapse, entity death nearby
/// <summary>Applied by MoodSystem when Sadness is in 0–33: quiet melancholy; low motivation.</summary>
public struct PensiveTag { }   // Sadness  0–33   quiet melancholy; low motivation
/// <summary>Applied by MoodSystem when Sadness is in 34–66: active sadness; reduced drive engagement.</summary>
public struct SadTag { }       // Sadness 34–66   active sadness; reduced drive engagement
/// <summary>Applied by MoodSystem when Sadness is in 67–100: incapacitating grief; strong drive suppression.</summary>
public struct GriefTag { }     // Sadness 67–100  incapacitating grief; strong drive suppression
#endregion

#region Disgust  (foul stimuli, rot, forced aversion — boredom at low intensity)
// Future inputs: proximity to RotTag entities, forced to eat spoiled food,
//               idle state sustained (Dominant == None for extended period → boredom)
/// <summary>Applied by MoodSystem when Disgust is in 0–33: nothing to do; idle state sustained (felt as boredom).</summary>
public struct BoredTag { }      // Disgust  0–33   nothing to do; idle state sustained
/// <summary>Applied by MoodSystem when Disgust is in 34–66: active aversion; avoiding a specific stimulus.</summary>
public struct DisgustTag { }    // Disgust 34–66   active aversion; avoiding a specific stimulus
/// <summary>Applied by MoodSystem when Disgust is in 67–100: extreme repulsion; total stimulus rejection.</summary>
public struct LoathingTag { }   // Disgust 67–100  extreme repulsion; total stimulus rejection
#endregion

#region Anger  (blocked needs, sustained irritability, injustice)
// Future inputs: sustained IrritableTag without resolution, drive repeatedly blocked,
//               another entity taking a food/water resource
/// <summary>Applied by MoodSystem when Anger is in 0–33: mild frustration; drive thwarted briefly.</summary>
public struct AnnoyedTag { }   // Anger  0–33   mild frustration; drive thwarted briefly
/// <summary>Applied by MoodSystem when Anger is in 34–66: active anger; strong drive with blocked outlet.</summary>
public struct AngryTag { }     // Anger 34–66   active anger; strong drive with blocked outlet
/// <summary>Applied by MoodSystem when Anger is in 67–100: uncontrolled rage; overrides social restraint.</summary>
public struct RagingTag { }    // Anger 67–100  uncontrolled rage; overrides social restraint
#endregion

#region Anticipation  (upcoming reward, goal-seeking, proximity to need-fulfillment)
// Future inputs: food/water entity in range but not yet consumed, drive urgency rising
//               but not yet dominant, sleep drive approaching threshold at night
/// <summary>Applied by MoodSystem when Anticipation is in 0–33: mild attention toward a resource.</summary>
public struct InterestedTag { }     // Anticipation  0–33   mild attention toward a resource
/// <summary>Applied by MoodSystem when Anticipation is in 34–66: goal-directed attention locked on.</summary>
public struct AnticipatingTag { }   // Anticipation 34–66   goal-directed attention locked on
/// <summary>Applied by MoodSystem when Anticipation is in 67–100: hyperaware; primed to act immediately.</summary>
public struct VigilantTag { }       // Anticipation 67–100  hyperaware; primed to act immediately
#endregion

#region Stress Tags
/// <summary>Applied by StressSystem when <see cref="StressComponent.AcuteLevel"/> ≥ stressedTagThreshold (default 60).</summary>
public struct StressedTag { }     // AcuteLevel ≥ stressedTagThreshold (default 60)
/// <summary>Applied by StressSystem when <see cref="StressComponent.AcuteLevel"/> ≥ overwhelmedTagThreshold (default 85).</summary>
public struct OverwhelmedTag { }  // AcuteLevel ≥ overwhelmedTagThreshold (default 85)
/// <summary>Applied by StressSystem when <see cref="StressComponent.ChronicLevel"/> ≥ burningOutTagThreshold. Sticky for the configured cooldown days.</summary>
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

#region Life-State / Scenario Tags (WP-3.0.1+)
/// <summary>
/// Applied by <see cref="APIFramework.Systems.LifeState.ChokingDetectionSystem"/> when an NPC begins to choke on a bolus.
/// Removed by <see cref="APIFramework.Systems.LifeState.ChokingCleanupSystem"/> on transition to Deceased.
/// While present, the NPC has <see cref="ChokingComponent"/> for detailed choke state.
/// </summary>
public struct IsChokingTag { }

/// <summary>
/// Applied by <see cref="APIFramework.Systems.LifeState.CorpseSpawnerSystem"/> when an NPC's death
/// narrative event is received. Persists for the lifetime of the entity — the body remains in the world.
/// The entity also receives <see cref="CorpseComponent"/> with death metadata.
/// WP-3.0.2: Deceased-Entity Handling + Bereavement.
/// </summary>
public struct CorpseTag { }

/// <summary>
/// Applied by <see cref="APIFramework.Systems.LifeState.FaintingDetectionSystem"/> when an NPC's
/// <see cref="MoodComponent.Fear"/> exceeds the configured threshold.
/// The NPC enters <see cref="LifeState.Incapacitated"/> for a fixed duration and then recovers
/// automatically — fainting never leads to death.
/// Removed by <see cref="APIFramework.Systems.LifeState.FaintingCleanupSystem"/> once the NPC
/// returns to <see cref="LifeState.Alive"/>.
/// The entity also carries <see cref="FaintingComponent"/> with timing metadata.
/// WP-3.0.6: Fainting System.
/// </summary>
public struct IsFaintingTag { }
#endregion
