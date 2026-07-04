using BattleCity.Client.Chat;
using BattleCity.Client.Input;
using BattleCity.Client.Network;
using BattleCity.Client.Rendering;
using BattleCity.Shared.Constants;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Scenes;

public sealed class InterviewScene : IScene
{
    private readonly SceneContext _context;
    private readonly GameClient _client;
    private readonly ScreenUiRenderer _ui;
    private readonly MenuInputReader _menuInput = new();
    private readonly InGameChatInput _chatInput = new();
    private readonly InGameChatLog _chatLog = new();
    private string _statusMessage = "Waiting for the mayor to respond...";

    public InterviewScene(SceneContext context, GameClient client)
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
        _chatLog.Append(
            "Please wait here while the mayor is notified of your arrival.",
            ChatColorResolver.System);
    }

    public SceneTransition Update(GameTime gameTime, int screenWidth, int screenHeight)
    {
        _client.Poll();
        foreach (var networkEvent in _client.DrainEvents())
        {
            switch (networkEvent.Kind)
            {
                case GameClientEventKind.StateGame:
                    _context.NetworkClient = _client;
                    return SceneTransition.InGameOnline;
                case GameClientEventKind.Comms:
                    AppendComms(networkEvent.ChatMessage.SenderId, networkEvent.ChatMessage.Message);
                    break;
                case GameClientEventKind.MayorDeclined:
                    _statusMessage = "The mayor declined your application.";
                    _context.NetworkClient = _client;
                    return SceneTransition.Meeting;
                case GameClientEventKind.InterviewCancel:
                    _statusMessage = "The interview was cancelled.";
                    _context.NetworkClient = _client;
                    return SceneTransition.Meeting;
                case GameClientEventKind.Disconnected:
                    _context.NetworkClient = null;
                    return SceneTransition.MainMenu;
            }
        }

        var menuInput = _menuInput.Poll();
        if (menuInput.CancelPressed && !_chatInput.IsActive)
        {
            _client.CancelJobApplication();
            _context.NetworkClient = _client;
            return SceneTransition.Meeting;
        }

        var keyboard = Keyboard.GetState();
        var chatUpdate = _chatInput.Update(keyboard);
        if (chatUpdate.Submitted)
        {
            if (!string.IsNullOrWhiteSpace(chatUpdate.Message))
            {
                _client.SendInterviewChat(chatUpdate.Message);
                InGameChatService.AppendLocalOutgoing(
                    _chatLog,
                    _context.PlayerName,
                    chatUpdate.Message,
                    isDead: false);
            }
        }

        return SceneTransition.None;
    }

    public void DrawWorld(SpriteBatch spriteBatch)
    {
    }

    public void DrawScreen(SpriteBatch spriteBatch)
    {
        var lines = new List<string>
        {
            $"Interview - Player {_client.PlayerId}",
            string.Empty,
            _statusMessage,
            string.Empty,
            "Interview chat:",
        };

        foreach (var line in _chatLog.Lines)
        {
            lines.Add(line.Text);
        }

        if (_chatInput.IsActive)
        {
            lines.Add($"> {_chatInput.Draft}_");
        }

        _ui.DrawBackdrop(spriteBatch, RenderConstants.DefaultWindowWidth, RenderConstants.DefaultWindowHeight);
        _ui.DrawTitle(spriteBatch, RenderConstants.DefaultWindowWidth);
        _ui.DrawMessageBlock(
            spriteBatch,
            RenderConstants.DefaultWindowWidth,
            RenderConstants.DefaultWindowHeight,
            "Interview",
            lines,
            "Enter chat with mayor   Esc cancel");
    }

    public void Dispose()
    {
    }

    private void AppendComms(byte senderId, string message)
    {
        var prefix = senderId == _client.PlayerId
            ? _context.PlayerName
            : $"Player{senderId}";
        _chatLog.Append($"{prefix}: {message}", ChatColorResolver.ForRemoteMessage(0, 1, senderIsDead: false));
    }
}
