using System.Numerics;

namespace BattleCity.Core.Ecs.Components;

public struct TankLifeState
{
    public bool IsDead;
    public float RespawnTimerSeconds;
    public Vector2 SpawnPosition;
    public float HospitalHealCooldownSeconds;

    /// <summary>Attacker city from the last killing blow; <see cref="EntityCityLookup.UnknownCity"/> if unknown.</summary>
    public byte KillerCityId;
}
