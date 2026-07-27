using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.Ecs.Systems;

/// <summary>
/// Legacy house staffing: each house supplies two 50-pop slots (research/factory/hospital).
/// House population is the sum of its attached buildings (max 100).
/// </summary>
public static class BuildingPopulationSystem
{
    /// <summary>Legacy cycle grows attached buildings every 250ms.</summary>
    private const float TickIntervalSeconds = 0.25f;
    private static float _accumulator;

    private static readonly QueryDescription BuildingQuery =
        new QueryDescription().WithAll<BuildingRef, BuildingState>();

    private readonly record struct BuildingEntry(Entity Entity, ushort NetworkId, int TypeCode, int CityId);

    public static void Update(World world, float deltaSeconds)
    {
        _accumulator += deltaSeconds;
        if (_accumulator < TickIntervalSeconds)
        {
            return;
        }

        _accumulator = 0f;

        var entries = new List<BuildingEntry>(64);
        world.Query(
            in BuildingQuery,
            (Entity entity, ref BuildingRef building, ref BuildingState _) =>
            {
                if (building.NetworkId == 0 || BuildingCatalog.IsCommandCenter(building.TypeCode))
                {
                    return;
                }

                entries.Add(new BuildingEntry(entity, building.NetworkId, building.TypeCode, building.CityId));
            });

        foreach (var entry in entries)
        {
            if (!world.IsAlive(entry.Entity))
            {
                continue;
            }

            ref var building = ref world.Get<BuildingRef>(entry.Entity);
            ref var state = ref world.Get<BuildingState>(entry.Entity);

            if (BuildingCatalog.IsHouse(building.TypeCode))
            {
                SyncHousePopulation(world, ref state);
                continue;
            }

            if (!NeedsHouseStaffing(building.TypeCode))
            {
                continue;
            }

            if (state.AttachedHouseNetworkId == 0)
            {
                TryAttachToHouse(world, ref building, ref state, entries);
            }

            if (state.AttachedHouseNetworkId == 0)
            {
                continue;
            }

            if (state.Population < EconomyConstants.PopulationMaxNonHouse)
            {
                state.Population = Math.Min(
                    EconomyConstants.PopulationMaxNonHouse,
                    state.Population + 5);
            }

            SyncAttachedHouseFromWorker(world, ref state);
        }
    }

    public static bool NeedsHouseStaffing(int typeCode) =>
        BuildingCatalog.IsFactory(typeCode)
        || BuildingCatalog.IsResearch(typeCode)
        || BuildingCatalog.IsHospital(typeCode);

    /// <summary>Clear house slots / worker links before destroying a building (legacy delBuilding).</summary>
    public static void DetachBeforeDestroy(World world, Entity entity)
    {
        if (!world.IsAlive(entity)
            || !world.Has<BuildingRef>(entity)
            || !world.Has<BuildingState>(entity))
        {
            return;
        }

        ref var building = ref world.Get<BuildingRef>(entity);
        ref var state = ref world.Get<BuildingState>(entity);
        var networkId = building.NetworkId;

        if (BuildingCatalog.IsHouse(building.TypeCode))
        {
            ZeroWorker(world, state.AttachedBuildingNetworkId1);
            ZeroWorker(world, state.AttachedBuildingNetworkId2);
            state.AttachedBuildingNetworkId1 = 0;
            state.AttachedBuildingNetworkId2 = 0;
            state.Population = 0;
            return;
        }

        if (state.AttachedHouseNetworkId == 0 || networkId == 0)
        {
            return;
        }

        if (!TryFindByNetworkId(world, state.AttachedHouseNetworkId, out var houseEntity))
        {
            state.AttachedHouseNetworkId = 0;
            return;
        }

        ref var houseState = ref world.Get<BuildingState>(houseEntity);
        if (houseState.AttachedBuildingNetworkId1 == networkId)
        {
            houseState.AttachedBuildingNetworkId1 = 0;
        }
        else if (houseState.AttachedBuildingNetworkId2 == networkId)
        {
            houseState.AttachedBuildingNetworkId2 = 0;
        }

        SyncHousePopulation(world, ref houseState);
        state.AttachedHouseNetworkId = 0;
        state.Population = 0;
    }

    private static void TryAttachToHouse(
        World world,
        ref BuildingRef workerBuilding,
        ref BuildingState workerState,
        List<BuildingEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (!BuildingCatalog.IsHouse(entry.TypeCode)
                || entry.CityId != workerBuilding.CityId
                || !world.IsAlive(entry.Entity))
            {
                continue;
            }

            ref var houseState = ref world.Get<BuildingState>(entry.Entity);
            if (houseState.AttachedBuildingNetworkId1 == 0)
            {
                houseState.AttachedBuildingNetworkId1 = workerBuilding.NetworkId;
                workerState.AttachedHouseNetworkId = entry.NetworkId;
                SyncHousePopulation(world, ref houseState);
                return;
            }

            if (houseState.AttachedBuildingNetworkId2 == 0)
            {
                houseState.AttachedBuildingNetworkId2 = workerBuilding.NetworkId;
                workerState.AttachedHouseNetworkId = entry.NetworkId;
                SyncHousePopulation(world, ref houseState);
                return;
            }
        }
    }

    private static void SyncAttachedHouseFromWorker(World world, ref BuildingState workerState)
    {
        if (!TryFindByNetworkId(world, workerState.AttachedHouseNetworkId, out var houseEntity))
        {
            workerState.AttachedHouseNetworkId = 0;
            return;
        }

        ref var houseState = ref world.Get<BuildingState>(houseEntity);
        SyncHousePopulation(world, ref houseState);
    }

    private static void SyncHousePopulation(World world, ref BuildingState houseState)
    {
        var pop1 = GetWorkerPopulation(world, houseState.AttachedBuildingNetworkId1);
        var pop2 = GetWorkerPopulation(world, houseState.AttachedBuildingNetworkId2);
        houseState.Population = pop1 + pop2;
    }

    private static int GetWorkerPopulation(World world, ushort networkId)
    {
        if (networkId == 0 || !TryFindByNetworkId(world, networkId, out var entity))
        {
            return 0;
        }

        return world.Get<BuildingState>(entity).Population;
    }

    private static void ZeroWorker(World world, ushort networkId)
    {
        if (networkId == 0 || !TryFindByNetworkId(world, networkId, out var entity))
        {
            return;
        }

        ref var state = ref world.Get<BuildingState>(entity);
        state.Population = 0;
        state.AttachedHouseNetworkId = 0;
    }

    private static bool TryFindByNetworkId(World world, ushort networkId, out Entity entity)
    {
        entity = Entity.Null;
        if (networkId == 0)
        {
            return false;
        }

        var found = false;
        var foundEntity = Entity.Null;
        world.Query(
            in BuildingQuery,
            (Entity candidate, ref BuildingRef building, ref BuildingState _) =>
            {
                if (found || building.NetworkId != networkId)
                {
                    return;
                }

                foundEntity = candidate;
                found = true;
            });

        entity = foundEntity;
        return found;
    }
}
