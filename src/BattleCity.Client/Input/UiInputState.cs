using Microsoft.Xna.Framework;

namespace BattleCity.Client.Input;

public struct UiInputState
{
    public bool ToggleMiniMapPressed;
    public int ZoomSteps;
    public bool CameraPanLeft;
    public bool CameraPanRight;
    public bool CameraPanUp;
    public bool CameraPanDown;
    public Vector2 MouseLogicalPosition;
    public bool MouseLeftClicked;
    public bool MouseLeftHeld;
    public bool MouseRightClicked;
    public bool PointerOverUiPanel;
    public bool PointerOverWorld;
}
