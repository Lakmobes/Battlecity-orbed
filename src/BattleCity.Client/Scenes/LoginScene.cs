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
    private enum Field
    {
        Server,
        Username,
        Password,
    }

    private readonly SceneContext _context;
    private readonly ScreenUiRenderer _ui;
    private readonly MenuInputReader _menuInput = new();
    private readonly LoginTextInput _serverInput = new(maxLength: 64);
    private readonly LoginTextInput _usernameInput = new(maxLength: 15);
    private readonly LoginTextInput _passwordInput = new(maxLength: 15);
    private Field _activeField = Field.Username;
    private string? _statusMessage;
    private KeyboardState _previousKeyboard;

    public LoginScene(SceneContext context)
    {
        _context = context;
        _ui = new ScreenUiRenderer(context.Assets);
        _serverInput.SetText(FormatServerField(context.ServerHost, context.ServerPort));
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
        _ui.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        var menuInput = _menuInput.Poll();
        var keyboard = Keyboard.GetState();

        if (WasPressed(keyboard, Keys.Tab))
        {
            _context.Audio.Play(SoundId.Click);
            var shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
            _activeField = NextField(_activeField, shift ? -1 : 1);
            _previousKeyboard = keyboard;
            return SceneTransition.None;
        }

        if (WasPressed(keyboard, Keys.F2))
        {
            _context.Audio.Play(SoundId.Click);
            ApplyServerFieldToContext();
            _context.PlayerName = _usernameInput.Text.Trim();
            _context.PlayerPassword = _passwordInput.Text;
            _previousKeyboard = keyboard;
            return SceneTransition.CreateAccount;
        }

        if (menuInput.MoveDownPressed)
        {
            _activeField = NextField(_activeField, 1);
        }

        if (menuInput.MoveUpPressed)
        {
            _activeField = NextField(_activeField, -1);
        }

        if (menuInput.CancelPressed)
        {
            _context.Audio.Play(SoundId.Click);
            return SceneTransition.MainMenu;
        }

        ActiveInput().Update();

        if (menuInput.ConfirmPressed)
        {
            if (_activeField != Field.Password)
            {
                _activeField = NextField(_activeField, 1);
            }
            else
            {
                return TryConnect();
            }
        }

        _previousKeyboard = keyboard;
        return SceneTransition.None;
    }

    private LoginTextInput ActiveInput() =>
        _activeField switch
        {
            Field.Server => _serverInput,
            Field.Username => _usernameInput,
            _ => _passwordInput,
        };

    private static Field NextField(Field current, int delta)
    {
        var values = Enum.GetValues<Field>();
        var index = ((int)current + delta) % values.Length;
        if (index < 0)
        {
            index += values.Length;
        }

        return values[index];
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    private SceneTransition TryConnect()
    {
        _context.Audio.Play(SoundId.Click);
        _statusMessage = "Connecting...";

        if (!ApplyServerFieldToContext())
        {
            _statusMessage = "Invalid server. Use host or host:port (example 192.168.1.10:5643).";
            _activeField = Field.Server;
            return SceneTransition.None;
        }

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
            _context.ServerPort,
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

    private bool ApplyServerFieldToContext()
    {
        if (!TryParseServer(_serverInput.Text, out var host, out var port))
        {
            return false;
        }

        _context.ServerHost = host;
        _context.ServerPort = port;
        return true;
    }

    public static bool TryParseServer(string text, out string host, out int port)
    {
        host = "127.0.0.1";
        port = NetworkConstants.TcpPort;
        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        // host:port — split on last ':' so IPv6 is not required for now.
        var colon = text.LastIndexOf(':');
        if (colon > 0 && colon < text.Length - 1
            && int.TryParse(text[(colon + 1)..], out var parsedPort)
            && parsedPort is > 0 and <= 65535)
        {
            host = text[..colon].Trim();
            port = parsedPort;
            return !string.IsNullOrWhiteSpace(host);
        }

        host = text;
        return !string.IsNullOrWhiteSpace(host);
    }

    private static string FormatServerField(string host, int port) =>
        port == NetworkConstants.TcpPort ? host : $"{host}:{port}";

    public void DrawWorld(SpriteBatch spriteBatch)
    {
    }

    public void DrawScreen(SpriteBatch spriteBatch)
    {
        var width = UiLayout.LogicalWidth;
        var height = UiLayout.LogicalHeight;
        _ui.DrawBackdrop(spriteBatch, width, height);
        _ui.DrawTitle(spriteBatch, width);

        var panel = ScreenUiRenderer.CenteredFormPanel(width, height, 600, 420);
        _ui.DrawFormPanel(
            spriteBatch,
            panel,
            "Multiplayer Login",
            [
                "Paste the host invite (IP or IP:port)",
                "Guest login: leave user blank or set password to guest",
                string.Empty,
                FormatField("Server", _serverInput.Text, mask: false, focused: _activeField == Field.Server),
                FormatField("Username", _usernameInput.Text, mask: false, focused: _activeField == Field.Username),
                FormatField("Password", _passwordInput.Text, mask: true, focused: _activeField == Field.Password),
                string.Empty,
                _statusMessage ?? "Enter connects   Tab switches field",
            ],
            "Tab - next field   Enter - connect   F2 - create account   Esc - back");
    }

    private static string FormatField(string label, string value, bool mask, bool focused)
    {
        var display = mask ? new string('*', value.Length) : value;
        var cursor = focused ? "_" : string.Empty;
        var marker = focused ? "> " : "  ";
        return $"{marker}{label}: {display}{cursor}";
    }

    public void Dispose()
    {
    }
}
