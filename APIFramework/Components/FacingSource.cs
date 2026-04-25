namespace APIFramework.Components;

/// <summary>Why an entity is facing the direction it is facing.</summary>
public enum FacingSource
{
    MovementVelocity    = 0,
    ConversationPartner = 1,
    FixedTarget         = 2,
    Idle                = 3,
}
