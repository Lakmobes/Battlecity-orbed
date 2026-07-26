using BattleCity.Client.Assets;
using BattleCity.Core.City;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Levels;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>Semi-transparent building ghost while in build mode (legacy CDrawing::DrawBuildPlacement).</summary>
public sealed class BuildPreviewRenderer
{
    private static readonly Color ValidTint = new(120, 255, 120, 150);
    private static readonly Color InvalidTint = new(255, 90, 90, 150);
    private static readonly Color DemolishTint = new(255, 160, 60, 170);

    private readonly AssetService _assets;

    public BuildPreviewRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void Draw(SpriteBatch spriteBatch, in RenderContext context)
    {
        if (!context.ShowBuildPreview || context.CityBuild is null)
        {
            return;
        }

        var topLeft = BuildingPlacement.GridAnchorToWorldPosition(
            context.BuildPreviewGridX,
            context.BuildPreviewGridY);
        var drawX = (int)topLeft.X;
        var drawY = (int)topLeft.Y;

        if (context.BuildPreviewIsDemolish)
        {
            var tint = context.BuildPreviewIsValid ? DemolishTint : InvalidTint;
            spriteBatch.Draw(
                _assets.Pixel,
                new Rectangle(drawX, drawY, GameConstants.BuildingCollisionSize, GameConstants.BuildingCollisionSize),
                tint);
            return;
        }

        var (sourceX, sourceY) = BuildingSprites.GetSourceOrigin(context.BuildPreviewTypeCode);
        var buildings = _assets.LoadTexture(BuildingSprites.TextureKey);
        var tintColor = context.BuildPreviewIsValid ? ValidTint : InvalidTint;
        var legacySource = new Rectangle(
            sourceX,
            sourceY,
            BuildingSprites.SpriteSize,
            BuildingSprites.SpriteSize);

        WorldSpriteMetrics.DrawLegacySprite(
            spriteBatch,
            buildings,
            drawX,
            drawY,
            legacySource,
            tintColor);

        if (BuildingCatalog.IsFactory(context.BuildPreviewTypeCode)
            || BuildingCatalog.IsResearch(context.BuildPreviewTypeCode))
        {
            DrawEquipmentIcon(spriteBatch, context.BuildPreviewTypeCode, drawX, drawY, tintColor);
        }
    }

    private void DrawEquipmentIcon(SpriteBatch spriteBatch, int typeCode, int tileX, int tileY, Color tint)
    {
        if (!BuildingCatalog.TryGetEquipmentItemType(typeCode, out var itemType))
        {
            return;
        }

        var isFactory = BuildingCatalog.IsFactory(typeCode);
        var iconSize = isFactory ? 43 : 24;
        var (offsetX, offsetY) = isFactory ? (50, 50) : (18, 106);
        var (sourceX, sourceY) = ItemSprites.GetWorldSpriteOrigin(itemType);
        var items = _assets.Items;
        var legacySource = new Rectangle(
            sourceX,
            sourceY,
            ItemSprites.WorldSpriteSize,
            ItemSprites.WorldSpriteSize);

        WorldSpriteMetrics.DrawLegacySprite(
            spriteBatch,
            items,
            tileX + offsetX,
            tileY + offsetY,
            legacySource,
            iconSize,
            iconSize,
            tint);
    }
}
