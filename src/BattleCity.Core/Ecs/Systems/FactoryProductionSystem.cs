using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Core.Ecs.Systems;

/// <summary>
/// Spawns inactive items on factory bays (legacy/server/CBuilding.cpp, 7 s interval).
/// Keeps producing until the city holds <see cref="ItemCatalog.MaxCarryCount"/> of that product
/// (items may stack on the bay while waiting for pickup).
/// </summary>
public static class FactoryProductionSystem
{
    private const float ProductionIntervalSeconds = 7f;
    private static float _accumulator;

    private static readonly QueryDescription BuildingQuery =
        new QueryDescription().WithAll<BuildingRef, BuildingState>();

    private static readonly QueryDescription ItemQuery =
        new QueryDescription().WithAll<PlacedItemRef>();

    private static readonly QueryDescription InventoryQuery =
        new QueryDescription().WithAll<PlayerInventory, CityAffiliation>();

    public static void Update(
        World world,
        CityBuildState? build,
        float deltaSeconds,
        Func<ushort>? allocateNetworkItemId = null,
        Action<ServerAddItemPacket>? reportSpawn = null)
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

                var menuIndex = BuildingCatalog.GetMenuIndex(building.TypeCode);
                if (build is not null
                    && (menuIndex < 0
                        || menuIndex >= build.CanBuild.Length
                        || build.CanBuild[menuIndex] != 2))
                {
                    return;
                }

                var cityId = build?.CityId ?? 0;
                var capacity = ItemCatalog.MaxCarryCount[(int)product];
                var held = CountCityProduct(world, cityId, product);
                state.ItemsLeft = Math.Max(0, capacity - held);

                if (state.ItemsLeft <= 0)
                {
                    return;
                }

                if (product == ItemType.Orb && !OrbCityRules.CanAddOrbToCity(world, cityId))
                {
                    return;
                }

                var (bayX, bayY) = BuildingCatalog.GetFactoryBayTile(
                    building.GridAnchorX,
                    building.GridAnchorY);

                var networkItemId = allocateNetworkItemId?.Invoke() ?? 0;
                GameplayEntityFactory.CreatePlacedItem(
                    world,
                    product,
                    bayX,
                    bayY,
                    active: false,
                    cityId: cityId,
                    networkItemId: networkItemId);

                if (networkItemId != 0 && reportSpawn is not null)
                {
                    reportSpawn(new ServerAddItemPacket(
                        (ushort)bayX,
                        (ushort)bayY,
                        (byte)Math.Clamp(cityId, 0, byte.MaxValue),
                        (byte)product,
                        active: 0,
                        networkItemId));
                }

                state.ItemsLeft = Math.Max(0, capacity - CountCityProduct(world, cityId, product));
            });
    }

    public static int CountCityProduct(World world, int cityId, ItemType product)
    {
        _ = cityId;
        var count = 0;

        world.Query(
            in ItemQuery,
            (ref PlacedItemRef item) =>
            {
                if (item.Type == product)
                {
                    count++;
                }
            });

        world.Query(
            in InventoryQuery,
            (ref PlayerInventory inventory, ref CityAffiliation _) =>
            {
                count += inventory.GetCount(product);
            });

        return count;
    }

    public static bool TryFindFactoryBayForProduct(World world, ItemType product, out int bayX, out int bayY)
    {
        bayX = 0;
        bayY = 0;
        var found = false;
        var foundX = 0;
        var foundY = 0;

        world.Query(
            in BuildingQuery,
            (ref BuildingRef building, ref BuildingState _) =>
            {
                if (found
                    || !BuildingCatalog.IsFactory(building.TypeCode)
                    || !BuildingCatalog.TryGetFactoryProduct(building.TypeCode, out var factoryProduct)
                    || factoryProduct != product)
                {
                    return;
                }

                (foundX, foundY) = BuildingCatalog.GetFactoryBayTile(
                    building.GridAnchorX,
                    building.GridAnchorY);
                found = true;
            });

        if (!found)
        {
            return false;
        }

        bayX = foundX;
        bayY = foundY;
        return true;
    }
}
