namespace BattleCity.Shared.Data;

/// <summary>
/// Item type ids shared by client and server (legacy ITEM_TYPE_* constants).
/// Index 6 is Flare Gun on the client and Walkie Talkie on the server.
/// </summary>
public enum ItemType : int
{
    Cloak = 0,
    Rocket = 1,
    MedKit = 2,
    Bomb = 3,
    Mine = 4,
    Orb = 5,
    Flare = 6,
    Dfg = 7,
    Wall = 8,
    Turret = 9,
    Sleeper = 10,
    Plasma = 11,
}
