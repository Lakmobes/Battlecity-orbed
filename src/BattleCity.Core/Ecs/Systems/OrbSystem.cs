using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Systems;

/// <summary>Orb dropped on command center (legacy/server/CItem.cpp).</summary>
public static class OrbSystem
{
    private static readonly QueryDescription OrbQuery =
        new QueryDescription().WithAll<PlacedItemRef>();

    public static bool TryTrigger(World world, CityBuildState build, out int attackerCityId)
    {
        attackerCityId = 0;

        if (!build.IsOrbable)
        {
            return false;
        }

        var triggered = false;
        var capturedAttackerCityId = 0;

        world.Query(
            in OrbQuery,
            (Entity entity, ref PlacedItemRef item) =>
            {
                if (triggered || item.Type != ItemType.Orb || item.Active)
                {
                    return;
                }

                if (!IsOrbOnCommandCenter(build, item.GridX, item.GridY))
                {
                    return;
                }

                capturedAttackerCityId = item.CityId;
                world.Destroy(entity);
                triggered = true;
            });

        if (triggered)
        {
            attackerCityId = capturedAttackerCityId;
        }

        return triggered;
    }

    private static bool IsOrbOnCommandCenter(CityBuildState build, int gridX, int gridY)
    {
        var deltaX = build.CommandCenterGridX - gridX;
        var deltaY = build.CommandCenterGridY - gridY;
        return deltaY == 2 && deltaX is >= 0 and <= 2;
    }
}
