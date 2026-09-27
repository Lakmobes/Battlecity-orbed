using Arch.Core;

using BattleCity.Core.Collision;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
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

    public static void SpawnBuildings(World world, CityLayout layout, int cityId = 0)
    {
        foreach (var building in layout.Buildings)
        {
            SpawnBuilding(world, building, cityId);
        }
    }

    public static Entity SpawnCommandCenter(World world, int gridAnchorX, int gridAnchorY, int cityId = 0) =>
        SpawnBuilding(
            world,
            new CityBuildingPlacement(-1, gridAnchorX, gridAnchorY, BuildingCatalog.CommandCenterTypeCode),
            cityId);

    /// <summary>
    /// Spawns a command-center building on every map city-center tile cluster except the home CC.
    /// City ids follow the legacy 63→0 CityCenter scan order.
    /// </summary>
    public static void SpawnRemoteCommandCenters(
        World world,
        TileMap tileMap,
        int homeGridAnchorX,
        int homeGridAnchorY)
    {
        foreach (var (cityId, gridAnchorX, gridAnchorY) in EnumerateCommandCenters(tileMap))
        {
            if (OverlapsFootprint(gridAnchorX, gridAnchorY, homeGridAnchorX, homeGridAnchorY))
            {
                continue;
            }

            SpawnCommandCenter(world, gridAnchorX, gridAnchorY, cityId);
        }
    }

    /// <summary>
    /// Spawns every map command center with its legacy city id (63→0). Used for multiplayer
    /// CC-only world boot (no <c>.city</c> demo buildings).
    /// </summary>
    public static int SpawnAllCommandCenters(World world, TileMap tileMap)
    {
        ClearHazardsAroundCommandCenters(tileMap);

        var count = 0;
        foreach (var (cityId, gridAnchorX, gridAnchorY) in EnumerateCommandCenters(tileMap))
        {
            SpawnCommandCenter(world, gridAnchorX, gridAnchorY, cityId);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Some map.dat CCs (e.g. Cordoba, Rio Cuarto) have lava under the footprint / drive bay.
    /// Clear hazards so tanks can spawn and the CC is not visually buried in lava.
    /// </summary>
    public static void ClearHazardsAroundCommandCenters(TileMap tileMap)
    {
        foreach (var (_, gridAnchorX, gridAnchorY) in EnumerateCommandCenters(tileMap))
        {
            for (var j = 0; j < 3; j++)
            {
                for (var i = 0; i < 3; i++)
                {
                    ClearHazardTerrain(tileMap, gridAnchorX - j, gridAnchorY - i);
                }

                // Southern drive bay — one row below the 3×3 footprint.
                ClearHazardTerrain(tileMap, gridAnchorX - j, gridAnchorY + 1);
            }
        }
    }

    private static void ClearHazardTerrain(TileMap tileMap, int tileX, int tileY)
    {
        if (tileX < 0 || tileY < 0 || tileX >= TileMap.Size || tileY >= TileMap.Size)
        {
            return;
        }

        if (tileMap.Terrain[tileX, tileY] is TerrainTileType.Lava or TerrainTileType.Rock)
        {
            tileMap.Terrain[tileX, tileY] = TerrainTileType.Open;
        }
    }

    /// <summary>Legacy 63→0 CityCenter cluster scan.</summary>
    public static IEnumerable<(int CityId, int GridAnchorX, int GridAnchorY)> EnumerateCommandCenters(
        TileMap tileMap)
    {
        var citIndex = 63;
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
                yield return (citIndex, gridAnchorX, gridAnchorY);
                citIndex--;
                if (citIndex < 0)
                {
                    yield break;
                }
            }
        }
    }

    private static bool OverlapsFootprint(int gridAnchorX, int gridAnchorY, int otherGridX, int otherGridY) =>
        gridAnchorX >= otherGridX - 2
        && gridAnchorX <= otherGridX + 2
        && gridAnchorY >= otherGridY - 2
        && gridAnchorY <= otherGridY + 2;

    public static Entity SpawnBuilding(World world, CityBuildingPlacement building, int cityId = 0)
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
                CityId = cityId,
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
