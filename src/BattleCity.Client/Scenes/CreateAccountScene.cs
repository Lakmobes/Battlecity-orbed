using BattleCity.Client.Input;
using BattleCity.Client.Network;
using BattleCity.Client.Rendering;

using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;
using BattleCity.Shared.Network.Packets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Scenes;

public sealed class CreateAccountScene : IScene
{
    private enum Field
    {
        Username,
        Password,
        FullName,
        Email,
        Town,
        State,
    }

    private readonly SceneContext _context;
    private readonly ScreenUiRenderer _ui;
    private readonly MenuInputReader _menuInput = new();
    private readonly LoginTextInput _usernameInput = new(maxLength: 15);
    private readonly LoginTextInput _passwordInput = new(maxLength: 15);
    private readonly LoginTextInput _fullNameInput = new(maxLength: 20);
    private readonly LoginTextInput _emailInput = new(maxLength: 50);
    private readonly LoginTextInput _townInput = new(maxLength: 15);
    private readonly LoginTextInput _stateInput = new(maxLength: 15);
    private Field _activeField = Field.Username;
    private string? _statusMessage;
    private KeyboardState _previousKeyboard;

    public CreateAccountScene(SceneContext context)
    {
        _context = context;
        _ui = new ScreenUiRenderer(context.Assets);
        _usernameInput.SetText(context.PlayerName);
        if (!string.Equals(context.PlayerPassword, "guest", StringComparison.OrdinalIgnoreCase))
        {
            _passwordInput.SetText(context.PlayerPassword);
        }

        _townInput.SetText("Buenos Aires");
    }

    public bool DrawsWorld => false;

    public Matrix WorldViewMatrix => Matrix.Identity;

    public void LoadContent() => _ui.LoadContent();

    public SceneTransition Update(GameTime gameTime, int screenWidth, int screenHeight)
    {
        _ui.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        var menuInput = _menuInput.Poll(textEntryMode: true);
        var keyboard = Keyboard.GetState();

        if (menuInput.CancelPressed)
        {
            _context.Audio.Play(SoundId.Click);
            return SceneTransition.Login;
        }

        if (WasPressed(keyboard, Keys.Tab))
        {
            _context.Audio.Play(SoundId.Click);
            var shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
            _activeField = NextField(_activeField, shift ? -1 : 1);
            _previousKeyboard = keyboard;
            return SceneTransition.None;
        }

        if (menuInput.MoveDownPressed)
        {
            _activeField = NextField(_activeField, 1);
        }

        if (menuInput.MoveUpPressed)
        {
            _activeField = NextField(_activeField, -1);
        }

        ActiveInput().Update();

        if (menuInput.ConfirmPressed)
        {
            if (_activeField == Field.State)
            {
                return TryCreateAccount();
            }

            _activeField = NextField(_activeField, 1);
        }

        _previousKeyboard = keyboard;
        return SceneTransition.None;
    }

    private SceneTransition TryCreateAccount()
    {
        _context.Audio.Play(SoundId.Click);

        var username = _usernameInput.Text.Trim();
        var password = _passwordInput.Text;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _statusMessage = "Username and password are required.";
            _activeField = string.IsNullOrWhiteSpace(username) ? Field.Username : Field.Password;
            return SceneTransition.None;
        }

        _statusMessage = "Creating account...";

        var town = _townInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(town))
        {
            town = "Buenos Aires";
        }

        var account = new ClientNewAccountPacket(
            username,
            password,
            _emailInput.Text.Trim(),
            _fullNameInput.Text.Trim(),
            town,
            _stateInput.Text.Trim());

        using var client = new GameClient();
        var created = client.ConnectAndCreateAccount(
            _context.ServerHost,
            _context.ServerPort,
            account,
            TimeSpan.FromSeconds(5));

        if (!created)
        {
            _statusMessage = client.LastError ?? "Could not create account.";
            return SceneTransition.None;
        }

        _context.PlayerName = username;
        _context.PlayerPassword = password;
        _context.LoginStatusMessage = "Account created! Press Enter to log in.";
        return SceneTransition.Login;
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

        var panel = ScreenUiRenderer.CenteredFormPanel(width, height, 600, 500);
        _ui.DrawFormPanel(
            spriteBatch,
            panel,
            "Create Account",
            [
                $"Server: {_context.ServerHost}:{_context.ServerPort}",
                "Username: 1-15 letters, numbers, - or _",
                string.Empty,
                FormatField(Field.Username, _usernameInput.Text, mask: false),
                FormatField(Field.Password, _passwordInput.Text, mask: true),
                FormatField(Field.FullName, _fullNameInput.Text, mask: false),
                FormatField(Field.Email, _emailInput.Text, mask: false),
                FormatField(Field.Town, _townInput.Text, mask: false),
                FormatField(Field.State, _stateInput.Text, mask: false),
                string.Empty,
                _statusMessage ?? string.Empty,
            ],
            "Tab - next field   Enter - next/create   Esc - back to login");
    }

    private string FormatField(Field field, string value, bool mask)
    {
        var label = field switch
        {
            Field.Username => "Username",
            Field.Password => "Password",
            Field.FullName => "Full Name",
            Field.Email => "Email",
            Field.Town => "Town",
            Field.State => "State",
            _ => field.ToString(),
        };

        var display = mask ? new string('*', value.Length) : value;
        var cursor = _activeField == field ? "_" : string.Empty;
        var marker = _activeField == field ? "> " : "  ";
        return $"{marker}{label}: {display}{cursor}";
    }

    private LoginTextInput ActiveInput() => _activeField switch
    {
        Field.Username => _usernameInput,
        Field.Password => _passwordInput,
        Field.FullName => _fullNameInput,
        Field.Email => _emailInput,
        Field.Town => _townInput,
        _ => _stateInput,
    };

    private static Field NextField(Field field, int delta)
    {
        var count = Enum.GetValues<Field>().Length;
        var next = ((int)field + delta + count) % count;
        return (Field)next;
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    public void Dispose()
    {
    }
}
