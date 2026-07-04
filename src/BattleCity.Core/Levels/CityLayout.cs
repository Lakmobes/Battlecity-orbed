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

        return new Vector2(
            centerGridX * GameConstants.TileSize,
            spawnGridY * GameConstants.TileSize);
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

        return new Vector2(
            centerGridX * GameConstants.TileSize + GameConstants.TileSize / 2f,
            centerGridY * GameConstants.TileSize + GameConstants.TileSize / 2f);
    }
}
