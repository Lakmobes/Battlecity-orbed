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
                if (factoryIndex < build.CanBuild.Length)
                {
                    build.CanBuild[factoryIndex] = 1;
                }
            }

            if (building.TypeCode == 105)
            {
                build.HadBombFactory = true;
            }
            else if (building.TypeCode == 106)
            {
                build.HadOrbFactory = true;
            }
        }
    }

    public static void ResolveCommandCenter(CityBuildState build, CityLayout layout, TileMap tileMap)
    {
        var focus = layout.GetCameraFocus();
        var centerGridX = (int)(focus.X / GameConstants.TileSize);
        var centerGridY = (int)(focus.Y / GameConstants.TileSize);
        var searchRadius = 48;

        for (var dx = -searchRadius; dx <= searchRadius; dx++)
        {
            for (var dy = -searchRadius; dy <= searchRadius; dy++)
            {
                var x = centerGridX + dx;
                var y = centerGridY + dy;
                if (x < 0 || y < 0 || x >= TileMap.Size || y >= TileMap.Size)
                {
                    continue;
                }

                if (tileMap.Terrain[x, y] == TerrainTileType.CityCenter)
                {
                    build.CommandCenterGridX = x + GameConstants.BuildingCollisionOffset;
                    build.CommandCenterGridY = y + GameConstants.BuildingCollisionOffset;
                    return;
                }
            }
        }

        build.CommandCenterGridX = centerGridX + GameConstants.BuildingCollisionOffset;
        build.CommandCenterGridY = centerGridY + GameConstants.BuildingCollisionOffset;
    }
}
