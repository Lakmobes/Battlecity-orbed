namespace BattleCity.Client.Input;

/// <summary>Combines keyboard and gamepad gameplay samples for one frame.</summary>
public static class GameplayInputMerger
{
    public static GameplayInputState Merge(GameplayInputState keyboard, GameplayInputState gamepad) =>
        new()
        {
            Turn = Math.Clamp(keyboard.Turn + gamepad.Turn, -1, 1),
            Move = Math.Clamp(keyboard.Move + gamepad.Move, -1, 1),
            AimDeltaX = keyboard.AimDeltaX,
            AimDeltaY = keyboard.AimDeltaY,
            FireHeld = keyboard.FireHeld || gamepad.FireHeld,
            FireFlareHeld = keyboard.FireFlareHeld || gamepad.FireFlareHeld,
            UseCloakPressed = keyboard.UseCloakPressed || gamepad.UseCloakPressed,
            DropSelectedItemPressed = keyboard.DropSelectedItemPressed || gamepad.DropSelectedItemPressed,
            CycleInventoryPreviousPressed =
                keyboard.CycleInventoryPreviousPressed || gamepad.CycleInventoryPreviousPressed,
            CycleInventoryNextPressed =
                keyboard.CycleInventoryNextPressed || gamepad.CycleInventoryNextPressed,
            UseMedKitPressed = keyboard.UseMedKitPressed || gamepad.UseMedKitPressed,
            DropBombPressed = keyboard.DropBombPressed || gamepad.DropBombPressed,
            DropOrbPressed = keyboard.DropOrbPressed || gamepad.DropOrbPressed,
            PickUpItemPressed = keyboard.PickUpItemPressed || gamepad.PickUpItemPressed,
            CameraPanModifierHeld = keyboard.CameraPanModifierHeld || gamepad.CameraPanModifierHeld,
        };
}
