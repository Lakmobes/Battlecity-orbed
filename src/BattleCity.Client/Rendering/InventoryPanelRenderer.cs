using BattleCity.Client.Assets;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

public sealed class InventoryPanelRenderer
{
    private static readonly Color CountColor = new(255, 230, 90);
    private static readonly Color ReadyColor = new(120, 220, 255);
    private static readonly Color RechargeFillColor = new(70, 180, 220, 200);
    private static readonly Color RechargeEmptyColor = new(0, 0, 0, 140);
    private static readonly Color HealthFillColor = new(70, 195, 95);
    private static readonly Color HealthFillHighlight = new(140, 235, 150, 120);
    private static readonly Color HealthEmptyColor = new(18, 20, 28, 210);
    private static readonly Color HealthBorderColor = new(255, 255, 255, 70);

    private readonly AssetService _assets;
    private SpriteFont? _font;

    public InventoryPanelRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont(LegacySpriteNames.UiFont);
    }

    public void Draw(
        SpriteBatch spriteBatch,
        in PlayerInventory inventory,
        int? playerHealth,
        int? playerMaxHealth,
        float cloakRechargeSeconds = 0f,
        float flareRechargeSeconds = 0f,
        bool cloakRechargeUnlocked = false,
        bool flareRechargeUnlocked = false)
    {
        var visible = CollectVisibleItems(in inventory);
        DrawInventoryRow(
            spriteBatch,
            in inventory,
            visible,
            cloakRechargeSeconds,
            flareRechargeSeconds,
            cloakRechargeUnlocked,
            flareRechargeUnlocked);
        DrawHealthBar(spriteBatch, playerHealth, playerMaxHealth, visible.Count);
    }

    private static List<(ItemType Type, int Count)> CollectVisibleItems(in PlayerInventory inventory)
    {
        // Show gear + placeables (including count 0) so the player can see what they own.
        var visible = new List<(ItemType, int)>(capacity: PlayerInventory.HudItems.Length);
        foreach (var type in PlayerInventory.HudItems)
        {
            visible.Add((type, inventory.GetCount(type)));
        }

        return visible;
    }

    private void DrawInventoryRow(
        SpriteBatch spriteBatch,
        in PlayerInventory inventory,
        List<(ItemType Type, int Count)> visible,
        float cloakRechargeSeconds,
        float flareRechargeSeconds,
        bool cloakRechargeUnlocked,
        bool flareRechargeUnlocked)
    {
        var items = _assets.Items;
        var slotTexture = _assets.HudSlot;
        var selectedTexture = _assets.HudSlotSelected;
        var slotCount = visible.Count;
        var iconSize = (int)(InventoryPanelLayout.IconSize * 0.72f);

        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            var (type, count) = visible[slotIndex];
            var (drawX, drawY) = InventoryPanelLayout.GetSlotScreenPosition(slotIndex, slotCount);
            var slotBounds = new Rectangle(drawX, drawY, InventoryPanelLayout.IconSize, InventoryPanelLayout.IconSize);
            var isSelected = type == inventory.SelectedItemType;
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

            var rechargeUnlocked = type switch
            {
                ItemType.Cloak => cloakRechargeUnlocked,
                ItemType.Flare => flareRechargeUnlocked,
                _ => false,
            };
            var rechargeSeconds = type switch
            {
                ItemType.Cloak => cloakRechargeSeconds,
                ItemType.Flare => flareRechargeSeconds,
                _ => 0f,
            };
            var isRechargeItem = rechargeUnlocked;
            var isReady = isRechargeItem && rechargeSeconds <= 0f;
            var hasItem = isRechargeItem ? isReady : count > 0;

            var iconInset = (InventoryPanelLayout.IconSize - iconSize) / 2;
            var (sourceX, sourceY) = ItemSprites.GetInventorySpriteOrigin(type);
            var legacySource = new Rectangle(
                sourceX,
                sourceY,
                ItemSprites.WorldSpriteSize,
                ItemSprites.WorldSpriteSize);
            var iconColor = hasItem ? Color.White : Color.White * 0.35f;
            spriteBatch.Draw(
                items,
                new Rectangle(drawX + iconInset, drawY + iconInset, iconSize, iconSize),
                WorldSpriteMetrics.ScaleSource(legacySource),
                iconColor);

            if (isRechargeItem)
            {
                DrawRechargeOverlay(
                    spriteBatch,
                    slotBounds,
                    rechargeSeconds,
                    isReady);
            }
            else if (_font is not null)
            {
                var countText = count.ToString();
                spriteBatch.DrawString(
                    _font,
                    countText,
                    new Vector2(drawX + InventoryPanelLayout.IconSize - 18, drawY + 4),
                    count > 0 ? CountColor : new Color(140, 140, 150),
                    0f,
                    Vector2.Zero,
                    new Vector2(0.9f, 0.9f),
                    SpriteEffects.None,
                    0f);
            }
        }
    }

    private void DrawRechargeOverlay(
        SpriteBatch spriteBatch,
        Rectangle slotBounds,
        float rechargeSeconds,
        bool isReady)
    {
        var pixel = _assets.Pixel;
        var barHeight = 6;
        var barBounds = new Rectangle(
            slotBounds.X + 4,
            slotBounds.Bottom - barHeight - 4,
            slotBounds.Width - 8,
            barHeight);

        spriteBatch.Draw(pixel, barBounds, RechargeEmptyColor);

        var progress = isReady
            ? 1f
            : Math.Clamp(1f - (rechargeSeconds / EconomyConstants.AbilityRechargeSeconds), 0f, 1f);
        var fillWidth = Math.Max(isReady ? 1 : 0, (int)(barBounds.Width * progress));
        if (fillWidth > 0)
        {
            spriteBatch.Draw(
                pixel,
                new Rectangle(barBounds.X, barBounds.Y, fillWidth, barBounds.Height),
                isReady ? ReadyColor : RechargeFillColor);
        }

        if (_font is not null)
        {
            var label = isReady ? "R" : $"{Math.Ceiling(rechargeSeconds)}";
            spriteBatch.DrawString(
                _font,
                label,
                new Vector2(slotBounds.Right - 18, slotBounds.Y + 4),
                isReady ? ReadyColor : CountColor,
                0f,
                Vector2.Zero,
                new Vector2(0.9f, 0.9f),
                SpriteEffects.None,
                0f);
        }
    }

    private void DrawHealthBar(
        SpriteBatch spriteBatch,
        int? playerHealth,
        int? playerMaxHealth,
        int inventorySlotCount)
    {
        var pixel = _assets.Pixel;
        var rowWidth = inventorySlotCount > 0
            ? inventorySlotCount * ModernHudLayout.InventorySlotSize
              + (inventorySlotCount - 1) * ModernHudLayout.InventorySlotSpacing
            : ModernHudLayout.HealthBarWidth;
        var barWidth = Math.Max(ModernHudLayout.HealthBarWidth, rowWidth);
        var x = (UiLayout.LogicalWidth - barWidth) / 2;
        var y = ModernHudLayout.HealthBarY;
        var bounds = new Rectangle(x, y, barWidth, ModernHudLayout.HealthBarHeight);

        HudOverlayHelper.DrawFlatPanel(spriteBatch, pixel, bounds, HealthEmptyColor, borderThickness: 0);

        if (playerHealth.HasValue && playerMaxHealth.HasValue && playerMaxHealth.Value > 0)
        {
            var percent = Math.Clamp(playerHealth.Value / (float)playerMaxHealth.Value, 0f, 1f);
            var fillWidth = Math.Max(1, (int)(bounds.Width * percent));
            var fillBounds = new Rectangle(bounds.X, bounds.Y, fillWidth, bounds.Height);
            spriteBatch.Draw(pixel, fillBounds, HealthFillColor);
            spriteBatch.Draw(
                pixel,
                new Rectangle(fillBounds.X, fillBounds.Y, fillBounds.Width, Math.Max(1, fillBounds.Height / 3)),
                HealthFillHighlight);

            if (_font is not null)
            {
                var label = $"{playerHealth}/{playerMaxHealth}";
                var size = _font.MeasureString(label) * 0.85f;
                var labelPos = new Vector2(bounds.Center.X - size.X / 2f, bounds.Center.Y - size.Y / 2f);
                spriteBatch.DrawString(
                    _font,
                    label,
                    labelPos + new Vector2(1f, 1f),
                    new Color(0, 0, 0, 180),
                    0f,
                    Vector2.Zero,
                    new Vector2(0.85f, 0.85f),
                    SpriteEffects.None,
                    0f);
                spriteBatch.DrawString(
                    _font,
                    label,
                    labelPos,
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
                new Vector2(bounds.X + 8, bounds.Y + 2),
                new Color(200, 200, 210),
                0f,
                Vector2.Zero,
                new Vector2(0.75f, 0.75f),
                SpriteEffects.None,
                0f);
        }

        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), HealthBorderColor);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), HealthBorderColor);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), HealthBorderColor);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), HealthBorderColor);
    }
}
