using Arch.Core;

using BattleCity.Core.Ecs.Components;

namespace BattleCity.Client.Input;

public static class InputCommandWriter
{
    private static readonly QueryDescription PlayerQuery =
        new QueryDescription().WithAll<InputControlled, InputCommand>();

    public static void Apply(World world, GameplayInputState gameplay)
    {
        world.Query(
            in PlayerQuery,
            (ref InputCommand command) =>
            {
                command.Turn = gameplay.Turn;
                command.Move = gameplay.Move;
                command.AimDeltaX = gameplay.AimDeltaX;
                command.AimDeltaY = gameplay.AimDeltaY;
                command.FireHeld = gameplay.FireHeld;
                command.FireFlareHeld = gameplay.FireFlareHeld;
                command.UseCloakPressed = gameplay.UseCloakPressed;
                command.DropSelectedItemPressed = gameplay.DropSelectedItemPressed;
                command.CycleInventoryPreviousPressed = gameplay.CycleInventoryPreviousPressed;
                command.CycleInventoryNextPressed = gameplay.CycleInventoryNextPressed;
                command.UseMedKitPressed = gameplay.UseMedKitPressed;
                command.DropBombPressed = gameplay.DropBombPressed;
                command.DropOrbPressed = gameplay.DropOrbPressed;
                command.PickUpItemPressed = gameplay.PickUpItemPressed;
            });
    }
}
