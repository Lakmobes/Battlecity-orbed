using Arch.Core;

using BattleCity.Client.Assets;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Maps;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

public sealed class MiniMapRenderer
{
    private static readonly Color OwnCcColor = new(80, 160, 255);
    private static readonly Color OtherCcColor = new(220, 60, 60);
    private static readonly Color BuildingColor = new(220, 40, 40);
    private static readonly Color PlayerColor = Color.Yellow;

    private static readonly QueryDescription BuildingQuery =
        new QueryDescription().WithAll<BuildingRef, Transform2D>();

    private readonly AssetService _assets;

    public MiniMapRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void Draw(
        SpriteBatch spriteBatch,
        TileMap tileMap,
        World world,
        Vector2 focusWorldPosition,
        int homeCommandCenterGridX,
        int homeCommandCenterGridY)
    {
        var radius = RenderConstants.MiniMapRadiusTiles;
        var tilePixelSize = RenderConstants.MiniMapTilePixelSize;
        var mapSize = (2 * radius + 1) * tilePixelSize;
        var margin = ModernHudLayout.MiniMapMargin;
        var origin = new Point(margin, margin);
        var background = new Rectangle(origin.X, origin.Y, mapSize, mapSize);

        HudOverlayHelper.DrawPanel(
            spriteBatch,
            _assets,
            background,
            new Color(20, 20, 20, 220),
            borderThickness: 0);

        var centerTileX = (int)focusWorldPosition.X / GameConstants.TileSize;
        var centerTileY = (int)focusWorldPosition.Y / GameConstants.TileSize;
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
                if (!background.Contains(rect))
                {
                    continue;
                }

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

        DrawBuildings(
            spriteBatch,
            world,
            mapCenter,
            centerTileX,
            centerTileY,
            radius,
            tilePixelSize,
            background,
            homeCommandCenterGridX,
            homeCommandCenterGridY);

        var playerRect = new Rectangle(
            (int)mapCenter.X - tilePixelSize / 2,
            (int)mapCenter.Y - tilePixelSize / 2,
            tilePixelSize,
            tilePixelSize);
        spriteBatch.Draw(_assets.Pixel, playerRect, PlayerColor);
    }

    private void DrawBuildings(
        SpriteBatch spriteBatch,
        World world,
        Vector2 mapCenter,
        int centerTileX,
        int centerTileY,
        int radius,
        int tilePixelSize,
        Rectangle clipBounds,
        int homeCommandCenterGridX,
        int homeCommandCenterGridY)
    {
        world.Query(
            in BuildingQuery,
            (ref BuildingRef building, ref Transform2D _) =>
            {
                // GridAnchor is the SE corner of the 3×3 footprint; center tile is anchor − 1.
                var footprintCenterX = building.GridAnchorX - 1;
                var footprintCenterY = building.GridAnchorY - 1;
                if (Math.Abs(footprintCenterX - centerTileX) > radius + 2
                    || Math.Abs(footprintCenterY - centerTileY) > radius + 2)
                {
                    return;
                }

                var isCc = BuildingCatalog.IsCommandCenter(building.TypeCode);
                var isOwnCc = isCc
                    && building.GridAnchorX == homeCommandCenterGridX
                    && building.GridAnchorY == homeCommandCenterGridY;
                var color = isCc
                    ? (isOwnCc ? OwnCcColor : OtherCcColor)
                    : BuildingColor;

                DrawBuildingMarker(
                    spriteBatch,
                    mapCenter,
                    centerTileX,
                    centerTileY,
                    footprintCenterX,
                    footprintCenterY,
                    tilePixelSize,
                    color,
                    usePalette: isCc,
                    clipBounds);
            });
    }

    private void DrawBuildingMarker(
        SpriteBatch spriteBatch,
        Vector2 mapCenter,
        int centerTileX,
        int centerTileY,
        int footprintCenterX,
        int footprintCenterY,
        int tilePixelSize,
        Color color,
        bool usePalette,
        Rectangle clipBounds)
    {
        // Legacy DrawMiniMap: every building is a 3×3 marker.
        var size = tilePixelSize * 3;
        var screenX = (int)(mapCenter.X + (footprintCenterX - centerTileX) * tilePixelSize) - size / 2;
        var screenY = (int)(mapCenter.Y + (footprintCenterY - centerTileY) * tilePixelSize) - size / 2;
        var rect = ClipToBounds(new Rectangle(screenX, screenY, size, size), clipBounds);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        if (usePalette && _assets.IsTextureLoaded(LegacySpriteNames.MiniMapColors))
        {
            var paletteIndex = color.R < 150 ? 2 : 3;
            var source = new Rectangle(paletteIndex * 15, 0, 15, 15);
            spriteBatch.Draw(_assets.MiniMapColors, rect, source, Color.White);
            return;
        }

        spriteBatch.Draw(_assets.Pixel, rect, color);
    }

    private static Rectangle ClipToBounds(Rectangle rect, Rectangle bounds)
    {
        var left = Math.Max(rect.Left, bounds.Left);
        var top = Math.Max(rect.Top, bounds.Top);
        var right = Math.Min(rect.Right, bounds.Right);
        var bottom = Math.Min(rect.Bottom, bounds.Bottom);
        return new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static Color GetMiniMapColor(TerrainTileType terrain) =>
        terrain switch
        {
            TerrainTileType.Lava => new Color(220, 80, 20),
            TerrainTileType.Rock => new Color(120, 120, 120),
            _ => Color.Transparent,
        };
}
