using System.Numerics;

using BattleCity.Shared.Constants;

namespace BattleCity.Core.Levels;

public static class BuildingPlacement
{
    /// <summary>
    /// Top-left pixel of a building sprite from legacy grid anchor (see <c>CCollision.cpp</c>).
    /// </summary>
    public static Vector2 GridAnchorToWorldPosition(int gridX, int gridY) =>
        new(
            (gridX - GameConstants.BuildingCollisionOffset) * GameConstants.TileSize,
            (gridY - GameConstants.BuildingCollisionOffset) * GameConstants.TileSize);
}
