using BattleCity.Client.Assets;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>In-game HUD: modern transparent overlay or legacy right interface rail.</summary>
public sealed class UiRenderer
{
    public static readonly string[] SettingsMenuItems =
    [
        "Resume",
        "Toggle Info Box (F1)",
        "Toggle Minimap (M)",
        "Abandon City",
        "Return to Menu",
    ];

    private static readonly Color TopBarFill = new(6, 8, 18, 175);
    private static readonly Color TopBarAccent = new(90, 140, 220, 100);
    private static readonly Color TextColor = MenuTheme.TextPrimary;
    private static readonly Color TextShadowColor = new(0, 0, 0, 200);
    private static readonly Color StatusPanelFill = new(8, 10, 22, 180);

    private readonly AssetService _assets;
    private readonly InventoryPanelRenderer _inventoryPanel;
    private readonly UnderAttackPanelRenderer _underAttackPanel;
    private readonly BuildMenuRenderer _buildMenu;
    private SpriteFont? _font;

    public UiRenderer(AssetService assets)
    {
        _assets = assets;
        _inventoryPanel = new InventoryPanelRenderer(assets);
        _underAttackPanel = new UnderAttackPanelRenderer(assets);
        _buildMenu = new BuildMenuRenderer(assets);
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont(LegacySpriteNames.UiFont);
        _inventoryPanel.LoadContent();
        _underAttackPanel.LoadContent();
        _buildMenu.LoadContent();
    }

    public void Draw(SpriteBatch spriteBatch, in RenderContext context)
    {
        DrawModern(spriteBatch, in context);
    }

    private void DrawModern(SpriteBatch spriteBatch, in RenderContext context)
    {
        var pixel = _assets.Pixel;
        HudOverlayHelper.DrawPanel(spriteBatch, _assets, ModernHudLayout.TopBar, TopBarFill);
        spriteBatch.Draw(
            pixel,
            new Rectangle(0, ModernHudLayout.TopBar.Bottom - 2, UiLayout.LogicalWidth, 2),
            TopBarAccent);
        DrawHamburger(spriteBatch, context.ShowSettingsMenu);

        if (context.PlayerInventory.HasValue)
        {
            _inventoryPanel.Draw(
                spriteBatch,
                context.PlayerInventory.Value,
                context.PlayerHealth,
                context.PlayerMaxHealth,
                context.CloakRechargeSeconds,
                context.FlareRechargeSeconds,
                context.CloakRechargeUnlocked,
                context.FlareRechargeUnlocked);
        }

        _underAttackPanel.Draw(spriteBatch, in context);

        if (context.ShowStatusPanel)
        {
            DrawStatusPanel(spriteBatch, in context);
        }

        if (context.ShowBuildMenu && context.CityBuild is not null)
        {
            _buildMenu.Draw(
                spriteBatch,
                (int)context.BuildMenuAnchor.X,
                (int)context.BuildMenuAnchor.Y,
                context.ScreenWidth,
                context.ScreenHeight,
                context.CityBuild);
        }

        if (context.ShowSettingsMenu)
        {
            DrawSettingsMenu(spriteBatch, in context);
        }
    }

    private void DrawHamburger(SpriteBatch spriteBatch, bool highlighted)
    {
        var bounds = ModernHudLayout.HamburgerBounds;
        var fill = highlighted ? MenuTheme.ButtonFocusFill : new Color(8, 10, 24, 180);
        HudOverlayHelper.DrawPanel(spriteBatch, _assets, bounds, fill);

        var pixel = _assets.Pixel;
        var lineColor = highlighted ? MenuTheme.TextAccent : TextColor;
        var lineWidth = bounds.Width - 16;
        var lineHeight = 3;
        var startX = bounds.X + 8;
        var gap = 8;
        var startY = bounds.Y + (bounds.Height - (lineHeight * 3 + gap * 2)) / 2;
        for (var i = 0; i < 3; i++)
        {
            spriteBatch.Draw(
                pixel,
                new Rectangle(startX, startY + i * (lineHeight + gap), lineWidth, lineHeight),
                lineColor);
        }
    }

    private void DrawStatusPanel(SpriteBatch spriteBatch, in RenderContext context)
    {
        if (_font is null)
        {
            return;
        }

        var textLines = BuildStatusLines(in context);
        if (textLines.Count == 0)
        {
            return;
        }

        var panel = ModernHudLayout.StatusPanel(textLines.Count);
        HudOverlayHelper.DrawPanel(spriteBatch, _assets, panel, StatusPanelFill);

        var x = panel.X + ModernHudLayout.StatusPanelPadding;
        var y = panel.Y + ModernHudLayout.StatusPanelPadding;
        foreach (var line in textLines)
        {
            DrawLine(spriteBatch, x, ref y, line);
        }
    }

    private void DrawSettingsMenu(SpriteBatch spriteBatch, in RenderContext context)
    {
        if (_font is null)
        {
            return;
        }

        var pixel = _assets.Pixel;
        spriteBatch.Draw(
            pixel,
            new Rectangle(0, 0, UiLayout.LogicalWidth, UiLayout.LogicalHeight),
            new Color(0, 0, 0, 185));

        var itemCount = SettingsMenuItems.Length;
        var panelWidth = 480;
        var panelHeight = 88 + itemCount * (MenuTheme.MenuButtonHeight + MenuTheme.MenuButtonGap);
        var panel = new Rectangle(
            (UiLayout.LogicalWidth - panelWidth) / 2,
            (UiLayout.LogicalHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);
        HudOverlayHelper.DrawPanel(spriteBatch, _assets, panel, MenuTheme.PanelFill);

        var title = "Settings";
        var titleScale = new Vector2(1.15f, 1.15f);
        var titleSize = _font.MeasureString(title) * titleScale;
        spriteBatch.DrawString(
            _font,
            title,
            new Vector2(panel.Center.X - titleSize.X / 2f, panel.Y + 22),
            MenuTheme.TextPrimary,
            0f,
            Vector2.Zero,
            titleScale,
            SpriteEffects.None,
            0f);

        var buttonWidth = panelWidth - 64;
        var buttonX = panel.X + 32;
        var startY = panel.Y + 70;
        for (var i = 0; i < itemCount; i++)
        {
            var selected = i == context.SettingsSelectedIndex;
            var bounds = new Rectangle(
                buttonX,
                startY + i * (MenuTheme.MenuButtonHeight + MenuTheme.MenuButtonGap),
                buttonWidth,
                MenuTheme.MenuButtonHeight);
            var fill = selected ? MenuTheme.ButtonFocusFill : MenuTheme.ButtonIdleFill;
            var border = selected ? MenuTheme.ButtonFocusBorder : MenuTheme.ButtonIdleBorder;
            spriteBatch.Draw(pixel, bounds, fill);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, selected ? 3 : 2), border);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - (selected ? 3 : 2), bounds.Width, selected ? 3 : 2), border);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, selected ? 3 : 2, bounds.Height), border);
            spriteBatch.Draw(pixel, new Rectangle(bounds.Right - (selected ? 3 : 2), bounds.Y, selected ? 3 : 2, bounds.Height), border);

            var label = selected ? $">  {SettingsMenuItems[i]}  <" : SettingsMenuItems[i];
            var color = selected ? MenuTheme.TextAccent : MenuTheme.TextSecondary;
            var pulse = selected ? MenuTheme.FocusPulse(context.AnimationTime) : 1f;
            var scale = new Vector2(pulse, pulse);
            var size = _font.MeasureString(label) * scale;
            spriteBatch.DrawString(
                _font,
                label,
                new Vector2(bounds.Center.X - size.X / 2f, bounds.Y + 14),
                color,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f);
        }

        var footer = "Esc closes   Enter selects";
        var footerScale = new Vector2(0.75f, 0.75f);
        var footerSize = _font.MeasureString(footer) * footerScale;
        spriteBatch.DrawString(
            _font,
            footer,
            new Vector2(panel.Center.X - footerSize.X / 2f, panel.Bottom - 32),
            MenuTheme.TextMuted,
            0f,
            Vector2.Zero,
            footerScale,
            SpriteEffects.None,
            0f);
    }

    private List<string> BuildStatusLines(in RenderContext context)
    {
        var textLines = new List<string>
        {
            context.PlayerDisplayName ?? "Player",
            context.LoadedCityName ?? "Unknown City",
            $"Buildings: {context.BuildingCount}",
        };

        if (context.PlayerHealth.HasValue
            && context.PlayerMaxHealth.HasValue
            && !context.PlayerRespawnSeconds.HasValue)
        {
            textLines.Add($"HP: {context.PlayerHealth}/{context.PlayerMaxHealth}");
        }

        if (context.PlayerInventory.HasValue)
        {
            textLines.Add("D-drop placeables  [ ]-cycle");
            textLines.Add("C-cloak  H-medkit  Shift-fire");
        }

        textLines.Add(context.ShowMiniMap ? "Minimap: ON (M)" : "Minimap: OFF (M)");
        textLines.Add("F1 - hide/show this info");
        textLines.Add("F11 / Alt+Enter - fullscreen");
        textLines.Add("Build: right-click map");

        if (context.BuildModeSlot != 0)
        {
            var label = context.BuildModeSlot == -1
                ? "Demolish: left-click building"
                : $"Building {GetBuildModeLabel(context.BuildModeSlot)}: left-click map";
            textLines.Add(label);
        }

        if (context.CityBuild?.IsOrbable == true)
        {
            textLines.Add("City is orbable");
        }

        textLines.Add("Menu - hamburger / Esc");
        return textLines;
    }

    private void DrawLine(SpriteBatch spriteBatch, int x, ref int y, string text)
    {
        if (_font is null)
        {
            y += ModernHudLayout.StatusLineHeight;
            return;
        }

        var scale = new Vector2(0.95f, 0.95f);
        var position = new Vector2(x, y);
        spriteBatch.DrawString(_font, text, position + new Vector2(1f, 1f), TextShadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, text, position, TextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        y += ModernHudLayout.StatusLineHeight;
    }

    private static string GetBuildModeLabel(int buildModeSlot)
    {
        var menuIndex = buildModeSlot - 1;
        if (menuIndex < 0 || menuIndex >= BuildingCatalog.MenuNames.Count)
        {
            return "selected";
        }

        return BuildingCatalog.MenuNames[menuIndex];
    }
}
