using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Systems;

/// <summary>Orb dropped on command center (legacy/server/CItem.cpp).</summary>
public static class OrbSystem
{
    private static readonly QueryDescription OrbQuery =
        new QueryDescription().WithAll<PlacedItemRef>();

    public static bool TryTrigger(World world, CityBuildState build, out int attackerCityId) =>
        TryTrigger(world, [build], out _, out attackerCityId, out _);

    /// <summary>
    /// Scans inactive orbs against every orbable enemy city CC (legacy drop-time check).
    /// </summary>
    public static bool TryTrigger(
        World world,
        IEnumerable<CityBuildState> cities,
        out int victimCityId,
        out int attackerCityId,
        out ushort removedNetworkItemId)
    {
        victimCityId = 0;
        attackerCityId = 0;
        removedNetworkItemId = 0;

        var cityList = cities as IList<CityBuildState> ?? cities.ToList();
        if (cityList.Count == 0)
        {
            return false;
        }

        var triggered = false;
        var capturedVictimCityId = 0;
        var capturedAttackerCityId = 0;
        var capturedItemId = (ushort)0;

        world.Query(
            in OrbQuery,
            (Entity entity, ref PlacedItemRef item) =>
            {
                if (triggered || item.Type != ItemType.Orb || item.Active)
                {
                    return;
                }

                foreach (var build in cityList)
                {
                    if (!build.IsOrbable || build.CityId == item.CityId)
                    {
                        continue;
                    }

                    if (!IsOrbOnCommandCenter(build, item.GridX, item.GridY))
                    {
                        continue;
                    }

                    capturedVictimCityId = build.CityId;
                    capturedAttackerCityId = item.CityId;
                    if (world.Has<NetworkItemRef>(entity))
                    {
                        capturedItemId = world.Get<NetworkItemRef>(entity).ItemId;
                    }

                    world.Destroy(entity);
                    triggered = true;
                    return;
                }
            });

        if (!triggered)
        {
            return false;
        }

        victimCityId = capturedVictimCityId;
        attackerCityId = capturedAttackerCityId;
        removedNetworkItemId = capturedItemId;
        return true;
    }

    /// <summary>
    /// An orb orbs a city when it is on that command center.
    /// The southeast anchor is the building's grid corner. Tanks can stand on the south
    /// drive row; the top of the sprite is solid, so that row alone never received a drop.
    /// The legacy formula also accepts the open strip just north of the old collision box
    /// (<c>CItem::drop</c>: <c>CalcY == 2</c> and <c>CalcX</c> in 0..2, with CityX/Y = anchor − 2).
    /// </summary>
    public static bool IsOrbOnCommandCenter(CityBuildState build, int gridX, int gridY)
    {
        var anchorX = build.CommandCenterGridX;
        var anchorY = build.CommandCenterGridY;
        var onFootprint = gridX >= anchorX - GameConstants.BuildingCollisionOffset
            && gridX <= anchorX
            && gridY >= anchorY - GameConstants.BuildingCollisionOffset
            && gridY <= anchorY;
        if (onFootprint)
        {
            return true;
        }

        var cityX = anchorX - GameConstants.BuildingCollisionOffset;
        var cityY = anchorY - GameConstants.BuildingCollisionOffset;
        var calcX = cityX - gridX;
        var calcY = cityY - gridY;
        return calcY == 2 && calcX is >= 0 and <= 2;
    }
}
