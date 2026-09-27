using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Input;

/// <summary>Single-line text field with caret for login / chat entry.</summary>
public sealed class LoginTextInput
{
    private readonly int _maxLength;
    private string _text = string.Empty;
    private int _caret;
    private KeyboardState _previousKeyboard;

    public LoginTextInput(int maxLength = 15)
    {
        _maxLength = maxLength;
    }

    public string Text => _text;

    public int Caret => _caret;

    public void SetText(string text)
    {
        _text = Trim(text);
        _caret = _text.Length;
    }

    public void Update()
    {
        var keyboard = Keyboard.GetState();
        var ctrl = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);

        if (ctrl && WasPressed(keyboard, Keys.V) && NativeClipboard.TryGetText(out var pasted))
        {
            Insert(pasted);
            _previousKeyboard = keyboard;
            return;
        }

        if (ctrl)
        {
            _previousKeyboard = keyboard;
            return;
        }

        if (WasPressed(keyboard, Keys.Left))
        {
            _caret = Math.Max(0, _caret - 1);
            _previousKeyboard = keyboard;
            return;
        }

        if (WasPressed(keyboard, Keys.Right))
        {
            _caret = Math.Min(_text.Length, _caret + 1);
            _previousKeyboard = keyboard;
            return;
        }

        if (WasPressed(keyboard, Keys.Home))
        {
            _caret = 0;
            _previousKeyboard = keyboard;
            return;
        }

        if (WasPressed(keyboard, Keys.End))
        {
            _caret = _text.Length;
            _previousKeyboard = keyboard;
            return;
        }

        if (WasPressed(keyboard, Keys.Delete) && _caret < _text.Length)
        {
            _text = _text.Remove(_caret, 1);
            _previousKeyboard = keyboard;
            return;
        }

        if (WasPressed(keyboard, Keys.Back) && _caret > 0)
        {
            _text = _text.Remove(_caret - 1, 1);
            _caret--;
            _previousKeyboard = keyboard;
            return;
        }

        foreach (var key in keyboard.GetPressedKeys())
        {
            if (!_previousKeyboard.IsKeyDown(key))
            {
                AppendKey(key, keyboard);
            }
        }

        _previousKeyboard = keyboard;
    }

    /// <summary>Visible field string with caret when focused.</summary>
    public string FormatDisplay(bool mask, bool focused)
    {
        var display = mask ? new string('*', _text.Length) : _text;
        if (!focused)
        {
            return display;
        }

        var caret = Math.Clamp(_caret, 0, display.Length);
        return display[..caret] + "_" + display[caret..];
    }

    private void Insert(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var before = _text[.._caret];
        var after = _text[_caret..];
        var combined = Trim(before + value + after);
        var inserted = Math.Min(value.Length, combined.Length - before.Length);
        _text = combined;
        _caret = Math.Min(combined.Length, before.Length + Math.Max(0, inserted));
    }

    private void AppendKey(Keys key, KeyboardState keyboard)
    {
        if (key is Keys.Enter or Keys.Escape or Keys.Tab or Keys.Up or Keys.Down
            or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Delete or Keys.Back
            or Keys.LeftControl or Keys.RightControl or Keys.LeftAlt or Keys.RightAlt
            or Keys.LeftShift or Keys.RightShift)
        {
            return;
        }

        var character = KeyToCharacter(key, keyboard);
        if (character is null)
        {
            return;
        }

        Insert(character.Value.ToString());
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

        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
        {
            return (char)('0' + (key - Keys.NumPad0));
        }

        return key switch
        {
            Keys.Space => ' ',
            Keys.OemMinus => shift ? '_' : '-',
            Keys.OemPeriod or Keys.Decimal => '.',
            Keys.OemSemicolon => shift ? ':' : null,
            Keys.Divide => '/',
            _ => null,
        };
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
}
