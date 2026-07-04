using BattleCity.Client.Assets;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;

using Microsoft.Xna.Framework;

using Microsoft.Xna.Framework.Graphics;



namespace BattleCity.Client.Rendering;



/// <summary>In-game HUD and legacy 200 px right interface rail.</summary>

public sealed class UiRenderer

{

    private const int InterfaceTopHeight = 430;

    private const int InterfaceBottomHeight = 170;

    private const int TextPanelPadding = 8;

    private static readonly Color TextColor = new(255, 255, 210);

    private static readonly Color TextShadowColor = new(0, 0, 0, 200);

    private static readonly Color TextPanelColor = new(8, 10, 24, 210);



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

        var panelX = context.ScreenWidth - RenderConstants.UiPanelWidth;

        DrawInterfacePanel(spriteBatch, panelX, context.ScreenHeight);

        _underAttackPanel.Draw(spriteBatch, panelX, in context);

        if (context.PlayerInventory.HasValue)
        {
            _inventoryPanel.Draw(
                spriteBatch,
                panelX,
                context.PlayerInventory.Value,
                context.PlayerHealth,
                context.PlayerMaxHealth);
        }

        var textX = panelX + 12;
        var y = InterfaceTopHeight + 12;

        var textLines = new List<string>

        {

            context.PlayerDisplayName ?? "Player",

            context.LoadedCityName ?? "Unknown City",

            $"Buildings: {context.BuildingCount}",

        };



        if (context.PlayerHealth.HasValue && context.PlayerMaxHealth.HasValue)

        {

            if (!context.PlayerRespawnSeconds.HasValue)

            {

                textLines.Add($"HP: {context.PlayerHealth}/{context.PlayerMaxHealth}");

            }

        }



        if (context.PlayerInventory.HasValue)
        {
            textLines.Add("D-drop  [ ]-cycle  B-bomb");
            textLines.Add("Shift-laser  stop+Shift-rocket");
        }



        textLines.Add(context.ShowMiniMap ? "Minimap: ON (M)" : "Minimap: OFF (M)");
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



        DrawTextPanel(spriteBatch, textX - TextPanelPadding, y - TextPanelPadding, textLines);



        foreach (var line in textLines)

        {

            DrawLine(spriteBatch, textX, ref y, line);

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

    }



    private void DrawTextPanel(SpriteBatch spriteBatch, int x, int y, IReadOnlyList<string> lines)

    {

        if (_font is null || lines.Count == 0)

        {

            return;

        }



        const int lineHeight = 20;

        var maxWidth = 0f;

        foreach (var line in lines)

        {

            maxWidth = Math.Max(maxWidth, _font.MeasureString(line).X * 0.95f);

        }



        var panelWidth = (int)maxWidth + TextPanelPadding * 2;

        var panelHeight = lines.Count * lineHeight + TextPanelPadding * 2;

        spriteBatch.Draw(_assets.Pixel, new Rectangle(x, y, panelWidth, panelHeight), TextPanelColor);

    }



    private void DrawInterfacePanel(SpriteBatch spriteBatch, int panelX, int screenHeight)

    {

        var pixel = _assets.Pixel;

        var interfaceTop = _assets.LoadTexture(LegacySpriteNames.Interface);

        var interfaceBottom = _assets.LoadTexture(LegacySpriteNames.InterfaceBottom);



        if (interfaceTop != pixel)

        {

            spriteBatch.Draw(

                interfaceTop,

                new Rectangle(panelX, 0, RenderConstants.UiPanelWidth, InterfaceTopHeight),

                Color.White);

        }

        else

        {

            spriteBatch.Draw(

                pixel,

                new Rectangle(panelX, 0, RenderConstants.UiPanelWidth, screenHeight),

                new Color(32, 32, 48, 255));

        }



        if (interfaceBottom != pixel)

        {

            spriteBatch.Draw(

                interfaceBottom,

                new Rectangle(panelX, InterfaceTopHeight, RenderConstants.UiPanelWidth, InterfaceBottomHeight),

                Color.White);

        }



        var separator = new Rectangle(panelX - 1, 0, 1, screenHeight);

        spriteBatch.Draw(pixel, separator, new Color(80, 80, 100));

    }



    private void DrawLine(SpriteBatch spriteBatch, int x, ref int y, string text)

    {

        if (_font is null)

        {

            y += 20;

            return;

        }



        var scale = new Vector2(0.95f, 0.95f);

        var position = new Vector2(x, y);

        spriteBatch.DrawString(_font, text, position + new Vector2(1f, 1f), TextShadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        spriteBatch.DrawString(_font, text, position, TextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        y += 20;

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


