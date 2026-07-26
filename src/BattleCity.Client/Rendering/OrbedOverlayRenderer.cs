using BattleCity.Client.Assets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>Center-screen orbed notification (legacy CProcess::ProcessOrbed).</summary>
public sealed class OrbedOverlayRenderer
{
    private readonly AssetService _assets;
    private SpriteFont? _font;

    public OrbedOverlayRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont(LegacySpriteNames.UiFont);
    }

    public void Draw(SpriteBatch spriteBatch, string message, bool isVictim)
    {
        if (_font is null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var centerX = ModernHudLayout.ScreenCenterX;
        var title = isVictim ? "CITY ORBED!" : "ORB STRIKE!";
        var titleColor = isVictim ? Color.Red : Color.Gold;

        DrawCenteredLine(spriteBatch, title, centerX, ModernHudLayout.ScreenCenterY - 60, titleColor);

        var lines = message.Split('\n');
        var y = ModernHudLayout.ScreenCenterY - 30;
        foreach (var line in lines)
        {
            DrawCenteredLine(spriteBatch, line, centerX, y, Color.White);
            y += 18;
        }
    }

    private void DrawCenteredLine(SpriteBatch spriteBatch, string text, int centerX, int y, Color color)
    {
        var size = _font!.MeasureString(text);
        var position = new Vector2(centerX - size.X / 2f, y);
        spriteBatch.DrawString(_font, text, position + new Vector2(1f, 1f), new Color(0, 0, 0, 200));
        spriteBatch.DrawString(_font, text, position, color);
    }
}
