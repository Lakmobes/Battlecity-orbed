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
    private static readonly Color ReadyColor = new(0, 229, 255);
    private static readonly Color RechargeFillColor = new(0, 180, 220, 200);
    private static readonly Color RechargeEmptyColor = new(0, 0, 0, 140);

    private readonly AssetService _assets;
    private SpriteFont? _font;

    public InventoryPanelRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont(LegacySpriteNames.HudFont);
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

    private void DrawSlotBadge(SpriteBatch spriteBatch, Rectangle slotBounds, string label)
    {
        if (_font is null)
        {
            return;
        }

        var badge = new Rectangle(slotBounds.X + 2, slotBounds.Y + 2, label.Length > 1 ? 18 : 14, 12);
        spriteBatch.Draw(_assets.Pixel, badge, HudTheme.BadgeFill);
        HudTheme.DrawLabel(
            spriteBatch,
            _font,
            label,
            new Vector2(badge.X + 2, badge.Y - 1),
            HudTheme.Accent,
            scale: 0.75f);
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
        var slotCount = visible.Count;
        var iconSize = (int)(InventoryPanelLayout.IconSize * 0.72f);

        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            var (type, count) = visible[slotIndex];
            var (drawX, drawY) = InventoryPanelLayout.GetSlotScreenPosition(slotIndex, slotCount);
            var slotBounds = new Rectangle(drawX, drawY, InventoryPanelLayout.IconSize, InventoryPanelLayout.IconSize);
            var isSelected = type == inventory.SelectedItemType;
            var slotFrame = isSelected ? _assets.HudSlotSelected : _assets.HudSlot;
            if (slotFrame != _assets.Pixel)
            {
                spriteBatch.Draw(slotFrame, slotBounds, Color.White);
            }
            else
            {
                HudTheme.DrawPanel(spriteBatch, _assets.Pixel, slotBounds, active: isSelected);
            }

            if (isSelected && slotFrame == _assets.Pixel)
            {
                var glow = new Rectangle(slotBounds.X - 2, slotBounds.Y - 2, slotBounds.Width + 4, slotBounds.Height + 4);
                spriteBatch.Draw(_assets.Pixel, new Rectangle(glow.X, glow.Y, glow.Width, 2), HudTheme.Accent);
                spriteBatch.Draw(_assets.Pixel, new Rectangle(glow.X, glow.Bottom - 2, glow.Width, 2), HudTheme.Accent);
                spriteBatch.Draw(_assets.Pixel, new Rectangle(glow.X, glow.Y, 2, glow.Height), HudTheme.Accent);
                spriteBatch.Draw(_assets.Pixel, new Rectangle(glow.Right - 2, glow.Y, 2, glow.Height), HudTheme.Accent);
            }

            DrawSlotBadge(spriteBatch, slotBounds, (slotIndex + 1).ToString());

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
                    Vector2.One,
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
        var rowWidth = inventorySlotCount > 0
            ? inventorySlotCount * ModernHudLayout.InventorySlotSize
              + (inventorySlotCount - 1) * ModernHudLayout.InventorySlotSpacing
            : ModernHudLayout.HealthBarWidth;
        var barWidth = Math.Min(rowWidth, 720);
        var x = ModernHudLayout.GetCenteredRowStartX(inventorySlotCount);
        if (inventorySlotCount <= 0)
        {
            x = (UiLayout.LogicalWidth - barWidth) / 2;
        }

        var bounds = new Rectangle(x, ModernHudLayout.HealthBarY, barWidth, ModernHudLayout.HealthBarHeight);
        var current = playerHealth ?? 0;
        var max = playerMaxHealth ?? 0;
        var percent = max > 0 ? Math.Clamp(current / (float)max, 0f, 1f) : 0f;
        var frame = _assets.LoadTexture(HudSpriteNames.HealthBar);
        if (frame != _assets.Pixel)
        {
            spriteBatch.Draw(frame, bounds, Color.White);
        }
        else
        {
            spriteBatch.Draw(_assets.Pixel, bounds, HudTheme.HealthTrack);
        }

        var inner = new Rectangle(bounds.X + 4, bounds.Y + 4, Math.Max(0, bounds.Width - 8), Math.Max(0, bounds.Height - 8));
        var fillWidth = (int)(inner.Width * percent);
        if (fillWidth > 0)
        {
            var fillArt = _assets.LoadTexture(HudSpriteNames.HealthFill);
            if (fillArt == _assets.Pixel)
            {
                fillArt = _assets.Health;
            }

            if (fillArt != _assets.Pixel)
            {
                var sourceWidth = Math.Max(1, (int)(fillArt.Width * percent));
                spriteBatch.Draw(
                    fillArt,
                    new Rectangle(inner.X, inner.Y, fillWidth, inner.Height),
                    new Rectangle(0, 0, sourceWidth, fillArt.Height),
                    Color.White);
            }
            else
            {
                spriteBatch.Draw(_assets.Pixel, new Rectangle(inner.X, inner.Y, fillWidth, inner.Height), HudTheme.HealthFill);
            }
        }
    }
}
