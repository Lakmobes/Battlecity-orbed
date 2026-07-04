using BattleCity.Client.Assets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>Interface-panel compass and under-attack flash (legacy CDrawing::DrawArrow).</summary>
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

    public void Draw(
        SpriteBatch spriteBatch,
        int panelX,
        in RenderContext context)
    {
        if (_font is null)
        {
            return;
        }

        const int arrowY = 160;
        var arrowX = panelX + 5;
        var glyph = CompassArrowHelper.GetDirectionGlyph(
            ToNumerics(context.FocusWorldPosition),
            ToNumerics(context.CityCenterWorldPosition));
        var arrowColor = context is { IsUnderAttack: true, UnderAttackFlashVisible: true }
            ? Color.Red
            : new Color(180, 180, 200);

        spriteBatch.DrawString(_font, glyph, new Vector2(arrowX + 8, arrowY + 10), arrowColor, 0f, Vector2.Zero, new Vector2(1.2f, 1.2f), SpriteEffects.None, 0f);

        if (context is { IsUnderAttack: true, UnderAttackFlashVisible: true })
        {
            var label = "UNDER ATTACK";
            var scale = new Vector2(0.85f, 0.85f);
            var size = _font.MeasureString(label) * scale;
            var labelX = panelX + (RenderConstants.UiPanelWidth - size.X) / 2f;
            spriteBatch.DrawString(_font, label, new Vector2(labelX, arrowY + 44), Color.Red, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    private static System.Numerics.Vector2 ToNumerics(Vector2 value) => new(value.X, value.Y);
}
