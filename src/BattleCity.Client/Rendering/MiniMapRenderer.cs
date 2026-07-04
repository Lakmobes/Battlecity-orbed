using BattleCity.Client.Assets;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

public sealed class MiniMapRenderer
{
    private readonly AssetService _assets;

    public MiniMapRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void Draw(
        SpriteBatch spriteBatch,
        TileMap tileMap,
        Vector2 focusWorldPosition,
        Vector2 commandCenterWorldPosition)
    {
        const int mapSize = 240;
        const int margin = 10;
        var origin = new Point(margin, margin);
        var background = new Rectangle(origin.X, origin.Y, mapSize, mapSize);

        spriteBatch.Draw(_assets.Pixel, background, new Color(20, 20, 20, 220));

        var centerTileX = (int)focusWorldPosition.X / GameConstants.TileSize;
        var centerTileY = (int)focusWorldPosition.Y / GameConstants.TileSize;
        var radius = RenderConstants.MiniMapRadiusTiles;
        var tilePixelSize = RenderConstants.MiniMapTilePixelSize;
        var mapCenter = new Vector2(origin.X + mapSize / 2f, origin.Y + mapSize / 2f);

        for (var tileY = centerTileY - radius; tileY <= centerTileY + radius; tileY++)
        {
            for (var tileX = centerTileX - radius; tileX <= centerTileX + radius; tileX++)
            {
                if (tileX <= 0 || tileY <= 0 || tileX >= TileMap.Size || tileY >= TileMap.Size)
                {
                    continue;
                }

                var terrain = tileMap.Terrain[tileX, tileY];
                if (terrain is not (TerrainTileType.Lava or TerrainTileType.Rock))
                {
                    continue;
                }

                var screenX = (int)(mapCenter.X + (tileX - centerTileX) * tilePixelSize);
                var screenY = (int)(mapCenter.Y + (tileY - centerTileY) * tilePixelSize);
                var rect = new Rectangle(screenX, screenY, tilePixelSize, tilePixelSize);
                var color = GetMiniMapColor(terrain);

                if (_assets.IsTextureLoaded(LegacySpriteNames.MiniMapColors))
                {
                    var paletteIndex = terrain == TerrainTileType.Lava ? 0 : 1;
                    var source = new Rectangle(paletteIndex * 15, 0, tilePixelSize, tilePixelSize);
                    spriteBatch.Draw(_assets.MiniMapColors, rect, source, Color.White);
                }
                else
                {
                    spriteBatch.Draw(_assets.Pixel, rect, color);
                }
            }
        }

        DrawMiniMapDot(
            _assets,
            spriteBatch,
            mapCenter,
            centerTileX,
            centerTileY,
            commandCenterWorldPosition,
            tilePixelSize,
            new Color(80, 160, 255));

        var playerRect = new Rectangle(
            (int)mapCenter.X - tilePixelSize / 2,
            (int)mapCenter.Y - tilePixelSize / 2,
            tilePixelSize,
            tilePixelSize);
        spriteBatch.Draw(_assets.Pixel, playerRect, Color.Yellow);
    }

    private static void DrawMiniMapDot(
        AssetService assets,
        SpriteBatch spriteBatch,
        Vector2 mapCenter,
        int centerTileX,
        int centerTileY,
        Vector2 worldPosition,
        int tilePixelSize,
        Color color)
    {
        var tileX = (int)worldPosition.X / GameConstants.TileSize;
        var tileY = (int)worldPosition.Y / GameConstants.TileSize;
        var screenX = (int)(mapCenter.X + (tileX - centerTileX) * tilePixelSize) - tilePixelSize / 2;
        var screenY = (int)(mapCenter.Y + (tileY - centerTileY) * tilePixelSize) - tilePixelSize / 2;
        var rect = new Rectangle(screenX, screenY, tilePixelSize, tilePixelSize);
        spriteBatch.Draw(assets.Pixel, rect, color);
    }

    private static Color GetMiniMapColor(TerrainTileType terrain) =>
        terrain switch
        {
            TerrainTileType.Lava => new Color(220, 80, 20),
            TerrainTileType.Rock => new Color(120, 120, 120),
            _ => Color.Transparent,
        };
}
