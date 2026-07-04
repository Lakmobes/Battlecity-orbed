using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Levels;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.City;

public static class ResearchVisuals
{
    public static bool IsResearchInProgress(
        CityBuildState? build,
        int typeCode,
        int population,
        out int treeIndex)
    {
        treeIndex = 0;
        if (build is null
            || !BuildingCatalog.IsResearch(typeCode)
            || !BuildingCatalog.TryGetResearchTreeIndex(typeCode, out treeIndex))
        {
            return false;
        }

        return build.ResearchStatus[treeIndex] != -1
            && build.ResearchTimers[treeIndex] > 0f
            && population >= EconomyConstants.PopulationMaxNonHouse;
    }

    public static int GetAnimationFrameOffset(float animationTimeSeconds) =>
        ((int)(animationTimeSeconds * 2f) % 3) * BuildingSprites.SpriteSize;
}
