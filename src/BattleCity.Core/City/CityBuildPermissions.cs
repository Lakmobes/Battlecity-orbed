using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;

namespace BattleCity.Core.City;

public static class CityBuildPermissions
{
    /// <summary>Legacy canBuild: show in menu only when the player can actually place one.</summary>
    public static bool IsVisibleInMenu(CityBuildState build, int menuIndex)
    {
        if (menuIndex < 0 || menuIndex >= build.CanBuild.Length)
        {
            return false;
        }

        if (BuildingCatalog.IsHouse(BuildingCatalog.MenuTypeCodes[menuIndex]))
        {
            return build.CanBuild[menuIndex] == 1;
        }

        return build.CanBuild[menuIndex] == 1;
    }

    public static bool CanPlace(CityBuildState build, int menuIndex)
    {
        if (menuIndex < 0 || menuIndex >= build.CanBuild.Length)
        {
            return false;
        }

        return build.CanBuild[menuIndex] == 1;
    }
}
