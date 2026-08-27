using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Input;

/// <summary>
/// Xbox-layout gamepad bindings for gameplay and UI.
/// <list type="table">
/// <listheader><term>Action</term><description>Control</description></listheader>
/// <item><term>Turn / move</term><description>Left stick (X turn, Y drive) or D-pad</description></item>
/// <item><term>Fire laser/rocket</term><description>Right trigger (hold)</description></item>
/// <item><term>Fire flare</term><description>Left trigger (hold)</description></item>
/// <item><term>Cloak</term><description>Y</description></item>
/// <item><term>Drop selected item</term><description>A (combat mode)</description></item>
/// <item><term>Pick up item</term><description>X</description></item>
/// <item><term>Med kit</term><description>B (combat mode)</description></item>
/// <item><term>Cycle inventory</term><description>LB / RB</description></item>
/// <item><term>Drop bomb</term><description>View (Back) + A</description></item>
/// <item><term>Drop orb</term><description>View (Back) + X</description></item>
/// <item><term>Camera pan</term><description>Right stick (combat mode)</description></item>
/// <item><term>Settings</term><description>Menu (Start)</description></item>
/// <item><term>Minimap</term><description>Left stick click</description></item>
/// <item><term>Toggle pointer mode</term><description>Right stick click</description></item>
/// <item><term>Move pointer</term><description>Right stick (pointer mode)</description></item>
/// <item><term>Primary click</term><description>A (pointer mode)</description></item>
/// <item><term>Context / build menu</term><description>B (pointer mode)</description></item>
/// <item><term>Menu navigate</term><description>D-pad / left stick</description></item>
/// <item><term>Menu confirm / cancel</term><description>A / B</description></item>
/// <item><term>Hotplug</term><description>Connect/disconnect clears pointer mode; no phantom button edges</description></item>
/// </list>
/// </summary>
public static class GameplayGamepadMap
{
    public static PlayerIndex Player => PlayerIndex.One;

    /// <summary>Circular deadzone for sticks before axes are read.</summary>
    public const float StickDeadzone = 0.25f;

    /// <summary>Trigger axis value treated as held fire / flare.</summary>
    public const float TriggerThreshold = 0.5f;

    /// <summary>Logical pixels per second for the virtual cursor in pointer mode.</summary>
    public const float PointerSpeedPixelsPerSecond = 520f;

    public static Buttons UseCloak => Buttons.Y;
    public static Buttons DropSelectedItem => Buttons.A;
    public static Buttons PickUpItem => Buttons.X;
    public static Buttons UseMedKit => Buttons.B;
    public static Buttons CycleInventoryPrevious => Buttons.LeftShoulder;
    public static Buttons CycleInventoryNext => Buttons.RightShoulder;

    /// <summary>Xbox View / Back — held as a modifier for bomb/orb.</summary>
    public static Buttons ItemModifier => Buttons.Back;

    public static Buttons DropBomb => Buttons.A;
    public static Buttons DropOrb => Buttons.X;

    public static Buttons ToggleSettings => Buttons.Start;
    public static Buttons ToggleMiniMap => Buttons.LeftStick;
    public static Buttons TogglePointerMode => Buttons.RightStick;

    public static Buttons PrimaryClick => Buttons.A;
    public static Buttons ContextClick => Buttons.B;

    public static Buttons MenuConfirm => Buttons.A;
    public static Buttons MenuCancel => Buttons.B;
}
