using BattleCity.Client.Assets;
using BattleCity.Client.Chat;
using BattleCity.Shared.Constants;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

public sealed class ChatOverlayRenderer
{
    private const int LineHeight = 18;
    private const int InputLineHeight = 20;
    private const float TextScale = 1f;
    private static readonly Color PanelBackground = new(0, 0, 0, 140);
    private static readonly Color InputBackground = new(0, 0, 0, 180);

    private readonly AssetService _assets;
    private SpriteFont? _font;

    public ChatOverlayRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent() => _font = _assets.LoadFont(LegacySpriteNames.UiFont);

    public void Draw(
        SpriteBatch spriteBatch,
        int viewportWidth,
        int viewportHeight,
        IReadOnlyCollection<ChatLine> lines,
        bool isChatting,
        string? chatDraft)
    {
        if (_font is null)
        {
            return;
        }

        var showPanel = isChatting || lines.Count > 0;
        if (!showPanel)
        {
            return;
        }

        var chatHeight = ModernHudLayout.ChatAreaHeight;
        var startY = viewportHeight - chatHeight;
        var chatWidth = Math.Min(560, viewportWidth);
        var pixel = _assets.Pixel;

        HudOverlayHelper.DrawPanel(
            spriteBatch,
            _assets,
            new Rectangle(0, startY, chatWidth, chatHeight),
            PanelBackground,
            borderThickness: 0);

        var y = startY + 6;
        var maxLines = Math.Max(1, (chatHeight - InputLineHeight - 12) / LineHeight);
        foreach (var line in lines.TakeLast(maxLines))
        {
            spriteBatch.DrawString(
                _font,
                line.Text,
                new Vector2(8, y),
                line.Color,
                0f,
                Vector2.Zero,
                TextScale,
                SpriteEffects.None,
                0f);
            y += LineHeight;
        }

        if (isChatting)
        {
            var inputY = viewportHeight - InputLineHeight - 4;
            spriteBatch.Draw(
                pixel,
                new Rectangle(0, inputY, chatWidth, InputLineHeight + 4),
                InputBackground);
            spriteBatch.DrawString(
                _font,
                (chatDraft ?? string.Empty) + "_",
                new Vector2(8, inputY + 2),
                new Color(UiColors.Yellow.R, UiColors.Yellow.G, UiColors.Yellow.B),
                0f,
                Vector2.Zero,
                TextScale,
                SpriteEffects.None,
                0f);
        }
    }
}

