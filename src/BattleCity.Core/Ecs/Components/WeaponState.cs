namespace BattleCity.Core.Ecs.Components;

public struct WeaponState
{
    public float LaserCooldownSeconds;
    public float RocketCooldownSeconds;
    public float FlareCooldownSeconds;

    /// <summary>Countdown after using city-unlocked cloak; 0 = ready.</summary>
    public float CloakRechargeSeconds;

    /// <summary>Countdown after using city-unlocked flare; 0 = ready.</summary>
    public float FlareRechargeSeconds;
}
