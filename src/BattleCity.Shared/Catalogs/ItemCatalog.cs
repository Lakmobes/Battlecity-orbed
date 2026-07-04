namespace BattleCity.Shared.Catalogs;

/// <summary>
/// Item display names and inventory limits.
/// Names from legacy/client/Structs.cpp (linked runtime data).
/// Max counts from legacy/server/CConstants.h.
/// </summary>
public static class ItemCatalog
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "Laser",
        "Cougar Missile",
        "MedKit",
        "Bomb",
        "Mine",
        "Orb",
        "Flare Gun",
        "DFG",
        "Wall",
        "Turret",
        "Sleeper Turret",
        "Plasma Turret",
    ];

    /// <summary>Maximum carry count per item type (legacy/server maxItems).</summary>
    public static IReadOnlyList<int> MaxCarryCount { get; } =
    [
        4,  // Cloak
        4,  // Rocket
        5,  // MedKit
        20, // Bomb
        10, // Mine
        1,  // Orb
        4,  // Flare / Walkie
        5,  // DFG
        20, // Wall
        10, // Turret
        5,  // Sleeper
        5,  // Plasma
    ];

    public static string GetName(Data.ItemType type) => Names[(int)type];
}
