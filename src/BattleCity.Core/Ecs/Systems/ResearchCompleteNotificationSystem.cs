using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;

namespace BattleCity.Core.Ecs.Systems;

public static class ResearchCompleteNotificationSystem
{
    private const float OverlayDurationSeconds = 6f;

    private static readonly QueryDescription PlayerQuery =
        new QueryDescription().WithAll<InputControlled, CityAffiliation, CityResearchCompleteState>();

    public static void Update(World world, float deltaSeconds)
    {
        world.Query(
            in PlayerQuery,
            (ref CityResearchCompleteState complete) =>
            {
                if (!complete.ShowOverlay)
                {
                    return;
                }

                complete.RemainingSeconds -= deltaSeconds;
                if (complete.RemainingSeconds <= 0f)
                {
                    complete.ShowOverlay = false;
                    complete.RemainingSeconds = 0f;
                    complete.Message = string.Empty;
                }
            });
    }

    public static void Trigger(World world, int cityId, int treeIndex)
    {
        var factoryMenuIndex = BuildingCatalog.GetFactoryMenuIndex(treeIndex);
        var factoryName = factoryMenuIndex >= 0 && factoryMenuIndex < BuildingCatalog.MenuNames.Count
            ? BuildingCatalog.MenuNames[factoryMenuIndex]
            : "Factory";

        var message = $"Research complete!\n{factoryName} is now available to build.";

        world.Query(
            in PlayerQuery,
            (ref CityAffiliation city, ref CityResearchCompleteState complete) =>
            {
                if (city.CityId != cityId)
                {
                    return;
                }

                complete.ShowOverlay = true;
                complete.RemainingSeconds = OverlayDurationSeconds;
                complete.Message = message;
            });
    }
}
