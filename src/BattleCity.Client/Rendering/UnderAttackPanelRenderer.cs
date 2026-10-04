using BattleCity.Client.Assets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>
/// Dual compass: outer ring points to the nearest other CC (orbable city),
/// inner ring points to the home command center. Uses legacy 8-sector arrow
/// sprites (<c>imgArrows</c> / <c>imgArrowsRed</c>) for home direction (**P-2**).
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
        _font = _assets.LoadFont(LegacySpriteNames.HudFont);
    }

    public void Draw(SpriteBatch spriteBatch, in RenderContext context)
    {
        if (_font is null)
        {
            return;
        }

        var homeBounds = ModernHudLayout.CompassBounds;
        var orbBounds = ModernHudLayout.OrbCompassBounds;
        DrawRing(spriteBatch, homeBounds, new Color(255, 255, 255, 230));
        DrawRing(spriteBatch, orbBounds, new Color(255, 210, 120, 230));

        var playerCenter = ToNumerics(context.FocusWorldPosition);
        var homeCenter = ToNumerics(context.CityCenterWorldPosition);
        var homeIndex = CompassArrowHelper.ComputeArrowIndex(playerCenter, homeCenter);
        var underAttackFlash = context is { IsUnderAttack: true, UnderAttackFlashVisible: true };
        DrawArrowSprite(
            spriteBatch,
            new Vector2(homeBounds.Center.X, homeBounds.Center.Y),
            homeIndex,
            underAttackFlash ? _assets.HudCompassArrowsRed : _assets.HudCompassArrows,
            underAttackFlash ? AttackArrowColor : HomeArrowColor);
        DrawCornerLabel(spriteBatch, homeBounds, "CC", underAttackFlash ? AttackArrowColor : HomeArrowColor, topLeft: true);

        if (context.NearestOtherCityWorldPosition is { } targetCity)
        {
            var targetIndex = CompassArrowHelper.ComputeArrowIndex(playerCenter, ToNumerics(targetCity));
            DrawArrowSprite(
                spriteBatch,
                new Vector2(orbBounds.Center.X, orbBounds.Center.Y),
                targetIndex,
                _assets.HudCompassOrb,
                context.NearestOtherCityIsOrbable ? OrbArrowColor : new Color(200, 200, 210));
            DrawCornerLabel(
                spriteBatch,
                orbBounds,
                context.NearestOtherCityIsOrbable ? "ORB" : "CITY",
                context.NearestOtherCityIsOrbable ? OrbArrowColor : new Color(200, 200, 210),
                topLeft: false);
        }

        if (underAttackFlash)
        {
            var alert = "ATTACK";
            var alertScale = Vector2.One;
            var alertSize = _font.MeasureString(alert) * alertScale;
            spriteBatch.DrawString(
                _font,
                alert,
                new Vector2(homeBounds.Center.X - alertSize.X / 2f, homeBounds.Bottom - 18),
                Color.Red,
                0f,
                Vector2.Zero,
                alertScale,
                SpriteEffects.None,
                0f);
        }
    }

    private void DrawArrowSprite(
        SpriteBatch spriteBatch,
        Vector2 center,
        int arrowIndex,
        Texture2D sheet,
        Color fallbackColor)
    {
        if (sheet != _assets.Pixel
            && sheet.Width >= CompassArrowHelper.LegacyArrowFrameSize * CompassArrowHelper.LegacyArrowFrameCount)
        {
            var frame = CompassArrowHelper.ToLegacyArrowFrame(arrowIndex);
            var source = new Rectangle(
                frame * CompassArrowHelper.LegacyArrowFrameSize,
                0,
                CompassArrowHelper.LegacyArrowFrameSize,
                CompassArrowHelper.LegacyArrowFrameSize);
            var destSize = 48;
            var dest = new Rectangle(
                (int)center.X - destSize / 2,
                (int)center.Y - destSize / 2,
                destSize,
                destSize);
            spriteBatch.Draw(sheet, dest, source, Color.White);
            return;
        }

        var radians = CompassArrowHelper.RadiansFromArrowIndex(arrowIndex);
        DrawCompassArrow(spriteBatch, _assets.Pixel, center, radians, fallbackColor, tipLength: 22f, wingLength: 10f);
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

        var scale = Vector2.One;
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
