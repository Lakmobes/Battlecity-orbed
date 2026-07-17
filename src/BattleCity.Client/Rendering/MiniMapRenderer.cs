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
        Vector2 commandCenterWorldPosition)
    {
        const int mapSize = 240;
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
        var radius = RenderConstants.MiniMapRadiusTiles;
        var tilePixelSize = RenderConstants.MiniMapTilePixelSize;
        var mapCenter = new Vector2(origin.X + mapSize / 2f, origin.Y + mapSize / 2f);
        var ownCcTileX = (int)commandCenterWorldPosition.X / GameConstants.TileSize;
        var ownCcTileY = (int)commandCenterWorldPosition.Y / GameConstants.TileSize;

        for (var tileY = centerTileY - radius; tileY <= centerTileY + radius; tileY++)
        {
            for (var tileX = centerTileX - radius; tileX <= centerTileX + radius; tileX++)
            {
                if (tileX <= 0 || tileY <= 0 || tileX >= TileMap.Size || tileY >= TileMap.Size)
                {
                    continue;
                }

                var terrain = tileMap.Terrain[tileX, tileY];
                if (terrain is not (TerrainTileType.Lava or TerrainTileType.Rock or TerrainTileType.CityCenter))
                {
                    continue;
                }

                if (terrain == TerrainTileType.CityCenter)
                {
                    // One marker per city-center footprint (skip interior/edge duplicates).
                    if (tileX > 0 && tileMap.Terrain[tileX - 1, tileY] == TerrainTileType.CityCenter)
                    {
                        continue;
                    }

                    if (tileY > 0 && tileMap.Terrain[tileX, tileY - 1] == TerrainTileType.CityCenter)
                    {
                        continue;
                    }

                    var isOwn = Math.Abs(tileX - ownCcTileX) <= 2 && Math.Abs(tileY - ownCcTileY) <= 2;
                    DrawCityCenterMarker(
                        spriteBatch,
                        mapCenter,
                        centerTileX,
                        centerTileY,
                        tileX,
                        tileY,
                        tilePixelSize,
                        isOwn ? OwnCcColor : OtherCcColor);
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

        DrawBuildings(spriteBatch, world, mapCenter, centerTileX, centerTileY, radius, tilePixelSize);

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
        int tilePixelSize)
    {
        world.Query(
            in BuildingQuery,
            (ref BuildingRef building, ref Transform2D transform) =>
            {
                // CCs already drawn from CityCenter terrain tiles.
                if (BuildingCatalog.IsCommandCenter(building.TypeCode))
                {
                    return;
                }

                var tileX = building.GridAnchorX;
                var tileY = building.GridAnchorY;
                if (Math.Abs(tileX - centerTileX) > radius || Math.Abs(tileY - centerTileY) > radius)
                {
                    return;
                }

                var size = tilePixelSize * 2;
                var screenX = (int)(mapCenter.X + (tileX - centerTileX) * tilePixelSize) - size / 2;
                var screenY = (int)(mapCenter.Y + (tileY - centerTileY) * tilePixelSize) - size / 2;
                var rect = new Rectangle(screenX, screenY, size, size);
                spriteBatch.Draw(_assets.Pixel, rect, BuildingColor);
            });
    }

    private void DrawCityCenterMarker(
        SpriteBatch spriteBatch,
        Vector2 mapCenter,
        int centerTileX,
        int centerTileY,
        int tileX,
        int tileY,
        int tilePixelSize,
        Color color)
    {
        var size = tilePixelSize * 3;
        var screenX = (int)(mapCenter.X + (tileX - centerTileX) * tilePixelSize) - size / 2;
        var screenY = (int)(mapCenter.Y + (tileY - centerTileY) * tilePixelSize) - size / 2;
        var rect = new Rectangle(screenX, screenY, size, size);

        if (_assets.IsTextureLoaded(LegacySpriteNames.MiniMapColors))
        {
            // Palette: 2 = friendly CC, 3 = enemy CC (legacy DrawMiniMap).
            var paletteIndex = color.R < 150 ? 2 : 3;
            var source = new Rectangle(paletteIndex * 15, 0, 15, 15);
            spriteBatch.Draw(_assets.MiniMapColors, rect, source, Color.White);
        }
        else
        {
            spriteBatch.Draw(_assets.Pixel, rect, color);
        }
    }

    private static Color GetMiniMapColor(TerrainTileType terrain) =>
        terrain switch
        {
            TerrainTileType.Lava => new Color(220, 80, 20),
            TerrainTileType.Rock => new Color(120, 120, 120),
            TerrainTileType.CityCenter => OtherCcColor,
            _ => Color.Transparent,
        };
}
