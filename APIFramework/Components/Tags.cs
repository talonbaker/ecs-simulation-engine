namespace APIFramework.Components;

#region Biological Urge Tags
public struct HungerTag { }     // Standardized to 'Hunger'
public struct ThirstTag { }     // Standardized to 'Thirst'
public struct HungryTag { }
public struct StarvingTag { }
public struct ThirstyTag { }
public struct DehydratedTag { }
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
#endregion

#region Desire Tags
public struct FoodDesireTag { }
public struct WaterDesireTag { }
#endregion