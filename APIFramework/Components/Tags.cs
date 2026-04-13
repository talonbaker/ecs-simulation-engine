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
public struct ExhaustedTag { } // For your sleep logic
public struct IrritableTag { } // For that "low energy/angry" state
public struct SleepingTag { }
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