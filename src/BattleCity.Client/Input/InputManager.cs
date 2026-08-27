using BattleCity.Client.Rendering;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Input;

public readonly record struct FrameInput(GameplayInputState Gameplay, UiInputState Ui);

public sealed class InputManager
{
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private GamePadState _previousGamePad;
    private Vector2 _virtualCursorLogical;
    private bool _virtualCursorInitialized;
    private bool _pointerMode;

    public FrameInput Poll(
        Camera2D camera,
        Vector2 playerWorldCenter,
        int worldViewportWidth,
        DisplayPresentation presentation,
        float deltaSeconds = 1f / 60f)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        var gamePad = GamePad.GetState(GameplayGamepadMap.Player);
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
        EnsureVirtualCursor(mouseLogical);

        if (mouse.X != _previousMouse.X || mouse.Y != _previousMouse.Y)
        {
            _pointerMode = false;
        }

        var modifierHeld = gamePad.IsConnected && gamePad.IsButtonDown(GameplayGamepadMap.ItemModifier);
        if (GamepadUiMapper.WasPressed(gamePad, _previousGamePad, GameplayGamepadMap.TogglePointerMode))
        {
            _pointerMode = !_pointerMode;
            if (_pointerMode)
            {
                _virtualCursorLogical = mouseLogical;
            }
        }

        if (_pointerMode && gamePad.IsConnected)
        {
            _virtualCursorLogical = GamepadUiMapper.MovePointer(
                _virtualCursorLogical,
                gamePad.ThumbSticks.Right,
                deltaSeconds,
                DisplaySettings.LogicalWidth,
                DisplaySettings.LogicalHeight);
        }

        var pointerLogical = _pointerMode && gamePad.IsConnected ? _virtualCursorLogical : mouseLogical;
        var pointerOverUiPanel = pointerLogical.X >= worldViewportWidth;
        var pointerOverWorld = !pointerOverUiPanel
            && pointerLogical.X >= 0
            && pointerLogical.Y >= 0
            && pointerLogical.X < worldViewportWidth
            && pointerLogical.Y < UiLayout.LogicalHeight;

        var mouseScreen = new Vector2(
            Math.Clamp(pointerLogical.X, 0, worldViewportWidth - 1),
            Math.Clamp(pointerLogical.Y, 0, DisplaySettings.LogicalHeight - 1));

        var mouseWorld = camera.ScreenToWorld(mouseScreen);
        var aimDelta = mouseWorld - playerWorldCenter;

        var mouseLeftHeld = mouse.LeftButton == ButtonState.Pressed;
        var mouseLeftClicked = mouseLeftHeld && _previousMouse.LeftButton == ButtonState.Released;
        var mouseRightClicked = mouse.RightButton == ButtonState.Pressed
            && _previousMouse.RightButton == ButtonState.Released;

        var pointerPrimaryHeld = _pointerMode
            && gamePad.IsConnected
            && !modifierHeld
            && gamePad.IsButtonDown(GameplayGamepadMap.PrimaryClick);
        var pointerPrimaryClicked = pointerPrimaryHeld
            && !_previousGamePad.IsButtonDown(GameplayGamepadMap.PrimaryClick);
        var pointerContextClicked = _pointerMode
            && gamePad.IsConnected
            && !modifierHeld
            && GamepadUiMapper.WasPressed(gamePad, _previousGamePad, GameplayGamepadMap.ContextClick);

        var keyboardGameplay = new GameplayInputState
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

        var gamepadGameplay = GamepadGameplayMapper.Sample(gamePad, _previousGamePad);
        if (_pointerMode)
        {
            // A/B are primary/context clicks while the virtual cursor is active.
            gamepadGameplay.DropSelectedItemPressed = false;
            gamepadGameplay.UseMedKitPressed = false;
        }

        var gameplay = GameplayInputMerger.Merge(keyboardGameplay, gamepadGameplay);

        var cameraPanLeft = cameraPan && keyboard.IsKeyDown(UiInputMap.CameraPanLeft);
        var cameraPanRight = cameraPan && keyboard.IsKeyDown(UiInputMap.CameraPanRight);
        var cameraPanUp = cameraPan && keyboard.IsKeyDown(UiInputMap.CameraPanUp);
        var cameraPanDown = cameraPan && keyboard.IsKeyDown(UiInputMap.CameraPanDown);
        if (!_pointerMode && gamePad.IsConnected)
        {
            GamepadUiMapper.ApplyCameraPanFromStick(
                gamePad.ThumbSticks.Right,
                ref cameraPanLeft,
                ref cameraPanRight,
                ref cameraPanUp,
                ref cameraPanDown);
        }

        var ui = new UiInputState
        {
            ToggleMiniMapPressed = (keyboard.IsKeyDown(UiInputMap.ToggleMiniMap)
                    && !_previousKeyboard.IsKeyDown(UiInputMap.ToggleMiniMap))
                || GamepadUiMapper.WasPressed(gamePad, _previousGamePad, GameplayGamepadMap.ToggleMiniMap),
            ToggleStatusPanelPressed = keyboard.IsKeyDown(UiInputMap.ToggleStatusPanel)
                && !_previousKeyboard.IsKeyDown(UiInputMap.ToggleStatusPanel),
            ToggleSettingsPressed = (keyboard.IsKeyDown(UiInputMap.ToggleSettings)
                    && !_previousKeyboard.IsKeyDown(UiInputMap.ToggleSettings))
                || GamepadUiMapper.WasPressed(gamePad, _previousGamePad, GameplayGamepadMap.ToggleSettings),
            ZoomSteps = Math.Sign(mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue),
            CameraPanLeft = cameraPanLeft,
            CameraPanRight = cameraPanRight,
            CameraPanUp = cameraPanUp,
            CameraPanDown = cameraPanDown,
            MouseLogicalPosition = pointerLogical,
            MouseLeftClicked = mouseLeftClicked || pointerPrimaryClicked,
            MouseLeftHeld = mouseLeftHeld || pointerPrimaryHeld,
            MouseRightClicked = mouseRightClicked || pointerContextClicked,
            PointerOverUiPanel = pointerOverUiPanel,
            PointerOverWorld = pointerOverWorld,
            ShowVirtualCursor = _pointerMode && gamePad.IsConnected,
        };

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        _previousGamePad = gamePad;
        return new FrameInput(gameplay, ui);
    }

    private void EnsureVirtualCursor(Vector2 mouseLogical)
    {
        if (_virtualCursorInitialized)
        {
            return;
        }

        _virtualCursorLogical = mouseLogical;
        _virtualCursorInitialized = true;
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    private static bool IsDown(KeyboardState keyboard, Keys primary, Keys alternate) =>
        keyboard.IsKeyDown(primary) || keyboard.IsKeyDown(alternate);
}
