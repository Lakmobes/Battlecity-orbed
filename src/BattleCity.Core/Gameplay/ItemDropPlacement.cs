using System.Numerics;

using Arch.Core;

using BattleCity.Core.City;
using BattleCity.Core.Collision;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

/// <summary>Resolves legacy item drop tiles (defensive items avoid the tank tile).</summary>
public static class ItemDropPlacement
{
    public static bool RequiresDedicatedTile(ItemType type) => type >= ItemType.Wall;

    public static bool TryFindDropTile(
        World world,
        Entity owner,
        Vector2 tankTopLeft,
        ItemType type,
        out int gridX,
        out int gridY,
        CityBuildState? cityBuild = null)
    {
        if (RequiresDedicatedTile(type)
            && cityBuild is not null
            && !DefensiveItemRangeValidator.IsWithinRange(world, cityBuild, tankTopLeft))
        {
            gridX = 0;
            gridY = 0;
            return false;
        }

        foreach (var candidate in EnumerateCandidateTiles(world, owner, tankTopLeft, type))
        {
            if (!IsInsideMap(candidate.GridX, candidate.GridY))
            {
                continue;
            }

            if (ItemDropActions.CanPlaceItem(world, owner, candidate.GridX, candidate.GridY, type))
            {
                gridX = candidate.GridX;
                gridY = candidate.GridY;
                return true;
            }
        }

        gridX = 0;
        gridY = 0;
        return false;
    }

    private static IEnumerable<(int GridX, int GridY)> EnumerateCandidateTiles(
        World world,
        Entity owner,
        Vector2 tankTopLeft,
        ItemType type)
    {
        var (playerGridX, playerGridY) = TankPlacement.GetTileFromTopLeft(tankTopLeft);

        if (!RequiresDedicatedTile(type))
        {
            yield return (playerGridX, playerGridY);
            yield break;
        }

        if (world.Has<TankFacing>(owner))
        {
            var travelDirection = InputSystem.ToTravelDirection(world.Get<TankFacing>(owner).Direction);
            var (forwardDx, forwardDy) = GetForwardTileOffset(travelDirection);
            if (forwardDx != 0 || forwardDy != 0)
            {
                yield return (playerGridX + forwardDx, playerGridY + forwardDy);
            }
        }

        foreach (var (dx, dy) in NeighborOffsets)
        {
            if (dx == 0 && dy == 0)
            {
                continue;
            }

            yield return (playerGridX + dx, playerGridY + dy);
        }
    }

    private static (int Dx, int Dy) GetForwardTileOffset(int travelDirection)
    {
        var radians = InputSystem.LegacyDirectionToRadians(travelDirection);
        var dx = Math.Sign(MathF.Sin(radians));
        var dy = Math.Sign(MathF.Cos(radians));
        return (dx, dy);
    }

    private static bool IsInsideMap(int gridX, int gridY) =>
        gridX > 0 && gridY > 0 && gridX < TileMap.Size - 1 && gridY < TileMap.Size - 1;

    private static readonly (int Dx, int Dy)[] NeighborOffsets =
    [
        (-1, 0),
        (1, 0),
        (0, -1),
        (0, 1),
        (-1, -1),
        (1, -1),
        (-1, 1),
        (1, 1),
    ];
}
