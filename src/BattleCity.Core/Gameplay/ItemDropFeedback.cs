using System.Numerics;

using Arch.Core;

using BattleCity.Core.City;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Maps;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

public static class ItemDropFeedback
{
    public const string OutOfRangeMessage = "You are too far from your city to drop that item.";
    public const string BlockedPlacementMessage = "There is no room to drop that item here.";

    public static string? GetFailureMessage(
        World world,
        Entity owner,
        Vector2 tankTopLeft,
        ItemType type,
        CityBuildState? cityBuild)
    {
        if (ItemDropPlacement.RequiresDedicatedTile(type)
            && cityBuild is not null
            && !DefensiveItemRangeValidator.IsWithinRange(world, cityBuild, tankTopLeft))
        {
            return OutOfRangeMessage;
        }

        if (!ItemDropPlacement.TryFindDropTile(world, owner, tankTopLeft, type, out _, out _, cityBuild))
        {
            return BlockedPlacementMessage;
        }

        return null;
    }
}
