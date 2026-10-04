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
    private readonly RadarRenderer _radar;
    private readonly BuildMenuRenderer _buildMenu;
    private SpriteFont? _font;

    public UiRenderer(AssetService assets)
    {
        _assets = assets;
        _inventoryPanel = new InventoryPanelRenderer(assets);
        _underAttackPanel = new UnderAttackPanelRenderer(assets);
        _radar = new RadarRenderer(assets);
        _buildMenu = new BuildMenuRenderer(assets);
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont(LegacySpriteNames.HudFont);
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
        DrawMenuButton(spriteBatch, context.ShowSettingsMenu);
        DrawMapButton(spriteBatch, context.ShowMiniMap);

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

        _radar.DrawBackdrop(spriteBatch);
        _underAttackPanel.Draw(spriteBatch, in context);
        _radar.Draw(spriteBatch, in context);
        DrawInfoCard(spriteBatch, in context);

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

        if (context.ShowHirePanel)
        {
            DrawHirePanel(spriteBatch, in context);
        }

        if (context.ShowVirtualCursor)
        {
            DrawVirtualCursor(spriteBatch, context.VirtualCursorLogical);
        }
    }

    private void DrawVirtualCursor(SpriteBatch spriteBatch, Vector2 logicalPosition)
    {
        var pixel = _assets.Pixel;
        var x = (int)logicalPosition.X;
        var y = (int)logicalPosition.Y;
        const int arm = 7;
        const int thickness = 2;
        var fill = MenuTheme.TextAccent;
        var outline = new Color(0, 0, 0, 200);

        spriteBatch.Draw(pixel, new Rectangle(x - arm - 1, y - thickness / 2 - 1, arm * 2 + 3, thickness + 2), outline);
        spriteBatch.Draw(pixel, new Rectangle(x - thickness / 2 - 1, y - arm - 1, thickness + 2, arm * 2 + 3), outline);
        spriteBatch.Draw(pixel, new Rectangle(x - arm, y - thickness / 2, arm * 2 + 1, thickness), fill);
        spriteBatch.Draw(pixel, new Rectangle(x - thickness / 2, y - arm, thickness, arm * 2 + 1), fill);
    }

    private void DrawHirePanel(SpriteBatch spriteBatch, in RenderContext context)
    {
        if (_font is null)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(context.HireApplicantName)
            ? "Applicant"
            : context.HireApplicantName!;
        var panelWidth = 420;
        var panelHeight = 168;
        var panel = new Rectangle(
            (UiLayout.LogicalWidth - panelWidth) / 2,
            (UiLayout.LogicalHeight - panelHeight) / 2 - 40,
            panelWidth,
            panelHeight);
        HudOverlayHelper.DrawPanel(spriteBatch, _assets, panel, MenuTheme.PanelFill);

        var title = "Personnel";
        var titleSize = _font.MeasureString(title);
        spriteBatch.DrawString(
            _font,
            title,
            new Vector2(panel.Center.X - titleSize.X / 2f, panel.Y + 16),
            MenuTheme.TextAccent);

        spriteBatch.DrawString(
            _font,
            $"{name} wants to join your city.",
            new Vector2(panel.X + 24, panel.Y + 52),
            MenuTheme.TextPrimary);

        spriteBatch.DrawString(
            _font,
            "Enter = Welcome    Esc = Reject",
            new Vector2(panel.X + 24, panel.Y + 84),
            MenuTheme.TextMuted);

        var denyLabel = context.DenyApplicants
            ? "[N] Not hiring: ON (auto-reject)"
            : "[N] Not hiring: OFF";
        spriteBatch.DrawString(
            _font,
            denyLabel,
            new Vector2(panel.X + 24, panel.Y + 116),
            context.DenyApplicants ? new Color(255, 180, 90) : MenuTheme.TextMuted);
    }

    private void DrawMenuButton(SpriteBatch spriteBatch, bool highlighted)
    {
        var bounds = ModernHudLayout.HamburgerBounds;
        var menuArt = _assets.LoadTexture(HudSpriteNames.MenuButton);
        if (menuArt != _assets.Pixel)
        {
            spriteBatch.Draw(menuArt, bounds, Color.White);
        }
        else
        {
            HudTheme.DrawPanel(spriteBatch, _assets.Pixel, bounds, active: highlighted);
        }

        var pixel = _assets.Pixel;
        var lineColor = highlighted ? HudTheme.Accent : HudTheme.Text;
        var startX = bounds.X + 12;
        var startY = bounds.Y + 14;
        for (var i = 0; i < 3; i++)
        {
            spriteBatch.Draw(pixel, new Rectangle(startX, startY + i * 7, 16, 2), lineColor);
        }

        if (_font is not null)
        {
            HudTheme.DrawLabel(
                spriteBatch,
                _font,
                "MENU (ESC)",
                new Vector2(bounds.X + 38, bounds.Y + 12),
                highlighted ? HudTheme.Accent : HudTheme.Text,
                scale: 1f);
        }
    }

    private void DrawMapButton(SpriteBatch spriteBatch, bool mapOpen)
    {
        var bounds = ModernHudLayout.MapButtonBounds;
        var art = _assets.LoadTexture(HudSpriteNames.MenuButton);
        if (art != _assets.Pixel)
        {
            spriteBatch.Draw(art, bounds, Color.White);
        }
        else
        {
            HudTheme.DrawPanel(spriteBatch, _assets.Pixel, bounds, active: mapOpen);
        }

        if (_font is null)
        {
            return;
        }

        var label = mapOpen ? "MAP ON (M)" : "MAP (M)";
        var scale = 1f;
        var size = _font.MeasureString(label) * scale;
        HudTheme.DrawLabel(
            spriteBatch,
            _font,
            label,
            new Vector2(bounds.Center.X - size.X / 2f, bounds.Center.Y - size.Y / 2f),
            mapOpen ? HudTheme.Accent : HudTheme.Text,
            scale);
    }

    private void DrawInfoCard(SpriteBatch spriteBatch, in RenderContext context)
    {
        if (_font is null)
        {
            return;
        }

        var panel = ModernHudLayout.InfoCard;
        HudTheme.DrawPanel(spriteBatch, _assets.Pixel, panel, active: false);
        var y = panel.Y + 8;
        HudTheme.DrawLabel(spriteBatch, _font, "Location", new Vector2(panel.X + 10, y), HudTheme.TextMuted, 1f);
        HudTheme.DrawLabel(spriteBatch, _font, context.LoadedCityName ?? "-", new Vector2(panel.X + 140, y), HudTheme.Text, 1f);
        y += 28;
        HudTheme.DrawLabel(spriteBatch, _font, "Bldgs", new Vector2(panel.X + 10, y), HudTheme.TextMuted, 1f);
        HudTheme.DrawLabel(spriteBatch, _font, context.BuildingCount.ToString(), new Vector2(panel.X + 140, y), HudTheme.Text, 1f);
        y += 28;
        foreach (var line in context.TeamRoster)
        {
            if (y > panel.Bottom - 18)
            {
                break;
            }

            HudTheme.DrawLabel(
                spriteBatch,
                _font,
                line.Text,
                new Vector2(panel.X + (line.Heading ? 10 : 22), y),
                line.Dead ? new Color(255, 90, 80) : line.Heading ? HudTheme.Accent : HudTheme.Text,
                1f);
            y += 24;
        }
    }

    public static bool TryHitSettingsItem(int x, int y, out int index)
    {
        var point = new Point(x, y);
        for (var i = 0; i < SettingsMenuItems.Length; i++)
        {
            if (GetSettingsButtonBounds(i).Contains(point))
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }

    public static Rectangle GetSettingsButtonBounds(int index)
    {
        const int panelWidth = 480;
        var itemCount = SettingsMenuItems.Length;
        var panelHeight = 88 + itemCount * (MenuTheme.MenuButtonHeight + MenuTheme.MenuButtonGap);
        var panel = new Rectangle(
            (UiLayout.LogicalWidth - panelWidth) / 2,
            (UiLayout.LogicalHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);
        var buttonWidth = panelWidth - 64;
        return new Rectangle(
            panel.X + 32,
            panel.Y + 70 + index * (MenuTheme.MenuButtonHeight + MenuTheme.MenuButtonGap),
            buttonWidth,
            MenuTheme.MenuButtonHeight);
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

        for (var i = 0; i < itemCount; i++)
        {
            var selected = i == context.SettingsSelectedIndex;
            var bounds = GetSettingsButtonBounds(i);
            var fill = selected ? MenuTheme.ButtonFocusFill : MenuTheme.ButtonIdleFill;
            var border = selected ? MenuTheme.ButtonFocusBorder : MenuTheme.ButtonIdleBorder;
            spriteBatch.Draw(pixel, bounds, fill);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), border);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), border);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), border);
            spriteBatch.Draw(pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), border);

            if (selected)
            {
                spriteBatch.Draw(
                    pixel,
                    new Rectangle(bounds.X, bounds.Y, MenuTheme.SelectionBarWidth, bounds.Height),
                    MenuTheme.SelectionAccent);
            }

            var color = selected ? MenuTheme.TextAccent : MenuTheme.TextSecondary;
            var size = _font.MeasureString(SettingsMenuItems[i]);
            spriteBatch.DrawString(
                _font,
                SettingsMenuItems[i],
                new Vector2(bounds.Center.X - size.X / 2f, bounds.Center.Y - size.Y / 2f),
                color,
                0f,
                Vector2.Zero,
                Vector2.One,
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

}
