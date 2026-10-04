using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>
/// Sci-fi HUD palette. Layout lives in <see cref="ModernHudLayout"/>.
/// Gameplay state is not stored here.
/// </summary>
public static class HudTheme
{
    public static readonly Color PanelFill = new(10, 18, 28, 217);
    public static readonly Color PanelFillIdle = new(10, 18, 28, 110);
    public static readonly Color PanelBorder = new(0, 229, 255, 180);
    public static readonly Color PanelBorderIdle = new(0, 136, 204, 110);
    public static readonly Color Glow = new(0, 229, 255, 90);
    public static readonly Color Accent = new(0, 229, 255);
    public static readonly Color AccentDeep = new(0, 136, 204);
    public static readonly Color Text = new(232, 244, 252);
    public static readonly Color TextMuted = new(150, 176, 196);
    public static readonly Color HealthFill = new(40, 220, 160);
    public static readonly Color HealthTrack = new(8, 16, 24, 220);
    public static readonly Color ShieldFill = new(40, 140, 255);
    public static readonly Color ShieldTrack = new(8, 14, 28, 220);
    public static readonly Color Ally = new(70, 230, 110);
    public static readonly Color Enemy = new(255, 70, 70);
    public static readonly Color Objective = new(255, 210, 60);
    public static readonly Color SlotFill = new(8, 14, 22, 210);
    public static readonly Color BadgeFill = new(0, 0, 0, 170);

    /// <summary>Chamfered panel: fill, 1px cyan edge, and a soft outer glow.</summary>
    public static void DrawPanel(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        bool active = true,
        bool idle = false)
    {
        var fill = idle ? PanelFillIdle : PanelFill;
        var border = active ? PanelBorder : PanelBorderIdle;
        if (active && !idle)
        {
            spriteBatch.Draw(pixel, new Rectangle(bounds.X - 1, bounds.Y - 1, bounds.Width + 2, bounds.Height + 2), Glow);
        }

        spriteBatch.Draw(pixel, bounds, fill);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), border);

        const int chamfer = 6;
        var cut = idle ? fill : new Color(0, 0, 0, 0);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, chamfer, 1), cut);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - chamfer, bounds.Y, chamfer, 1), cut);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 1, chamfer, 1), cut);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - chamfer, bounds.Bottom - 1, chamfer, 1), cut);
    }

    public static void DrawLabel(
        SpriteBatch spriteBatch,
        SpriteFont font,
        string text,
        Vector2 position,
        Color color,
        float scale = 0.55f)
    {
        var scaleVec = new Vector2(scale, scale);
        spriteBatch.DrawString(font, text, position + new Vector2(1f, 1f), new Color(0, 0, 0, 180), 0f, Vector2.Zero, scaleVec, SpriteEffects.None, 0f);
        spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, scaleVec, SpriteEffects.None, 0f);
    }
}
