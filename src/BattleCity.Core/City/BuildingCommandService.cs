using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Shared.Catalogs;

namespace BattleCity.Core.City;

public static class BuildingCommandService
{
    public static bool TryPlaceBuilding(
        World world,
        CityBuildState build,
        TileMap tileMap,
        int buildSlot,
        int gridAnchorX,
        int gridAnchorY,
        Vector2? playerCenter = null)
    {
        var menuIndex = buildSlot - 1;
        if (menuIndex < 0 || menuIndex >= CityBuildState.MenuSlotCount)
        {
            return false;
        }

        if (!CityBuildPermissions.CanPlace(build, menuIndex))
        {
            return false;
        }

        var typeCode = BuildingCatalog.MenuTypeCodes[menuIndex];
        if (!BuildingPlacementValidator.CanPlace(
                world,
                tileMap,
                build,
                gridAnchorX,
                gridAnchorY,
                playerCenter))
        {
            return false;
        }

        var placement = new CityBuildingPlacement(menuIndex, gridAnchorX, gridAnchorY, typeCode);
        LevelLoader.SpawnBuilding(world, placement);
        ApplyBuiltPermissions(build, menuIndex, typeCode);
        build.RegisterBuildingPlaced(menuIndex, typeCode);
        return true;
    }

    public static bool TryDemolishAt(World world, CityBuildState build, int gridAnchorX, int gridAnchorY)
    {
        if (!BuildingPlacementValidator.TryFindBuildingAt(world, gridAnchorX, gridAnchorY, out var entity))
        {
            return false;
        }

        return TryDemolishEntity(world, build, entity);
    }

    public static bool TryDemolishByNetworkId(World world, CityBuildState build, ushort networkId)
    {
        if (!TryFindBuildingByNetworkId(world, networkId, out var entity))
        {
            return false;
        }

        return TryDemolishEntity(world, build, entity);
    }

    public static bool TryFindBuildingNetworkIdAt(
        World world,
        int gridAnchorX,
        int gridAnchorY,
        out ushort networkId)
    {
        networkId = 0;
        if (!BuildingPlacementValidator.TryFindBuildingAt(world, gridAnchorX, gridAnchorY, out var entity))
        {
            return false;
        }

        networkId = world.Get<BuildingRef>(entity).NetworkId;
        return networkId != 0;
    }

    public static bool TryFindBuildingByNetworkId(World world, ushort networkId, out Entity entity)
    {
        entity = Entity.Null;
        if (networkId == 0)
        {
            return false;
        }

        var found = false;
        var foundEntity = Entity.Null;
        var query = new QueryDescription().WithAll<BuildingRef>();
        world.Query(
            in query,
            (Entity candidate, ref BuildingRef building) =>
            {
                if (found || building.NetworkId != networkId)
                {
                    return;
                }

                foundEntity = candidate;
                found = true;
            });

        entity = foundEntity;
        return found;
    }

    private static bool TryDemolishEntity(World world, CityBuildState build, Entity entity)
    {
        ref var building = ref world.Get<BuildingRef>(entity);
        var menuIndex = building.MenuIndex;
        var typeCode = building.TypeCode;

        world.Destroy(entity);
        RestoreDemolishedPermissions(build, menuIndex, typeCode);
        build.RegisterBuildingRemoved();
        return true;
    }

    private static void ApplyBuiltPermissions(CityBuildState build, int menuIndex, int typeCode)
    {
        if (BuildingCatalog.IsHouse(typeCode)
            || menuIndex < 0
            || menuIndex >= build.CanBuild.Length)
        {
            return;
        }

        build.CanBuild[menuIndex] = 2;

        if (BuildingCatalog.TryGetResearchTreeIndex(typeCode, out var treeIndex))
        {
            build.ResearchStatus[treeIndex] = 0;
            build.ResearchTimers[treeIndex] = 0f;
        }
    }

    private static void RestoreDemolishedPermissions(CityBuildState build, int menuIndex, int typeCode)
    {
        if (BuildingCatalog.IsHouse(typeCode)
            || menuIndex < 0
            || menuIndex >= build.CanBuild.Length)
        {
            return;
        }

        build.CanBuild[menuIndex] = 1;

        if (BuildingCatalog.TryGetResearchTreeIndex(typeCode, out var treeIndex))
        {
            build.ResearchStatus[treeIndex] = 0;
            build.ResearchTimers[treeIndex] = 0f;

            var factoryIndex = BuildingCatalog.GetFactoryMenuIndex(treeIndex);
            if (factoryIndex < build.CanBuild.Length && build.CanBuild[factoryIndex] != 2)
            {
                build.CanBuild[factoryIndex] = 0;
            }
        }
    }
}
