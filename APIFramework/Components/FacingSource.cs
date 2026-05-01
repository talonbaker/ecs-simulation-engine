namespace APIFramework.Components;

/// <summary>Why an entity is facing the direction it is facing.</summary>
public enum FacingSource
{
    /// <summary>Facing derived from the entity's last movement velocity.</summary>
    MovementVelocity    = 0,
    /// <summary>Facing derived from a conversation partner's position.</summary>
    ConversationPartner = 1,
    /// <summary>Facing locked toward an explicit fixed target.</summary>
    FixedTarget         = 2,
    /// <summary>Entity is stationary — IdleMovementSystem may drift the facing slowly.</summary>
    Idle                = 3,
}
