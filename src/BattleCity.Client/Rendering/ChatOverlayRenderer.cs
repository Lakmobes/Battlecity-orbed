using BattleCity.Client.Assets;
using BattleCity.Client.Chat;
using BattleCity.Client.Input;
using BattleCity.Shared.Chat;
using BattleCity.Shared.Constants;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

public sealed class ChatOverlayRenderer
{
    private const int LineHeight = 26;
    private const float TextScale = 1f;
    private static readonly string[] TabNames = ["All", "Team", "System", "Commands"];
    private static string KeyLabel(GameAction action) =>
        KeyBindings.FormatKey(KeyBindings.Current.Primary(action));

    private readonly AssetService _assets;
    private SpriteFont? _font;

    public ChatOverlayRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent() => _font = _assets.LoadFont(LegacySpriteNames.HudFont);

    public void Draw(
        SpriteBatch spriteBatch,
        int viewportWidth,
        int viewportHeight,
        IReadOnlyCollection<ChatLine> lines,
        bool isChatting,
        string? chatDraft,
        bool showAdminCommands = false)
    {
        if (_font is null)
        {
            return;
        }

        var panel = ModernHudLayout.ChatPanel;
        HudTheme.DrawPanel(spriteBatch, _assets.Pixel, panel, active: isChatting, idle: !isChatting);

        for (var i = 0; i < TabNames.Length; i++)
        {
            var tab = ModernHudLayout.ChatTabBounds(i);
            var selected = ModernHudLayout.ChatTab == i;
            HudTheme.DrawPanel(spriteBatch, _assets.Pixel, tab, active: selected, idle: !selected);
            var scale = 1f;
            var size = _font.MeasureString(TabNames[i]) * scale;
            HudTheme.DrawLabel(
                spriteBatch,
                _font,
                TabNames[i],
                new Vector2(tab.Center.X - size.X / 2f, tab.Center.Y - size.Y / 2f),
                selected ? HudTheme.Accent : HudTheme.TextMuted,
                scale);
        }

        var inputHeight = 28;
        var listTop = panel.Y + 40;
        if (ModernHudLayout.ChatTab == 3)
        {
            DrawCommands(spriteBatch, panel, listTop, showAdminCommands);
        }
        else
        {
            var filtered = lines.Where(line => PassesTab(line, ModernHudLayout.ChatTab)).ToList();
            var listBottom = panel.Bottom - inputHeight - 8;
            var maxLines = Math.Max(1, (listBottom - listTop) / LineHeight);
            var y = listTop;
            foreach (var line in filtered.TakeLast(maxLines))
            {
                HudTheme.DrawLabel(spriteBatch, _font, line.Text, new Vector2(panel.X + 12, y), line.Color, TextScale);
                y += LineHeight;
            }
        }

        var input = new Rectangle(panel.X + 8, panel.Bottom - inputHeight - 6, panel.Width - 16, inputHeight);
        spriteBatch.Draw(_assets.Pixel, input, isChatting ? new Color(6, 12, 20, 230) : new Color(6, 12, 20, 140));
        spriteBatch.Draw(_assets.Pixel, new Rectangle(input.X, input.Y, input.Width, 1), isChatting ? HudTheme.Accent : HudTheme.AccentDeep);
        var prompt = isChatting
            ? (chatDraft ?? string.Empty) + "_"
            : "[Enter] to chat...";
        var promptColor = isChatting ? HudTheme.Text : HudTheme.TextMuted;
        HudTheme.DrawLabel(spriteBatch, _font, prompt, new Vector2(input.X + 8, input.Y + 2), promptColor, 1f);
    }

    private void DrawCommands(SpriteBatch spriteBatch, Rectangle panel, int top, bool showAdminCommands)
    {
        if (_font is null)
        {
            return;
        }

        if (showAdminCommands)
        {
            var y = top;
            foreach (var line in AdminCommandHelp.Lines)
            {
                HudTheme.DrawLabel(spriteBatch, _font, line, new Vector2(panel.X + 12, y), HudTheme.Text, 1f);
                y += 22;
            }

            HudTheme.DrawLabel(
                spriteBatch,
                _font,
                $"Keys: {KeyLabel(GameAction.DropItem)} drop  {KeyLabel(GameAction.UseCloak)} cloak  {KeyLabel(GameAction.UseMedKit)} medkit  {KeyLabel(GameAction.DropBomb)} bomb  {KeyLabel(GameAction.DropOrb)} orb  {KeyLabel(GameAction.PickUp)} pickup",
                new Vector2(panel.X + 12, y + 4),
                HudTheme.TextMuted,
                1f);
            return;
        }

        var commands = new[]
        {
            $"[{KeyLabel(GameAction.DropItem)}] DROP",
            $"[{KeyLabel(GameAction.UseCloak)}] CLOAK",
            $"[{KeyLabel(GameAction.UseMedKit)}] MEDKIT",
            $"[{KeyLabel(GameAction.DropBomb)}] BOMB",
            $"[{KeyLabel(GameAction.DropOrb)}] ORB",
            $"[{KeyLabel(GameAction.PickUp)}] PICK UP",
            $"[{KeyLabel(GameAction.InventoryPrevious)}] CYCLE",
            $"[{KeyLabel(GameAction.ToggleMiniMap)}] MAP",
            $"[{KeyLabel(GameAction.Fire)}] FIRE",
        };
        for (var i = 0; i < commands.Length; i++)
        {
            var cell = new Rectangle(panel.X + 12 + ((i % 3) * 204), top + ((i / 3) * 44), 192, 36);
            var actionArt = _assets.LoadTexture(HudSpriteNames.ActionButton);
            if (actionArt != _assets.Pixel)
            {
                spriteBatch.Draw(actionArt, cell, Color.White);
            }
            else
            {
                HudTheme.DrawPanel(spriteBatch, _assets.Pixel, cell, active: false);
            }

            HudTheme.DrawLabel(spriteBatch, _font, commands[i], new Vector2(cell.X + 10, cell.Y + 6), HudTheme.Accent, 1f);
        }
    }

    private static bool PassesTab(ChatLine line, int tab)
    {
        if (tab == 0)
        {
            return true;
        }

        var system = ChatColorResolver.System;
        var isSystem = ColorsClose(line.Color, system);
        if (tab == 2)
        {
            return isSystem;
        }

        return line.Color.G > line.Color.R + 20 && line.Color.G > line.Color.B + 20;
    }

    private static bool ColorsClose(Color a, Color b) =>
        Math.Abs(a.R - b.R) < 8 && Math.Abs(a.G - b.G) < 8 && Math.Abs(a.B - b.B) < 8;
}
