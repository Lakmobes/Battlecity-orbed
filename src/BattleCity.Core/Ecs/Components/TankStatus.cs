namespace BattleCity.Core.Ecs.Components;

/// <summary>Temporary cloak / DFG freeze state (legacy CPlayer isCloaked / isFrozen).</summary>
public struct TankStatus
{
    public bool IsCloaked;
    public float CloakRemainingSeconds;

    public bool IsFrozen;
    public float FrozenRemainingSeconds;
}
