using Arch.Core;

using BattleCity.Client.Assets;
using BattleCity.Core.City;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

public sealed class BuildingOverlayRenderer
{
    private const int LegacyNumberSize = 16;

    private static readonly Arch.Core.QueryDescription BuildingQuery =
        new Arch.Core.QueryDescription().WithAll<Transform2D, BuildingRef, BuildingState>();

    private readonly AssetService _assets;

    public BuildingOverlayRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void Draw(SpriteBatch spriteBatch, World world, CityBuildState? cityBuild)
    {
        var population = _assets.Population;
        var items = _assets.Items;
        var numbers = _assets.BlackNumbers;
        var hasNumbers = numbers != _assets.Pixel;

        world.Query(
            in BuildingQuery,
            (ref Transform2D transform, ref BuildingRef building, ref BuildingState state) =>
            {
                var typeCode = building.TypeCode;
                var tileX = (int)transform.Position.X;
                var tileY = (int)transform.Position.Y;

                if (BuildingCatalog.IsFactory(typeCode) || BuildingCatalog.IsResearch(typeCode))
                {
                    DrawEquipmentIcon(spriteBatch, items, typeCode, tileX, tileY);
                }

                if (hasNumbers && BuildingCatalog.IsFactory(typeCode) && state.ItemsLeft > 0)
                {
                    DrawFactoryStock(spriteBatch, numbers, tileX, tileY, state.ItemsLeft);
                }

                if (hasNumbers
                    && cityBuild is not null
                    && ResearchVisuals.IsResearchInProgress(cityBuild, typeCode, state.Population, out var treeIndex))
                {
                    var seconds = Math.Max(1, (int)Math.Ceiling(cityBuild.ResearchTimers[treeIndex]));
                    DrawResearchTimer(spriteBatch, numbers, tileX, tileY, seconds);
                }

                if (state.Population <= 0)
                {
                    return;
                }

                var displayPop = GetDisplayPopulation(typeCode, state.Population);
                var (destX, destY, popSourceY) = GetPopulationDrawOffset(typeCode, transform.Position);
                var sourceX = Math.Clamp(displayPop, 0, 49) * GameConstants.TileSize;
                var legacySource = new Rectangle(sourceX, popSourceY, GameConstants.TileSize, GameConstants.TileSize);

                WorldSpriteMetrics.DrawLegacySprite(
                    spriteBatch,
                    population,
                    destX,
                    destY,
                    legacySource,
                    Color.White);
            });
    }

    private static void DrawEquipmentIcon(SpriteBatch spriteBatch, Texture2D items, int typeCode, int tileX, int tileY)
    {
        if (!TryGetEquipmentItemType(typeCode, out var itemType))
        {
            return;
        }

        // Full item cell is 48px; research bay needs half-size, factory ~90%.
        var isFactory = BuildingCatalog.IsFactory(typeCode);
        var iconSize = isFactory ? 43 : 24;
        var (offsetX, offsetY) = isFactory ? (50, 50) : (18, 106);

        var (sourceX, sourceY) = ItemSprites.GetWorldSpriteOrigin(itemType);
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
            Color.White);
    }

    private static bool TryGetEquipmentItemType(int typeCode, out ItemType itemType) =>
        BuildingCatalog.TryGetEquipmentItemType(typeCode, out itemType);

    private static void DrawFactoryStock(SpriteBatch spriteBatch, Texture2D numbers, int tileX, int tileY, int itemsLeft)
    {
        DrawTwoDigitNumber(spriteBatch, numbers, tileX + 56, tileY + 84, itemsLeft);
    }

    private static void DrawResearchTimer(SpriteBatch spriteBatch, Texture2D numbers, int tileX, int tileY, int seconds)
    {
        DrawTwoDigitNumber(spriteBatch, numbers, tileX + 56, tileY + 68, seconds);
    }

    private static void DrawTwoDigitNumber(SpriteBatch spriteBatch, Texture2D numbers, int x, int y, int value)
    {
        var clamped = Math.Clamp(value, 0, 99);
        var tens = clamped / 10;
        var ones = clamped % 10;
        const int digitSpacing = 16;
        var legacyDigit = new Rectangle(0, 0, LegacyNumberSize, LegacyNumberSize);

        WorldSpriteMetrics.DrawLegacySprite(
            spriteBatch,
            numbers,
            x,
            y,
            legacyDigit with { X = tens * LegacyNumberSize },
            Color.White);

        WorldSpriteMetrics.DrawLegacySprite(
            spriteBatch,
            numbers,
            x + digitSpacing,
            y,
            legacyDigit with { X = ones * LegacyNumberSize },
            Color.White);
    }

    private static int GetDisplayPopulation(int typeCode, int population)
    {
        if (BuildingCatalog.IsHouse(typeCode))
        {
            return population / 16;
        }

        return population / 8;
    }

    private static (int DestX, int DestY, int PopSourceY) GetPopulationDrawOffset(int typeCode, System.Numerics.Vector2 spriteTopLeft)
    {
        var tileX = (int)spriteTopLeft.X;
        var tileY = (int)spriteTopLeft.Y;
        var buildingType = typeCode / 100;

        return buildingType switch
        {
            2 => (tileX + 96, tileY + 33, 0),
            3 => (tileX + 92, tileY + 92, 0),
            1 => (tileX + 96, tileY + 48, 0),
            4 => (tileX + 96, tileY + 90, 0),
            _ => (tileX + 96, tileY + 49, GameConstants.TileSize),
        };
    }
}
