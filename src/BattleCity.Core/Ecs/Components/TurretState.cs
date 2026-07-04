using Arch.Core;

namespace BattleCity.Core.Ecs.Components;

public struct TurretState
{
    public float AimAngleDegrees;
    public float TurnCooldownSeconds;
    public float StartupDelaySeconds;
    public Entity ActiveBullet;
    public int AnimationFrame;
    public float AnimationCooldownSeconds;
    public bool HasTarget;
    public Entity Target;
}
