namespace BattleCity.Shared.Catalogs;

/// <summary>
/// Building menu metadata from legacy/client/Structs.cpp.
/// Type encoding: 2xx Hospital, 3xx House, 4xx Research, 1xx Factory (xx = item type).
/// </summary>
public static class BuildingCatalog
{
    public const int CommandCenterTypeCode = 0;

    public static IReadOnlyList<int> MenuTypeCodes { get; } =
    [
        200, 300, 400, 100, 409, 109, 403, 103, 402, 102, 411, 111,
        404, 104, 405, 105, 401, 101, 410, 110, 408, 108, 407, 107, 406, 106,
    ];

    public static IReadOnlyList<string> MenuNames { get; } =
    [
        "Hospital",
        "House",
        "Laser Research",
        "Laser Factory",
        "Turret Research",
        "Turret Factory",
        "Time Bomb Research",
        "Time Bomb Factory",
        "MedKit Research",
        "MedKit Factory",
        "Plasma Turret Research",
        "Plasma Turret Factory",
        "Mine Research",
        "Mine Factory",
        "Orb Research",
        "Orb Factory",
        "Bazooka Research",
        "Bazooka Factory",
        "Sleeper Research",
        "Sleeper Factory",
        "Wall Research",
        "Wall Factory",
        "DFG Research",
        "DFG Factory",
        "Flare Gun Research",
        "Flare Gun Factory",
    ];

    /// <summary>Build-menu icon indices (legacy buildButton).</summary>
    public static IReadOnlyList<int> MenuIconIndices { get; } =
    [
        12, 0, 1, 1, 9, 9, 4, 4, 3, 3, 10, 10, 5, 5, 6, 6, 2, 2, 8, 8, 11, 11, 8, 8, 7, 7,
    ];

    /// <summary>Item produced by each factory slot (legacy/server itemTypes).</summary>
    public static IReadOnlyList<Data.ItemType> FactoryProducts { get; } =
    [
        Data.ItemType.Rocket,
        Data.ItemType.Turret,
        Data.ItemType.Cloak,
        Data.ItemType.MedKit,
        Data.ItemType.Plasma,
        Data.ItemType.Mine,
        Data.ItemType.Orb,
        Data.ItemType.Bomb,
        Data.ItemType.Sleeper,
        Data.ItemType.Wall,
        Data.ItemType.Dfg,
        Data.ItemType.Flare,
    ];

    /// <summary>Build-tree prerequisite index per factory product (-1 = root).</summary>
    public static IReadOnlyList<int> BuildTreePrerequisites { get; } =
    [
        -1, // Rocket
        -1, // Turret
        0,  // Cloak
        0,  // MedKit
        1,  // Plasma
        1,  // Mine
        2,  // Orb
        2,  // Bomb
        4,  // Sleeper
        4,  // Wall
        5,  // DFG
        6,  // Flare
    ];

    public static bool IsCommandCenter(int typeCode) => typeCode == CommandCenterTypeCode;

    public static bool IsHospital(int typeCode) => typeCode is >= 200 and < 300;
    public static bool IsHouse(int typeCode) => typeCode is >= 300 and < 400;
    public static bool IsResearch(int typeCode) => typeCode is >= 400 and < 500;
    public static bool IsFactory(int typeCode) => typeCode is >= 100 and < 200;

    public static int GetMenuIndex(int typeCode)
    {
        for (var i = 0; i < MenuTypeCodes.Count; i++)
        {
            if (MenuTypeCodes[i] == typeCode)
            {
                return i;
            }
        }

        return -1;
    }

    public static bool TryGetFactoryProduct(int factoryTypeCode, out Data.ItemType product)
    {
        product = default;
        if (!IsFactory(factoryTypeCode))
        {
            return false;
        }

        var menuIndex = GetMenuIndex(factoryTypeCode);
        if (menuIndex < 3 || (menuIndex - 3) % 2 != 0)
        {
            return false;
        }

        var treeIndex = (menuIndex - 3) / 2;
        if (treeIndex < 0 || treeIndex >= FactoryProducts.Count)
        {
            return false;
        }

        product = FactoryProducts[treeIndex];
        return true;
    }

    /// <summary>
    /// Item icon for factory/research overlays (legacy build-tree product, not <c>typeCode % 100</c>).
    /// </summary>
    public static bool TryGetEquipmentItemType(int typeCode, out Data.ItemType itemType)
    {
        if (TryGetFactoryProduct(typeCode, out itemType))
        {
            return true;
        }

        if (TryGetResearchTreeIndex(typeCode, out var treeIndex))
        {
            itemType = FactoryProducts[treeIndex];
            return true;
        }

        itemType = default;
        return false;
    }

    public static int GetItemTypeFromCode(int typeCode)
    {
        if (IsHospital(typeCode) || IsHouse(typeCode))
        {
            return typeCode % 100;
        }

        return typeCode % 100;
    }

    public static bool TryGetResearchTreeIndex(int typeCode, out int treeIndex)
    {
        treeIndex = 0;
        if (!IsResearch(typeCode))
        {
            return false;
        }

        var menuIndex = GetMenuIndex(typeCode);
        if (menuIndex < 2 || (menuIndex - 2) % 2 != 0)
        {
            return false;
        }

        treeIndex = (menuIndex - 2) / 2;
        return treeIndex >= 0 && treeIndex < FactoryProducts.Count;
    }

    public static int GetResearchMenuIndex(int treeIndex) => 2 + treeIndex * 2;

    public static int GetFactoryMenuIndex(int treeIndex) => 3 + treeIndex * 2;

    /// <summary>
    /// Factory bay tile where produced items appear (center of the driveable southern row).
    /// Legacy used the northern row <c>(anchorX - 1, anchorY - 2)</c>; this rewrite keeps the
    /// bottom third driveable to match the 3×2 structure / 1×1 bay art layout.
    /// </summary>
    public static (int GridX, int GridY) GetFactoryBayTile(int gridAnchorX, int gridAnchorY) =>
        (gridAnchorX - 1, gridAnchorY);
}
