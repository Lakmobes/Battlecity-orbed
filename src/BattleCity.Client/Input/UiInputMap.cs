using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Input;

public static class UiInputMap
{
    public static Keys ToggleMiniMap => Keys.M;
    public static Keys ToggleStatusPanel => Keys.F1;
    public static Keys ToggleSettings => Keys.Escape;
    public static Keys CameraPanLeft => Keys.Left;
    public static Keys CameraPanRight => Keys.Right;
    public static Keys CameraPanUp => Keys.Up;
    public static Keys CameraPanDown => Keys.Down;
    public static Keys CameraPanModifier => Keys.Tab;
}
