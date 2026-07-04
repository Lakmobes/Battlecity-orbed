using BattleCity.Client.Assets;
using BattleCity.Shared;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>Full-screen UI for menus and boot (not the in-game right rail).</summary>
public sealed class ScreenUiRenderer
{
    private readonly AssetService _assets;
    private SpriteFont? _font;

    public ScreenUiRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont("Fonts/MenuFont");
    }

    public void DrawBackdrop(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        var pixel = _assets.Pixel;
        spriteBatch.Draw(pixel, new Rectangle(0, 0, screenWidth, screenHeight), new Color(16, 20, 32));
        spriteBatch.Draw(
            pixel,
            new Rectangle(0, 0, screenWidth, 48),
            new Color(32, 40, 64, 220));
    }

    public void DrawTitle(SpriteBatch spriteBatch, int screenWidth)
    {
        DrawCenteredText(spriteBatch, GameInfo.Title, screenWidth / 2, 14, Color.White, 1.4f);
        DrawCenteredText(spriteBatch, $"v{GameInfo.Version}", screenWidth / 2, 38, new Color(180, 180, 200), 0.85f);
    }

    public void DrawMenu(
        SpriteBatch spriteBatch,
        int screenWidth,
        int screenHeight,
        IReadOnlyList<string> items,
        int selectedIndex,
        string footer)
    {
        var startY = screenHeight / 2 - items.Count * 18;
        for (var i = 0; i < items.Count; i++)
        {
            var color = i == selectedIndex ? Color.Yellow : new Color(200, 200, 220);
            var prefix = i == selectedIndex ? "> " : "  ";
            DrawCenteredText(spriteBatch, prefix + items[i], screenWidth / 2, startY + i * 28, color);
        }

        DrawCenteredText(spriteBatch, footer, screenWidth / 2, screenHeight - 32, new Color(140, 140, 160), 0.85f);
    }

    public void DrawMessageBlock(
        SpriteBatch spriteBatch,
        int screenWidth,
        int screenHeight,
        string title,
        IReadOnlyList<string> lines,
        string footer)
    {
        DrawCenteredText(spriteBatch, title, screenWidth / 2, screenHeight / 2 - 60, Color.White, 1.1f);
        for (var i = 0; i < lines.Count; i++)
        {
            DrawCenteredText(
                spriteBatch,
                lines[i],
                screenWidth / 2,
                screenHeight / 2 - 20 + i * 24,
                new Color(200, 200, 220));
        }

        DrawCenteredText(spriteBatch, footer, screenWidth / 2, screenHeight - 32, new Color(140, 140, 160), 0.85f);
    }

    public void DrawCenteredText(
        SpriteBatch spriteBatch,
        string text,
        int centerX,
        int y,
        Color color,
        float scale = 1f)
    {
        if (_font is null)
        {
            return;
        }

        var size = _font.MeasureString(text) * scale;
        var position = new Vector2(centerX - size.X / 2f, y);
        spriteBatch.DrawString(
            _font,
            text,
            position,
            color,
            0f,
            Vector2.Zero,
            new Vector2(scale, scale),
            SpriteEffects.None,
            0f);
    }

    public void DrawText(
        SpriteBatch spriteBatch,
        string text,
        int x,
        int y,
        Color color,
        float scale = 1f)
    {
        if (_font is null)
        {
            return;
        }

        spriteBatch.DrawString(
            _font,
            text,
            new Vector2(x, y),
            color,
            0f,
            Vector2.Zero,
            new Vector2(scale, scale),
            SpriteEffects.None,
            0f);
    }

    public void DrawPanel(SpriteBatch spriteBatch, Rectangle bounds, Color fill, Color border)
    {
        var pixel = _assets.Pixel;
        spriteBatch.Draw(pixel, bounds, fill);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), border);
    }
}
