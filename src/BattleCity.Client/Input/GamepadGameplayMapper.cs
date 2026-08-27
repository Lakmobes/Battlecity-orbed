using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Input;

/// <summary>
/// Maps an Xbox-style <see cref="GamePadState"/> into <see cref="GameplayInputState"/>.
/// Does not read keyboard/mouse; <see cref="GameplayInputMerger"/> combines devices.
/// </summary>
public static class GamepadGameplayMapper
{
    public static GameplayInputState Sample(GamePadState current, GamePadState previous)
    {
        if (!current.IsConnected)
        {
            return default;
        }

        var (turn, move) = ReadDriveAxes(current.ThumbSticks.Left, current.DPad);
        var modifierHeld = current.IsButtonDown(GameplayGamepadMap.ItemModifier);

        return new GameplayInputState
        {
            Turn = turn,
            Move = move,
            FireHeld = current.Triggers.Right >= GameplayGamepadMap.TriggerThreshold,
            FireFlareHeld = current.Triggers.Left >= GameplayGamepadMap.TriggerThreshold,
            UseCloakPressed = !modifierHeld && WasPressed(current, previous, GameplayGamepadMap.UseCloak),
            DropSelectedItemPressed = !modifierHeld && WasPressed(current, previous, GameplayGamepadMap.DropSelectedItem),
            PickUpItemPressed = !modifierHeld && WasPressed(current, previous, GameplayGamepadMap.PickUpItem),
            UseMedKitPressed = !modifierHeld && WasPressed(current, previous, GameplayGamepadMap.UseMedKit),
            CycleInventoryPreviousPressed = WasPressed(current, previous, GameplayGamepadMap.CycleInventoryPrevious),
            CycleInventoryNextPressed = WasPressed(current, previous, GameplayGamepadMap.CycleInventoryNext),
            DropBombPressed = modifierHeld && WasPressed(current, previous, GameplayGamepadMap.DropBomb),
            DropOrbPressed = modifierHeld && WasPressed(current, previous, GameplayGamepadMap.DropOrb),
        };
    }

    /// <summary>
    /// Left stick (after deadzone) and D-pad both contribute; opposing inputs cancel via clamp.
    /// Stick +Y is forward (MonoGame thumbstick convention).
    /// </summary>
    public static (int Turn, int Move) ReadDriveAxes(Vector2 leftStick, GamePadDPad dpad)
    {
        var stickTurn = 0;
        var stickMove = 0;
        if (leftStick.LengthSquared() >= GameplayGamepadMap.StickDeadzone * GameplayGamepadMap.StickDeadzone)
        {
            if (leftStick.X <= -GameplayGamepadMap.StickDeadzone)
            {
                stickTurn = -1;
            }
            else if (leftStick.X >= GameplayGamepadMap.StickDeadzone)
            {
                stickTurn = 1;
            }

            if (leftStick.Y <= -GameplayGamepadMap.StickDeadzone)
            {
                stickMove = -1;
            }
            else if (leftStick.Y >= GameplayGamepadMap.StickDeadzone)
            {
                stickMove = 1;
            }
        }

        var dpadTurn = 0;
        if (dpad.Left == ButtonState.Pressed)
        {
            dpadTurn = -1;
        }
        else if (dpad.Right == ButtonState.Pressed)
        {
            dpadTurn = 1;
        }

        var dpadMove = 0;
        if (dpad.Up == ButtonState.Pressed)
        {
            dpadMove = 1;
        }
        else if (dpad.Down == ButtonState.Pressed)
        {
            dpadMove = -1;
        }

        return (
            Math.Clamp(stickTurn + dpadTurn, -1, 1),
            Math.Clamp(stickMove + dpadMove, -1, 1));
    }

    private static bool WasPressed(GamePadState current, GamePadState previous, Buttons button) =>
        current.IsButtonDown(button) && !previous.IsButtonDown(button);
}
