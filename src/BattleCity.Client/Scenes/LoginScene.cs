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
    private ButtonState _previousMouseButton;
    private bool _isConnecting;
    private GameClient? _pendingClient;
    private Task<bool>? _connectTask;

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

        if (_isConnecting)
        {
            return PollConnect();
        }

        var menuInput = _menuInput.Poll(textEntryMode: true);
        var keyboard = Keyboard.GetState();
        HandleMouseClicks();

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

        // Left/Right are caret keys while typing — don't steal them for field nav.
        ActiveInput().Update();

        if (menuInput.ConfirmPressed)
        {
            if (_activeField != Field.Password)
            {
                _activeField = NextField(_activeField, 1);
            }
            else
            {
                return BeginConnect();
            }
        }

        _previousKeyboard = keyboard;
        return SceneTransition.None;
    }

    private void HandleMouseClicks()
    {
        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed
            && _previousMouseButton == ButtonState.Released;
        _previousMouseButton = mouse.LeftButton;
        if (!clicked)
        {
            return;
        }

        var logical = _context.Presentation.ScreenToLogical(new Vector2(mouse.X, mouse.Y));
        var point = new Point((int)logical.X, (int)logical.Y);
        var panel = ScreenUiRenderer.CenteredFormPanel(UiLayout.LogicalWidth, UiLayout.LogicalHeight, 600, 420);
        var fields = GetFieldBounds(panel);
        for (var i = 0; i < fields.Length; i++)
        {
            if (fields[i].Contains(point))
            {
                _context.Audio.Play(SoundId.Click);
                _activeField = (Field)i;
                return;
            }
        }
    }

    private static Rectangle[] GetFieldBounds(Rectangle panel)
    {
        // Must match LoginScene.DrawScreen line layout + ScreenUiRenderer.DrawFormPanel spacing.
        var fieldWidth = panel.Width - 64;
        var fieldX = panel.X + 32;
        var y = panel.Y + 76;
        y += 26; // hint
        y += 26; // hint
        y += 14; // blank
        var server = new Rectangle(fieldX, y, fieldWidth, MenuTheme.FormFieldHeight);
        y += MenuTheme.FormFieldHeight + MenuTheme.FormFieldGap;
        var username = new Rectangle(fieldX, y, fieldWidth, MenuTheme.FormFieldHeight);
        y += MenuTheme.FormFieldHeight + MenuTheme.FormFieldGap;
        var password = new Rectangle(fieldX, y, fieldWidth, MenuTheme.FormFieldHeight);
        return [server, username, password];
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

    private SceneTransition BeginConnect()
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
        _pendingClient = new GameClient();
        var host = _context.ServerHost;
        var port = _context.ServerPort;
        _isConnecting = true;
        _connectTask = Task.Run(() =>
            _pendingClient.ConnectAndLogin(host, port, username, password, TimeSpan.FromSeconds(5)));
        return SceneTransition.None;
    }

    private SceneTransition PollConnect()
    {
        if (_connectTask is null || !_connectTask.IsCompleted)
        {
            _statusMessage = "Connecting...";
            return SceneTransition.None;
        }

        _isConnecting = false;
        var connected = false;
        try
        {
            connected = _connectTask.Result;
        }
        catch (Exception ex)
        {
            _statusMessage = ex.InnerException?.Message ?? ex.Message;
            _pendingClient?.Dispose();
            _pendingClient = null;
            _connectTask = null;
            return SceneTransition.None;
        }

        var client = _pendingClient;
        _pendingClient = null;
        _connectTask = null;

        if (client is null || !connected)
        {
            _statusMessage = client?.LastError ?? "Connection failed.";
            client?.Dispose();
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
        var status = _statusMessage
            ?? (_isConnecting
                ? "Connecting..."
                : "Enter connects   Tab / arrows switch field   click a field to focus");
        _ui.DrawFormPanel(
            spriteBatch,
            panel,
            "Multiplayer Login",
            [
                "Local Server menu auto-starts 127.0.0.1 — or paste a friend invite (Ctrl+V)",
                "Guest login: leave user blank or set password to guest",
                string.Empty,
                FormatField("Server", _serverInput, mask: false, focused: _activeField == Field.Server),
                FormatField("Username", _usernameInput, mask: false, focused: _activeField == Field.Username),
                FormatField("Password", _passwordInput, mask: true, focused: _activeField == Field.Password),
                string.Empty,
                status,
            ],
            "Tab/arrows - next field   Enter - connect   Ctrl+V - paste   F2 - create account   Esc - back");
    }

    private static string FormatField(string label, LoginTextInput input, bool mask, bool focused)
    {
        var marker = focused ? "> " : "  ";
        return $"{marker}{label}: {input.FormatDisplay(mask, focused)}";
    }

    public void Dispose()
    {
        if (_isConnecting)
        {
            _pendingClient?.Dispose();
            _pendingClient = null;
        }
    }
}
