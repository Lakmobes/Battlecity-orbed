using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.City;

/// <summary>
/// Base placed around an AI command center. It is large enough to orb (21 buildings),
/// includes an orb factory, and is ringed with walls and turrets.
/// </summary>
public static class AiCityTemplate
{
    public const int HospitalTypeCode = 200;
    public const int HouseTypeCode = 300;
    public const int BombFactoryTypeCode = 101;
    public const int OrbFactoryTypeCode = 105;
    public const int MaxWalls = 24;
    public const int TurretCount = 4;

    /// <summary>One of each, placed before extra houses fill the city out to orb size.</summary>
    private static readonly int[] FeaturedTypeCodes =
    [
        HouseTypeCode,
        HospitalTypeCode,
        BombFactoryTypeCode,
        OrbFactoryTypeCode,
        100, // Missile factory
        109, // Turret factory
        108, // Wall factory
        102, // Medkit factory
        103, // Cloak factory
    ];

    public static AiCityTemplateResult Apply(World world, TileMap tileMap, CityBuildState build)
    {
        var result = new AiCityTemplateResult();
        var ccX = build.CommandCenterGridX;
        var ccY = build.CommandCenterGridY;

        while (build.CurrentBuildingCount < EconomyConstants.OrbableSize)
        {
            if (!TryChooseType(world, build.CityId, out var typeCode))
            {
                break;
            }

            if (!TryFindSlot(world, tileMap, build, ccX, ccY, out var anchorX, out var anchorY))
            {
                break;
            }

            var menuIndex = BuildingCatalog.GetMenuIndex(typeCode);
            if (menuIndex < 0)
            {
                break;
            }

            var placement = new CityBuildingPlacement(menuIndex, anchorX, anchorY, typeCode);
            var entity = LevelLoader.SpawnBuilding(world, placement, build.CityId);
            if (!BuildingCatalog.IsHouse(typeCode) && menuIndex < build.CanBuild.Length)
            {
                build.CanBuild[menuIndex] = 2;
            }

            build.RegisterBuildingPlaced(menuIndex, typeCode);
            result.Buildings.Add(entity);
            TryAddOuterWalls(world, tileMap, ccX, ccY, anchorX, anchorY, result.WallTiles);
        }

        if (TryFindBuildingAnchor(world, build.CityId, OrbFactoryTypeCode, out var orbX, out var orbY))
        {
            var bay = BuildingCatalog.GetFactoryBayTile(orbX, orbY);
            if (!TileHasItem(world, bay.GridX, bay.GridY) && !result.WallTiles.Contains((bay.GridX, bay.GridY)))
            {
                result.OrbBay = bay;
            }
        }

        TryAddTurrets(world, tileMap, build.CityId, ccX, ccY, result.WallTiles, result.TurretTiles);
        return result;
    }

    /// <summary>
    /// Staff houses so factories and the hospital start populated and are not erased by the first laser.
    /// </summary>
    public static void Staff(World world, int cityId)
    {
        var houses = new List<Entity>();
        var workers = new List<Entity>();
        var query = new QueryDescription().WithAll<BuildingRef, BuildingState>();
        world.Query(
            in query,
            (Entity entity, ref BuildingRef building, ref BuildingState _) =>
            {
                if (building.CityId != cityId || building.NetworkId == 0)
                {
                    return;
                }

                if (building.TypeCode == HouseTypeCode)
                {
                    houses.Add(entity);
                }
                else if (BuildingPopulationSystem.NeedsHouseStaffing(building.TypeCode))
                {
                    workers.Add(entity);
                }
            });

        foreach (var workerEntity in workers)
        {
            if (!world.IsAlive(workerEntity))
            {
                continue;
            }

            ref var workerBuilding = ref world.Get<BuildingRef>(workerEntity);
            ref var workerState = ref world.Get<BuildingState>(workerEntity);
            if (workerState.Population <= 0)
            {
                workerState.Population = EconomyConstants.PopulationMaxNonHouse;
            }

            if (workerState.AttachedHouseNetworkId != 0 || workerBuilding.NetworkId == 0)
            {
                continue;
            }

            foreach (var houseEntity in houses)
            {
                if (!world.IsAlive(houseEntity))
                {
                    continue;
                }

                ref var houseBuilding = ref world.Get<BuildingRef>(houseEntity);
                ref var houseState = ref world.Get<BuildingState>(houseEntity);
                if (houseState.AttachedBuildingNetworkId1 == 0)
                {
                    houseState.AttachedBuildingNetworkId1 = workerBuilding.NetworkId;
                    workerState.AttachedHouseNetworkId = houseBuilding.NetworkId;
                    break;
                }

                if (houseState.AttachedBuildingNetworkId2 == 0)
                {
                    houseState.AttachedBuildingNetworkId2 = workerBuilding.NetworkId;
                    workerState.AttachedHouseNetworkId = houseBuilding.NetworkId;
                    break;
                }
            }
        }

        foreach (var houseEntity in houses)
        {
            if (!world.IsAlive(houseEntity))
            {
                continue;
            }

            ref var houseState = ref world.Get<BuildingState>(houseEntity);
            var pop1 = PopulationOf(world, houseState.AttachedBuildingNetworkId1);
            var pop2 = PopulationOf(world, houseState.AttachedBuildingNetworkId2);
            houseState.Population = pop1 + pop2;
        }
    }

    private static bool TryFindSlot(
        World world,
        TileMap tileMap,
        CityBuildState build,
        int ccX,
        int ccY,
        out int anchorX,
        out int anchorY)
    {
        var bestDistance = int.MaxValue;
        var found = false;
        anchorX = 0;
        anchorY = 0;
        for (var dy = -18; dy <= 18; dy += 3)
        {
            for (var dx = -18; dx <= 18; dx += 3)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var distance = (dx * dx) + (dy * dy);
                if (distance >= bestDistance)
                {
                    continue;
                }

                var candidateX = ccX + dx;
                var candidateY = ccY + dy;
                if (!BuildingPlacementValidator.CanPlace(world, tileMap, build, candidateX, candidateY))
                {
                    continue;
                }

                bestDistance = distance;
                anchorX = candidateX;
                anchorY = candidateY;
                found = true;
            }
        }

        return found;
    }

    private static bool TryChooseType(World world, int cityId, out int typeCode)
    {
        foreach (var featured in FeaturedTypeCodes)
        {
            if (featured == HouseTypeCode)
            {
                if (!CityHasType(world, cityId, HouseTypeCode))
                {
                    typeCode = HouseTypeCode;
                    return true;
                }

                continue;
            }

            if (!CityHasType(world, cityId, featured))
            {
                typeCode = featured;
                return true;
            }
        }

        typeCode = HouseTypeCode;
        return true;
    }

    private static void TryAddTurrets(
        World world,
        TileMap tileMap,
        int cityId,
        int ccX,
        int ccY,
        List<(int X, int Y)> walls,
        List<(int X, int Y)> turrets)
    {
        var needed = TurretCount - CountCityItems(world, cityId, ItemType.Turret);
        if (needed <= 0)
        {
            return;
        }

        for (var radius = 2; radius <= 14 && turrets.Count < needed; radius++)
        {
            for (var dy = -radius; dy <= radius && turrets.Count < needed; dy++)
            {
                for (var dx = -radius; dx <= radius && turrets.Count < needed; dx++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                    {
                        continue;
                    }

                    var tile = (ccX + dx, ccY + dy);
                    if (walls.Contains(tile) || turrets.Contains(tile) || !CanPlaceWall(world, tileMap, tile.Item1, tile.Item2))
                    {
                        continue;
                    }

                    turrets.Add(tile);
                }
            }
        }
    }

    private static void TryAddOuterWalls(
        World world,
        TileMap tileMap,
        int ccX,
        int ccY,
        int anchorX,
        int anchorY,
        List<(int X, int Y)> walls)
    {
        if (walls.Count >= MaxWalls)
        {
            return;
        }

        var dx = anchorX - ccX;
        var dy = anchorY - ccY;
        var faceX = Math.Abs(dx) >= Math.Abs(dy);
        var tiles = new List<(int X, int Y)>(3);
        if (faceX)
        {
            var x = dx >= 0 ? anchorX + 1 : anchorX - 3;
            tiles.Add((x, anchorY - 2));
            tiles.Add((x, anchorY - 1));
            tiles.Add((x, anchorY));
        }
        else
        {
            var y = dy >= 0 ? anchorY + 1 : anchorY - 3;
            tiles.Add((anchorX - 2, y));
            tiles.Add((anchorX - 1, y));
            tiles.Add((anchorX, y));
        }

        foreach (var tile in tiles)
        {
            if (walls.Count >= MaxWalls)
            {
                return;
            }

            if (walls.Contains(tile) || !CanPlaceWall(world, tileMap, tile.X, tile.Y))
            {
                continue;
            }

            walls.Add(tile);
        }
    }

    private static bool CanPlaceWall(World world, TileMap tileMap, int tileX, int tileY)
    {
        if (tileX < 0 || tileY < 0 || tileX >= TileMap.Size || tileY >= TileMap.Size)
        {
            return false;
        }

        if (tileMap.Terrain[tileX, tileY] != TerrainTileType.Open)
        {
            return false;
        }

        if (TileInsideAnyBuilding(world, tileX, tileY) || TileHasItem(world, tileX, tileY))
        {
            return false;
        }

        return true;
    }

    private static bool TileInsideAnyBuilding(World world, int tileX, int tileY)
    {
        var inside = false;
        var query = new QueryDescription().WithAll<BuildingRef>();
        world.Query(
            in query,
            (ref BuildingRef building) =>
            {
                if (inside)
                {
                    return;
                }

                if (tileX >= building.GridAnchorX - 2
                    && tileX <= building.GridAnchorX
                    && tileY >= building.GridAnchorY - 2
                    && tileY <= building.GridAnchorY)
                {
                    inside = true;
                }
            });

        return inside;
    }

    private static bool TileHasItem(World world, int tileX, int tileY)
    {
        var occupied = false;
        var query = new QueryDescription().WithAll<PlacedItemRef>();
        world.Query(
            in query,
            (ref PlacedItemRef item) =>
            {
                if (!occupied && item.GridX == tileX && item.GridY == tileY)
                {
                    occupied = true;
                }
            });

        return occupied;
    }

    private static int CountCityItems(World world, int cityId, ItemType type)
    {
        var count = 0;
        var query = new QueryDescription().WithAll<PlacedItemRef>();
        world.Query(
            in query,
            (ref PlacedItemRef item) =>
            {
                if (item.CityId == cityId && item.Type == type)
                {
                    count++;
                }
            });

        return count;
    }

    private static bool TryFindBuildingAnchor(World world, int cityId, int typeCode, out int anchorX, out int anchorY)
    {
        var foundX = 0;
        var foundY = 0;
        var found = false;
        var query = new QueryDescription().WithAll<BuildingRef>();
        world.Query(
            in query,
            (ref BuildingRef building) =>
            {
                if (found || building.CityId != cityId || building.TypeCode != typeCode)
                {
                    return;
                }

                foundX = building.GridAnchorX;
                foundY = building.GridAnchorY;
                found = true;
            });

        anchorX = foundX;
        anchorY = foundY;
        return found;
    }

    private static bool CityHasType(World world, int cityId, int typeCode)
    {
        var found = false;
        var query = new QueryDescription().WithAll<BuildingRef>();
        world.Query(
            in query,
            (ref BuildingRef building) =>
            {
                if (!found && building.CityId == cityId && building.TypeCode == typeCode)
                {
                    found = true;
                }
            });

        return found;
    }

    private static int PopulationOf(World world, ushort networkId)
    {
        if (networkId == 0)
        {
            return 0;
        }

        var population = 0;
        var query = new QueryDescription().WithAll<BuildingRef, BuildingState>();
        world.Query(
            in query,
            (ref BuildingRef building, ref BuildingState state) =>
            {
                if (building.NetworkId == networkId)
                {
                    population = state.Population;
                }
            });

        return population;
    }
}

public sealed class AiCityTemplateResult
{
    public List<Entity> Buildings { get; } = [];

    public List<(int X, int Y)> WallTiles { get; } = [];

    public List<(int X, int Y)> TurretTiles { get; } = [];

    public (int X, int Y)? OrbBay { get; set; }
}
