using BattleCity.Client.Assets;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

public sealed class TerrainRenderer
{
    private const int LegacyGroundSourceSize = 128;
    private const int LegacyGroundDrawSize = 144;

    private static int GroundSourceSize => WorldSpriteMetrics.Scaled(LegacyGroundSourceSize);
    private static int GroundDrawSize => WorldSpriteMetrics.Scaled(LegacyGroundDrawSize);

    private readonly AssetService _assets;

    public TerrainRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void Draw(SpriteBatch spriteBatch, TileMap tileMap, Rectangle visibleWorldRect)
    {
        DrawGround(spriteBatch, visibleWorldRect);
        DrawTerrainTiles(spriteBatch, tileMap, visibleWorldRect);
    }

    private void DrawGround(SpriteBatch spriteBatch, Rectangle visibleWorldRect)
    {
        var ground = _assets.Ground;
        var source = new Rectangle(0, 0, GroundSourceSize, GroundSourceSize);
        var startX = FloorToGrid(visibleWorldRect.Left, GroundDrawSize) - GroundDrawSize;
        var startY = FloorToGrid(visibleWorldRect.Top, GroundDrawSize) - GroundDrawSize;
        var endX = visibleWorldRect.Right + GroundDrawSize;
        var endY = visibleWorldRect.Bottom + GroundDrawSize;

        for (var y = startY; y < endY; y += GroundDrawSize)
        {
            for (var x = startX; x < endX; x += GroundDrawSize)
            {
                spriteBatch.Draw(
                    ground,
                    new Rectangle(x, y, GroundDrawSize, GroundDrawSize),
                    source,
                    Color.White);
            }
        }
    }

    private void DrawTerrainTiles(SpriteBatch spriteBatch, TileMap tileMap, Rectangle visibleWorldRect)
    {
        var minTileX = Math.Clamp(visibleWorldRect.Left / GameConstants.TileSize, 0, TileMap.Size - 1);
        var maxTileX = Math.Clamp(visibleWorldRect.Right / GameConstants.TileSize, 0, TileMap.Size - 1);
        var minTileY = Math.Clamp(visibleWorldRect.Top / GameConstants.TileSize, 0, TileMap.Size - 1);
        var maxTileY = Math.Clamp(visibleWorldRect.Bottom / GameConstants.TileSize, 0, TileMap.Size - 1);

        for (var tileX = minTileX; tileX <= maxTileX; tileX++)
        {
            for (var tileY = minTileY; tileY <= maxTileY; tileY++)
            {
                var terrain = tileMap.Terrain[tileX, tileY];
                if (terrain is not (TerrainTileType.Lava or TerrainTileType.Rock))
                {
                    continue;
                }

                var texture = terrain == TerrainTileType.Lava ? _assets.Lava : _assets.Rocks;
                var autotileIndex = tileMap.AutotileIndices[tileX, tileY];
                var destination = WorldSpriteMetrics.LegacyWorldDestination(
                    tileX * GameConstants.TileSize,
                    tileY * GameConstants.TileSize,
                    GameConstants.TileSize,
                    GameConstants.TileSize);
                var source = WorldSpriteMetrics.ScaleSource(
                    new Rectangle(autotileIndex, 0, GameConstants.TileSize, GameConstants.TileSize));

                spriteBatch.Draw(texture, destination, source, Color.White);
            }
        }
    }

    private static int FloorToGrid(int value, int gridSize)
    {
        return value / gridSize * gridSize;
    }
}
