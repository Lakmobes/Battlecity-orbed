using BattleCity.Client.Assets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>Center-screen death message (legacy/client/CDrawing.cpp DrawTank).</summary>
public sealed class DeathOverlayRenderer
{
    private readonly AssetService _assets;
    private SpriteFont? _font;

    public DeathOverlayRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont("Fonts/MenuFont");
    }

    public void Draw(SpriteBatch spriteBatch, float respawnSecondsRemaining)
    {
        if (_font is null)
        {
            return;
        }

        var seconds = Math.Max(0, (int)Math.Ceiling(respawnSecondsRemaining));
        var centerX = UiLayout.WorldViewportWidth / 2;

        DrawCenteredLine(spriteBatch, "You have been blown up!", centerX, 270, Color.White);
        DrawCenteredLine(spriteBatch, $"You will respawn in: {seconds}", centerX, 285, Color.White);
    }

    private void DrawCenteredLine(SpriteBatch spriteBatch, string text, int centerX, int y, Color color)
    {
        var size = _font!.MeasureString(text);
        var position = new Vector2(centerX - size.X / 2f, y);
        spriteBatch.DrawString(_font, text, position + new Vector2(1f, 1f), new Color(0, 0, 0, 200));
        spriteBatch.DrawString(_font, text, position, color);
    }
}
