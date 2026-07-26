using BattleCity.Client.Rendering;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Input;

public readonly record struct FrameInput(GameplayInputState Gameplay, UiInputState Ui);

public sealed class InputManager
{
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;

    public FrameInput Poll(
        Camera2D camera,
        Vector2 playerWorldCenter,
        int worldViewportWidth,
        DisplayPresentation presentation)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        var cameraPan = keyboard.IsKeyDown(GameplayInputMap.CameraPanModifier);

        var turn = 0;
        if (IsDown(keyboard, GameplayInputMap.TurnLeftPrimary, GameplayInputMap.TurnLeftAlt))
        {
            turn = -1;
        }
        else if (IsDown(keyboard, GameplayInputMap.TurnRightPrimary, GameplayInputMap.TurnRightAlt))
        {
            turn = 1;
        }

        var move = 0;
        if (IsDown(keyboard, GameplayInputMap.MoveForwardPrimary, GameplayInputMap.MoveForwardAlt))
        {
            move = 1;
        }
        else if (IsDown(keyboard, GameplayInputMap.MoveBackwardPrimary, GameplayInputMap.MoveBackwardAlt))
        {
            move = -1;
        }

        var mouseLogical = presentation.ScreenToLogical(new Vector2(mouse.X, mouse.Y));
        var pointerOverUiPanel = mouseLogical.X >= worldViewportWidth;
        var pointerOverWorld = !pointerOverUiPanel
            && mouseLogical.X >= 0
            && mouseLogical.Y >= 0
            && mouseLogical.X < worldViewportWidth
            && mouseLogical.Y < UiLayout.LogicalHeight;

        var mouseScreen = new Vector2(
            Math.Clamp(mouseLogical.X, 0, worldViewportWidth - 1),
            Math.Clamp(mouseLogical.Y, 0, DisplaySettings.LogicalHeight - 1));

        var mouseWorld = camera.ScreenToWorld(mouseScreen);
        var aimDelta = mouseWorld - playerWorldCenter;

        var mouseLeftHeld = mouse.LeftButton == ButtonState.Pressed;
        var mouseLeftClicked = mouseLeftHeld && _previousMouse.LeftButton == ButtonState.Released;
        var mouseRightClicked = mouse.RightButton == ButtonState.Pressed
            && _previousMouse.RightButton == ButtonState.Released;

        var gameplay = new GameplayInputState
        {
            Turn = turn,
            Move = move,
            AimDeltaX = aimDelta.X,
            AimDeltaY = aimDelta.Y,
            FireHeld = keyboard.IsKeyDown(GameplayInputMap.FirePrimary)
                || keyboard.IsKeyDown(GameplayInputMap.FireAlt),
            FireFlareHeld = keyboard.IsKeyDown(GameplayInputMap.FireFlarePrimary)
                || keyboard.IsKeyDown(GameplayInputMap.FireFlareAlt),
            UseCloakPressed = WasPressed(keyboard, GameplayInputMap.UseCloak),
            DropSelectedItemPressed = WasPressed(keyboard, GameplayInputMap.DropSelectedItem),
            CycleInventoryPreviousPressed = WasPressed(keyboard, GameplayInputMap.CycleInventoryPrevious),
            CycleInventoryNextPressed = WasPressed(keyboard, GameplayInputMap.CycleInventoryNext),
            UseMedKitPressed = WasPressed(keyboard, GameplayInputMap.UseMedKit),
            DropBombPressed = WasPressed(keyboard, GameplayInputMap.DropBomb),
            DropOrbPressed = WasPressed(keyboard, GameplayInputMap.DropOrb),
            PickUpItemPressed = WasPressed(keyboard, GameplayInputMap.PickUpItem),
            CameraPanModifierHeld = cameraPan,
        };

        var ui = new UiInputState
        {
            ToggleMiniMapPressed = keyboard.IsKeyDown(UiInputMap.ToggleMiniMap)
                && !_previousKeyboard.IsKeyDown(UiInputMap.ToggleMiniMap),
            ToggleStatusPanelPressed = keyboard.IsKeyDown(UiInputMap.ToggleStatusPanel)
                && !_previousKeyboard.IsKeyDown(UiInputMap.ToggleStatusPanel),
            ToggleSettingsPressed = keyboard.IsKeyDown(UiInputMap.ToggleSettings)
                && !_previousKeyboard.IsKeyDown(UiInputMap.ToggleSettings),
            ZoomSteps = Math.Sign(mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue),
            CameraPanLeft = cameraPan && keyboard.IsKeyDown(UiInputMap.CameraPanLeft),
            CameraPanRight = cameraPan && keyboard.IsKeyDown(UiInputMap.CameraPanRight),
            CameraPanUp = cameraPan && keyboard.IsKeyDown(UiInputMap.CameraPanUp),
            CameraPanDown = cameraPan && keyboard.IsKeyDown(UiInputMap.CameraPanDown),
            MouseLogicalPosition = mouseLogical,
            MouseLeftClicked = mouseLeftClicked,
            MouseLeftHeld = mouseLeftHeld,
            MouseRightClicked = mouseRightClicked,
            PointerOverUiPanel = pointerOverUiPanel,
            PointerOverWorld = pointerOverWorld,
        };

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        return new FrameInput(gameplay, ui);
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    private static bool IsDown(KeyboardState keyboard, Keys primary, Keys alternate) =>
        keyboard.IsKeyDown(primary) || keyboard.IsKeyDown(alternate);
}
