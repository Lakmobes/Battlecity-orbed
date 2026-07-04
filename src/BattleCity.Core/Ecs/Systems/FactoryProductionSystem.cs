using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.Ecs.Systems;

/// <summary>
/// Spawns inactive items on factory bays (legacy/server/CBuilding.cpp, 7 s interval).
/// </summary>
public static class FactoryProductionSystem
{
    private const float ProductionIntervalSeconds = 7f;
    private static float _accumulator;

    private static readonly QueryDescription BuildingQuery =
        new QueryDescription().WithAll<BuildingRef, BuildingState>();

    private static readonly QueryDescription ItemQuery =
        new QueryDescription().WithAll<PlacedItemRef>();

    public static void Update(World world, CityBuildState? build, float deltaSeconds)
    {
        _accumulator += deltaSeconds;
        if (_accumulator < ProductionIntervalSeconds)
        {
            return;
        }

        _accumulator = 0f;

        world.Query(
            in BuildingQuery,
            (ref BuildingRef building, ref BuildingState state) =>
            {
                if (!BuildingCatalog.IsFactory(building.TypeCode)
                    || !BuildingCatalog.TryGetFactoryProduct(building.TypeCode, out var product))
                {
                    return;
                }

                if (state.Population < EconomyConstants.PopulationMaxNonHouse)
                {
                    return;
                }

                if (build is not null
                    && (building.MenuIndex < 0
                        || building.MenuIndex >= build.CanBuild.Length
                        || build.CanBuild[building.MenuIndex] != 2))
                {
                    return;
                }

                var (bayX, bayY) = BuildingCatalog.GetFactoryBayTile(
                    building.GridAnchorX,
                    building.GridAnchorY);

                if (HasItemAt(world, bayX, bayY))
                {
                    return;
                }

                if (state.ItemsLeft <= 0)
                {
                    state.ItemsLeft = ItemCatalog.MaxCarryCount[(int)product];
                }

                if (state.ItemsLeft <= 0)
                {
                    return;
                }

                GameplayEntityFactory.CreatePlacedItem(
                    world,
                    product,
                    bayX,
                    bayY,
                    active: false,
                    cityId: 0);

                state.ItemsLeft--;
            });
    }

    private static bool HasItemAt(World world, int gridX, int gridY)
    {
        var found = false;

        world.Query(
            in ItemQuery,
            (ref PlacedItemRef item) =>
            {
                if (!found && item.GridX == gridX && item.GridY == gridY)
                {
                    found = true;
                }
            });

        return found;
    }
}
