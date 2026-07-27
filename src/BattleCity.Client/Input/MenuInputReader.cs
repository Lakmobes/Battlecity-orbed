using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Input;

public readonly struct MenuInputState
{
    public bool MoveUpPressed { get; init; }
    public bool MoveDownPressed { get; init; }
    public bool ConfirmPressed { get; init; }
    public bool CancelPressed { get; init; }
}

public sealed class MenuInputReader
{
    private KeyboardState _previousKeyboard;
    private bool _primed;

    public void Reset()
    {
        _previousKeyboard = Keyboard.GetState();
        _primed = true;
    }

    public MenuInputState Poll(bool textEntryMode = false)
    {
        var keyboard = Keyboard.GetState();
        if (!_primed)
        {
            _previousKeyboard = keyboard;
            _primed = true;
        }

        // WASD / Space fight letter and space typing in login/account fields.
        var state = new MenuInputState
        {
            MoveUpPressed = WasPressed(keyboard, Keys.Up)
                || (!textEntryMode && WasPressed(keyboard, Keys.W)),
            MoveDownPressed = WasPressed(keyboard, Keys.Down)
                || (!textEntryMode && WasPressed(keyboard, Keys.S)),
            ConfirmPressed = WasPressed(keyboard, Keys.Enter)
                || (!textEntryMode && WasPressed(keyboard, Keys.Space)),
            CancelPressed = WasPressed(keyboard, Keys.Escape),
        };

        _previousKeyboard = keyboard;
        return state;
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
}
