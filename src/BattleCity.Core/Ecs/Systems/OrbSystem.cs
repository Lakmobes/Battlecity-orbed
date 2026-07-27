using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Systems;

/// <summary>Orb dropped on command center (legacy/server/CItem.cpp).</summary>
public static class OrbSystem
{
    private static readonly QueryDescription OrbQuery =
        new QueryDescription().WithAll<PlacedItemRef>();

    public static bool TryTrigger(World world, CityBuildState build, out int attackerCityId) =>
        TryTrigger(world, [build], out _, out attackerCityId);

    /// <summary>
    /// Scans inactive orbs against every orbable enemy city CC (legacy drop-time check).
    /// </summary>
    public static bool TryTrigger(
        World world,
        IEnumerable<CityBuildState> cities,
        out int victimCityId,
        out int attackerCityId)
    {
        victimCityId = 0;
        attackerCityId = 0;

        var cityList = cities as IList<CityBuildState> ?? cities.ToList();
        if (cityList.Count == 0)
        {
            return false;
        }

        var triggered = false;
        var capturedVictimCityId = 0;
        var capturedAttackerCityId = 0;

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
        return true;
    }

    private static bool IsOrbOnCommandCenter(CityBuildState build, int gridX, int gridY)
    {
        var deltaX = build.CommandCenterGridX - gridX;
        var deltaY = build.CommandCenterGridY - gridY;
        return deltaY == 2 && deltaX is >= 0 and <= 2;
    }
}
