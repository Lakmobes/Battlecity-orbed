using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.City;

/// <summary>Legacy CCollision::CheckBuildingCollision rules for mayor builds.</summary>
public static class BuildingPlacementValidator
{
    private static readonly QueryDescription BuildingQuery =
        new QueryDescription().WithAll<BuildingRef, Transform2D>();

    private static readonly QueryDescription ItemQuery =
        new QueryDescription().WithAll<PlacedItemRef>();

    public static (int GridX, int GridY) WorldToGridAnchor(Vector2 worldPosition)
    {
        var gridX = (int)(worldPosition.X / GameConstants.TileSize) + GameConstants.BuildingCollisionOffset;
        var gridY = (int)(worldPosition.Y / GameConstants.TileSize) + GameConstants.BuildingCollisionOffset;
        return (gridX, gridY);
    }

    public static bool CanPlace(
        World world,
        TileMap tileMap,
        CityBuildState build,
        int gridAnchorX,
        int gridAnchorY,
        Vector2? playerCenter = null)
    {
        if (!CanPlaceAt(world, tileMap, gridAnchorX, gridAnchorY, playerCenter))
        {
            return false;
        }

        return playerCenter is null || IsWithinMayorBuildRange(build, playerCenter.Value);
    }

    public static bool IsWithinMayorBuildRange(CityBuildState build, Vector2 playerCenter)
    {
        var playerTileX = (int)(playerCenter.X / GameConstants.TileSize);
        var playerTileY = (int)(playerCenter.Y / GameConstants.TileSize);
        return IsWithinCommandCenterTileRange(build, playerTileX, playerTileY);
    }

    public static bool IsWithinCommandCenterTileRange(CityBuildState build, int playerTileX, int playerTileY)
    {
        var ccTileX = build.CommandCenterGridX - 1;
        var ccTileY = build.CommandCenterGridY - 1;

        return Math.Abs(ccTileX - playerTileX) < GameConstants.DistanceMaxFromCommandCenter
            && Math.Abs(ccTileY - playerTileY) < GameConstants.DistanceMaxFromCommandCenter;
    }

    public static bool TryFindBuildingAt(World world, int gridAnchorX, int gridAnchorY, out Entity entity)
    {
        Entity foundEntity = Entity.Null;
        var found = false;

        world.Query(
            in BuildingQuery,
            (Entity candidate, ref BuildingRef building) =>
            {
                if (found)
                {
                    return;
                }

                if (gridAnchorX >= building.GridAnchorX - 2
                    && gridAnchorX <= building.GridAnchorX + 2
                    && gridAnchorY >= building.GridAnchorY - 2
                    && gridAnchorY <= building.GridAnchorY + 2)
                {
                    foundEntity = candidate;
                    found = true;
                }
            });

        entity = foundEntity;
        return found;
    }

    private static bool CanPlaceAt(
        World world,
        TileMap tileMap,
        int gridAnchorX,
        int gridAnchorY,
        Vector2? playerCenter)
    {
        if (gridAnchorX < 0 || gridAnchorY < 0
            || gridAnchorX > GameConstants.MapSize || gridAnchorY > GameConstants.MapSize)
        {
            return false;
        }

        for (var j = 0; j < 3; j++)
        {
            for (var i = 0; i < 3; i++)
            {
                var tileX = gridAnchorX - j;
                var tileY = gridAnchorY - i;
                if (!IsOpenTerrain(tileMap, tileX, tileY))
                {
                    return false;
                }
            }
        }

        if (HasItemNear(world, gridAnchorX, gridAnchorY))
        {
            return false;
        }

        if (HasBuildingNear(world, gridAnchorX, gridAnchorY))
        {
            return false;
        }

        if (playerCenter.HasValue && PlayerOccupiesFootprint(playerCenter.Value, gridAnchorX, gridAnchorY))
        {
            return false;
        }

        return true;
    }

    private static bool IsOpenTerrain(TileMap tileMap, int tileX, int tileY)
    {
        if (tileX < 0 || tileY < 0 || tileX >= TileMap.Size || tileY >= TileMap.Size)
        {
            return false;
        }

        return tileMap.Terrain[tileX, tileY] == TerrainTileType.Open;
    }

    private static bool HasItemNear(World world, int gridAnchorX, int gridAnchorY)
    {
        var found = false;

        world.Query(
            in ItemQuery,
            (ref PlacedItemRef item) =>
            {
                if (found)
                {
                    return;
                }

                for (var j = 0; j < 3; j++)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        if (gridAnchorX == item.GridX + j && gridAnchorY == item.GridY + i)
                        {
                            found = true;
                            return;
                        }
                    }
                }
            });

        return found;
    }

    private static bool HasBuildingNear(World world, int gridAnchorX, int gridAnchorY)
    {
        var found = false;

        world.Query(
            in BuildingQuery,
            (ref BuildingRef building) =>
            {
                if (found)
                {
                    return;
                }

                if (gridAnchorX >= building.GridAnchorX - 2
                    && gridAnchorX <= building.GridAnchorX + 2
                    && gridAnchorY >= building.GridAnchorY - 2
                    && gridAnchorY <= building.GridAnchorY + 2)
                {
                    found = true;
                }
            });

        return found;
    }

    private static bool PlayerOccupiesFootprint(Vector2 playerCenter, int gridAnchorX, int gridAnchorY)
    {
        var playerGridX = (int)(playerCenter.X / GameConstants.TileSize);
        var playerGridY = (int)(playerCenter.Y / GameConstants.TileSize);

        return playerGridX - 1 <= gridAnchorX
            && playerGridX + 1 >= gridAnchorX - 2
            && playerGridY - 1 <= gridAnchorY
            && playerGridY + 1 >= gridAnchorY - 2;
    }
}
