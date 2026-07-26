using BattleCity.Client.Assets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>
/// Dual compass: outer ring points to the nearest other CC (orbable city),
/// inner ring points to the home command center. Uses <see cref="HudSpriteNames.CompassRing"/>.
/// </summary>
public sealed class UnderAttackPanelRenderer
{
    private static readonly Color HomeArrowColor = new(120, 200, 255);
    private static readonly Color OrbArrowColor = new(255, 180, 70);
    private static readonly Color AttackArrowColor = Color.Red;

    private readonly AssetService _assets;
    private SpriteFont? _font;

    public UnderAttackPanelRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont(LegacySpriteNames.UiFont);
    }

    public void Draw(SpriteBatch spriteBatch, in RenderContext context)
    {
        if (_font is null)
        {
            return;
        }

        var outerBounds = ModernHudLayout.CompassBounds;
        var innerBounds = ModernHudLayout.CompassInnerBounds;
        DrawRing(spriteBatch, outerBounds, new Color(255, 255, 255, 230));
        DrawRing(spriteBatch, innerBounds, new Color(180, 220, 255, 240));

        var center = new Vector2(outerBounds.Center.X, outerBounds.Center.Y);
        var playerCenter = ToNumerics(context.FocusWorldPosition);
        var homeCenter = ToNumerics(context.CityCenterWorldPosition);
        var homeRadians = CompassArrowHelper.ComputeArrowRadians(playerCenter, homeCenter);
        var homeColor = context is { IsUnderAttack: true, UnderAttackFlashVisible: true }
            ? AttackArrowColor
            : HomeArrowColor;

        if (context.NearestOrbableCityWorldPosition is { } orbTarget)
        {
            var orbRadians = CompassArrowHelper.ComputeArrowRadians(playerCenter, ToNumerics(orbTarget));
            DrawCompassArrow(spriteBatch, _assets.Pixel, center, orbRadians, OrbArrowColor, tipLength: 42f, wingLength: 16f);
            DrawCornerLabel(spriteBatch, outerBounds, "ORB", OrbArrowColor, topLeft: false);
        }

        DrawCompassArrow(spriteBatch, _assets.Pixel, center, homeRadians, homeColor, tipLength: 22f, wingLength: 10f);
        DrawCornerLabel(spriteBatch, outerBounds, "CC", homeColor, topLeft: true);

        if (context is { IsUnderAttack: true, UnderAttackFlashVisible: true })
        {
            var alert = "ATTACK";
            var alertScale = new Vector2(0.65f, 0.65f);
            var alertSize = _font.MeasureString(alert) * alertScale;
            spriteBatch.DrawString(
                _font,
                alert,
                new Vector2(outerBounds.Center.X - alertSize.X / 2f, outerBounds.Bottom - 18),
                Color.Red,
                0f,
                Vector2.Zero,
                alertScale,
                SpriteEffects.None,
                0f);
        }
    }

    private void DrawRing(SpriteBatch spriteBatch, Rectangle bounds, Color tint)
    {
        var ring = _assets.HudCompassRing;
        if (ring != _assets.Pixel)
        {
            spriteBatch.Draw(ring, bounds, tint);
            return;
        }

        HudOverlayHelper.DrawPanel(
            spriteBatch,
            _assets,
            bounds,
            new Color(8, 12, 24, 150));
    }

    private void DrawCornerLabel(SpriteBatch spriteBatch, Rectangle bounds, string text, Color color, bool topLeft)
    {
        if (_font is null)
        {
            return;
        }

        var scale = new Vector2(0.55f, 0.55f);
        var size = _font.MeasureString(text) * scale;
        var position = topLeft
            ? new Vector2(bounds.X + 6, bounds.Y + 4)
            : new Vector2(bounds.Right - size.X - 6, bounds.Y + 4);
        spriteBatch.DrawString(
            _font,
            text,
            position,
            color,
            0f,
            Vector2.Zero,
            scale,
            SpriteEffects.None,
            0f);
    }

    private static void DrawCompassArrow(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 center,
        float radians,
        Color color,
        float tipLength,
        float wingLength)
    {
        var tip = center + new Vector2(MathF.Sin(radians), -MathF.Cos(radians)) * tipLength;
        var left = center + new Vector2(MathF.Sin(radians + 2.6f), -MathF.Cos(radians + 2.6f)) * wingLength;
        var right = center + new Vector2(MathF.Sin(radians - 2.6f), -MathF.Cos(radians - 2.6f)) * wingLength;

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
