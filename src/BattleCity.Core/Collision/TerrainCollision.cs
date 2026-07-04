using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Collision;

public static class TerrainCollision
{
    public static bool IsBlocking(TileMap map, in AxisAlignedBox bounds) =>
        IsBlockingCorner(map, bounds.Left - 1f, bounds.Top - 1f, BlocksPlayerMovement)
        || IsBlockingCorner(map, bounds.Right, bounds.Top - 1f, BlocksPlayerMovement)
        || IsBlockingCorner(map, bounds.Right, bounds.Bottom, BlocksPlayerMovement)
        || IsBlockingCorner(map, bounds.Left - 1f, bounds.Bottom, BlocksPlayerMovement);

    /// <summary>Legacy bullets only stop on rocks (<c>CBullet.cpp</c> map == 2), not lava/water.</summary>
    public static bool IsBlockingForBullet(TileMap map, in AxisAlignedBox bounds) =>
        IsBlockingCorner(map, bounds.Left - 1f, bounds.Top - 1f, BlocksBullet)
        || IsBlockingCorner(map, bounds.Right, bounds.Top - 1f, BlocksBullet)
        || IsBlockingCorner(map, bounds.Right, bounds.Bottom, BlocksBullet)
        || IsBlockingCorner(map, bounds.Left - 1f, bounds.Bottom, BlocksBullet);

    public static bool IsBlockingTile(TerrainTileType tileType) =>
        BlocksPlayerMovement(tileType);

    public static bool IsBlockingTileForBullet(TerrainTileType tileType) =>
        BlocksBullet(tileType);

    private static bool BlocksPlayerMovement(TerrainTileType tileType) =>
        tileType != TerrainTileType.Open;

    private static bool BlocksBullet(TerrainTileType tileType) =>
        tileType == TerrainTileType.Rock;

    private static bool IsBlockingCorner(
        TileMap map,
        float pixelX,
        float pixelY,
        Func<TerrainTileType, bool> blocks)
    {
        var tileX = (int)(pixelX / GameConstants.TileSize);
        var tileY = (int)(pixelY / GameConstants.TileSize);

        if (tileX < 0 || tileY < 0 || tileX >= TileMap.Size || tileY >= TileMap.Size)
        {
            return true;
        }

        return blocks(map.Terrain[tileX, tileY]);
    }
}
