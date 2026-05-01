using System;

[Serializable]
public struct InspectorPopupData
{
    public SurfaceTierData   Surface;
    public BehaviourTierData Behaviour;
    public InternalTierData  Internal;
}

[Serializable]
public struct SurfaceTierData
{
    public string Name;
    public string CurrentAction;
}

[Serializable]
public struct BehaviourTierData
{
    public string DrivesSummary;
    public string MoodSummary;
}

[Serializable]
public struct InternalTierData
{
    public string WorkloadSummary;
    public string RecentMemoryFragment;
}
