using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Input;

/// <summary>Default bindings aligned with legacy/client/CInput.cpp plus WASD aliases.</summary>
public static class GameplayInputMap
{
    public static Keys TurnLeftPrimary => Keys.Left;
    public static Keys TurnRightPrimary => Keys.Right;
    public static Keys MoveForwardPrimary => Keys.Up;
    public static Keys MoveBackwardPrimary => Keys.Down;

    public static Keys TurnLeftAlt => Keys.A;
    public static Keys TurnRightAlt => Keys.E;
    public static Keys MoveForwardAlt => Keys.W;
    public static Keys MoveBackwardAlt => Keys.S;

    public static Keys FirePrimary => Keys.LeftShift;
    public static Keys FireAlt => Keys.RightShift;
    public static Keys FireFlarePrimary => Keys.LeftControl;
    public static Keys FireFlareAlt => Keys.RightControl;

    public static Keys UseCloak => Keys.C;
    /// <summary>Legacy used D to drop the selected inventory item (CGame.cpp).</summary>
    public static Keys DropSelectedItem => Keys.D;

    public static Keys CycleInventoryPrevious => Keys.OemOpenBrackets;
    public static Keys CycleInventoryNext => Keys.OemCloseBrackets;
    public static Keys UseMedKit => Keys.H;
    public static Keys DropBomb => Keys.B;
    public static Keys DropOrb => Keys.O;
    public static Keys PickUpItem => Keys.U;

    public static Keys CameraPanModifier => Keys.Tab;
}
