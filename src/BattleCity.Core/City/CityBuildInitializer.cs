using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.City;

public static class CityBuildInitializer
{
    public static void ApplyLegacyStartingPermissions(CityBuildState build)
    {
        Array.Clear(build.CanBuild, 0, build.CanBuild.Length);
        build.CanBuild[1] = 1; // House
        build.CanBuild[2] = 1; // Laser Research
        build.CanBuild[4] = 1; // Turret Research
    }

    public static void InitializeFromLayout(CityBuildState build, CityLayout layout, TileMap tileMap)
    {
        ApplyLegacyStartingPermissions(build);
        MarkExistingBuildings(build, layout);
        ResolveCommandCenter(build, layout, tileMap);
        build.CurrentBuildingCount = Math.Max(1, layout.Buildings.Count);
        build.MaxBuildingCount = build.CurrentBuildingCount;
    }

    public static void MarkExistingBuildings(CityBuildState build, CityLayout layout)
    {
        foreach (var building in layout.Buildings)
        {
            var menuIndex = BuildingCatalog.GetMenuIndex(building.TypeCode);
            if (menuIndex < 0)
            {
                continue;
            }

            if (BuildingCatalog.IsHouse(building.TypeCode))
            {
                continue;
            }

            build.CanBuild[menuIndex] = 2;

            if (BuildingCatalog.TryGetResearchTreeIndex(building.TypeCode, out var treeIndex))
            {
                build.ResearchStatus[treeIndex] = -1;
                var factoryIndex = BuildingCatalog.GetFactoryMenuIndex(treeIndex);
                // Do not reopen a factory that already exists in the layout (CanBuild == 2).
                if (factoryIndex < build.CanBuild.Length && build.CanBuild[factoryIndex] != 2)
                {
                    build.CanBuild[factoryIndex] = 1;
                }
            }

            if (building.TypeCode == 103)
            {
                build.HadBombFactory = true;
            }
            else if (building.TypeCode == 105)
            {
                build.HadOrbFactory = true;
            }
        }
    }

    /// <summary>
    /// Legacy <c>CMap::CalculateTiles</c>: scan map top-to-bottom, left-to-right, assign
    /// CityCenter clusters to city ids 63→0. Never fall back onto open/lava tiles.
    /// </summary>
    public static void ResolveCommandCenter(CityBuildState build, CityLayout layout, TileMap tileMap)
    {
        if (TryResolveByCityIndex(build, tileMap))
        {
            return;
        }

        // Fallback: nearest CityCenter terrain cluster to the layout centroid (never lava/open).
        var focus = layout.GetCameraFocus();
        var centerGridX = (int)(focus.X / GameConstants.TileSize);
        var centerGridY = (int)(focus.Y / GameConstants.TileSize);
        var bestDistance = int.MaxValue;
        var bestX = centerGridX;
        var bestY = centerGridY;
        var found = false;

        for (var y = 1; y < TileMap.Size - 1; y++)
        {
            for (var x = 1; x < TileMap.Size - 1; x++)
            {
                if (!IsCityCenterClusterOrigin(tileMap, x, y))
                {
                    continue;
                }

                var dx = x - centerGridX;
                var dy = y - centerGridY;
                var distance = dx * dx + dy * dy;
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestX = x;
                bestY = y;
                found = true;
            }
        }

        if (found)
        {
            build.CommandCenterGridX = bestX + GameConstants.BuildingCollisionOffset;
            build.CommandCenterGridY = bestY + GameConstants.BuildingCollisionOffset;
            return;
        }

        // Empty/test maps with no CityCenter tiles: use layout focus only on open ground.
        var focusTileX = (int)(focus.X / GameConstants.TileSize);
        var focusTileY = (int)(focus.Y / GameConstants.TileSize);
        if (!IsHazardTerrain(tileMap, focusTileX, focusTileY))
        {
            build.CommandCenterGridX = focusTileX + GameConstants.BuildingCollisionOffset;
            build.CommandCenterGridY = focusTileY + GameConstants.BuildingCollisionOffset;
        }
    }

    private static bool IsHazardTerrain(TileMap tileMap, int tileX, int tileY)
    {
        if (tileX < 0 || tileY < 0 || tileX >= TileMap.Size || tileY >= TileMap.Size)
        {
            return true;
        }

        var terrain = tileMap.Terrain[tileX, tileY];
        return terrain is TerrainTileType.Lava or TerrainTileType.Rock;
    }

    private static bool TryResolveByCityIndex(CityBuildState build, TileMap tileMap)
    {
        if (!CityCatalog.IsValidCityId(build.CityId))
        {
            return false;
        }

        var citIndex = 63;
        for (var y = 1; y < TileMap.Size - 1; y++)
        {
            for (var x = 1; x < TileMap.Size - 1; x++)
            {
                if (!IsCityCenterClusterOrigin(tileMap, x, y))
                {
                    continue;
                }

                if (citIndex == build.CityId)
                {
                    build.CommandCenterGridX = x + GameConstants.BuildingCollisionOffset;
                    build.CommandCenterGridY = y + GameConstants.BuildingCollisionOffset;
                    return true;
                }

                citIndex--;
                if (citIndex < 0)
                {
                    return false;
                }
            }
        }

        return false;
    }

    private static bool IsCityCenterClusterOrigin(TileMap tileMap, int x, int y)
    {
        if (tileMap.Terrain[x, y] != TerrainTileType.CityCenter)
        {
            return false;
        }

        return tileMap.Terrain[x - 1, y] != TerrainTileType.CityCenter
            && tileMap.Terrain[x, y - 1] != TerrainTileType.CityCenter;
    }
}
