using BattleCity.Client.Assets;
using BattleCity.Client.Chat;
using BattleCity.Client.Input;
using BattleCity.Client.Network;
using BattleCity.Client.Rendering;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;
using BattleCity.Shared.Network.Packets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Scenes;

public sealed class MeetingScene : IScene
{
    private readonly SceneContext _context;
    private readonly GameClient _client;
    private readonly ScreenUiRenderer _ui;
    private readonly MenuInputReader _menuInput = new();
    private readonly InGameChatInput _chatInput = new();
    private readonly InGameChatLog _chatLog = new();
    private readonly List<MeetingCityEntry> _cities = [];
    private int _selectedCityIndex;
    private int _hoverCityIndex = -1;
    private KeyboardState _previousKeyboard;
    private Point _previousLogicalMouse;
    private ButtonState _previousMouseButton;

    public MeetingScene(SceneContext context, GameClient client)
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
        _client.EnterMeetingRoom();
        _chatLog.Append(
            "Welcome to the meeting room. Click a city to apply. Press Enter to chat.",
            ChatColorResolver.System);
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
        if (menuInput.CancelPressed && !_chatInput.IsActive)
        {
            _context.Audio.Play(SoundId.Click);
            _client.Dispose();
            _context.NetworkClient = null;
            return SceneTransition.MainMenu;
        }

        var visibleCount = VisibleCityCount;
        if (!_chatInput.IsActive && _cities.Count > 0)
        {
            if (menuInput.MoveUpPressed)
            {
                _selectedCityIndex = (_selectedCityIndex - 1 + _cities.Count) % _cities.Count;
            }

            if (menuInput.MoveDownPressed)
            {
                _selectedCityIndex = (_selectedCityIndex + 1) % _cities.Count;
            }
        }

        var keyboard = Keyboard.GetState();
        if (WasPressed(keyboard, Keys.R) && !_chatInput.IsActive)
        {
            RefreshCityList();
        }

        var chatUpdate = _chatInput.Update(keyboard);
        if (chatUpdate.Submitted && !string.IsNullOrWhiteSpace(chatUpdate.Message))
        {
            _client.SendMeetingChat(chatUpdate.Message);
            InGameChatService.AppendLocalOutgoing(
                _chatLog,
                _context.PlayerName,
                chatUpdate.Message,
                isDead: false);
        }

        var mouse = Mouse.GetState();
        var logicalMouse = _context.Presentation.ScreenToLogical(new Vector2(mouse.X, mouse.Y));
        var logicalPoint = new Point((int)logicalMouse.X, (int)logicalMouse.Y);

        _hoverCityIndex = !_chatInput.IsActive
            && TryGetCityIndexAtLogical(logicalPoint.X, logicalPoint.Y, out var hoverIndex)
            ? hoverIndex
            : -1;

        var clicked = mouse.LeftButton == ButtonState.Pressed
            && _previousMouseButton == ButtonState.Released;

        if (clicked && !_chatInput.IsActive)
        {
            if (MeetingRoomLayout.RefreshButton.Contains(logicalPoint))
            {
                RefreshCityList();
            }
            else if (TryGetCityIndexAtLogical(logicalPoint.X, logicalPoint.Y, out var cityIndex))
            {
                ApplyToCity(cityIndex);
            }
        }

        _previousKeyboard = keyboard;
        _previousLogicalMouse = logicalPoint;
        _previousMouseButton = mouse.LeftButton;
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

        DrawCitiesPanel(spriteBatch);
        DrawChatPanel(spriteBatch);
        DrawFooter(spriteBatch, width);
    }

    public void Dispose()
    {
    }

    private int VisibleCityCount => Math.Min(_cities.Count, MeetingRoomLayout.MaxVisibleCityRows);

    private int GetCityScrollOffset()
    {
        var visible = VisibleCityCount;
        if (visible <= 0 || _cities.Count <= visible)
        {
            return 0;
        }

        var scroll = Math.Clamp(_selectedCityIndex - visible + 1, 0, _cities.Count - visible);
        if (_selectedCityIndex < scroll)
        {
            scroll = _selectedCityIndex;
        }

        return scroll;
    }

    private bool TryGetCityIndexAtLogical(int x, int y, out int cityIndex)
    {
        cityIndex = -1;
        if (!MeetingRoomLayout.TryGetCityIndexAt(x, y, _cities.Count, out var rowIndex))
        {
            return false;
        }

        cityIndex = rowIndex + GetCityScrollOffset();
        return cityIndex >= 0 && cityIndex < _cities.Count;
    }

    private void DrawCitiesPanel(SpriteBatch spriteBatch)
    {
        var panel = MeetingRoomLayout.CitiesPanel;
        _ui.DrawPanel(spriteBatch, panel, MenuTheme.PanelFill, MenuTheme.PanelBorder);
        _ui.DrawText(spriteBatch, "Open Cities", panel.X + MeetingRoomLayout.PanelPadding, panel.Y + 6, MenuTheme.TextPrimary);

        if (_cities.Count == 0)
        {
            _ui.DrawText(
                spriteBatch,
                "Waiting for city list...",
                panel.X + MeetingRoomLayout.PanelPadding,
                MeetingRoomLayout.CityListTop + 4,
                MenuTheme.TextMuted);
            _ui.DrawText(
                spriteBatch,
                "Click Refresh or press R",
                panel.X + MeetingRoomLayout.PanelPadding,
                MeetingRoomLayout.CityListTop + 22,
                MenuTheme.TextMuted);
            return;
        }

        var visible = VisibleCityCount;
        var scroll = GetCityScrollOffset();

        for (var row = 0; row < visible; row++)
        {
            var i = row + scroll;
            if (i >= _cities.Count)
            {
                break;
            }

            var bounds = MeetingRoomLayout.GetCityRowBounds(row);
            if (i == _selectedCityIndex)
            {
                _ui.DrawPanel(spriteBatch, bounds, MenuTheme.RowSelected, MenuTheme.ButtonFocusBorder);
            }
            else if (i == _hoverCityIndex)
            {
                _ui.DrawPanel(spriteBatch, bounds, MenuTheme.RowHover, MenuTheme.RowHover);
            }

            var city = _cities[i];
            var nameColor = i == _selectedCityIndex ? MenuTheme.TextAccent : MenuTheme.TextPrimary;
            var label = $"{city.CityName} - {city.RoleLabel}";
            _ui.DrawText(spriteBatch, label, bounds.X + 6, bounds.Y + 6, nameColor);
        }

        if (_cities.Count > visible)
        {
            _ui.DrawText(
                spriteBatch,
                $"+ {_cities.Count - visible} more (use Up/Down)",
                panel.X + MeetingRoomLayout.PanelPadding,
                panel.Bottom - 22,
                MenuTheme.TextMuted);
        }
        else
        {
            _ui.DrawText(
                spriteBatch,
                "Click a city to apply",
                panel.X + MeetingRoomLayout.PanelPadding,
                panel.Bottom - 22,
                MenuTheme.TextMuted);
        }
    }

    private void DrawChatPanel(SpriteBatch spriteBatch)
    {
        var panel = MeetingRoomLayout.ChatPanel;
        _ui.DrawPanel(spriteBatch, panel, MenuTheme.PanelFill, MenuTheme.PanelBorder);
        _ui.DrawText(spriteBatch, "Lobby Chat", panel.X + MeetingRoomLayout.PanelPadding, panel.Y + 6, MenuTheme.TextPrimary);

        var chatY = MeetingRoomLayout.CityListTop;
        var maxY = panel.Bottom - 36;
        foreach (var line in _chatLog.Lines)
        {
            if (chatY + 16 > maxY)
            {
                break;
            }

            _ui.DrawText(spriteBatch, line.Text, panel.X + MeetingRoomLayout.PanelPadding, chatY, line.Color);
            chatY += 16;
        }

        if (_chatInput.IsActive)
        {
            _ui.DrawText(
                spriteBatch,
                $"> {_chatInput.Draft}_",
                panel.X + MeetingRoomLayout.PanelPadding,
                panel.Bottom - 24,
                MenuTheme.TextAccent);
        }
        else
        {
            _ui.DrawText(
                spriteBatch,
                "Press Enter to chat",
                panel.X + MeetingRoomLayout.PanelPadding,
                panel.Bottom - 24,
                MenuTheme.TextMuted);
        }
    }

    private void DrawFooter(SpriteBatch spriteBatch, int screenWidth)
    {
        var refresh = MeetingRoomLayout.RefreshButton;
        var refreshHover = refresh.Contains(_previousLogicalMouse);
        _ui.DrawPanel(
            spriteBatch,
            refresh,
            refreshHover ? MenuTheme.RowHover : MenuTheme.ButtonIdleFill,
            MenuTheme.PanelBorder);
        _ui.DrawText(spriteBatch, "Refresh (R)", refresh.X + 10, refresh.Y + 6, MenuTheme.TextPrimary);

        _ui.DrawCenteredText(
            spriteBatch,
            $"Player {_client.PlayerId}  |  Up/Down highlight  |  Esc quit",
            screenWidth / 2,
            UiLayout.LogicalHeight - 28,
            MenuTheme.TextMuted);
    }

    private void RefreshCityList()
    {
        _client.RefreshCityList();
        _cities.Clear();
        _selectedCityIndex = 0;
        _chatLog.Append("Refreshing city list...", ChatColorResolver.System);
    }

    private void ApplyToCity(int cityIndex)
    {
        if (cityIndex < 0 || cityIndex >= _cities.Count)
        {
            return;
        }

        _context.Audio.Play(SoundId.Click);
        _selectedCityIndex = cityIndex;
        var city = _cities[cityIndex];
        _context.SelectedCity = city.CityName;
        _client.ApplyToCity(city.CityId);
        _chatLog.Append(
            city.NeedsMayor
                ? $"Applying to become mayor of {city.CityName}..."
                : $"Applying to join {city.CityName}...",
            ChatColorResolver.System);
    }

    private SceneTransition ApplyNetworkEvents()
    {
        foreach (var networkEvent in _client.DrainEvents())
        {
            switch (networkEvent.Kind)
            {
                case GameClientEventKind.AddRemCity:
                    AddCityEntry(networkEvent.AddRemCity);
                    break;
                case GameClientEventKind.ChatMessage:
                    _chatLog.Append(
                        $"Player{networkEvent.ChatMessage.SenderId}: {networkEvent.ChatMessage.Message}",
                        ChatColorResolver.ForRemoteMessage(0, 1, senderIsDead: false));
                    break;
                case GameClientEventKind.MayorInInterview:
                    _chatLog.Append("That city is not accepting applications right now.", ChatColorResolver.System);
                    break;
                case GameClientEventKind.Interview:
                    _context.NetworkClient = _client;
                    return SceneTransition.Interview;
                case GameClientEventKind.StateGame:
                    if (_client.SpawnState is { } spawn && CityCatalog.IsValidCityId(spawn.City))
                    {
                        _context.SelectedCity = CityCatalog.GetName(spawn.City);
                    }

                    _context.NetworkClient = _client;
                    return SceneTransition.InGameOnline;
                case GameClientEventKind.Disconnected:
                    _context.NetworkClient = null;
                    return SceneTransition.MainMenu;
            }
        }

        return SceneTransition.None;
    }

    private void AddCityEntry(ServerAddRemCityPacket packet)
    {
        var name = CityCatalog.GetName(packet.CityId);
        var roleLabel = packet.NeedsMayor
            ? "Mayor required"
            : $"Commando ({packet.PlayerCount}/{GameConstants.MaxPlayersPerCity})";
        var entry = new MeetingCityEntry(packet.CityId, name, packet.NeedsMayor, roleLabel);
        var index = _cities.FindIndex(city => city.CityId == packet.CityId);
        if (index >= 0)
        {
            _cities[index] = entry;
        }
        else
        {
            _cities.Add(entry);
        }
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    private sealed record MeetingCityEntry(byte CityId, string CityName, bool NeedsMayor, string RoleLabel);
}
