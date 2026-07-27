using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.City;

/// <summary>City tech unlocks that grant rechargeable tank equipment.</summary>
public static class CityEquipmentRules
{
    public static bool HasRechargeableCloak(CityBuildState? build) =>
        HasResearchAndFactory(build, EconomyConstants.CloakResearchTreeIndex);

    public static bool HasRechargeableFlare(CityBuildState? build) =>
        HasResearchAndFactory(build, EconomyConstants.FlareResearchTreeIndex);

    private static bool HasResearchAndFactory(CityBuildState? build, int treeIndex)
    {
        if (build is null
            || treeIndex < 0
            || treeIndex >= build.ResearchStatus.Length)
        {
            return false;
        }

        var factoryMenu = BuildingCatalog.GetFactoryMenuIndex(treeIndex);
        // Match spawn loadout: factory ownership unlocks rechargeable equipment.
        // Research complete (-1) is preferred but not required once the factory exists.
        return factoryMenu >= 0
            && factoryMenu < build.CanBuild.Length
            && build.CanBuild[factoryMenu] == 2;
    }
}
