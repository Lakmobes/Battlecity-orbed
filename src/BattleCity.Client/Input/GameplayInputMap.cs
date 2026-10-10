using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Input;

/// <summary>Keyboard bindings. Defaults match legacy/client/CInput.cpp plus the WASD aliases. Players can change them in Settings → Controls.</summary>
public static class GameplayInputMap
{
    public static Keys TurnLeftPrimary => KeyBindings.Current.Primary(GameAction.TurnLeft);
    public static Keys TurnRightPrimary => KeyBindings.Current.Primary(GameAction.TurnRight);
    public static Keys MoveForwardPrimary => KeyBindings.Current.Primary(GameAction.MoveForward);
    public static Keys MoveBackwardPrimary => KeyBindings.Current.Primary(GameAction.MoveBackward);

    public static Keys TurnLeftAlt => KeyBindings.Current.Alt(GameAction.TurnLeft);
    public static Keys TurnRightAlt => KeyBindings.Current.Alt(GameAction.TurnRight);
    public static Keys MoveForwardAlt => KeyBindings.Current.Alt(GameAction.MoveForward);
    public static Keys MoveBackwardAlt => KeyBindings.Current.Alt(GameAction.MoveBackward);

    public static Keys FirePrimary => KeyBindings.Current.Primary(GameAction.Fire);
    public static Keys FireAlt => KeyBindings.Current.Alt(GameAction.Fire);
    public static Keys FireFlarePrimary => KeyBindings.Current.Primary(GameAction.FireFlare);
    public static Keys FireFlareAlt => KeyBindings.Current.Alt(GameAction.FireFlare);

    public static Keys UseCloak => KeyBindings.Current.Primary(GameAction.UseCloak);
    public static Keys DropSelectedItem => KeyBindings.Current.Primary(GameAction.DropItem);

    public static Keys CycleInventoryPrevious => KeyBindings.Current.Primary(GameAction.InventoryPrevious);
    public static Keys CycleInventoryNext => KeyBindings.Current.Primary(GameAction.InventoryNext);
    public static Keys UseMedKit => KeyBindings.Current.Primary(GameAction.UseMedKit);
    public static Keys DropBomb => KeyBindings.Current.Primary(GameAction.DropBomb);
    public static Keys DropOrb => KeyBindings.Current.Primary(GameAction.DropOrb);
    public static Keys PickUpItem => KeyBindings.Current.Primary(GameAction.PickUp);

    public static Keys CameraPanModifier => KeyBindings.Current.Primary(GameAction.CameraPan);
}
