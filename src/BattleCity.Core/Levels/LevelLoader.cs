using Arch.Core;

using BattleCity.Core.Collision;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Levels;

public static class LevelLoader
{
    public static CityLayout LoadLegacyCity(string cityName, string layoutName = "demo")
    {
        var path = CityLayoutPaths.FindLegacyCityLayout(cityName, layoutName)
            ?? throw new FileNotFoundException(
                $"Legacy city layout '{layoutName}' for '{cityName}' was not found under legacy/data/cities/.");

        return CityLayoutParser.ParseFile(path, cityName);
    }

    public static void SpawnBuildings(World world, CityLayout layout)
    {
        foreach (var building in layout.Buildings)
        {
            SpawnBuilding(world, building);
        }
    }

    public static Entity SpawnCommandCenter(World world, int gridAnchorX, int gridAnchorY) =>
        SpawnBuilding(
            world,
            new CityBuildingPlacement(-1, gridAnchorX, gridAnchorY, BuildingCatalog.CommandCenterTypeCode));

    /// <summary>
    /// Spawns a command-center building on every map city-center tile cluster except the home CC.
    /// Offline clients do not load other cities' .city files, but CCs should still be visible.
    /// </summary>
    public static void SpawnRemoteCommandCenters(
        World world,
        TileMap tileMap,
        int homeGridAnchorX,
        int homeGridAnchorY)
    {
        for (var y = 1; y < TileMap.Size - 1; y++)
        {
            for (var x = 1; x < TileMap.Size - 1; x++)
            {
                if (tileMap.Terrain[x, y] != TerrainTileType.CityCenter)
                {
                    continue;
                }

                // Top-left tile of a contiguous city-center region.
                if (tileMap.Terrain[x - 1, y] == TerrainTileType.CityCenter
                    || tileMap.Terrain[x, y - 1] == TerrainTileType.CityCenter)
                {
                    continue;
                }

                var gridAnchorX = x + GameConstants.BuildingCollisionOffset;
                var gridAnchorY = y + GameConstants.BuildingCollisionOffset;
                if (OverlapsFootprint(gridAnchorX, gridAnchorY, homeGridAnchorX, homeGridAnchorY))
                {
                    continue;
                }

                SpawnCommandCenter(world, gridAnchorX, gridAnchorY);
            }
        }
    }

    private static bool OverlapsFootprint(int gridAnchorX, int gridAnchorY, int otherGridX, int otherGridY) =>
        gridAnchorX >= otherGridX - 2
        && gridAnchorX <= otherGridX + 2
        && gridAnchorY >= otherGridY - 2
        && gridAnchorY <= otherGridY + 2;

    public static Entity SpawnBuilding(World world, CityBuildingPlacement building)
    {
        var position = BuildingPlacement.GridAnchorToWorldPosition(building.GridX, building.GridY);
        var animationFrame = Random.Shared.Next(0, 6);
        var (sourceX, sourceY) = BuildingSprites.GetSourceOrigin(building.TypeCode, animationFrame);

        var (offsetX, offsetY, width, height) = BuildingCollision.GetPlayerColliderShape(building.TypeCode);

        return world.Create(
            new Transform2D { Position = position, PreviousPosition = position },
            new BuildingRef
            {
                MenuIndex = building.MenuIndex,
                TypeCode = building.TypeCode,
                GridAnchorX = building.GridX,
                GridAnchorY = building.GridY,
            },
            new BuildingState
            {
                Population = GetInitialPopulation(building.TypeCode),
                ItemsLeft = 0,
                AnimationFrame = animationFrame,
                AnimationCooldownSeconds = 0.5f,
            },
            new SpriteRef
            {
                TextureKey = BuildingSprites.TextureKey,
                SourceX = sourceX,
                SourceY = sourceY,
                Width = BuildingSprites.SpriteSize,
                Height = BuildingSprites.SpriteSize,
            },
            new Collider
            {
                OffsetX = offsetX,
                OffsetY = offsetY,
                Width = width,
                Height = height,
                Layer = CollisionLayer.Building,
            });
    }

    private static int GetInitialPopulation(int typeCode) => 0;
}
