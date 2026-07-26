using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

/// <summary>Shared item pickup rules for offline sim and authoritative server.</summary>
public static class ItemPickupActions
{
    public static bool TryFindItemAtTank(
        World world,
        Entity player,
        out Entity itemEntity,
        out ItemType itemType,
        out ushort networkItemId,
        int? mapCityId = null)
    {
        itemEntity = Entity.Null;
        itemType = default;
        networkItemId = 0;

        var cityId = world.Has<CityAffiliation>(player)
            ? world.Get<CityAffiliation>(player).CityId
            : 0;

        var tankTopLeft = world.Get<Transform2D>(player).Position;
        var centerGridX = (int)(tankTopLeft.X + GameConstants.TileSize / 2f) / GameConstants.TileSize;
        var centerGridY = (int)(tankTopLeft.Y + GameConstants.TileSize / 2f) / GameConstants.TileSize;

        var itemQuery = new QueryDescription().WithAll<PlacedItemRef>();
        var found = false;
        var foundEntity = Entity.Null;
        var foundType = default(ItemType);
        var foundNetworkId = (ushort)0;

        world.Query(
            in itemQuery,
            (Entity entity, ref PlacedItemRef item) =>
            {
                if (found || item.GridX != centerGridX || item.GridY != centerGridY)
                {
                    return;
                }

                // Own-city items, plus shared-map factory stock tagged with the layout city.
                if (item.CityId != cityId
                    && !(mapCityId.HasValue && item.CityId == mapCityId.Value))
                {
                    return;
                }

                foundEntity = entity;
                foundType = item.Type;
                found = true;
                if (world.Has<NetworkItemRef>(entity))
                {
                    foundNetworkId = world.Get<NetworkItemRef>(entity).ItemId;
                }
            });

        if (!found)
        {
            return false;
        }

        itemEntity = foundEntity;
        itemType = foundType;
        networkItemId = foundNetworkId;
        return true;
    }

    public static bool TryFindNetworkItem(World world, ushort itemId, out Entity itemEntity, out ItemType itemType)
    {
        itemEntity = Entity.Null;
        itemType = default;

        if (itemId == 0)
        {
            return false;
        }

        var found = false;
        var foundEntity = Entity.Null;
        var foundType = default(ItemType);
        var query = new QueryDescription().WithAll<NetworkItemRef, PlacedItemRef>();
        world.Query(
            in query,
            (Entity entity, ref NetworkItemRef networkItem, ref PlacedItemRef item) =>
            {
                if (found || networkItem.ItemId != itemId)
                {
                    return;
                }

                foundEntity = entity;
                foundType = item.Type;
                found = true;
            });

        if (!found)
        {
            return false;
        }

        itemEntity = foundEntity;
        itemType = foundType;
        return true;
    }

    public static bool TryPickUp(
        World world,
        Entity player,
        ref PlayerInventory inventory,
        Entity itemEntity,
        ItemType itemType)
    {
        if (itemType == ItemType.Orb)
        {
            var cityId = world.Has<CityAffiliation>(player)
                ? world.Get<CityAffiliation>(player).CityId
                : 0;
            // Picking up this city's orb is fine; refuse if another orb already exists for the city.
            if (OrbCityRules.CityAlreadyHasOrb(
                    world,
                    cityId,
                    exceptOwner: player,
                    exceptItem: itemEntity))
            {
                return false;
            }
        }

        if (!inventory.TryAdd(itemType))
        {
            return false;
        }

        world.Destroy(itemEntity);
        return true;
    }
}
