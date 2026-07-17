using BattleCity.Client.Assets;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>In-game HUD: modern transparent overlay or legacy right interface rail.</summary>
public sealed class UiRenderer
{
    private static readonly Color TopBarFill = new(8, 10, 24, 150);
    private static readonly Color TextColor = new(235, 235, 245);
    private static readonly Color TextShadowColor = new(0, 0, 0, 200);
    private static readonly Color StatusPanelFill = new(8, 10, 24, 140);

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
        _font = _assets.LoadFont("Fonts/MenuFont");
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

        if (context.PlayerInventory.HasValue)
        {
            _inventoryPanel.Draw(
                spriteBatch,
                context.PlayerInventory.Value,
                context.PlayerHealth,
                context.PlayerMaxHealth);
        }

        _underAttackPanel.Draw(spriteBatch, in context);
        DrawStatusPanel(spriteBatch, in context);

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

        textLines.Add("Esc - return to menu");
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
