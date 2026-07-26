using BattleCity.Client.Assets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>Center-screen research completion message.</summary>
public sealed class ResearchCompleteOverlayRenderer
{
    private readonly AssetService _assets;
    private SpriteFont? _font;

    public ResearchCompleteOverlayRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont(LegacySpriteNames.UiFont);
    }

    public void Draw(SpriteBatch spriteBatch, string message)
    {
        if (_font is null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var centerX = ModernHudLayout.ScreenCenterX;
        DrawCenteredLine(spriteBatch, "RESEARCH COMPLETE!", centerX, ModernHudLayout.ScreenCenterY - 40, Color.Gold);

        var lines = message.Split('\n');
        var y = 275;
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
