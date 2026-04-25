using System;

namespace APIFramework.Systems.Spatial;

/// <summary>
/// Thin event bus for spatial proximity and room-membership signals.
/// Registered as a singleton by SimulationBootstrapper; shared across all spatial systems.
///
/// Events are batched by ProximityEventSystem and fired at end-of-tick so subscribers
/// always see a consistent snapshot of the spatial state.
/// </summary>
public sealed class ProximityEventBus
{
    public event Action<ProximityEnteredConversationRange>? OnEnteredConversationRange;
    public event Action<ProximityLeftConversationRange>?   OnLeftConversationRange;
    public event Action<ProximityEnteredRoom>?             OnEnteredRoom;
    public event Action<ProximityLeftRoom>?                OnLeftRoom;
    public event Action<ProximityVisibleFromHere>?         OnVisibleFromHere;
    public event Action<RoomMembershipChanged>?            OnRoomMembershipChanged;

    public void RaiseEnteredConversationRange(ProximityEnteredConversationRange e) => OnEnteredConversationRange?.Invoke(e);
    public void RaiseLeftConversationRange(ProximityLeftConversationRange e)       => OnLeftConversationRange?.Invoke(e);
    public void RaiseEnteredRoom(ProximityEnteredRoom e)                           => OnEnteredRoom?.Invoke(e);
    public void RaiseLeftRoom(ProximityLeftRoom e)                                 => OnLeftRoom?.Invoke(e);
    public void RaiseVisibleFromHere(ProximityVisibleFromHere e)                   => OnVisibleFromHere?.Invoke(e);
    public void RaiseRoomMembershipChanged(RoomMembershipChanged e)                => OnRoomMembershipChanged?.Invoke(e);
}
