using BattleCity.Client.Assets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

internal static class HudOverlayHelper
{
    public static void DrawPanel(
        SpriteBatch spriteBatch,
        AssetService assets,
        Rectangle bounds,
        Color fallbackFill,
        int borderThickness = 1)
    {
        var panel = assets.LoadTexture(HudSpriteNames.Panel);
        if (panel != assets.Pixel)
        {
            NineSliceRenderer.Draw(
                spriteBatch,
                panel,
                bounds,
                HudSpriteNames.PanelBorder,
                Color.White);
            return;
        }

        DrawFlatPanel(spriteBatch, assets.Pixel, bounds, fallbackFill, borderThickness);
    }

    public static void DrawFlatPanel(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color fill,
        int borderThickness = 1)
    {
        spriteBatch.Draw(pixel, bounds, fill);

        if (borderThickness <= 0)
        {
            return;
        }

        var border = new Color(255, 255, 255, 40);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, borderThickness), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - borderThickness, bounds.Width, borderThickness), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, borderThickness, bounds.Height), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - borderThickness, bounds.Y, borderThickness, bounds.Height), border);
    }
}
