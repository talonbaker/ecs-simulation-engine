namespace APIFramework.Components;

/// <summary>
/// Room functional category. Integer values are intentionally identical to
/// <c>Warden.Contracts.Telemetry.RoomCategory</c> so projectors can cast without a lookup table.
/// </summary>
public enum RoomCategory
{
    Breakroom       = 0,
    Bathroom        = 1,
    CubicleGrid     = 2,
    Office          = 3,
    ConferenceRoom  = 4,
    SupplyCloset    = 5,
    ItCloset        = 6,
    Hallway         = 7,
    Stairwell       = 8,
    Elevator        = 9,
    ParkingLot      = 10,
    SmokingArea     = 11,
    LoadingDock     = 12,
    ProductionFloor = 13,
    Lobby           = 14,
    Outdoor         = 15,
}
