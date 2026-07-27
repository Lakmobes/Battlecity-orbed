using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Shared.Catalogs;

namespace BattleCity.Core.City;

public static class CityOrbedService
{
    private static readonly QueryDescription BuildingQuery =
        new QueryDescription().WithAll<BuildingRef>();

    private static readonly QueryDescription ItemQuery =
        new QueryDescription().WithAll<PlacedItemRef>();

    public static void ApplyOrbed(World world, CityBuildState build)
    {
        var cityId = build.CityId;
        var buildingsToDestroy = new List<Entity>();
        var itemsToDestroy = new List<Entity>();

        world.Query(
            in BuildingQuery,
            (Entity entity, ref BuildingRef building) =>
            {
                if (building.CityId != cityId)
                {
                    return;
                }

                // Remake keeps houses (overlay text); legacy also wiped houses.
                if (!BuildingCatalog.IsHouse(building.TypeCode)
                    && !BuildingCatalog.IsCommandCenter(building.TypeCode))
                {
                    buildingsToDestroy.Add(entity);
                }
            });

        world.Query(
            in ItemQuery,
            (Entity entity, ref PlacedItemRef item) =>
            {
                if (item.CityId == cityId)
                {
                    itemsToDestroy.Add(entity);
                }
            });

        foreach (var entity in buildingsToDestroy)
        {
            if (world.IsAlive(entity))
            {
                BuildingPopulationSystem.DetachBeforeDestroy(world, entity);
                world.Destroy(entity);
            }
        }

        foreach (var entity in itemsToDestroy)
        {
            if (world.IsAlive(entity))
            {
                world.Destroy(entity);
            }
        }

        CityBuildInitializer.ApplyLegacyStartingPermissions(build);
        build.CurrentBuildingCount = 1;
        build.MaxBuildingCount = 1;
        build.HadBombFactory = false;
        build.HadOrbFactory = false;
        build.Orbs = 0;
        Array.Clear(build.ResearchStatus, 0, build.ResearchStatus.Length);
        Array.Clear(build.ResearchTimers, 0, build.ResearchTimers.Length);
    }
}
