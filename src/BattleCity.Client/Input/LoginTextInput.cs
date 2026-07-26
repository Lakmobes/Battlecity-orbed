using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Input;

public sealed class LoginTextInput
{
    private readonly int _maxLength;
    private string _text = string.Empty;
    private KeyboardState _previousKeyboard;

    public LoginTextInput(int maxLength = 15)
    {
        _maxLength = maxLength;
    }

    public string Text => _text;

    public void SetText(string text) => _text = Trim(text);

    public void Update()
    {
        var keyboard = Keyboard.GetState();

        foreach (var key in keyboard.GetPressedKeys())
        {
            if (!_previousKeyboard.IsKeyDown(key))
            {
                AppendKey(key, keyboard);
            }
        }

        if (WasPressed(keyboard, Keys.Back) && _text.Length > 0)
        {
            _text = _text[..^1];
        }

        _previousKeyboard = keyboard;
    }

    private void AppendKey(Keys key, KeyboardState keyboard)
    {
        if (key is Keys.Enter or Keys.Escape or Keys.Tab or Keys.Up or Keys.Down)
        {
            return;
        }

        var character = KeyToCharacter(key, keyboard);
        if (character is null)
        {
            return;
        }

        _text = Trim(_text + character.Value);
    }

    private string Trim(string value) =>
        value.Length <= _maxLength ? value : value[.._maxLength];

    private static char? KeyToCharacter(Keys key, KeyboardState keyboard)
    {
        var shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

        if (key >= Keys.A && key <= Keys.Z)
        {
            var offset = key - Keys.A;
            return (char)((shift ? 'A' : 'a') + offset);
        }

        if (key >= Keys.D0 && key <= Keys.D9)
        {
            if (shift)
            {
                return key switch
                {
                    Keys.D2 => '@',
                    _ => null,
                };
            }

            return (char)('0' + (key - Keys.D0));
        }

        return key switch
        {
            Keys.Space => ' ',
            Keys.OemMinus => shift ? '_' : '-',
            Keys.OemPeriod => '.',
            Keys.OemSemicolon => shift ? ':' : null,
            _ => null,
        };
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
}
