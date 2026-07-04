namespace BattleCity.Client.Input;

public struct GameplayInputState
{
    public int Turn;
    public int Move;
    public float AimDeltaX;
    public float AimDeltaY;
    public bool FireHeld;
    public bool FireFlareHeld;
    public bool UseCloakPressed;
    public bool DropSelectedItemPressed;
    public bool CycleInventoryPreviousPressed;
    public bool CycleInventoryNextPressed;
    public bool UseMedKitPressed;
    public bool DropBombPressed;
    public bool DropOrbPressed;
    public bool PickUpItemPressed;
    public bool CameraPanModifierHeld;
}
