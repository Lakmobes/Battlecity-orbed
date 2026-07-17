using System.Numerics;

using Arch.Core;

using BattleCity.Core.City;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Maps;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

/// <summary>
/// Resolves item drop tiles. Legacy drops on the tank tile
/// (<c>CProcess::ProcessItemDrop</c> → <c>getTileX/Y</c>); solids then nudge the tank clear.
/// </summary>
public static class ItemDropPlacement
{
    /// <summary>Walls/turrets/etc. block movement and need a free tile.</summary>
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

        var (playerGridX, playerGridY) = TankPlacement.GetTileFromTopLeft(tankTopLeft);
        if (!IsInsideMap(playerGridX, playerGridY))
        {
            gridX = 0;
            gridY = 0;
            return false;
        }

        if (!ItemDropActions.CanPlaceItem(world, owner, playerGridX, playerGridY, type))
        {
            gridX = 0;
            gridY = 0;
            return false;
        }

        gridX = playerGridX;
        gridY = playerGridY;
        return true;
    }

    private static bool IsInsideMap(int gridX, int gridY) =>
        gridX > 0 && gridY > 0 && gridX < TileMap.Size - 1 && gridY < TileMap.Size - 1;
}
