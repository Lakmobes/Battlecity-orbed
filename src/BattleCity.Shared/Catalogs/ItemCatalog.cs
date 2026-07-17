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

    /// <summary>
    /// Items placed in the world (walls, turrets, mines, bombs, etc.).
    /// Gear like missiles, medkits, cloak, and flares are used from inventory instead.
    /// </summary>
    public static bool IsPlaceable(Data.ItemType type) =>
        type is Data.ItemType.Wall
            or Data.ItemType.Turret
            or Data.ItemType.Sleeper
            or Data.ItemType.Plasma
            or Data.ItemType.Mine
            or Data.ItemType.Bomb
            or Data.ItemType.Orb
            or Data.ItemType.Dfg;

    /// <summary>Owned equipment / ammo used via hotkeys or fire, not dropped on the ground.</summary>
    public static bool IsGear(Data.ItemType type) =>
        type is Data.ItemType.Cloak
            or Data.ItemType.Rocket
            or Data.ItemType.MedKit
            or Data.ItemType.Flare;
}
