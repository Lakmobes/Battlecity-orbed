using BattleCity.Client.Assets;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Data;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

public sealed class InventoryPanelRenderer
{
    private static readonly Color CountColor = new(255, 255, 0);
    private static readonly Color HealthFillColor = new(80, 200, 90);
    private static readonly Color HealthEmptyColor = new(40, 40, 50, 180);
    private static readonly Color HealthBorderColor = new(255, 255, 255, 60);

    private readonly AssetService _assets;
    private SpriteFont? _font;

    public InventoryPanelRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont("Fonts/MenuFont");
    }

    public void Draw(
        SpriteBatch spriteBatch,
        in PlayerInventory inventory,
        int? playerHealth,
        int? playerMaxHealth)
    {
        DrawHealthBar(spriteBatch, playerHealth, playerMaxHealth);
        DrawInventoryRow(spriteBatch, inventory);
    }

    private void DrawInventoryRow(SpriteBatch spriteBatch, in PlayerInventory inventory)
    {
        var items = _assets.Items;
        var slotTexture = _assets.HudSlot;
        var selectedTexture = _assets.HudSlotSelected;
        var slotIndex = 0;

        for (var typeIndex = 0; typeIndex <= (int)ItemType.Plasma; typeIndex++)
        {
            var type = (ItemType)typeIndex;
            var count = inventory.GetCount(type);
            if (count <= 0)
            {
                continue;
            }

            // Placeable items cycle with [ ] / D-drop; gear (missiles, medkit, cloak, flare) is hotkey-only.
            var isPlaceable = ItemCatalog.IsPlaceable(type);
            var (drawX, drawY) = InventoryPanelLayout.GetSlotScreenPosition(slotIndex);
            var slotBounds = new Rectangle(drawX, drawY, InventoryPanelLayout.IconSize, InventoryPanelLayout.IconSize);
            var isSelected = isPlaceable && type == inventory.SelectedItemType;
            var frame = isSelected && selectedTexture != _assets.Pixel
                ? selectedTexture
                : slotTexture;

            if (frame != _assets.Pixel)
            {
                spriteBatch.Draw(frame, slotBounds, Color.White);
            }
            else
            {
                HudOverlayHelper.DrawFlatPanel(
                    spriteBatch,
                    _assets.Pixel,
                    slotBounds,
                    new Color(0, 0, 0, 90));
            }

            var iconSize = 32;
            var iconInset = (InventoryPanelLayout.IconSize - iconSize) / 2;
            var (sourceX, sourceY) = ItemSprites.GetInventorySpriteOrigin(type);
            spriteBatch.Draw(
                items,
                new Rectangle(drawX + iconInset, drawY + iconInset, iconSize, iconSize),
                new Rectangle(sourceX, sourceY, iconSize, iconSize),
                Color.White);

            if (count > 1 && _font is not null)
            {
                var countText = count.ToString();
                spriteBatch.DrawString(
                    _font,
                    countText,
                    new Vector2(drawX + InventoryPanelLayout.IconSize - 14, drawY + 2),
                    CountColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(0.8f, 0.8f),
                    SpriteEffects.None,
                    0f);
            }

            slotIndex++;
        }
    }

    private void DrawHealthBar(SpriteBatch spriteBatch, int? playerHealth, int? playerMaxHealth)
    {
        var pixel = _assets.Pixel;
        var x = ModernHudLayout.TopBarPadding;
        var y = ModernHudLayout.TopBarPadding + (ModernHudLayout.TopBarHeight - ModernHudLayout.TopBarPadding * 2 - ModernHudLayout.HealthBarHeight) / 2;
        var bounds = new Rectangle(x, y, ModernHudLayout.HealthBarWidth, ModernHudLayout.HealthBarHeight);

        HudOverlayHelper.DrawPanel(spriteBatch, _assets, bounds, HealthEmptyColor, borderThickness: 0);

        if (playerHealth.HasValue && playerMaxHealth.HasValue && playerMaxHealth.Value > 0)
        {
            var percent = Math.Clamp(playerHealth.Value / (float)playerMaxHealth.Value, 0f, 1f);
            var fillWidth = Math.Max(1, (int)(bounds.Width * percent));
            spriteBatch.Draw(
                pixel,
                new Rectangle(bounds.X, bounds.Y, fillWidth, bounds.Height),
                HealthFillColor);

            if (_font is not null)
            {
                var label = $"{playerHealth}/{playerMaxHealth}";
                var size = _font.MeasureString(label) * 0.85f;
                spriteBatch.DrawString(
                    _font,
                    label,
                    new Vector2(bounds.Center.X - size.X / 2f, bounds.Center.Y - size.Y / 2f),
                    Color.White,
                    0f,
                    Vector2.Zero,
                    new Vector2(0.85f, 0.85f),
                    SpriteEffects.None,
                    0f);
            }
        }
        else if (_font is not null)
        {
            spriteBatch.DrawString(
                _font,
                "HP",
                new Vector2(bounds.X + 8, bounds.Y + 3),
                new Color(200, 200, 210),
                0f,
                Vector2.Zero,
                new Vector2(0.75f, 0.75f),
                SpriteEffects.None,
                0f);
        }

        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), HealthBorderColor);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), HealthBorderColor);
    }
}
