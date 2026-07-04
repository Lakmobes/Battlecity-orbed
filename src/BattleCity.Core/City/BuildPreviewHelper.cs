using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Maps;
using BattleCity.Shared.Catalogs;

namespace BattleCity.Core.City;

public static class BuildPreviewHelper
{
    public static bool Evaluate(
        World world,
        CityBuildState build,
        TileMap tileMap,
        int buildModeSlot,
        int gridAnchorX,
        int gridAnchorY,
        Vector2? playerCenter,
        out int typeCode,
        out bool isDemolish)
    {
        typeCode = 0;
        isDemolish = buildModeSlot == -1;

        if (isDemolish)
        {
            return BuildingPlacementValidator.TryFindBuildingAt(world, gridAnchorX, gridAnchorY, out _);
        }

        var menuIndex = buildModeSlot - 1;
        if (menuIndex < 0 || menuIndex >= BuildingCatalog.MenuTypeCodes.Count)
        {
            return false;
        }

        typeCode = BuildingCatalog.MenuTypeCodes[menuIndex];
        if (!CityBuildPermissions.CanPlace(build, menuIndex))
        {
            return false;
        }

        return BuildingPlacementValidator.CanPlace(
            world,
            tileMap,
            build,
            gridAnchorX,
            gridAnchorY,
            playerCenter);
    }
}
