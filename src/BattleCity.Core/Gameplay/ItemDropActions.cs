using System.Numerics;

using Arch.Core;

using BattleCity.Core.City;
using BattleCity.Core.Collision;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

/// <summary>Shared item drop/pickup rules for offline sim and authoritative server.</summary>
public static class ItemDropActions
{
    public static bool TryDropForEntity(
        World world,
        Entity owner,
        Vector2 tankTopLeft,
        ItemType type,
        bool active,
        out int gridX,
        out int gridY,
        ushort networkItemId = 0,
        CityBuildState? cityBuild = null)
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
        return true;
    }

    public static bool CanPlaceItem(World world, Entity owner, int gridX, int gridY, ItemType type)
    {
        var tileSize = GameConstants.TileSize;
        var worldPos = type is ItemType.Turret or ItemType.Sleeper or ItemType.Plasma
            ? GameplayEntityFactory.LegacyItemWorldPosition(gridX, gridY)
            : PlacedItemPlacement.GridToWorldPosition(gridX, gridY);
        var bounds = new AxisAlignedBox(worldPos.X, worldPos.Y, tileSize, tileSize);

        if (CollisionQueries.IntersectsBlockingCollider(world, owner, bounds))
        {
            return false;
        }

        if (ItemDropPlacement.RequiresDedicatedTile(type)
            && world.Has<Transform2D>(owner)
            && world.Has<Collider>(owner))
        {
            var tankBounds = AxisAlignedBox.FromCollider(
                world.Get<Transform2D>(owner).Position,
                world.Get<Collider>(owner));
            if (bounds.Intersects(tankBounds))
            {
                return false;
            }
        }

        return true;
    }
}
