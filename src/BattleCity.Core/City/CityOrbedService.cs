using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;

namespace BattleCity.Core.City;

public static class CityOrbedService
{
    private static readonly QueryDescription BuildingQuery =
        new QueryDescription().WithAll<BuildingRef>();

    public static void ApplyOrbed(World world, CityBuildState build)
    {
        var toDestroy = new List<Entity>();

        world.Query(
            in BuildingQuery,
            (Entity entity, ref BuildingRef building) =>
            {
                if (!BuildingCatalog.IsHouse(building.TypeCode)
                    && !BuildingCatalog.IsCommandCenter(building.TypeCode))
                {
                    toDestroy.Add(entity);
                }
            });

        foreach (var entity in toDestroy)
        {
            world.Destroy(entity);
        }

        CityBuildInitializer.ApplyLegacyStartingPermissions(build);
        build.CurrentBuildingCount = 1;
        build.MaxBuildingCount = Math.Max(build.MaxBuildingCount, build.CurrentBuildingCount);
        Array.Clear(build.ResearchStatus, 0, build.ResearchStatus.Length);
        Array.Clear(build.ResearchTimers, 0, build.ResearchTimers.Length);
    }
}
