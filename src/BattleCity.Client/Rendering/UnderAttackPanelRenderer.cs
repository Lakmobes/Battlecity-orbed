using BattleCity.Client.Assets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>Transparent compass overlay pointing toward the command center.</summary>
public sealed class UnderAttackPanelRenderer
{
    private readonly AssetService _assets;
    private SpriteFont? _font;

    public UnderAttackPanelRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont("Fonts/MenuFont");
    }

    public void Draw(SpriteBatch spriteBatch, in RenderContext context)
    {
        if (_font is null)
        {
            return;
        }

        var bounds = ModernHudLayout.CompassBounds;
        var ring = _assets.HudCompassRing;
        if (ring != _assets.Pixel)
        {
            spriteBatch.Draw(ring, bounds, Color.White);
        }
        else
        {
            HudOverlayHelper.DrawPanel(
                spriteBatch,
                _assets,
                bounds,
                new Color(8, 12, 24, 150));
        }

        var center = new Vector2(bounds.Center.X, bounds.Center.Y);
        var playerCenter = ToNumerics(context.FocusWorldPosition);
        var cityCenter = ToNumerics(context.CityCenterWorldPosition);
        var glyph = CompassArrowHelper.GetDirectionGlyph(playerCenter, cityCenter);
        var arrowRadians = CompassArrowHelper.ComputeArrowRadians(playerCenter, cityCenter);
        var arrowColor = context is { IsUnderAttack: true, UnderAttackFlashVisible: true }
            ? Color.Red
            : new Color(120, 200, 255);

        DrawCompassArrow(spriteBatch, _assets.Pixel, center, arrowRadians, arrowColor);

        var labelScale = new Vector2(0.7f, 0.7f);
        var labelSize = _font.MeasureString(glyph) * labelScale;
        spriteBatch.DrawString(
            _font,
            glyph,
            center - labelSize / 2f + new Vector2(0, 10),
            arrowColor,
            0f,
            Vector2.Zero,
            labelScale,
            SpriteEffects.None,
            0f);

        spriteBatch.DrawString(
            _font,
            "CC",
            new Vector2(bounds.X + 8, bounds.Y + 6),
            new Color(180, 180, 200),
            0f,
            Vector2.Zero,
            new Vector2(0.6f, 0.6f),
            SpriteEffects.None,
            0f);

        if (context is { IsUnderAttack: true, UnderAttackFlashVisible: true })
        {
            var alert = "ATTACK";
            var alertScale = new Vector2(0.65f, 0.65f);
            var alertSize = _font.MeasureString(alert) * alertScale;
            spriteBatch.DrawString(
                _font,
                alert,
                new Vector2(bounds.Center.X - alertSize.X / 2f, bounds.Bottom - 18),
                Color.Red,
                0f,
                Vector2.Zero,
                alertScale,
                SpriteEffects.None,
                0f);
        }
    }

    private static void DrawCompassArrow(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 center,
        float radians,
        Color color)
    {
        var tip = center + new Vector2(MathF.Sin(radians), -MathF.Cos(radians)) * 28f;
        var left = center + new Vector2(MathF.Sin(radians + 2.6f), -MathF.Cos(radians + 2.6f)) * 14f;
        var right = center + new Vector2(MathF.Sin(radians - 2.6f), -MathF.Cos(radians - 2.6f)) * 14f;

        DrawLine(spriteBatch, pixel, left, tip, color, 3);
        DrawLine(spriteBatch, pixel, right, tip, color, 3);
        DrawLine(spriteBatch, pixel, left, right, color, 2);
        DrawLine(spriteBatch, pixel, center, tip, color, 2);
    }

    private static void DrawLine(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 start,
        Vector2 end,
        Color color,
        int thickness)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length <= 0.5f)
        {
            return;
        }

        var angle = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(
            pixel,
            start,
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);
    }

    private static System.Numerics.Vector2 ToNumerics(Vector2 value) => new(value.X, value.Y);
}
