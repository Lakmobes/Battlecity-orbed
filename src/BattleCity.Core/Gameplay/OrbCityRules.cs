using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

/// <summary>A city may only have one orb at a time (carried or sitting on the map).</summary>
public static class OrbCityRules
{
    private static readonly QueryDescription ItemQuery =
        new QueryDescription().WithAll<PlacedItemRef>();

    private static readonly QueryDescription InventoryQuery =
        new QueryDescription().WithAll<PlayerInventory, CityAffiliation>();

    public static bool CityHasPlacedOrb(World world, int cityId, Entity? exceptItem = null)
    {
        var found = false;

        world.Query(
            in ItemQuery,
            (Entity entity, ref PlacedItemRef item) =>
            {
                if (found
                    || item.Type != ItemType.Orb
                    || item.CityId != cityId)
                {
                    return;
                }

                if (exceptItem.HasValue && entity == exceptItem.Value)
                {
                    return;
                }

                found = true;
            });

        return found;
    }

    public static bool CityHasCarriedOrb(World world, int cityId, Entity? exceptOwner = null)
    {
        var found = false;

        world.Query(
            in InventoryQuery,
            (Entity entity, ref PlayerInventory inventory, ref CityAffiliation city) =>
            {
                if (found
                    || city.CityId != cityId
                    || inventory.GetCount(ItemType.Orb) <= 0)
                {
                    return;
                }

                if (exceptOwner.HasValue && entity == exceptOwner.Value)
                {
                    return;
                }

                found = true;
            });

        return found;
    }

    public static bool CityAlreadyHasOrb(
        World world,
        int cityId,
        Entity? exceptOwner = null,
        Entity? exceptItem = null) =>
        CityHasPlacedOrb(world, cityId, exceptItem) || CityHasCarriedOrb(world, cityId, exceptOwner);

    public static bool CanAddOrbToCity(World world, int cityId) => !CityAlreadyHasOrb(world, cityId);
}
