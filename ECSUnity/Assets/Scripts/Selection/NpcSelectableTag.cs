using UnityEngine;

/// <summary>
/// Records the engine EntityId on an NPC dot so <see cref="SelectionManager"/>
/// can map a click back to a <see cref="Warden.Contracts.Telemetry.WorldStateDto"/>
/// entry.
/// </summary>
public sealed class NpcSelectableTag : MonoBehaviour
{
    [Tooltip("Engine EntityId of the NPC this dot represents.")]
    public string EntityId;
}
