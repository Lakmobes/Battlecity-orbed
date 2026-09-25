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
    /// Resolves an item type from a numeric id (0–11) or a display/enum name prefix
    /// (e.g. "wall", "med", "laser", "cloak", "cougar").
    /// </summary>
    public static bool TryParse(string text, out Data.ItemType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();
        if (int.TryParse(text, out var id)
            && id >= 0
            && id < Names.Count
            && Enum.IsDefined(typeof(Data.ItemType), id))
        {
            type = (Data.ItemType)id;
            return true;
        }

        if (Enum.TryParse(text, ignoreCase: true, out Data.ItemType byEnum)
            && Enum.IsDefined(byEnum))
        {
            type = byEnum;
            return true;
        }

        // Common aliases that differ from legacy display names.
        if (text.Equals("cloak", StringComparison.OrdinalIgnoreCase)
            || text.Equals("laser", StringComparison.OrdinalIgnoreCase))
        {
            type = Data.ItemType.Cloak;
            return true;
        }

        if (text.Equals("rocket", StringComparison.OrdinalIgnoreCase)
            || text.Equals("missile", StringComparison.OrdinalIgnoreCase)
            || text.Equals("cougar", StringComparison.OrdinalIgnoreCase))
        {
            type = Data.ItemType.Rocket;
            return true;
        }

        if (text.Equals("flare", StringComparison.OrdinalIgnoreCase)
            || text.Equals("walkie", StringComparison.OrdinalIgnoreCase))
        {
            type = Data.ItemType.Flare;
            return true;
        }

        if (text.Equals("sleeper", StringComparison.OrdinalIgnoreCase))
        {
            type = Data.ItemType.Sleeper;
            return true;
        }

        if (text.Equals("plasma", StringComparison.OrdinalIgnoreCase))
        {
            type = Data.ItemType.Plasma;
            return true;
        }

        Data.ItemType? match = null;
        for (var i = 0; i < Names.Count; i++)
        {
            if (!Names[i].StartsWith(text, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (match.HasValue)
            {
                return false; // ambiguous prefix
            }

            match = (Data.ItemType)i;
        }

        if (!match.HasValue)
        {
            return false;
        }

        type = match.Value;
        return true;
    }

    /// <summary>Gear used via hotkeys/fire; may also be dropped/picked up (except laser).</summary>
    public static bool IsGear(Data.ItemType type) =>
        type is Data.ItemType.Cloak
            or Data.ItemType.Rocket
            or Data.ItemType.Flare;

    /// <summary>
    /// Items placed in the world (walls, turrets, mines, bombs, etc.).
    /// Flare can be dropped/picked up; cloak/rocket stay as pure gear.
    /// </summary>
    public static bool IsPlaceable(Data.ItemType type) =>
        type is Data.ItemType.MedKit
            or Data.ItemType.Wall
            or Data.ItemType.Turret
            or Data.ItemType.Sleeper
            or Data.ItemType.Plasma
            or Data.ItemType.Mine
            or Data.ItemType.Bomb
            or Data.ItemType.Orb
            or Data.ItemType.Dfg
            or Data.ItemType.Flare;

    /// <summary>Inventory cleared back to factories on death (not cloak/rocket/flare upgrades).</summary>
    public static bool ReturnsToFactoryOnDeath(Data.ItemType type) =>
        IsPlaceable(type) && type != Data.ItemType.Flare;
}
