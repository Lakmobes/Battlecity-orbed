using Arch.Core;

using BattleCity.Core.Audio;
using BattleCity.Core.City;
using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Systems;

/// <summary>Research timer and unlock chain (legacy/server/CBuilding.cpp research tick).</summary>
public static class ResearchSystem
{
    private const float ResearchPollIntervalSeconds = 1f;
    private static float _accumulator;

    private static readonly QueryDescription BuildingQuery =
        new QueryDescription().WithAll<BuildingRef, BuildingState>();

    public static void Update(
        World world,
        CityBuildState? build,
        float deltaSeconds,
        SimulationAudioBuffer? audio = null)
    {
        if (build is null)
        {
            return;
        }

        _accumulator += deltaSeconds;
        if (_accumulator < ResearchPollIntervalSeconds)
        {
            return;
        }

        _accumulator = 0f;

        world.Query(
            in BuildingQuery,
            (ref BuildingRef building, ref BuildingState state) =>
            {
                if (!BuildingCatalog.IsResearch(building.TypeCode)
                    || !BuildingCatalog.TryGetResearchTreeIndex(building.TypeCode, out var treeIndex))
                {
                    return;
                }

                if (build.ResearchStatus[treeIndex] == -1)
                {
                    return;
                }

                if (state.Population < EconomyConstants.PopulationMaxNonHouse)
                {
                    build.ResearchStatus[treeIndex] = 0;
                    build.ResearchTimers[treeIndex] = 0f;
                    return;
                }

                if (build.ResearchStatus[treeIndex] == 0 && build.ResearchTimers[treeIndex] <= 0f)
                {
                    build.ResearchTimers[treeIndex] = EconomyConstants.TimerResearch / 1000f;
                }

                if (build.ResearchTimers[treeIndex] <= 0f)
                {
                    return;
                }

                build.ResearchTimers[treeIndex] -= ResearchPollIntervalSeconds;
                if (build.ResearchTimers[treeIndex] > 0f)
                {
                    return;
                }

                build.ResearchStatus[treeIndex] = -1;
                build.ResearchTimers[treeIndex] = 0f;
                UnlockResearchRewards(build, treeIndex);
                ResearchCompleteNotificationSystem.Trigger(world, build.CityId, treeIndex);

                if (audio is not null
                    && CommandCenterLookup.TryGetWorldPosition(world, out var ccPosition))
                {
                    audio.Play(SoundId.Build, ccPosition);
                }
            });
    }

    internal static void UnlockResearchRewards(CityBuildState build, int treeIndex)
    {
        var factoryMenuIndex = BuildingCatalog.GetFactoryMenuIndex(treeIndex);
        if (factoryMenuIndex < build.CanBuild.Length && build.CanBuild[factoryMenuIndex] != 2)
        {
            build.CanBuild[factoryMenuIndex] = 1;
        }

        if (treeIndex == 3)
        {
            build.CanBuild[0] = 1;
        }

        for (var productIndex = 0; productIndex < BuildingCatalog.BuildTreePrerequisites.Count; productIndex++)
        {
            if (BuildingCatalog.BuildTreePrerequisites[productIndex] != treeIndex)
            {
                continue;
            }

            var researchMenuIndex = BuildingCatalog.GetResearchMenuIndex(productIndex);
            if (researchMenuIndex < build.CanBuild.Length && build.CanBuild[researchMenuIndex] == 0)
            {
                build.CanBuild[researchMenuIndex] = 1;
            }
        }
    }
}
