using System.Numerics;

using BattleCity.Shared.Constants;

namespace BattleCity.Core.Gameplay;

public static class PlacedItemPlacement
{
    public static Vector2 GridToWorldPosition(int gridX, int gridY) =>
        new(gridX * GameConstants.TileSize, gridY * GameConstants.TileSize);
}
