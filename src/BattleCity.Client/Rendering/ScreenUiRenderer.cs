using BattleCity.Client.Assets;
using BattleCity.Shared;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>Full-screen UI for secondary menus (login, meeting, etc.) at 1920×1080.</summary>
public sealed class ScreenUiRenderer
{
    private readonly AssetService _assets;
    private SpriteFont? _titleFont;
    private SpriteFont? _bodyFont;
    private float _timeSeconds;

    public ScreenUiRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent()
    {
        _titleFont = _assets.LoadFont("Fonts/MenuFont");
        _bodyFont = _assets.LoadFont("Fonts/UiFont");
    }

    public void Update(float deltaSeconds) => _timeSeconds += deltaSeconds;

    public void DrawBackdrop(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        var pixel = _assets.Pixel;
        spriteBatch.Draw(pixel, new Rectangle(0, 0, screenWidth, screenHeight), MenuTheme.Backdrop);
        spriteBatch.Draw(
            pixel,
            new Rectangle(0, 0, screenWidth, MenuTheme.HeaderHeight),
            MenuTheme.HeaderBar);
        spriteBatch.Draw(
            pixel,
            new Rectangle(0, screenHeight - MenuTheme.FooterHeight, screenWidth, MenuTheme.FooterHeight),
            MenuTheme.FooterBar);
        // Soft accent line under header.
        spriteBatch.Draw(
            pixel,
            new Rectangle(0, MenuTheme.HeaderHeight - 2, screenWidth, 2),
            new Color(90, 140, 220, 80));
    }

    public void DrawTitle(SpriteBatch spriteBatch, int screenWidth)
    {
        DrawCenteredText(spriteBatch, GameInfo.Title, screenWidth / 2, 16, MenuTheme.TextPrimary, 1.05f, title: true);
        DrawCenteredText(spriteBatch, $"v{GameInfo.Version}", screenWidth / 2, 52, MenuTheme.TextMuted, 1f);
    }

    public void DrawMenu(
        SpriteBatch spriteBatch,
        int screenWidth,
        int screenHeight,
        IReadOnlyList<string> items,
        int selectedIndex,
        string footer)
    {
        const int buttonWidth = 520;
        var totalHeight = items.Count * MenuTheme.MenuButtonHeight
            + Math.Max(0, items.Count - 1) * MenuTheme.MenuButtonGap;
        var startY = (screenHeight - totalHeight) / 2;
        var x = (screenWidth - buttonWidth) / 2;

        var panel = new Rectangle(x - 28, startY - 28, buttonWidth + 56, totalHeight + 56);
        DrawThemedPanel(spriteBatch, panel);

        for (var i = 0; i < items.Count; i++)
        {
            var bounds = new Rectangle(
                x,
                startY + i * (MenuTheme.MenuButtonHeight + MenuTheme.MenuButtonGap),
                buttonWidth,
                MenuTheme.MenuButtonHeight);
            DrawMenuButton(spriteBatch, bounds, items[i], selected: i == selectedIndex);
        }

        DrawCenteredText(spriteBatch, footer, screenWidth / 2, screenHeight - 36, MenuTheme.TextMuted, 1f);
    }

    public void DrawMessageBlock(
        SpriteBatch spriteBatch,
        int screenWidth,
        int screenHeight,
        string title,
        IReadOnlyList<string> lines,
        string footer)
    {
        var panel = CenteredFormPanel(screenWidth, screenHeight, 560, 280);
        DrawThemedPanel(spriteBatch, panel);
        DrawCenteredText(spriteBatch, title, panel.Center.X, panel.Y + 28, MenuTheme.TextPrimary, 1.15f, title: true);
        for (var i = 0; i < lines.Count; i++)
        {
            DrawCenteredText(
                spriteBatch,
                lines[i],
                panel.Center.X,
                panel.Y + 90 + i * 28,
                MenuTheme.TextSecondary,
                1f);
        }

        DrawCenteredText(spriteBatch, footer, panel.Center.X, panel.Bottom - 36, MenuTheme.TextMuted, 1f);
    }

    public static Rectangle CenteredFormPanel(int screenWidth, int screenHeight, int panelWidth, int panelHeight) =>
        new(
            (screenWidth - panelWidth) / 2,
            Math.Max(MenuTheme.HeaderHeight + 16, (screenHeight - panelHeight) / 2 + 12),
            panelWidth,
            panelHeight);

    public void DrawFormPanel(
        SpriteBatch spriteBatch,
        Rectangle panel,
        string title,
        IReadOnlyList<string> lines,
        string footer)
    {
        DrawThemedPanel(spriteBatch, panel);
        DrawCenteredText(spriteBatch, title, panel.Center.X, panel.Y + 22, MenuTheme.TextPrimary, 1.2f, title: true);

        var y = panel.Y + 72;
        var fieldWidth = panel.Width - 64;
        var fieldX = panel.X + 32;
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                y += 12;
                continue;
            }

            var focused = line.StartsWith("> ", StringComparison.Ordinal);
            var display = focused ? line[2..] : line.TrimStart();
            var isField = display.Contains(':', StringComparison.Ordinal);

            if (isField)
            {
                var fieldBounds = new Rectangle(fieldX, y, fieldWidth, MenuTheme.FormFieldHeight);
                DrawFieldRow(spriteBatch, fieldBounds, display, focused);
                y += MenuTheme.FormFieldHeight + MenuTheme.FormFieldGap;
            }
            else
            {
                DrawCenteredText(
                    spriteBatch,
                    display,
                    panel.Center.X,
                    y + 6,
                    focused ? MenuTheme.TextAccent : MenuTheme.TextSecondary,
                    focused ? MenuTheme.FocusPulse(_timeSeconds) : 1f);
                y += 28;
            }
        }

        DrawCenteredText(
            spriteBatch,
            footer,
            panel.Center.X,
            panel.Bottom - 36,
            MenuTheme.TextMuted,
            0.95f);
    }

    public void DrawThemedPanel(SpriteBatch spriteBatch, Rectangle panel) =>
        DrawPanel(spriteBatch, panel, MenuTheme.PanelFill, MenuTheme.PanelBorder);

    public void DrawMenuButton(SpriteBatch spriteBatch, Rectangle bounds, string label, bool selected)
    {
        var pixel = _assets.Pixel;
        var fill = selected ? MenuTheme.ButtonFocusFill : MenuTheme.ButtonIdleFill;
        var border = selected ? MenuTheme.ButtonFocusBorder : MenuTheme.ButtonIdleBorder;
        var thickness = selected ? 3 : 2;
        spriteBatch.Draw(pixel, bounds, fill);
        DrawRectBorder(spriteBatch, pixel, bounds, border, thickness);

        var text = selected ? $">  {label}  <" : label;
        var color = selected ? MenuTheme.TextAccent : MenuTheme.TextSecondary;
        var scale = selected ? MenuTheme.FocusPulse(_timeSeconds) : 1f;
        DrawCenteredText(spriteBatch, text, bounds.Center.X, bounds.Y + 14, color, scale, title: true);
    }

    public void DrawFieldRow(SpriteBatch spriteBatch, Rectangle bounds, string text, bool focused)
    {
        var pixel = _assets.Pixel;
        var fill = focused ? MenuTheme.FieldFocusFill : MenuTheme.FieldIdleFill;
        var border = focused ? MenuTheme.FieldFocusBorder : MenuTheme.FieldIdleBorder;
        spriteBatch.Draw(pixel, bounds, fill);
        DrawRectBorder(spriteBatch, pixel, bounds, border, focused ? 2 : 1);

        if (_bodyFont is null)
        {
            return;
        }

        text = SanitizeForSpriteFont(text);
        var scale = focused ? MenuTheme.FocusPulse(_timeSeconds) : 1f;
        var size = _bodyFont.MeasureString(text) * scale;
        var position = new Vector2(
            bounds.X + 12,
            bounds.Y + (bounds.Height - size.Y) / 2f);
        spriteBatch.DrawString(
            _bodyFont,
            text,
            position,
            focused ? MenuTheme.TextAccent : MenuTheme.TextSecondary,
            0f,
            Vector2.Zero,
            new Vector2(scale, scale),
            SpriteEffects.None,
            0f);
    }

    public void DrawText(
        SpriteBatch spriteBatch,
        string text,
        int x,
        int y,
        Color color,
        float scale = 1f)
    {
        if (_bodyFont is null)
        {
            return;
        }

        text = SanitizeForSpriteFont(text);
        spriteBatch.DrawString(
            _bodyFont,
            text,
            new Vector2(x, y),
            color,
            0f,
            Vector2.Zero,
            new Vector2(scale, scale),
            SpriteEffects.None,
            0f);
    }

    public void DrawCenteredText(
        SpriteBatch spriteBatch,
        string text,
        int centerX,
        int y,
        Color color,
        float scale = 1f,
        bool title = false)
    {
        var font = title ? _titleFont : _bodyFont;
        if (font is null)
        {
            return;
        }

        text = SanitizeForSpriteFont(text);
        var size = font.MeasureString(text) * scale;
        var position = new Vector2(centerX - size.X / 2f, y);
        spriteBatch.DrawString(
            font,
            text,
            position,
            color,
            0f,
            Vector2.Zero,
            new Vector2(scale, scale),
            SpriteEffects.None,
            0f);
    }

    /// <summary>
    /// Menu/Ui fonts only include ASCII printable glyphs; strip anything else so DrawString cannot throw.
    /// </summary>
    private static string SanitizeForSpriteFont(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var needsSanitize = false;
        foreach (var ch in text)
        {
            if (ch is < ' ' or > '~')
            {
                needsSanitize = true;
                break;
            }
        }

        if (!needsSanitize)
        {
            return text;
        }

        var buffer = new char[text.Length];
        var length = 0;
        foreach (var ch in text)
        {
            buffer[length++] = ch is >= ' ' and <= '~' ? ch : '?';
        }

        return new string(buffer, 0, length);
    }

    public void DrawPanel(SpriteBatch spriteBatch, Rectangle bounds, Color fill, Color border)
    {
        var pixel = _assets.Pixel;
        spriteBatch.Draw(pixel, bounds, fill);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height), border);
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
}
