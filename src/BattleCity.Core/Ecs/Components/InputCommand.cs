namespace BattleCity.Core.Ecs.Components;

/// <summary>
/// Per-frame input sampled on the client and applied by <see cref="Systems.InputSystem"/>.
/// Serializable shape for future multiplayer command packets.
/// </summary>
public struct InputCommand
{
    /// <summary>Turn left (-1), none (0), or right (1). Legacy arrow keys.</summary>
    public int Turn;

    /// <summary>Reverse (-1), stop (0), or forward (1). Legacy up/down arrows.</summary>
    public int Move;

    /// <summary>World-space offset from tank center to aim point (mouse).</summary>
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
}
