using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Components;

public struct ExplosionRef
{
    public ExplosionKind Kind;
    public int AnimationFrame;
    public float FrameTimerSeconds;
}
