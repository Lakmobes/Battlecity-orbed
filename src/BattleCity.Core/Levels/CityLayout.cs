using System.Numerics;

using BattleCity.Shared.Constants;

namespace BattleCity.Core.Levels;

public sealed class CityLayout
{
    public required string CityName { get; init; }

    public required string SourcePath { get; init; }

    public required IReadOnlyList<CityBuildingPlacement> Buildings { get; init; }

    public Vector2 GetSpawnPosition()
    {
        if (Buildings.Count == 0)
        {
            return new Vector2(GameConstants.WorldSizePixels / 2f, GameConstants.WorldSizePixels / 2f);
        }

        var sumX = 0;
        var maxY = 0;

        foreach (var building in Buildings)
        {
            sumX += building.GridX;
            if (building.GridY > maxY)
            {
                maxY = building.GridY;
            }
        }

        var centerGridX = sumX / Buildings.Count;
        var spawnGridY = maxY + 3;

        return BuildingPlacement.GridAnchorToWorldPosition(centerGridX, spawnGridY);
    }

    public Vector2 GetCameraFocus()
    {
        if (Buildings.Count == 0)
        {
            return GetSpawnPosition();
        }

        var sumX = 0;
        var sumY = 0;

        foreach (var building in Buildings)
        {
            sumX += building.GridX;
            sumY += building.GridY;
        }

        var centerGridX = sumX / Buildings.Count;
        var centerGridY = sumY / Buildings.Count;
        var topLeft = BuildingPlacement.GridAnchorToWorldPosition(centerGridX, centerGridY);
        return topLeft + new Vector2(
            GameConstants.BuildingCollisionSize / 2f,
            GameConstants.BuildingCollisionSize / 2f);
    }
}
