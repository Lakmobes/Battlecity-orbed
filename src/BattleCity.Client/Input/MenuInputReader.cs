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

    public MenuInputState Poll()
    {
        var keyboard = Keyboard.GetState();
        var state = new MenuInputState
        {
            MoveUpPressed = WasPressed(keyboard, Keys.Up) || WasPressed(keyboard, Keys.W),
            MoveDownPressed = WasPressed(keyboard, Keys.Down) || WasPressed(keyboard, Keys.S),
            ConfirmPressed = WasPressed(keyboard, Keys.Enter) || WasPressed(keyboard, Keys.Space),
            CancelPressed = WasPressed(keyboard, Keys.Escape),
        };

        _previousKeyboard = keyboard;
        return state;
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
}
