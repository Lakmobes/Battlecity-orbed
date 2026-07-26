using BattleCity.Client.Assets;
using BattleCity.Shared;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Gameplay;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>1920×1080 title / boot presentation with full-bleed logo art and large menu.</summary>
public sealed class TitleScreenRenderer
{
    private static readonly Color SkyTop = new(8, 12, 22);
    private static readonly Color SkyMid = new(18, 28, 44);
    private static readonly Color HorizonGlow = new(180, 70, 28, 90);

    private readonly AssetService _assets;
    private SpriteFont? _font;
    private float _timeSeconds;
    private float _scrollPixels;
    private bool _hasFullBleedLogo;

    public TitleScreenRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont("Fonts/MenuFont");
    }

    public void Update(float deltaSeconds)
    {
        _timeSeconds += deltaSeconds;
        _scrollPixels = (_scrollPixels + deltaSeconds * 28f) % GameConstants.TileSize;
    }

    public void DrawBoot(SpriteBatch spriteBatch, int width, int height)
    {
        DrawBackground(spriteBatch, width, height);
        DrawCentered(
            spriteBatch,
            "Loading...",
            width / 2,
            height - 120,
            MenuTheme.TextMuted,
            1.1f);
    }

    public void DrawMainMenu(
        SpriteBatch spriteBatch,
        int width,
        int height,
        IReadOnlyList<string> items,
        int selectedIndex,
        string footer)
    {
        DrawBackground(spriteBatch, width, height);
        if (!_hasFullBleedLogo)
        {
            DrawDecorativeTanks(spriteBatch, width, height);
        }

        DrawVersion(spriteBatch, width);
        DrawMenuColumn(spriteBatch, width, height, items, selectedIndex);
        DrawCentered(spriteBatch, footer, width / 2, height - 48, MenuTheme.TextMuted, 0.85f);
    }

    private void DrawBackground(SpriteBatch spriteBatch, int width, int height)
    {
        var logo = _assets.TitleLogo;
        _hasFullBleedLogo = logo != _assets.Pixel;
        if (_hasFullBleedLogo)
        {
            // Stretch BCLogo to the full logical frame (1920×1080).
            spriteBatch.Draw(logo, new Rectangle(0, 0, width, height), Color.White);
            spriteBatch.Draw(
                _assets.Pixel,
                new Rectangle(0, height - 420, width, 420),
                new Color(0, 0, 0, 120));
            return;
        }

        DrawAtmosphere(spriteBatch, width, height);
        DrawHorizon(spriteBatch, width, height);
        DrawCentered(spriteBatch, GameInfo.Title.ToUpperInvariant(), width / 2, height / 5, Color.White, 2.4f);
    }

    private void DrawAtmosphere(SpriteBatch spriteBatch, int width, int height)
    {
        var pixel = _assets.Pixel;
        const int bandCount = 24;
        var bandHeight = (height + bandCount - 1) / bandCount;
        for (var i = 0; i < bandCount; i++)
        {
            var t = i / (float)(bandCount - 1);
            var color = Color.Lerp(SkyTop, SkyMid, t);
            spriteBatch.Draw(pixel, new Rectangle(0, i * bandHeight, width, bandHeight + 1), color);
        }

        spriteBatch.Draw(
            pixel,
            new Rectangle(0, height - 340, width, 180),
            HorizonGlow);
    }

    private void DrawHorizon(SpriteBatch spriteBatch, int width, int height)
    {
        var ground = _assets.Ground;
        var lava = _assets.Lava;
        var tile = GameConstants.TileSize;
        var baseY = height - tile * 3;
        var startX = -(int)_scrollPixels;

        for (var x = startX; x < width + tile; x += tile)
        {
            var groundSource = new Rectangle(0, 0, tile, tile);
            spriteBatch.Draw(ground, new Rectangle(x, baseY, tile, tile), groundSource, Color.White * 0.85f);
            spriteBatch.Draw(ground, new Rectangle(x, baseY + tile, tile, tile), groundSource, Color.White * 0.7f);

            var lavaSource = new Rectangle(0, 0, tile, tile);
            spriteBatch.Draw(
                lava,
                new Rectangle(x, baseY + tile * 2, tile, tile),
                lavaSource,
                Color.White * 0.9f);
        }

        var pixel = _assets.Pixel;
        spriteBatch.Draw(pixel, new Rectangle(0, 0, width, 80), new Color(0, 0, 0, 90));
        spriteBatch.Draw(pixel, new Rectangle(0, height - 90, width, 90), new Color(0, 0, 0, 120));
    }

    private void DrawDecorativeTanks(SpriteBatch spriteBatch, int width, int height)
    {
        var tanks = _assets.Tanks;
        if (tanks == _assets.Pixel)
        {
            return;
        }

        var tile = GameConstants.TileSize;
        var bob = MathF.Sin(_timeSeconds * 1.6f) * 6f;
        var left = new Rectangle(120, (int)(height * 0.42f + bob), tile * 3, tile * 3);
        var right = new Rectangle(width - 120 - tile * 3, (int)(height * 0.42f - bob), tile * 3, tile * 3);
        var sourceTeam = WorldSpriteMetrics.ScaleSource(
            new Rectangle(0, TankSpriteSelector.TeamMayorRow * tile, tile, tile));
        var sourceEnemy = WorldSpriteMetrics.ScaleSource(
            new Rectangle(0, TankSpriteSelector.EnemyRegularRow * tile, tile, tile));

        spriteBatch.Draw(tanks, left, sourceTeam, Color.White * 0.55f);
        spriteBatch.Draw(tanks, right, sourceEnemy, Color.White * 0.45f);
    }

    private void DrawVersion(SpriteBatch spriteBatch, int width)
    {
        var y = _hasFullBleedLogo ? 48 : 560;
        DrawCentered(
            spriteBatch,
            $"v{GameInfo.Version}",
            width / 2,
            y,
            MenuTheme.TextMuted,
            0.75f);
    }

    private void DrawMenuColumn(
        SpriteBatch spriteBatch,
        int width,
        int height,
        IReadOnlyList<string> items,
        int selectedIndex)
    {
        const int buttonWidth = 560;
        var buttonHeight = MenuTheme.MenuButtonHeight + 8;
        var gap = MenuTheme.MenuButtonGap + 4;
        var totalHeight = items.Count * buttonHeight + (items.Count - 1) * gap;
        var startY = height - 220 - totalHeight / 2;
        var x = (width - buttonWidth) / 2;
        var pixel = _assets.Pixel;

        var panelBounds = new Rectangle(
            x - 36,
            startY - 36,
            buttonWidth + 72,
            totalHeight + 72);
        HudOverlayHelper.DrawPanel(
            spriteBatch,
            _assets,
            panelBounds,
            MenuTheme.PanelFill);

        for (var i = 0; i < items.Count; i++)
        {
            var bounds = new Rectangle(x, startY + i * (buttonHeight + gap), buttonWidth, buttonHeight);
            var selected = i == selectedIndex;
            var fill = selected ? MenuTheme.ButtonFocusFill : MenuTheme.ButtonIdleFill;
            var border = selected ? MenuTheme.ButtonFocusBorder : MenuTheme.ButtonIdleBorder;
            var thickness = selected ? 3 : 2;

            spriteBatch.Draw(pixel, bounds, fill);
            DrawRectBorder(spriteBatch, pixel, bounds, border, thickness);

            var label = selected ? $">  {items[i]}  <" : items[i];
            var color = selected ? MenuTheme.TextAccent : MenuTheme.TextSecondary;
            var pulseScale = selected ? MenuTheme.FocusPulse(_timeSeconds) : 1f;
            DrawCentered(spriteBatch, label, bounds.Center.X, bounds.Y + 14, color, pulseScale);
        }
    }

    private static void DrawRectBorder(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color color,
        int thickness)
    {
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }

    private void DrawCentered(
        SpriteBatch spriteBatch,
        string text,
        int centerX,
        int y,
        Color color,
        float scale)
    {
        if (_font is null)
        {
            return;
        }

        var size = _font.MeasureString(text) * scale;
        spriteBatch.DrawString(
            _font,
            text,
            new Vector2(centerX - size.X / 2f, y),
            color,
            0f,
            Vector2.Zero,
            new Vector2(scale, scale),
            SpriteEffects.None,
            0f);
    }
}
