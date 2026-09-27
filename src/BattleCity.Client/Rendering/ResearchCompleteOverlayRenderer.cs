using BattleCity.Client.Assets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>Bottom-right city toast (research complete, etc.).</summary>
public sealed class ResearchCompleteOverlayRenderer
{
    private readonly AssetService _assets;
    private SpriteFont? _font;

    public ResearchCompleteOverlayRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent()
    {
        _font = _assets.LoadFont(LegacySpriteNames.UiFont);
    }

    public void Draw(SpriteBatch spriteBatch, string message)
    {
        if (_font is null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var lines = new List<string> { "Research complete" };
        foreach (var line in message.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0
                && !trimmed.Equals("Research complete", StringComparison.OrdinalIgnoreCase)
                && !trimmed.Equals("RESEARCH COMPLETE!", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add(trimmed);
            }
        }

        const int padding = 14;
        const int lineHeight = 20;
        var maxWidth = 0f;
        foreach (var line in lines)
        {
            maxWidth = Math.Max(maxWidth, _font.MeasureString(line).X);
        }

        var panelWidth = (int)maxWidth + padding * 2;
        var panelHeight = lines.Count * lineHeight + padding * 2;
        var panel = new Rectangle(
            UiLayout.LogicalWidth - panelWidth - 20,
            UiLayout.LogicalHeight - ModernHudLayout.ChatAreaHeight - panelHeight - 24,
            panelWidth,
            panelHeight);

        HudOverlayHelper.DrawPanel(spriteBatch, _assets, panel, new Color(14, 18, 28, 220));

        var y = panel.Y + padding;
        for (var i = 0; i < lines.Count; i++)
        {
            var color = i == 0 ? Color.Gold : Color.White;
            var position = new Vector2(panel.X + padding, y);
            spriteBatch.DrawString(_font, lines[i], position + new Vector2(1f, 1f), new Color(0, 0, 0, 200));
            spriteBatch.DrawString(_font, lines[i], position, color);
            y += lineHeight;
        }
    }
}
