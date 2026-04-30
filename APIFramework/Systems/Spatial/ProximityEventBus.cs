using System;
using APIFramework.Systems.Dialog;

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
    /// <summary>Fires when a target enters the observer's conversation range.</summary>
    public event Action<ProximityEnteredConversationRange>? OnEnteredConversationRange;
    /// <summary>Fires when a target leaves the observer's conversation range.</summary>
    public event Action<ProximityLeftConversationRange>?   OnLeftConversationRange;
    /// <summary>Fires when a target enters the observer's room (and is not in conversation range).</summary>
    public event Action<ProximityEnteredRoom>?             OnEnteredRoom;
    /// <summary>Fires when a target leaves the observer's room.</summary>
    public event Action<ProximityLeftRoom>?                OnLeftRoom;
    /// <summary>Fires when a target is first visible to the observer (awareness-range entry only; no paired Left event).</summary>
    public event Action<ProximityVisibleFromHere>?         OnVisibleFromHere;
    /// <summary>Fires when an entity transitions between rooms (or in/out of any room).</summary>
    public event Action<RoomMembershipChanged>?            OnRoomMembershipChanged;

    /// <summary>Fires when an NPC speaks a phrase fragment to a listener (raised by <see cref="APIFramework.Systems.Dialog.DialogFragmentRetrievalSystem"/>).</summary>
    public event Action<SpokenFragmentEvent>? OnSpokenFragment;

    /// <summary>Publishes <paramref name="e"/> on <see cref="OnEnteredConversationRange"/>.</summary>
    /// <param name="e">The event to publish.</param>
    public void RaiseEnteredConversationRange(ProximityEnteredConversationRange e) => OnEnteredConversationRange?.Invoke(e);
    /// <summary>Publishes <paramref name="e"/> on <see cref="OnLeftConversationRange"/>.</summary>
    /// <param name="e">The event to publish.</param>
    public void RaiseLeftConversationRange(ProximityLeftConversationRange e)       => OnLeftConversationRange?.Invoke(e);
    /// <summary>Publishes <paramref name="e"/> on <see cref="OnEnteredRoom"/>.</summary>
    /// <param name="e">The event to publish.</param>
    public void RaiseEnteredRoom(ProximityEnteredRoom e)                           => OnEnteredRoom?.Invoke(e);
    /// <summary>Publishes <paramref name="e"/> on <see cref="OnLeftRoom"/>.</summary>
    /// <param name="e">The event to publish.</param>
    public void RaiseLeftRoom(ProximityLeftRoom e)                                 => OnLeftRoom?.Invoke(e);
    /// <summary>Publishes <paramref name="e"/> on <see cref="OnVisibleFromHere"/>.</summary>
    /// <param name="e">The event to publish.</param>
    public void RaiseVisibleFromHere(ProximityVisibleFromHere e)                   => OnVisibleFromHere?.Invoke(e);
    /// <summary>Publishes <paramref name="e"/> on <see cref="OnRoomMembershipChanged"/>.</summary>
    /// <param name="e">The event to publish.</param>
    public void RaiseRoomMembershipChanged(RoomMembershipChanged e)                => OnRoomMembershipChanged?.Invoke(e);
    /// <summary>Publishes <paramref name="e"/> on <see cref="OnSpokenFragment"/>.</summary>
    /// <param name="e">The event to publish.</param>
    public void RaiseSpokenFragment(SpokenFragmentEvent e)                         => OnSpokenFragment?.Invoke(e);
}
