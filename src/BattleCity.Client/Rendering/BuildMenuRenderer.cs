using BattleCity.Client.Assets;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>Build menu listing (legacy CDrawing::DrawBuildMenu).</summary>
public sealed class BuildMenuRenderer
{
    private static readonly Color PanelColor = new(8, 10, 24, 220);
    private static readonly Color EntryColor = new(255, 255, 0);

    private readonly AssetService _assets;
    private SpriteFont? _font;

    public BuildMenuRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont(LegacySpriteNames.UiFont);
    }

    public void Draw(
        SpriteBatch spriteBatch,
        int menuAnchorX,
        int menuAnchorY,
        int screenWidth,
        int screenHeight,
        CityBuildState build)
    {
        if (_font is null)
        {
            return;
        }

        var layout = BuildMenuLayout.Create(menuAnchorX, menuAnchorY, screenWidth, screenHeight, build);
        if (layout.Entries.Count == 0)
        {
            return;
        }

        var contentHeight = layout.BottomY - layout.TopY;
        spriteBatch.Draw(
            _assets.Pixel,
            new Rectangle(layout.MenuX, layout.TopY, BuildMenuLayout.MenuWidth, contentHeight),
            PanelColor);

        var scale = new Vector2(0.85f, 0.85f);
        foreach (var entry in layout.Entries)
        {
            spriteBatch.DrawString(
                _font,
                entry.Label,
                new Vector2(layout.MenuX + 8, entry.TopY),
                EntryColor,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f);
        }
    }
}
