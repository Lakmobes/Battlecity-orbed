using BattleCity.Client.Input;
using BattleCity.Client.Network;
using BattleCity.Client.Rendering;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Data;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Scenes;

public sealed class RankingsScene : IScene
{
    private static readonly string[] BoardTitles = ["Overall", "This month", "Season", "Per death"];

    private readonly SceneContext _context;
    private readonly GameClient _client;
    private readonly ScreenUiRenderer _ui;
    private readonly MenuInputReader _menuInput = new();
    private readonly List<(string Name, int Points)> _rows = [];
    private KeyboardState _previousKeyboard;
    private ButtonState _previousMouseButton;
    private Point _previousLogicalMouse;
    private byte _board;
    private string _seasonName = "Season";
    private bool _waiting;

    public RankingsScene(SceneContext context, GameClient client)
    {
        _context = context;
        _client = client;
        _ui = new ScreenUiRenderer(context.Assets);
    }

    public bool DrawsWorld => false;

    public Matrix WorldViewMatrix => Matrix.Identity;

    public void LoadContent()
    {
        _ui.LoadContent();
        RequestBoard(0);
    }

    public SceneTransition Update(GameTime gameTime, int screenWidth, int screenHeight)
    {
        _ui.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        _client.Poll();
        var transition = ApplyNetworkEvents();
        if (transition != SceneTransition.None)
        {
            return transition;
        }

        var menuInput = _menuInput.Poll();
        if (menuInput.CancelPressed)
        {
            _context.Audio.Play(SoundId.Click);
            _context.NetworkClient = _client;
            return SceneTransition.Meeting;
        }

        var keyboard = Keyboard.GetState();
        if (WasPressed(keyboard, Keys.D1) || WasPressed(keyboard, Keys.NumPad1))
        {
            RequestBoard(0);
        }
        else if (WasPressed(keyboard, Keys.D2) || WasPressed(keyboard, Keys.NumPad2))
        {
            RequestBoard(1);
        }
        else if (WasPressed(keyboard, Keys.D3) || WasPressed(keyboard, Keys.NumPad3))
        {
            RequestBoard(2);
        }
        else if (WasPressed(keyboard, Keys.D4) || WasPressed(keyboard, Keys.NumPad4))
        {
            RequestBoard(3);
        }
        else if (WasPressed(keyboard, Keys.Left) || WasPressed(keyboard, Keys.Right))
        {
            var next = WasPressed(keyboard, Keys.Right)
                ? (_board + 1) % BoardTitles.Length
                : (_board + BoardTitles.Length - 1) % BoardTitles.Length;
            RequestBoard((byte)next);
        }

        var mouse = Mouse.GetState();
        var logicalMouse = _context.Presentation.ScreenToLogical(new Vector2(mouse.X, mouse.Y));
        var logicalPoint = new Point((int)logicalMouse.X, (int)logicalMouse.Y);
        var clicked = mouse.LeftButton == ButtonState.Pressed && _previousMouseButton == ButtonState.Released;
        if (clicked)
        {
            for (var i = 0; i < BoardTitles.Length; i++)
            {
                if (GetTabBounds(i).Contains(logicalPoint))
                {
                    RequestBoard((byte)i);
                    break;
                }
            }
        }

        _previousKeyboard = keyboard;
        _previousMouseButton = mouse.LeftButton;
        _previousLogicalMouse = logicalPoint;
        return SceneTransition.None;
    }

    public void DrawWorld(SpriteBatch spriteBatch)
    {
    }

    public void DrawScreen(SpriteBatch spriteBatch)
    {
        var width = UiLayout.LogicalWidth;
        var height = UiLayout.LogicalHeight;
        _ui.DrawBackdrop(spriteBatch, width, height);
        _ui.DrawTitle(spriteBatch, width);
        _ui.DrawCenteredText(spriteBatch, "Rankings", width / 2, 78, MenuTheme.TextPrimary);

        for (var i = 0; i < BoardTitles.Length; i++)
        {
            var tab = GetTabBounds(i);
            var hover = tab.Contains(_previousLogicalMouse);
            _ui.DrawMenuButton(spriteBatch, tab, BoardTitles[i], selected: _board == i || hover, titleFont: false);
        }

        var boardNote = _board == 3
            ? "Score is points × 10000 / deaths. Accounts with 100 deaths or fewer are left off."
            : $"Current season: {_seasonName}";
        _ui.DrawCenteredText(
            spriteBatch,
            boardNote,
            width / 2,
            172,
            MenuTheme.TextMuted);

        var panel = new Rectangle(360, 220, width - 720, height - 340);
        _ui.DrawThemedPanel(spriteBatch, panel);
        if (_waiting && _rows.Count == 0)
        {
            _ui.DrawText(spriteBatch, "Loading...", panel.X + 28, panel.Y + 28, MenuTheme.TextMuted);
        }
        else if (_rows.Count == 0)
        {
            _ui.DrawText(spriteBatch, "No ranked players yet.", panel.X + 28, panel.Y + 28, MenuTheme.TextMuted);
        }
        else
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                var (name, points) = _rows[i];
                var y = panel.Y + 24 + (i * 36);
                var color = i == 0 ? MenuTheme.TextAccent : MenuTheme.TextPrimary;
                _ui.DrawText(spriteBatch, $"{i + 1,2}.  {name}", panel.X + 28, y, color);
                _ui.DrawText(spriteBatch, points.ToString("N0"), panel.Right - 180, y, color);
            }
        }

        _ui.DrawCenteredText(
            spriteBatch,
            "1 overall   2 month   3 season   4 per death   Left/Right switch   Esc back",
            width / 2,
            height - 28,
            MenuTheme.TextMuted);
    }

    public void Dispose()
    {
    }

    private void RequestBoard(byte board)
    {
        _board = board;
        _waiting = true;
        _rows.Clear();
        _client.RequestRankBoard(board);
    }

    private SceneTransition ApplyNetworkEvents()
    {
        foreach (var networkEvent in _client.DrainEvents())
        {
            if (networkEvent.Kind == GameClientEventKind.Disconnected
                || networkEvent.Kind == GameClientEventKind.Kicked)
            {
                _client.Dispose();
                _context.NetworkClient = null;
                return SceneTransition.MainMenu;
            }

            if (networkEvent.Kind == GameClientEventKind.StateGame && _client.SpawnState is { } spawn)
            {
                if (CityCatalog.IsValidCityId(spawn.City))
                {
                    _context.SelectedCity = CityCatalog.GetName(spawn.City);
                }

                _context.NetworkClient = _client;
                return SceneTransition.InGameOnline;
            }

            if (networkEvent.Kind == GameClientEventKind.Interview)
            {
                _context.NetworkClient = _client;
                return SceneTransition.Interview;
            }

            if (networkEvent.Kind != GameClientEventKind.RankBoard || networkEvent.RankBoardKind != _board)
            {
                continue;
            }

            _waiting = false;
            _seasonName = string.IsNullOrWhiteSpace(networkEvent.RankSeasonName)
                ? "Season"
                : networkEvent.RankSeasonName;
            _rows.Clear();
            foreach (var row in networkEvent.RankRows)
            {
                if (!string.IsNullOrWhiteSpace(row.Name))
                {
                    _rows.Add(row);
                }
            }
        }

        return SceneTransition.None;
    }

    private static Rectangle GetTabBounds(int index)
    {
        const int tabWidth = 280;
        const int gap = 16;
        var total = (BoardTitles.Length * tabWidth) + ((BoardTitles.Length - 1) * gap);
        var x = (UiLayout.LogicalWidth - total) / 2;
        return new Rectangle(x + (index * (tabWidth + gap)), 118, tabWidth, MenuTheme.MenuButtonHeight - 12);
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
}
