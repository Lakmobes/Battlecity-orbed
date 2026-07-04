using BattleCity.Client.Assets;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

public sealed class InventoryPanelRenderer
{
    private static readonly Color CountColor = new(255, 255, 0);

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

    public void Draw(SpriteBatch spriteBatch, int panelX, in PlayerInventory inventory, int? playerHealth, int? playerMaxHealth)
    {
        DrawHealthBar(spriteBatch, panelX, playerHealth, playerMaxHealth);
        DrawInventoryGrid(spriteBatch, panelX, inventory);
    }

    private void DrawInventoryGrid(SpriteBatch spriteBatch, int panelX, in PlayerInventory inventory)
    {
        var items = _assets.Items;
        var selection = _assets.InventorySelection;
        var pixel = _assets.Pixel;

        for (var typeIndex = 0; typeIndex <= (int)ItemType.Plasma; typeIndex++)
        {
            var type = (ItemType)typeIndex;
            var count = inventory.GetCount(type);
            if (count <= 0)
            {
                continue;
            }

            var (drawX, drawY) = InventoryPanelLayout.GetSlotScreenPosition(panelX, type);

            if (type == inventory.SelectedItemType && selection != pixel)
            {
                spriteBatch.Draw(
                    selection,
                    new Rectangle(drawX, drawY, InventoryPanelLayout.IconSize, InventoryPanelLayout.IconSize),
                    new Rectangle(0, 0, InventoryPanelLayout.IconSize, InventoryPanelLayout.IconSize),
                    Color.White);
            }

            var (sourceX, sourceY) = ItemSprites.GetInventorySpriteOrigin(type);
            spriteBatch.Draw(
                items,
                new Rectangle(drawX, drawY, InventoryPanelLayout.IconSize, InventoryPanelLayout.IconSize),
                new Rectangle(sourceX, sourceY, InventoryPanelLayout.IconSize, InventoryPanelLayout.IconSize),
                Color.White);

            if (count > 1 && _font is not null)
            {
                var countText = count.ToString();
                var scale = new Vector2(0.75f, 0.75f);
                spriteBatch.DrawString(
                    _font,
                    countText,
                    new Vector2(drawX + 22, drawY + 12),
                    CountColor,
                    0f,
                    Vector2.Zero,
                    scale,
                    SpriteEffects.None,
                    0f);
            }
        }
    }

    private void DrawHealthBar(SpriteBatch spriteBatch, int panelX, int? playerHealth, int? playerMaxHealth)
    {
        if (!playerHealth.HasValue || !playerMaxHealth.HasValue || playerMaxHealth.Value <= 0)
        {
            return;
        }

        var healthTexture = _assets.Health;
        if (healthTexture == _assets.Pixel)
        {
            return;
        }

        const int barWidth = 38;
        const int barMaxHeight = 87;
        const int barBottom = 250;
        var percent = Math.Clamp(playerHealth.Value / (float)playerMaxHealth.Value, 0f, 1f);
        var fillHeight = Math.Max(1, (int)(barMaxHeight * percent));
        var destY = barBottom - fillHeight;

        spriteBatch.Draw(
            healthTexture,
            new Rectangle(panelX + 137, destY, barWidth, fillHeight),
            new Rectangle(0, barMaxHeight - fillHeight, barWidth, fillHeight),
            Color.White);
    }
}
