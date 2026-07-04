using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.City;

/// <summary>Legacy <c>CBuildingList::inRange(true)</c> for defensive item drops.</summary>
public static class DefensiveItemRangeValidator
{
    private static readonly QueryDescription BuildingQuery =
        new QueryDescription().WithAll<BuildingRef>();

    public static bool IsWithinRange(World world, CityBuildState build, Vector2 tankTopLeft)
    {
        var (playerTileX, playerTileY) = TankPlacement.GetTileFromTopLeft(tankTopLeft);

        if (BuildingPlacementValidator.IsWithinCommandCenterTileRange(build, playerTileX, playerTileY))
        {
            return true;
        }

        return IsNearFriendlyBuilding(world, playerTileX, playerTileY);
    }

    private static bool IsNearFriendlyBuilding(World world, int playerTileX, int playerTileY)
    {
        var found = false;

        world.Query(
            in BuildingQuery,
            (ref BuildingRef building) =>
            {
                if (found)
                {
                    return;
                }

                var buildingTileX = building.GridAnchorX - 1;
                var buildingTileY = building.GridAnchorY - 1;

                if (Math.Abs(buildingTileX - playerTileX) <= GameConstants.DistanceMaxFromBuilding
                    && Math.Abs(buildingTileY - playerTileY) <= GameConstants.DistanceMaxFromBuilding)
                {
                    found = true;
                }
            });

        return found;
    }
}
