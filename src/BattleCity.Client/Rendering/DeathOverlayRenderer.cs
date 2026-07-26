using BattleCity.Client.Assets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>Center-screen death message (legacy/client/CDrawing.cpp DrawTank).</summary>
public sealed class DeathOverlayRenderer
{
    private const float FadeInSeconds = 0.35f;

    private readonly AssetService _assets;
    private SpriteFont? _font;
    private float _fade;
    private bool _visibleLastFrame;

    public DeathOverlayRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont(LegacySpriteNames.UiFont);
    }

    public void Reset()
    {
        _fade = 0f;
        _visibleLastFrame = false;
    }

    public void Draw(SpriteBatch spriteBatch, float respawnSecondsRemaining, float deltaSeconds = 1f / 60f)
    {
        if (_font is null)
        {
            return;
        }

        if (!_visibleLastFrame)
        {
            _fade = 0f;
        }

        _visibleLastFrame = true;
        _fade = Math.Min(1f, _fade + deltaSeconds / FadeInSeconds);
        var alpha = _fade;

        var seconds = Math.Max(0, (int)Math.Ceiling(respawnSecondsRemaining));
        var centerX = ModernHudLayout.ScreenCenterX;
        var centerY = ModernHudLayout.ScreenCenterY;

        var panel = new Rectangle(centerX - 240, centerY - 56, 480, 112);
        var panelColor = new Color(10, 12, 20, (int)(210 * alpha));
        HudOverlayHelper.DrawPanel(spriteBatch, _assets, panel, panelColor);

        var title = new Color(255, 255, 255, (int)(255 * alpha));
        var timer = new Color(255, 220, 120, (int)(255 * alpha));
        DrawCenteredLine(spriteBatch, "You have been blown up!", centerX, centerY - 22, title);
        var timerLine = seconds > 0
            ? $"You will respawn in: {seconds}"
            : "Respawning...";
        DrawCenteredLine(spriteBatch, timerLine, centerX, centerY + 10, timer);
    }

    public void NotifyHidden()
    {
        _visibleLastFrame = false;
        _fade = 0f;
    }

    private void DrawCenteredLine(SpriteBatch spriteBatch, string text, int centerX, int y, Color color)
    {
        var size = _font!.MeasureString(text);
        var position = new Vector2(centerX - size.X / 2f, y);
        var shadow = new Color((byte)0, (byte)0, (byte)0, (byte)(200 * (color.A / 255f)));
        spriteBatch.DrawString(_font, text, position + new Vector2(1f, 1f), shadow);
        spriteBatch.DrawString(_font, text, position, color);
    }
}
