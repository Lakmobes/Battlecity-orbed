using BattleCity.Client.Input;
using BattleCity.Client.Network;
using BattleCity.Client.Rendering;

using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Scenes;

public sealed class LoginScene : IScene
{
    private readonly SceneContext _context;
    private readonly ScreenUiRenderer _ui;
    private readonly MenuInputReader _menuInput = new();
    private readonly LoginTextInput _usernameInput = new(maxLength: 15);
    private readonly LoginTextInput _passwordInput = new(maxLength: 15);
    private bool _editingPassword;
    private string? _statusMessage;
    private KeyboardState _previousKeyboard;

    public LoginScene(SceneContext context)
    {
        _context = context;
        _ui = new ScreenUiRenderer(context.Assets);
        _usernameInput.SetText(context.PlayerName);
        _passwordInput.SetText(context.PlayerPassword);
        _statusMessage = context.LoginStatusMessage;
        context.LoginStatusMessage = null;
    }

    public bool DrawsWorld => false;

    public Matrix WorldViewMatrix => Matrix.Identity;

    public void LoadContent() => _ui.LoadContent();

    public SceneTransition Update(GameTime gameTime, int screenWidth, int screenHeight)
    {
        var menuInput = _menuInput.Poll();
        var keyboard = Keyboard.GetState();

        if (WasPressed(keyboard, Keys.Tab))
        {
            _context.Audio.Play(SoundId.Click);
            _context.PlayerName = _usernameInput.Text.Trim();
            _context.PlayerPassword = _passwordInput.Text;
            _previousKeyboard = keyboard;
            return SceneTransition.CreateAccount;
        }

        if (menuInput.MoveDownPressed)
        {
            _editingPassword = true;
        }

        if (menuInput.MoveUpPressed)
        {
            _editingPassword = false;
        }

        if (menuInput.CancelPressed)
        {
            _context.Audio.Play(SoundId.Click);
            return SceneTransition.MainMenu;
        }

        if (!_editingPassword)
        {
            _usernameInput.Update();
        }
        else
        {
            _passwordInput.Update();
        }

        if (menuInput.ConfirmPressed)
        {
            if (!_editingPassword)
            {
                _editingPassword = true;
            }
            else
            {
                return TryConnect();
            }
        }

        _previousKeyboard = keyboard;
        return SceneTransition.None;
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    private SceneTransition TryConnect()
    {
        _context.Audio.Play(SoundId.Click);
        _statusMessage = "Connecting...";

        var username = _usernameInput.Text.Trim();
        var password = _passwordInput.Text;
        if (string.IsNullOrWhiteSpace(username))
        {
            username = $"Guest{Random.Shared.Next(100, 999)}";
        }

        _context.PlayerName = username;
        _context.PlayerPassword = password;

        _context.NetworkClient?.Dispose();
        var client = new GameClient();
        var connected = client.ConnectAndLogin(
            _context.ServerHost,
            NetworkConstants.TcpPort,
            username,
            password,
            TimeSpan.FromSeconds(5));

        if (!connected)
        {
            _statusMessage = client.LastError ?? "Connection failed.";
            client.Dispose();
            return SceneTransition.None;
        }

        _context.NetworkClient = client;
        _context.SelectedCity = "Buenos Aires";
        _context.CityDesign = "demo";
        return SceneTransition.Meeting;
    }

    public void DrawWorld(SpriteBatch spriteBatch)
    {
    }

    public void DrawScreen(SpriteBatch spriteBatch)
    {
        _ui.DrawBackdrop(spriteBatch, RenderConstants.DefaultWindowWidth, RenderConstants.DefaultWindowHeight);
        _ui.DrawTitle(spriteBatch, RenderConstants.DefaultWindowWidth);
        _ui.DrawMessageBlock(
            spriteBatch,
            RenderConstants.DefaultWindowWidth,
            RenderConstants.DefaultWindowHeight,
            "Multiplayer Login",
            [
                $"Server: {_context.ServerHost}:{NetworkConstants.TcpPort}",
                "Guest login: password = guest",
                $"Username: {_usernameInput.Text}{(_editingPassword ? string.Empty : "_")}",
                $"Password: {new string('*', _passwordInput.Text.Length)}{(_editingPassword ? "_" : string.Empty)}",
                _statusMessage ?? "Down - password field   Tab - create account",
            ],
            "Up/Down - switch field   Enter - next/connect   Tab - create account   Esc - back");
    }

    public void Dispose()
    {
    }
}
