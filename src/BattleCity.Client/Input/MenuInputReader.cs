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
    private GamePadState _previousGamePad;
    private bool _primed;

    public void Reset()
    {
        _previousKeyboard = Keyboard.GetState();
        _previousGamePad = GamePad.GetState(GameplayGamepadMap.Player);
        _primed = true;
    }

    public MenuInputState Poll(bool textEntryMode = false)
    {
        var keyboard = Keyboard.GetState();
        var gamePad = GamePad.GetState(GameplayGamepadMap.Player);
        if (!_primed)
        {
            _previousKeyboard = keyboard;
            _previousGamePad = gamePad;
            _primed = true;
        }

        if (gamePad.IsConnected != _previousGamePad.IsConnected)
        {
            _previousGamePad = gamePad;
        }

        // WASD / Space fight letter and space typing in login/account fields.
        // Gamepad A/B stay available so pads can confirm/cancel without the keyboard.
        var state = new MenuInputState
        {
            MoveUpPressed = WasPressed(keyboard, Keys.Up)
                || (!textEntryMode && WasPressed(keyboard, Keys.W))
                || StickOrDpadUpPressed(gamePad, _previousGamePad),
            MoveDownPressed = WasPressed(keyboard, Keys.Down)
                || (!textEntryMode && WasPressed(keyboard, Keys.S))
                || StickOrDpadDownPressed(gamePad, _previousGamePad),
            ConfirmPressed = WasPressed(keyboard, Keys.Enter)
                || (!textEntryMode && WasPressed(keyboard, Keys.Space))
                || GamepadUiMapper.WasPressed(gamePad, _previousGamePad, GameplayGamepadMap.MenuConfirm),
            CancelPressed = WasPressed(keyboard, Keys.Escape)
                || GamepadUiMapper.WasPressed(gamePad, _previousGamePad, GameplayGamepadMap.MenuCancel)
                || GamepadUiMapper.WasPressed(gamePad, _previousGamePad, GameplayGamepadMap.ToggleSettings),
        };

        _previousKeyboard = keyboard;
        _previousGamePad = gamePad;
        return state;
    }

    private static bool StickOrDpadUpPressed(GamePadState current, GamePadState previous)
    {
        if (!current.IsConnected)
        {
            return false;
        }

        if (current.DPad.Up == ButtonState.Pressed && previous.DPad.Up != ButtonState.Pressed)
        {
            return true;
        }

        return CrossedStickThreshold(
            previous.ThumbSticks.Left.Y,
            current.ThumbSticks.Left.Y,
            upward: true);
    }

    private static bool StickOrDpadDownPressed(GamePadState current, GamePadState previous)
    {
        if (!current.IsConnected)
        {
            return false;
        }

        if (current.DPad.Down == ButtonState.Pressed && previous.DPad.Down != ButtonState.Pressed)
        {
            return true;
        }

        return CrossedStickThreshold(
            previous.ThumbSticks.Left.Y,
            current.ThumbSticks.Left.Y,
            upward: false);
    }

    private static bool CrossedStickThreshold(float previousY, float currentY, bool upward)
    {
        var threshold = GameplayGamepadMap.StickDeadzone;
        if (upward)
        {
            return previousY < threshold && currentY >= threshold;
        }

        return previousY > -threshold && currentY <= -threshold;
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
}
