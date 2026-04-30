namespace APIFramework.Components;

/// <summary>
/// Room functional category. Integer values are intentionally identical to
/// <c>Warden.Contracts.Telemetry.RoomCategory</c> so projectors can cast without a lookup table.
/// </summary>
public enum RoomCategory
{
    /// <summary>Communal break / kitchen area.</summary>
    Breakroom       = 0,
    /// <summary>Toilet / washroom.</summary>
    Bathroom        = 1,
    /// <summary>Open cubicle workspace.</summary>
    CubicleGrid     = 2,
    /// <summary>Private office.</summary>
    Office          = 3,
    /// <summary>Meeting / conference room.</summary>
    ConferenceRoom  = 4,
    /// <summary>Storage closet for supplies.</summary>
    SupplyCloset    = 5,
    /// <summary>IT / server closet.</summary>
    ItCloset        = 6,
    /// <summary>Connecting hallway.</summary>
    Hallway         = 7,
    /// <summary>Stairwell.</summary>
    Stairwell       = 8,
    /// <summary>Elevator car / shaft.</summary>
    Elevator        = 9,
    /// <summary>Outdoor parking lot.</summary>
    ParkingLot      = 10,
    /// <summary>Designated smoking area.</summary>
    SmokingArea     = 11,
    /// <summary>Loading dock / shipping area.</summary>
    LoadingDock     = 12,
    /// <summary>Industrial production floor.</summary>
    ProductionFloor = 13,
    /// <summary>Building lobby / reception.</summary>
    Lobby           = 14,
    /// <summary>Generic outdoor area.</summary>
    Outdoor         = 15,
}
