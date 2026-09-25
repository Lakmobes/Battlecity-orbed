using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Input;

/// <summary>Gamepad contributions to <see cref="UiInputState"/> (camera, settings, pointer).</summary>
public static class GamepadUiMapper
{
    public static void ApplyCameraPanFromStick(Vector2 rightStick, ref bool left, ref bool right, ref bool up, ref bool down)
    {
        if (rightStick.LengthSquared() < GameplayGamepadMap.StickDeadzone * GameplayGamepadMap.StickDeadzone)
        {
            return;
        }

        if (rightStick.X <= -GameplayGamepadMap.StickDeadzone)
        {
            left = true;
        }
        else if (rightStick.X >= GameplayGamepadMap.StickDeadzone)
        {
            right = true;
        }

        // MonoGame thumbstick +Y is up.
        if (rightStick.Y <= -GameplayGamepadMap.StickDeadzone)
        {
            down = true;
        }
        else if (rightStick.Y >= GameplayGamepadMap.StickDeadzone)
        {
            up = true;
        }
    }

    public static Vector2 MovePointer(Vector2 current, Vector2 rightStick, float deltaSeconds, int width, int height)
    {
        if (rightStick.LengthSquared() < GameplayGamepadMap.StickDeadzone * GameplayGamepadMap.StickDeadzone)
        {
            return current;
        }

        // Rescale stick so motion starts at the deadzone edge.
        var magnitude = rightStick.Length();
        var scaled = rightStick * ((magnitude - GameplayGamepadMap.StickDeadzone) / (1f - GameplayGamepadMap.StickDeadzone) / magnitude);
        var delta = scaled * GameplayGamepadMap.PointerSpeedPixelsPerSecond * MathF.Max(0f, deltaSeconds);
        // Stick +Y is up; logical Y increases downward.
        return new Vector2(
            Math.Clamp(current.X + delta.X, 0f, Math.Max(0, width - 1)),
            Math.Clamp(current.Y - delta.Y, 0f, Math.Max(0, height - 1)));
    }

    public static bool WasPressed(GamePadState current, GamePadState previous, Buttons button) =>
        current.IsConnected
        && current.IsButtonDown(button)
        && !previous.IsButtonDown(button);
}
