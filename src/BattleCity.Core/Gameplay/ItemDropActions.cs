using System.Numerics;

using Arch.Core;

using BattleCity.Core.City;
using BattleCity.Core.Collision;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

/// <summary>Shared item drop/pickup rules for offline sim and authoritative server.</summary>
public static class ItemDropActions
{
    /// <summary>After dropping a solid under the tank: try left, then down, then right.</summary>
    private static readonly (int Dx, int Dy)[] SolidDropNudgeOffsets =
    [
        (-1, 0),
        (0, 1),
        (1, 0),
    ];

    public static bool TryDropForEntity(
        World world,
        Entity owner,
        Vector2 tankTopLeft,
        ItemType type,
        bool active,
        out int gridX,
        out int gridY,
        ushort networkItemId = 0,
        CityBuildState? cityBuild = null,
        TileMap? tileMap = null)
    {
        if (!ItemDropPlacement.TryFindDropTile(world, owner, tankTopLeft, type, out gridX, out gridY, cityBuild))
        {
            return false;
        }

        var cityId = world.Has<CityAffiliation>(owner)
            ? world.Get<CityAffiliation>(owner).CityId
            : 0;

        GameplayEntityFactory.CreatePlacedItem(
            world,
            type,
            gridX,
            gridY,
            active,
            cityId: cityId,
            networkItemId: networkItemId);

        if (ItemDropPlacement.RequiresDedicatedTile(type) && tileMap is not null)
        {
            TryNudgeTankOffSolid(world, tileMap, owner);
        }

        return true;
    }

    public static bool CanPlaceItem(World world, Entity owner, int gridX, int gridY, ItemType type)
    {
        // Soft items (bomb/mine/orb/dfg) may share a tile. Solids need a free tile.
        if (!ItemDropPlacement.RequiresDedicatedTile(type))
        {
            return true;
        }

        var tileSize = GameConstants.TileSize;
        var worldPos = PlacedItemPlacement.GridToWorldPosition(gridX, gridY);
        var bounds = new AxisAlignedBox(worldPos.X, worldPos.Y, tileSize, tileSize);

        if (CollisionQueries.IntersectsItemCollider(world, owner, bounds)
            || HasPlacedItemAt(world, gridX, gridY))
        {
            return false;
        }

        return true;
    }

    public static bool TryNudgeTankOffSolid(World world, TileMap map, Entity owner)
    {
        if (!world.Has<Transform2D>(owner) || !world.Has<Collider>(owner))
        {
            return false;
        }

        ref var transform = ref world.Get<Transform2D>(owner);
        ref var collider = ref world.Get<Collider>(owner);
        var tileSize = GameConstants.TileSize;

        foreach (var (dx, dy) in SolidDropNudgeOffsets)
        {
            var candidate = transform.Position + new Vector2(dx * tileSize, dy * tileSize);
            if (CollisionQueries.CheckPlayerCollision(world, map, owner, candidate, collider)
                != PlayerCollisionResult.None)
            {
                continue;
            }

            transform.PreviousPosition = transform.Position;
            transform.Position = candidate;
            return true;
        }

        return false;
    }

    private static bool HasPlacedItemAt(World world, int gridX, int gridY)
    {
        var query = new QueryDescription().WithAll<PlacedItemRef>();
        var occupied = false;

        world.Query(
            in query,
            (ref PlacedItemRef item) =>
            {
                if (!occupied && item.GridX == gridX && item.GridY == gridY)
                {
                    occupied = true;
                }
            });

        return occupied;
    }
}
