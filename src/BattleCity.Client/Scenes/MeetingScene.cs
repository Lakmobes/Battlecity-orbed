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
    private static readonly Color PanelFill = new(24, 30, 48, 230);
    private static readonly Color PanelBorder = new(72, 88, 128);
    private static readonly Color RowSelected = new(48, 64, 104, 180);
    private static readonly Color RowHover = new(40, 52, 80, 140);
    private static readonly Color MutedText = new(160, 168, 188);
    private static readonly Color AccentText = new(255, 220, 96);

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
    private MouseState _previousMouse;

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
        _hoverCityIndex = !_chatInput.IsActive
            && MeetingRoomLayout.TryGetCityIndexAt(mouse.X, mouse.Y, _cities.Count, out var hoverIndex)
            ? hoverIndex
            : -1;

        var clicked = mouse.LeftButton == ButtonState.Pressed
            && _previousMouse.LeftButton == ButtonState.Released;

        if (clicked && !_chatInput.IsActive)
        {
            if (MeetingRoomLayout.RefreshButton.Contains(mouse.X, mouse.Y))
            {
                RefreshCityList();
            }
            else if (MeetingRoomLayout.TryGetCityIndexAt(mouse.X, mouse.Y, _cities.Count, out var cityIndex))
            {
                ApplyToCity(cityIndex);
            }
        }

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        return SceneTransition.None;
    }

    public void DrawWorld(SpriteBatch spriteBatch)
    {
    }

    public void DrawScreen(SpriteBatch spriteBatch)
    {
        var width = RenderConstants.DefaultWindowWidth;
        var height = RenderConstants.DefaultWindowHeight;

        _ui.DrawBackdrop(spriteBatch, width, height);
        _ui.DrawTitle(spriteBatch, width);

        DrawCitiesPanel(spriteBatch);
        DrawChatPanel(spriteBatch);
        DrawFooter(spriteBatch, width);
    }

    public void Dispose()
    {
    }

    private void DrawCitiesPanel(SpriteBatch spriteBatch)
    {
        var panel = MeetingRoomLayout.CitiesPanel;
        _ui.DrawPanel(spriteBatch, panel, PanelFill, PanelBorder);
        _ui.DrawText(spriteBatch, "Open Cities", panel.X + MeetingRoomLayout.PanelPadding, panel.Y + 8, Color.White);

        if (_cities.Count == 0)
        {
            _ui.DrawText(
                spriteBatch,
                "Waiting for city list...",
                panel.X + MeetingRoomLayout.PanelPadding,
                MeetingRoomLayout.CityListTop + 4,
                MutedText,
                0.9f);
            _ui.DrawText(
                spriteBatch,
                "Click Refresh or press R",
                panel.X + MeetingRoomLayout.PanelPadding,
                MeetingRoomLayout.CityListTop + 28,
                MutedText,
                0.85f);
            return;
        }

        for (var i = 0; i < _cities.Count; i++)
        {
            var row = MeetingRoomLayout.GetCityRowBounds(i);
            if (i == _selectedCityIndex)
            {
                _ui.DrawPanel(spriteBatch, row, RowSelected, RowSelected);
            }
            else if (i == _hoverCityIndex)
            {
                _ui.DrawPanel(spriteBatch, row, RowHover, RowHover);
            }

            var city = _cities[i];
            var nameColor = i == _selectedCityIndex ? AccentText : Color.White;
            _ui.DrawText(spriteBatch, city.CityName, row.X + 6, row.Y + 4, nameColor, 0.95f);
            _ui.DrawText(spriteBatch, city.RoleLabel, row.X + 6, row.Y + 16, MutedText, 0.75f);
        }

        _ui.DrawText(
            spriteBatch,
            "Click a city to apply",
            panel.X + MeetingRoomLayout.PanelPadding,
            panel.Bottom - 24,
            MutedText,
            0.85f);
    }

    private void DrawChatPanel(SpriteBatch spriteBatch)
    {
        var panel = MeetingRoomLayout.ChatPanel;
        _ui.DrawPanel(spriteBatch, panel, PanelFill, PanelBorder);
        _ui.DrawText(spriteBatch, "Lobby Chat", panel.X + MeetingRoomLayout.PanelPadding, panel.Y + 8, Color.White);

        var chatY = MeetingRoomLayout.CityListTop;
        foreach (var line in _chatLog.Lines)
        {
            _ui.DrawText(spriteBatch, line.Text, panel.X + MeetingRoomLayout.PanelPadding, chatY, line.Color, 0.85f);
            chatY += 18;
        }

        if (_chatInput.IsActive)
        {
            _ui.DrawText(
                spriteBatch,
                $"> {_chatInput.Draft}_",
                panel.X + MeetingRoomLayout.PanelPadding,
                panel.Bottom - 28,
                AccentText,
                0.9f);
        }
        else
        {
            _ui.DrawText(
                spriteBatch,
                "Press Enter to chat",
                panel.X + MeetingRoomLayout.PanelPadding,
                panel.Bottom - 28,
                MutedText,
                0.85f);
        }
    }

    private void DrawFooter(SpriteBatch spriteBatch, int screenWidth)
    {
        var refresh = MeetingRoomLayout.RefreshButton;
        var refreshHover = refresh.Contains(_previousMouse.X, _previousMouse.Y);
        _ui.DrawPanel(
            spriteBatch,
            refresh,
            refreshHover ? RowHover : new Color(36, 44, 68),
            PanelBorder);
        _ui.DrawText(spriteBatch, "Refresh (R)", refresh.X + 10, refresh.Y + 6, Color.White, 0.85f);

        _ui.DrawCenteredText(
            spriteBatch,
            $"Player {_client.PlayerId}  |  Up/Down highlight  |  Esc quit",
            screenWidth / 2,
            RenderConstants.DefaultWindowHeight - 24,
            MutedText,
            0.85f);
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
        _cities.Add(new MeetingCityEntry(packet.CityId, name, packet.NeedsMayor, roleLabel));
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    private sealed record MeetingCityEntry(byte CityId, string CityName, bool NeedsMayor, string RoleLabel);
}
