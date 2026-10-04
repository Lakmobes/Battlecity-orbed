using BattleCity.Client.Assets;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

public sealed class TerrainRenderer
{
    /// <summary>
    /// Legacy <c>CDrawing::DrawMap</c> places ground every 128px but blits at 144×144 (16px overlap),
    /// sampling a 128×128 source. HD Ground.png is 288×288 (2× of the 144 draw cell).
    /// </summary>
    private const int LegacyGroundStride = 128;
    private const int LegacyGroundDrawSize = 144;

    private readonly AssetService _assets;
    private readonly LavaSheetComposer _lavaComposer = new();

    public TerrainRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void Draw(
        SpriteBatch spriteBatch,
        TileMap tileMap,
        Rectangle visibleWorldRect,
        float animationTime)
    {
        DrawGround(spriteBatch, visibleWorldRect);
        DrawTerrainTiles(spriteBatch, tileMap, visibleWorldRect, animationTime);
    }

    private void DrawGround(SpriteBatch spriteBatch, Rectangle visibleWorldRect)
    {
        var ground = _assets.Ground;
        // Use the full HD sheet into the 144 world footprint (legacy stretched 128→144).
        var source = new Rectangle(0, 0, ground.Width, ground.Height);
        var startX = FloorToGrid(visibleWorldRect.Left, LegacyGroundStride) - LegacyGroundStride;
        var startY = FloorToGrid(visibleWorldRect.Top, LegacyGroundStride) - LegacyGroundStride;
        var endX = visibleWorldRect.Right + LegacyGroundDrawSize;
        var endY = visibleWorldRect.Bottom + LegacyGroundDrawSize;

        for (var y = startY; y < endY; y += LegacyGroundStride)
        {
            for (var x = startX; x < endX; x += LegacyGroundStride)
            {
                spriteBatch.Draw(
                    ground,
                    new Rectangle(x, y, LegacyGroundDrawSize, LegacyGroundDrawSize),
                    source,
                    Color.White);
            }
        }
    }

    private void DrawTerrainTiles(
        SpriteBatch spriteBatch,
        TileMap tileMap,
        Rectangle visibleWorldRect,
        float animationTime)
    {
        var minTileX = Math.Clamp(visibleWorldRect.Left / GameConstants.TileSize, 0, TileMap.Size - 1);
        var maxTileX = Math.Clamp(visibleWorldRect.Right / GameConstants.TileSize, 0, TileMap.Size - 1);
        var minTileY = Math.Clamp(visibleWorldRect.Top / GameConstants.TileSize, 0, TileMap.Size - 1);
        var maxTileY = Math.Clamp(visibleWorldRect.Bottom / GameConstants.TileSize, 0, TileMap.Size - 1);

        var lavaTiles = new List<(Rectangle Destination, Rectangle Source)>();
        for (var tileX = minTileX; tileX <= maxTileX; tileX++)
        {
            for (var tileY = minTileY; tileY <= maxTileY; tileY++)
            {
                var terrain = tileMap.Terrain[tileX, tileY];
                if (terrain is not (TerrainTileType.Lava or TerrainTileType.Rock))
                {
                    continue;
                }

                var autotileSourceX = tileMap.AutotileIndices[tileX, tileY];
                var destination = WorldSpriteMetrics.LegacyWorldDestination(
                    tileX * GameConstants.TileSize,
                    tileY * GameConstants.TileSize,
                    GameConstants.TileSize,
                    GameConstants.TileSize);
                var source = WorldSpriteMetrics.ScaleSource(
                    new Rectangle(autotileSourceX, 0, GameConstants.TileSize, GameConstants.TileSize));

                if (terrain == TerrainTileType.Rock)
                {
                    spriteBatch.Draw(_assets.Rocks, destination, source, Color.White);
                    continue;
                }

                lavaTiles.Add((destination, source));
            }
        }

        if (lavaTiles.Count == 0)
        {
            return;
        }

        DrawLavaTiles(spriteBatch, lavaTiles, animationTime);
    }

    private void DrawLavaTiles(
        SpriteBatch spriteBatch,
        List<(Rectangle Destination, Rectangle Source)> lavaTiles,
        float animationTime)
    {
        var lava = _assets.Lava;
        var fill = _assets.LavaFill;
        if (fill == _assets.Pixel || lava == _assets.Pixel)
        {
            foreach (var tile in lavaTiles)
            {
                spriteBatch.Draw(lava, tile.Destination, tile.Source, Color.White);
            }

            return;
        }

        var frames = LavaSheetComposer.FrameCount(fill);
        var frame = (int)(animationTime / 0.45f) % frames;
        var cell = lavaTiles[0].Source.Width;
        var sheet = _lavaComposer.Compose(spriteBatch.GraphicsDevice, lava, fill, frame, cell);
        foreach (var tile in lavaTiles)
        {
            spriteBatch.Draw(sheet, tile.Destination, tile.Source, Color.White);
        }
    }

    private static int FloorToGrid(int value, int gridSize)
    {
        if (value >= 0)
        {
            return value / gridSize * gridSize;
        }

        return (value - gridSize + 1) / gridSize * gridSize;
    }
}
