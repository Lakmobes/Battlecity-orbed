using System.Numerics;

using BattleCity.Shared.Constants;

namespace BattleCity.Core.Gameplay;

public static class TankPlacement
{
    /// <summary>Legacy tile from tank top-left (<c>CPlayer::getTileX/Y</c>).</summary>
    public static (int GridX, int GridY) GetTileFromTopLeft(Vector2 tankTopLeft)
    {
        var gridX = (int)((tankTopLeft.X + GameConstants.TileSize / 2f) / GameConstants.TileSize);
        var gridY = (int)((tankTopLeft.Y + GameConstants.TileSize / 2f) / GameConstants.TileSize);
        return (gridX, gridY);
    }
}
